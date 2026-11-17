"""Tests for the Funnel builder and Release compare (analytics_compare.py, Frogma Rev 66 A). Run with the rest:
python3 analytics_server.py --self-test."""
import json
import shutil
import tempfile
import threading
import unittest
from http.server import ThreadingHTTPServer

import analytics_compare as CMP
import analytics_server as A
import analytics_store as S
import test_analytics_server as T

ev, started, completed = T.ev, T.started, T.completed

DAY = 86400 * 1000
NOW = S.now_ms()
D10 = NOW - 10 * DAY                      # day 0 for the funnel players and for version A


def v(event, player, ms, version="0.9.13", **props):
    return ev(event, player, ms, app_version=version, **props)


def funnel_events():
    out = []
    # p1 does every step in order; p2 stops after the first match; p3 only launches, then opens settings;
    # p4 launches on Android and comes back the next day without a match
    for p, platform in (("p1", "WindowsPlayer"), ("p2", "WindowsPlayer"), ("p3", "WindowsPlayer"),
                        ("p4", "Android")):
        out.append(ev("game_first_launched", p, D10, platform=platform))
    out += [ev("ftue_step_completed", "p1", D10 + 60_000, quest_id="q", phase_index=0, step_id="boost",
               step_name="First boost", seconds_since_launch=60),
            started("p1", "m1", D10 + 120_000), completed("p1", "m1", D10 + 240_000),
            ev("menu_ready", "p1", D10 + DAY, seconds_since_launch=5, is_first=True),
            started("p2", "m2", D10 + 30_000), completed("p2", "m2", D10 + 90_000),
            ev("setting_changed", "p3", D10 + 5_000, setting="invert_y", value="true"),
            ev("menu_ready", "p4", D10 + DAY, platform="Android", seconds_since_launch=9, is_first=True)]
    return out


class FunnelCase(unittest.TestCase):
    def setUp(self):
        self.dir = tempfile.mkdtemp()
        self.store = S.Store(self.dir, A.schema_events(A.REPO_SCHEMA), ["client_exception"])
        self.store.ingest(funnel_events())

    def tearDown(self):
        self.store.db.close()
        shutil.rmtree(self.dir)

    def run_funnel(self, **body):
        db = self.store._read()
        try:
            return CMP.funnel(db, body, self.store.allowed_events)
        finally:
            db.close()

    def test_strict_order_counts_each_step_and_splits_by_platform(self):
        out = self.run_funnel(steps=["game_first_launched", "game_completed", "second_day"], within_days=7)
        self.assertEqual([s["players"] for s in out["steps"]], [4, 2, 1])
        self.assertEqual(out["steps"][1]["drop_pct"], 50.0)
        groups = {g["group"]: g["players"] for g in out["groups"]}
        self.assertEqual(groups, {"WindowsPlayer": [3, 2, 1], "Android": [1, 0, 0]})
        # the biggest drop is after step 1: p3's next event is the setting, p4 only opens the menu again
        self.assertEqual(out["instead"]["after_step"], 1)
        self.assertEqual({n["event"]: n["players"] for n in out["instead"]["next"]},
                         {"setting_changed": 1, "No later event": 1})   # a menu is bookkeeping, not a choice
        self.assertEqual((out["times"][0]["median_s"], out["times"][0]["p75_s"]), (165.0, 202.5))  # p1 240 s, p2 90 s
        self.assertNotIn("p1", json.dumps(out))

    def test_an_ftue_node_is_a_step_and_strict_order_is_strict(self):
        out = self.run_funnel(steps=[{"event": "game_first_launched"},
                                     {"event": "ftue_step_completed", "step_id": "boost"},
                                     {"event": "game_started"}])
        self.assertEqual([s["players"] for s in out["steps"]], [4, 1, 1])
        out = self.run_funnel(steps=["game_first_launched", "game_completed", "game_started"])
        self.assertEqual(out["steps"][2]["players"], 0)          # nobody started a match after finishing one
        out = self.run_funnel(steps=["game_first_launched", "game_completed", "game_started"], order="any")
        self.assertEqual(out["steps"][2]["players"], 2)          # in any order both did

    def test_within_one_day_cuts_a_step_two_days_later(self):
        self.store.ingest([ev("game_first_launched", "p5", D10), started("p5", "m5", D10 + 2 * DAY + 1000)])
        self.assertEqual(self.run_funnel(steps=["game_first_launched", "game_started"], within_days=1)
                         ["steps"][1]["players"], 2)
        self.assertEqual(self.run_funnel(steps=["game_first_launched", "game_started"], within_days=7)
                         ["steps"][1]["players"], 3)

    def test_bad_funnels_are_refused(self):
        for body in ({"steps": ["game_started"]}, {"steps": ["nope", "game_started"]},
                     {"steps": ["second_day", "game_started"]}, {"steps": ["a"] * 9},
                     {"steps": ["game_started", "game_completed"], "within_days": 3},
                     {"steps": ["game_started", "game_completed"], "order": "random"},
                     {"steps": ["game_started", {"event": "game_completed", "step_id": "x"}]},
                     {"steps": ["game_started", "game_completed"], "since": "2026-13-01"}):
            with self.assertRaises(S.Invalid, msg=json.dumps(body)):
                self.run_funnel(**body)

    def test_the_step_list_names_ftue_nodes(self):
        db = self.store._read()
        try:
            out = CMP.funnel_steps(db, self.store.allowed_events)
        finally:
            db.close()
        self.assertEqual(out["ftue_nodes"][0]["step_id"], "boost")
        self.assertEqual(out["ftue_nodes"][0]["name"], "First boost")
        self.assertEqual(out["events"][0], {"event": "game_first_launched", "players": 4})


