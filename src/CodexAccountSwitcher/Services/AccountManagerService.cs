using System.Security.Cryptography;
using CodexAccountSwitcher.Models;

namespace CodexAccountSwitcher.Services;

public sealed class AccountManagerService : IAccountManagerService
{
    private readonly IAccountStoreService _accountStore;
    private readonly ICodexCliService _codexCli;

    public AccountManagerService(IAccountStoreService accountStore, ICodexCliService codexCli)
    {
        _accountStore = accountStore;
        _codexCli = codexCli;
    }

    public async Task<IReadOnlyList<CodexAccount>> GetAccountsAsync(CancellationToken cancellationToken = default)
    {
        var accounts = (await _accountStore.LoadAccountsAsync(cancellationToken)).ToList();
        await ApplyActiveStateAsync(accounts, cancellationToken);
        return accounts;
    }

    public async Task<CodexAccount> ImportCurrentAccountAsync(
        string displayName,
        CancellationToken cancellationToken = default)
    {
        ValidateDisplayName(displayName);

        var authPath = GetAuthPath();
        if (!File.Exists(authPath))
        {
            throw new InvalidOperationException(
                "No file-based Codex auth state was found. Sign in to Codex first.");
        }

        var authBytes = await File.ReadAllBytesAsync(authPath, cancellationToken);
        var accounts = (await _accountStore.LoadAccountsAsync(cancellationToken)).ToList();

        var currentHash = ComputeHash(authBytes);
        var existing = await FindMatchingAccountAsync(accounts, currentHash, cancellationToken);

        if (existing is not null)
        {
            existing.DisplayName = displayName.Trim();
            existing.LastUsedAt = DateTimeOffset.UtcNow;
            await _accountStore.SaveCredentialsAsync(existing.Id, authBytes, cancellationToken);
            await _accountStore.SaveAccountsAsync(accounts, cancellationToken);
            await ApplyActiveStateAsync(accounts, cancellationToken);
            return existing;
        }

        var account = new CodexAccount
        {
            Id = Guid.NewGuid(),
            DisplayName = displayName.Trim(),
            CreatedAt = DateTimeOffset.UtcNow,
            LastUsedAt = DateTimeOffset.UtcNow
        };

        account.CredentialFileName = $"{account.Id:N}.bin";

        await _accountStore.SaveCredentialsAsync(account.Id, authBytes, cancellationToken);
        accounts.Add(account);
        await _accountStore.SaveAccountsAsync(accounts, cancellationToken);
        await ApplyActiveStateAsync(accounts, cancellationToken);

        return account;
    }

    public async Task<CodexAccount> AddAccountViaLoginAsync(
        string displayName,
        CancellationToken cancellationToken = default)
    {
        ValidateDisplayName(displayName);

        var authPath = GetAuthPath();
        var accounts = (await _accountStore.LoadAccountsAsync(cancellationToken)).ToList();
        byte[]? previousAuth = null;

        if (File.Exists(authPath))
        {
            previousAuth = await File.ReadAllBytesAsync(authPath, cancellationToken);
            var previousHash = ComputeHash(previousAuth);
            var previousAccount = await FindMatchingAccountAsync(
                accounts,
                previousHash,
                cancellationToken);

            if (previousAccount is null)
            {
                throw new InvalidOperationException(
                    "The currently active Codex login has not been imported yet. Import the current account before adding another.");
            }

            previousAccount.LastUsedAt = DateTimeOffset.UtcNow;
            await _accountStore.SaveCredentialsAsync(previousAccount.Id, previousAuth, cancellationToken);
            await _accountStore.SaveAccountsAsync(accounts, cancellationToken);
        }

        var executable = await _codexCli.FindCodexExecutableAsync(cancellationToken)
                         ?? throw new InvalidOperationException(
                             "Codex CLI could not be found in PATH.");

        var stagingPath = authPath + ".account-switcher-staging";

        try
        {
            if (File.Exists(stagingPath))
            {
                File.Delete(stagingPath);
            }

            if (File.Exists(authPath))
            {
                File.Move(authPath, stagingPath);
            }

            var exitCode = await _codexCli.RunInteractiveLoginAsync(executable, cancellationToken);

            if (exitCode != 0 || !File.Exists(authPath))
            {
                throw new InvalidOperationException(
                    $"Codex login did not complete successfully (exit code {exitCode}).");
            }

            var newAuth = await File.ReadAllBytesAsync(authPath, cancellationToken);
            var newHash = ComputeHash(newAuth);
            var existing = await FindMatchingAccountAsync(accounts, newHash, cancellationToken);

            CodexAccount result;

            if (existing is not null)
            {
                existing.DisplayName = displayName.Trim();
                existing.LastUsedAt = DateTimeOffset.UtcNow;
                await _accountStore.SaveCredentialsAsync(existing.Id, newAuth, cancellationToken);
                result = existing;
            }
            else
            {
                result = new CodexAccount
                {
                    Id = Guid.NewGuid(),
                    DisplayName = displayName.Trim(),
                    CreatedAt = DateTimeOffset.UtcNow,
                    LastUsedAt = DateTimeOffset.UtcNow
                };

                result.CredentialFileName = $"{result.Id:N}.bin";

                await _accountStore.SaveCredentialsAsync(result.Id, newAuth, cancellationToken);
                accounts.Add(result);
            }

            await _accountStore.SaveAccountsAsync(accounts, cancellationToken);

            if (File.Exists(stagingPath))
            {
                File.Delete(stagingPath);
            }

            await ApplyActiveStateAsync(accounts, cancellationToken);
            return result;
        }
        catch
        {
            if (File.Exists(authPath))
            {
                File.Delete(authPath);
            }

            if (File.Exists(stagingPath))
            {
                File.Move(stagingPath, authPath);
            }
            else if (previousAuth is not null)
            {
                await AtomicWriteAsync(authPath, previousAuth, cancellationToken);
            }

            throw;
        }
    }

