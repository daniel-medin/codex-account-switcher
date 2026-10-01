# Codex Account Switcher v1.2.3

- Shows a prominent countdown until each account's weekly usage resets, alongside the remaining percentage. Select **Refresh usage** to update the countdown.
- Puts accounts with usable 5-hour and weekly allowance first, ordered by the soonest weekly reset. The first usable account is marked **USE FIRST**.
- Removes the diagnostics panel so the account list uses the full window width.

**Use account** still changes the saved Codex credentials and refreshes Codex workers in detected VS Code windows. Finish all Codex turns before confirming a switch. The windows, projects, and conversations stay open. If a window cannot recover, the app names it; run **Developer: Reload Window** there and reopen the conversation.

The worker restart was tested in an isolated VS Code profile with Codex extension 26.928.31416. A window with a preexisting extension UI error required a manual reload. The normal VS Code profile and server-side account attribution have not been verified end to end.

Install by extracting the self-contained Windows x64 ZIP to a folder and running `CodexAccountSwitcher.exe`. The encrypted account store and Codex sessions stay in their existing locations.
