#!/usr/bin/env python3
"""Froglet Analytics - our own analytics server, replacing PostHog (Shombith, 2026-11-05).

    python3 analytics_server.py init                 write ~/.froglet-analytics/config.json with fresh keys (once)
    python3 analytics_server.py serve                run the server (the VM runs it under systemd)
    python3 analytics_server.py nightly              retention, backup, aggregates, the overnight read (the server
                                                     also runs this itself once a day at nightly_hour_utc)
    python3 analytics_server.py rebuild              rebuild aggregates.db now
    python3 analytics_server.py erase <player id>    delete everything about one player
    python3 analytics_server.py key read <name>      add a dashboard read key for a person (printed once)
    python3 analytics_server.py import-posthog --project <id> [--days 365] [--dry-run]
                                                     copy PostHog's history in, once (import_posthog.py; needs
                                                     POSTHOG_PERSONAL_API_KEY with the query:read scope)
    python3 analytics_server.py --self-test          the tests (test_analytics_server.py)

HTTP:
    POST /batch/                 ingest, PostHog's own batch shape ({api_key, batch:[{event, properties, timestamp}]}),
                                 so PostHogAnalyticsSink sends here once its config's host and key point at us
    POST /v1/batch               the same
    GET  /                       the console page (Tools/Analytics/console/; public: it holds no data and asks for a key)
    GET  /healthz                {"ok": true} (public, for uptime checks)
    GET  /v1/me                  {"name", "admin"} for the key sent
    GET  /v1/overview?build=release&days=90[&play=all|solo_vs_ai|solo|multi]   Overview (play: Retention, Matches too)      } Authorization:
    GET  /v1/retention?build=..&days=..              Retention                        } Bearer <read key>
    GET  /v1/matches?build=..&days=..                Matches                          }
    GET  /v1/health?days=30                          Data health                      }
    GET  /v1/audience|ftue|economy|social?build&days Players, FTUE, Economy, Social   }
    GET  /v1/people?build&days&q=                    every player, newest first       } profile readers only:
    GET  /v1/person?id=<player id>                   one player's whole profile       } admin keys + profile_readers
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
import analytics_geo as G  # noqa: E402
import analytics_store as S  # noqa: E402
import analytics_views as V  # noqa: E402

CONFIG_PATH = os.path.expanduser(os.environ.get("FROGLET_ANALYTICS_CONFIG", "~/.froglet-analytics/config.json"))
CONSOLE_DIR = os.path.normpath(os.path.join(HERE, "..", "console"))
CONSOLE_FILES = {"/": ("index.html", "text/html; charset=utf-8"),
                 "/index.html": ("index.html", "text/html; charset=utf-8"),
                 "/console.css": ("console.css", "text/css; charset=utf-8"),
                 "/console.js": ("console.js", "text/javascript; charset=utf-8"),
                 "/themes.css": ("themes.css", "text/css; charset=utf-8"),
                 "/theme.js": ("theme.js", "text/javascript; charset=utf-8")}
CONSOLE_CSP = ("default-src 'none'; script-src 'self'; style-src 'self' 'unsafe-inline' https://fonts.googleapis.com; "
               "font-src https://fonts.gstatic.com; connect-src 'self'; img-src 'self' data:; "
               "base-uri 'none'; form-action 'none'; frame-ancestors 'none'")
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
    "profile_readers": [],             # read-key names that may open player profiles (admin keys always may)
    "geo_csv": "",                     # DB-IP country lite file; default <data_dir>/dbip-country-lite.csv.gz
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


PUBLIC_KEY_FILE = os.path.join(HERE, "ingest_key.public")


def public_ingest_key(path=PUBLIC_KEY_FILE):
    """The write-only key committed to the repo and shipped in every build, so the game reaches the server with
    nothing filled in by hand. Its twin is Assets/Resources/FrogletAnalyticsConfig.asset (a test keeps them equal)."""
    try:
        with open(path, encoding="utf-8") as handle:
            return handle.read().strip()
    except OSError:
        return ""


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


# What `key read|admin <name>` and a hand edit of config.json can change while the server runs.
LIVE_FIELDS = ("read_keys", "admin_keys", "profile_readers", "ingest_keys", "allowed_origins")


class App:
    def __init__(self, config, config_path=None):
        self.config = config
        self.config_path = config_path       # given: keys made after start work without a restart (reload_keys)
        self._config_stamp = self._stamp()
        self.store = S.Store(config["data_dir"], schema_events(config["schema_path"]), config["extra_events"])
        self.limit = RateLimit(config["ingest_per_minute"])
        self.ingest_keys = {f"ingest{i}": k for i, k in enumerate(config["ingest_keys"])}
        public = public_ingest_key()
        if public:                           # the key every build ships with (FrogletAnalyticsConfig.asset)
            self.ingest_keys["public"] = public
        self.geo_path = config["geo_csv"] or os.path.join(config["data_dir"], "dbip-country-lite.csv.gz")
        self.geo = G.CountryLookup(self.geo_path)
        self.stop = threading.Event()

    def _stamp(self):
        try:
            st = os.stat(self.config_path) if self.config_path else None
        except OSError:
            return None
        return (st.st_mtime_ns, st.st_size) if st else None

    def reload_keys(self):
        """Pick up keys written to config.json since the server started (`key read ian` writes the file the running
        server would otherwise never read again, so a new key was refused until a restart). One stat per request."""
        stamp = self._stamp()
        if stamp is None or stamp == self._config_stamp:
            return False
        try:
            fresh = load_config(self.config_path)
        except (OSError, ValueError):
            return False                        # half-written: keep the keys we have and look again next request
        for field in LIVE_FIELDS:
            self.config[field] = fresh[field]
        ingest = {f"ingest{i}": k for i, k in enumerate(fresh["ingest_keys"])}
        if "public" in self.ingest_keys:
            ingest["public"] = self.ingest_keys["public"]
        self.ingest_keys = ingest
        self._config_stamp = stamp
        return True

    @property
    def agg(self):
        return self.store.agg_path

    def rebuild(self):
        plays = self.store.rebuild_aggregates()
        self.store.mark_job("rebuild", f"{plays} plays")
        return plays

    def refresh_geo(self):
        if not self.config.get("geo_csv") and G.stale(self.geo_path):
            try:
                G.refresh(self.geo_path)
                self.geo = G.CountryLookup(self.geo_path)
                self.store.mark_job("geo", "refreshed")
            except Exception as exc:   # no network, or the month's file is not out yet: keep the old one
                self.store.mark_job("geo", f"refresh failed: {type(exc).__name__}")

    def nightly(self):
        self.refresh_geo()
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

        def _profiles_allowed(self):
            admin = _match_key(self._bearer(), app.config["admin_keys"])
            reader = _match_key(self._bearer(), app.config["read_keys"])
            return bool(admin or (reader and reader in app.config["profile_readers"]))

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
            app.reload_keys()
            self.send_response(204)
            self._cors()
            self.end_headers()

        def _static(self, path):
            name, kind = CONSOLE_FILES[path]
            with open(os.path.join(CONSOLE_DIR, name), "rb") as handle:
                body = handle.read()
            self.send_response(200)
            self.send_header("Content-Type", kind)
            self.send_header("Content-Length", str(len(body)))
            self.send_header("Cache-Control", "no-cache")
            self.send_header("Content-Security-Policy", CONSOLE_CSP)
            self.send_header("X-Content-Type-Options", "nosniff")
            self.end_headers()
            self.wfile.write(body)

        def do_GET(self):
            app.reload_keys()
            raw = urlparse(self.path).path
            if raw in CONSOLE_FILES:
                return self._static(raw)
            path = raw.rstrip("/") or "/"
            if path == "/healthz":
                return self._send(200, {"ok": True})
            if self._reader() is None:
                return self._send(401, {"error": "A read key is needed (Authorization: Bearer <key>)."})
            if path == "/v1/me":
                admin = _match_key(self._bearer(), app.config["admin_keys"])
                return self._send(200, {"name": admin or self._reader(), "admin": bool(admin),
                                        "profiles": self._profiles_allowed()})
            if path in ("/v1/people", "/v1/person"):
                if not self._profiles_allowed():
                    return self._send(403, {"error": "Player profiles need an admin key or a profile reader's key."})
                q = parse_qs(urlparse(self.path).query)
                if path == "/v1/person":
                    found = app.store.person((q.get("id") or [""])[0])
                    return self._send(200, found) if found else self._send(404, {"error": "No player with that id."})
                build = (q.get("build") or [""])[0] or None
                days = int((q.get("days") or ["90"])[0]) if (q.get("days") or ["90"])[0].isdigit() else 90
                return self._send(200, app.store.people_list(build, min(days, 400), (q.get("q") or [""])[0]))
            build, days = self._query()
            if build not in S.BUILD_KINDS:
                return self._send(400, {"error": f"build must be one of {list(S.BUILD_KINDS)}"})
            if days not in V.WINDOWS:
                return self._send(400, {"error": f"days must be one of {list(V.WINDOWS)}"})
            play = (parse_qs(urlparse(self.path).query).get("play") or ["all"])[0] or "all"
            if play not in V.PLAYS:
                return self._send(400, {"error": f"play must be one of {list(V.PLAYS)}"})
            try:
                if path == "/v1/overview":
                    page = V.overview(app.agg, build, days, play)
                    page["digest"] = app.store.latest_digest()
                    return self._send(200, page)
                if path == "/v1/retention":
                    return self._send(200, V.retention(app.agg, build, days, play))
                if path == "/v1/matches":
                    return self._send(200, V.matches(app.agg, build, days, play))
                if path == "/v1/health":
                    return self._send(200, V.health(app.agg, app.store.health_facts(), days))
                if path in ("/v1/audience", "/v1/ftue", "/v1/economy", "/v1/social"):
                    view = {"/v1/audience": V.audience, "/v1/ftue": V.ftue, "/v1/economy": V.economy,
                            "/v1/social": V.social}[path]
                    return self._send(200, view(app.agg, build, days))
                if path == "/v1/digest":
                    return self._send(200, app.store.latest_digest() or {})
            except Exception:
                traceback.print_exc()
                return self._send(500, {"error": "The server failed to build that page; see its log."})
            return self._send(404, {"error": "No such page."})

        def do_POST(self):
            app.reload_keys()
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
            return self._send(200, {"status": 1, **app.store.ingest(batch, country=app.geo.country(client))})

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
    app = App(config, CONFIG_PATH)
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
    if command == "import-posthog":
        import argparse
        import import_posthog as P
        parser = argparse.ArgumentParser(prog="analytics_server.py import-posthog")
        parser.add_argument("--project", required=True, help="the PostHog project id (Project settings)")
        parser.add_argument("--days", type=int, default=365)
        parser.add_argument("--host", default=P.DEFAULT_HOST)
        parser.add_argument("--dry-run", action="store_true")
        code = P.main(app.store, config, parser.parse_args(argv[1:]))
        if code == 0 and "--dry-run" not in argv:
            print(f"{app.rebuild()} plays after the import")
        return code
    if command == "erase" and len(argv) == 2:
        print(f"{app.store.erase(argv[1], 'cli')} rows deleted")
        app.rebuild()
        return 0
    print(__doc__)
    return 2


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