def release_events():
    out = []
    # version A: 3 new players on D10; a1 and a2 come back the next day; 2 matches, 1 finished, one double-sent start
    for p in ("a1", "a2", "a3"):
        out.append(v("session_started", p, D10, session_id="s-" + p, launch_kind="cold"))
        out.append(v("menu_ready", p, D10 + 1000, seconds_since_launch=6, is_first=True))
    out += [v("menu_ready", "a1", D10 + 900_000, seconds_since_launch=900, is_first=False),   # a return, not a launch
            v("game_started", "a1", D10 + 2000, match_id="m1", player_count_human=1, player_count_ai=2,
              game_mode="SkimRace"),
            v("game_completed", "a1", D10 + 60_000, match_id="m1"),
            v("game_started", "a1", D10 + 120_000, match_id="m2", player_count_human=1, player_count_ai=2,
              game_mode="SkimRace"),
            v("game_started", "a1", D10 + 3_120_000, match_id="m2", player_count_human=1, player_count_ai=2,
              game_mode="SkimRace", intensity=3),                                    # the same match sent twice
            v("client_exception", "a2", D10 + 5000, exception_type="NullReferenceException", top_frame="A.B",
              message_hash="h1", count=2),
            v("session_started", "a1", D10 + DAY, session_id="s-a1b", launch_kind="cold"),
            v("session_started", "a2", D10 + DAY, session_id="s-a2b", launch_kind="cold")]
    # version B: 2 new players four days later, faster launches, every match finished, no exceptions
    d4 = D10 + 4 * DAY
    for p in ("b1", "b2"):
        out += [v("session_started", p, d4, version="0.9.14", session_id="s-" + p, launch_kind="cold"),
                v("menu_ready", p, d4 + 1000, version="0.9.14", seconds_since_launch=4, is_first=True),
                v("game_started", p, d4 + 2000, version="0.9.14", match_id="m-" + p, player_count_human=1,
                  player_count_ai=2, game_mode="Joust"),
                v("game_completed", p, d4 + 30_000, version="0.9.14", match_id="m-" + p)]
    out.append(v("game_started", "b1", d4 + 200_000, version="0.9.14", match_id="m-b1x", player_count_human=1,
                 player_count_ai=2, game_mode="Joust"))                              # a rematch within 10 minutes
    out.append(v("session_started", "a3", d4 + DAY, version="0.9.14", session_id="s-a3b", launch_kind="cold"))
    return out


