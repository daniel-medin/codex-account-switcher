# AGENTS.md

## Project goal

Build a safe Windows utility that lets a user switch between multiple ChatGPT/Codex accounts without deleting the shared Codex environment or losing sessions, skills, configuration, history, or project context.

## Primary rules

1. **Research before modifying real Codex data.**
   - Do not manipulate the user's real `.codex` authentication state until the current Codex behavior has been inspected and documented.
   - Build and test against a temporary/fake Codex home first.

2. **Never delete the entire `.codex` directory.**
   - Account switching must be surgical.
   - Sessions, skills, config, history, and unrelated files must remain untouched.

3. **Do not assume auth implementation details.**
   - Inspect the installed Codex version and official/open-source Codex code.
   - Do not assume `auth.json` is the only auth source.
   - Check whether Windows Credential Manager or other storage is involved.

4. **Treat credentials as secrets.**
   - Never print or log access tokens, refresh tokens, cookies, authorization headers, or full auth payloads.
   - Prefer DPAPI and/or Windows Credential Manager.
   - Sensitive backups must also be encrypted/protected.

5. **Treat each account auth state atomically.**
   - Never mix fields from different accounts.
   - Persist the current account's latest auth state before switching away.
   - Account for refresh-token rotation.

6. **Use transactional switching.**
   - Backup current state.
   - Prepare replacement state.
   - Validate.
   - Replace atomically where possible.
   - Restart/reload only what is necessary.
   - Verify the new account.
   - Roll back automatically on failure.

7. **Preserve Codex sessions.**
   - A switch must not alter session/history files.
   - Tests should verify preserved files remain byte-identical.

8. **Prefer minimal VS Code disruption.**
   Priority:
   - restart only Codex/Codex child process
   - reload relevant extension host/window
   - restart VS Code only if necessary

9. **Isolate unstable integrations.**
   - Usage/rate-limit retrieval must sit behind an interface such as `ICodexUsageService`.
   - Any undocumented endpoint or internal format must be documented and easy to replace.

10. **Keep the implementation understandable.**
    - C# / .NET 10 when available.
    - WPF desktop app.
    - MVVM where it improves maintainability.
    - Avoid unnecessary frameworks and overengineering.

## Development order

Work in this order unless research proves another order is necessary:

1. Inspect repository and local environment.
2. Create project/solution structure.
3. Research installed Codex behavior.
4. Write findings to `docs/codex-research.md`.
5. Build diagnostics.
6. Build secure account storage.
7. Build switching against a fake Codex home.
8. Add automated tests.
9. Only then connect switching to the real Codex home.
10. Add usage retrieval.
11. Build/refine WPF UI.
12. Add tray support and VS Code integration.

## Research checklist

Determine:

- exact Codex home location
- installed Codex version
- auth file(s)
- auth storage mechanism
- token refresh behavior
- whether auth state is rewritten after refresh
- session file location and format
- whether sessions embed account IDs
- source of usage/rate-limit information
- primary/secondary usage window structure
- reset-time representation
- process used by the VS Code Codex extension
- minimum process/reload action required after auth replacement

Prefer:

- installed Codex behavior
- official OpenAI Codex source
- official OpenAI documentation

Do not rely on old third-party blog posts when direct evidence is available.

## Suggested project structure

```text
CodexAccountSwitcher.sln

src/
  CodexAccountSwitcher/
    App.xaml
    MainWindow.xaml

    Models/
      CodexAccount.cs
      CodexUsage.cs
      CodexEnvironment.cs

    Services/
      AccountService.cs
      AuthenticationService.cs
      CodexUsageService.cs
      CodexEnvironmentService.cs
      CodexProcessService.cs
      VsCodeService.cs
      SecureStorageService.cs

    ViewModels/
      MainViewModel.cs
      AccountViewModel.cs

    Views/
      MainWindow.xaml
      AddAccountWindow.xaml
      SettingsWindow.xaml

tests/
  CodexAccountSwitcher.Tests/

docs/
  codex-research.md
```

Adjust this structure if research shows a better design.

## Required tests

At minimum cover:

- credential encrypt/decrypt
- Account A -> Account B switching
- rollback after failed replacement
- detection of active account
- refreshed auth-state persistence
- usage response parsing
- config preservation
- skills preservation
- session preservation
- byte-identical unchanged session files

Use a temporary Codex home for tests.

## Logging

Safe examples:

```text
Found CODEX_HOME
Detected Codex version 1.x
Switch requested: Account 2
Current auth state persisted
New auth state activated
Codex process restarted
Switch verified
```

Never log secrets.

## Definition of done

The core scenario must work:

1. VS Code is open in Project X.
2. A long-running Codex session exists.
3. Account 1 reaches its usage limit.
4. The app shows capacity for registered accounts.
5. User selects Account 3.
6. Current Account 1 auth state is safely persisted.
7. Account 3 auth state is activated.
8. Codex is reloaded/restarted.
9. Project X remains open.
10. The same Codex session remains available.
11. The next request uses Account 3.

No deletion of `.codex`, no lost session, no repeated manual login, and no manual file copying.
