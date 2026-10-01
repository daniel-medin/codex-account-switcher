# Codex research notes

## Status

The first usable multi-account MVP is implemented on `feature/multi-account-mvp`.

The implementation is intentionally scoped to Codex's default **file auth** mode. It does not manipulate session, skill, history, or project files.

## Verified against current OpenAI Codex source

### Authentication storage

Current Codex defines `$CODEX_HOME/auth.json` as the default credential store. Codex also supports keyring-backed modes, so the switcher must not assume every installation uses the file backend forever.

The current `auth.json` ChatGPT token object contains:

- ID token
- access token
- refresh token
- account/workspace ID

The ID token exposes stable non-secret identity metadata used by Codex itself, including:

- email
- ChatGPT user ID
- ChatGPT account/workspace ID
- ChatGPT plan type

The switcher now identifies accounts using these stable IDs rather than hashing the complete `auth.json`. This survives normal access-token and refresh-token rotation.

### Token refresh

Current Codex refreshes ChatGPT OAuth tokens through:

`https://auth.openai.com/oauth/token`

using the refresh-token grant and Codex's public OAuth client ID.

The switcher follows the same refresh shape when an inactive stored account returns HTTP 401 while reading usage. Returned token fields are merged into that account's encrypted auth state.

If the refreshed account is also the active account, the live `auth.json` is updated only when its refresh token has not changed since the refresh started. This avoids overwriting a newer token rotation performed by Codex itself.

### Usage

Current Codex reads ChatGPT usage from the ChatGPT backend usage endpoint:

`https://chatgpt.com/backend-api/wham/usage`

The request uses:

- `Authorization: Bearer <access token>`
- `ChatGPT-Account-Id: <account/workspace id>`

The response supplies the primary and secondary rate-limit windows, including:

- used percentage
- window duration
- reset timestamp
- plan type
- response account/user IDs

The switcher validates returned identity metadata before displaying usage.

### Logout behavior

Current Codex logout can revoke stored tokens before deleting credentials.

For that reason the switcher **does not call `codex logout`** when registering or switching accounts.

## Multi-account registration design

Adding another account no longer touches the live `.codex/auth.json` at all.

The flow is:

1. Persist any refreshed auth state for the currently active registered account.
2. Create a temporary isolated `CODEX_HOME`.
3. Run normal `codex login` using that temporary home.
4. Let Codex perform the real OpenAI browser/OAuth login.
5. Read the temporary `auth.json` after successful login.
6. Extract stable account identity metadata.
7. Encrypt and store the complete auth state using Windows DPAPI / CurrentUser scope.
8. Delete the temporary login home.
9. Leave the original active Codex account and running session untouched.

This is safer than temporarily deleting or moving the live auth file.

## Account switching

When switching to a stored account:

1. Read the live `auth.json`.
2. Identify the currently active account using stable identity claims.
3. Persist its latest complete auth state so token rotations are retained.
4. Decrypt the selected account's complete auth state.
5. Verify its identity.
6. Write it to live `auth.json` using an atomic replace.
7. Re-read and verify the written identity.
8. Roll back if replacement or verification fails.
9. Stop only Codex worker processes that have a VS Code `Code.exe` ancestor.

The process filter is deliberate: standalone Codex CLI jobs are not killed.

VS Code is expected to recreate/reconnect its Codex worker when needed while the local session files remain in the shared `CODEX_HOME`.

## Usage dashboard

The WPF dashboard now reads all registered accounts independently and displays:

- alias
- email
- plan
- active account
- primary remaining percentage
- secondary remaining percentage
- reset times
- usage refresh status
- a visual "best available" suggestion

The suggestion never changes accounts automatically.

## Secure storage

Per-account auth snapshots are encrypted with Windows DPAPI using `DataProtectionScope.CurrentUser`.

Account metadata is stored separately and contains only non-secret identity/display information.

Tokens are never intentionally written to diagnostic output or logs.

## Current limitations

1. Keyring-backed Codex auth is not yet managed by the switcher.
2. The usage and OAuth endpoints are Codex internals rather than a versioned public third-party API, so those implementations are isolated and may need maintenance when Codex changes.
3. Automatic reload currently stops VS Code-owned Codex worker processes. It does not force-restart the whole VS Code window.
4. Actual end-to-end behavior still needs to be exercised on a real Windows workstation with multiple Plus accounts.

## Next useful work

- add automated fake-auth tests for identity parsing, switching and rollback
- detect configured Codex credential-store mode before enabling file switching
- add account rename/remove actions
- add system tray support
- package as a self-contained Windows executable / installer
