#!/usr/bin/env python3
"""Froglet Analytics - our own analytics server, replacing PostHog (Shombith, 2026-11-05).

    python3 analytics_server.py init                 write ~/.froglet-analytics/config.json with fresh keys (once)
    python3 analytics_server.py serve                run the server (the VM runs it under systemd)
    python3 analytics_server.py nightly              retention, backup, aggregates, the overnight read (the server
                                                     also runs this itself once a day at nightly_hour_utc)
    python3 analytics_server.py rebuild              rebuild aggregates.db now
    python3 analytics_server.py backup               a consistent, gzipped snapshot of events.db and aggregates.db into
                                                     <data_dir>/backups/ (14 kept); the VM's timer runs it at 04:40 UTC
    python3 analytics_server.py backup --restore <file> [--force]
                                                     put a backup back (stop the service first; the databases it
                                                     replaces are kept in <data_dir>/before-restore-<time>/)
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

  Rev 65 (Shombith, 2026-11-06). era=<id> on overview, retention, matches, playtypes, cohorts, people, series and
  dashboard tiles replaces the days window with the era's range, source and builds:
    GET  /v1/eras[?build=]                 eras with events/players/matches, events per week by era, milestones
    PUT  /v1/eras {"eras": [...], "milestones"?: [...]}           (admin) replaces the list
    GET  /v1/eras/compare?a=&b=[&build=]   two eras side by side
    GET  /v1/cohorts?build&days&play&era   weekly cohorts, lifespan tiles; lifespans (player rows) for profile readers
    GET  /v1/playtypes?build&days&era      solo vs AI / solo / multiplayer, the party funnel
    GET  /v1/series?measure&by&over&build&days&play&era          one switchable chart (400 if not answerable)
    GET  /v1/schema                        (admin) aggregates.db tables, saved tables included
    POST /v1/query {"sql"}                 (admin) the Ask box's read-only guard; aggregates.db only
    GET  /v1/queries | POST (admin) | DELETE ?id= (admin)         saved queries; as_table makes t_<name>
    GET  /v1/dashboards | PUT (admin) | DELETE ?id= (admin)      dashboards of tiles
    GET  /v1/dashboards/tile?source=query:<id>|builtin:<name>&build&days&play&era
    GET  /v1/segments | POST | DELETE ?id= (profile readers)     saved player filters

  2026-11-06 (Shombith): elements, the Toybox, saved views.
    GET  /v1/series?...&by=element         a match's element (its own, else its mode's genre: mode_elements.json)
    GET  /v1/series?measure=freestyle_entries|freestyle_players|freestyle_minutes|freestyle_switches
    GET  /v1/freestyle?build&days&era      Freestyle (Toybox): tiles, per day, vessels, switch pairs, what history exists
    GET  /v1/presets | POST {id?, name, page, chart_id, series, settings, filters, public} | DELETE ?id=
                                           saved chart views: own + public; the owner (or a key in admin_keys) deletes
    GET  /v1/dashboards/tile?source=preset:<id>   a saved view's own series as a tile

  2026-11-07 (Shombith): match setups.
    GET  /v1/overview                      adds setups (multiplayer then solo), setup_history, setup_notes
    GET  /v1/series?...&by=setup           every plays measure (and players, new players) by match setup

  2026-11-07 (Shombith): the web build, build=all and devices (README.md, The web build, All builds and devices).
    build=all|release|editor|port|web|development and device=all|ios|iphone|ipad|android|android_phone|android_tablet|
    windows|mac|linux|web|web_desktop|web_mobile|phone|tablet|desktop on every read route (a bad value is 400)
    GET  /v1/series?...&by=device|os       by device (every measure but Toybox players), by OS family (plays)
    OPTIONS/POST the ingest routes         CORS for ingest_origins (the browser Toybox); read routes: allowed_origins only

  2026-11-07 (Shombith): sessions, balance, quits, invites, errors; backups.
    GET  /v1/sessions|balance|quits|invites|errors?build&days&play&era    the five round-67 pages (README.md)
    GET  /v1/series?measure=score|placement|win_pct|abandon_pct, by=ai_difficulty
    GET  /v1/health                        adds new_events and backups (the last local copy; off-VM is Oracle's policy)

  The Notebook (a chat, a report and a notebook in one page; analytics_notebook.py holds its routes and rules):
    GET|POST|PUT|DELETE /v1/notebooks    POST /v1/notebooks/run (one cell)    POST /v1/notebooks/ask (Claude)

Data lives in data_dir (events.db raw, aggregates.db for reading). Standard library only, except the two Claude
features, which need `pip install anthropic` and ANTHROPIC_API_KEY. Run behind Caddy for HTTPS
(setup-analytics-vm.sh). README.md beside this file has the whole picture.
"""
import gzip
import hmac
import json
import os
import re
import secrets
import sys
import threading
import time
import traceback
from datetime import datetime, timezone
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer
from urllib.parse import parse_qs, parse_qsl, urlparse

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)

