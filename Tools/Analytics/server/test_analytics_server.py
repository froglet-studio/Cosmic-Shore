"""Tests for the Froglet Analytics server. Run: python3 analytics_server.py --self-test (standard library only;
Claude is replaced by a fake, so no key and no network are needed)."""
import json
import os
import re
import shutil
import sqlite3
import sys
import tempfile
import threading
import time
import unittest
import urllib.error
import urllib.request
from http.server import ThreadingHTTPServer

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)

import analytics_claude as C  # noqa: E402
import analytics_geo as G  # noqa: E402
import import_posthog as P  # noqa: E402
import analytics_server as A  # noqa: E402
import analytics_store as S  # noqa: E402
import analytics_views as V  # noqa: E402

T0 = S.now_ms() - 2 * 86400 * 1000          # two days ago, so every day falls inside every window


def iso(ms):
    from datetime import datetime, timezone
    return datetime.fromtimestamp(ms / 1000, tz=timezone.utc).strftime("%Y-%m-%dT%H:%M:%S.") + f"{ms % 1000:03d}Z"


def ev(event, player, ms, **props):
    """One event exactly as PostHogAnalyticsSink sends it."""
    base = {"distinct_id": player, "install_id": "dev-" + player, "player_id": player,
            "app_version": "0.2.1", "platform": "WindowsPlayer", "schema_version": 1}
    base.update(props)
    return {"event": event, "properties": base, "timestamp": iso(ms)}


def started(player, match, ms, humans=1, ai=2, **extra):
    props = dict(game_mode="SkimRace", intensity=2, vessel_class="Squirrel")
    props.update(extra)
    return ev("game_started", player, ms, match_id=match, player_count_human=humans, player_count_ai=ai, **props)


def completed(player, match, ms, **extra):
    return ev("game_completed", player, ms, match_id=match, flight_time_seconds=95.5, duration_seconds=110, **extra)


class StoreCase(unittest.TestCase):
    def setUp(self):
        self.dir = tempfile.mkdtemp()
        self.store = S.Store(self.dir, A.schema_events(A.REPO_SCHEMA), ["client_exception"])

    def tearDown(self):
        self.store.db.close()
        shutil.rmtree(self.dir)

    def rows(self, sql, *args):
        return self.store.db.execute(sql, args).fetchall()


class Ingest(StoreCase):
    def test_posthog_batch_is_stored_and_bad_events_are_counted_not_stored(self):
        result = self.store.ingest([
            started("p1", "m1", T0),
            {"event": "not_in_the_schema", "properties": {"distinct_id": "p1"}, "timestamp": iso(T0)},
            {"event": "game_started", "properties": {"match_id": "m2"}, "timestamp": iso(T0)},
            {"event": "game_started", "properties": {"distinct_id": "p1"}, "timestamp": "1999-01-01T00:00:00Z"},
            "not an object",
        ])
        self.assertEqual(result["accepted"], 1)
        self.assertEqual(result["rejected"], 4)
        reasons = {r[0] for r in self.rows("SELECT reason FROM rejects")}
        self.assertEqual(reasons, {"unknown_event", "no_player_id", "bad_timestamp", "not_an_object"})

    def test_a_resent_batch_is_stored_once(self):
        batch = [started("p1", "m1", T0)]
        self.store.ingest(batch)
        self.assertEqual(self.store.ingest(batch)["resent"], 1)
        self.assertEqual(self.rows("SELECT COUNT(*) FROM events")[0][0], 1)

    def test_the_double_send_is_flagged_as_a_near_duplicate(self):
        # The facade bug: same player, same match, 2 ms apart, a fresh event each time.
        out = self.store.ingest([started("p1", "m1", T0), started("p1", "m1", T0 + 2)])
        self.assertEqual(out["near_duplicates"], 1)
        self.assertEqual(self.rows("SELECT SUM(near_dup) FROM events")[0][0], 1)
        # A real rematch a minute later is not a duplicate.
        self.assertEqual(self.store.ingest([started("p1", "m1", T0 + 60_000)])["accepted"], 1)

    def test_build_kind_comes_from_platform_and_version_until_the_client_sends_it(self):
        self.assertEqual(S.build_kind_of({"platform": "WindowsEditor"}), "editor")
        self.assertEqual(S.build_kind_of({"platform": "WindowsPlayer", "app_version": "0.2.0-port"}), "port")
        self.assertEqual(S.build_kind_of({"platform": "Android"}), "release")
        self.assertEqual(S.build_kind_of({"platform": "WindowsEditor", "build_kind": "development"}), "development")

    def test_play_type_splits_coop_and_versus_when_the_team_fields_exist(self):
        self.assertEqual(S.play_type_of({"player_count_human": 1, "player_count_ai": 0}), "solo_alone")
        self.assertEqual(S.play_type_of({"player_count_human": 1, "player_count_ai": 3}), "solo_vs_ai")
        self.assertEqual(S.play_type_of({"player_count_human": 2}), "multi_human_unknown")
        self.assertEqual(S.play_type_of({"player_count_human": 2, "humans_on_other_domains": 0}), "coop")
        self.assertEqual(S.play_type_of({"player_count_human": 2, "humans_on_other_domains": 1,
                                         "humans_on_my_domain": 1}), "adversarial")
        self.assertEqual(S.play_type_of({"player_count_human": 4, "humans_on_other_domains": 2,
                                         "humans_on_my_domain": 2}), "mixed")


