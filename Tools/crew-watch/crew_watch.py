"""crew-watch: a local, zero-token live view of the running Claude workflow (harbour polish and later runs).

Serves an animated page at http://127.0.0.1:8777 that polls /status.json. Status is computed from the workflow
logs Claude Code already writes (journal.jsonl + agent-*.jsonl under ~/.claude/projects/C--Dev-FishGame/*/subagents/workflows)
and from the newest screenshot round folder. Reads files only; never calls Claude, never writes anything.

Run:  python Tools/crew-watch/crew_watch.py [--port 8777] [--shots <dir with r0, r1, ... folders>]
Binds to 127.0.0.1 only (not reachable from other machines).
"""
import argparse
import calendar
import glob
import json
import os
import re
import time
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer

HERE = os.path.dirname(os.path.abspath(__file__))
PROJECTS = os.path.join(os.path.expanduser("~"), ".claude", "projects", "C--Dev-FishGame")
TEMP_ROOT = os.path.join(os.environ.get("LOCALAPPDATA", ""), "Temp", "claude", "C--Dev-FishGame")

_cache = {}  # agent log path -> {"offset": int, "state": dict}


def newest_run():
    runs = glob.glob(os.path.join(PROJECTS, "*", "subagents", "workflows", "wf_*"))
    runs = [r for r in runs if os.path.isfile(os.path.join(r, "journal.jsonl"))]
    return max(runs, key=lambda r: os.path.getmtime(os.path.join(r, "journal.jsonl"))) if runs else None


def newest_shots(override):
    """Newest round folder (r0, r0b, r1, ...) that already holds PNGs, searched one level into subfolders.
    Returns (round dir, [paths relative to it, newest first])."""
    roots = [override] if override else glob.glob(os.path.join(TEMP_ROOT, "*", "scratchpad", "harbour-polish"))
    best, best_files, best_time = None, [], -1.0
    for root in roots:
        if not (root and os.path.isdir(root)):
            continue
        for d in glob.glob(os.path.join(root, "r*")):
            if not os.path.isdir(d):
                continue
            pngs = glob.glob(os.path.join(d, "*.png")) + glob.glob(os.path.join(d, "*", "*.png"))
            if not pngs:
                continue
            newest = max(os.path.getmtime(p) for p in pngs)
            if newest > best_time:
                best_time = newest
                best = d
                best_files = [os.path.relpath(p, d).replace(os.sep, "/") for p in sorted(pngs, key=os.path.getmtime, reverse=True)[:16]]
    return best, best_files


def classify(tool, detail):
    t, d = tool.lower(), detail.lower()
    if "blender" in t:
        return "sculpt"
    if t in ("bash", "powershell", "monitor"):
        if re.search(r"unity|test-play|test-edit|batchmode|capture", d):
            return "unity"
        if re.search(r"\bgit\b|commit|push", d):
            return "ship"
        if re.search(r"sleep|wait|until|block", d):
            return "wait"
        return "shell"
    if t in ("read", "grep", "glob", "lsp") or t.startswith("mcp__claude_browser"):
        return "read"
    if t in ("edit", "write", "notebookedit"):
        return "build"
    return "think"


def tool_detail(inp):
    if not isinstance(inp, dict):
        return ""
    for k in ("description", "file_path", "pattern", "query", "command", "url"):
        v = inp.get(k)
        if isinstance(v, str) and v.strip():
            v = v.strip().splitlines()[0]
            if k == "file_path":
                v = os.path.basename(v)
            return v[:140]
    code = inp.get("code")
    if isinstance(code, str):
        first = next((ln for ln in code.splitlines() if ln.strip() and not ln.strip().startswith("#")), "")
        return ("blender: " + first.strip())[:140]
    return ""


def read_agent(path):
    c = _cache.setdefault(path, {"offset": 0, "state": {"tools": 0, "first": None, "last": None,
                                                        "tool": "", "detail": "", "activity": "think", "text": "",
                                                        "recent": [], "context": 0, "processed": 0, "seen": {}}})
    st = c["state"]
    try:
        size = os.path.getsize(path)
        if size < c["offset"]:
            c["offset"] = 0
        with open(path, "rb") as f:
            f.seek(c["offset"])
            chunk = f.read()
    except OSError:
        return st
    end = chunk.rfind(b"\n")
    if end < 0:
        return st
    c["offset"] += end + 1
    for raw in chunk[: end + 1].splitlines():
        try:
            d = json.loads(raw)
        except ValueError:
            continue
        ts = d.get("timestamp")
        if ts:
            st["first"] = st["first"] or ts
            st["last"] = ts
        m = d.get("message")
        if not isinstance(m, dict) or m.get("role") != "assistant":
            continue
        usage = m.get("usage") or {}
        ctx = sum(int(usage.get(k) or 0) for k in ("input_tokens", "cache_creation_input_tokens", "cache_read_input_tokens"))
        total = ctx + int(usage.get("output_tokens") or 0)
        mid = m.get("id") or raw[:40]
        st["processed"] += total - st["seen"].get(mid, 0)   # a message is logged once per content block: count it once
        st["seen"][mid] = total
        st["context"] = ctx
        content = m.get("content")
        if not isinstance(content, list):
            continue
        for part in content:
            if part.get("type") == "tool_use":
                name = part.get("name", "")
                detail = tool_detail(part.get("input"))
                short = name.split("__")[-1] if name.startswith("mcp__") else name
                st["tools"] += 1
                st["tool"], st["detail"] = short, detail
                st["activity"] = classify(name, detail)
                st["recent"] = ([{"tool": short, "detail": detail, "at": ts}] + st["recent"])[:6]
            elif part.get("type") == "text" and part.get("text", "").strip():
                st["text"] = part["text"].strip().splitlines()[0][:200]
    return st