import analytics_claude as C  # noqa: E402
import analytics_geo as G  # noqa: E402
import analytics_store as S  # noqa: E402
import analytics_views as V  # noqa: E402
import analytics_notebook as NB  # noqa: E402  the Notebook page's routes

CONFIG_PATH = os.path.expanduser(os.environ.get("FROGLET_ANALYTICS_CONFIG", "~/.froglet-analytics/config.json"))
CONSOLE_DIR = os.path.normpath(os.path.join(HERE, "..", "console"))
CONSOLE_FILES = {"/": ("index.html", "text/html; charset=utf-8"),
                 "/index.html": ("index.html", "text/html; charset=utf-8"),
                 "/console.css": ("console.css", "text/css; charset=utf-8"),
                 "/console.js": ("console.js", "text/javascript; charset=utf-8"),
                 "/themes.css": ("themes.css", "text/css; charset=utf-8"),
                 "/theme.js": ("theme.js", "text/javascript; charset=utf-8"),
                 "/notebook.js": ("notebook.js", "text/javascript; charset=utf-8"),
                 "/notebook.css": ("notebook.css", "text/css; charset=utf-8")}
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
    "allowed_origins": [],             # origins that may READ (the console on another host): every read route
    # Origins whose pages may SEND events from a browser (the WebGL Toybox on play.froglet.games, embedded in
    # www.froglet.games): CORS on the ingest routes only, never on a read route. The ingest key is public anyway.
    "ingest_origins": ["https://play.froglet.games", "https://www.froglet.games"],
    "retention_days": 395,
    "nightly_hour_utc": 5,
    "claude_model": C.DEFAULT_MODEL,
    "claude_effort": "medium",
    "ask_daily_limit": 50,
    "ingest_per_minute": 600,
    "profile_readers": [],             # read-key names that may open player profiles (admin keys always may)
    # Shombith, 2026-11-06: "Give everyone who has a key an admin access there is no seperation between us." While
    # true, every read key is an admin key too (Admin pages, eras, saved queries, dashboards, Erasure, every profile).
    # Set it false in config.json to bring the read / admin split back; profile_readers then applies again.
    "all_keys_admin": True,
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
LIVE_FIELDS = ("read_keys", "admin_keys", "profile_readers", "ingest_keys", "allowed_origins", "ingest_origins",
               "all_keys_admin")


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
        self.rebuild_lock = threading.Lock()

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
        with self.rebuild_lock:            # the background loop and a request (erase, eras, a saved table) share tmp
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

    def backup(self):
        """The nightly backup: a consistent snapshot of both databases, gzipped, 14 kept on the VM; the Oracle
        boot-volume policy copies the disk off the VM at 05:00 UTC. When FA_BACKUP_* are set (they are not, today),
        the snapshot is also PUT to that S3-compatible bucket."""
        target, size = self.store.backup()
        self.store.mark_job("backup", f"{os.path.basename(target)} ({size} bytes)")
        upload = backup_settings()
        result = {"file": target, "bytes": size, "uploaded": None}
        if upload:
            key = upload["prefix"] + os.path.basename(target)
            try:
                with open(target, "rb") as handle:
                    s3_put(upload, key, handle.read())
                result["uploaded"] = f"{upload['bucket']}/{key}"
                self.store.mark_job("backup_upload", f"uploaded to bucket {upload['bucket']} as {key}")
            except Exception as exc:          # the local copy stands; the next night tries again
                result["upload_error"] = f"{type(exc).__name__}: {str(exc)[:200]}"
                self.store.mark_job("backup_upload", f"upload failed ({type(exc).__name__}); the local copy is kept")
        return result

    def nightly(self):
        self.refresh_geo()
        gone = self.store.apply_retention(self.config["retention_days"])
        last = self.store.job("backup")
        if last and S.now_ms() - last["last_ms"] < 20 * 3600 * 1000:
            target = {"skipped": "the 04:40 UTC backup timer already made tonight's"}
        else:
            target = self.backup()                 # no timer on this machine (a local run): back up here
        self.rebuild()
        try:                                       # Claude's read never costs the night its retention, backup or rebuild
            digest = C.write_digest(self.store, self.agg, self.config["claude_model"], self.config["claude_effort"])
        except Exception as exc:
            digest = {"author": f"nobody (the read failed: {type(exc).__name__})"}
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