class Erasure(StoreCase):
    def test_the_in_game_delete_button_now_really_deletes(self):
        self.store.ingest([started("p1", "m1", T0), started("p2", "m2", T0),
                           {"event": "$identify", "properties": {"distinct_id": "p1", "$set": {"display_name": "Kes"}},
                            "timestamp": iso(T0)}])
        out = self.store.ingest([{"event": "$identify", "timestamp": iso(T0 + 5),
                                  "properties": {"distinct_id": "p1", "$set": {"gdpr_deletion_requested": True}}}])
        self.assertEqual(out["erased"], 1)
        self.assertEqual(self.rows("SELECT COUNT(*) FROM events WHERE distinct_id='p1'")[0][0], 0)
        self.assertEqual(self.rows("SELECT COUNT(*) FROM people WHERE distinct_id='p1'")[0][0], 0)
        self.assertEqual(self.rows("SELECT COUNT(*) FROM events WHERE distinct_id='p2'")[0][0], 1)
        log = self.rows("SELECT player_hash, source FROM erasures")
        self.assertEqual(log[0][1], "in_game_request")
        self.assertNotIn("p1", log[0][0])
        dump = "\n".join(self.store.db.iterdump())
        self.assertNotIn("'p1'", dump)


class Aggregates(StoreCase):
    def seed(self):
        hour = 3600 * 1000
        self.store.ingest([
            # p1 solo vs AI: finishes, rematches 2 min later, finishes again, no rematch
            started("p1", "m1", T0), completed("p1", "m1", T0 + 120_000),
            started("p1", "m2", T0 + 240_000), completed("p1", "m2", T0 + 360_000),
            # p2 solo alone: the double-send, finishes, comes back an hour later (not a rematch)
            started("p2", "m3", T0, ai=0), started("p2", "m3", T0 + 2, ai=0), completed("p2", "m3", T0 + 100_000),
            started("p2", "m4", T0 + hour, ai=0),
            # editor traffic, kept apart
            started("dev", "m5", T0, platform="WindowsEditor"),
            # the port build: starts, never completes
            started("p3", "m6", T0, app_version="0.2.0-port"),
        ])
        return self.store.rebuild_aggregates()

    def test_plays_collapse_duplicates_and_mark_rematches(self):
        self.assertEqual(self.seed(), 6)
        agg = sqlite3.connect(self.store.agg_path)
        rows = {r[0]: r[1:] for r in agg.execute("SELECT match_id, play_type, completed, rematched FROM plays")}
        self.assertEqual(rows["m1"], ("solo_vs_ai", 1, 1))
        self.assertEqual(rows["m2"], ("solo_vs_ai", 1, 0))
        self.assertEqual(rows["m3"], ("solo_alone", 1, 0))
        self.assertEqual(rows["m4"], ("solo_alone", 0, None))
        rpp = agg.execute("SELECT SUM(rows), SUM(plays) FROM daily_plays WHERE build_kind='release'").fetchone()
        self.assertEqual(rpp, (5, 4))   # m3 twice; the port and editor rows are their own builds

    def test_aggregates_hold_no_player_ids(self):
        self.seed()
        dump = "\n".join(sqlite3.connect(self.store.agg_path).iterdump())
        for player in ("p1", "p2", "p3", "dev"):
            self.assertNotIn(f"'{player}'", dump)

    def test_overview_matches_the_seeded_numbers(self):
        self.seed()
        page = V.overview(self.store.agg_path, "release", 90)
        self.assertEqual(page["tiles"]["players"], 2)
        self.assertEqual(page["tiles"]["matches"], 4)
        self.assertEqual(page["tiles"]["finished_pct"], 75.0)
        self.assertEqual(page["tiles"]["rematch_solo_vs_ai_pct"], 50.0)
        self.assertEqual(page["tiles"]["rows_per_play"], 1.25)
        health = {r["build"]: r for r in page["match_health_by_build"]}
        self.assertEqual(health["port"]["finished_pct"], 0.0)
        self.assertEqual(V.overview(self.store.agg_path, "editor", 90)["tiles"]["players"], 1)
        per_day = V.matches(self.store.agg_path, "release", 7)["per_day"]
        self.assertEqual(len(per_day), 7)                      # every day of the range, zeros included
        self.assertEqual(sum(d["matches"] for d in per_day), 4)

    def test_rule_notes_flag_the_double_send(self):
        self.seed()
        notes = C.rule_notes(V.digest_facts(self.store.agg_path))
        self.assertEqual(notes[0]["severity"], "problem")
        self.assertIn("1.25", notes[0]["body"])


