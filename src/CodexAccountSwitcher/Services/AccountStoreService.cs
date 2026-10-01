using System.Security.Cryptography;
using System.Text.Json;
using CodexAccountSwitcher.Models;

namespace CodexAccountSwitcher.Services;

public sealed class AccountStoreService : IAccountStoreService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    private readonly string _rootDirectory;
    private readonly string _accountsFile;
    private readonly string _credentialsDirectory;

    public AccountStoreService()
    {
        _rootDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "CodexAccountSwitcher");

        _accountsFile = Path.Combine(_rootDirectory, "accounts.json");
        _credentialsDirectory = Path.Combine(_rootDirectory, "credentials");
    }

    public async Task<IReadOnlyList<CodexAccount>> LoadAccountsAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(_accountsFile))
        {
            return Array.Empty<CodexAccount>();
        }

        await using var stream = File.OpenRead(_accountsFile);
        var accounts = await JsonSerializer.DeserializeAsync<List<CodexAccount>>(
            stream,
            JsonOptions,
            cancellationToken);

        return accounts ?? Array.Empty<CodexAccount>();
    }

    public async Task SaveAccountsAsync(
        IEnumerable<CodexAccount> accounts,
        CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(_rootDirectory);

        var tempPath = _accountsFile + ".tmp";

        await using (var stream = File.Create(tempPath))
        {
            await JsonSerializer.SerializeAsync(
                stream,
                accounts,
                JsonOptions,
                cancellationToken);
        }

        ReplaceFile(tempPath, _accountsFile);
    }

    public async Task SaveCredentialsAsync(
        Guid accountId,
        byte[] authState,
        CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(_credentialsDirectory);

        var protectedBytes = ProtectedData.Protect(
            authState,
            optionalEntropy: null,
            scope: DataProtectionScope.CurrentUser);

        var destination = GetCredentialPath(accountId);
        var tempPath = destination + ".tmp";

        await File.WriteAllBytesAsync(tempPath, protectedBytes, cancellationToken);
        ReplaceFile(tempPath, destination);
    }

    public async Task<byte[]> LoadCredentialsAsync(
        Guid accountId,
        CancellationToken cancellationToken = default)
    {
        var encrypted = await File.ReadAllBytesAsync(
            GetCredentialPath(accountId),
            cancellationToken);

        return ProtectedData.Unprotect(
            encrypted,
            optionalEntropy: null,
            scope: DataProtectionScope.CurrentUser);
    }

    private string GetCredentialPath(Guid accountId)
    {
        return Path.Combine(_credentialsDirectory, $"{accountId:N}.bin");
    }

    private static void ReplaceFile(string source, string destination)
    {
        if (File.Exists(destination))
        {
            File.Replace(source, destination, destination + ".bak", ignoreMetadataErrors: true);

            if (File.Exists(destination + ".bak"))
            {
                File.Delete(destination + ".bak");
            }

            return;
        }

        File.Move(source, destination);
    }
}