# ---------------------------------------------------------------- backups to an S3-compatible bucket (dormant)
# Shombith, 2026-11-07: off-VM copies come from the Oracle boot-volume backup policy, with no key on the VM. This upload
# stays for the day a bucket is wanted: it runs only when FA_BACKUP_ENDPOINT, FA_BACKUP_BUCKET, FA_BACKUP_ACCESS_KEY and
# FA_BACKUP_SECRET_KEY are all set (in /etc/froglet-analytics.env, never config.json). Nothing logs or returns a key.

BACKUP_ENV = ("FA_BACKUP_ENDPOINT", "FA_BACKUP_BUCKET", "FA_BACKUP_ACCESS_KEY", "FA_BACKUP_SECRET_KEY")


def backup_settings(env=None):
    env = os.environ if env is None else env
    if not all(env.get(k) for k in BACKUP_ENV):
        return None
    return {"endpoint": env["FA_BACKUP_ENDPOINT"].rstrip("/"), "bucket": env["FA_BACKUP_BUCKET"],
            "access": env["FA_BACKUP_ACCESS_KEY"], "secret": env["FA_BACKUP_SECRET_KEY"],
            "region": env.get("FA_BACKUP_REGION") or "us-ashburn-1",
            "prefix": env.get("FA_BACKUP_PREFIX", "froglet-analytics/")}


def sigv4_headers(method, url, headers, payload_hash, access, secret, region, amz_date, service="s3"):
    """AWS Signature Version 4 (header form), standard library only. `headers` are the ones to sign besides host,
    x-amz-content-sha256 and x-amz-date, which this adds. Returns the headers to send, Authorization included."""
    import hashlib
    from urllib.parse import quote, urlsplit
    parts = urlsplit(url)
    signed = {k.lower(): str(v).strip() for k, v in headers.items()}
    signed.update({"host": parts.netloc, "x-amz-content-sha256": payload_hash, "x-amz-date": amz_date})
    names = sorted(signed)
    pairs = sorted((quote(k, safe="-_.~"), quote(v, safe="-_.~")) for k, v in parse_qsl(parts.query, keep_blank_values=True))
    # the path is sent as the caller encoded it (s3_put quotes the key once), as S3 signs it: no second encoding
    canonical = "\n".join([method, parts.path or "/", "&".join(f"{k}={v}" for k, v in pairs),
                           "".join(f"{n}:{signed[n]}\n" for n in names), ";".join(names), payload_hash])
    scope = f"{amz_date[:8]}/{region}/{service}/aws4_request"
    to_sign = "\n".join(["AWS4-HMAC-SHA256", amz_date, scope, hashlib.sha256(canonical.encode()).hexdigest()])
    key = ("AWS4" + secret).encode()
    for part in (amz_date[:8], region, service, "aws4_request"):
        key = hmac.new(key, part.encode(), hashlib.sha256).digest()
    signature = hmac.new(key, to_sign.encode(), hashlib.sha256).hexdigest()
    out = dict(signed)
    out["authorization"] = (f"AWS4-HMAC-SHA256 Credential={access}/{scope}, SignedHeaders={';'.join(names)}, "
                            f"Signature={signature}")
    out.pop("host")
    return out


def s3_put(settings, key, body, now=None):
    """PUT one object, path style ({endpoint}/{bucket}/{key}), as Oracle Object Storage's S3 endpoint wants it."""
    import hashlib
    import urllib.request
    from urllib.parse import quote
    url = f"{settings['endpoint']}/{quote(settings['bucket'], safe='')}/{quote(key, safe='/-_.~')}"
    amz_date = (now or datetime.now(timezone.utc)).strftime("%Y%m%dT%H%M%SZ")
    headers = sigv4_headers("PUT", url, {"content-type": "application/gzip"}, hashlib.sha256(body).hexdigest(),
                            settings["access"], settings["secret"], settings["region"], amz_date)
    request = urllib.request.Request(url, data=body, method="PUT", headers=headers)
    with urllib.request.urlopen(request, timeout=120) as response:
        return response.status


# ---------------------------------------------------------------- Rev 65 routes: (status, payload) from a handler

ADMIN_ONLY = (403, {"error": "That needs an admin key."})
PROFILES_ONLY = (403, {"error": "Player profiles need an admin key or a profile reader's key."})


def _app(h):
    return h.app_ref


