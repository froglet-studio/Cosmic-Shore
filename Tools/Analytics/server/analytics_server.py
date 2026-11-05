#!/usr/bin/env python3
"""Froglet Analytics - our own analytics server, replacing PostHog (Shombith, 2026-11-05).

    python3 analytics_server.py init                 write ~/.froglet-analytics/config.json with fresh keys (once)
    python3 analytics_server.py serve                run the server (the VM runs it under systemd)
    python3 analytics_server.py nightly              retention, backup, aggregates, the overnight read (the server
                                                     also runs this itself once a day at nightly_hour_utc)
    python3 analytics_server.py rebuild              rebuild aggregates.db now
    python3 analytics_server.py erase <player id>    delete everything about one player
    python3 analytics_server.py key read <name>      add a dashboard read key for a person (printed once)
    python3 analytics_server.py --self-test          the tests (test_analytics_server.py)

HTTP:
    POST /batch/                 ingest, PostHog's own batch shape ({api_key, batch:[{event, properties, timestamp}]}),
                                 so PostHogAnalyticsSink sends here once its config's host and key point at us
    POST /v1/batch               the same
    GET  /healthz                {"ok": true} (public, for uptime checks)
    GET  /v1/overview?build=release&days=90          the console's Overview page      } Authorization:
    GET  /v1/retention?build=..&days=..              Retention                        } Bearer <read key>
    GET  /v1/matches?build=..&days=..                Matches                          }
    GET  /v1/health?days=30                          Data health                      }
    GET  /v1/digest                                  the latest overnight read        }
    POST /v1/ask        {"question": "..."}          Ask Claude                       }
    POST /v1/erase      {"player_id": "..."}         Erasure (admin key)

Data lives in data_dir (events.db raw, aggregates.db for reading). Standard library only, except the two Claude
features, which need `pip install anthropic` and ANTHROPIC_API_KEY. Run behind Caddy for HTTPS
(setup-analytics-vm.sh). README.md beside this file has the whole picture.
"""
import gzip
import hmac
import json
import os
import secrets
import sys
import threading
import time
import traceback
from datetime import datetime, timezone
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer
from urllib.parse import parse_qs, urlparse

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)

import analytics_claude as C  # noqa: E402
import analytics_store as S  # noqa: E402
import analytics_views as V  # noqa: E402

CONFIG_PATH = os.path.expanduser(os.environ.get("FROGLET_ANALYTICS_CONFIG", "~/.froglet-analytics/config.json"))
REPO_SCHEMA = os.path.normpath(os.path.join(HERE, "..", "..", "..", "Docs", "Analytics", "EVENT_SCHEMA.json"))
MAX_BODY = 2 * 1024 * 1024
MAX_BATCH = 1000
REBUILD_EVERY_S = 300

DEFAULTS = {
    "listen": "127.0.0.1",
    "port": 8787,
    "data_dir": "~/.froglet-analytics/data",
    "schema_path": REPO_SCHEMA,
    "extra_events": ["client_exception"],
    "ingest_keys": [],
    "read_keys": {},
    "admin_keys": {},
    "allowed_origins": [],
    "retention_days": 395,
    "nightly_hour_utc": 5,
    "claude_model": C.DEFAULT_MODEL,
    "claude_effort": "medium",
    "ask_daily_limit": 50,
    "ingest_per_minute": 600,
}


def load_config(path=CONFIG_PATH):
    config = dict(DEFAULTS)
    if os.path.exists(path):
        with open(path, encoding="utf-8") as handle:
            config.update(json.load(handle))
    config["data_dir"] = os.path.expanduser(config["data_dir"])
    return config


def save_config(config, path=CONFIG_PATH):
    os.makedirs(os.path.dirname(path), exist_ok=True)
    with open(path, "w", encoding="utf-8") as handle:
        json.dump(config, handle, indent=2)
    os.chmod(path, 0o600)


def schema_events(path):
    with open(path, encoding="utf-8") as handle:
        return list(json.load(handle)["events"])


def _match_key(supplied, keys):
    """The name a supplied key belongs to, compared in constant time; None when it matches none."""
    found = None
    for name, key in keys.items():
        if supplied and key and hmac.compare_digest(supplied.encode(), key.encode()):
            found = name
    return found


