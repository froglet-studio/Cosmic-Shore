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
        self.assertEqual(self.rows("SELECT COUNT(*) FROM person_snapshots WHERE distinct_id='p1'")[0][0], 1)
        out = self.store.ingest([{"event": "$identify", "timestamp": iso(T0 + 5),
                                  "properties": {"distinct_id": "p1", "$set": {"gdpr_deletion_requested": True}}}])
        self.assertEqual(out["erased"], 1)
        self.assertEqual(self.rows("SELECT COUNT(*) FROM events WHERE distinct_id='p1'")[0][0], 0)
        self.assertEqual(self.rows("SELECT COUNT(*) FROM people WHERE distinct_id='p1'")[0][0], 0)
        self.assertEqual(self.rows("SELECT COUNT(*) FROM person_snapshots WHERE distinct_id='p1'")[0][0], 0)  # the history too
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


class SeriesAnswerable(StoreCase):
    def test_a_series_lists_the_splits_its_measure_can_take(self):
        self.store.ingest([started("a", "m1", T0), completed("a", "m1", T0 + 60_000)])
        self.store.rebuild_aggregates()
        events = V.series(self.store.agg_path, "events", "none", "day", "release", 7)
        self.assertEqual(sorted(events["answerable_by"]), sorted(V.SERIES_ANSWERS["events"]))
        self.assertNotIn("platform", events["answerable_by"])
        for by in V.SERIES_BY:                              # what it lists is exactly what it answers
            if by in events["answerable_by"]:
                V.series(self.store.agg_path, "events", by, "day", "release", 7)
            else:
                with self.assertRaises(V.BadRequest):
                    V.series(self.store.agg_path, "events", by, "day", "release", 7)


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


class KeyCommand(unittest.TestCase):
    """2026-11-06: `key read admin` made a READ key named "admin"; Garrett signed in with it and saw no Admin pages."""

    def config(self):
        return dict(A.DEFAULTS, admin_keys={"admin": "fa_admin_x"}, read_keys={}, profile_readers=["garrett"],
                    all_keys_admin=False)

    def test_with_every_key_an_admins_the_message_says_so(self):
        c = dict(self.config(), all_keys_admin=True)
        message, _ = A.key_command(c, ["read", "ian"])
        self.assertIn("admin's", message)
        self.assertIn("an admin's too", A.key_command(c, ["list"])[0])

    def test_a_read_key_cannot_be_named_admin_or_reuse_another_kinds_name(self):
        c = self.config()
        with self.assertRaises(ValueError):
            A.key_command(c, ["read", "admin"])
        lone = dict(self.config(), admin_keys={"shombith": "fa_admin_y"})   # no admin key named "admin" at all:
        with self.assertRaises(ValueError):                                # the name itself is refused
            A.key_command(lone, ["read", "admin"])
        self.assertEqual(lone["read_keys"], {})
        A.key_command(c, ["admin", "shombith"])
        with self.assertRaises(ValueError):
            A.key_command(c, ["read", "shombith"])
        self.assertEqual(c["read_keys"], {})

    def test_make_list_and_delete(self):
        c = self.config()
        message, changed = A.key_command(c, ["read", "Garrett"])
        self.assertTrue(changed and c["read_keys"]["garrett"].startswith("fa_read_") and "opens player profiles" in message)
        listing, changed = A.key_command(c, ["list"])
        self.assertFalse(changed)
        self.assertIn("garrett", listing)
        self.assertNotIn(c["read_keys"]["garrett"], listing)         # list never prints a secret
        self.assertNotIn("fa_admin_x", listing)
        A.key_command(c, ["delete", "garrett"])
        self.assertNotIn("garrett", c["read_keys"])
        with self.assertRaises(ValueError):
            A.key_command(c, ["delete", "garrett"])
        with self.assertRaises(ValueError):
            A.key_command(c, ["read", "bad name!"])

    def test_deleting_a_name_that_is_both_kinds_asks_which_and_never_drops_the_last_admin(self):
        c = self.config()
        c["read_keys"]["admin"] = "fa_read_stray"                    # what `key read admin` left on the VM
        with self.assertRaises(ValueError):
            A.key_command(c, ["delete", "admin"])
        self.assertEqual((c["admin_keys"], c["read_keys"]), ({"admin": "fa_admin_x"}, {"admin": "fa_read_stray"}))
        A.key_command(c, ["delete", "read", "admin"])
        self.assertEqual((c["admin_keys"], c["read_keys"]), ({"admin": "fa_admin_x"}, {}))
        with self.assertRaises(ValueError):
            A.key_command(c, ["delete", "admin", "admin"])            # the only admin key stays


class EveryKeyIsAdmin(unittest.TestCase):
    """Shombith, 2026-11-06: "Give everyone who has a key an admin access there is no seperation between us." The
    default; the split tests above run with all_keys_admin false."""

    def test_with_the_default_a_read_key_is_an_admin_and_a_wrong_key_is_still_refused(self):
        self.assertTrue(A.DEFAULTS["all_keys_admin"])
        work = tempfile.mkdtemp()
        try:
            config = dict(A.DEFAULTS, data_dir=work, ingest_keys=["in-key"], read_keys={"garrett": "g-key"},
                          admin_keys={"admin": "admin-key"})
            app = A.App(config)
            app.rebuild()                                                      # an (empty) aggregates db to query
            server = ThreadingHTTPServer(("127.0.0.1", 0), A.make_handler(app))
            threading.Thread(target=server.serve_forever, daemon=True).start()
            base = f"http://127.0.0.1:{server.server_address[1]}"

            def call(method, path, key, body=None):
                request = urllib.request.Request(base + path, method=method, headers={"Authorization": "Bearer " + key,
                                                 "Content-Type": "application/json"},
                                                 data=json.dumps(body).encode() if body is not None else None)
                try:
                    with urllib.request.urlopen(request) as response:
                        return response.status, json.loads(response.read())
                except urllib.error.HTTPError as err:
                    return err.code, json.loads(err.read() or b"{}")
            status, me = call("GET", "/v1/me", "g-key")
            self.assertEqual((status, me["name"], me["admin"], me["profiles"]), (200, "garrett", True, True))
            self.assertEqual(call("POST", "/v1/query", "g-key", {"sql": "SELECT 1 AS one"})[0], 200)
            self.assertEqual(call("GET", "/v1/people?build=release&days=7", "g-key")[0], 200)
            self.assertEqual(call("GET", "/v1/me", "not-a-key")[0], 401)       # no key, no access, as before
            app.config["all_keys_admin"] = False                               # the split comes back
            status, me = call("GET", "/v1/me", "g-key")
            self.assertEqual((me["admin"], me["profiles"]), (False, False))
            self.assertEqual(call("POST", "/v1/query", "g-key", {"sql": "SELECT 1 AS one"})[0], 403)
            server.shutdown()
            server.server_close()
            app.store.db.close()
        finally:
            shutil.rmtree(work)


class Http(unittest.TestCase):
    def setUp(self):
        self.dir = tempfile.mkdtemp()
        # These tests guard the read / admin split, which comes back when all_keys_admin is false.
        config = dict(A.DEFAULTS, data_dir=self.dir, ingest_keys=["in-key"], read_keys={"ian": "read-key"},
                      admin_keys={"admin": "admin-key"}, allowed_origins=["https://dash.example"], all_keys_admin=False)
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


DAY = 86400 * 1000


def day(ms):
    return S.day_of(ms)


def era(era_id, start, end, source="any", **extra):
    return dict({"id": era_id, "name": era_id.replace("_", " ").title(), "from": start, "to": end, "source": source,
                 "kind": "data", "builds": [], "colour": "jade"}, **extra)


class Rev65Store(StoreCase):
    """Rev 65 (Shombith, 2026-11-06): eras, cohorts, play types, the switchable charts, from the aggregates."""

    def seed(self):
        old = S.now_ms() - 30 * DAY
        # PostHog history: stored by the import (trusted), so received_ms == ts_ms
        self.store.ingest([started("h1", "o1", old), completed("h1", "o1", old + 60_000),
                           started("h2", "o2", old, humans=2, ai=0)], trusted=True, max_age_days=365)
        # the same day as live data too: source must come from how the event arrived, not from its date
        self.store.ingest([started("h3", "o3", T0 - 3600_000)], trusted=True, max_age_days=365)
        self.store.ingest([
            started("p1", "m1", T0), completed("p1", "m1", T0 + 60_000),
            started("p1", "m2", T0 + 120_000),
            started("p2", "m3", T0, humans=2, ai=0, game_mode="Joust"),
            started("p3", "m3", T0, humans=2, ai=0, game_mode="Joust"),
            completed("p2", "m3", T0 + 90_000), completed("p3", "m3", T0 + 90_000),
            started("p2", "m4", T0 + 200_000, humans=2, ai=0), started("p3", "m4", T0 + 200_000, humans=2, ai=0),
            ev("party_invite_sent", "p2", T0 - 1000), ev("party_joined", "p3", T0 - 500),
            ev("crystals_earned", "p1", T0 + 70_000, amount=50, source="game_placement", balance=50),
            ev("crystals_earned", "p1", T0 + 80_000, amount=25, source="daily", balance=75),
        ], country="SE")
        self.store.set_eras([era("history", day(old - DAY), day(T0), "imported"),
                             era("testing", day(T0 - DAY), day(T0 + 30 * DAY), "live")], None, "admin")
        self.store.rebuild_aggregates()
        return {e["id"]: e for e in self.store.eras()}

    def test_imported_events_are_told_apart_exactly_not_by_date(self):
        eras = self.seed()
        agg = sqlite3.connect(self.store.agg_path)
        flags = dict(agg.execute("SELECT match_id, imported FROM plays"))
        self.assertEqual({k: flags[k] for k in ("o1", "o2", "o3", "m1")}, {"o1": 1, "o2": 1, "o3": 1, "m1": 0})
        history = V.overview(self.store.agg_path, "release", 90, "all", eras["history"])
        self.assertEqual(history["tiles"]["matches"], 3)          # o3 is on a live day and still counts as history
        self.assertEqual(history["tiles"]["players"], 3)
        testing = V.overview(self.store.agg_path, "release", 90, "all", eras["testing"])
        self.assertEqual(testing["tiles"]["matches"], 4)          # o3 is inside its dates but was imported
        self.assertEqual((testing["source"], testing["from"]), ("live", eras["testing"]["from"]))
        page = V.eras_page(self.store.agg_path, list(eras.values()), [])
        counts = {e["id"]: (e["events"], e["players"], e["matches"]) for e in page["eras"]}
        self.assertEqual(counts["history"], (4, 3, 3))
        self.assertEqual(counts["testing"][1:], (3, 4))
        self.assertEqual(sum(w["events"] for w in page["weekly"]), 4 + counts["testing"][0])

    def test_the_defaults_are_made_once_and_an_empty_list_stays_empty(self):
        self.store.ingest([started("h1", "o1", S.now_ms() - 20 * DAY)], trusted=True, max_age_days=365)
        self.store.db.execute("DELETE FROM store_meta")
        self.store.db.commit()
        again = S.Store(self.dir, A.schema_events(A.REPO_SCHEMA))
        eras = {e["id"]: e for e in again.eras()}
        self.assertEqual(list(eras), ["posthog", "pre_beta", "closed_beta", "steam"])
        self.assertEqual(eras["posthog"]["from"], day(S.now_ms() - 20 * DAY))   # the first imported day
        self.assertEqual((eras["closed_beta"]["kind"], eras["steam"]["colour"]), ("planned", "ruby"))
        self.assertEqual(again.milestones()[0]["name"], "Server live, PostHog off")
        again.set_eras([], None, "admin")
        again.db.close()
        third = S.Store(self.dir, A.schema_events(A.REPO_SCHEMA))
        self.assertEqual(third.eras(), [])
        third.db.close()

    def test_bad_eras_are_refused_with_a_reason(self):
        for bad, why in (([era("Bad Id", "2026-01-01", "2026-01-02")], "id"),
                         ([era("x", "2026-02-01", "2026-01-01")], "from must be on or before"),
                         ([era("x", "2026-02-30", "2026-03-01")], "real date"),
                         ([era("x", "2026-01-01", "2026-01-02", "posthog")], "source"),
                         ([era("x", "2026-01-01", "2026-01-02", builds=["mobile"])], "builds"),
                         ([era("x", "2026-01-01", "2026-01-02"), era("x", "2026-01-01", "2026-01-02")], "twice")):
            with self.assertRaisesRegex(S.Invalid, why):
                S.validate_eras(bad)
        with self.assertRaises(S.Invalid):
            self.store.era("../x")

    def test_series_splits_and_refuses_what_the_aggregates_cannot_answer(self):
        eras = self.seed()
        agg = self.store.agg_path
        by_play = V.series(agg, "matches", "play_type", "none", "release", 7)
        self.assertEqual({p["key"]: p["value"] for p in by_play["points"]},
                         {"solo_vs_ai": 3, "multi_human_unknown": 2})
        players = V.series(agg, "players", "none", "none", "release", 90)
        self.assertEqual(players["points"], [{"x": None, "key": None, "value": 6}])
        per_day = V.series(agg, "matches", "none", "day", "release", 7)
        self.assertEqual(len(per_day["points"]), 7)                # every day, zeros included
        week = V.series(agg, "players", "none", "week", "release", 7)
        self.assertTrue(all(p["x"] == S.week_of(p["x"]) for p in week["points"]))
        by_era = V.series(agg, "players", "era", "none", "release", 90, eras=list(eras.values()))
        self.assertEqual({p["key"]: p["value"] for p in by_era["points"]}, {"history": 3, "testing": 3})
        multi = V.series(agg, "players", "none", "none", "release", 90, "multi")
        self.assertEqual(multi["points"][0]["value"], 3)           # h2, p2, p3 played multiplayer
        by_country = V.series(agg, "finished_pct", "country", "none", "release", 7)
        self.assertEqual({p["key"]: p["value"] for p in by_country["points"]}, {"SE": 50.0, "unknown": 0.0})
        for measure, by in (("events", "platform"), ("events", "game_mode"), ("matches", "everything")):
            with self.assertRaises(V.BadRequest, msg=(measure, by)):
                V.series(agg, measure, by, "day", "release", 7)
        with self.assertRaises(V.BadRequest):
            V.series(agg, "revenue", "none", "day", "release", 7)

    def test_a_split_with_many_keys_keeps_eight_and_other(self):
        self.store.ingest([started(f"p{i}", f"m{i}", T0, game_mode=f"Mode{i:02d}") for i in range(11)])
        self.store.rebuild_aggregates()
        out = V.series(self.store.agg_path, "matches", "game_mode", "none", "release", 7)
        self.assertEqual(len(out["keys"]), 9)
        self.assertEqual(out["keys"][-1], "other")
        self.assertEqual({p["key"]: p["value"] for p in out["points"]}["other"], 3)
        self.assertEqual(sum(p["value"] for p in out["points"]), 11)

    def test_cohorts_play_types_and_tiles(self):
        eras = self.seed()
        agg = self.store.agg_path
        page = V.cohorts(agg, "release", 90)
        cohorts = {c["cohort"]: c for c in page["weekly"]}
        self.assertEqual(cohorts[S.week_of(day(S.now_ms() - 30 * DAY))]["weeks"][:2], [100.0, 0.0])
        self.assertEqual(page["tiles"]["players"], 6)
        self.assertEqual(page["tiles"]["median_lifespan_days"], 1)
        self.assertEqual(V.cohorts(agg, "release", 90, "multi")["tiles"]["players"], 3)
        types = V.playtypes(agg, "release", 7)
        self.assertEqual(types["share"], {"solo_vs_ai": 3, "solo": 0, "multi": 2})   # o3 (history) is in the week
        self.assertEqual(V.playtypes(agg, "release", 7, eras["testing"])["share"]["solo_vs_ai"], 2)
        self.assertEqual([f["count"] for f in types["funnel"]], [1, 1, 2, 1, 1])
        self.assertIn("2+ of the humans finished", types["funnel_note"])
        self.assertEqual(V.builtin_tile(agg, "play_mix", "release", 7)["rows"],
                         [["solo_vs_ai", 3], ["multi_human_unknown", 2]])
        with self.assertRaises(V.BadRequest):
            V.builtin_tile(agg, "revenue")
        marketing = V.overview(agg, "release", 7)["marketing"]
        self.assertEqual((marketing["invites_sent"], marketing["by_country"]),
                         (1, [{"country": "SE", "players": 3}, {"country": "unknown", "players": 1}]))
        self.assertEqual(V.overview(agg, "release", 7)["business"]["crystals_earned"], 75)

    def test_players_profiles_and_lifespans_carry_the_new_fields(self):
        eras = self.seed()
        multi = {p["distinct_id"] for p in self.store.people_list("release", 90, play="multi")["people"]}
        self.assertEqual(multi, {"h2", "p2", "p3"})
        one = self.store.people_list("release", 90)["people"][0]
        self.assertEqual(one["active_days"], [day(T0)])
        live = self.store.people_list("release", 90, since=eras["testing"]["from"], source="live")["people"]
        self.assertEqual({p["distinct_id"] for p in live}, {"p1", "p2", "p3"})
        p = self.store.person("p1")
        self.assertEqual(p["lifespan_days"], 1)
        self.assertEqual(p["economy"]["by_source"], [{"source": "game_placement", "earned": 50},
                                                     {"source": "daily", "earned": 25}])
        self.assertEqual(sum(map(sum, p["activity"]["by_hour_weekday"])), 2)
        self.assertEqual(len(p["activity"]["by_hour_weekday"]), 7)
        self.assertEqual(p["matches"]["recent"][-1]["duration_seconds"], 110)
        self.assertIn("h3", {x["distinct_id"] for x in self.store.lifespans("release", day(T0 - DAY))})
        spans = {x["distinct_id"]: x for x in self.store.lifespans("release", day(T0 - DAY), source="live")}
        self.assertEqual(set(spans), {"p1", "p2", "p3"})            # h3 came in with the history
        self.assertEqual((spans["p2"]["mostly"], spans["p1"]["mostly"]), ("multi", "solo_vs_ai"))

    def test_the_new_aggregates_hold_no_player_ids(self):
        self.seed()
        dump = "\n".join(sqlite3.connect(self.store.agg_path).iterdump())
        for player in ("h1", "h2", "h3", "p1", "p2", "p3"):
            self.assertNotIn(f"'{player}'", dump)