def _people(h):
    q = h._params()
    build = q.get("build") or None
    if build and build not in V.BUILD_FILTERS:
        raise S.Invalid(f"build must be one of {list(V.BUILD_FILTERS)}")
    build = None if build == "all" else build
    device = h._device_param(q)
    days = int(q["days"]) if (q.get("days") or "").isdigit() else 90
    play = q.get("play") or "all"
    if play not in V.PLAYS:
        raise S.Invalid(f"play must be one of {list(V.PLAYS)}")
    era = h._era_param(q)
    extra = {}
    if era:
        extra = {"since": era["from"], "until": min(era["to"], S.day_of(S.now_ms())), "source": era["source"],
                 "builds": era["builds"] or None}
    page = _app(h).store.people_list(build, min(max(days, 1), 400), q.get("q") or "", play=play,
                                     devices=S.DEVICE_GROUPS.get(device), **extra)
    page["era"] = era["id"] if era else None
    page["device"] = device
    page["unfiltered"] = []
    return 200, page


def _build_or_every(q):
    """The eras pages' build: one kind, or None for every build (no build, or build=all)."""
    build = q.get("build") or None
    if build and build not in V.BUILD_FILTERS:
        raise S.Invalid(f"build must be one of {list(V.BUILD_FILTERS)}")
    return None if build == "all" else build


def _eras_get(h):
    q = h._params()
    build = _build_or_every(q)
    store = _app(h).store
    return 200, V.eras_page(_app(h).agg, store.eras(), store.milestones(), build, h._device_param(q))


def _eras_put(h):
    if not h._admin():
        return ADMIN_ONLY
    body = h._body()
    if not isinstance(body, dict) or "eras" not in body:
        raise S.Invalid("send {\"eras\": [...], \"milestones\"?: [...]}")
    eras = _app(h).store.set_eras(body["eras"], body.get("milestones"), h._admin())
    _app(h).rebuild()                         # the era spans (players per era) are counted in the rebuild
    return 200, {"eras": eras, "milestones": _app(h).store.milestones()}


def _eras_compare(h):
    q = h._params()
    a, b = h._era_param(q, "a"), h._era_param(q, "b")
    if not a or not b:
        raise S.Invalid("name two eras: ?a=<era id>&b=<era id>")
    return 200, V.eras_compare(_app(h).agg, a, b, _build_or_every(q), h._device_param(q))


def _cohorts(h):
    build, days, play, era, device = h._scope_params(h._params())
    page = V.cohorts(_app(h).agg, build, days, play, era, device)
    if h._profiles_allowed():
        sc = V.Scope(build, days, play, era, device=device)
        page["lifespans"] = _app(h).store.lifespans(None if build == "all" else build, sc.since, sc.until, sc.source,
                                                    era["builds"] or None if era else None, play,
                                                    devices=S.DEVICE_GROUPS.get(device))
    else:
        page["unfiltered"] = page.get("unfiltered", []) + ["lifespans"]
    return 200, page


def _playtypes(h):
    build, days, play, era, device = h._scope_params(h._params())
    page = V.playtypes(_app(h).agg, build, days, era, device)
    if play != "all":
        page["unfiltered"] = ["play"]           # this page is the play types side by side
    return 200, page


def _series(h):
    q = h._params()
    build, days, play, era, device = h._scope_params(q)
    return 200, V.series(_app(h).agg, q.get("measure") or "matches", q.get("by") or "none", q.get("over") or "day",
                         build, days, play, era, _app(h).store.eras(), device)


def _freestyle(h):
    build, days, play, era, device = h._scope_params(h._params())
    return 200, V.freestyle(_app(h).agg, build, days, play, era, device)


def _moderator(h):
    """A key in admin_keys itself. all_keys_admin makes every key an admin for the pages, but a saved view stays its
    owner's: only a real admin key may delete someone else's public one."""
    return _match_key(h._bearer(), _app(h).config["admin_keys"]) is not None


def _presets_get(h):
    return 200, {"presets": _app(h).store.presets(h._reader())}


def _presets_post(h):
    store = _app(h).store
    return 200, store.save_preset(h._body(), h._reader(), store.eras())


def _presets_delete(h):
    done = _app(h).store.delete_preset(h._params().get("id") or "", h._reader(), _moderator(h))
    if done is None:
        return 404, {"error": "No saved view with that id that this key may see."}
    return 200, {"deleted": h._params()["id"]}


def _saved_tables(store):
    return {q["table"] for q in store.queries() if q["as_table"]}


def _schema(h):
    if not h._admin():
        return ADMIN_ONLY
    return 200, V.schema(_app(h).agg, _saved_tables(_app(h).store))


def _query_run(h):
    if not h._admin():
        return ADMIN_ONLY
    body = h._body()
    if not isinstance(body, dict) or not isinstance(body.get("sql"), str):
        raise S.Invalid("send {\"sql\": \"SELECT ...\"}")
    out = V.run_readonly_sql(_app(h).agg, body["sql"])
    return 200, {"columns": out["columns"], "rows": out["rows"], "ms": out["ms"], "truncated": out["truncated"]}