class PlayFilter(StoreCase):
    """Shombith, 2026-11-06: "need features to filter solo vs ai, multiplayer"; "need filters and sorting"."""

    def setUp(self):
        super().setUp()
        self.store.ingest([
            started("a", "m1", T0, humans=1, ai=2), completed("a", "m1", T0 + 60_000, player_won=True),
            started("a", "m2", T0 + 120_000, humans=1, ai=0), completed("a", "m2", T0 + 180_000),
            started("b", "m3", T0, humans=2, ai=0, game_mode="Joust"),
            started("c", "m3", T0, humans=2, ai=0, game_mode="Joust"),
            started("b", "m4", T0 + 300_000, humans=1, ai=3),
        ])
        self.store.rebuild_aggregates()

    def test_each_play_group_counts_only_its_own_matches(self):
        agg = self.store.agg_path
        self.assertEqual(V.overview(agg, "release", 7)["tiles"]["matches"], 4)
        self.assertEqual(V.overview(agg, "release", 7, "solo_vs_ai")["tiles"]["matches"], 2)
        self.assertEqual(V.overview(agg, "release", 7, "solo")["tiles"]["matches"], 1)
        multi = V.overview(agg, "release", 7, "multi")
        self.assertEqual((multi["tiles"]["matches"], multi["tiles"]["multi_human_matches"]), (1, 1))
        self.assertIn("players", multi["unfiltered"])           # the console says which tiles ignore the filter
        self.assertEqual(V.overview(agg, "release", 7)["unfiltered"], [])
        self.assertEqual({r["game_mode"] for r in V.matches(agg, "release", 7, "multi")["by_mode"]}, {"Joust"})
        self.assertEqual({r["play_type"] for r in V.retention(agg, "release", 7, "solo")["by_play_type"]},
                         {"solo_alone"})
        with self.assertRaises(ValueError):
            V.overview(agg, "release", 7, "everyone")

    def test_the_players_list_carries_each_players_play_mix_and_wins(self):
        people = {p["distinct_id"]: p for p in self.store.people_list("release", 7)["people"]}
        a, b = people["a"], people["b"]
        self.assertEqual((a["solo_vs_ai"], a["solo"], a["multi"], a["won"]), (1, 1, 0, 1))
        self.assertEqual((b["solo_vs_ai"], b["solo"], b["multi"], b["won"]), (1, 0, 1, 0))


