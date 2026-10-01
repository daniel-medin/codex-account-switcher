# Codex Account Switcher

A Windows desktop app for managing multiple ChatGPT/Codex accounts while preserving the same local Codex sessions, skills, configuration, and project context.

## Why

When a Codex account reaches its usage limit, switching to another ChatGPT account currently tends to involve closing VS Code, removing or replacing parts of `.codex`, reopening VS Code, and signing in again.

The goal of this project is to make that workflow safe and fast:

- keep one shared Codex environment
- keep sessions/history intact
- keep skills and configuration intact
- store account authentication states separately and securely
- show Codex usage/rate limits for each account
- switch the active account with one click
- reload only what is necessary
- continue the same project/session after switching accounts

## Intended workflow

1. Work in VS Code with Codex using Account A.
2. Account A approaches or reaches its usage limit.
3. Open Codex Account Switcher.
4. See usage for all registered accounts.
5. Choose another account.
6. The app safely persists the current auth state, activates the selected account, and reloads Codex.
7. Continue the same Codex session in the same project.

## Planned stack

- C#
- .NET 10 where supported
- WPF
- MVVM where useful, without unnecessary abstraction
- Windows DPAPI and/or Windows Credential Manager for sensitive credentials

## Core requirements

- Never delete the whole `.codex` directory.
- Preserve sessions, skills, configuration, and history.
- Treat each account authentication state as an atomic unit.
- Handle refreshed/rotated OAuth credentials safely.
- Never log access tokens, refresh tokens, cookies, or full auth payloads.
- Use atomic switching with rollback on failure.
- Prefer restarting only Codex rather than all of VS Code.
- Keep usage retrieval behind an abstraction because the underlying endpoint may change.

## Initial phases

### Phase 1 — Research and diagnostics

Determine how the current Codex installation handles:

- authentication
- token refresh
- session storage
- usage/rate limits
- VS Code integration
- process lifecycle

Document findings in `docs/codex-research.md`.

### Phase 2 — Account storage

Implement:

- import current account
- add account
- remove account
- secure credential storage
- active account detection

### Phase 3 — Safe switching

Implement Account A -> Account B while leaving shared Codex data untouched.

### Phase 4 — Usage

Show:

- primary usage window
- secondary usage window
- reset times
- optional credits/plan information when available

### Phase 5 — Desktop UI

Build the WPF application with account cards, usage bars, refresh controls, and account switching.

### Phase 6 — Tray and VS Code integration

Add a tray UI and minimize disruption during account switching.

## Repository guidance

See:

- [AGENTS.md](AGENTS.md) for implementation rules
- [PROMPT.md](PROMPT.md) for the full project brief

## Status

Initial research / bootstrap stage.

## Disclaimer

This is an independent utility and is not an official OpenAI product. The implementation should prefer documented and supported mechanisms where available and isolate any dependency on undocumented Codex internals.


## Current MVP

The `feature/multi-account-mvp` implementation adds the first real multi-account workflow for **file-based Codex authentication**:

1. Sign in to Codex normally.
2. Enter an alias and choose **Import current**.
3. Enter another alias and choose **Add another account**.
4. The app saves the current auth state securely, temporarily removes the active `auth.json` without calling `codex logout`, starts `codex login`, and captures the new account after successful login.
5. Repeat for more accounts.
6. Choose **Use account** to replace only the active auth state.

Stored credential blobs are encrypted with Windows DPAPI using `CurrentUser` scope.

### Important MVP limitation

This version deliberately supports only Codex installations where the active ChatGPT authentication is represented by `$CODEX_HOME/auth.json`. Current Codex also supports configurable credential storage/keyring backends, so the app refuses to pretend those cases are supported until they have been verified and implemented.

After switching auth, reload the Codex VS Code window/session so the running Codex process reads the new credentials.


## Multi-account MVP

The current feature branch implements the core workflow:

- import the currently active Codex login
- register additional accounts through normal Codex browser login without logging out the active account
- store each account's complete auth state encrypted with Windows DPAPI
- identify accounts using stable ChatGPT user/account IDs instead of auth-file hashes
- retain refreshed/rotated credentials
- read primary and secondary Codex usage for every stored account
- refresh expired inactive-account access tokens using the same OAuth refresh flow as Codex
- show reset times and a best-available visual hint
- switch only the active authentication state
- atomically roll back failed switches
- stop only VS Code-owned Codex worker processes after switching so the extension can reconnect
- preserve shared sessions, skills, configuration and history

### Setup flow

1. Start the app while your first Codex account is already signed in.
2. Enter an alias and choose **Import current**.
3. Enter another alias and choose **Add another account**.
4. Complete the normal Codex browser login.
5. Repeat for the remaining accounts.
6. Use **Use account** whenever you want to switch.

Adding another account uses a temporary isolated `CODEX_HOME`, so the live Codex login does not need to be logged out or moved during registration.

### Internal integration note

Usage and OAuth refresh follow the behavior of the current open-source Codex client. Those endpoints are intentionally isolated in services because they are not a stable public third-party API contract.