def _queries_get(h):
    admin = h._admin()
    return 200, {"queries": _app(h).store.queries(None if admin else h._reader(), bool(admin))}


def _queries_post(h):
    if not h._admin():
        return ADMIN_ONLY
    app = _app(h)
    saved = app.store.save_query(h._body(), h._admin(), lambda sql: V.run_readonly_sql(app.agg, sql))
    if saved["as_table"]:
        app.rebuild()                          # a save makes the table now; the rebuild keeps it fresh after
    return 200, saved


def _queries_delete(h):
    if not h._admin():
        return ADMIN_ONLY
    app = _app(h)
    found = app.store.query(h._params().get("id") or "")
    if not found:
        return 404, {"error": "No saved query with that id."}
    app.store.delete_query(found["id"])
    if found["as_table"]:
        app.rebuild()
    return 200, {"deleted": found["id"]}


def _dashboards_get(h):
    return 200, {"dashboards": _app(h).store.dashboards(h._admin() or h._reader(), bool(h._admin()))}


def _dashboards_put(h):
    if not h._admin():
        return ADMIN_ONLY
    return 200, _app(h).store.save_dashboard(h._body(), h._reader())


def _dashboards_delete(h):
    if not h._admin():
        return ADMIN_ONLY
    if not _app(h).store.delete_dashboard(h._params().get("id") or ""):
        return 404, {"error": "No dashboard with that id."}
    return 200, {"deleted": h._params()["id"]}


def _tile(h):
    q = h._params()
    build, days, play, era, device = h._scope_params(q)
    source = q.get("source") or ""
    app = _app(h)
    if source.startswith("builtin:"):
        if source[8:] not in S.BUILTIN_TILES:
            raise S.Invalid(f"builtin tiles are {', '.join(S.BUILTIN_TILES)}")
        return 200, V.builtin_tile(app.agg, source[8:], build, days, play, era, device)
    if source.startswith("preset:"):
        found = app.store.preset(source[7:], h._reader())
        if not found:
            return 404, {"error": "No saved view with that id that this key may see."}
        era_id = found["filters"].get("era")
        era = app.store.era(era_id) if era_id else None
        if era_id and era is None:
            raise S.Invalid(f"this saved view's era {era_id} no longer exists")
        return 200, V.preset_tile(app.agg, found, era, app.store.eras())
    if source.startswith("query:"):
        admin = h._admin()
        visible = {x["id"]: x for x in app.store.queries(None if admin else h._reader(), bool(admin))}
        found = visible.get(source[6:])
        if not found:
            return 404, {"error": "No saved query with that id that this key may read."}
        out = V.run_readonly_sql(app.agg, found["sql"])
        # a saved query is its own SQL: the page's build, days, play and era do not reach it
        return 200, {"columns": out["columns"], "rows": out["rows"], "truncated": out["truncated"],
                     "unfiltered": ["build", "days", "play", "era", "device"]}
    raise S.Invalid("source must be query:<saved query id>, preset:<saved view id> or builtin:<name>")


def _segments_get(h):
    if not h._profiles_allowed():
        return PROFILES_ONLY
    name = h._admin() or h._reader()
    return 200, {"segments": _app(h).store.segments(name, bool(h._admin()))}


def _segments_post(h):
    if not h._profiles_allowed():
        return PROFILES_ONLY
    return 200, _app(h).store.save_segment(h._body(), h._admin() or h._reader(), bool(h._admin()))


def _segments_delete(h):
    if not h._profiles_allowed():
        return PROFILES_ONLY
    done = _app(h).store.delete_segment(h._params().get("id") or "", h._admin() or h._reader(), bool(h._admin()))
    if done is None:
        return 404, {"error": "No segment with that id."}
    return 200, {"deleted": h._params()["id"]}


def _r67_page(view):
    """A round-67 page (Shombith, 2026-11-07): sessions, balance, quits, invites, errors. Same filters as the rest."""
    def route(h):
        build, days, play, era, device = h._scope_params(h._params())
        return 200, view(_app(h).agg, build, days, play, era, device)
    return route


R67_GET = {"/v1/sessions": _r67_page(V.sessions), "/v1/balance": _r67_page(V.balance), "/v1/quits": _r67_page(V.quits),
           "/v1/invites": _r67_page(V.invites), "/v1/errors": _r67_page(V.errors)}

