using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using CodexAccountSwitcher.Models;

namespace CodexAccountSwitcher.Services;

public sealed class CodexUsageService : ICodexUsageService
{
    private const string UsageUrl =
        "https://chatgpt.com/backend-api/wham/usage";

    private const string RefreshUrl =
        "https://auth.openai.com/oauth/token";

    // This is the public client id used by the current Codex OAuth refresh flow.
    private const string CodexClientId =
        "app_EMoamEEZ73f0CkXaXp7hrann";

    private readonly IAccountStoreService _accountStore;
    private readonly ICodexAuthParser _authParser;
    private readonly HttpClient _httpClient;
    private readonly SemaphoreSlim _refreshLock = new(1, 1);

    public CodexUsageService(
        IAccountStoreService accountStore,
        ICodexAuthParser authParser,
        HttpClient httpClient)
    {
        _accountStore = accountStore;
        _authParser = authParser;
        _httpClient = httpClient;
    }

    public async Task<CodexUsage> GetUsageAsync(
        CodexAccount account,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var authState = await GetBestCredentialStateAsync(account, cancellationToken);
            var authInfo = _authParser.Parse(authState);

            var result = await FetchUsageAsync(authInfo, cancellationToken);

            if (result.StatusCode == HttpStatusCode.Unauthorized &&
                !string.IsNullOrWhiteSpace(authInfo.RefreshToken))
            {
                await _refreshLock.WaitAsync(cancellationToken);

                try
                {
                    // Another refresh may have completed while this request waited.
                    authState = await GetBestCredentialStateAsync(account, cancellationToken);
                    authInfo = _authParser.Parse(authState);
                    result = await FetchUsageAsync(authInfo, cancellationToken);

                    if (result.StatusCode == HttpStatusCode.Unauthorized)
                    {
                        var refreshedState = await RefreshAuthAsync(
                            account,
                            authState,
                            authInfo,
                            cancellationToken);

                        authInfo = _authParser.Parse(refreshedState);
                        result = await FetchUsageAsync(authInfo, cancellationToken);
                    }
                }
                finally
                {
                    _refreshLock.Release();
                }
            }

            if (result.StatusCode != HttpStatusCode.OK)
            {
                return new CodexUsage
                {
                    Error = result.StatusCode == HttpStatusCode.Unauthorized
                        ? "Sign-in required"
                        : $"Usage unavailable ({(int)result.StatusCode})"
                };
            }

            return ParseUsage(result.Body, account);
        }
        catch (Exception ex)
        {
            return new CodexUsage
            {
                Error = ToSafeError(ex)
            };
        }
    }

    private async Task<byte[]> GetBestCredentialStateAsync(
        CodexAccount account,
        CancellationToken cancellationToken)
    {
        var stored = await _accountStore.LoadCredentialsAsync(account.Id, cancellationToken);
        var activePath = GetActiveAuthPath();

        if (!File.Exists(activePath))
        {
            return stored;
        }

        try
        {
            var active = await File.ReadAllBytesAsync(activePath, cancellationToken);
            var activeInfo = _authParser.Parse(active);

            if (_authParser.Matches(account, activeInfo))
            {
                await _accountStore.SaveCredentialsAsync(account.Id, active, cancellationToken);
                return active;
            }
        }
        catch
        {
            // A partially written or unrelated active file must not block stored-account usage.
        }

        return stored;
    }

    private async Task<UsageHttpResult> FetchUsageAsync(
        CodexAuthInfo authInfo,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, UsageUrl);
        request.Headers.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue(
                "Bearer",
                authInfo.AccessToken);

        if (!string.IsNullOrWhiteSpace(authInfo.ChatGptAccountId))
        {
            request.Headers.TryAddWithoutValidation(
                "ChatGPT-Account-Id",
                authInfo.ChatGptAccountId);
        }

        request.Headers.UserAgent.ParseAdd("codex-account-switcher/0.1");

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);

        return new UsageHttpResult(response.StatusCode, body);
    }

    private async Task<byte[]> RefreshAuthAsync(
        CodexAccount account,
        byte[] originalState,
        CodexAuthInfo originalInfo,
        CancellationToken cancellationToken)
    {
        var payload = JsonSerializer.Serialize(new Dictionary<string, string>
        {
            ["grant_type"] = "refresh_token",
            ["client_id"] = CodexClientId,
            ["refresh_token"] = originalInfo.RefreshToken
        });

        using var request = new HttpRequestMessage(HttpMethod.Post, RefreshUrl)
        {
            Content = new StringContent(
                payload,
                Encoding.UTF8,
                "application/json")
        };

        using var response = await _httpClient.SendAsync(request, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(
                response.StatusCode == HttpStatusCode.BadRequest ||
                response.StatusCode == HttpStatusCode.Unauthorized
                    ? "Stored sign-in has expired. Activate this account and sign in again."
                    : "Could not refresh this account right now.");
        }

        var responseJson = await response.Content.ReadAsStringAsync(cancellationToken);
        var refresh = JsonSerializer.Deserialize<RefreshResponse>(responseJson)
                      ?? throw new InvalidOperationException(
                          "Codex token refresh returned an invalid response.");

        var root = JsonNode.Parse(originalState)?.AsObject()
                   ?? throw new InvalidOperationException(
                       "Stored Codex auth state is invalid.");

        var tokens = root["tokens"]?.AsObject()
                     ?? throw new InvalidOperationException(
                         "Stored Codex auth state has no token object.");

        if (!string.IsNullOrWhiteSpace(refresh.IdToken))
        {
            tokens["id_token"] = refresh.IdToken;
        }

        if (!string.IsNullOrWhiteSpace(refresh.AccessToken))
        {
            tokens["access_token"] = refresh.AccessToken;
        }

        if (!string.IsNullOrWhiteSpace(refresh.RefreshToken))
        {
            tokens["refresh_token"] = refresh.RefreshToken;
        }

        root["last_refresh"] = DateTimeOffset.UtcNow.ToString("O");

        var refreshedState = Encoding.UTF8.GetBytes(
            root.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));

        var refreshedInfo = _authParser.Parse(refreshedState);

        if (!_authParser.Matches(account, refreshedInfo))
        {
            throw new InvalidOperationException(
                "Refreshed credentials did not match the selected account.");
        }

        await _accountStore.SaveCredentialsAsync(
            account.Id,
            refreshedState,
            cancellationToken);

        await TryUpdateActiveAuthAsync(
            account,
            originalInfo,
            refreshedState,
            cancellationToken);

        return refreshedState;
    }

    private async Task TryUpdateActiveAuthAsync(
        CodexAccount account,
        CodexAuthInfo refreshSource,
        byte[] refreshedState,
        CancellationToken cancellationToken)
    {
        var activePath = GetActiveAuthPath();

        if (!File.Exists(activePath))
        {
            return;
        }

        byte[] activeState;

        try
        {
            activeState = await File.ReadAllBytesAsync(activePath, cancellationToken);
        }
        catch
        {
            return;
        }

        CodexAuthInfo activeInfo;

        try
        {
            activeInfo = _authParser.Parse(activeState);
        }
        catch
        {
            return;
        }

        if (!_authParser.Matches(account, activeInfo))
        {
            return;
        }

        // Do not overwrite a token rotation performed by Codex after our request started.
        if (!string.Equals(
                activeInfo.RefreshToken,
                refreshSource.RefreshToken,
                StringComparison.Ordinal))
        {
            await _accountStore.SaveCredentialsAsync(
                account.Id,
                activeState,
                cancellationToken);
            return;
        }

        await AtomicWriteAsync(activePath, refreshedState, cancellationToken);
    }

    private CodexUsage ParseUsage(string json, CodexAccount account)
    {
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;

        var responseAccountId = GetString(root, "account_id");
        var responseUserId = GetString(root, "user_id");

        if (!IdentityMatches(account.ChatGptAccountId, responseAccountId) ||
            !IdentityMatches(account.ChatGptUserId, responseUserId))
        {
            return new CodexUsage
            {
                Error = "Usage response identity mismatch"
            };
        }

        if (!root.TryGetProperty("rate_limit", out var rateLimit) ||
            rateLimit.ValueKind != JsonValueKind.Object)
        {
            return new CodexUsage
            {
                PlanType = GetString(root, "plan_type"),
                Error = "No Codex usage window returned"
            };
        }

        var primary = ReadWindow(rateLimit, "primary_window");
        var secondary = ReadWindow(rateLimit, "secondary_window");

        return new CodexUsage
        {
            PrimaryUsedPercent = primary.UsedPercent,
            PrimaryWindowDurationMinutes = primary.WindowDurationMinutes,
            PrimaryResetAt = primary.ResetAt,
            SecondaryUsedPercent = secondary.UsedPercent,
            SecondaryWindowDurationMinutes = secondary.WindowDurationMinutes,
            SecondaryResetAt = secondary.ResetAt,
            PlanType = GetString(root, "plan_type")
        };
    }

    private static WindowData ReadWindow(JsonElement rateLimit, string propertyName)
    {
        if (!rateLimit.TryGetProperty(propertyName, out var window) ||
            window.ValueKind != JsonValueKind.Object)
        {
            return default;
        }

        double? usedPercent = null;
        int? durationMinutes = null;
        DateTimeOffset? resetAt = null;

        if (window.TryGetProperty("used_percent", out var used) &&
            used.TryGetDouble(out var usedValue))
        {
            usedPercent = usedValue;
        }

        if (window.TryGetProperty("limit_window_seconds", out var duration) &&
            duration.TryGetInt32(out var durationSeconds))
        {
            durationMinutes = durationSeconds / 60;
        }

        if (window.TryGetProperty("reset_at", out var reset) &&
            reset.TryGetInt64(out var resetSeconds))
        {
            resetAt = DateTimeOffset.FromUnixTimeSeconds(resetSeconds);
        }

        return new WindowData(usedPercent, durationMinutes, resetAt);
    }

    private static bool IdentityMatches(string? expected, string? actual)
    {
        return string.IsNullOrWhiteSpace(expected) ||
               string.IsNullOrWhiteSpace(actual) ||
               string.Equals(expected, actual, StringComparison.Ordinal);
    }

    private static string? GetString(JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out var property) &&
               property.ValueKind == JsonValueKind.String
            ? property.GetString()
            : null;
    }

    private static string ToSafeError(Exception exception)
    {
        return exception switch
        {
            FileNotFoundException => "Stored credentials not found",
            System.Security.Cryptography.CryptographicException =>
                "Stored credentials could not be decrypted",
            HttpRequestException => "Usage service unavailable",
            TaskCanceledException => "Usage request timed out",
            InvalidOperationException invalid => invalid.Message,
            _ => "Usage could not be loaded"
        };
    }

    private static string GetActiveAuthPath()
    {
        var explicitHome = Environment.GetEnvironmentVariable("CODEX_HOME");
        var codexHome = string.IsNullOrWhiteSpace(explicitHome)
            ? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                ".codex")
            : Environment.ExpandEnvironmentVariables(explicitHome);

        return Path.Combine(codexHome, "auth.json");
    }

    private static async Task AtomicWriteAsync(
        string destination,
        byte[] bytes,
        CancellationToken cancellationToken)
    {
        var tempPath = destination + ".account-switcher-usage-tmp";
        await File.WriteAllBytesAsync(tempPath, bytes, cancellationToken);

        if (File.Exists(destination))
        {
            var backupPath = destination + ".account-switcher-usage-backup";
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

    private sealed class RefreshResponse
    {
        [System.Text.Json.Serialization.JsonPropertyName("id_token")]
        public string? IdToken { get; init; }

        [System.Text.Json.Serialization.JsonPropertyName("access_token")]
        public string? AccessToken { get; init; }

        [System.Text.Json.Serialization.JsonPropertyName("refresh_token")]
        public string? RefreshToken { get; init; }
    }

    private readonly record struct UsageHttpResult(
        HttpStatusCode StatusCode,
        string Body);

    private readonly record struct WindowData(
        double? UsedPercent,
        int? WindowDurationMinutes,
        DateTimeOffset? ResetAt);
}
