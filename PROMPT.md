# Codex Account Switcher — Full Build Prompt

I want you to build a real Windows application in C#/.NET for managing multiple ChatGPT Plus/Codex accounts and switching between them quickly without losing ongoing Codex sessions or deleting the full `.codex` directory.

## Background

I use four different ChatGPT Plus accounts with Codex in VS Code.

Current problem:

- I work in VS Code with Codex on a project.
- When one account reaches its usage/rate limit, I want to continue the same work with another ChatGPT account.
- Today I effectively have to:
  1. Close VS Code.
  2. Delete or manipulate `C:\Users\<username>\.codex`.
  3. Start VS Code again.
  4. Sign in with another ChatGPT account.
  5. Resume the work manually.

This is slow and risks affecting sessions, skills, configuration, history, and other data stored under `.codex`.

## Goal

Build a small Windows program with the working name:

**Codex Account Switcher**

It should allow me to:

- register four or more ChatGPT/Codex accounts
- see usage/rate limits for each account
- see which account is active
- switch accounts with one click
- preserve the same Codex sessions and history
- preserve the same shared Codex environment
- avoid deleting the full `.codex` directory
- avoid logging in again every time
- preferably avoid closing all of VS Code
- continue the same project/session after switching

## Important architectural constraint

Do **not** solve this by assigning each account a completely separate `CODEX_HOME` if that separates session/history data.

I want to share:

- sessions
- skills
- config
- project context/history
- other non-auth Codex state

while maintaining separate authentication state per ChatGPT account.

Conceptually:

```text
Shared Codex environment
|
+-- sessions
+-- skills
+-- config
+-- history
+-- other shared state
|
+-- ACTIVE AUTH  <-- switch this safely
```

The application stores per-account credential state separately.

Potential layout:

```text
Codex Account Switcher
|
+-- Account 1 auth state
+-- Account 2 auth state
+-- Account 3 auth state
+-- Account 4 auth state

C:\Users\<user>\.codex
|
+-- sessions
+-- skills
+-- config.toml
+-- ...
+-- current auth-related state
```

This is only a proposed model.

Before implementing final switching logic, inspect how the currently installed Codex version on Windows actually manages authentication, sessions, token refresh, and process state.

## Technology

Build using:

- C#
- current stable .NET, preferably .NET 10 if supported
- WPF
- Windows desktop

Use MVVM where useful, but avoid overengineering.

Suggested structure:

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

Change this if research proves a different structure is better.

## Feature 1 — Detect Codex installation

On startup, attempt to identify:

```text
%USERPROFILE%\.codex
```

Inspect the files/directories that exist there and classify relevant state such as:

- authentication
- config
- sessions
- skills
- logs
- other Codex files

Do not blindly assume `auth.json` is the only authentication source.

If Codex CLI is installed, locate it and inspect its version, for example:

```powershell
where codex
codex --version
```

Use other safe diagnostics as needed.

## Feature 2 — Register an account

The UI should offer:

```text
Add account

Name:
Daniel 1

[ Login with ChatGPT ]
```

Use Codex's normal authentication/OAuth flow as much as possible.

Do **not** implement a custom username/password collector.

After successful login, associate the resulting complete auth state with the application's secure account store.

Example model:

```text
Account
{
    Id
    DisplayName
    CreatedAt
    LastUsedAt
    CredentialLocation
}
```

Support adding Account 2, 3, 4, etc.

## Feature 3 — Secure credential storage

Credentials should not be stored as plaintext JSON if avoidable.

Prefer Windows-native protection such as:

- DPAPI
- Windows Credential Manager

Metadata may live under:

```text
%LOCALAPPDATA%\CodexAccountSwitcher\
```

but access tokens, refresh tokens, cookies, and similar secrets must be protected.

Secrets must never appear in application logs.

## Feature 4 — Token refresh and rotation

This is critical.

Do not simply keep four stale snapshots of auth data if Codex refreshes or rotates tokens.

Required scenario:

```text
Account 1 is active
↓
Codex refreshes its token
↓
auth state changes
↓
the app must preserve the latest valid Account 1 state
```

Before switching away from an active account:

1. inspect the current auth state
2. compare/update the stored state for that account
3. securely persist the latest complete state
4. only then activate the target account

Never combine pieces of auth state belonging to different accounts.

## Feature 5 — Account switching

Main UI:

```text
Account 1     ACTIVE
Account 2     [Use]
Account 3     [Use]
Account 4     [Use]
```

When the user chooses Account 3:

1. determine the currently active account
2. safely persist any newly refreshed auth state for the current account
3. stop/disconnect only relevant Codex processes if required
4. activate Account 3's complete auth state
5. leave sessions/config/skills/history untouched
6. restart/reload Codex
7. verify Account 3 is really active
8. roll back to the previous state if any step fails

Never delete `.codex`.

## Feature 6 — Preserve current sessions

The most important user story:

```text
I am working in a Codex chat with Account 1.

Account 1 reaches its limit.

I open Codex Account Switcher.

I choose Account 2.

I return to VS Code.

I continue the same Codex session.
```