class Rev65Http(unittest.TestCase):
    tearDown = Http.tearDown
    call = Http.call

    def setUp(self):
        Http.setUp(self)
        self.app.config["read_keys"]["kim"] = "kim-key"
        self.app.config["profile_readers"] = ["ian"]
        self.call("POST", "/batch/", {"api_key": "in-key", "batch": [
            started("p1", "m1", T0), completed("p1", "m1", T0 + 60_000),
            started("p2", "m2", T0, humans=2, ai=0), started("p3", "m2", T0, humans=2, ai=0)]})
        self.app.store.ingest([started("h1", "o1", S.now_ms() - 30 * DAY)], trusted=True, max_age_days=365)
        self.app.rebuild()

    def put_eras(self, key="admin-key", eras=None):
        eras = eras if eras is not None else [era("history", day(S.now_ms() - 40 * DAY), day(T0), "imported"),
                                              era("testing", day(T0 - DAY), day(T0 + 30 * DAY), "live")]
        return self.call("PUT", "/v1/eras", {"eras": eras}, key=key)

    def test_eras_are_read_by_readers_and_written_by_admins_only(self):
        status, page, _ = self.call("GET", "/v1/eras", key="read-key")
        self.assertEqual(status, 200)
        self.assertEqual([e["id"] for e in page["eras"]], ["posthog", "pre_beta", "closed_beta", "steam"])
        self.assertEqual(page["milestones"][0]["day"], "2026-11-06")
        self.assertEqual(self.put_eras(key="read-key")[0], 403)      # negative control: a reader cannot write eras
        self.assertEqual(self.put_eras(eras=[era("Bad Id", "2026-01-01", "2026-01-02")])[0], 400)
        self.assertEqual(self.put_eras(eras=[era("x", "2026-02-01", "2026-01-01")])[0], 400)
        self.assertEqual(self.call("PUT", "/v1/eras", {"nope": []}, key="admin-key")[0], 400)
        status, out, _ = self.put_eras()
        self.assertEqual((status, [e["id"] for e in out["eras"]]), (200, ["history", "testing"]))
        self.assertEqual(out["eras"][0]["owner"], "admin")
        page = self.call("GET", "/v1/eras", key="read-key")[1]
        self.assertEqual({e["id"]: e["matches"] for e in page["eras"]}, {"history": 1, "testing": 2})
        status, cmp, _ = self.call("GET", "/v1/eras/compare?a=history&b=testing", key="read-key")
        self.assertEqual((status, cmp["a"]["players"], cmp["b"]["players"]), (200, 1, 3))
        for key in ("matches", "new_players", "editor_share_pct", "finished_pct", "rematch_pct", "d1_pct",
                    "rows_per_play"):
            self.assertIn(key, cmp["b"])
        self.assertEqual(self.call("GET", "/v1/eras/compare?a=history", key="read-key")[0], 400)
        self.assertEqual(self.call("GET", "/v1/eras/compare?a=history&b=nope", key="read-key")[0], 400)

    def test_the_era_filter_reaches_every_page_and_a_bad_era_is_400(self):
        self.put_eras()
        for path in ("/v1/overview", "/v1/retention", "/v1/matches", "/v1/playtypes", "/v1/cohorts",
                     "/v1/series?measure=players&by=platform&over=week", "/v1/people",
                     "/v1/dashboards/tile?source=builtin:matches_per_day"):
            sep = "&" if "?" in path else "?"
            status, page, _ = self.call("GET", path + sep + "era=testing", key="admin-key")
            self.assertEqual(status, 200, path)
            self.assertEqual(self.call("GET", path + sep + "era=nope", key="admin-key")[0], 400, path)
            self.assertEqual(self.call("GET", path + sep + "era=Bad%20Id", key="admin-key")[0], 400, path)
        overview = self.call("GET", "/v1/overview?days=7&era=history", key="read-key")[1]
        self.assertEqual((overview["tiles"]["matches"], overview["source"]), (1, "imported"))
        self.assertIn("events_24h", overview["unfiltered"])
        people = self.call("GET", "/v1/people?era=history", key="admin-key")[1]
        self.assertEqual([p["distinct_id"] for p in people["people"]], ["h1"])

    def test_series_answers_a_chart_and_refuses_an_unanswerable_pair(self):
        status, out, _ = self.call("GET", "/v1/series?measure=matches&by=play_type&over=none&days=7", key="read-key")
        self.assertEqual((status, {p["key"]: p["value"] for p in out["points"]}),
                         (200, {"solo_vs_ai": 1, "multi_human_unknown": 1}))
        self.assertEqual(set(out), {"measure", "by", "over", "build", "days", "play", "from", "to", "source", "era",
                                    "device", "points", "keys", "unfiltered", "notes", "answerable_by"})
        status, out, _ = self.call("GET", "/v1/series?measure=events&by=country", key="read-key")
        self.assertEqual(status, 400)                               # negative control: events have no country
        self.assertIn("cannot be split by country", out["error"])
        for bad in ("measure=revenue", "by=colour", "over=month", "days=5", "play=everyone", "build=mobile"):
            self.assertEqual(self.call("GET", "/v1/series?" + bad, key="read-key")[0], 400, bad)

    def test_the_query_runner_is_admin_only_and_never_reaches_events_db(self):
        sql = {"sql": "SELECT play_type, COUNT(*) AS n FROM plays GROUP BY play_type"}
        self.assertEqual(self.call("POST", "/v1/query", sql, key="read-key")[0], 403)
        status, out, _ = self.call("POST", "/v1/query", sql, key="admin-key")
        self.assertEqual((status, out["columns"]), (200, ["play_type", "n"]))
        self.assertIn("ms", out)
        raw = os.path.join(self.dir, "events.db")
        for bad in ("SELECT * FROM events", "SELECT * FROM people", f"ATTACH DATABASE '{raw}' AS raw",
                    "SELECT * FROM pragma_database_list", "SELECT 1; DELETE FROM plays", "DELETE FROM plays"):
            status, out, _ = self.call("POST", "/v1/query", {"sql": bad}, key="admin-key")
            self.assertEqual(status, 400, bad)
        self.assertEqual(self.call("GET", "/v1/schema", key="read-key")[0], 403)
        tables = {t["name"]: t for t in self.call("GET", "/v1/schema", key="admin-key")[1]["tables"]}
        self.assertNotIn("events", tables)
        self.assertIn("match_id", tables["plays"]["columns"])
        self.assertEqual(tables["plays"]["rows"], 4)

    def test_saved_queries_and_their_tables(self):
        body = {"name": "mix", "sql": "SELECT play_type, COUNT(*) AS n FROM plays GROUP BY play_type",
                "chart": "donut", "x": "play_type", "y": "n", "visibility": "readers", "as_table": True}
        self.assertEqual(self.call("POST", "/v1/queries", body, key="read-key")[0], 403)   # negative control
        for bad in ({**body, "chart": "radar"}, {**body, "visibility": "world"}, {**body, "name": "Mix It"},
                    {**body, "sql": "SELECT * FROM events"}, {**body, "sql": "DROP TABLE plays"}):
            self.assertEqual(self.call("POST", "/v1/queries", bad, key="admin-key")[0], 400, bad)
        status, saved, _ = self.call("POST", "/v1/queries", body, key="admin-key")
        self.assertEqual((status, saved["table"], saved["owner"]), (200, "t_mix", "admin"))
        self.assertEqual(self.call("POST", "/v1/queries", {**body, "visibility": "me"}, key="admin-key")[0], 400)
        mine = self.call("POST", "/v1/queries", {**body, "name": "secret", "as_table": False, "visibility": "me"},
                         key="admin-key")[1]
        tables = {t["name"]: t for t in self.call("GET", "/v1/schema", key="admin-key")[1]["tables"]}
        self.assertEqual((tables["t_mix"]["saved"], tables["t_mix"]["rows"]), (True, 2))
        out = self.call("POST", "/v1/query", {"sql": "SELECT SUM(n) FROM t_mix"}, key="admin-key")[1]
        self.assertEqual(out["rows"], [[4]])
        self.assertEqual([q["id"] for q in self.call("GET", "/v1/queries", key="read-key")[1]["queries"]],
                         [saved["id"]])                              # a reader sees the shared one, not "me"
        self.assertEqual(len(self.call("GET", "/v1/queries", key="admin-key")[1]["queries"]), 2)
        self.assertEqual(self.call("DELETE", "/v1/queries?id=" + mine["id"], key="read-key")[0], 403)
        self.assertEqual(self.call("DELETE", "/v1/queries?id=" + saved["id"], key="admin-key")[0], 200)
        self.assertEqual(self.call("DELETE", "/v1/queries?id=" + saved["id"], key="admin-key")[0], 404)
        tables = {t["name"] for t in self.call("GET", "/v1/schema", key="admin-key")[1]["tables"]}
        self.assertNotIn("t_mix", tables)

    def test_dashboards_and_their_tiles(self):
        query = self.call("POST", "/v1/queries", {"name": "starts", "sql": "SELECT COUNT(*) AS n FROM plays",
                                                  "chart": "number", "visibility": "readers"}, key="admin-key")[1]
        board = {"name": "Launch week", "shared": "readers", "email": "mondays 09:00", "tiles": [
            {"id": "a", "source": "builtin:new_players", "title": "New players", "chart": "line", "w": 2, "h": 1},
            {"id": "b", "source": "query:" + query["id"], "title": "Starts", "chart": "number", "w": 1, "h": 1}]}
        self.assertEqual(self.call("PUT", "/v1/dashboards", board, key="read-key")[0], 403)   # negative control
        for bad in ({**board, "shared": "world"}, {**board, "tiles": [{**board["tiles"][0], "w": 5}]},
                    {**board, "tiles": [{**board["tiles"][0], "source": "builtin:revenue"}]},
                    {**board, "tiles": [{**board["tiles"][1], "source": "query:q_nope"}]}):
            self.assertEqual(self.call("PUT", "/v1/dashboards", bad, key="admin-key")[0], 400)
        status, saved, _ = self.call("PUT", "/v1/dashboards", board, key="admin-key")
        self.assertEqual((status, saved["email"], saved["owner"]), (200, {"email": "mondays 09:00"}, "admin"))
        self.call("PUT", "/v1/dashboards", {**board, "name": "Private", "shared": "admins"}, key="admin-key")
        seen = self.call("GET", "/v1/dashboards", key="read-key")[1]["dashboards"]
        self.assertEqual([d["name"] for d in seen], ["Launch week"])
        self.assertEqual(len(self.call("GET", "/v1/dashboards", key="admin-key")[1]["dashboards"]), 2)
        status, tile, _ = self.call("GET", "/v1/dashboards/tile?source=query:" + query["id"], key="read-key")
        self.assertEqual((status, tile["rows"]), (200, [[4]]))
        status, tile, _ = self.call("GET", "/v1/dashboards/tile?source=builtin:matches_per_day&days=7", key="read-key")
        self.assertEqual((status, tile["columns"], sum(r[1] for r in tile["rows"])), (200, ["day", "matches"], 2))
        self.assertEqual(self.call("GET", "/v1/dashboards/tile?source=builtin:revenue", key="read-key")[0], 400)
        self.assertEqual(self.call("GET", "/v1/dashboards/tile?source=query:q_nope", key="read-key")[0], 404)
        renamed = self.call("PUT", "/v1/dashboards", {**board, "id": saved["id"], "name": "Launch"}, key="admin-key")[1]
        self.assertEqual((renamed["id"], renamed["created_ms"]), (saved["id"], saved["created_ms"]))
        self.assertEqual(self.call("DELETE", "/v1/dashboards?id=" + saved["id"], key="read-key")[0], 403)
        self.assertEqual(self.call("DELETE", "/v1/dashboards?id=" + saved["id"], key="admin-key")[0], 200)

    def test_segments_belong_to_profile_readers_and_only_admins_share(self):
        seg = {"name": "Lapsed solo", "filters": {"play": "solo_vs_ai", "last_seen": ">14d"}}
        self.assertEqual(self.call("POST", "/v1/segments", seg, key="kim-key")[0], 403)    # no profiles for kim
        self.assertEqual(self.call("GET", "/v1/segments", key="kim-key")[0], 403)
        self.assertEqual(self.call("POST", "/v1/segments", {**seg, "shared": True}, key="read-key")[0], 403)
        self.assertEqual(self.call("POST", "/v1/segments", {**seg, "filters": "play=solo"}, key="read-key")[0], 400)
        status, mine, _ = self.call("POST", "/v1/segments", seg, key="read-key")
        self.assertEqual((status, mine["owner"], mine["shared"]), (200, "ian", False))
        shared = self.call("POST", "/v1/segments", {**seg, "name": "Everyone's", "shared": True}, key="admin-key")[1]
        self.assertEqual({s["name"] for s in self.call("GET", "/v1/segments", key="read-key")[1]["segments"]},
                         {"Lapsed solo", "Everyone's"})
        self.assertEqual(self.call("DELETE", "/v1/segments?id=" + shared["id"], key="read-key")[0], 403)
        self.assertEqual(self.call("DELETE", "/v1/segments?id=" + mine["id"], key="read-key")[0], 200)
        self.assertEqual(self.call("DELETE", "/v1/segments?id=" + shared["id"], key="admin-key")[0], 200)
        self.assertEqual(self.call("DELETE", "/v1/segments?id=s_gone", key="admin-key")[0], 404)

    def test_cohort_lifespans_need_profiles_and_the_old_routes_keep_their_shape(self):
        status, page, _ = self.call("GET", "/v1/cohorts?days=90", key="kim-key")
        self.assertEqual(status, 200)
        self.assertNotIn("lifespans", page)
        self.assertIn("lifespans", page["unfiltered"])
        page = self.call("GET", "/v1/cohorts?days=90", key="read-key")[1]     # ian reads profiles
        self.assertEqual({p["distinct_id"] for p in page["lifespans"]}, {"h1", "p1", "p2", "p3"})
        self.assertEqual(set(page["tiles"]), {"median_lifespan_days", "d1_pct", "d7_pct", "lapsed_14d", "players"})
        types = self.call("GET", "/v1/playtypes?days=7", key="kim-key")[1]
        self.assertEqual(set(types["groups"]), {"solo_vs_ai", "solo", "multi"})
        self.assertEqual(self.call("GET", "/v1/people?play=everyone", key="read-key")[0], 400)
        people = self.call("GET", "/v1/people?days=7&play=multi", key="read-key")[1]
        self.assertEqual({p["distinct_id"] for p in people["people"]}, {"p2", "p3"})
        self.assertTrue(all("active_days" in p for p in people["people"]))
        person = self.call("GET", "/v1/person?id=p1", key="read-key")[1]
        for key in ("lifespan_days", "where", "activity", "matches", "economy", "ftue", "settings", "social"):
            self.assertIn(key, person)
        self.assertIn("by_hour_weekday", person["activity"])
        self.assertIn("by_source", person["economy"])
        overview = self.call("GET", "/v1/overview?days=7", key="kim-key")[1]
        for key in ("tiles", "rematch_by_play_type", "match_health_by_build", "events_24h", "digest", "marketing",
                    "business"):
            self.assertIn(key, overview)
        self.assertEqual(overview["unfiltered"], [])
        _, _, headers = self.call("GET", "/v1/eras", key="read-key", origin="https://dash.example")
        self.assertIn("PUT", headers.get("Access-Control-Allow-Methods"))



