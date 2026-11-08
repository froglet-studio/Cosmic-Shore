"""Tests for the Notebook (analytics_notebook.py). Run with the server's: python3 analytics_server.py --self-test.
Claude is replaced by a fake, so no key and no network are needed."""
import json
import os
import shutil
import sys
import tempfile
import threading
import unittest
from http.server import ThreadingHTTPServer

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)

import analytics_claude as C  # noqa: E402
import analytics_notebook as NB  # noqa: E402
import analytics_server as A  # noqa: E402
import analytics_store as S  # noqa: E402
import analytics_views as V  # noqa: E402
import test_analytics_server as TS  # noqa: E402  (its helpers; imported as a module so its cases run once)

# Player ids shaped like the real ones (UGS ids are long and mixed), so a leak would be a real leak.
P1 = "Qk7xLm2PzR9wTb4NvC8d"
P2 = "sample-player-0002"


def data(store):
    store.ingest([TS.started(P1, "m1", TS.T0), TS.completed(P1, "m1", TS.T0 + 60_000),
                  TS.started(P2, "m2", TS.T0, humans=1, ai=0), TS.completed(P2, "m2", TS.T0 + 90_000),
                  TS.ev("$identify", P1, TS.T0 + 1000, **{"$set": {"display_name": "Tidewalker"}})])
    store.rebuild_aggregates()


def cells(*extra):
    return [{"id": "c1", "kind": "text", "source": "# Launch week\nHow do **solo** players do?"},
            {"id": "c2", "kind": "sql", "source": "SELECT play_type, COUNT(*) AS plays FROM plays GROUP BY play_type",
             "output": {"columns": ["play_type", "plays"], "rows": [["solo_vs_ai", 1], ["solo_alone", 1]],
                        "truncated": False, "ms": 1}},
            {"id": "c3", "kind": "chart", "chart": {"from": "c2", "form": "bars", "x": "play_type", "y": "plays"}},
            {"id": "c4", "kind": "chart", "chart": {"from": "series", "measure": "matches", "by": "play_type",
                                                     "over": "none", "form": "pie"}},
            *extra]


class FakeClaude:
    """Stands in for analytics_claude's client: records every prompt and answers from a script of replies."""

    def __init__(self, replies):
        self.replies, self.sent = list(replies), []

    def __enter__(self):
        self.real = (C._client, C._structured)

        def structured(client, model, effort, system, user, schema, max_tokens=4000):
            self.sent.append(system + "\n" + user)
            if schema is C.ANSWER_SCHEMA:
                return {"answer": "Solo vs AI and solo each played 1 match.", "caveat": "Two matches."}, None
            return self.replies.pop(0), None

        C._client, C._structured = (lambda: (object(), None)), structured
        return self

    def __exit__(self, *exc):
        C._client, C._structured = self.real