Research where sessions are actually stored.

Determine whether sessions embed an account identifier and whether that affects cross-account resume behavior.

If a running Codex process caches auth/session state in memory, handle that carefully.

Automated tests should prove that unrelated session files are unchanged during switching.

## Feature 7 — VS Code integration

The first version does **not** need to be a VS Code extension.

Build a standalone Windows application.

However, attempt account switching without closing all of VS Code.

Investigate:

- restarting only the Codex process
- restarting a Codex CLI child process
- reloading the VS Code extension host
- `Developer: Reload Window`
- other safe mechanisms

Priority:

1. restart/reload only Codex
2. reload VS Code window
3. restart VS Code only if genuinely necessary

If VS Code must restart, attempt to reopen the current workspace/project.

## Feature 8 — Usage/rate limits

Show current Codex usage for every registered account.

Example:

```text
Daniel 1
5 hour: 72% remaining
Weekly: 41% remaining
Reset: 13:42

Daniel 2
5 hour: 18% remaining
Weekly: 77% remaining
Reset: 11:05

Daniel 3
5 hour: 96% remaining
Weekly: 92% remaining
Reset: 15:31
```

Investigate how Codex itself obtains data shown by `/status` or equivalent UI.

Search current official/open-source Codex code for concepts such as:

- rate limits
- usage
- used_percent
- reset_at
- primary window
- secondary window
- credits
- plan type

Prefer the same underlying mechanism Codex itself uses.

Do not use browser scraping when a cleaner mechanism exists.

If the usage endpoint/mechanism is internal or undocumented, isolate it behind an abstraction such as:

```csharp
public interface ICodexUsageService
{
    Task<CodexUsage?> GetUsageAsync(CodexAccount account);
}
```

so it can be replaced later.

## Feature 9 — Usage model

A possible model:

```csharp
public sealed class CodexUsage
{
    public double? PrimaryUsedPercent { get; set; }
    public double? SecondaryUsedPercent { get; set; }

    public DateTimeOffset? PrimaryResetAt { get; set; }
    public DateTimeOffset? SecondaryResetAt { get; set; }

    public decimal? CreditBalance { get; set; }
}
```

The UI can calculate:

```text
remaining = 100 - used
```

Display known windows as `5 hour` / `Weekly` only when the retrieved data allows them to be identified correctly.

Do not unnecessarily hardcode window durations if the backend supplies them.

## Feature 10 — Recommended account

The app may indicate which account has the most immediately useful capacity left.

Example:

```text
★ Best available
```

Use a simple transparent heuristic:

1. avoid accounts whose primary window is exhausted
2. prefer the account with the most primary capacity remaining
3. use secondary/weekly capacity as a tie-breaker

Do **not** switch automatically.

The user must choose the account.

## Feature 11 — UI

Build a compact modern dark Windows interface.

Concept:

```text
┌────────────────────────────────────────┐
│ CODEX ACCOUNT SWITCHER             ↻   │
├────────────────────────────────────────┤
│ ● Daniel 1                   ACTIVE    │
│                                        │
│   5h       ███████░░░  68% remaining  │
│   Weekly   ████░░░░░░  39% remaining  │
│                                        │
│   Reset 5h:       13:42                │
│   Reset weekly:   Friday 08:00         │
├────────────────────────────────────────┤
│ ○ Daniel 2                             │
│                                        │
│   5h       ██░░░░░░░░  21% remaining  │
│   Weekly   ████████░░  81% remaining  │
│                                        │
│                        [ USE ACCOUNT ] │
├────────────────────────────────────────┤
│ ○ Daniel 3                     ★       │
│                                        │
│   5h       █████████░  94% remaining  │
│   Weekly   █████████░  90% remaining  │
│                                        │
│                        [ USE ACCOUNT ] │
├────────────────────────────────────────┤
│ + Add account          ⚙ Settings      │
└────────────────────────────────────────┘
```

Show:

- account alias
- active account
- usage bars
- percent remaining
- reset times
- plan if known
- refresh button
- Use Account button
- Add Account
- Remove Account
- Settings

## Feature 12 — Tray app

After the core application works, add Windows system tray support.

Possible menu:

```text
Codex Account Switcher

✓ Daniel 1
  Daniel 2 – 21% 5h
  Daniel 3 – 94% 5h
  Daniel 4 – 55% 5h

Open
Refresh usage
Exit
```

## Feature 13 — Usage refresh

Support manual usage refresh.

Automatic refresh while the app is running may use a conservative interval such as five minutes.

Do not hammer usage endpoints.

## Feature 14 — Robust transactional switching

Avoid failure states such as:

```text
Account 1 auth removed
Account 2 auth write failed
=> Codex broken
```

Use a transactional sequence approximately like:

```text
capture/backup current auth
↓
persist current account state
↓
prepare target auth in temporary storage
↓
validate
↓
atomic replacement where possible
↓
restart/reload Codex
↓
verify target account
```

On failure:

```text
restore previous auth
```

## Feature 15 — Backup

Before manipulating real Codex auth state, create a safe protected backup.

