using CodexAccountSwitcher.Models;

namespace CodexAccountSwitcher.Services;

public sealed class AccountManagerService : IAccountManagerService
{
    private readonly IAccountStoreService _accountStore;
    private readonly ICodexCliService _codexCli;
    private readonly ICodexAuthParser _authParser;

    public AccountManagerService(
        IAccountStoreService accountStore,
        ICodexCliService codexCli,
        ICodexAuthParser authParser)
    {
        _accountStore = accountStore;
        _codexCli = codexCli;
        _authParser = authParser;
    }

    public async Task<IReadOnlyList<CodexAccount>> GetAccountsAsync(
        CancellationToken cancellationToken = default)
    {
        var accounts = (await _accountStore.LoadAccountsAsync(cancellationToken)).ToList();
        var metadataChanged = await MigrateMetadataAsync(accounts, cancellationToken);
        metadataChanged |= await ApplyActiveStateAsync(accounts, cancellationToken);

        if (metadataChanged)
        {
            await _accountStore.SaveAccountsAsync(accounts, cancellationToken);
        }

        return accounts;
    }

    public async Task RenameAccountAsync(
        Guid accountId,
        string displayName,
        CancellationToken cancellationToken = default)
    {
        ValidateDisplayName(displayName);

        var accounts = (await _accountStore.LoadAccountsAsync(cancellationToken)).ToList();
        var account = accounts.SingleOrDefault(candidate => candidate.Id == accountId)
                      ?? throw new InvalidOperationException("Account was not found.");

        account.DisplayName = displayName.Trim();
        await _accountStore.SaveAccountsAsync(accounts, cancellationToken);
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
                "No file-based Codex login was found. Sign in to Codex first.");
        }

        var authState = await File.ReadAllBytesAsync(authPath, cancellationToken);
        var authInfo = _authParser.Parse(authState);
        var accounts = (await _accountStore.LoadAccountsAsync(cancellationToken)).ToList();

        var existing = await FindMatchingAccountAsync(accounts, authInfo, cancellationToken);

        if (existing is null)
        {
            existing = new CodexAccount
            {
                Id = Guid.NewGuid(),
                DisplayName = displayName.Trim(),
                CreatedAt = DateTimeOffset.UtcNow,
                LastUsedAt = DateTimeOffset.UtcNow
            };

            existing.CredentialFileName = $"{existing.Id:N}.bin";
            accounts.Add(existing);
        }
        else
        {
            existing.DisplayName = displayName.Trim();
            existing.LastUsedAt = DateTimeOffset.UtcNow;
        }

        _authParser.ApplyIdentity(existing, authInfo);

        await _accountStore.SaveCredentialsAsync(
            existing.Id,
            authState,
            cancellationToken);

        await _accountStore.SaveAccountsAsync(accounts, cancellationToken);
        await ApplyActiveStateAsync(accounts, cancellationToken);

        return existing;
    }

    public async Task<CodexAccount> AddAccountViaLoginAsync(
        string displayName,
        CancellationToken cancellationToken = default)
    {
        ValidateDisplayName(displayName);

        var accounts = (await _accountStore.LoadAccountsAsync(cancellationToken)).ToList();
        var activePath = GetAuthPath();

        if (File.Exists(activePath))
        {
            var activeState = await File.ReadAllBytesAsync(activePath, cancellationToken);
            var activeInfo = _authParser.Parse(activeState);
            var activeAccount = await FindMatchingAccountAsync(
                accounts,
                activeInfo,
                cancellationToken);

            if (activeAccount is null)
            {
                throw new InvalidOperationException(
                    "Import the currently active Codex account before adding another account.");
            }

            _authParser.ApplyIdentity(activeAccount, activeInfo);
            activeAccount.LastUsedAt = DateTimeOffset.UtcNow;

            await _accountStore.SaveCredentialsAsync(
                activeAccount.Id,
                activeState,
                cancellationToken);

            await _accountStore.SaveAccountsAsync(accounts, cancellationToken);
        }

        var executable = await _codexCli.FindCodexExecutableAsync(cancellationToken)
                         ?? throw new InvalidOperationException(
                             "Codex CLI could not be found. Make sure 'codex' is available in PATH.");

        var loginHome = Path.Combine(
            Path.GetTempPath(),
            "CodexAccountSwitcher",
            $"login-{Guid.NewGuid():N}");

        try
        {
            Directory.CreateDirectory(loginHome);

            var exitCode = await _codexCli.RunInteractiveLoginAsync(
                executable,
                loginHome,
                cancellationToken);

            var loginAuthPath = Path.Combine(loginHome, "auth.json");

            if (exitCode != 0 || !File.Exists(loginAuthPath))
            {
                throw new InvalidOperationException(
                    $"Codex login did not complete successfully (exit code {exitCode}).");
            }

            var newAuthState = await File.ReadAllBytesAsync(
                loginAuthPath,
                cancellationToken);

            var newAuthInfo = _authParser.Parse(newAuthState);
            var existing = await FindMatchingAccountAsync(
                accounts,
                newAuthInfo,
                cancellationToken);

            CodexAccount account;

            if (existing is null)
            {
                account = new CodexAccount
                {
                    Id = Guid.NewGuid(),
                    DisplayName = displayName.Trim(),
                    CreatedAt = DateTimeOffset.UtcNow
                };

                account.CredentialFileName = $"{account.Id:N}.bin";
                accounts.Add(account);
            }
            else
            {
                account = existing;
                account.DisplayName = displayName.Trim();
            }

            _authParser.ApplyIdentity(account, newAuthInfo);

            await _accountStore.SaveCredentialsAsync(
                account.Id,
                newAuthState,
                cancellationToken);

            await _accountStore.SaveAccountsAsync(accounts, cancellationToken);

            return account;
        }
        finally
        {
            try
            {
                if (Directory.Exists(loginHome))
                {
                    Directory.Delete(loginHome, recursive: true);
                }
            }
            catch
            {
                // The temporary login home contains only disposable registration state.
            }
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
        byte[]? currentState = null;

        if (File.Exists(authPath))
        {
            currentState = await File.ReadAllBytesAsync(authPath, cancellationToken);
            var currentInfo = _authParser.Parse(currentState);
            var currentAccount = await FindMatchingAccountAsync(
                accounts,
                currentInfo,
                cancellationToken);

            if (currentAccount is null)
            {
                throw new InvalidOperationException(
                    "The active Codex login is not registered. Import it before switching.");
            }

            _authParser.ApplyIdentity(currentAccount, currentInfo);
            currentAccount.LastUsedAt = DateTimeOffset.UtcNow;

            await _accountStore.SaveCredentialsAsync(
                currentAccount.Id,
                currentState,
                cancellationToken);
        }

        var targetState = await _accountStore.LoadCredentialsAsync(
            target.Id,
            cancellationToken);

        var targetInfo = _authParser.Parse(targetState);

        if (!_authParser.Matches(target, targetInfo))
        {
            throw new InvalidOperationException(
                "Stored credentials no longer match the selected account.");
        }

        _authParser.ApplyIdentity(target, targetInfo);

        try
        {
            await AtomicWriteAsync(authPath, targetState, cancellationToken);

            var writtenState = await File.ReadAllBytesAsync(authPath, cancellationToken);
            var writtenInfo = _authParser.Parse(writtenState);

            if (!_authParser.Matches(target, writtenInfo))
            {
                throw new InvalidOperationException(
                    "The activated Codex login failed identity verification.");
            }

            target.LastUsedAt = DateTimeOffset.UtcNow;
            await _accountStore.SaveAccountsAsync(accounts, cancellationToken);

        }
        catch
        {
            if (currentState is not null)
            {
                await AtomicWriteAsync(authPath, currentState, CancellationToken.None);
            }
            else if (File.Exists(authPath))
            {
                File.Delete(authPath);
            }

            throw;
        }
    }

    private async Task<bool> ApplyActiveStateAsync(
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
            return false;
        }

        byte[] currentState;
        CodexAuthInfo currentInfo;

        try
        {
            currentState = await File.ReadAllBytesAsync(authPath, cancellationToken);
            currentInfo = _authParser.Parse(currentState);
        }
        catch
        {
            return false;
        }

        var active = await FindMatchingAccountAsync(
            accounts,
            currentInfo,
            cancellationToken);

        if (active is null)
        {
            return false;
        }

        active.IsActive = true;
        _authParser.ApplyIdentity(active, currentInfo);

        // Capture token rotations Codex performed while this account was active.
        await _accountStore.SaveCredentialsAsync(
            active.Id,
            currentState,
            cancellationToken);

        return true;
    }

    private async Task<bool> MigrateMetadataAsync(
        List<CodexAccount> accounts,
        CancellationToken cancellationToken)
    {
        var changed = false;

        foreach (var account in accounts)
        {
            if (!string.IsNullOrWhiteSpace(account.ChatGptUserId) ||
                !string.IsNullOrWhiteSpace(account.Email))
            {
                continue;
            }

            try
            {
                var state = await _accountStore.LoadCredentialsAsync(
                    account.Id,
                    cancellationToken);

                var info = _authParser.Parse(state);
                _authParser.ApplyIdentity(account, info);
                changed = true;
            }
            catch
            {
                // Old/incomplete metadata should not block the other accounts.
            }
        }

        return changed;
    }

    private async Task<CodexAccount?> FindMatchingAccountAsync(
        IEnumerable<CodexAccount> accounts,
        CodexAuthInfo authInfo,
        CancellationToken cancellationToken)
    {
        foreach (var account in accounts)
        {
            if (_authParser.Matches(account, authInfo))
            {
                return account;
            }
        }

        // Migration fallback for accounts created by the early hash-based MVP.
        foreach (var account in accounts)
        {
            try
            {
                var storedState = await _accountStore.LoadCredentialsAsync(
                    account.Id,
                    cancellationToken);

                var storedInfo = _authParser.Parse(storedState);

                if (string.Equals(
                        storedInfo.StableIdentity,
                        authInfo.StableIdentity,
                        StringComparison.Ordinal))
                {
                    _authParser.ApplyIdentity(account, storedInfo);
                    return account;
                }
            }
            catch
            {
                // Ignore unreadable legacy entries here.
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

    private static void ValidateDisplayName(string displayName)
    {
        if (string.IsNullOrWhiteSpace(displayName))
        {
            throw new ArgumentException(
                "Enter an account name first.",
                nameof(displayName));
        }
    }

    private static async Task AtomicWriteAsync(
        string destination,
        byte[] bytes,
        CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);

        var tempPath = destination + ".account-switcher-tmp";
        try
        {
            await File.WriteAllBytesAsync(tempPath, bytes, cancellationToken);

            if (File.Exists(destination))
            {
                File.Replace(tempPath, destination, null, ignoreMetadataErrors: true);
            }
            else
            {
                File.Move(tempPath, destination);
            }
        }
        finally
        {
            if (File.Exists(tempPath))
            {
                File.Delete(tempPath);
            }
        }
    }
}
