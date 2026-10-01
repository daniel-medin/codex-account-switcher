using System.Text;
using System.Text.Json;
using CodexAccountSwitcher.Models;

namespace CodexAccountSwitcher.Services;

public sealed class CodexAuthParser : ICodexAuthParser
{
    public CodexAuthInfo Parse(byte[] authState)
    {
        using var document = JsonDocument.Parse(authState);
        var root = document.RootElement;

        if (!root.TryGetProperty("tokens", out var tokens) ||
            tokens.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidOperationException(
                "The Codex auth state does not contain ChatGPT token data.");
        }

        var accessToken = GetString(tokens, "access_token");
        var refreshToken = GetString(tokens, "refresh_token");
        var idToken = GetString(tokens, "id_token");
        var routingAccountId = GetString(tokens, "account_id");

        if (string.IsNullOrWhiteSpace(accessToken))
        {
            throw new InvalidOperationException(
                "The Codex auth state does not contain a ChatGPT access token.");
        }

        string? email = null;
        string? userId = null;
        string? claimAccountId = null;
        string? planType = null;

        if (!string.IsNullOrWhiteSpace(idToken))
        {
            using var claims = DecodeJwtPayload(idToken);
            var claimsRoot = claims.RootElement;

            email = GetString(claimsRoot, "email");

            if (claimsRoot.TryGetProperty("https://api.openai.com/profile", out var profile) &&
                profile.ValueKind == JsonValueKind.Object)
            {
                email ??= GetString(profile, "email");
            }

            if (claimsRoot.TryGetProperty("https://api.openai.com/auth", out var authClaims) &&
                authClaims.ValueKind == JsonValueKind.Object)
            {
                userId = GetString(authClaims, "chatgpt_user_id")
                         ?? GetString(authClaims, "user_id");

                claimAccountId = GetString(authClaims, "chatgpt_account_id");
                planType = GetString(authClaims, "chatgpt_plan_type");
            }
        }

        return new CodexAuthInfo
        {
            Email = email,
            ChatGptUserId = userId,
            ChatGptAccountId = routingAccountId ?? claimAccountId,
            PlanType = planType,
            AccessToken = accessToken,
            RefreshToken = refreshToken ?? string.Empty,
            IdToken = idToken,
            AccessTokenExpiresAt = TryReadJwtExpiration(accessToken)
        };
    }

    public bool Matches(CodexAccount account, CodexAuthInfo authInfo)
    {
        if (!string.IsNullOrWhiteSpace(account.ChatGptUserId) &&
            !string.IsNullOrWhiteSpace(authInfo.ChatGptUserId))
        {
            return string.Equals(
                       account.ChatGptUserId,
                       authInfo.ChatGptUserId,
                       StringComparison.Ordinal) &&
                   AccountsCompatible(account.ChatGptAccountId, authInfo.ChatGptAccountId);
        }

        if (!string.IsNullOrWhiteSpace(account.Email) &&
            !string.IsNullOrWhiteSpace(authInfo.Email))
        {
            return string.Equals(
                       account.Email.Trim(),
                       authInfo.Email.Trim(),
                       StringComparison.OrdinalIgnoreCase) &&
                   AccountsCompatible(account.ChatGptAccountId, authInfo.ChatGptAccountId);
        }

        if (!string.IsNullOrWhiteSpace(account.ChatGptAccountId) &&
            !string.IsNullOrWhiteSpace(authInfo.ChatGptAccountId))
        {
            return string.Equals(
                account.ChatGptAccountId,
                authInfo.ChatGptAccountId,
                StringComparison.Ordinal);
        }

        return false;
    }

    public void ApplyIdentity(CodexAccount account, CodexAuthInfo authInfo)
    {
        account.Email = authInfo.Email ?? account.Email;
        account.ChatGptUserId = authInfo.ChatGptUserId ?? account.ChatGptUserId;
        account.ChatGptAccountId = authInfo.ChatGptAccountId ?? account.ChatGptAccountId;
        account.PlanType = authInfo.PlanType ?? account.PlanType;
    }

    private static bool AccountsCompatible(string? left, string? right)
    {
        return string.IsNullOrWhiteSpace(left) ||
               string.IsNullOrWhiteSpace(right) ||
               string.Equals(left, right, StringComparison.Ordinal);
    }

    private static string? GetString(JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out var property) &&
               property.ValueKind == JsonValueKind.String
            ? property.GetString()
            : null;
    }

    private static JsonDocument DecodeJwtPayload(string jwt)
    {
        var parts = jwt.Split('.');
        if (parts.Length != 3 || string.IsNullOrWhiteSpace(parts[1]))
        {
            throw new InvalidOperationException("Codex ID token has an invalid JWT format.");
        }

        var payload = parts[1]
            .Replace('-', '+')
            .Replace('_', '/');

        payload = payload.PadRight(
            payload.Length + ((4 - payload.Length % 4) % 4),
            '=');

        try
        {
            return JsonDocument.Parse(Convert.FromBase64String(payload));
        }
        catch (Exception ex) when (ex is FormatException or JsonException)
        {
            throw new InvalidOperationException(
                "Codex ID token payload could not be decoded.",
                ex);
        }
    }

    private static DateTimeOffset? TryReadJwtExpiration(string jwt)
    {
        try
        {
            using var payload = DecodeJwtPayload(jwt);
            if (payload.RootElement.TryGetProperty("exp", out var exp) &&
                exp.TryGetInt64(out var seconds))
            {
                return DateTimeOffset.FromUnixTimeSeconds(seconds);
            }
        }
        catch
        {
            // Some bearer tokens are opaque. Expiration is optional for this app.
        }

        return null;
    }
}