class AskDoor(StoreCase):
    def setUp(self):
        super().setUp()
        self.store.ingest([started("p1", "m1", T0), completed("p1", "m1", T0 + 1000)])
        self.store.rebuild_aggregates()

    def test_a_select_over_the_aggregates_runs(self):
        out = V.run_readonly_sql(self.store.agg_path, "SELECT play_type, COUNT(*) FROM plays GROUP BY play_type")
        self.assertEqual(out["rows"], [["solo_vs_ai", 1]])

    def test_anything_else_is_refused(self):
        for sql in ("DELETE FROM plays", "SELECT 1; DROP TABLE plays", "ATTACH DATABASE 'x' AS y",
                    "PRAGMA table_info(plays)", "SELECT * FROM events", "WITH x AS (SELECT 1) INSERT INTO meta VALUES(1,2)",
                    "SELECT load_extension('x')"):
            with self.assertRaises(V.UnsafeQuery, msg=sql):
                V.run_readonly_sql(self.store.agg_path, sql)

    def test_ask_sends_claude_only_the_question_the_schema_and_aggregate_rows(self):
        sent = []

        def fake_structured(client, model, effort, system, user, schema, max_tokens=4000):
            sent.append(system + "\n" + user)
            if schema is C.SQL_SCHEMA:
                return {"sql": "SELECT play_type, SUM(completed) FROM plays GROUP BY play_type",
                        "can_answer": True, "why_not": ""}, None
            return {"answer": "Solo vs AI finished 1 match.", "caveat": "One match."}, None

        real = (C._client, C._structured)
        C._client, C._structured = (lambda: (object(), None)), fake_structured
        try:
            out = C.ask(self.store.agg_path, "How many matches finished?")
        finally:
            C._client, C._structured = real
        self.assertEqual(out["answer"], "Solo vs AI finished 1 match.")
        self.assertEqual(out["rows"], [["solo_vs_ai", 1]])
        self.assertFalse(any("p1" in text for text in sent))

    def test_ask_without_claude_says_it_is_off(self):
        real = C._client
        C._client = lambda: (None, "the anthropic package is not installed on the server")
        try:
            self.assertIn("off", C.ask(self.store.agg_path, "anything")["error"])
        finally:
            C._client = real


class Details(StoreCase):
    def seed(self):
        hour = 3600 * 1000
        self.store.ingest([
            ev("game_first_launched", "p1", T0), ev("menu_ready", "p1", T0 + 1000, seconds_since_launch=12, is_first=True),
            ev("ftue_step_completed", "p1", T0 + 2000, quest_id="q", phase_index=0, step_id="n1", step_name="Fly",
               seconds_since_launch=40),
            started("p1", "m1", T0 + 3000), completed("p1", "m1", T0 + 90_000, player_won=True),
            ev("crystals_earned", "p1", T0 + 91_000, amount=200, source="game_placement", balance=200),
            ev("crystals_earned", "p1", T0 + 91_020, amount=200, source="game_placement", balance=400),  # paid twice
            ev("vessel_unlocked", "p1", T0 + 95_000, vessel="Manta", cost=300, balance=100),
            ev("setting_changed", "p1", T0 + 96_000, setting="invert_y", value="true"),
            ev("cloud_save_failed", "p1", T0 + 97_000, key="HANGAR_DATA"),
            ev("menu_ready", "p2", T0 + hour, seconds_since_launch=30),
            ev("friend_added", "p2", T0 + hour + 10, friend_id="p1"),
        ], country="SE")
        self.store.ingest([{"event": "$identify", "timestamp": iso(T0),
                            "properties": {"distinct_id": "p1", "$set": {"display_name": "Kestrel"}}}], country="SE")
        self.store.rebuild_aggregates()

    def test_the_new_pages_count_what_was_sent(self):
        self.seed()
        agg = self.store.agg_path
        aud = V.audience(agg, "release", 7)
        self.assertEqual({r["country"]: r["players"] for r in aud["countries"]}, {"SE": 2})
        self.assertEqual([f["players"] for f in aud["funnel"]][:4], [2, 2, 1, 1])
        self.assertEqual(V.ftue(agg, "release", 7)["steps"][0]["players"], 1)
        eco = V.economy(agg, "release", 7)
        self.assertEqual(eco["double_payouts"], {"count": 1, "amount": 200})
        self.assertEqual(eco["totals"]["earned"]["amount"], 400)
        self.assertEqual(V.matches(agg, "release", 7)["by_mode"][0]["win_pct"], 100.0)
        self.assertEqual(V.social(agg, "release", 7)["totals"]["friend_added"], 1)
        self.assertEqual(V.health(agg, {}, 14)["cloud_save_failures"][0]["key"], "HANGAR_DATA")
        dump = "\n".join(sqlite3.connect(agg).iterdump())
        for secret in ("'p1'", "'p2'", "Kestrel"):
            self.assertNotIn(secret, dump)

    def test_a_profile_holds_everything_about_one_player(self):
        self.seed()
        listing = self.store.people_list(days=7)
        self.assertEqual({p["distinct_id"]: p["display_name"] for p in listing["people"]}, {"p1": "Kestrel", "p2": ""})
        self.assertEqual(self.store.people_list(days=7, search="kest")["total"], 1)
        p = self.store.person("p1")
        self.assertEqual((p["display_name"], p["where"]["latest_country"]), ("Kestrel", "SE"))
        self.assertEqual((p["matches"]["total"], p["matches"]["won"]), (1, 1))
        self.assertEqual(p["economy"]["balance"], 100)
        self.assertEqual(p["settings"]["invert_y"]["value"], "true")
        self.assertEqual(p["ftue"][0]["step"], "Fly")
        self.assertIsNone(self.store.person("nobody"))
        self.store.erase("p1", "admin")
        self.assertIsNone(self.store.person("p1"))