# ---------------------------------------------------------------- 2026-11-06: elements, the Toybox, saved views

import mode_elements as M  # noqa: E402


def identify(player, ms, **person):
    return {"event": "$identify", "timestamp": iso(ms), "properties": {"distinct_id": player, "$set": person}}


class ModeElements(unittest.TestCase):
    """Shombith, 2026-11-06: "the matches over time should also have element support". The table is generated from
    the game's own ModeGenre.cs; a test keeps it current."""

    def test_the_committed_table_is_current_and_its_known_answers_hold(self):
        if not M.have_sources():
            self.skipTest("no Unity sources in this checkout (the VM's sparse checkout)")
        self.assertEqual(M.check(), 0, "run python3 Tools/Analytics/server/mode_elements.py")
        self.assertEqual(M.self_test(), 0)

    def test_the_server_reads_the_table(self):
        table = M.load()
        self.assertEqual(table.get("BroodRush"), ("Mass", "Space"))      # the two-element mode
        self.assertEqual(table.get("Scurry"), ("Mass", None))            # a gather, scored on crystals
        self.assertEqual(table.get("Tollway"), ("Mass", None))           # scored on goals, an acquisition
        self.assertNotIn("Maelstrom", table)                             # draws other modes: no genre
        self.assertEqual(M.load("/nonexistent/mode_elements.json"), {})


class Elements(StoreCase):
    def seed(self):
        old = S.now_ms() - 30 * DAY
        self.store.ingest([started("h1", "o1", old, game_mode="BroodRush"), completed("h1", "o1", old + 60_000),
                           started("h2", "o2", old, game_mode="CrystalCapture")], trusted=True, max_age_days=365)
        self.store.ingest([
            started("p1", "m1", T0, element="Space"),                     # a new build: its own element wins
            started("p1", "m2", T0 + 120_000, game_mode="Scurry"),        # no element sent: the mode's row
            started("p2", "m3", T0, game_mode="Joust", element="charge", element_secondary="nonsense"),
            started("p3", "m4", T0, game_mode="BroodRush", element="Mass", element_secondary="Space"),
        ])
        self.store.rebuild_aggregates()

    def test_a_match_takes_its_own_element_else_its_modes(self):
        self.seed()
        agg = sqlite3.connect(self.store.agg_path)
        rows = {r[0]: r[1:] for r in agg.execute("SELECT match_id, element, element_secondary, element_source FROM plays")}
        self.assertEqual(rows["m1"], ("Space", None, "event"))
        self.assertEqual(rows["m2"], ("Mass", None, "table"))
        self.assertEqual(rows["m3"], ("Charge", None, "event"))           # names normalised; a bad second dropped
        self.assertEqual(rows["m4"], ("Mass", "Space", "event"))
        self.assertEqual(rows["o1"], ("Mass", "Space", "table"))          # the PostHog history has one too
        self.assertEqual(rows["o2"], (None, None, None))                  # an old name with no genre: honest none
        health = V.health(self.store.agg_path, {}, 90)["modes_without_element"]
        self.assertEqual(health, [{"game_mode": "CrystalCapture", "plays": 1, "imported_plays": 1}])

    def test_series_splits_by_element_and_lists_the_second_apart(self):
        self.seed()
        agg = self.store.agg_path
        out = V.series(agg, "matches", "element", "none", "release", 90)
        self.assertEqual({p["key"]: p["value"] for p in out["points"]}, {"Charge": 1, "Mass": 3, "Space": 1, "none": 1})
        self.assertEqual(out["keys"], ["Charge", "Mass", "Space", "none"])              # the elements' own order
        self.assertEqual(out["secondary"], [{"key": "Space", "value": 2}])               # never added into the split
        self.assertTrue(any("two-element mode" in n for n in out["notes"]))
        for measure in V.PLAY_MEASURES + ("players", "new_players"):
            self.assertIn("element", V.series(agg, measure, "none", "day", "release", 7)["answerable_by"])
            V.series(agg, measure, "element", "week", "release", 30)
        with self.assertRaises(V.BadRequest):                                            # negative control
            V.series(agg, "events", "element", "day", "release", 7)
        players = V.series(agg, "players", "element", "none", "release", 90)
        self.assertEqual({p["key"]: p["value"] for p in players["points"]}["Mass"], 3)   # h1, p1, p3
        page = V.matches(agg, "release", 90)
        self.assertEqual({r["element"]: r["matches"] for r in page["by_element"]}, {"Charge": 1, "Mass": 3, "Space": 1, "none": 1})
        self.assertEqual(page["element_secondary"][0]["element"], "Space")
        self.assertEqual(next(r for r in page["by_mode"] if r["game_mode"] == "Scurry")["element"], "Mass")


class Freestyle(StoreCase):
    """Shombith, 2026-11-06: players who "went inside freestyle/toybox and played vessels ... can we bring it for the
    old one?" Entries come from the PostHog history; minutes, vessels and switches from builds after 2026-11-06; older
    switches are recovered from the identify history."""

    def seed(self):
        old = S.now_ms() - 30 * DAY
        self.store.ingest([
            ev("freestyle_entered", "h1", old, is_first=True, seconds_since_launch=30),
            ev("freestyle_entered", "h1", old + 600_000, is_first=False, seconds_since_launch=640),
            ev("freestyle_entered", "h2", old, is_first=True, seconds_since_launch=12),
            identify("h1", old + 1000, selected_vessel="Squirrel", total_flight_time_seconds=10),
            identify("h1", old + 2000, selected_vessel="Squirrel", total_flight_time_seconds=40),
            identify("h1", old + 3000, selected_vessel="Manta", total_flight_time_seconds=90),
            identify("h1", old + 4000, selected_vessel="Rhino", total_flight_time_seconds=120),
        ], trusted=True, max_age_days=365)
        self.store.ingest([
            ev("freestyle_entered", "p1", T0, is_first=True, seconds_since_launch=8, vessel_class="Squirrel"),
            ev("freestyle_vessel_changed", "p1", T0 + 30_000, vessel_class="Manta", from_vessel_class="Squirrel",
               seconds_in_freestyle=30.5),
            ev("freestyle_vessel_changed", "p1", T0 + 60_000, vessel_class="Sparrow", from_vessel_class="Manta",
               seconds_in_freestyle=60),
            ev("freestyle_ended", "p1", T0 + 90_000, seconds=90, vessel_class="Sparrow", vessels_flown=3),
            ev("freestyle_ended", "p1", T0 + 90_002, seconds=90, vessel_class="Sparrow", vessels_flown=3),  # double-send
            # p1's identify history after its first live switch is not recovered again (the event counts)
            identify("p1", T0 + 1000, selected_vessel="Squirrel"), identify("p1", T0 + 70_000, selected_vessel="Sparrow"),
        ])
        self.store.rebuild_aggregates()

    def test_snapshots_are_kept_once_and_erased_with_the_player(self):
        row = identify("h9", T0, selected_vessel="Manta", preferred_vessel="Squirrel", total_flight_time_seconds=61.5)
        self.store.ingest([row], trusted=True, max_age_days=365)
        self.store.ingest([row], trusted=True, max_age_days=365)        # import-posthog run again: stored once
        self.store.ingest([identify("h9", T0 + 5, selected_vessel="Rhino")])   # a live one adds a row
        rows = self.rows("SELECT ts_ms, imported, selected_vessel, preferred_vessel, total_flight_time_seconds "
                         "FROM person_snapshots WHERE distinct_id='h9' ORDER BY ts_ms")
        self.assertEqual(len(rows), 2)
        self.assertEqual(rows[0][1:], (1, "Manta", "Squirrel", 61.5))
        self.assertEqual(rows[1][2], "Rhino")
        self.assertEqual(self.store.erase("h9", "admin"), 3)            # two snapshots and the person
        self.assertEqual(self.rows("SELECT COUNT(*) FROM person_snapshots WHERE distinct_id='h9'")[0][0], 0)
        self.assertNotIn("'h9'", "\n".join(self.store.db.iterdump()))

    def test_switches_are_derived_where_the_selected_vessel_changes(self):
        snaps = [(1, "A", 1), (2, "A", 1), (3, "B", 1), (4, "", 1), (5, "C", 0)]
        self.assertEqual(S.derive_switches(snaps), [(3, "A", "B", 1), (5, "B", "C", 0)])
        self.assertEqual(S.derive_switches([(1, "A", 1), (2, "A", 1)]), [])      # negative control: no change

    def test_the_toybox_aggregates_count_entries_minutes_and_switches(self):
        self.seed()
        agg = self.store.agg_path
        page = V.freestyle(agg, "release", 90)
        t = page["tiles"]
        self.assertEqual((t["entries"], t["first_entries"], t["players"]), (4, 3, 3))
        self.assertEqual((t["stretches"], t["minutes"], t["avg_vessels_flown"]), (1, 1.5, 3.0))   # the double-send once
        self.assertEqual((t["switches"], t["recovered_switches"]), (2, 2))                       # h1: S->M, M->R
        pairs = {(p["from_vessel"], p["to_vessel"]): (p["switches"], p["recovered"]) for p in page["switch_pairs"]}
        self.assertEqual(pairs, {("Squirrel", "Manta"): (2, 1), ("Manta", "Sparrow"): (1, 0), ("Manta", "Rhino"): (1, 1)})
        self.assertEqual(page["history"]["first_imported_entry_day"], day(S.now_ms() - 30 * DAY))
        self.assertEqual(page["history"]["first_minutes_day"], day(T0))
        entries = V.series(agg, "freestyle_entries", "vessel_class", "none", "release", 90)
        self.assertEqual({p["key"]: p["value"] for p in entries["points"]}, {"unknown": 3, "Squirrel": 1})
        switches = V.series(agg, "freestyle_switches", "switch_source", "none", "release", 90)
        self.assertEqual({p["key"]: p["value"] for p in switches["points"]}, {"event": 2, "recovered": 2})
        to = V.series(agg, "freestyle_switches", "vessel_class", "none", "release", 90)
        self.assertEqual({p["key"]: p["value"] for p in to["points"]}, {"Manta": 2, "Sparrow": 1, "Rhino": 1})
        players = V.series(agg, "freestyle_players", "none", "none", "release", 90)
        self.assertEqual(players["points"][0]["value"], 3)
        minutes = V.series(agg, "freestyle_minutes", "vessel_class", "none", "release", 90)
        self.assertEqual({p["key"]: p["value"] for p in minutes["points"]}, {"Sparrow": 1.5})
        self.assertTrue(any("freestyle_ended" in n for n in minutes["notes"]))
        for measure, by in (("freestyle_entries", "platform"), ("freestyle_minutes", "element"),
                            ("freestyle_players", "game_mode"), ("freestyle_entries", "from_vessel_class")):
            with self.assertRaises(V.BadRequest, msg=(measure, by)):                         # negative controls
                V.series(agg, measure, by, "day", "release", 7)
        self.assertEqual(sum(r[1] for r in V.builtin_tile(agg, "freestyle_entries", "release", 90)["rows"]), 4)

    def test_profiles_and_the_players_list_carry_the_toybox(self):
        self.seed()
        p = self.store.person("p1")["freestyle"]
        self.assertEqual((p["entries"], p["stretches"], p["minutes"], p["switch_count"]), (1, 1, 1.5, 2))
        self.assertEqual([(x["from"], x["to"], x["approx"]) for x in p["switches"]],
                         [("Squirrel", "Manta", False), ("Manta", "Sparrow", False)])
        self.assertEqual(p["switches"][0]["seconds_in_freestyle"], 30.5)
        self.assertEqual(p["switched_to"], {"Manta": 1, "Sparrow": 1})
        h = self.store.person("h1")["freestyle"]
        self.assertEqual((h["entries"], h["recovered_switches"]), (2, 2))
        self.assertEqual([(x["from"], x["to"], x["approx"]) for x in h["switches"]],
                         [("Squirrel", "Manta", True), ("Manta", "Rhino", True)])
        people = {x["distinct_id"]: x for x in self.store.people_list("release", 90)["people"]}
        self.assertEqual((people["h1"]["freestyle_entries"], people["p1"]["freestyle_seconds"]), (2, 90.0))

    def test_the_toybox_aggregates_hold_no_player_ids(self):
        self.seed()
        dump = "\n".join(sqlite3.connect(self.store.agg_path).iterdump())
        for player in ("h1", "h2", "p1"):
            self.assertNotIn(f"'{player}'", dump)


