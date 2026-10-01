# Codex research notes

## Status

Phase 2 has started on the `feature/multi-account-mvp` branch.

The first multi-account implementation is intentionally limited to **file-based authentication via `$CODEX_HOME/auth.json`**.

## Verified from current Codex source

Current Codex authentication is abstracted behind a credential storage layer and can use an auth file and/or keyring-backed storage depending on configuration.

The current logout implementation can revoke stored authentication tokens before deleting credential state. Because the account switcher needs to preserve credentials for later reuse, the application must **not call `codex logout` as part of ordinary account registration/switching**.

For the file-based MVP, adding another account therefore uses this flow:

1. Persist the current known `auth.json` into DPAPI-protected application storage.
2. Move `auth.json` to a temporary staging path.
3. Run normal `codex login`.
4. On successful login, capture the new `auth.json`.
5. On failure, restore the staged previous auth state.

## Current implementation

The app now supports:

- importing the current active file-based Codex account
- DPAPI-protected per-account credential blobs
- account metadata under `%LOCALAPPDATA%\CodexAccountSwitcher`
- interactive `codex login` to register another account
- detecting whether the current `auth.json` matches a registered account
- switching `auth.json` to a registered account
- atomic writes and rollback on failed activation
- preserving `sessions`, `skills`, `config.toml`, and all unrelated Codex state

## Still to verify/implement

1. Detect and support keyring credential storage.
2. Detect the configured `cli_auth_credentials_store_mode`.
3. Verify behavior with the VS Code extension on the target Windows machine.
4. Automatically reload/restart only the Codex process after switching.
5. Verify that a resumed session can continue across ChatGPT account IDs.
6. Retrieve usage/rate limits for each stored account.
7. Add automated fake-CODEX_HOME tests for switching and rollback.
8. Add safe account removal.

## Safety constraint

Until keyring support is implemented, do not broaden auth manipulation beyond the verified `auth.json` path.