REV65_GET = {"/v1/eras": _eras_get, "/v1/eras/compare": _eras_compare, "/v1/cohorts": _cohorts,
             "/v1/playtypes": _playtypes, "/v1/series": _series, "/v1/schema": _schema, "/v1/queries": _queries_get,
             "/v1/dashboards": _dashboards_get, "/v1/dashboards/tile": _tile, "/v1/segments": _segments_get,
             "/v1/freestyle": _freestyle, "/v1/presets": _presets_get}
REV65_POST = {"/v1/query": _query_run, "/v1/queries": _queries_post, "/v1/segments": _segments_post,
              "/v1/presets": _presets_post}
REV65_PUT = {"/v1/eras": _eras_put, "/v1/dashboards": _dashboards_put}
REV65_DELETE = {"/v1/queries": _queries_delete, "/v1/dashboards": _dashboards_delete,
                "/v1/segments": _segments_delete, "/v1/presets": _presets_delete}
REV65_GET.update(R67_GET)
# The Notebook (analytics_notebook.py) mounts its routes into the tables above.
for _routes, _nb in ((REV65_GET, NB.GET), (REV65_POST, NB.POST), (REV65_PUT, NB.PUT), (REV65_DELETE, NB.DELETE)):
    _routes.update(_nb)


INGEST_PATHS = ("/batch", "/v1/batch", "/capture", "/e")