class ReleaseCase(unittest.TestCase):
    tearDown = FunnelCase.tearDown

    def setUp(self):
        self.dir = tempfile.mkdtemp()
        self.store = S.Store(self.dir, A.schema_events(A.REPO_SCHEMA), ["client_exception"])
        self.store.ingest(release_events())

    def compare(self, **kw):
        db = self.store._read()
        try:
            return CMP.releases(db, **kw)
        finally:
            db.close()

    def test_versions_are_listed_newest_first_with_their_new_players(self):
        page = self.compare()
        self.assertEqual([(x["version"], x["new_players"]) for x in page["versions"]], [("0.9.14", 2), ("0.9.13", 3)])
        self.assertEqual({m["version"]: m["players"] for m in page["on_version_now"]}, {"0.9.14": 3})

    def test_b_is_read_against_a(self):
        page = self.compare(a="0.9.13", b="0.9.14")
        A_, B_ = page["a"]["values"], page["b"]["values"]
        self.assertEqual((A_["finished_pct"], B_["finished_pct"]), (50.0, 66.7))
        self.assertEqual(A_["rows_per_play"], 1.5)                # 3 start rows for 2 plays: the double-send shows
        self.assertEqual(A_["launch_p50_s"], 6)                   # the 900 s return to the menu is not a launch
        self.assertEqual(A_["exceptions_per_100_sessions"], 40.0)  # 2 exceptions over 5 sessions
        self.assertEqual(A_["d1_pct"], 66.7)
        self.assertEqual(B_["rematch_pct"], 50.0)
        self.assertEqual(page["b"]["return_curve"][1]["players"], 2)
        self.assertEqual(page["a"]["top_exceptions"][0], {"exception_type": "NullReferenceException",
                                                          "top_frame": "A.B", "count": 2})
        reads = {r["measure"]: r["read"] for r in page["reads"]}
        self.assertEqual(reads["finished_pct"], "too early")      # under 30 on either side
        self.assertEqual(page["top_modes"], ["Joust", "SkimRace"])
        self.assertNotIn("a1", json.dumps(page))

    def test_the_read_says_better_and_worse_once_both_sides_are_big_enough(self):
        self.assertEqual(CMP._read_of("x", True, 50, 60, 40, 40), "better")
        self.assertEqual(CMP._read_of("x", False, 5, 6, 40, 40), "worse")
        self.assertEqual(CMP._read_of("x", True, 50, 60, 40, 29), "too early")
        self.assertEqual(CMP._read_of("x", None, 1.0, 1.2, 40, 40), "changed")

    def test_bad_compares_are_refused(self):
        for kw in ({"a": "0.9.13"}, {"a": "0.9.13", "b": "9.9.9"}, {"span": "forever"}):
            with self.assertRaises(S.Invalid, msg=str(kw)):
                self.compare(**kw)


class CompareHttp(unittest.TestCase):
    tearDown = T.Http.tearDown
    call = T.Http.call

    def setUp(self):
        self.dir = tempfile.mkdtemp()
        config = dict(A.DEFAULTS, data_dir=self.dir, ingest_keys=["in-key"], read_keys={"ian": "read-key"},
                      admin_keys={"admin": "admin-key"}, extra_events=["client_exception"])
        self.app = A.App(config)
        self.server = ThreadingHTTPServer(("127.0.0.1", 0), A.make_handler(self.app))
        self.base = f"http://127.0.0.1:{self.server.server_address[1]}"
        threading.Thread(target=self.server.serve_forever, daemon=True).start()
        self.call("POST", "/batch/", {"api_key": "in-key", "batch": funnel_events() + release_events()})

    def test_readers_get_both_pages_and_bad_input_is_400(self):
        body = {"steps": ["game_first_launched", "game_completed"], "within_days": 7}
        status, out, _ = self.call("POST", "/v1/funnel", body, key="read-key")
        self.assertEqual((status, out["steps"][1]["players"]), (200, 2))
        self.assertEqual(self.call("POST", "/v1/funnel", body)[0], 401)
        self.assertEqual(self.call("POST", "/v1/funnel", dict(body, device="phone"), key="read-key")[1]["starters"], 0)
        self.assertEqual(self.call("POST", "/v1/funnel", dict(body, build="nope"), key="read-key")[0], 400)
        status, out, _ = self.call("GET", "/v1/funnel/steps", key="read-key")
        self.assertEqual((status, out["ftue_nodes"][0]["step_id"]), (200, "boost"))
        status, out, _ = self.call("GET", "/v1/releases?a=0.9.13&b=0.9.14", key="read-key")
        self.assertEqual((status, out["b"]["new_players"]), (200, 2))
        self.assertEqual(self.call("GET", "/v1/releases?a=0.9.13&b=x", key="read-key")[0], 400)
        self.assertEqual(self.call("GET", "/v1/releases", key=None)[0], 401)


if __name__ == "__main__":
    unittest.main()