class PresetsHttp(unittest.TestCase):
    """Shombith, 2026-11-06: "save our own view as well and make it public and that will saved in saved presets under
    dashboards". Every key is an admin's (all_keys_admin), so a saved view belongs to its key's name."""
    tearDown = Http.tearDown
    call = Http.call

    def setUp(self):
        self.dir = tempfile.mkdtemp()
        config = dict(A.DEFAULTS, data_dir=self.dir, ingest_keys=["in-key"], read_keys={"ian": "ian-key", "kim": "kim-key"},
                      admin_keys={"admin": "admin-key"})
        self.app = A.App(config)
        self.server = ThreadingHTTPServer(("127.0.0.1", 0), A.make_handler(self.app))
        self.base = f"http://127.0.0.1:{self.server.server_address[1]}"
        threading.Thread(target=self.server.serve_forever, daemon=True).start()
        self.call("POST", "/batch/", {"api_key": "in-key", "batch": [
            started("p1", "m1", T0, element="Time"), completed("p1", "m1", T0 + 60_000),
            ev("freestyle_entered", "p1", T0 + 70_000, is_first=True, vessel_class="Manta")]})
        self.app.rebuild()

    def view(self, **extra):
        return dict({"name": "Matches by element", "page": "overview", "chart_id": "overview.series", "series": True,
                     "settings": {"form": "bars", "measure": "matches", "by": "element", "over": "week"},
                     "filters": {"build": "release", "days": 30, "play": "all"}, "public": False}, **extra)

    def test_saved_views_are_their_owners_and_public_ones_are_everyones(self):
        status, mine, _ = self.call("POST", "/v1/presets", self.view(), key="ian-key")
        self.assertEqual((status, mine["owner"], mine["public"]), (200, "ian", False))
        shared = self.call("POST", "/v1/presets", self.view(name="Toybox", public=True, chart_id="overview.freestyle",
                           settings={"measure": "freestyle_entries", "by": "vessel_class", "over": "day"}), key="ian-key")[1]
        self.assertEqual({p["name"] for p in self.call("GET", "/v1/presets", key="ian-key")[1]["presets"]},
                         {"Matches by element", "Toybox"})
        # negative controls: a private view is not listed to another key, and another key cannot delete or change it
        self.assertEqual([p["id"] for p in self.call("GET", "/v1/presets", key="kim-key")[1]["presets"]], [shared["id"]])
        self.assertEqual(self.call("DELETE", "/v1/presets?id=" + mine["id"], key="kim-key")[0], 404)
        self.assertEqual(self.call("POST", "/v1/presets", self.view(id=mine["id"], name="mine now"), key="kim-key")[0], 400)
        self.assertEqual(self.call("POST", "/v1/presets", self.view(id=shared["id"]), key="kim-key")[0], 403)
        self.assertEqual(self.call("DELETE", "/v1/presets?id=" + shared["id"], key="kim-key")[0], 403)
        self.assertEqual(self.call("GET", "/v1/dashboards/tile?source=preset:" + mine["id"], key="kim-key")[0], 404)
        # the owner changes and deletes; a real admin key (admin_keys) may remove a public one
        status, renamed, _ = self.call("POST", "/v1/presets", self.view(id=mine["id"], name="Elements"), key="ian-key")
        self.assertEqual((status, renamed["name"], renamed["created_ms"]), (200, "Elements", mine["created_ms"]))
        self.assertEqual(self.call("DELETE", "/v1/presets?id=" + mine["id"], key="ian-key")[0], 200)
        self.assertEqual(self.call("DELETE", "/v1/presets?id=" + shared["id"], key="admin-key")[0], 200)
        self.assertEqual(self.call("GET", "/v1/presets", key="kim-key")[1]["presets"], [])

    def test_bad_views_are_400_with_a_reason(self):
        for bad in (self.view(page="nowhere"), self.view(chart_id="bad id!"), self.view(name=""),
                    self.view(settings={"measure": "Matches; DROP"}), self.view(settings={"colour": "jade"}),
                    self.view(filters={"build": "mobile"}), self.view(filters={"days": 5}), self.view(filters={"era": "nope"}),
                    self.view(filters={"play": "everyone"}), self.view(public="yes"), self.view(extra=1), ["not", "an", "object"]):
            status, out, _ = self.call("POST", "/v1/presets", bad, key="ian-key")
            self.assertEqual(status, 400, bad)
            self.assertTrue(out["error"])
        self.assertEqual(self.call("DELETE", "/v1/presets?id=p_gone", key="ian-key")[0], 404)
        self.assertEqual(self.call("GET", "/v1/presets")[0], 401)

    def test_a_saved_view_is_a_dashboard_tile_running_its_own_series(self):
        view = self.call("POST", "/v1/presets", self.view(public=True), key="ian-key")[1]
        status, tile, _ = self.call("GET", "/v1/dashboards/tile?source=preset:" + view["id"] + "&days=7", key="kim-key")
        self.assertEqual(status, 200)
        self.assertEqual((tile["series"]["by"], tile["series"]["days"], tile["columns"]), ("element", 30, ["x", "element", "matches"]))
        self.assertEqual(sum(r[2] for r in tile["rows"]), 1)
        self.assertEqual(tile["unfiltered"], ["build", "days", "play", "era", "device"])  # its own filters, not the page's
        flat = self.call("POST", "/v1/presets", self.view(name="Health", chart_id="overview.health", series=False), key="ian-key")[1]
        self.assertEqual(self.call("GET", "/v1/dashboards/tile?source=preset:" + flat["id"], key="ian-key")[0], 400)
        board = {"name": "Views", "shared": "readers", "tiles": [{"id": "a", "source": "preset:" + view["id"], "title": "x",
                                                                    "chart": "bars", "w": 2, "h": 1}]}
        self.assertEqual(self.call("PUT", "/v1/dashboards", board, key="kim-key")[0], 200)
        self.assertEqual(self.call("PUT", "/v1/dashboards", {**board, "tiles": [{**board["tiles"][0], "source": "preset:" + flat["id"]}]},
                                   key="ian-key")[0], 400)                              # not a series view
        private = self.call("POST", "/v1/presets", self.view(name="Mine"), key="ian-key")[1]
        self.assertEqual(self.call("PUT", "/v1/dashboards", {**board, "tiles": [{**board["tiles"][0], "source": "preset:" + private["id"]}]},
                                   key="kim-key")[0], 400)                              # someone else's private view

    def test_the_toybox_routes_answer(self):
        status, page, _ = self.call("GET", "/v1/freestyle?days=7", key="kim-key")
        self.assertEqual((status, page["tiles"]["entries"], page["tiles"]["players"]), (200, 1, 1))
        self.assertEqual(self.call("GET", "/v1/freestyle?days=5", key="kim-key")[0], 400)
        status, out, _ = self.call("GET", "/v1/series?measure=freestyle_entries&by=vessel_class&over=none&days=7", key="kim-key")
        self.assertEqual((status, out["points"]), (200, [{"x": None, "key": "Manta", "value": 1}]))
        self.assertEqual(self.call("GET", "/v1/series?measure=freestyle_entries&by=country", key="kim-key")[0], 400)
        self.assertEqual(self.call("GET", "/v1/series?measure=matches&by=element&over=none&days=7", key="kim-key")[1]["points"],
                         [{"x": None, "key": "Time", "value": 1}])
        person = self.call("GET", "/v1/person?id=p1", key="kim-key")[1]
        self.assertEqual(person["freestyle"]["entries"], 1)
        people = self.call("GET", "/v1/people?days=7", key="kim-key")[1]["people"]
        self.assertEqual(people[0]["freestyle_entries"], 1)



# ---------------------------------------------------------------- 2026-11-07: match setups

# (props, setup) - every rule of analytics_store.setup_of, old builds' props included (no domain fields)
SETUP_TABLE = (
    # multiplayer, with the domain fields (builds from 2026-11-07)
    ({"player_count_human": 2, "humans_on_my_domain": 1, "humans_on_other_domains": 1}, "competitive"),
    ({"player_count_human": 4, "humans_on_my_domain": 1, "humans_on_other_domains": 3}, "competitive"),
    ({"player_count_human": 4, "humans_on_my_domain": 2, "humans_on_other_domains": 2}, "teams"),
    ({"player_count_human": 3, "humans_on_my_domain": 2, "humans_on_other_domains": 1}, "teams"),
    ({"player_count_human": 2, "humans_on_my_domain": 2, "humans_on_other_domains": 0, "ai_on_other_domains": 3},
     "teams_vs_ai"),
    ({"player_count_human": 3, "humans_on_my_domain": 3, "humans_on_other_domains": 0}, "teams_vs_ai"),
    ({"player_count_human": "2", "humans_on_my_domain": "1", "humans_on_other_domains": "1"}, "competitive"),
    # multiplayer without them: older builds and the PostHog history, never guessed
    ({"player_count_human": 2, "player_count_ai": 2}, "multi_unrecorded"),
    ({"player_count_human": 2, "humans_on_other_domains": 1}, "multi_unrecorded"),          # no side of my own
    ({"player_count_human": 2, "humans_on_my_domain": 1, "humans_on_other_domains": -1}, "multi_unrecorded"),
    ({"player_count_human": 2, "humans_on_my_domain": True, "humans_on_other_domains": True}, "multi_unrecorded"),
    # solo, old props: by the AI count (player_count_ai, else ai_count)
    ({"player_count_human": 1, "player_count_ai": 0}, "solo_alone"),
    ({"player_count_human": 1}, "solo_alone"),
    ({}, "solo_alone"),
    ({"player_count_human": 1, "player_count_ai": 1}, "solo_vs_1_ai"),
    ({"player_count_human": 1, "player_count_ai": 2}, "solo_vs_2_ai"),
    ({"player_count_human": 1, "player_count_ai": 3}, "solo_vs_3plus_ai"),
    ({"player_count_human": 1, "player_count_ai": 7}, "solo_vs_3plus_ai"),
    ({"player_count_human": 1, "ai_count": 2}, "solo_vs_2_ai"),                                # ai_count stands in
    ({"player_count_human": 1, "player_count_ai": 0, "ai_count": 3}, "solo_alone"),            # the real count wins
    ({"player_count_human": 1, "player_count_ai": "nonsense", "ai_count": 1}, "solo_vs_1_ai"),
    # solo, new props: opponents are the AI on other domains; AI teammates are a slice of their own
    ({"player_count_human": 1, "player_count_ai": 3, "humans_on_my_domain": 1, "humans_on_other_domains": 0,
      "ai_on_my_domain": 0, "ai_on_other_domains": 3}, "solo_vs_3plus_ai"),
    ({"player_count_human": 1, "player_count_ai": 3, "humans_on_my_domain": 1, "humans_on_other_domains": 0,
      "ai_on_my_domain": 1, "ai_on_other_domains": 2}, "solo_with_ai_team"),
    ({"player_count_human": 1, "player_count_ai": 1, "ai_on_my_domain": 1, "ai_on_other_domains": 0}, "solo_with_ai_team"),
    ({"player_count_human": 1, "player_count_ai": 0, "ai_on_my_domain": 0, "ai_on_other_domains": 0}, "solo_alone"),
)


def check_setup_table(classify):
    """Every row of SETUP_TABLE through `classify`; raises AssertionError naming the first row it gets wrong."""
    for props, want in SETUP_TABLE:
        got = classify(props)[0]
        if got != want:
            raise AssertionError(f"{props} -> {got}, expected {want}")