class RateLimit:
    """A per-client token bucket on ingest: a broken client looping on send cannot fill the disk."""

    def __init__(self, per_minute):
        self.rate = per_minute / 60.0
        self.burst = max(10, per_minute // 4)
        self.buckets = {}
        self.lock = threading.Lock()

    def allow(self, client, cost=1):
        now = time.monotonic()
        with self.lock:
            tokens, last = self.buckets.get(client, (self.burst, now))
            tokens = min(self.burst, tokens + (now - last) * self.rate)
            if tokens < cost:
                self.buckets[client] = (tokens, now)
                return False
            self.buckets[client] = (tokens - cost, now)
            return True


class App:
    def __init__(self, config):
        self.config = config
        self.store = S.Store(config["data_dir"], schema_events(config["schema_path"]), config["extra_events"])
        self.limit = RateLimit(config["ingest_per_minute"])
        self.ingest_keys = {f"ingest{i}": k for i, k in enumerate(config["ingest_keys"])}
        self.stop = threading.Event()

    @property
    def agg(self):
        return self.store.agg_path

    def rebuild(self):
        plays = self.store.rebuild_aggregates()
        self.store.mark_job("rebuild", f"{plays} plays")
        return plays

    def nightly(self):
        gone = self.store.apply_retention(self.config["retention_days"])
        target = self.store.backup()
        self.store.mark_job("backup", os.path.basename(target))
        self.rebuild()
        digest = C.write_digest(self.store, self.agg, self.config["claude_model"], self.config["claude_effort"])
        self.store.mark_job("nightly", f"retention removed {gone}; read by {digest['author']}")
        return {"retention_removed": gone, "backup": target, "digest": digest}

    def background(self):
        """Rebuild the aggregates every few minutes; run the nightly job once a day at nightly_hour_utc."""
        while not self.stop.is_set():
            try:
                self.rebuild()
                now = datetime.now(timezone.utc)
                last = self.store.job("nightly")
                ran_today = last and S.day_of(last["last_ms"]) == now.strftime("%Y-%m-%d")
                if now.hour >= self.config["nightly_hour_utc"] and not ran_today:
                    self.nightly()
            except Exception:
                traceback.print_exc()
            self.stop.wait(REBUILD_EVERY_S)


def make_handler(app):
    class Handler(BaseHTTPRequestHandler):
        server_version = "FrogletAnalytics/1"

        def log_message(self, fmt, *args):  # one short line per request, no bodies, no keys
            sys.stderr.write("%s %s\n" % (self.command, self.path.split("?")[0]))

        # ------------------------------------------------ plumbing

        def _cors(self):
            origin = self.headers.get("Origin")
            if origin and origin in app.config["allowed_origins"]:
                self.send_header("Access-Control-Allow-Origin", origin)
                self.send_header("Vary", "Origin")
                self.send_header("Access-Control-Allow-Headers", "Authorization, Content-Type")
                self.send_header("Access-Control-Allow-Methods", "GET, POST, OPTIONS")

        def _send(self, status, payload):
            body = json.dumps(payload, default=str).encode()
            self.send_response(status)
            self.send_header("Content-Type", "application/json")
            self.send_header("Content-Length", str(len(body)))
            self.send_header("Cache-Control", "no-store")
            self._cors()
            self.end_headers()
            self.wfile.write(body)

        def _body(self):
            length = int(self.headers.get("Content-Length") or 0)
            if length <= 0 or length > MAX_BODY:
                return None
            raw = self.rfile.read(length)
            if (self.headers.get("Content-Encoding") or "").lower() == "gzip":
                raw = gzip.decompress(raw)
                if len(raw) > MAX_BODY * 8:
                    return None
            try:
                return json.loads(raw)
            except (json.JSONDecodeError, UnicodeDecodeError):
                return None

        def _bearer(self):
            value = self.headers.get("Authorization") or ""
            return value[7:].strip() if value.lower().startswith("bearer ") else ""

        def _reader(self):
            return _match_key(self._bearer(), app.config["read_keys"]) or \
                _match_key(self._bearer(), app.config["admin_keys"])

        def _query(self):
            q = parse_qs(urlparse(self.path).query)
            build = (q.get("build") or ["release"])[0]
            try:
                days = int((q.get("days") or ["90"])[0])
            except ValueError:
                days = 90
            return build, days

        # ------------------------------------------------ routes

        def do_OPTIONS(self):
            self.send_response(204)
            self._cors()
            self.end_headers()

        def do_GET(self):
            path = urlparse(self.path).path.rstrip("/") or "/"
            if path == "/healthz":
                return self._send(200, {"ok": True})
            if self._reader() is None:
                return self._send(401, {"error": "A read key is needed (Authorization: Bearer <key>)."})
            build, days = self._query()
            if build not in S.BUILD_KINDS:
                return self._send(400, {"error": f"build must be one of {list(S.BUILD_KINDS)}"})
            if days not in V.WINDOWS:
                return self._send(400, {"error": f"days must be one of {list(V.WINDOWS)}"})
            try:
                if path == "/v1/overview":
                    page = V.overview(app.agg, build, days)
                    page["digest"] = app.store.latest_digest()
                    return self._send(200, page)
                if path == "/v1/retention":
                    return self._send(200, V.retention(app.agg, build, days))
                if path == "/v1/matches":
                    return self._send(200, V.matches(app.agg, build, days))
                if path == "/v1/health":
                    return self._send(200, V.health(app.agg, app.store.health_facts(), days))
                if path == "/v1/digest":
                    return self._send(200, app.store.latest_digest() or {})
            except Exception:
                traceback.print_exc()
                return self._send(500, {"error": "The server failed to build that page; see its log."})
            return self._send(404, {"error": "No such page."})

        def do_POST(self):
            path = urlparse(self.path).path.rstrip("/")
            if path in ("/batch", "/v1/batch", "/capture", "/e"):
                return self._ingest()
            if path == "/v1/ask":
                return self._ask()
            if path == "/v1/erase":
                return self._erase()
            return self._send(404, {"error": "No such endpoint."})

        def _ingest(self):
            client = self.headers.get("X-Forwarded-For", self.client_address[0]).split(",")[0].strip()
            body = self._body()
            if not isinstance(body, dict):
                return self._send(400, {"error": "Send a JSON object: {api_key, batch: [...]}."})
            key = body.get("api_key") or self._bearer()
            if _match_key(key, app.ingest_keys) is None:
                return self._send(401, {"error": "Unknown ingest key."})
            batch = body.get("batch")
            if batch is None and "event" in body:
                batch = [body]                       # PostHog's single-event /capture shape
            if not isinstance(batch, list) or len(batch) > MAX_BATCH:
                return self._send(400, {"error": f"batch must be a list of at most {MAX_BATCH} events."})
            if not app.limit.allow(client, cost=max(1, len(batch) // 20)):
                return self._send(429, {"error": "Too many requests; the client will retry."})
            return self._send(200, {"status": 1, **app.store.ingest(batch)})

        def _ask(self):
            reader = self._reader()
            if reader is None:
                return self._send(401, {"error": "A read key is needed."})
            body = self._body() or {}
            if app.store.asks_today(reader) >= app.config["ask_daily_limit"]:
                return self._send(429, {"error": "Today's questions are used up; the limit resets at 00:00 UTC."})
            result = C.ask(app.agg, body.get("question"), app.config["claude_model"], app.config["claude_effort"])
            app.store.log_ask(reader, str(body.get("question") or "")[:500], result.get("sql"), "error" not in result)
            return self._send(200, result)

        def _erase(self):
            if _match_key(self._bearer(), app.config["admin_keys"]) is None:
                return self._send(401, {"error": "An admin key is needed to erase a player."})
            body = self._body() or {}
            player = body.get("player_id")
            if not isinstance(player, str) or not player:
                return self._send(400, {"error": "Send {\"player_id\": \"...\"}."})
            deleted = app.store.erase(player, "admin")
            app.rebuild()
            return self._send(200, {"rows_deleted": deleted})

    return Handler


def serve(config):
    app = App(config)
    threading.Thread(target=app.background, daemon=True).start()
    server = ThreadingHTTPServer((config["listen"], config["port"]), make_handler(app))
    print(f"Froglet Analytics on http://{config['listen']}:{config['port']} (data in {config['data_dir']})")
    try:
        server.serve_forever()
    finally:
        app.stop.set()


def main(argv):
    if not argv or argv[0] in ("-h", "--help"):
        print(__doc__)
        return 0
    command = argv[0]
    if command == "--self-test":
        import unittest
        suite = unittest.defaultTestLoader.discover(HERE, pattern="test_analytics_server.py")
        return 0 if unittest.TextTestRunner(verbosity=1).run(suite).wasSuccessful() else 1
    config = load_config()
    if command == "init":
        if config["ingest_keys"]:
            print(f"{CONFIG_PATH} already has keys; nothing changed.")
            return 0
        config["ingest_keys"] = ["fa_in_" + secrets.token_urlsafe(24)]
        config["admin_keys"] = {"admin": "fa_admin_" + secrets.token_urlsafe(32)}
        save_config(config)
        print(f"Wrote {CONFIG_PATH}.")
        print("Ingest key (goes in the game's analytics config; it ships in builds, like PostHog's project key):")
        print("  " + config["ingest_keys"][0])
        print("Admin key (erasure; keep it private):")
        print("  " + config["admin_keys"]["admin"])
        return 0
    if command == "key" and len(argv) == 3 and argv[1] in ("read", "admin"):
        field = "read_keys" if argv[1] == "read" else "admin_keys"
        key = ("fa_read_" if argv[1] == "read" else "fa_admin_") + secrets.token_urlsafe(24)
        config[field][argv[2]] = key
        save_config(config)
        print(f"{argv[1]} key for {argv[2]} (shown once): {key}")
        return 0
    if command == "serve":
        serve(config)
        return 0
    app = App(config)
    if command == "rebuild":
        print(f"{app.rebuild()} plays")
        return 0
    if command == "nightly":
        print(json.dumps(app.nightly(), indent=2, default=str))
        return 0
    if command == "erase" and len(argv) == 2:
        print(f"{app.store.erase(argv[1], 'cli')} rows deleted")
        app.rebuild()
        return 0
    print(__doc__)
    return 2


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
