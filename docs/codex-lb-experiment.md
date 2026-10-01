# Test codex-lb conversation continuity on Windows

This manual experiment checks whether a Codex conversation in VS Code can use a different ChatGPT account without reloading the window. It does not alter the normal `CODEX_HOME`, VS Code profile, or this switcher's account store.

The launchers keep their state under `%LOCALAPPDATA%\CodexLbSwitcherExperiment`:

- `codex-home`: isolated Codex auth, config, and sessions
- `vscode-user-data` and `vscode-extensions`: isolated VS Code profile
- `proxy-data`: isolated codex-lb account database and encryption key
- `test-project`: disposable workspace

These directories can contain credentials after sign-in. Keep them private. The scripts never clear or overwrite them on a later run.

If credentials are imported from another Codex home or account store, the proxy and Codex may later rotate refresh tokens independently. A stale copy can then require reauthentication. Keep the proxy stopped when the experiment is not in use.

## Start

Prerequisites: `uvx` and `code` on PATH, two ChatGPT accounts available for the test, and ports 2455 and 1455 free.

In one PowerShell terminal, from the repository root, run:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\Start-CodexLbProxy.ps1
```

Leave that terminal open. Open `http://127.0.0.1:2455` and add accounts A and B in the codex-lb dashboard. Perform the browser sign-ins yourself.

In a second PowerShell terminal, run:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\Start-CodexLbVsCode.ps1
```

The second script installs the Codex extension only in the isolated VS Code profile, writes the proxy provider to the isolated `config.toml` on first run, and opens the disposable project with workspace trust disabled for this test window. Sign in to the extension if it asks. Do not copy credentials from the normal Codex home unless you intend to use that account for the experiment.

## Check the same conversation

1. In codex-lb, select the `single_account` routing strategy and choose A.
2. In the isolated VS Code window, start a new Codex conversation. Ask it to read `context-marker.txt`, then have a few more turns. Check codex-lb's request log to confirm A served those turns.
3. Leave that conversation open. In codex-lb, change `single_account` to B. Send another turn in the **same conversation**, asking for the marker phrase and one other detail from an earlier turn.
4. Check the request log again. Record whether the turn succeeded, whether B served it, whether earlier context survived, and whether VS Code needed a reload.
5. For a separate limit-recovery test, use the normal pooled routing strategy and repeat when A actually reaches its usage limit. Disabling A or manually selecting B is only a simulation of that condition.

Passing the manual switch test requires the same conversation to answer through B without a VS Code reload. Passing the limit-recovery test additionally requires that result when A is genuinely exhausted. A new conversation working through B does not establish either result.

When finished, press Ctrl+C in the proxy terminal. The test state remains available for a later run. Do not delete the experiment directory while you still need its sessions or account sign-ins.

Configuration and launch details follow [codex-lb's Codex/IDE setup](https://soju06.github.io/codex-lb/client-setup/) and its [local installation instructions](https://soju06.github.io/codex-lb/getting-started/).

## Result on 2026-10-01

- codex-lb started locally and accepted account imports through its `auth.json` import endpoint. The imports used in-memory multipart requests; no plaintext export file was created. The switcher's five saved entries represent four distinct account identities, and all four are now in the isolated proxy. Two currently have available capacity.
- The isolated Codex home was authenticated using a copy of the active Codex auth file. The normal auth file was only read.
- Codex CLI 0.159.2 completed a request through codex-lb. The proxy request log recorded success.
- The same CLI session ID was resumed after routing was changed to the other account. It answered successfully, and codex-lb's log attributed the resumed request to that other account. The original routing setting was restored.
- The isolated VS Code window opened and its Codex panel activated. A first run walkthrough initially covered the prompt; after the user advanced it, the composer appeared, but the panel showed an out-of-credits warning for its current workspace. The extension reported an authenticated ChatGPT account in its local log, alongside HTTP 403 errors during account setup. No VS Code turn reached codex-lb, so continuation of the same visible conversation without reload remains untested.
- `scripts/Test-CodexLbAppServer.py` kept a single Codex app-server process and thread alive across two turns. It uses the isolated Codex home and proxy. The second turn remembered a phrase from the first. With proxy routing changed between turns, however, both requests were attributed to the first account. Disabling sticky threads and forcing HTTP downstream did not change that result. When the first account was paused to simulate exhaustion, the second turn failed instead of reaching another account; no successful second proxy request was logged. These tests do **not** establish seamless switching in a running Codex process. The script checks proxy request attribution and fails unless it sees distinct accounts.
- After the tests, proxy routing was restored to `capacity_weighted`, sticky threads were re-enabled, and the temporarily paused account was reactivated. The normal Codex home and VS Code profile were not modified.

The isolated Codex home was later changed to the built-in `openai` provider for the separate [manual VS Code reload test](vscode-switch-test.md). To repeat this proxy experiment in the same test home, set `model_provider = "codex-lb"` again in its `config.toml` and reload the isolated VS Code window.