    public async Task ActivateAccountAsync(
        Guid accountId,
        CancellationToken cancellationToken = default)
    {
        var accounts = (await _accountStore.LoadAccountsAsync(cancellationToken)).ToList();
        var target = accounts.SingleOrDefault(account => account.Id == accountId)
                     ?? throw new InvalidOperationException("Account was not found.");

        var authPath = GetAuthPath();
        byte[]? currentAuth = null;

        if (File.Exists(authPath))
        {
            currentAuth = await File.ReadAllBytesAsync(authPath, cancellationToken);
            var currentHash = ComputeHash(currentAuth);
            var currentAccount = await FindMatchingAccountAsync(accounts, currentHash, cancellationToken);

            if (currentAccount is null)
            {
                throw new InvalidOperationException(
                    "The active Codex login is unknown. Import it before switching accounts.");
            }

            currentAccount.LastUsedAt = DateTimeOffset.UtcNow;
            await _accountStore.SaveCredentialsAsync(currentAccount.Id, currentAuth, cancellationToken);
        }

        var targetAuth = await _accountStore.LoadCredentialsAsync(target.Id, cancellationToken);
        var rollbackPath = authPath + ".account-switcher-rollback";

        try
        {
            if (File.Exists(rollbackPath))
            {
                File.Delete(rollbackPath);
            }

            if (File.Exists(authPath))
            {
                File.Copy(authPath, rollbackPath, overwrite: true);
            }

            await AtomicWriteAsync(authPath, targetAuth, cancellationToken);

            var written = await File.ReadAllBytesAsync(authPath, cancellationToken);
            if (!CryptographicOperations.FixedTimeEquals(ComputeHash(written), ComputeHash(targetAuth)))
            {
                throw new InvalidOperationException(
                    "The activated Codex auth state failed verification.");
            }

            target.LastUsedAt = DateTimeOffset.UtcNow;
            await _accountStore.SaveAccountsAsync(accounts, cancellationToken);

            if (File.Exists(rollbackPath))
            {
                File.Delete(rollbackPath);
            }
        }
        catch
        {
            if (File.Exists(rollbackPath))
            {
                File.Copy(rollbackPath, authPath, overwrite: true);
                File.Delete(rollbackPath);
            }
            else if (currentAuth is not null)
            {
                await AtomicWriteAsync(authPath, currentAuth, cancellationToken);
            }

            throw;
        }
    }

    private async Task ApplyActiveStateAsync(
        List<CodexAccount> accounts,
        CancellationToken cancellationToken)
    {
        foreach (var account in accounts)
        {
            account.IsActive = false;
        }

        var authPath = GetAuthPath();
        if (!File.Exists(authPath))
        {
            return;
        }

        var currentHash = ComputeHash(await File.ReadAllBytesAsync(authPath, cancellationToken));
        var active = await FindMatchingAccountAsync(accounts, currentHash, cancellationToken);

        if (active is not null)
        {
            active.IsActive = true;
        }
    }

    private async Task<CodexAccount?> FindMatchingAccountAsync(
        IEnumerable<CodexAccount> accounts,
        byte[] authHash,
        CancellationToken cancellationToken)
    {
        foreach (var account in accounts)
        {
            try
            {
                var stored = await _accountStore.LoadCredentialsAsync(account.Id, cancellationToken);
                var storedHash = ComputeHash(stored);

                if (CryptographicOperations.FixedTimeEquals(authHash, storedHash))
                {
                    return account;
                }
            }
            catch (FileNotFoundException)
            {
            }
            catch (CryptographicException)
            {
            }
        }

        return null;
    }

    private static string GetAuthPath()
    {
        var explicitHome = Environment.GetEnvironmentVariable("CODEX_HOME");
        var codexHome = string.IsNullOrWhiteSpace(explicitHome)
            ? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                ".codex")
            : Environment.ExpandEnvironmentVariables(explicitHome);

        return Path.Combine(codexHome, "auth.json");
    }

    private static byte[] ComputeHash(byte[] bytes) => SHA256.HashData(bytes);

    private static void ValidateDisplayName(string displayName)
    {
        if (string.IsNullOrWhiteSpace(displayName))
        {
            throw new ArgumentException("Enter an account name first.", nameof(displayName));
        }
    }

    private static async Task AtomicWriteAsync(
        string destination,
        byte[] bytes,
        CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);

        var tempPath = destination + ".account-switcher-tmp";
        await File.WriteAllBytesAsync(tempPath, bytes, cancellationToken);

        if (File.Exists(destination))
        {
            var backupPath = destination + ".account-switcher-write-backup";
            File.Replace(tempPath, destination, backupPath, ignoreMetadataErrors: true);

            if (File.Exists(backupPath))
            {
                File.Delete(backupPath);
            }
        }
        else
        {
            File.Move(tempPath, destination);
        }
    }
}
