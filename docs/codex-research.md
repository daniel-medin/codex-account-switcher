# Codex research notes

## Status

Phase 1 has started.

The initial application intentionally performs read-only diagnostics only. It does not replace, copy, delete, encrypt, or otherwise mutate Codex authentication state.

## What the first build currently detects

The application attempts to determine:

- effective `CODEX_HOME`
  - explicit `CODEX_HOME` environment variable when set
  - otherwise `%USERPROFILE%\.codex`
- whether the Codex home exists
- whether `auth.json` exists as an **auth candidate**
- whether `sessions` exists
- whether `skills` exists
- Codex executable location
- output from `codex --version`
- number of VS Code processes
- number of Codex-like processes

## Important

Finding `auth.json` does **not** yet mean we have proven it is the complete or only authentication state.

Before implementing account switching on real data we still need to verify on the target Windows machine:

1. Which Codex build/version is installed.
2. Whether the VS Code extension uses the same Codex home as the CLI.
3. All auth-related storage locations.
4. Whether Windows Credential Manager is involved.
5. Whether auth data changes after token refresh.
6. Whether sessions contain account-specific identifiers.
7. Which process needs to be restarted after authentication changes.
8. How usage/rate-limit information is retrieved in the currently installed build.
9. Whether a supported API exists for retrieving usage for a non-active stored account.

## Safety rule

Do not implement real account swapping until diagnostics from an actual target machine have been reviewed.

A fake/temp Codex home should be used for switching tests first.

## Next implementation step

After running Phase 1 on the target Windows machine:

- capture non-secret diagnostics
- expand diagnostics where needed
- document verified auth/session behavior
- implement secure account metadata/credential storage
- implement a fake-environment switcher with rollback tests