class Geo(unittest.TestCase):
    def test_country_lookup_keeps_only_the_code(self):
        folder = tempfile.mkdtemp()
        path = os.path.join(folder, "geo.csv")
        with open(path, "w") as handle:
            handle.write("1.0.0.0,1.0.0.255,AU\n81.0.0.0,81.255.255.255,SE\n2a00:1450::,2a00:1450:ffff::,NL\n")
        geo = G.CountryLookup(path)
        self.assertEqual(geo.country("81.2.3.4"), "SE")
        self.assertEqual(geo.country("2a00:1450::1"), "NL")
        self.assertIsNone(geo.country("10.0.0.1"))
        self.assertIsNone(geo.country("not an ip"))
        self.assertIsNone(G.CountryLookup(os.path.join(folder, "missing.csv")).country("81.2.3.4"))
        shutil.rmtree(folder)


class SchemaCoversTheGame(unittest.TestCase):
    def test_every_event_the_game_sends_is_in_the_schema(self):
        """The server rejects events missing from EVENT_SCHEMA.json; the FTUE events were missing (2026-11-05)."""
        import re
        keys = os.path.join(HERE, "..", "..", "..", "Assets", "_Scripts", "System", "UGSKeys.cs")
        if not os.path.exists(keys):
            self.skipTest("no Unity sources in this checkout (the VM's sparse checkout)")
        sent = set(re.findall(r'public const string Event\w+\s*=\s*"([^"]+)"', open(keys).read()))
        missing = sent - set(A.schema_events(A.REPO_SCHEMA))
        self.assertEqual(missing, set(), "add these to Docs/Analytics/EVENT_SCHEMA.json")