Potential location:

```text
%LOCALAPPDATA%\CodexAccountSwitcher\backup\
```

Backups containing credentials must be encrypted/protected as carefully as primary credential storage.

## Feature 16 — Logging

Add useful diagnostic logging.

Safe example:

```text
2026-10-01 10:04 Found CODEX_HOME
2026-10-01 10:04 Codex version: ...
2026-10-01 10:05 Account switch requested: Daniel 2
2026-10-01 10:05 Existing account credentials persisted
2026-10-01 10:05 Authentication swapped
2026-10-01 10:05 Codex restarted
```

Never log:

- access tokens
- refresh tokens
- authorization headers
- full auth payloads
- cookies
- other secrets

## Feature 17 — Diagnostics

Provide a diagnostics screen/view showing safe information such as:

```text
Codex path:
C:\...

Codex version:
...

CODEX_HOME:
C:\Users\...\.codex

Auth mechanism:
...

VS Code detected:
Yes

Codex process:
Running

Current account:
Daniel 1
```

This will make troubleshooting much easier.

## Feature 18 — First run

On first launch:

1. detect Codex
2. detect an existing login
3. offer to import the currently active account

Example:

```text
An existing Codex login was detected.

Would you like to save it as an account?

Account name:
[ Daniel 1 ]

[ Save account ]
```

Then allow:

```text
Add another account
```

for the remaining accounts.

## Account identification

If available safely from Codex auth/API data, display:

- account ID
- email
- subscription/plan

Do not depend on those fields being present.

Always allow a manual alias such as:

```text
Daniel 1
Daniel 2
Daniel 3
Daniel 4
```

## Credential isolation

Be extremely careful about credential mixing.

Never create a state containing:

```text
Account 1 access token
+
Account 2 refresh token
```

Each account auth state must be treated as one complete unit.

## Testing

Write automated tests for critical behavior.

At minimum:

- encrypt/decrypt credentials
- switch Account A -> Account B
- rollback after failed write
- preserve session directory
- preserve config
- preserve skills
- parse usage payload
- persist refreshed auth state
- detect active account

Use a fake/temp Codex environment.

Example:

```text
TempCodexHome/
    auth.json
    config.toml
    sessions/
        session1.json
    skills/
        ...
```

After switching, only auth-related state should have changed.

Session data must remain byte-identical.

## Research first

Before implementing production switching, inspect the current Codex implementation and determine:

1. exactly which authentication files/state Codex uses on Windows
2. how `auth.json` or equivalent is structured
3. whether secrets are also stored in Windows Credential Manager
4. how token refresh works
5. whether credentials are rewritten after refresh
6. where sessions are stored
7. whether sessions are tied to a specific account ID
8. how `/status` obtains usage data
9. what endpoint/mechanism Codex uses for usage/rate limits
10. how the VS Code Codex extension communicates with Codex
11. which process must restart after changing auth
12. whether an official reload/restart method exists without restarting all of VS Code

Prefer:

- official Codex source code
- the currently installed Codex build
- official OpenAI documentation

Do not make assumptions based on old articles.

## Phases

### Phase 1

Research and diagnostics.

Verify:

- Codex home
- auth state
- sessions
- current account
- usage source
- Codex process
- VS Code process

Document findings in:

```text
docs/codex-research.md
```

### Phase 2

Implement account storage:

- import current account
- add account
- list accounts
- secure credential storage

### Phase 3

Implement safe switching:

```text
Account A
↓
Account B
```

without changing shared sessions/history.

### Phase 4

Implement usage:

- primary/5h window
- secondary/weekly window
- reset times
- optional credits/plan information

### Phase 5

Finish WPF UI.

### Phase 6

Add tray support and VS Code integration.

## Critical implementation rule

Research first.

Do **not** modify the real user's `.codex` auth state while still discovering how it works.

First build a model of Codex behavior and test switching against a temporary test directory.

Only connect to the real Codex home once the exact mutation surface has been verified.

## Definition of done

This scenario must work:

```text
1. VS Code is open.
2. I am working in Project X.
3. I have a long-running Codex session.
4. Account 1 reaches its usage limit.
5. I open Codex Account Switcher.
6. I see:

   Account 1
   5h: 0%

   Account 2
   5h: 81%

   Account 3
   5h: 94%

7. I click "Use Account 3".
8. The program safely switches auth.
9. Codex reloads/restarts.
10. I return to the same Project X.
11. The same Codex session remains.
12. I continue the chat.
13. The next Codex request uses Account 3.
```

And especially:

```text
No deletion of .codex.
No lost session.
No repeated manual login.
No manual file copying.
```

## Working method

Start now by:

1. inspecting the repository/environment
2. creating the solution/project structure
3. researching how current Codex auth/session/usage works
4. documenting findings in `docs/codex-research.md`
5. implementing Phase 1
6. running build/tests
7. continuing incrementally toward a working MVP

If research proves that assumptions in this brief are wrong, change the architecture rather than forcing Codex to behave according to the assumptions.

The goal is a robust working utility, not blind adherence to a guessed internal implementation.
