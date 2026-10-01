# Codex Account Switcher v1.2.0

The manual **Restart Codex only** dropdown and buttons have been removed from the main screen. **Use account** handles the restart across detected VS Code windows.

**Use account** now changes the saved Codex credentials and refreshes Codex workers in detected VS Code windows. Wait for all Codex turns to finish before confirming the switch. The windows, projects, and conversations stay open. If recovery fails in a window, the app names that window; reload it manually with **Developer: Reload Window**.

The app matches the normal VS Code user-data profile by default. When running with a custom `CODEX_HOME`, set `CODEX_ACCOUNT_SWITCHER_VSCODE_USER_DATA_DIR` to the matching VS Code profile.

The switch and worker recovery were exercised in an isolated VS Code profile with the Codex extension 26.928.31416. A clean window kept its PID and conversation visible while its Codex worker was replaced. A window already carrying an extension UI startup error did not recover automatically and required a manual reload. The app has not verified server-side account attribution or every Codex extension version.

Install by extracting the self-contained Windows x64 ZIP to a folder and running `CodexAccountSwitcher.exe`. The app's encrypted account store and Codex sessions remain in their existing locations.