class PostHogHistory(StoreCase):
    def fake_posthog(self, rows_by_day):
        calls = []

        def when(text):
            from datetime import datetime
            return datetime.fromisoformat(text.replace("Z", "+00:00"))

        def post(url, body, key):
            calls.append((url, body["query"]["query"], key))
            q = body["query"]["query"]
            # Like PostHog: OFFSET is refused on queries made with a personal API key (HTTP 400 there).
            self.assertNotIn("OFFSET", q)
            wanted = {n.strip(" '") for n in q.split("event IN (", 1)[1].split(")", 1)[0].split(",")}
            if "count()" in q:
                first = q.split("toDate(timestamp) >= '")[1][:10]
                counts = {d: sum(r[0] in wanted for r in rs) for d, rs in rows_by_day.items() if d >= first}
                return {"columns": ["day", "count()"], "results": [[d, n] for d, n in sorted(counts.items()) if n]}
            day = q.split("toDate(timestamp) = '")[1][:10]
            limit = int(q.rsplit("LIMIT ", 1)[1].split()[0])
            rows = [r for r in rows_by_day.get(day, []) if r[0] in wanted]
            for op in (">", "="):
                marker = f"timestamp {op} toDateTime('"
                if marker in q:
                    at = when(q.split(marker, 1)[1].split("'", 1)[0])
                    rows = [r for r in rows if (when(r[2]) > at if op == ">" else when(r[2]) == at)]
            rows.sort(key=lambda r: when(r[2]))
            return {"columns": ["event", "distinct_id", "timestamp", "properties"], "results": rows[:limit]}
        return post, calls

    def test_paging_by_timestamp_loses_and_repeats_nothing_across_a_page_edge(self):
        base = S.now_ms() - 10 * 86400 * 1000
        base -= base % 86400000 - 3600 * 1000                   # 01:00 UTC, so every row is on one day
        stamps = [base, base + 1, base + 1, base + 1, base + 2, base + 3, base + 3]   # ties straddle each page edge
        rows = {S.day_of(base): [["game_started", f"p{i}", iso(ms), json.dumps({"match_id": f"m{i}"})]
                                 for i, ms in enumerate(stamps)]}
        post, calls = self.fake_posthog(rows)
        page, P.PAGE = P.PAGE, 2
        try:
            totals = P.run(self.store, "123", "phx_test", A.schema_events(A.REPO_SCHEMA), days=12, post=post,
                           log=lambda *_: None)
        finally:
            P.PAGE = page
        self.assertEqual(totals["rows"], len(stamps))
        self.assertEqual((totals["days"], totals["days_short"]), (1, 0))
        self.assertEqual(sorted(r[0] for r in self.rows("SELECT match_id FROM events")),
                         sorted(f"m{i}" for i in range(len(stamps))))

    def test_history_comes_in_once_without_ips_and_keeps_its_own_time(self):
        old = S.now_ms() - 40 * 86400 * 1000
        older = S.now_ms() - 200 * 86400 * 1000                # older than live ingest accepts (90 days)
        day = lambda ms: S.day_of(ms)
        props = lambda **k: json.dumps(dict({"$ip": "81.2.3.4", "$geoip_country_code": "se", "$lib": "web",
                                             "platform": "WindowsPlayer", "app_version": "0.2.1"}, **k))
        rows = {day(old): [["game_started", "p1", iso(old), props(match_id="m1", player_count_human=1)],
                           ["$identify", "p1", iso(old + 5), json.dumps({"$set": {"display_name": "Kes"}})]],
                day(older): [["game_completed", "p1", iso(older), props(match_id="m0")],
                             ["$pageview", "p1", iso(older), props()]]}
        post, calls = self.fake_posthog(rows)
        totals = P.run(self.store, "123", "phx_test", A.schema_events(A.REPO_SCHEMA), days=365, post=post,
                       log=lambda *_: None)
        self.assertEqual(totals["rows"], 3)                     # $pageview is never asked for
        self.assertEqual(totals["accepted"], 3)
        self.assertNotIn("$pageview", calls[0][1])
        dump = "\n".join(self.store.db.iterdump())
        self.assertNotIn("81.2.3.4", dump)
        self.assertNotIn("$lib", dump)
        self.assertEqual(self.rows("SELECT country, received_ms = ts_ms FROM events WHERE match_id='m1'")[0], ("SE", 1))
        self.assertEqual(self.rows("SELECT json_extract(props, '$.display_name') FROM people")[0][0], "Kes")
        again = P.run(self.store, "123", "phx_test", A.schema_events(A.REPO_SCHEMA), days=365, post=post,
                      log=lambda *_: None)
        self.assertEqual(again["resent"], 2)                   # stored once; $identify updates the person again

    def test_a_rate_limited_query_waits_and_asks_again(self):
        answers = [urllib.error.HTTPError("u", 429, "slow", {"Retry-After": "7"}, None),
                   urllib.error.HTTPError("u", 429, "slow", {}, None)]
        waits = []

        class Reply:
            def __enter__(self):
                return self

            def __exit__(self, *_):
                return False

            def read(self):
                return b'{"results": [[1]]}'

        def urlopen(request, timeout):
            if answers:
                raise answers.pop(0)
            return Reply()
        real, P.urllib.request.urlopen = P.urllib.request.urlopen, urlopen
        try:
            self.assertEqual(P._post("https://x/q", {}, "k", sleep=waits.append), {"results": [[1]]})
        finally:
            P.urllib.request.urlopen = real
        self.assertEqual(waits, [7, 60])

    def test_live_http_ingest_cannot_claim_a_country_or_old_time(self):
        older = S.now_ms() - 200 * 86400 * 1000
        item = started("p9", "m9", older)
        item["country"] = "ZZ"
        out = self.store.ingest([item])
        self.assertEqual(out["rejected"], 1)                   # too old for live ingest
        fresh = started("p9", "m10", T0)
        fresh["country"] = "ZZ"
        self.store.ingest([fresh], country="SE")
        self.assertEqual(self.rows("SELECT country FROM events WHERE match_id='m10'")[0][0], "SE")


class PublicKey(unittest.TestCase):
    def test_the_game_and_the_server_ship_the_same_ingest_key(self):
        asset = os.path.join(HERE, "..", "..", "..", "Assets", "Resources", "FrogletAnalyticsConfig.asset")
        if not os.path.exists(asset):
            self.skipTest("no Unity assets in this checkout (the VM's sparse checkout)")
        text = open(asset).read()
        key = text.split("ingestKey:", 1)[1].split("\n", 1)[0].strip()
        self.assertTrue(key.startswith("fa_in_public_"))
        self.assertEqual(key, A.public_ingest_key())
        url = text.split("serverUrl:", 1)[1].split("\n", 1)[0].strip()
        self.assertTrue(url.startswith("https://"), url)


