# Codex Account Switcher

A Windows desktop app for saving multiple ChatGPT/Codex logins and switching the file-based Codex authentication. It refreshes Codex in detected VS Code windows while leaving sessions, skills, configuration, and history in place.

This is an independent utility, not an OpenAI product.

## Install on Windows

1. Install the [Codex VS Code extension](https://marketplace.visualstudio.com/items?itemName=openai.chatgpt) or another Codex CLI installation. The `codex` command must be available to the app for **Add another account**.
2. Download `CodexAccountSwitcher-v1.2.0-win-x64.zip` from [GitHub Releases](https://github.com/daniel-medin/codex-account-switcher/releases). Extract it to a folder you keep, such as `%LOCALAPPDATA%\Programs\CodexAccountSwitcher`.
3. Run `CodexAccountSwitcher.exe`. The release is self-contained; it does not require a separate .NET installation. Windows may ask you to confirm running an unsigned app.
4. To launch it from the desktop, right-click the EXE and choose **Show more options → Send to → Desktop (create shortcut)**. Keep the EXE in its extracted folder and use the shortcut.

The app runs as your Windows user. Registered account credentials are protected with Windows DPAPI for that user and saved under `%LOCALAPPDATA%\CodexAccountSwitcher`. The active Codex login remains in the configured `CODEX_HOME` (normally `%USERPROFILE%\.codex\auth.json`). Do not copy the credential store to another Windows user profile.

## Use

1. Sign in to Codex normally, enter an alias, and select **Import current**.
2. Enter an alias and select **Add another account**. Complete the Codex browser login. This registration uses a temporary `CODEX_HOME` and does not sign out the active account.
3. Finish all active Codex turns, then select **Use account** for the account you want. Confirm the prompt. The app saves the current account's latest auth state, replaces only the active auth file, and restarts Codex in each detected VS Code window. The windows, projects, and conversations stay open.
4. Continue in the same conversation after the app reports that Codex restarted. If recovery fails, the app names the affected window. In that window, use **Ctrl+Shift+P → Developer: Reload Window → Enter** and reopen the conversation.

The automated restart was verified in an isolated VS Code profile with Codex extension 26.928.31416. A preexisting extension UI error prevented recovery in one test window and required a manual reload. A running background turn may not be visible to the app, so finish all turns before confirming the switch. See [the test notes](docs/vscode-switch-test.md) for details. The normal VS Code profile and server-side account attribution have not been tested end to end.

The app supports Codex's file-based ChatGPT auth. Keyring-backed auth and other credential sources are outside v1's supported scope. Usage retrieval depends on undocumented Codex/ChatGPT endpoints and may need updates when those change.

## Build from source

Install the .NET 10 SDK on Windows, then run:

```powershell
dotnet restore CodexAccountSwitcher.sln
dotnet test CodexAccountSwitcher.sln --configuration Release --no-restore
dotnet publish src/CodexAccountSwitcher/CodexAccountSwitcher.csproj --configuration Release --runtime win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:DebugType=None --output artifacts/publish/win-x64
```

The standalone EXE is at `artifacts/publish/win-x64/CodexAccountSwitcher.exe`. The project can also be launched from VS Code with **F5**.

For isolated testing, set `CODEX_HOME`, `CODEX_ACCOUNT_SWITCHER_DATA_HOME`, and `CODEX_ACCOUNT_SWITCHER_VSCODE_USER_DATA_DIR` before starting the app. The second variable redirects its encrypted account store; without it, the app uses `%LOCALAPPDATA%\CodexAccountSwitcher`. The third variable restricts worker restarts to the matching VS Code profile. With a custom `CODEX_HOME`, it is required before switching accounts.

## Release and research notes

The [research notes](docs/codex-research.md) describe Codex auth, refresh, usage, and session behavior. The source and test project are in `src/` and `tests/`. Release builds target Windows x64 and are currently unsigned.

The [isolated codex-lb experiment](docs/codex-lb-experiment.md) records proxy switching tests. A CLI session resumed through another account, but switching within one running app-server process did not succeed. The [isolated VS Code switch test](docs/vscode-switch-test.md) records the successful manual reload test using the app's auth switch.