class Setups(StoreCase):
    """Shombith, 2026-11-07: "a sliced grouping in overview with competitive, teams, teams vs ai as per the domains in
    both multiplayer and for solo like the same how many AIs"."""

    def seed(self):
        old = S.now_ms() - 30 * DAY
        team = lambda mine, other, **ai: dict(humans_on_my_domain=mine, humans_on_other_domains=other, domain="Jade", **ai)
        # the PostHog history: no domain fields, so its multiplayer reads not recorded; solo still has its AI count
        self.store.ingest([started("u1", "o1", old, humans=2, ai=0), started("u2", "o1", old, humans=2, ai=0),
                           completed("u1", "o1", old + 60_000),
                           ev("game_started", "u3", old, match_id="o2", player_count_human=1, ai_count=1,
                              game_mode="SkimRace")], trusted=True, max_age_days=365)
        self.store.ingest([
            started("p1", "m1", T0, ai=0), completed("p1", "m1", T0 + 60_000),
            started("p1", "m2", T0 + 120_000, ai=1),
            started("p2", "m3", T0, ai=2), started("p2", "m4", T0 + 300_000, ai=5),
            started("p3", "m5", T0, ai=3, **team(1, 0, ai_on_my_domain=1, ai_on_other_domains=2)),
            started("p3", "m6", T0 + 300_000, ai=2, **team(1, 0, ai_on_my_domain=0, ai_on_other_domains=2)),
            started("q1", "c1", T0, humans=2, ai=0, **team(1, 1)), started("q2", "c1", T0, humans=2, ai=0, **team(1, 1)),
            completed("q1", "c1", T0 + 60_000, player_won=False), completed("q2", "c1", T0 + 60_000, player_won=True),
            *[started(f"t{i}", "k1", T0, humans=4, ai=0, **team(2, 2)) for i in range(4)],
            started("v1", "a1", T0, humans=2, ai=3, **team(2, 0, ai_on_my_domain=0, ai_on_other_domains=3)),
            started("v2", "a1", T0, humans=2, ai=3, **team(2, 0, ai_on_my_domain=0, ai_on_other_domains=3)),
            # uneven: the pair are Teams, the one is Competitive, so the match counts in both
            started("x1", "e1", T0, humans=3, ai=0, **team(2, 1)), started("x2", "e1", T0, humans=3, ai=0, **team(2, 1)),
            started("x3", "e1", T0, humans=3, ai=0, **team(1, 2)),
        ])
        self.store.rebuild_aggregates()

    def test_each_setup_comes_from_its_props_and_old_props_are_never_guessed(self):
        check_setup_table(S.setup_of)
        self.assertEqual(S.setup_of({"player_count_human": 2, "humans_on_my_domain": 2, "humans_on_other_domains": 0,
                                     "ai_on_my_domain": 1, "ai_on_other_domains": 3}), ("teams_vs_ai", 2, 0, 1, 3))
        self.assertEqual(S.setup_of({"player_count_human": 2}), ("multi_unrecorded", None, None, None, None))
        for props, want in SETUP_TABLE:                     # a setup's group always matches the play type's
            group = "multi" if (S.as_int(props.get("player_count_human")) or 0) >= 2 else None
            self.assertEqual(V.SETUP_GROUP[want] == "multiplayer", group == "multi", props)
        self.assertEqual(S.SETUP_IDS[:4], ("competitive", "teams", "teams_vs_ai", "multi_unrecorded"))  # multiplayer first

    def test_a_broken_classification_fails_the_table(self):
        # negative control: swap Competitive and Teams, and the same check must fail
        def broken(props):
            got = S.setup_of(props)
            swap = {"competitive": "teams", "teams": "competitive"}
            return (swap.get(got[0], got[0]),) + got[1:]
        with self.assertRaisesRegex(AssertionError, "expected competitive"):
            check_setup_table(broken)
        with self.assertRaisesRegex(AssertionError, "expected solo_vs_2_ai"):     # and with ai_count ignored
            check_setup_table(lambda props: S.setup_of({k: v for k, v in props.items() if k != "ai_count"}))

    def test_plays_store_the_setup_and_the_counts(self):
        self.seed()
        agg = sqlite3.connect(self.store.agg_path)
        rows = {r[0]: r[1:] for r in agg.execute(
            "SELECT match_id || ':' || COALESCE(humans_mine, -1), setup, humans_mine, humans_other, ai_mine, ai_other "
            "FROM plays")}
        self.assertEqual(rows["o1:-1"], ("multi_unrecorded", None, None, None, None))
        self.assertEqual(rows["o2:-1"], ("solo_vs_1_ai", None, None, None, None))     # ai_count, the old fallback
        self.assertEqual(rows["m5:1"], ("solo_with_ai_team", 1, 0, 1, 2))
        self.assertEqual(rows["a1:2"], ("teams_vs_ai", 2, 0, 0, 3))
        self.assertEqual(rows["e1:2"][0], "teams")
        self.assertEqual(rows["e1:1"][0], "competitive")
        play_types = dict(agg.execute("SELECT match_id, play_type FROM plays WHERE match_id IN ('o1', 'c1', 'm1')"))
        self.assertEqual(play_types, {"o1": "multi_human_unknown", "c1": "adversarial", "m1": "solo_alone"})  # as it was

    def test_overview_lists_every_setup_in_order_with_its_counts(self):
        self.seed()
        page = V.overview(self.store.agg_path, "release", 90)
        setups = page["setups"]
        self.assertEqual([s["setup"] for s in setups], list(S.SETUP_IDS))
        self.assertEqual([s["group"] for s in setups], ["multiplayer"] * 4 + ["solo"] * 5)
        got = {s["setup"]: (s["matches"], s["plays"], s["players"]) for s in setups}
        self.assertEqual(got, {"competitive": (2, 3, 3), "teams": (2, 6, 6), "teams_vs_ai": (1, 2, 2),
                               "multi_unrecorded": (1, 2, 2), "solo_alone": (1, 1, 1), "solo_vs_1_ai": (2, 2, 2),
                               "solo_vs_2_ai": (2, 2, 2), "solo_vs_3plus_ai": (1, 1, 1), "solo_with_ai_team": (1, 1, 1)})
        one = {s["setup"]: s for s in setups}
        self.assertEqual((one["competitive"]["finished_pct"], one["competitive"]["win_pct"]), (66.7, 50.0))
        self.assertEqual((one["teams"]["finished_pct"], one["teams"]["win_pct"]), (0.0, None))
        self.assertEqual(one["solo_with_ai_team"]["label"], "With AI teammates")
        self.assertEqual(page["setup_history"]["first_domain_day"], day(T0))
        self.assertEqual((page["setup_history"]["multi_plays"], page["setup_history"]["multi_unrecorded_pct"]), (13, 15.4))
        self.assertTrue(any("Teams not recorded" in n for n in page["setup_notes"]))
        solo = {s["setup"]: s["matches"] for s in V.overview(self.store.agg_path, "release", 90, "solo")["setups"]}
        # the Play filter reaches the setups. Its Solo is play_type solo_alone (player_count_ai 0 or missing), so the
        # PostHog play that sent only ai_count is in it, and its setup says what it really was: vs 1 AI
        self.assertEqual({k: n for k, n in solo.items() if n}, {"solo_alone": 1, "solo_vs_1_ai": 1})
        multi = {s["setup"]: s["players"] for s in V.overview(self.store.agg_path, "release", 90, "multi")["setups"]}
        self.assertEqual((multi["teams"], multi["solo_vs_1_ai"]), (6, 0))
        history = V.overview(self.store.agg_path, "release", 90, "all",
                             {"id": "h", "from": day(S.now_ms() - 40 * DAY), "to": day(T0), "source": "imported", "builds": []})
        self.assertEqual({s["setup"]: s["matches"] for s in history["setups"] if s["matches"]},
                         {"multi_unrecorded": 1, "solo_vs_1_ai": 1})          # the history: solo yes, teams not recorded

    def test_series_splits_every_plays_measure_by_setup(self):
        self.seed()
        agg = self.store.agg_path
        out = V.series(agg, "matches", "setup", "none", "release", 90)
        self.assertEqual(out["keys"], list(S.SETUP_IDS))                       # the setups' own order
        self.assertEqual({p["key"]: p["value"] for p in out["points"]}["teams"], 2)
        self.assertTrue(any("Teams not recorded" in n for n in out["notes"]))
        for measure in V.PLAY_MEASURES + ("players", "new_players"):
            self.assertIn("setup", V.series(agg, measure, "none", "day", "release", 7)["answerable_by"])
            V.series(agg, measure, "setup", "week", "release", 30)
        for measure in ("events",) + V.FREESTYLE_MEASURES:                      # negative control: not a match
            with self.assertRaises(V.BadRequest):
                V.series(agg, measure, "setup", "day", "release", 7)
        players = {p["key"]: p["value"] for p in V.series(agg, "players", "setup", "none", "release", 90)["points"]}
        self.assertEqual((players["teams"], players["competitive"]), (6, 3))
        solo = V.series(agg, "players", "setup", "none", "release", 90, "solo_vs_ai")
        self.assertEqual(solo["keys"], ["solo_vs_1_ai", "solo_vs_2_ai", "solo_vs_3plus_ai", "solo_with_ai_team"])
        self.assertNotIn("play", solo["unfiltered"])
        pct = {p["key"]: p["value"] for p in V.series(agg, "finished_pct", "setup", "none", "release", 90)["points"]}
        self.assertEqual((pct["competitive"], pct["solo_alone"]), (66.7, 100.0))

    def test_the_setups_hold_no_player_ids(self):
        self.seed()
        dump = "\n".join(sqlite3.connect(self.store.agg_path).iterdump())
        for player in ("u1", "p1", "q1", "t0", "v1", "x3"):
            self.assertNotIn(f"'{player}'", dump)


class SetupsHttp(unittest.TestCase):
    tearDown = Http.tearDown
    call = Http.call

    def setUp(self):
        Http.setUp(self)
        team = dict(humans_on_my_domain=1, humans_on_other_domains=1)
        self.call("POST", "/batch/", {"api_key": "in-key", "batch": [
            started("p1", "m1", T0, ai=2), started("q1", "c1", T0, humans=2, ai=0, **team),
            started("q2", "c1", T0, humans=2, ai=0, **team)]})
        self.app.rebuild()

    def test_overview_and_series_carry_the_setups_and_bad_input_is_400(self):
        status, page, _ = self.call("GET", "/v1/overview?days=7", key="read-key")
        self.assertEqual(status, 200)
        self.assertEqual([(s["setup"], s["matches"]) for s in page["setups"] if s["matches"]],
                         [("competitive", 1), ("solo_vs_2_ai", 1)])
        status, out, _ = self.call("GET", "/v1/series?measure=matches&by=setup&over=none&days=7", key="read-key")
        self.assertEqual((status, {p["key"]: p["value"] for p in out["points"]}), (200, {"competitive": 1, "solo_vs_2_ai": 1}))
        for bad in ("measure=events&by=setup", "measure=freestyle_entries&by=setup", "by=setups", "by=setup&days=5",
                    "by=setup&play=teams"):
            status, out, _ = self.call("GET", "/v1/series?" + bad, key="read-key")
            self.assertEqual(status, 400, bad)
            self.assertTrue(out["error"])



# ---------------------------------------------------------------- round 67: sessions, balance, quits, invites, errors,
# backups, the Ask door end to end (Shombith, 2026-11-07)

R67_EVENTS = ("session_started", "match_abandoned", "client_exception")


def r67_extra():
    """The round's new events the schema does not list yet (the game's half lands them in EVENT_SCHEMA.json)."""
    have = set(A.schema_events(A.REPO_SCHEMA))
    return sorted({"client_exception"} | {e for e in R67_EVENTS if e not in have})


def write(path, content):
    with open(path, "wb" if isinstance(content, bytes) else "w") as handle:
        handle.write(content)


def days_ago(n, hour=12):
    from datetime import datetime, timedelta, timezone
    d = datetime.now(timezone.utc).replace(hour=hour, minute=0, second=0, microsecond=0) - timedelta(days=n)
    return int(d.timestamp() * 1000)


class R67Case(StoreCase):
    def setUp(self):
        self.dir = tempfile.mkdtemp()
        self.store = S.Store(self.dir, A.schema_events(A.REPO_SCHEMA), r67_extra())

    def dump(self):
        return "\n".join(sqlite3.connect(self.store.agg_path).iterdump())


class Sessions(R67Case):
    def test_dau_wau_mau_and_session_lengths(self):
        d10, d3 = days_ago(10), days_ago(3)
        self.store.ingest([
            ev("menu_ready", "zq-p1", d10), ev("menu_ready", "zq-p1", d3), ev("menu_ready", "zq-p2", d3 + 1000),
            ev("session_started", "zq-p1", d3, session_id="s1", launch_kind="cold"),
            ev("session_started", "zq-p1", d3 + 50, session_id="s1", launch_kind="cold"),       # sent again: once
            ev("session_ended", "zq-p1", d3 + 600_000, session_id="s1", session_seconds=600, reason="quit"),
            ev("session_started", "zq-p1", d3 + 3_600_000, session_id="s2", launch_kind="resume"),
            ev("session_ended", "zq-p1", d3 + 3_720_000, session_id="s2", reason="pause"),      # no seconds: the gap
            ev("session_started", "zq-p1", d3 + 7_200_000, session_id="s3", launch_kind="cold"),   # never ended
            ev("session_started", "zq-p2", d3 + 1000, session_id="t1", launch_kind="cold"),
        ])
        self.store.rebuild_aggregates()
        page = V.sessions(self.store.agg_path, "release", 30)
        per = {r["day"]: r for r in page["per_day"]}
        self.assertEqual((per[S.day_of(d3)]["dau"], per[S.day_of(d3)]["wau"], per[S.day_of(d3)]["mau"]), (2, 2, 2))
        five = S.day_of(days_ago(5))
        self.assertEqual((per[five]["dau"], per[five]["wau"], per[five]["mau"]), (0, 1, 1))   # p1's day 10 is 5 days back
        self.assertEqual(per[S.day_of(days_ago(1))]["wau"], 2)
        self.assertEqual(per[S.day_of(days_ago(0))]["wau"], 2)
        self.assertEqual(per[S.day_of(days_ago(0))]["mau"], 2)
        t = page["tiles"]
        self.assertEqual((t["sessions"], t["ended"], t["sessions_per_player_day"]), (4, 2, 2.0))
        self.assertEqual(t["median_session_min"], 6.0)          # 600 s and 120 s
        self.assertEqual({r["bucket"]: r["sessions"] for r in page["lengths"]}["5-15 min"], 1)
        self.assertEqual(sum(r["cold"] for r in page["sessions_per_day"]), 3)
        self.assertEqual((t["wau"], t["mau"]), (2, 2))
        ov = V.overview(self.store.agg_path, "release", 30)["sessions"]
        self.assertEqual((ov["mau"], ov["sessions"]), (2, 4))
        self.assertNotIn("zq-p", self.dump())