def make_handler(app):
    class Handler(BaseHTTPRequestHandler):
        server_version = "FrogletAnalytics/1"
        app_ref = app

        def log_message(self, fmt, *args):  # one short line per request, no bodies, no keys
            sys.stderr.write("%s %s\n" % (self.command, self.path.split("?")[0]))

        # ------------------------------------------------ plumbing

        def _cors(self):
            """Read routes answer CORS for allowed_origins (a console on another host); the ingest routes answer it
            for ingest_origins too (the browser Toybox posting its events). An ingest origin never reads."""
            origin = self.headers.get("Origin")
            if not origin:
                return
            if origin in app.config["allowed_origins"]:
                self.send_header("Access-Control-Allow-Origin", origin)
                self.send_header("Vary", "Origin")
                self.send_header("Access-Control-Allow-Headers", "Authorization, Content-Type")
                self.send_header("Access-Control-Allow-Methods", "GET, POST, PUT, DELETE, OPTIONS")
            elif origin in (app.config.get("ingest_origins") or []) and self._is_ingest():
                self.send_header("Access-Control-Allow-Origin", origin)
                self.send_header("Vary", "Origin")
                self.send_header("Access-Control-Allow-Headers", "Content-Type, Content-Encoding")
                self.send_header("Access-Control-Allow-Methods", "POST, OPTIONS")
                self.send_header("Access-Control-Max-Age", "86400")

        def _is_ingest(self):
            return urlparse(self.path).path.rstrip("/") in INGEST_PATHS

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
            reader = _match_key(self._bearer(), app.config["read_keys"])
            return bool(self._admin() or (reader and reader in app.config["profile_readers"]))

        def _admin(self):
            """The admin's name, or None. With all_keys_admin (the default) any valid key is an admin's."""
            return _match_key(self._bearer(), app.config["admin_keys"]) or \
                (_match_key(self._bearer(), app.config["read_keys"]) if app.config.get("all_keys_admin") else None)

        def _params(self):
            return {k: v[0] for k, v in parse_qs(urlparse(self.path).query).items()}

        def _scope_params(self, q):
            """build, days, play, era and device from the query string; S.Invalid (400) on any bad value."""
            build = q.get("build") or "release"
            if build not in V.BUILD_FILTERS:
                raise S.Invalid(f"build must be one of {list(V.BUILD_FILTERS)}")
            days = q.get("days") or "90"
            if not days.isdigit() or int(days) not in V.WINDOWS:
                raise S.Invalid(f"days must be one of {list(V.WINDOWS)}")
            play = q.get("play") or "all"
            if play not in V.PLAYS:
                raise S.Invalid(f"play must be one of {list(V.PLAYS)}")
            return build, int(days), play, self._era_param(q), self._device_param(q)

        def _device_param(self, q):
            device = q.get("device") or "all"
            if device not in V.DEVICES:
                raise S.Invalid(f"device must be one of {list(V.DEVICES)}")
            return device

        def _era_param(self, q, name="era"):
            if not q.get(name):
                return None
            era = app.store.era(q[name])
            if era is None:
                raise S.Invalid(f"no era with id {q[name]}")
            return era

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
                admin = self._admin()
                return self._send(200, {"name": admin or self._reader(), "admin": bool(admin),
                                        "profiles": self._profiles_allowed()})
            if path in REV65_GET:
                return self._guarded(REV65_GET[path], self)
            if path in ("/v1/people", "/v1/person"):
                if not self._profiles_allowed():
                    return self._send(403, {"error": "Player profiles need an admin key or a profile reader's key."})
                q = parse_qs(urlparse(self.path).query)
                if path == "/v1/person":
                    found = app.store.person((q.get("id") or [""])[0])
                    return self._send(200, found) if found else self._send(404, {"error": "No player with that id."})
                return self._guarded(_people, self)
            build, days = self._query()
            if build not in V.BUILD_FILTERS:
                return self._send(400, {"error": f"build must be one of {list(V.BUILD_FILTERS)}"})
            if days not in V.WINDOWS:
                return self._send(400, {"error": f"days must be one of {list(V.WINDOWS)}"})
            play = (parse_qs(urlparse(self.path).query).get("play") or ["all"])[0] or "all"
            if play not in V.PLAYS:
                return self._send(400, {"error": f"play must be one of {list(V.PLAYS)}"})
            try:
                era = self._era_param(self._params())
                device = self._device_param(self._params())
            except S.Invalid as exc:
                return self._send(400, {"error": str(exc)})
            try:
                if path == "/v1/overview":
                    page = V.overview(app.agg, build, days, play, era, device)
                    page["digest"] = app.store.latest_digest()
                    return self._send(200, page)
                if path == "/v1/retention":
                    return self._send(200, V.retention(app.agg, build, days, play, era, device))
                if path == "/v1/matches":
                    return self._send(200, V.matches(app.agg, build, days, play, era, device))
                if path == "/v1/health":
                    page = V.health(app.agg, app.store.health_facts(), days)
                    page["unfiltered"] = ["page"] if (play != "all" or era or device != "all") else []
                    return self._send(200, page)
                if path in ("/v1/audience", "/v1/ftue", "/v1/economy", "/v1/social"):
                    view = {"/v1/audience": V.audience, "/v1/ftue": V.ftue, "/v1/economy": V.economy,
                            "/v1/social": V.social}[path]
                    page = view(app.agg, build, days)
                    # these pages read no play type, era or device
                    page["unfiltered"] = ["page"] if (play != "all" or era or device != "all") else []
                    return self._send(200, page)
                if path == "/v1/digest":
                    return self._send(200, app.store.latest_digest() or {})
            except Exception:
                traceback.print_exc()
                return self._send(500, {"error": "The server failed to build that page; see its log."})
            return self._send(404, {"error": "No such page."})

        def do_POST(self):
            app.reload_keys()
            path = urlparse(self.path).path.rstrip("/")
            if path in INGEST_PATHS:
                return self._ingest()
            if path == "/v1/ask":
                return self._ask()
            if path == "/v1/erase":
                return self._erase()
            if path in REV65_POST:
                if self._reader() is None:
                    return self._send(401, {"error": "A read key is needed (Authorization: Bearer <key>)."})
                return self._guarded(REV65_POST[path], self)
            return self._send(404, {"error": "No such endpoint."})

        def do_PUT(self):
            return self._write(REV65_PUT)

        def do_DELETE(self):
            return self._write(REV65_DELETE)

        def _write(self, routes):
            app.reload_keys()
            path = urlparse(self.path).path.rstrip("/")
            if path not in routes:
                return self._send(404, {"error": "No such endpoint."})
            if self._reader() is None:
                return self._send(401, {"error": "A read key is needed (Authorization: Bearer <key>)."})
            return self._guarded(routes[path], self)

        def _guarded(self, route, handler):
            """Run a Rev 65 route: a bad value is 400 with the reason, never a 500."""
            try:
                status, payload = route(handler)
            except (S.Invalid, V.BadRequest, V.UnsafeQuery) as exc:
                return self._send(400, {"error": str(exc)})
            except PermissionError as exc:
                return self._send(403, {"error": str(exc)})
            except Exception:
                traceback.print_exc()
                return self._send(500, {"error": "The server failed to build that page; see its log."})
            return self._send(status, payload)

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
            if self._admin() is None:
                return self._send(401, {"error": "An admin key is needed to erase a player."})
            body = self._body() or {}
            player = body.get("player_id")
            if not isinstance(player, str) or not player:
                return self._send(400, {"error": "Send {\"player_id\": \"...\"}."})
            deleted = app.store.erase(player, "admin")
            app.rebuild()
            return self._send(200, {"rows_deleted": deleted})

    return Handler


KEY_NAME = re.compile(r"^[a-z0-9_.-]{1,40}$")


