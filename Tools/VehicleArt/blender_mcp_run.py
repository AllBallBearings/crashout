#!/usr/bin/env python3
"""Minimal client for Blender 5.2's lab_blender_org MCP TCP server on localhost:9876."""
import json
import socket
import sys
from pathlib import Path


def run_code(code: str, host: str = "127.0.0.1", port: int = 9876, timeout: float = 180.0) -> dict:
    s = socket.create_connection((host, port), timeout=10.0)
    s.settimeout(timeout)
    payload = (json.dumps({"type": "execute", "code": code, "strict_json": False}) + "\0").encode("utf-8")
    s.sendall(payload)
    buf = bytearray()
    while True:
        chunk = s.recv(65536)
        if not chunk:
            break
        buf.extend(chunk)
        if b"\0" in buf:
            break
    s.close()
    raw = buf.split(b"\0", 1)[0].decode("utf-8")
    return json.loads(raw)


def main():
    if len(sys.argv) < 2:
        print("Usage: blender_mcp_run.py <script.py | -c 'code'>", file=sys.stderr)
        sys.exit(2)
    if sys.argv[1] == "-c":
        code = sys.argv[2]
    else:
        script_path = Path(sys.argv[1]).resolve()
        code = (
            f"import sys; p = {str(script_path.parent)!r}\n"
            f"if p not in sys.path: sys.path.insert(0, p)\n"
            f"__file__ = {str(script_path)!r}\n"
            f"__name__ = '__main__'\n"
            f"with open(__file__, 'r', encoding='utf-8') as _f:\n"
            f"    exec(compile(_f.read(), __file__, 'exec'), globals())\n"
        )
    resp = run_code(code)
    if resp.get("stdout"):
        print(resp["stdout"], end="" if resp["stdout"].endswith("\n") else "\n")
    if resp.get("stderr"):
        print(resp["stderr"], file=sys.stderr, end="" if resp["stderr"].endswith("\n") else "\n")
    if resp.get("status") != "ok":
        print("ERROR:", resp.get("message", resp), file=sys.stderr)
        sys.exit(1)
    if resp.get("result"):
        print("RESULT:", json.dumps(resp["result"], indent=2))


if __name__ == "__main__":
    main()