class Balance(R67Case):
    def seed(self):
        rows = []
        for i, (diff, intensity, won, place, score) in enumerate([
                ("Easy", 1, True, 1, 900), ("Easy", 1, False, 2, 700), ("Hard", 3, False, 3, 400),
                ("Hard", 3, False, 4, 300), (None, 2, True, None, None)]):
            m = f"bm{i}"
            extra = {"score": score, "placement": place, "players_in_match": 4, "behind_by": 0 if won else 100}
            extra = {k: v for k, v in extra.items() if v is not None}
            if diff:
                extra["ai_difficulty"] = diff
            rows += [started("zq-b", m, T0 + i * 600_000, intensity=intensity, ai=3),
                     completed("zq-b", m, T0 + i * 600_000 + 100_000, player_won=won, **extra)]
        self.store.ingest(rows)
        self.store.rebuild_aggregates()

    def test_score_placement_and_difficulty(self):
        self.seed()
        page = V.balance(self.store.agg_path, "release", 7)
        self.assertEqual([r["ai_difficulty"] for r in page["by_difficulty"]], ["Easy", "Hard", "unknown"])
        easy = page["by_difficulty"][0]
        self.assertEqual((easy["plays"], easy["win_pct"], easy["avg_placement"], easy["avg_score"]), (2, 50.0, 1.5, 800.0))
        self.assertEqual(page["by_difficulty"][1]["avg_behind_by"], 100.0)
        cell = {(r["intensity"], r["ai_difficulty"]): r for r in page["grid"]}
        self.assertEqual(cell[("3", "Hard")]["win_pct"], 0.0)
        self.assertEqual({r["placement"]: r["plays"] for r in page["placements"]}, {1: 1, 2: 1, 3: 1, 4: 1})
        self.assertEqual((page["tiles"]["scored"], page["tiles"]["with_difficulty"]), (4, 4))
        out = V.series(self.store.agg_path, "score", "ai_difficulty", "none", "release", 7)
        self.assertEqual([(p["key"], p["value"]) for p in out["points"]], [("Easy", 800.0), ("Hard", 350.0), ("unknown", None)])
        self.assertEqual({p["key"]: p["value"] for p in V.series(
            self.store.agg_path, "placement", "intensity", "none", "release", 7)["points"]}["3"], 3.5)
        self.assertEqual(V.series(self.store.agg_path, "win_pct", "none", "none", "release", 7)["points"][0]["value"], 40.0)
        players = V.series(self.store.agg_path, "players", "ai_difficulty", "none", "release", 7)
        self.assertEqual({p["key"]: p["value"] for p in players["points"]}, {"Easy": 1, "Hard": 1, "unknown": 1})
        self.assertTrue(any("2026-11-07" in n for n in out["notes"]))
        with self.assertRaises(V.BadRequest):
            V.series(self.store.agg_path, "events", "ai_difficulty", "none", "release", 7)


class Quits(R67Case):
    def test_only_builds_that_send_quits_count_and_the_chain_follows_a_fail(self):
        f = days_ago(9)
        new = dict(app_version="0.3.0")
        self.store.ingest([
            started("zq-q1", "old1", T0, app_version="0.2.1"),                     # an older build: not in the rate
            ev("session_started", "zq-q1", T0 - 1000, session_id="x", launch_kind="cold", **new),
            started("zq-q1", "n1", T0, **new), completed("zq-q1", "n1", T0 + 90_000, **new),
            started("zq-q1", "n2", T0 + 200_000, **new),
            ev("match_abandoned", "zq-q1", T0 + 245_000, match_id="n2", game_mode="SkimRace", reason="quit_to_menu",
               elapsed_seconds=45, score_at_quit=10, **new),
            # the chain: a fail, a quit 20 minutes later, then nothing for 7 days
            ev("session_started", "zq-q2", f - 1000, session_id="y", launch_kind="cold", **new),
            started("zq-q2", "f1", f, **new),
            ev("repeated_game_fail", "zq-q2", f + 60_000, game_mode="SkimRace", fail_count=3, **new),
            ev("match_abandoned", "zq-q2", f + 1_260_000, match_id="f1", reason="app_quit", elapsed_seconds=400, **new),
            # a fail followed by a quit only after 2 hours: not part of the chain (negative control)
            started("zq-q3", "g1", f, **new),
            ev("repeated_game_fail", "zq-q3", f + 60_000, game_mode="Joust", fail_count=4, **new),
            ev("match_abandoned", "zq-q3", f + 7_400_000, match_id="g1", reason="disconnect", elapsed_seconds=700, **new),
            ev("menu_ready", "zq-q3", days_ago(6), **new),
        ])
        self.store.rebuild_aggregates()
        page = V.quits(self.store.agg_path, "release", 30)
        t = page["tiles"]
        self.assertEqual((t["plays"], t["tracked_plays"], t["quits"]), (5, 4, 3))   # old1 is from a build that never says
        self.assertEqual(t["quit_pct"], 75.0)
        self.assertEqual({r["reason"]: r["quits"] for r in page["reasons"]},
                         {"quit_to_menu": 1, "app_quit": 1, "disconnect": 1})
        self.assertEqual({r["bucket"]: r["quits"] for r in page["by_elapsed"]}["30-60 s"], 1)
        c = page["chain"]
        self.assertEqual((c["failed"], c["quit_after"], c["quit_mature"], c["gone_7d"]), (2, 1, 1, 1))
        self.assertEqual((c["failed_mature"], c["failed_gone_7d"]), (2, 1))          # q3 came back on day 6 before now
        self.assertEqual(self.rows("SELECT COUNT(*) FROM events WHERE event='match_abandoned'")[0][0], 3)
        out = V.series(self.store.agg_path, "abandon_pct", "game_mode", "none", "release", 30)
        self.assertEqual({p["key"]: p["value"] for p in out["points"]}["SkimRace"], 75.0)
        self.assertEqual(sqlite3.connect(self.store.agg_path).execute(
            "SELECT abandoned FROM plays WHERE match_id='n1'").fetchone()[0], 0)
        self.assertNotIn("zq-q", self.dump())


class Invites(R67Case):
    def test_an_invite_id_is_followed_exactly_and_older_invites_by_time(self):
        j = days_ago(9)
        minute = 60_000
        self.store.ingest([
            ev("party_invite_sent", "zq-a", j, target_id="zq-b", invite_id="inv1"),
            ev("party_invite_received", "zq-b", j + 5_000, host_id="zq-a", invite_id="inv1"),
            ev("party_joined", "zq-b", j + 20_000, invite_id="inv1", inviter_id="zq-a"),
            started("zq-a", "pt1", j + 5 * minute, humans=2, ai=0), started("zq-b", "pt1", j + 5 * minute, humans=2, ai=0),
            ev("menu_ready", "zq-b", days_ago(6)),                                   # back on day 3
            ev("menu_ready", "zq-a", days_ago(8)),                                   # the inviter back on day 1
            # an older build: no invite id, matched by time
            ev("party_invite_sent", "zq-c", j, target_id="zq-d"),
            ev("party_invite_received", "zq-d", j + 2 * minute, host_id="zq-c"),
            ev("party_joined", "zq-d", j + 4 * minute),
            # received 20 minutes after the send: too late to be this invite (negative control)
            ev("party_invite_sent", "zq-e", j, target_id="zq-f"),
            ev("party_invite_received", "zq-f", j + 20 * minute, host_id="zq-e"),
            ev("party_joined", "zq-f", j + 21 * minute),
        ])
        self.store.rebuild_aggregates()
        page = V.invites(self.store.agg_path, "release", 30)
        self.assertEqual([s["count"] for s in page["funnel"]], [3, 2, 2, 1, 1])
        self.assertEqual(page["funnel"][-1]["of"], 2)
        method = {m["method"]: m for m in page["by_method"]}
        self.assertEqual((method["invite_id"]["joined"], method["time"]["sent"], method["time"]["joined"]), (1, 2, 1))
        roles = {r["role"]: r for r in page["roles"]}
        self.assertEqual((roles["inviter"]["players"], roles["inviter"]["d1_pct"]), (2, 50.0))
        self.assertEqual((roles["invitee"]["players"], roles["invitee"]["d7_pct"], roles["invitee"]["d1_pct"]), (2, 50.0, 0.0))
        self.assertEqual(page["history"]["first_invite_id_day"], S.day_of(j))
        dump = self.dump()
        self.assertNotIn("zq-", dump)
        self.assertNotIn("inv1", dump)


class Errors(R67Case):
    def test_errors_group_by_frame_count_players_and_keep_no_text(self):
        boom = dict(exception_type="NullReferenceException", top_frame="CosmicShore.Game.VesselStatus.Update",
                    scene="Minigame_SkimRace", message_hash="a1b2c3", message="Kestrel's vessel was null")
        self.store.ingest([
            ev("client_exception", "zq-e1", T0, count=3, **boom), ev("client_exception", "zq-e2", T0 + 1000, count=1, **boom),
            ev("client_exception", "zq-e1", days_ago(20), exception_type="ArgumentException", top_frame="CosmicShore.UI.Menu.Open",
               scene="Menu_Main", message_hash="ffff00", count=1, platform="Android"),
        ])
        self.store.rebuild_aggregates()
        page = V.errors(self.store.agg_path, "release", 30)
        t = page["tiles"]
        self.assertEqual((t["events"], t["occurrences"], t["signatures"], t["players"], t["new_this_week"]), (3, 5, 2, 2, 1))
        top = page["by_frame"][0]
        self.assertEqual((top["top_frame"], top["occurrences"], top["players"], top["new"]),
                         ("CosmicShore.Game.VesselStatus.Update", 4, 2, True))
        self.assertFalse(page["by_frame"][1]["new"])
        self.assertEqual({r["key"]: r["occurrences"] for r in page["by_platform"]}, {"WindowsPlayer": 4, "Android": 1})
        dump = self.dump()
        self.assertNotIn("Kestrel", dump)
        self.assertNotIn("zq-e", dump)

    def test_data_health_lists_this_rounds_events(self):
        self.store.ingest([ev("session_started", "zq-h", T0, session_id="s", launch_kind="cold"),
                           completed("zq-h", "m", T0, score=5)])
        self.store.rebuild_aggregates()
        listed = {(r["event"], r["field"]): r for r in V.health(self.store.agg_path, {}, 30)["new_events"]}
        self.assertEqual(listed[("session_started", None)]["rows"], 1)
        self.assertIsNotNone(listed[("session_started", None)]["last_received_ms"])
        self.assertEqual(listed[("game_completed", "score")]["rows"], 1)
        self.assertEqual(listed[("game_completed", "ai_difficulty")]["rows"], 0)
        self.assertIsNone(listed[("match_abandoned", None)]["last_received_ms"])


class Backups(R67Case):
    def test_a_backup_is_a_snapshot_of_both_databases_and_restores(self):
        import tarfile
        self.store.ingest([started("zq-k", "m1", T0), completed("zq-k", "m1", T0 + 1000)])
        self.store.rebuild_aggregates()
        path, size = self.store.backup()
        self.assertTrue(path.endswith(".tar.gz") and size > 0)
        self.assertEqual(os.stat(path).st_mode & 0o777, 0o600)
        with tarfile.open(path) as tar:
            self.assertEqual(sorted(tar.getnames()), ["aggregates.db", "events.db"])
        self.store.ingest([started("zq-k", "m2", T0 + 5000)])        # after the backup: gone after a restore
        target = tempfile.mkdtemp()
        try:
            write(os.path.join(target, "events.db"), "the database it replaces")
            out = S.restore_backup(path, target)
            self.assertEqual(out["restored"], ["aggregates.db", "events.db"] if out["restored"][0] == "aggregates.db"
                             else ["events.db", "aggregates.db"])
            db = sqlite3.connect(os.path.join(target, "events.db"))
            self.assertEqual(db.execute("SELECT COUNT(*) FROM events").fetchone()[0], 2)
            db.close()
            with open(os.path.join(out["kept_in"], "events.db")) as kept:
                self.assertEqual(kept.read(), "the database it replaces")
            # not one of ours: refused, nothing moved
            bad = os.path.join(target, "bad.tar.gz")
            with tarfile.open(bad, "w:gz") as tar:
                tar.add(os.path.join(target, "events.db"), arcname="../events.db")
            with self.assertRaises(S.Invalid):
                S.restore_backup(bad, target)
            broken = os.path.join(target, "broken.tar.gz")
            junk = os.path.join(target, "junk")
            write(junk, b"SQLite format 3\x00" + b"\xff" * 4000)
            with tarfile.open(broken, "w:gz") as tar:
                tar.add(junk, arcname="events.db")
            with self.assertRaises(S.Invalid):
                S.restore_backup(broken, target)
            db = sqlite3.connect(os.path.join(target, "events.db"))
            self.assertEqual(db.execute("SELECT COUNT(*) FROM events").fetchone()[0], 2)    # still the restored one
            db.close()
        finally:
            shutil.rmtree(target)

    def test_the_newest_fourteen_are_kept(self):
        folder = os.path.join(self.dir, "backups")
        os.makedirs(folder)
        for i in range(15):
            write(os.path.join(folder, f"froglet-analytics-20260101-0000{i:02d}-000000.tar.gz"), "x")
        write(os.path.join(folder, "events-20260101.db"), "an older kind: left alone")
        path, _ = self.store.backup()
        kept = sorted(f for f in os.listdir(folder) if f.endswith(".tar.gz"))
        self.assertEqual(len(kept), 14)
        self.assertEqual(kept[-1], os.path.basename(path))
        self.assertNotIn("froglet-analytics-20260101-000000-000000.tar.gz", kept)
        self.assertNotIn("froglet-analytics-20260101-000001-000000.tar.gz", kept)
        self.assertTrue(os.path.exists(os.path.join(folder, "events-20260101.db")))
        facts = self.store.health_facts()["backups"]
        self.assertEqual((facts["local_kept"], facts["local_file"]), (14, os.path.basename(path)))
        self.assertIn("Oracle", facts["off_vm"])