def key_command(config, args):
    """`key read <name>` | `key admin <name>` | `key delete <name>` | `key list`. Returns (what to print, whether the
    config changed). A read key may not be named "admin" and a name is one kind of key only: a read key named "admin"
    (made by `key read admin` while looking for the admin key) signed Garrett in as a reader called "admin", with no
    Admin pages and no profiles (2026-11-06). Secrets are printed once, when made, and never by `list`."""
    action = args[0]
    if action == "list" and len(args) == 1:
        rows = [(n, "admin") for n in sorted(config["admin_keys"])] + [(n, "read") for n in sorted(config["read_keys"])]
        readers = set(config.get("profile_readers") or [])
        everyone = config.get("all_keys_admin")
        lines = [f"  {n:<20} {kind} key" + ("" if kind == "admin" else
                 " (an admin's too: all_keys_admin)" if everyone else (", opens profiles" if n in readers else ", no profiles"))
                 for n, kind in rows]
        return "Keys (names only; a secret is shown once, when it is made):\n" + ("\n".join(lines) or "  none"), False
    usage = "use: key read <name> | key admin <name> | key delete [read|admin] <name> | key list"
    if action == "delete" and len(args) == 3 and args[1] in ("read", "admin"):
        kinds, args = [args[1] + "_keys"], [action, args[2]]
    else:
        kinds = ["read_keys", "admin_keys"]
    if len(args) != 2 or action not in ("read", "admin", "delete"):
        raise ValueError(usage)
    name = args[1].strip().lower()
    if not KEY_NAME.match(name):
        raise ValueError("a name is 1-40 of a-z, 0-9, '.', '_' or '-' (use the person's name, e.g. garrett)")
    if action == "delete":
        found = [k for k in kinds if name in config[k]]
        if not found:
            raise ValueError(f"no key named {name} (see: key list)")
        if len(found) > 1:      # e.g. the admin key and a stray read key both named "admin": never delete both at once
            raise ValueError(f"{name} is both a read key and an admin key; say which: key delete read {name} or key delete admin {name}")
        if found[0] == "admin_keys" and len(config["admin_keys"]) == 1:
            raise ValueError("that is the only admin key; make another first (key admin <name>) so nobody is locked out")
        del config[found[0]][name]
        return f"deleted the {found[0].split('_')[0]} key named {name}; it stops working at its next request", True
    field, other = ("read_keys", "admin_keys") if action == "read" else ("admin_keys", "read_keys")
    if action == "read" and name == "admin":
        raise ValueError("'admin' is not a reader's name: the admin key was printed by init and is in config.json "
                         "under admin_keys; for a person, use their name (key read garrett, or key admin garrett)")
    if name in config[other]:
        raise ValueError(f"{name} already has a {other.split('_')[0]} key; delete it first (key delete {name})")
    key = ("fa_read_" if action == "read" else "fa_admin_") + secrets.token_urlsafe(24)
    config[field][name] = key
    note = "" if action == "admin" else (
        " Every key is an admin's while all_keys_admin is on (the default), so this one is too." if config.get("all_keys_admin")
        else " It opens player profiles." if name in (config.get("profile_readers") or [])
        else " It cannot open player profiles: add the name to profile_readers for that.")
    return f"{action.upper()} key for {name} (shown once): {key}{note}", True


def server_answers(config, timeout=1.0):
    """True when a server on this config's port answers /healthz (a restore under a running server would be lost)."""
    import urllib.request
    try:
        with urllib.request.urlopen(f"http://{config['listen']}:{config['port']}/healthz", timeout=timeout) as response:
            return response.status == 200
    except Exception:
        return False


def restore_command(config, archive, force=False):
    if server_answers(config) and not force:
        print("restore: the server is running on this data; stop it first (sudo systemctl stop froglet-analytics), "
              "then run this again, then start it")
        return 2
    try:
        out = S.restore_backup(os.path.abspath(archive), config["data_dir"])
    except S.Invalid as exc:
        print(f"restore: {exc}")
        return 2
    print(f"restored {', '.join(out['restored'])} into {config['data_dir']}; the databases it replaced are in "
          f"{out['kept_in']}. Start the server again: sudo systemctl start froglet-analytics")
    return 0


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
        suite = unittest.defaultTestLoader.discover(HERE, pattern="test_analytics_*.py")   # the server's and the Notebook's
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
    if command == "key" and len(argv) >= 2:
        try:
            message, changed = key_command(config, argv[1:])
        except ValueError as err:
            print(f"key: {err}")
            return 2
        if changed:
            save_config(config)
        print(message)
        return 0
    if command == "serve":
        serve(config)
        return 0
    if command == "backup" and len(argv) >= 3 and argv[1] == "--restore":
        return restore_command(config, argv[2], "--force" in argv[3:])
    app = App(config)
    if command == "backup" and len(argv) == 1:
        print(json.dumps(app.backup(), indent=2))
        return 0
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