class ConsoleThemes(unittest.TestCase):
    def test_the_console_themes_match_the_game_and_are_served(self):
        console = os.path.join(HERE, "..", "console")
        if not os.path.exists(os.path.join(HERE, "..", "..", "..", "Assets", "_SO_Assets", "Color Palettes")):
            self.skipTest("no Unity assets in this checkout (the VM's sparse checkout)")
        sys.path.insert(0, console)
        import make_themes as T
        self.assertEqual(T.self_test(), 0)
        css, js, _ = T.build()
        self.assertEqual(open(T.OUT_CSS, encoding="utf-8").read(), css, "run Tools/Analytics/console/make_themes.py")
        self.assertEqual(open(T.OUT_JS, encoding="utf-8").read(), js, "run Tools/Analytics/console/make_themes.py")
        for name in ("themes.css", "theme.js"):
            self.assertIn("/" + name, A.CONSOLE_FILES)
            self.assertIn(f'"{name}"', open(os.path.join(console, "index.html"), encoding="utf-8").read().replace("src=", "").replace("href=", ""))


class LiveKeys(unittest.TestCase):
    """Shombith, 2026-11-06: a key made with `key read <name>` on the VM was refused - the running server had read
    config.json once, at start."""

    def test_a_key_made_after_the_server_started_works_without_a_restart(self):
        work = tempfile.mkdtemp()
        try:
            path = os.path.join(work, "config.json")
            config = dict(A.DEFAULTS, data_dir=work, ingest_keys=["in-key"], admin_keys={"admin": "admin-key"})
            A.save_config(config, path)
            app = A.App(A.load_config(path), path)
            server = ThreadingHTTPServer(("127.0.0.1", 0), A.make_handler(app))
            threading.Thread(target=server.serve_forever, daemon=True).start()
            base = f"http://127.0.0.1:{server.server_address[1]}"

            def me(key):
                request = urllib.request.Request(base + "/v1/me", headers={"Authorization": "Bearer " + key})
                try:
                    with urllib.request.urlopen(request) as response:
                        return response.status, json.loads(response.read())
                except urllib.error.HTTPError as err:
                    return err.code, {}
            self.assertEqual(me("fa_read_new")[0], 401)
            later = A.load_config(path)                         # what `key read ian` does, while the server runs
            later["read_keys"]["ian"] = "fa_read_new"
            later["profile_readers"] = ["ian"]
            A.save_config(later, path)
            os.utime(path, ns=(time.time_ns() + 10**9,) * 2)   # a new mtime even on a coarse clock
            status, body = me("fa_read_new")
            self.assertEqual((status, body.get("name"), body.get("profiles")), (200, "ian", True))
            self.assertEqual(me("admin-key")[0], 200)           # the keys it had still work
            server.shutdown()
            server.server_close()
            app.store.db.close()
        finally:
            shutil.rmtree(work)

    def test_the_key_screen_sits_above_the_page(self):
        """The sea lines put the page on z-index 1; a key screen without its own z-index sat under it, visible but
        unclickable, so no key could be typed or pasted."""
        css = open(os.path.join(HERE, "..", "console", "console.css"), encoding="utf-8").read()
        def z(selector):
            block = css.split("\n" + selector + " {", 1)[1].split("}", 1)[0]
            m = re.search(r"z-index:\s*(-?\d+)", block)
            return int(m.group(1)) if m else 0
        self.assertGreater(z(".gate"), z(".shell"))