def verify_sigv4(method, path, query, headers, body, secrets_by_access):
    """An S3 server's side of Signature Version 4, written apart from the server's signer: rebuild the canonical
    request from what arrived and compare. Returns None when it holds, else the reason."""
    import hashlib
    import hmac as hm
    from urllib.parse import parse_qsl, quote
    auth = headers.get("Authorization") or ""
    m = re.match(r"AWS4-HMAC-SHA256 Credential=([^/]+)/(\d{8})/([^/]+)/s3/aws4_request, "
                 r"SignedHeaders=([a-z0-9;-]+), Signature=([0-9a-f]{64})$", auth)
    if not m:
        return "no signature"
    access, day, region, names, signature = m.groups()
    if access not in secrets_by_access:
        return "unknown access key"
    if hashlib.sha256(body).hexdigest() != headers.get("x-amz-content-sha256"):
        return "the body does not match its hash"
    canon_headers = "".join(f"{n}:{(headers.get(n) or '').strip()}\n" for n in names.split(";"))
    canon_query = "&".join(f"{quote(k, safe='-_.~')}={quote(v, safe='-_.~')}" for k, v in sorted(parse_qsl(query, True)))
    canonical = "\n".join([method, path, canon_query, canon_headers, names, headers["x-amz-content-sha256"]])
    amz_date = headers.get("x-amz-date") or ""
    scope = f"{day}/{region}/s3/aws4_request"
    string = "\n".join(["AWS4-HMAC-SHA256", amz_date, scope, hashlib.sha256(canonical.encode()).hexdigest()])
    k = hm.new(("AWS4" + secrets_by_access[access]).encode(), day.encode(), hashlib.sha256).digest()
    for part in (region, "s3", "aws4_request"):
        k = hm.new(k, part.encode(), hashlib.sha256).digest()
    expected = hm.new(k, string.encode(), hashlib.sha256).hexdigest()
    return None if hm.compare_digest(expected, signature) else "signature mismatch"


class BucketUpload(unittest.TestCase):
    def setUp(self):
        from http.server import BaseHTTPRequestHandler
        stored = self.stored = {}

        class FakeS3(BaseHTTPRequestHandler):
            def log_message(self, *a):
                pass

            def do_PUT(self):
                body = self.rfile.read(int(self.headers.get("Content-Length") or 0))
                path, _, query = self.path.partition("?")
                why = verify_sigv4("PUT", path, query, {k: v for k, v in self.headers.items()} | {
                    k.lower(): v for k, v in self.headers.items()}, body, {"AKFROGLET": "right-secret"})
                if why:
                    self.send_response(403)
                    self.end_headers()
                    self.wfile.write(why.encode())
                    return
                stored[path] = body
                self.send_response(200)
                self.end_headers()

        self.s3 = ThreadingHTTPServer(("127.0.0.1", 0), FakeS3)
        threading.Thread(target=self.s3.serve_forever, daemon=True).start()
        self.env = {"FA_BACKUP_ENDPOINT": f"http://127.0.0.1:{self.s3.server_address[1]}", "FA_BACKUP_BUCKET": "fa-backups",
                    "FA_BACKUP_ACCESS_KEY": "AKFROGLET", "FA_BACKUP_SECRET_KEY": "right-secret"}

    def tearDown(self):
        self.s3.shutdown()
        self.s3.server_close()

    def test_the_signer_matches_aws_published_example(self):
        h = A.sigv4_headers("GET", "https://examplebucket.s3.amazonaws.com/test.txt", {"range": "bytes=0-9"},
                            "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855", "AKIAIOSFODNN7EXAMPLE",
                            "wJalrXUtnFEMI/K7MDENG/bPxRfiCYEXAMPLEKEY", "us-east-1", "20130524T000000Z")
        self.assertTrue(h["authorization"].endswith(
            "Signature=f0e8bdb87c964420e857bd35b5d6ed310bd44f0170aba48dd91039c6036bdb41"))

    def test_a_signed_put_is_accepted_and_a_wrong_secret_is_refused(self):
        settings = A.backup_settings(self.env)
        self.assertEqual(settings["region"], "us-ashburn-1")
        self.assertEqual(A.s3_put(settings, "froglet-analytics/x.tar.gz", b"snapshot"), 200)
        self.assertEqual(self.stored["/fa-backups/froglet-analytics/x.tar.gz"], b"snapshot")
        wrong = dict(settings, secret="wrong-secret")
        with self.assertRaises(urllib.error.HTTPError) as caught:
            A.s3_put(wrong, "froglet-analytics/y.tar.gz", b"snapshot")
        self.assertEqual(caught.exception.code, 403)
        self.assertNotIn("/fa-backups/froglet-analytics/y.tar.gz", self.stored)
        self.assertIsNone(A.backup_settings(dict(self.env, FA_BACKUP_SECRET_KEY="")))   # a key missing: dormant

    def test_the_nightly_backup_uploads_only_when_the_bucket_is_set(self):
        work = tempfile.mkdtemp()
        app = A.App(dict(A.DEFAULTS, data_dir=work, extra_events=r67_extra()))
        saved = {k: os.environ.get(k) for k in self.env}
        try:
            for k in self.env:
                os.environ.pop(k, None)
            out = app.backup()
            self.assertIsNone(out["uploaded"])
            self.assertEqual(self.stored, {})
            os.environ.update(self.env)
            out = app.backup()
            self.assertEqual(out["uploaded"], "fa-backups/froglet-analytics/" + os.path.basename(out["file"]))
            self.assertEqual(len(self.stored), 1)
            facts = json.dumps(app.store.health_facts())
            self.assertIn("fa-backups", facts)
            self.assertNotIn("right-secret", facts)
            self.assertNotIn("AKFROGLET", facts)
            app.store.mark_job("backup", "made a minute ago")
            real = C._client
            C._client = lambda: (None, "Claude is off in this test")             # a test never asks the real Claude
            try:
                self.assertIn("skipped", json.dumps(app.nightly()["backup"]))      # the 04:40 timer made tonight's
            finally:
                C._client = real
        finally:
            for k, v in saved.items():
                if v is None:
                    os.environ.pop(k, None)
                else:
                    os.environ[k] = v
            app.store.db.close()
            shutil.rmtree(work)


class R67Http(unittest.TestCase):
    tearDown = Http.tearDown
    call = Http.call

    def setUp(self):
        self.dir = tempfile.mkdtemp()
        config = dict(A.DEFAULTS, data_dir=self.dir, ingest_keys=["in-key"], read_keys={"ian": "read-key"},
                      admin_keys={"admin": "admin-key"}, extra_events=r67_extra())
        self.app = A.App(config)
        self.server = ThreadingHTTPServer(("127.0.0.1", 0), A.make_handler(self.app))
        self.base = f"http://127.0.0.1:{self.server.server_address[1]}"
        threading.Thread(target=self.server.serve_forever, daemon=True).start()
        self.call("POST", "/batch/", {"api_key": "in-key", "batch": [
            ev("session_started", "zq-player-7", T0, session_id="s", launch_kind="cold"),
            started("zq-player-7", "m1", T0), completed("zq-player-7", "m1", T0 + 60_000, score=12, placement=1,
                                                       ai_difficulty="Medium"),
            ev("client_exception", "zq-player-7", T0, exception_type="E", top_frame="A.B.C", message_hash="h", count=1)]})
        self.app.rebuild()

    def test_the_new_pages_answer_readers_and_refuse_bad_input(self):
        for path in ("/v1/sessions", "/v1/balance", "/v1/quits", "/v1/invites", "/v1/errors"):
            status, page, _ = self.call("GET", path + "?days=7", key="read-key")
            self.assertEqual(status, 200, path)
            self.assertNotIn("zq-player-7", json.dumps(page), path)
            self.assertEqual(self.call("GET", path)[0], 401, path)
            self.assertEqual(self.call("GET", path + "?days=5", key="read-key")[0], 400, path)
            self.assertEqual(self.call("GET", path + "?era=nope", key="read-key")[0], 400, path)
        status, page, _ = self.call("GET", "/v1/balance?days=7&play=solo_vs_ai", key="read-key")
        self.assertEqual((status, page["by_difficulty"][0]["ai_difficulty"]), (200, "Medium"))
        status, page, _ = self.call("GET", "/v1/errors?days=7", key="read-key")
        self.assertEqual(page["tiles"]["players"], 1)
        status, out, _ = self.call("GET", "/v1/series?measure=score&by=ai_difficulty&over=none&days=7", key="read-key")
        self.assertEqual((status, out["points"][0]["value"]), (200, 12.0))
        for bad in ("measure=abandon_pcts", "measure=score&by=difficulty"):
            self.assertEqual(self.call("GET", "/v1/series?" + bad, key="read-key")[0], 400, bad)
        status, health, _ = self.call("GET", "/v1/health?days=30", key="read-key")
        self.assertEqual(status, 200)
        self.assertIn("Oracle", health["backups"]["off_vm"])
        self.assertTrue(any(r["event"] == "session_started" and r["rows"] == 1 for r in health["new_events"]))

    def test_ask_and_query_studio_send_claude_no_player_id_end_to_end(self):
        sent = []

        def fake_structured(client, model, effort, system, user, schema, max_tokens=4000):
            sent.append(system + "\n" + user)
            if schema is C.SQL_SCHEMA:
                return {"sql": "SELECT ai_difficulty, COUNT(*) AS plays, AVG(score) AS score FROM plays GROUP BY 1",
                        "can_answer": True, "why_not": ""}, None
            return {"answer": "Medium: 1 play, score 12.", "caveat": ""}, None

        real = (C._client, C._structured)
        C._client, C._structured = (lambda: (object(), None)), fake_structured
        try:
            status, answer, _ = self.call("POST", "/v1/ask", {"question": "How do players score by AI difficulty?"},
                                          key="read-key")
            # Query Studio's Ask: the same door, then the SQL it wrote run again through /v1/query
            status2, ran, _ = self.call("POST", "/v1/query", {"sql": answer["sql"]}, key="admin-key")
        finally:
            C._client, C._structured = real
        self.assertEqual((status, answer["rows"]), (200, [["Medium", 1, 12.0]]))
        self.assertEqual((status2, ran["rows"]), (200, [["Medium", 1, 12.0]]))
        self.assertEqual(len(sent), 2)
        text = "\n".join(sent)
        self.assertIn("client_errors", text)                 # the new tables are in the schema Claude is shown
        for forbidden in ("distinct_id", "zq-player-7", "player_id"):
            self.assertNotIn(forbidden, text, forbidden)
        # the ids exist in events.db: the door is what keeps them out (negative control of the assertion itself)
        self.assertEqual(self.app.store.db.execute("SELECT COUNT(*) FROM events WHERE distinct_id='zq-player-7'").fetchone()[0], 4)
        self.assertNotIn("zq-player-7", "\n".join(sqlite3.connect(self.app.agg).iterdump()))



class ClaudeWithoutAKey(unittest.TestCase):
    """2026-11-07: the VM's self-update refused #1789 because its venv has the anthropic SDK and no key. Newer SDKs build
    a client without a key and raise TypeError only when the request is sent, which nothing caught, so the nightly job
    died before marking itself done. Each of the three guards is held here on its own."""

    def setUp(self):
        import types
        self.saved = sys.modules.get("anthropic")

        class _Err(Exception):
            pass

        class Keyless:
            api_key = None
            auth_token = None

            class beta:
                class messages:
                    @staticmethod
                    def create(**_):
                        raise TypeError("Could not resolve authentication method")

        fake = types.ModuleType("anthropic")
        fake.Anthropic = Keyless
        fake.RateLimitError = fake.APIStatusError = fake.APIConnectionError = _Err
        sys.modules["anthropic"] = fake
        self.keyless = Keyless

    def tearDown(self):
        if self.saved is None:
            sys.modules.pop("anthropic", None)
        else:
            sys.modules["anthropic"] = self.saved

    def test_a_client_without_a_key_is_off(self):
        client, why = C._client()
        self.assertIsNone(client)
        self.assertIn("ANTHROPIC_API_KEY", why)

    def test_an_unexpected_error_from_the_sdk_is_a_reason_not_a_crash(self):
        parsed, why = C._structured(self.keyless(), "m", "low", "sys", "user", {"type": "object"})
        self.assertIsNone(parsed)
        self.assertIn("TypeError", why)

    def test_the_nightly_job_survives_a_failing_read(self):
        tmp = tempfile.mkdtemp()
        app = A.App(dict(A.DEFAULTS, data_dir=tmp, extra_events=r67_extra()))
        real = C.write_digest

        def boom(*_):
            raise TypeError("Could not resolve authentication method")
        C.write_digest = boom
        try:
            out = app.nightly()
            self.assertIn("TypeError", out["digest"]["author"])
            self.assertTrue(app.store.job("nightly"))       # marked done, so it does not rerun every few minutes
        finally:
            C.write_digest = real
            app.store.db.close()
            shutil.rmtree(tmp)


# ---------------------------------------------------------------- the web build, devices and build=all (2026-11-07)

def on(device_props, event_fn, *args, **kw):
    """An event from a build that sends the new envelope (device_props: platform, device, os...), or an older one."""
    out = event_fn(*args, **kw)
    out["properties"].update(device_props)
    return out


IPHONE = {"platform": "IPhonePlayer", "device": "iphone", "os": "iOS 17.1", "device_model": "iPhone15,2"}
IPAD = {"platform": "IPhonePlayer", "device": "ipad", "os": "iPadOS 17.1", "device_model": "iPad13,1"}
OLD_IOS = {"platform": "IPhonePlayer"}                       # a build from before device was sent
ANDROID_TABLET = {"platform": "Android", "device": "android_tablet", "os": "Android OS 14 / API-34"}
WEB_MOBILE = {"platform": "WebGLPlayer", "device": "web_mobile", "build_kind": "web"}
OLD_WEB = {"platform": "WebGLPlayer"}                        # no build_kind, no device: read as web from the platform


