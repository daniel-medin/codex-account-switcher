using System.IO;
using System.Text;
using System.Text.Json;
using CodexAccountSwitcher.Models;
using CodexAccountSwitcher.Services;

[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace CodexAccountSwitcher.Tests;

public sealed class AccountSwitchTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "CodexAccountSwitcher.Tests", Guid.NewGuid().ToString("N"));
    private readonly string? _previousHome = Environment.GetEnvironmentVariable("CODEX_HOME");
    private readonly CodexAuthParser _parser = new();

    public AccountSwitchTests()
    {
        Directory.CreateDirectory(_root);
        Environment.SetEnvironmentVariable("CODEX_HOME", Path.Combine(_root, "codex-home"));
        Directory.CreateDirectory(CodexHome);
    }

    private string CodexHome => Path.Combine(_root, "codex-home");
    private string AuthPath => Path.Combine(CodexHome, "auth.json");
    private AccountStoreService Store => new(Path.Combine(_root, "switcher-store"));

    [Fact]
    public async Task CredentialStoreEncryptsAndDecryptsCompleteState()
    {
        var accountId = Guid.NewGuid();
        var state = Auth("a", "access-a", "refresh-a");

        await Store.SaveCredentialsAsync(accountId, state);

        var encrypted = await File.ReadAllBytesAsync(Path.Combine(_root, "switcher-store", "credentials", $"{accountId:N}.bin"));
        Assert.DoesNotContain("refresh-a", Encoding.UTF8.GetString(encrypted));
        Assert.Equal(state, await Store.LoadCredentialsAsync(accountId));
    }

    [Fact]
    public async Task DefaultStoreCanBeRedirectedForIsolatedRuns()
    {
        var previous = Environment.GetEnvironmentVariable("CODEX_ACCOUNT_SWITCHER_DATA_HOME");
        var isolatedStore = Path.Combine(_root, "isolated-default-store");

        try
        {
            Environment.SetEnvironmentVariable("CODEX_ACCOUNT_SWITCHER_DATA_HOME", isolatedStore);
            var account = Account("isolated");
            await new AccountStoreService().SaveAccountsAsync([account]);

            Assert.True(File.Exists(Path.Combine(isolatedStore, "accounts.json")));
            Assert.Equal(account.Id, (await new AccountStoreService().LoadAccountsAsync()).Single().Id);
        }
        finally
        {
            Environment.SetEnvironmentVariable("CODEX_ACCOUNT_SWITCHER_DATA_HOME", previous);
        }
    }

    [Fact]
    public async Task SwitchPersistsRotatedStateAndPreservesSharedCodexFiles()
    {
        var original = Auth("a", "access-a", "refresh-a");
        var rotated = Auth("a", "new-access-a", "new-refresh-a");
        var replacement = Auth("b", "access-b", "refresh-b");
        await File.WriteAllBytesAsync(AuthPath, rotated);

        var config = WriteShared("config.toml", [1, 2, 3, 4]);
        var skill = WriteShared(Path.Combine("skills", "sample", "SKILL.md"), [5, 6, 7]);
        var session = WriteShared(Path.Combine("sessions", "2026", "10", "01", "rollout.jsonl"), [8, 0, 9, 10]);

        var store = Store;
        var accountA = Account("a");
        var accountB = Account("b");
        await store.SaveAccountsAsync([accountA, accountB]);
        await store.SaveCredentialsAsync(accountA.Id, original);
        await store.SaveCredentialsAsync(accountB.Id, replacement);

        var manager = new AccountManagerService(store, new UnusedCli(), _parser);
        await manager.ActivateAccountAsync(accountB.Id);

        Assert.Equal(replacement, await File.ReadAllBytesAsync(AuthPath));
        Assert.Equal(rotated, await store.LoadCredentialsAsync(accountA.Id));
        Assert.Equal([1, 2, 3, 4], await File.ReadAllBytesAsync(config));
        Assert.Equal([5, 6, 7], await File.ReadAllBytesAsync(skill));
        Assert.Equal([8, 0, 9, 10], await File.ReadAllBytesAsync(session));
        Assert.True((await manager.GetAccountsAsync()).Single(account => account.Id == accountB.Id).IsActive);
        Assert.False(File.Exists(AuthPath + ".account-switcher-rollback"));
        Assert.False(File.Exists(AuthPath + ".account-switcher-write-backup"));
    }

    [Fact]
    public async Task FailedSwitchRestoresOriginalAuth()
    {
        var original = Auth("a", "access-a", "refresh-a");
        var replacement = Auth("b", "access-b", "refresh-b");
        await File.WriteAllBytesAsync(AuthPath, original);

        var store = Store;
        var accountA = Account("a");
        var accountB = Account("b");
        await store.SaveAccountsAsync([accountA, accountB]);
        await store.SaveCredentialsAsync(accountA.Id, original);
        await store.SaveCredentialsAsync(accountB.Id, replacement);

        var manager = new AccountManagerService(new FailingSaveStore(store), new UnusedCli(), _parser);
        await Assert.ThrowsAsync<IOException>(() => manager.ActivateAccountAsync(accountB.Id));

        Assert.Equal(original, await File.ReadAllBytesAsync(AuthPath));
        Assert.False(File.Exists(AuthPath + ".account-switcher-tmp"));
    }

    private string WriteShared(string relativePath, byte[] bytes)
    {
        var path = Path.Combine(CodexHome, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllBytes(path, bytes);
        return path;
    }

    private static CodexAccount Account(string name) => new()
    {
        DisplayName = name,
        Email = $"{name}@example.test",
        ChatGptUserId = $"user-{name}",
        ChatGptAccountId = $"account-{name}"
    };

    private static byte[] Auth(string name, string access, string refresh)
    {
        var claims = JsonSerializer.SerializeToUtf8Bytes(new Dictionary<string, object>
        {
            ["email"] = $"{name}@example.test",
            ["https://api.openai.com/auth"] = new
            {
                chatgpt_user_id = $"user-{name}",
                chatgpt_account_id = $"account-{name}"
            }
        });
        var idToken = $"e30.{Convert.ToBase64String(claims).TrimEnd('=').Replace('+', '-').Replace('/', '_')}.signature";
        return JsonSerializer.SerializeToUtf8Bytes(new
        {
            tokens = new { access_token = access, refresh_token = refresh, id_token = idToken, account_id = $"account-{name}" }
        });
    }

    public void Dispose()
    {
        Environment.SetEnvironmentVariable("CODEX_HOME", _previousHome);
        Directory.Delete(_root, recursive: true);
    }

    private sealed class UnusedCli : ICodexCliService
    {
        public Task<string?> FindCodexExecutableAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<int> RunInteractiveLoginAsync(string executablePath, string codexHome, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private sealed class FailingSaveStore(IAccountStoreService inner) : IAccountStoreService
    {
        public Task<IReadOnlyList<CodexAccount>> LoadAccountsAsync(CancellationToken cancellationToken = default) => inner.LoadAccountsAsync(cancellationToken);
        public Task SaveAccountsAsync(IEnumerable<CodexAccount> accounts, CancellationToken cancellationToken = default) => throw new IOException("Simulated metadata write failure");
        public Task SaveCredentialsAsync(Guid accountId, byte[] authState, CancellationToken cancellationToken = default) => inner.SaveCredentialsAsync(accountId, authState, cancellationToken);
        public Task<byte[]> LoadCredentialsAsync(Guid accountId, CancellationToken cancellationToken = default) => inner.LoadCredentialsAsync(accountId, cancellationToken);
    }
}