class Http(unittest.TestCase):
    def setUp(self):
        self.dir = tempfile.mkdtemp()
        config = dict(A.DEFAULTS, data_dir=self.dir, ingest_keys=["in-key"], read_keys={"ian": "read-key"},
                      admin_keys={"admin": "admin-key"}, allowed_origins=["https://dash.example"])
        self.app = A.App(config)
        self.server = ThreadingHTTPServer(("127.0.0.1", 0), A.make_handler(self.app))
        self.base = f"http://127.0.0.1:{self.server.server_address[1]}"
        threading.Thread(target=self.server.serve_forever, daemon=True).start()

    def tearDown(self):
        self.server.shutdown()
        self.server.server_close()
        self.app.store.db.close()
        shutil.rmtree(self.dir)

    def call(self, method, path, body=None, key=None, origin=None):
        data = json.dumps(body).encode() if body is not None else None
        request = urllib.request.Request(self.base + path, data=data, method=method)
        request.add_header("Content-Type", "application/json")
        if key:
            request.add_header("Authorization", "Bearer " + key)
        if origin:
            request.add_header("Origin", origin)
        try:
            with urllib.request.urlopen(request) as response:
                return response.status, json.loads(response.read()), response.headers
        except urllib.error.HTTPError as err:
            return err.code, json.loads(err.read()), err.headers

    def test_the_posthog_sink_can_post_here_unchanged(self):
        status, out, _ = self.call("POST", "/batch/", {"api_key": "in-key", "batch": [started("p1", "m1", T0)]})
        self.assertEqual((status, out["accepted"]), (200, 1))
        status, _, _ = self.call("POST", "/batch/", {"api_key": "wrong", "batch": []})
        self.assertEqual(status, 401)

    def test_pages_need_a_read_key_and_answer_with_cors_for_the_dashboard(self):
        self.call("POST", "/batch/", {"api_key": "in-key", "batch": [started("p1", "m1", T0)]})
        self.app.rebuild()
        self.assertEqual(self.call("GET", "/v1/overview")[0], 401)
        status, page, headers = self.call("GET", "/v1/overview?build=release&days=7", key="read-key",
                                          origin="https://dash.example")
        self.assertEqual(status, 200)
        self.assertEqual(page["tiles"]["matches"], 1)
        self.assertEqual(headers.get("Access-Control-Allow-Origin"), "https://dash.example")
        _, _, other = self.call("GET", "/v1/overview", key="read-key", origin="https://evil.example")
        self.assertIsNone(other.get("Access-Control-Allow-Origin"))
        self.assertEqual(self.call("GET", "/v1/overview?days=3", key="read-key")[0], 400)
        for path in ("/v1/retention", "/v1/matches", "/v1/health", "/v1/digest"):
            self.assertEqual(self.call("GET", path, key="read-key")[0], 200, path)

    def test_erase_needs_the_admin_key(self):
        self.call("POST", "/batch/", {"api_key": "in-key", "batch": [started("p1", "m1", T0)]})
        self.assertEqual(self.call("POST", "/v1/erase", {"player_id": "p1"}, key="read-key")[0], 401)
        status, out, _ = self.call("POST", "/v1/erase", {"player_id": "p1"}, key="admin-key")
        self.assertEqual((status, out["rows_deleted"]), (200, 1))

    def test_the_console_page_is_served_with_a_strict_policy_and_me_names_the_reader(self):
        with urllib.request.urlopen(self.base + "/") as response:
            page = response.read().decode()
            self.assertIn("Froglet Analytics", page)
            self.assertIn("frame-ancestors 'none'", response.headers["Content-Security-Policy"])
        for name in ("/console.js", "/console.css"):
            with urllib.request.urlopen(self.base + name) as response:
                self.assertEqual(response.status, 200)
        self.assertEqual(self.call("GET", "/v1/me", key="read-key")[1], {"name": "ian", "admin": False, "profiles": False})
        self.assertEqual(self.call("GET", "/v1/me", key="admin-key")[1], {"name": "admin", "admin": True, "profiles": True})
        self.assertEqual(self.call("GET", "/../analytics_server.py")[0], 401)

    def test_profiles_need_a_profile_readers_key(self):
        self.call("POST", "/batch/", {"api_key": "in-key", "batch": [started("p1", "m1", T0)]})
        self.assertEqual(self.call("GET", "/v1/people", key="read-key")[0], 403)
        self.app.config["profile_readers"] = ["ian"]
        status, out, _ = self.call("GET", "/v1/people?days=7", key="read-key")
        self.assertEqual((status, out["total"]), (200, 1))
        self.assertEqual(self.call("GET", "/v1/person?id=p1", key="admin-key")[1]["player_id"], "p1")
        self.assertEqual(self.call("GET", "/v1/person?id=nope", key="admin-key")[0], 404)
        for path in ("/v1/audience", "/v1/ftue", "/v1/economy", "/v1/social"):
            self.assertEqual(self.call("GET", path + "?days=30", key="read-key")[0], 200, path)

    def test_the_committed_public_key_is_accepted(self):
        status, out, _ = self.call("POST", "/batch/", {"api_key": A.public_ingest_key(),
                                                      "batch": [started("p1", "m1", T0)]})
        self.assertEqual((status, out["accepted"]), (200, 1))

    def test_healthz_is_public(self):
        self.assertEqual(self.call("GET", "/healthz")[:2], (200, {"ok": True}))


if __name__ == "__main__":
    unittest.main()