class DeviceAndWeb(StoreCase):
    def seed(self):
        t = T0
        batch = [on(IPHONE, started, "a1", "m1", t), on(IPHONE, started, "a1", "m2", t + 1000),
                 on(IPAD, started, "a2", "m3", t), on(OLD_IOS, started, "a3", "m4", t),
                 on(IPHONE, started, "a8", "m5", t), on(IPAD, started, "a8", "m6", t + 1000),   # one player, two devices
                 on(ANDROID_TABLET, started, "a4", "m7", t), on(WEB_MOBILE, started, "a5", "m8", t),
                 on(OLD_WEB, started, "a6", "m9", t),
                 started("a7", "m10", t), on({"platform": "WindowsEditor"}, started, "a7", "m11", t)]  # release + editor
        self.assertEqual(self.store.ingest(batch)["accepted"], len(batch))
        self.store.rebuild_aggregates()

    def test_the_build_and_device_come_from_the_client_else_from_the_platform(self):
        self.assertEqual(S.build_kind_of({"platform": "WebGLPlayer"}), "web")
        self.assertEqual(S.build_kind_of({"platform": "WebGLPlayer", "build_kind": "development"}), "development")
        self.assertEqual(S.build_kind_of({"platform": "WindowsEditor"}), "editor")         # the old rules still hold
        self.assertEqual(S.build_kind_of({"platform": "Android", "app_version": "1.0-port"}), "port")
        self.assertEqual(S.device_of(IPAD), "ipad")
        self.assertEqual(S.device_of(OLD_IOS), "ios")                      # old iOS: iPhone or iPad is not known
        self.assertEqual(S.device_of(OLD_WEB), "web")
        self.assertEqual(S.device_of({"platform": "Android"}), "android")
        self.assertEqual(S.device_of({"platform": "OSXEditor"}), "mac")
        self.assertEqual(S.device_of({"platform": "PS5"}), "other")
        self.assertEqual(S.device_of({"platform": "IPhonePlayer", "device": "toaster"}), "ios")   # a bad value is not kept
        self.assertEqual(S.os_family_of({"os": "Android OS 14 / API-34"}), "Android 14")
        self.assertEqual(S.os_family_of({}), "not recorded")
        self.assertEqual(S.os_family_of({"os": "iPhone OS 17_1"}), "iOS 17")            # a phone's browser (WebGL)
        self.assertEqual(S.os_family_of({"os": "Mac OS X 10_15_7"}), "macOS 10.15")
        self.assertEqual(S.os_family_of({"os": "PlayStation 5"}), "other")
        for group, members in S.DEVICE_GROUPS.items():
            self.assertTrue(set(members) <= set(S.DEVICES), group)

    def test_older_rows_get_a_device_and_old_webgl_rows_become_web_once(self):
        self.store.ingest([on(OLD_WEB, started, "w1", "m1", T0), on(IPAD, started, "w2", "m2", T0)])
        # make the database look like one from before the device column (the column empty, WebGL read as release)
        self.store.db.execute("UPDATE events SET device = NULL")
        self.store.db.execute("UPDATE events SET build_kind = 'release' WHERE platform = 'WebGLPlayer'")
        self.store.db.execute("DELETE FROM store_meta WHERE key = 'devices_filled'")
        self.store.db.commit()
        self.store.db.close()
        self.store = S.Store(self.dir, A.schema_events(A.REPO_SCHEMA), ["client_exception"])
        self.assertEqual(sorted(self.rows("SELECT distinct_id, device, build_kind FROM events")),
                         [("w1", "web", "web"), ("w2", "ipad", "release")])
        # negative control: once done it is not redone (a row changed by hand afterwards stays as it is)
        self.store.db.execute("UPDATE events SET device = NULL WHERE distinct_id = 'w2'")
        self.store.db.commit()
        self.store.db.close()
        self.store = S.Store(self.dir, A.schema_events(A.REPO_SCHEMA), ["client_exception"])
        self.assertEqual(self.rows("SELECT device FROM events WHERE distinct_id = 'w2'"), [(None,)])

    def test_the_device_filter_counts_each_player_once_inside_a_group(self):
        self.seed()
        agg = self.store.agg_path
        ipad = V.overview(agg, "release", 7, device="ipad")
        self.assertEqual((ipad["tiles"]["matches"], ipad["tiles"]["players"], ipad["device"]), (2, 2, "ipad"))
        ios = V.overview(agg, "release", 7, device="ios")
        self.assertEqual(ios["tiles"]["matches"], 6)
        # a1, a2, a3 and a8: a8 flew an iPhone and an iPad and is ONE player of iOS (negative control: summing the
        # devices would say 5)
        self.assertEqual(ios["tiles"]["players"], 4)
        by_device = {r["device"]: r["players"] for r in V.overview(agg, "release", 7)["marketing"]["by_device"]}
        self.assertEqual(by_device["iphone"] + by_device["ipad"] + by_device["ios"], 5)
        self.assertEqual(V.overview(agg, "release", 7, device="tablet")["tiles"]["players"], 3)    # a2, a4, a8
        self.assertEqual(V.overview(agg, "release", 7, device="phone")["tiles"]["players"], 2)     # a1, a8
        self.assertEqual(V.overview(agg, "release", 7, device="android_phone")["tiles"]["players"], 0)
        self.assertEqual(V.overview(agg, "release", 7)["tiles"]["players"], 6)   # a5 and a6 are on the web build
        self.assertIn("business", ipad["unfiltered"])                 # crystals carry no device: listed, not filtered
        self.assertNotIn("business", V.overview(agg, "release", 7)["unfiltered"])
        out = V.series(agg, "matches", "device", "none", "release", 7)
        self.assertEqual({p["key"]: p["value"] for p in out["points"]},
                         {"iphone": 3, "ipad": 2, "ios": 1, "android_tablet": 1, "windows": 1})
        self.assertTrue(any("not recorded" in n for n in out["notes"]))
        players = V.series(agg, "players", "device", "none", "release", 7)
        self.assertEqual({p["key"]: p["value"] for p in players["points"]}["ipad"], 2)
        os_split = {p["key"]: p["value"] for p in V.series(agg, "matches", "os", "none", "release", 7)["points"]}
        self.assertEqual((os_split["iOS 17"], os_split["iPadOS 17"], os_split["not recorded"]), (3, 2, 2))
        with self.assertRaises(V.BadRequest):
            V.series(agg, "players", "os", "none", "release", 7)    # players are not counted by OS
        with self.assertRaises(V.BadRequest):
            V.overview(agg, "release", 7, device="toaster")
        for page in (V.retention, V.matches, V.cohorts, V.sessions, V.balance, V.quits, V.invites, V.errors,
                     V.freestyle):
            self.assertEqual(page(agg, "release", 7, "all", None, "ipad")["device"], "ipad", page.__name__)

    def test_the_web_build_and_build_all_add_up_without_counting_anyone_twice(self):
        self.seed()
        agg = self.store.agg_path
        web = V.overview(agg, "web", 7)
        self.assertEqual((web["tiles"]["matches"], web["tiles"]["players"]), (2, 2))      # a5 (sent web) and a6 (WebGL)
        kinds = {b: V.overview(agg, b, 7)["tiles"] for b in S.BUILD_KINDS}
        every = V.overview(agg, "all", 7)["tiles"]
        self.assertEqual(every["matches"], sum(t["matches"] for t in kinds.values()))   # rows add up, no kind dropped
        self.assertEqual(every["matches"], 11)
        # a7 played on release AND in the editor: one player of All (negative control: the kinds sum to one more)
        self.assertEqual(sum(t["players"] for t in kinds.values()), every["players"] + 1)
        self.assertEqual(every["players"], 8)
        self.assertEqual(V.series(agg, "players", "none", "none", "all", 7)["points"][0]["value"], 8)
        split = V.series(agg, "matches", "build_kind", "none", "all", 7)
        self.assertEqual({p["key"]: p["value"] for p in split["points"]}, {"release": 8, "web": 2, "editor": 1})
        health = {r["build"]: r["starts"] for r in V.overview(agg, "all", 7)["match_health_by_build"]}
        self.assertEqual(sum(health.values()), 11)                         # by build: each play under its own kind
        self.assertEqual(V.audience(agg, "all", 7)["funnel"][0]["players"], 8)
        self.assertEqual(V.retention(agg, "all", 7)["build"], "all")
        h = V.health(agg, self.store.health_facts(), 30)
        builds = {r["build_kind"]: r["events"] for r in h["events_by_build"]}
        self.assertEqual((builds["web"], builds["editor"], builds["port"]), (2, 1, 0))      # every kind, zeros listed
        self.assertEqual({r["device"] for r in h["events_by_device"]},
                         {"iphone", "ipad", "ios", "android_tablet", "web_mobile", "web", "windows"})
        with self.assertRaises(V.BadRequest):
            V.overview(agg, "mobile", 7)

    def test_the_device_tables_hold_no_player_ids(self):
        self.seed()
        db = sqlite3.connect(self.store.agg_path)
        for table in ("player_counts_device", "active_rolling_device", "session_players_device", "cohort_weeks_device",
                      "cohort_days_device", "plays", "daily_events"):
            dump = json.dumps(db.execute(f"SELECT * FROM {table}").fetchall())
            for player in ("a1", "a2", "a8"):
                self.assertNotIn(f'"{player}"', dump, table)
        self.assertGreater(db.execute("SELECT COUNT(*) FROM player_counts_device WHERE device='ios'").fetchone()[0], 0)
        db.close()

    def test_saved_views_and_notebooks_keep_a_device_and_build_all(self):
        made = self.store.save_preset({"name": "iPad", "page": "overview", "chart_id": "overview.series", "series": True,
                                       "settings": {"measure": "matches"}, "filters": {"build": "all", "device": "ipad"}},
                                      "ian")
        self.assertEqual(made["filters"], {"build": "all", "device": "ipad"})
        for bad in ({"device": "toaster"}, {"build": "mobile"}):
            with self.assertRaises(S.Invalid):
                self.store.save_preset({"name": "x", "page": "overview", "chart_id": "overview.series",
                                        "settings": {}, "filters": bad}, "ian")
        import analytics_notebook as NB
        self.assertEqual(NB.check_filters({"device": "tablet", "build": "all"}), {"device": "tablet", "build": "all"})
        with self.assertRaises(NB.Refused):
            NB.check_filters({"device": "toaster"})
        self.seed()
        out = NB.run_series(self.store, self.store.agg_path, {"measure": "matches", "by": "none", "over": "none"},
                            {"build": "all", "days": 7, "device": "ipad"})
        self.assertEqual(out["points"][0]["value"], 2)


class DeviceHttp(unittest.TestCase):
    tearDown = Http.tearDown
    call = Http.call

    def setUp(self):
        Http.setUp(self)
        self.call("POST", "/batch/", {"api_key": "in-key", "batch": [
            on(IPAD, started, "d1", "m1", T0), on(OLD_WEB, started, "d2", "m2", T0), started("d3", "m3", T0)]})
        self.app.rebuild()

    def preflight(self, path, origin):
        request = urllib.request.Request(self.base + path, method="OPTIONS")
        request.add_header("Origin", origin)
        request.add_header("Access-Control-Request-Method", "POST")
        with urllib.request.urlopen(request) as response:
            return response.headers

    def test_every_read_route_takes_device_and_build_all_and_refuses_bad_values(self):
        routes = ("/v1/overview", "/v1/retention", "/v1/matches", "/v1/health", "/v1/audience", "/v1/ftue",
                  "/v1/economy", "/v1/social", "/v1/cohorts", "/v1/playtypes", "/v1/freestyle", "/v1/sessions",
                  "/v1/balance", "/v1/quits", "/v1/invites", "/v1/errors", "/v1/people", "/v1/eras",
                  "/v1/series?measure=players&by=device&over=week",
                  "/v1/series?measure=matches&by=os&over=none", "/v1/dashboards/tile?source=builtin:new_players")
        for path in routes:
            sep = "&" if "?" in path else "?"
            for good in ("device=ipad", "device=web", "build=all", "build=web", "build=all&device=tablet"):
                status, page, _ = self.call("GET", path + sep + good + "&days=7", key="admin-key")
                self.assertEqual(status, 200, (path, good, page))
            for bad in ("device=toaster", "device=iPad", "build=mobile"):
                self.assertEqual(self.call("GET", path + sep + bad, key="admin-key")[0], 400, (path, bad))
        page = self.call("GET", "/v1/audience?days=7&device=ipad", key="read-key")[1]
        self.assertEqual(page["unfiltered"], ["page"])                          # the Players page has no device yet
        page = self.call("GET", "/v1/overview?days=7&device=ipad", key="read-key")[1]
        self.assertEqual((page["tiles"]["matches"], page["device"]), (1, "ipad"))
        page = self.call("GET", "/v1/overview?days=7&build=web", key="read-key")[1]
        self.assertEqual(page["tiles"]["matches"], 1)
        people = self.call("GET", "/v1/people?days=7&device=ipad&build=all", key="admin-key")[1]
        self.assertEqual([(p["distinct_id"], p["devices"]) for p in people["people"]], [("d1", ["ipad"])])
        status, cmp, _ = self.call("GET", "/v1/eras/compare?a=pre_beta&b=posthog&build=all&device=ios", key="read-key")
        self.assertEqual((status, cmp["device"]), (200, "ios"))

    def test_the_toybox_origin_may_send_events_but_never_read(self):
        toybox = "https://play.froglet.games"
        headers = self.preflight("/batch/", toybox)
        self.assertEqual(headers.get("Access-Control-Allow-Origin"), toybox)
        self.assertIn("POST", headers.get("Access-Control-Allow-Methods"))
        self.assertNotIn("GET", headers.get("Access-Control-Allow-Methods"))
        self.assertEqual(self.preflight("/v1/batch", "https://www.froglet.games").get("Access-Control-Allow-Origin"),
                         "https://www.froglet.games")
        status, out, headers = self.call("POST", "/batch/", {"api_key": "in-key", "batch": [
            on(WEB_MOBILE, started, "d4", "m4", T0)]}, origin=toybox)
        self.assertEqual((status, out["accepted"], headers.get("Access-Control-Allow-Origin")), (200, 1, toybox))
        # negative controls: the Toybox origin gets no CORS on a read route (preflight or answer) ...
        self.assertIsNone(self.preflight("/v1/overview", toybox).get("Access-Control-Allow-Origin"))
        _, _, headers = self.call("GET", "/v1/overview?days=7", key="read-key", origin=toybox)
        self.assertIsNone(headers.get("Access-Control-Allow-Origin"))
        # ... an unknown origin gets none anywhere ...
        self.assertIsNone(self.preflight("/batch/", "https://evil.example").get("Access-Control-Allow-Origin"))
        _, _, headers = self.call("POST", "/batch/", {"api_key": "in-key", "batch": []}, origin="https://evil.example")
        self.assertIsNone(headers.get("Access-Control-Allow-Origin"))
        # ... and the console's read origin still reads, as before
        _, _, headers = self.call("GET", "/v1/overview?days=7", key="read-key", origin="https://dash.example")
        self.assertEqual(headers.get("Access-Control-Allow-Origin"), "https://dash.example")
        self.assertIn("ingest_origins", A.LIVE_FIELDS)
        self.assertEqual(A.DEFAULTS["ingest_origins"], ["https://play.froglet.games", "https://www.froglet.games"])


if __name__ == "__main__":
    unittest.main()
