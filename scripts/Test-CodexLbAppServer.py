"""Manual isolated test: continue one Codex app-server thread after proxy routing changes."""

import json
import os
import queue
import shutil
import subprocess
import threading
import time
import urllib.request
from pathlib import Path


ROOT = Path(os.environ["LOCALAPPDATA"]) / "CodexLbSwitcherExperiment"
BASE = "http://127.0.0.1:2455"


def api(method, path, body=None):
    data = None if body is None else json.dumps(body).encode("utf-8")
    request = urllib.request.Request(
        BASE + path,
        data=data,
        method=method,
        headers={"Content-Type": "application/json"},
    )
    with urllib.request.urlopen(request, timeout=15) as response:
        return json.load(response)


def latest_thread_account(thread_id):
    logs = api("GET", "/api/request-logs?limit=50")["requests"]
    for entry in logs:
        if entry["conversationId"] == thread_id and entry["requestKind"] == "normal":
            if entry["status"] != "ok":
                raise RuntimeError(f"Proxy request failed with status {entry['status']}")
            return entry["accountId"]
    raise RuntimeError("No completed proxy request found for thread")


def reader(stream, output):
    for line in stream:
        try:
            output.put(json.loads(line))
        except json.JSONDecodeError:
            pass


def discard(stream):
    for _ in stream:
        pass


class AppServer:
    def __init__(self):
        environment = os.environ.copy()
        environment["CODEX_HOME"] = str(ROOT / "codex-home")
        command = shutil.which("codex")
        if command is None:
            raise RuntimeError("codex is not on PATH")
        self.process = subprocess.Popen(
            [command, "app-server", "--listen", "stdio://", "-c", "model_providers.codex-lb.supports_websockets=false"],
            cwd=ROOT / "test-project",
            env=environment,
            stdin=subprocess.PIPE,
            stdout=subprocess.PIPE,
            stderr=subprocess.PIPE,
            text=True,
            encoding="utf-8",
            bufsize=1,
        )
        self.messages = queue.Queue()
        self.deferred = []
        threading.Thread(target=reader, args=(self.process.stdout, self.messages), daemon=True).start()
        threading.Thread(target=discard, args=(self.process.stderr,), daemon=True).start()

    def send(self, message):
        self.process.stdin.write(json.dumps(message) + "\n")
        self.process.stdin.flush()

    def wait_for(self, predicate, timeout=150):
        deadline = time.monotonic() + timeout
        while time.monotonic() < deadline:
            for index, message in enumerate(self.deferred):
                if predicate(message):
                    return self.deferred.pop(index)
            try:
                message = self.messages.get(timeout=min(1, deadline - time.monotonic()))
            except queue.Empty:
                if self.process.poll() is not None:
                    raise RuntimeError(f"app-server exited with code {self.process.returncode}")
                continue
            if predicate(message):
                return message
            self.deferred.append(message)
        raise TimeoutError("Timed out waiting for app-server response")

    def call(self, number, method, params):
        self.send({"id": number, "method": method, "params": params})
        response = self.wait_for(lambda msg: msg.get("id") == number)
        if "error" in response:
            error = response["error"]
            raise RuntimeError(f"{method} failed: code {error.get('code')}; {error.get('message')}")
        return response["result"]

    def turn(self, number, thread_id, prompt):
        self.call(number, "turn/start", {
            "threadId": thread_id,
            "input": [{"type": "text", "text": prompt}],
        })
        done = self.wait_for(lambda msg: msg.get("method") == "turn/completed")
        turn = done["params"]["turn"]
        if turn["status"] != "completed":
            raise RuntimeError(f"Turn ended with status {turn['status']}")
        chunks = [
            msg.get("params", {}).get("delta", "")
            for msg in self.deferred
            if msg.get("method") == "item/agentMessage/delta"
        ]
        self.deferred = [
            msg for msg in self.deferred
            if msg.get("method") != "item/agentMessage/delta"
        ]
        return "".join(chunks).strip()

    def close(self):
        if self.process.poll() is None:
            self.process.terminate()
            try:
                self.process.wait(timeout=5)
            except subprocess.TimeoutExpired:
                self.process.kill()


def main():
    accounts = api("GET", "/api/accounts")["accounts"]
    active = [account for account in accounts if account["status"] == "active"]
    if len(active) < 2:
        raise RuntimeError("Two active proxy accounts are required")
    original = api("GET", "/api/settings")
    server = None
    paused = False
    try:
        api("PUT", "/api/settings", {
            "routingStrategy": "single_account",
            "singleAccountId": active[0]["accountId"],
            "stickyThreadsEnabled": False,
        })
        server = AppServer()
        server.call(1, "initialize", {
            "clientInfo": {"name": "codex_lb_switch_test", "title": "Codex LB Switch Test", "version": "0.1.0"},
            "capabilities": {},
        })
        server.send({"method": "initialized"})
        result = server.call(2, "thread/start", {
            "cwd": str(ROOT / "test-project"),
            "model": "gpt-5.6-sol",
            "modelProvider": "codex-lb",
            "approvalPolicy": "never",
            "sandbox": "read-only",
            "ephemeral": True,
        })
        thread_id = result["thread"]["id"]
        first = server.turn(3, thread_id, "Remember the phrase violet compass. Reply only READY.")
        first_account = latest_thread_account(thread_id)
        print(f"First turn completed on account 1; answer: {first[:100]}")
        api("POST", f"/api/accounts/{active[0]['accountId']}/pause")
        paused = True
        api("PUT", "/api/settings", {"routingStrategy": "capacity_weighted"})
        second = server.turn(4, thread_id, "What phrase did I ask you to remember? Reply only the phrase.")
        second_account = latest_thread_account(thread_id)
        print(f"Second turn completed after the proxy route change in the same process and thread; answer: {second[:100]}")
        print(f"Context retained: {'violet compass' in second.lower()}")
        if first_account != active[0]["accountId"] or second_account != active[1]["accountId"]:
            raise RuntimeError("Proxy logs did not confirm the intended account switch")
        print("Proxy logs confirm different accounts handled the two turns")
    finally:
        if server is not None:
            server.close()
        if paused:
            api("POST", f"/api/accounts/{active[0]['accountId']}/reactivate")
        api("PUT", "/api/settings", {
            "routingStrategy": original["routingStrategy"],
            "singleAccountId": original.get("singleAccountId"),
            "stickyThreadsEnabled": original["stickyThreadsEnabled"],
        })


if __name__ == "__main__":
    main()