class NotebookStore(TS.StoreCase):
    def setUp(self):
        super().setUp()
        data(self.store)

    def book(self, **extra):
        return dict({"name": "Launch week", "visibility": "me", "filters": {"build": "release", "days": 90},
                     "cells": cells()}, **extra)

    def test_a_private_notebook_is_its_owners_alone(self):
        mine = NB.save(self.store, self.book(), "ian", True)
        self.assertEqual((mine["owner"], mine["cell_count"], mine["cells"][2]["chart"]["from"]), ("ian", 4, "c2"))
        # negative controls: another key (an admin's too) neither lists, opens, changes nor deletes it
        self.assertEqual(NB.notebooks(self.store, "kim", True), [])
        self.assertIsNone(NB.notebook(self.store, mine["id"], "kim", True))
        with self.assertRaises(NB.Refused):
            NB.save(self.store, {"id": mine["id"], "name": "mine now"}, "kim", True)
        self.assertIsNone(NB.delete(self.store, mine["id"], "kim", True, moderator=True))
        self.assertEqual([n["id"] for n in NB.notebooks(self.store, "ian", False)], [mine["id"]])

    def test_sharing_works_as_saved_queries_do(self):
        readers = NB.save(self.store, self.book(name="For everyone", visibility="readers"), "ian", False)
        admins = NB.save(self.store, self.book(name="For admins", visibility="admins"), "ian", False)
        self.assertEqual({n["name"] for n in NB.notebooks(self.store, "kim", False)}, {"For everyone"})
        self.assertEqual({n["name"] for n in NB.notebooks(self.store, "kim", True)}, {"For everyone", "For admins"})
        with self.assertRaises(PermissionError):                 # a shared one stays its owner's to change
            NB.save(self.store, {"id": readers["id"], "name": "taken"}, "kim", False)
        with self.assertRaises(PermissionError):
            NB.delete(self.store, readers["id"], "kim", False)
        copy = NB.save(self.store, {"duplicate": readers["id"]}, "kim", False)
        self.assertEqual((copy["owner"], copy["visibility"], copy["name"], len(copy["cells"])),
                         ("kim", "me", "Copy of For everyone", 4))
        renamed = NB.save(self.store, {"id": copy["id"], "name": "Kim's week"}, "kim", False)
        self.assertEqual((renamed["name"], renamed["cells"], renamed["created_ms"]),
                         ("Kim's week", copy["cells"], copy["created_ms"]))
        self.assertTrue(NB.delete(self.store, admins["id"], "kim", True, moderator=True))

    def test_a_notebook_never_keeps_a_player_id(self):
        # negative controls: a result column that names a player, and a player's id as a value or in a note
        out = {"columns": ["distinct_id", "plays"], "rows": [["x", 1]]}
        for bad, reason in (
                (cells({"id": "c9", "kind": "sql", "source": "SELECT 1", "output": out}), "player id column"),
                (cells({"id": "c9", "kind": "sql", "source": "SELECT 1",
                        "output": {"columns": ["who"], "rows": [[P1]]}}), "player's id"),
                (cells({"id": "c9", "kind": "text", "source": f"Look at {P2} again"}), "player's id")):
            with self.assertRaises(NB.Refused) as caught:
                NB.save(self.store, self.book(cells=bad), "ian", True)
            self.assertIn(reason, str(caught.exception))
        with self.assertRaises(NB.Refused):
            NB.save(self.store, self.book(name=f"About {P1}"), "ian", True)
        # the positive control: the same notebook without them is kept
        self.assertEqual(NB.save(self.store, self.book(), "ian", True)["cell_count"], 4)

    def test_bad_cells_are_refused_with_a_reason(self):
        for bad in ([{"kind": "html", "source": "x"}], [{"kind": "chart", "chart": {"from": "nope"}}],
                    [{"kind": "chart", "chart": {"from": "series", "measure": "events", "by": "game_mode"}}],
                    [{"kind": "text", "source": "x", "output": {}}], [{"kind": "text", "source": "x" * 9000}],
                    [{"id": "a", "kind": "text"}, {"id": "a", "kind": "text"}], [{"kind": "text"}] * 81, "cells"):
            with self.assertRaises(NB.Refused, msg=str(bad)[:80]):
                NB.save(self.store, self.book(cells=bad), "ian", True)
        for bad in ({"build": "mobile"}, {"days": 5}, {"era": "no_such_era"}, {"colour": "jade"}):
            with self.assertRaises(NB.Refused, msg=str(bad)):
                NB.save(self.store, self.book(filters=bad), "ian", True)

    def test_a_sql_cell_reads_the_aggregates_only(self):
        self.assertEqual(NB.run_sql(self.store.agg_path, "SELECT COUNT(*) AS plays FROM plays")["rows"], [[2]])
        # negative controls: events.db and people are not reachable, nothing but a SELECT runs
        for sql in ("SELECT * FROM events", "SELECT distinct_id FROM people", "ATTACH DATABASE 'events.db' AS raw",
                    "DELETE FROM plays", "SELECT 1; DROP TABLE plays"):
            with self.assertRaises(V.UnsafeQuery, msg=sql):
                NB.run_sql(self.store.agg_path, sql)
        with self.assertRaises(NB.Refused):                     # a result that only calls itself a player id
            NB.run_sql(self.store.agg_path, "SELECT match_id AS player_id FROM plays")

    def test_claude_sees_totals_rerun_here_and_never_a_player_id(self):
        earlier = cells({"id": "c5", "kind": "text", "source": f"{P1} and {P2} told us it was hard"},
                        {"id": "c6", "kind": "ask", "source": "Which play type finishes more?"})
        earlier[1]["output"]["rows"].append([P1, 999])         # a result the browser made up is never what is sent
        replies = [{"text": "Matches by play type.", "sql": "SELECT * FROM events",
                    "chart": {"form": "bars", "x": "play_type", "y": "matches"}},
                   {"text": "Matches by play type.", "sql": "SELECT play_type, COUNT(*) AS matches FROM plays "
                                                            "GROUP BY play_type ORDER BY play_type",
                    "chart": {"form": "bars", "x": "play_type", "y": "matches"}}]
        with FakeClaude(replies) as fake:
            out = NB.ask(self.store, self.store.agg_path, "Now split that by platform", earlier,
                         {"build": "release", "days": 90, "play": "all"})
        self.assertEqual([c["kind"] for c in out["cells"]], ["text", "sql", "chart"])
        self.assertEqual(out["cells"][0]["source"], "Solo vs AI and solo each played 1 match.\n\nTwo matches.")
        self.assertEqual(out["cells"][1]["output"]["rows"], [["solo_alone", 1], ["solo_vs_ai", 1]])
        self.assertEqual(out["cells"][2]["chart"], {"from": out["cells"][1]["id"], "form": "bars", "x": "play_type",
                                                    "y": "matches"})
        self.assertEqual(len(fake.sent), 3)                      # the reply, one retry, the answer from the rows
        self.assertIn("events", fake.sent[1].split("\n\n")[-1])  # the retry was told why the first query failed
        prompt = fake.sent[0]
        self.assertIn("CREATE TABLE plays", prompt)
        self.assertIn("build_kind = 'release', the last 90 days", prompt)
        self.assertIn('"rows": [["solo_alone", 1], ["solo_vs_ai", 1]]', prompt)   # c2, run here
        self.assertIn("[player id removed]", prompt)
        for text in fake.sent:                                   # the negative control: no id, no name, no made-up row
            for secret in (P1, P2, "Tidewalker", "dev-" + P1, "999"):
                self.assertNotIn(secret, text)
        # the shape guard on its own: an id-shaped token nobody knows is cut out too
        self.assertEqual(NB.scrub(self.store, "ask about Zz9Yy8Xx7Ww6Vv5Uu4Tt3 now"), "ask about [id removed] now")

    def test_without_claude_the_notebook_says_so(self):
        real = C._client
        C._client = lambda: (None, "the anthropic package is not installed on the server")
        try:
            out = NB.ask(self.store, self.store.agg_path, "anything", [], {})
        finally:
            C._client = real
        self.assertIn("off", out["error"])
        self.assertEqual(NB.ask(self.store, self.store.agg_path, "  ", [], {})["error"], "Ask a question first.")