def iso_to_epoch(ts):
    try:
        return calendar.timegm(time.strptime(ts[:19], "%Y-%m-%dT%H:%M:%S"))   # log timestamps are UTC
    except (TypeError, ValueError):
        return None


def find_scores(obj, out):
    if isinstance(obj, dict):
        if "overall" in obj and "scores" in obj and isinstance(obj.get("scores"), dict):
            out.append({"overall": obj.get("overall"), "scores": obj["scores"], "verdict": str(obj.get("verdict", ""))[:300]})
        for v in obj.values():
            find_scores(v, out)
    elif isinstance(obj, list):
        for v in obj:
            find_scores(v, out)


def status(shots_override):
    run = newest_run()
    now = time.time()
    res = {"now": now, "run": None, "agents": [], "phases": [], "scores": [], "shots": {"dir": None, "files": []}}
    if not run:
        return res
    res["run"] = os.path.basename(run)
    agents, order, phases, ended = {}, [], [], False
    with open(os.path.join(run, "journal.jsonl"), "rb") as f:
        for raw in f:
            try:
                e = json.loads(raw)
            except ValueError:
                continue
            typ, aid = e.get("type"), e.get("agentId")
            if typ in ("completed", "finished", "failed", "error", "cancelled", "aborted", "done", "workflow_completed"):
                if not aid:
                    ended = True
            if not aid:
                continue
            a = agents.get(aid)
            if a is None and typ == "started":
                for other in agents.values():   # a restarted step (same label) supersedes the old, unfinished one
                    if other["label"] == e.get("label") and other["state"] == "running":
                        other["state"] = "stopped"
            if a is None:
                a = agents[aid] = {"id": aid, "label": e.get("label", aid), "phase": e.get("phase", ""), "state": "running"}
                order.append(aid)
            if typ != "started":
                a["state"] = "failed" if typ in ("failed", "error") else "done"
                scores = []
                find_scores(e, scores)
                for s in scores:
                    s["label"] = a["label"]
                    res["scores"].append(s)
            if a["phase"] and a["phase"] not in phases:
                phases.append(a["phase"])
    for aid in order:
        a = agents[aid]
        st = read_agent(os.path.join(run, "agent-" + aid + ".jsonl"))
        first, last = iso_to_epoch(st["first"]), iso_to_epoch(st["last"])
        a.update({k: st[k] for k in ("tools", "context", "processed", "tool", "detail", "activity", "text", "recent")})
        a["elapsed"] = (last if a["state"] != "running" else now) - first if first else 0
        a["quiet"] = now - last if last else None
        if a["state"] == "running" and a["quiet"] is not None and a["quiet"] > 90 and a["activity"] in ("unity", "wait", "shell"):
            a["activity"] = "wait"
        res["agents"].append(a)
    res["phases"] = phases
    res["ended"] = ended or (bool(order) and all(agents[i]["state"] != "running" for i in order) and now - os.path.getmtime(os.path.join(run, "journal.jsonl")) > 600)
    d, files = newest_shots(shots_override)
    res["shots"] = {"dir": os.path.basename(d) if d else None, "files": files}
    return res


class Handler(BaseHTTPRequestHandler):
    shots_override = None

    def log_message(self, *args):
        pass

    def send(self, code, ctype, body):
        self.send_response(code)
        self.send_header("Content-Type", ctype)
        self.send_header("Cache-Control", "no-store")
        self.send_header("Content-Length", str(len(body)))
        self.end_headers()
        self.wfile.write(body)

    def do_GET(self):
        path = self.path.split("?")[0]
        if path in ("/", "/index.html"):
            with open(os.path.join(HERE, "index.html"), "rb") as f:
                return self.send(200, "text/html; charset=utf-8", f.read())
        if path == "/status.json":
            return self.send(200, "application/json", json.dumps(status(self.shots_override)).encode("utf-8"))
        if path.startswith("/shot/"):
            from urllib.parse import unquote
            name = unquote(path[len("/shot/"):])
            d, files = newest_shots(self.shots_override)
            if d and name in files:   # only files the listing returned, so no path tricks
                with open(os.path.join(d, name), "rb") as f:
                    return self.send(200, "image/png", f.read())
        self.send(404, "text/plain", b"not found")


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--port", type=int, default=8777)
    ap.add_argument("--shots", default=None)
    args = ap.parse_args()
    Handler.shots_override = args.shots
    srv = ThreadingHTTPServer(("127.0.0.1", args.port), Handler)
    print("crew-watch on http://127.0.0.1:%d" % args.port, flush=True)
    srv.serve_forever()


if __name__ == "__main__":
    main()
