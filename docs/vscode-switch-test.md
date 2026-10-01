# Isolated VS Code account-switch test

## Result on 1 October 2026

The app's manual-reload flow succeeded in an isolated VS Code profile with Codex CLI 0.159.2 and the Codex extension 26.928.31416. The normal Codex home, normal VS Code profile, and installed app account store were not modified by this test.

1. The switcher ran with `CODEX_HOME=%LOCALAPPDATA%\CodexLbSwitcherExperiment\codex-home` and `CODEX_ACCOUNT_SWITCHER_DATA_HOME=%LOCALAPPDATA%\CodexLbSwitcherExperiment\switcher-store`. The latter contained copies of the app's encrypted account files. The app selected the first Plus account, and the isolated auth file's account identity was verified.
2. The isolated Codex config was changed to the built-in `openai` provider so proxy routing could not choose an account independently of the app. After a VS Code reload, a new conversation in the disposable `test-project` read `context-marker.txt` and answered `violet compass`.
3. The app's **Use account** button selected the second Plus account. The isolated auth file's identity matched that account. Immediately after switching, all 53 captured config, session, skill, and history files were byte-identical; only the auth state changed.
4. **Developer: Reload Window** kept the project open. The earlier conversation appeared in the Codex chat list and reopened in its own webview. The extension's account check used the second Plus identity. A follow-up in that same conversation asked for the earlier phrase without reopening the file; Codex answered `violet compass`. The original session file contained both turns under the same session ID.

This verifies conversation continuity with a manual window reload in the isolated setup. The request used Codex's direct provider with the second Plus auth file active. Server-side billing attribution was not independently captured. The real VS Code profile and a genuinely exhausted first account were not tested.

The extension's first-run walkthrough reappeared after each reload in this isolated profile and had to be advanced before using the composer. This is an extension/profile behavior observed during the test, not an action performed by the switcher.

## Targeted restart test on 1 October 2026

This follow-up tested whether Codex could pick up a changed auth file without reloading the VS Code window. It used the same isolated profile, direct provider, two Plus accounts, and existing conversation. Only one new model turn was sent for the successful path.

- **Restart Extension Host:** After switching the isolated auth file, `Developer: Restart Extension Host` replaced both the extension host and Codex worker while the VS Code window PID and handle stayed the same. The conversation remained visible, but a second Codex pane reported that its user interface could not start. A follow-up stayed in the composer and was not sent. `Developer: Reload Webviews` did not repair that state. A full window reload restored it. This route failed in the tested extension version.
- **Restart only the Codex child:** From a working window, the app switched the isolated auth file to the other Plus account. Terminating only that window's `codex.exe app-server` process left the VS Code window PID and handle unchanged. Codex displayed a **Try again** recovery button. Invoking it started a new Codex worker, reopened the same conversation, and a short follow-up answered `violet compass` from prior context. The direct provider was active, the selected auth file matched the new account, and the same session file contained the new turn. The test was repeated without another model request to confirm that Windows UI Automation can locate and invoke **Try again**.

The child-process route briefly shows an error panel and depends on this extension version's recovery behavior. Terminating a worker during a turn could interrupt that turn.

## Experimental app control

The experiment first used a **Restart Codex only** control. It listed VS Code windows through the parent chain of each Codex `app-server` worker. The user chose a window after finishing all turns. Before stopping anything, the app rechecked the worker PID, start time, window PID, start time, and window handle, and refused a visible active turn. It then stopped only that worker, used Windows UI Automation to invoke the extension's **Try again** button, and verified a replacement worker in the same window. The control was later removed from the main screen when this restart became part of **Use account**.

The new control was exercised in the isolated app with two VS Code windows open. It selected the test project window, stopped only that window's worker, and verified a replacement without changing the window handle or sending another model request. Background turns that are not visible to Windows UI Automation cannot be detected reliably; the control asks the user to confirm that all turns have finished. The recovery text is extension UI text and may change or be localized. The app does not yet prove server-side account attribution after an automated restart. The currently published v1.0.0 release does not include this control.

## Integrated switch in the source build

**Use account** now checks all Codex workers in the matching VS Code profile for a visible active turn before changing auth. It asks the user to confirm that background turns have finished, switches the auth file, restarts each detected worker, and reports which windows need a manual reload if recovery fails. The normal VS Code user-data profile is selected by default. A custom `CODEX_HOME` requires `CODEX_ACCOUNT_SWITCHER_VSCODE_USER_DATA_DIR` so an isolated run cannot restart unrelated VS Code workers. The published v1.0.0 app used the manual reload flow; this integration ships in v1.2.0.