class NotebookHttp(unittest.TestCase):
    tearDown = TS.Http.tearDown
    call = TS.Http.call

    def setUp(self, **config):
        self.dir = tempfile.mkdtemp()
        settings = dict(A.DEFAULTS, data_dir=self.dir, ingest_keys=["in-key"],
                        read_keys={"ian": "ian-key", "kim": "kim-key"}, admin_keys={"admin": "admin-key"},
                        ask_daily_limit=2)
        settings.update(config)
        self.app = A.App(settings)
        self.server = ThreadingHTTPServer(("127.0.0.1", 0), A.make_handler(self.app))
        self.base = f"http://127.0.0.1:{self.server.server_address[1]}"
        threading.Thread(target=self.server.serve_forever, daemon=True).start()
        data(self.app.store)

    def book(self, **extra):
        return dict({"name": "Launch week", "visibility": "me", "filters": {"build": "release", "days": 90},
                     "cells": cells()}, **extra)

    def test_make_open_rename_duplicate_and_delete(self):
        self.assertEqual(self.call("GET", "/v1/notebooks")[0], 401)
        status, made, _ = self.call("POST", "/v1/notebooks", self.book(), key="ian-key")
        self.assertEqual((status, made["owner"]), (200, "ian"))
        listed = self.call("GET", "/v1/notebooks", key="ian-key")[1]["notebooks"]
        self.assertEqual([(n["id"], n["cell_count"]) for n in listed], [(made["id"], 4)])
        self.assertNotIn("cells", listed[0])                    # the list is summaries
        self.assertEqual(self.call("GET", "/v1/notebooks?id=" + made["id"], key="ian-key")[1]["cells"], made["cells"])
        status, renamed, _ = self.call("PUT", "/v1/notebooks", {"id": made["id"], "name": "Week one"}, key="ian-key")
        self.assertEqual((status, renamed["name"], len(renamed["cells"])), (200, "Week one", 4))
        # negative controls: a reader cannot see, open, change or delete another's private notebook
        self.assertEqual(self.call("GET", "/v1/notebooks", key="kim-key")[1]["notebooks"], [])
        self.assertEqual(self.call("GET", "/v1/notebooks?id=" + made["id"], key="kim-key")[0], 404)
        self.assertEqual(self.call("PUT", "/v1/notebooks", {"id": made["id"], "name": "x"}, key="kim-key")[0], 400)
        self.assertEqual(self.call("DELETE", "/v1/notebooks?id=" + made["id"], key="kim-key")[0], 404)
        self.assertEqual(self.call("POST", "/v1/notebooks", {"duplicate": made["id"]}, key="kim-key")[0], 400)
        # shared with readers: kim sees it, cannot change it, duplicates it as her own
        self.call("PUT", "/v1/notebooks", {"id": made["id"], "visibility": "readers"}, key="ian-key")
        self.assertEqual(self.call("PUT", "/v1/notebooks", {"id": made["id"], "name": "x"}, key="kim-key")[0], 403)
        status, copy, _ = self.call("POST", "/v1/notebooks", {"duplicate": made["id"], "name": "Kim's"}, key="kim-key")
        self.assertEqual((status, copy["owner"], copy["name"]), (200, "kim", "Kim's"))
        self.assertEqual(self.call("DELETE", "/v1/notebooks?id=" + made["id"], key="kim-key")[0], 403)
        self.assertEqual(self.call("DELETE", "/v1/notebooks?id=" + made["id"], key="ian-key")[0], 200)
        self.assertEqual([n["name"] for n in self.call("GET", "/v1/notebooks", key="kim-key")[1]["notebooks"]], ["Kim's"])

    def test_a_save_with_a_player_id_column_is_400(self):
        bad = cells({"id": "c9", "kind": "sql", "source": "SELECT 1",
                     "output": {"columns": ["player_id"], "rows": [["x"]]}})
        status, out, _ = self.call("POST", "/v1/notebooks", self.book(cells=bad), key="ian-key")
        self.assertEqual(status, 400)
        self.assertIn("player id column", out["error"])

    def test_run_one_cell(self):
        status, out, _ = self.call("POST", "/v1/notebooks/run", {"cell": cells()[1]}, key="ian-key")
        self.assertEqual((status, out["output"]["columns"]), (200, ["play_type", "plays"]))
        status, out, _ = self.call("POST", "/v1/notebooks/run", {"cell": cells()[3], "filters": {"days": 30}},
                                   key="ian-key")
        self.assertEqual((status, out["output"]["by"], out["output"]["days"]), (200, "play_type", 30))
        self.assertEqual(sum(p["value"] for p in out["output"]["points"]), 2)
        # negative controls: events.db is out of reach, a player column is refused, a text cell has nothing to run
        for cell in ({"kind": "sql", "source": "SELECT distinct_id FROM events"},
                     {"kind": "sql", "source": "SELECT 'x' AS distinct_id"}, {"kind": "text", "source": "hi"}):
            self.assertEqual(self.call("POST", "/v1/notebooks/run", {"cell": cell}, key="ian-key")[0], 400, cell)

    def test_ask_answers_as_cells_and_counts_toward_the_daily_questions(self):
        reply = {"text": "Matches by play type.", "sql": "SELECT play_type, COUNT(*) AS matches FROM plays GROUP BY 1",
                 "chart": {"form": "pie", "x": "play_type", "y": "matches"}}
        with FakeClaude([reply, reply]) as fake:
            status, out, _ = self.call("POST", "/v1/notebooks/ask", {"question": "Matches by play type?",
                                                                    "cells": cells(), "filters": {"days": 30}},
                                       key="ian-key")
            self.assertEqual((status, [c["kind"] for c in out["cells"]], out["asks_left"]), (200, ["text", "sql", "chart"], 1))
            self.call("POST", "/v1/notebooks/ask", {"question": "Again?", "cells": [], "filters": {}}, key="ian-key")
            status, out, _ = self.call("POST", "/v1/notebooks/ask", {"question": "Third?"}, key="ian-key")
        self.assertEqual(status, 429)                            # the limit is the Ask box's, shared with it
        self.assertEqual(self.app.store.asks_today("ian"), 2)
        self.assertFalse(any(P1 in t or P2 in t for t in fake.sent))
        self.assertEqual(self.call("POST", "/v1/notebooks/ask", {"question": "x", "filters": {"build": "mobile"}},
                                   key="kim-key")[0], 400)

    def test_a_question_asked_while_claude_is_off_costs_nothing(self):
        real = C._client
        C._client = lambda: (None, "no Claude credentials on the server")
        try:
            status, out, _ = self.call("POST", "/v1/notebooks/ask", {"question": "Anything?"}, key="ian-key")
        finally:
            C._client = real
        self.assertEqual((status, out["asks_left"], "off" in out), (200, 2, False))
        self.assertIn("Claude is off", out["error"])
        self.assertEqual(self.app.store.asks_today("ian"), 0)

    def test_the_console_serves_the_notebook_page(self):
        for path in ("/notebook.js", "/notebook.css"):
            import urllib.request
            with urllib.request.urlopen(self.base + path) as response:
                self.assertEqual(response.status, 200, path)
        with open(os.path.join(A.CONSOLE_DIR, "index.html"), encoding="utf-8") as handle:
            html = handle.read()
        self.assertIn('<script src="notebook.js"></script>', html)
        self.assertIn('data-page="notebook"', html)


class NotebookReadSplit(NotebookHttp):
    """With all_keys_admin off, running a query stays an admin's, as in the Query Studio; a reader may still ask."""

    def setUp(self):
        super().setUp(all_keys_admin=False)

    def test_make_open_rename_duplicate_and_delete(self):
        pass

    test_a_save_with_a_player_id_column_is_400 = test_ask_answers_as_cells_and_counts_toward_the_daily_questions = \
        test_a_question_asked_while_claude_is_off_costs_nothing = \
        test_the_console_serves_the_notebook_page = test_make_open_rename_duplicate_and_delete

    def test_run_one_cell(self):
        self.assertEqual(self.call("POST", "/v1/notebooks/run", {"cell": cells()[1]}, key="ian-key")[0], 403)
        self.assertEqual(self.call("POST", "/v1/notebooks/run", {"cell": cells()[1]}, key="admin-key")[0], 200)
        self.assertEqual(self.call("POST", "/v1/notebooks/run", {"cell": cells()[3]}, key="ian-key")[0], 200)


if __name__ == "__main__":
    unittest.main()
