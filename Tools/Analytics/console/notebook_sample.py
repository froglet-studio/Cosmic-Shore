"""The review copy's sample notebooks (make_preview.py calls add()). Each SQL cell is the real guarded query run on the
invented sample aggregates, and the notes in Claude's voice are written from those rows by this script, not by
Claude (the notebook says so). They are saved through analytics_notebook.save, so the sample passes the same checks a
real notebook does (no player-id column, no player's id anywhere)."""
import analytics_notebook as NB

LABELS = {"solo_vs_ai": "Solo vs AI", "solo_alone": "Solo", "coop": "Co-op", "adversarial": "Versus",
          "mixed": "Mixed teams", "multi_human_unknown": "Multi-human (teams not recorded)"}
REMATCH = ("SELECT play_type, COUNT(*) AS completed, ROUND(100.0 * AVG(rematched), 1) AS rematch_pct\n"
           "FROM plays\nWHERE build_kind = 'release' AND completed = 1 AND day >= date('now', '-90 days')\n"
           "GROUP BY play_type\nORDER BY rematch_pct DESC")
MODES = ("SELECT game_mode, COUNT(*) AS plays, ROUND(100.0 * AVG(completed), 1) AS finished_pct\n"
         "FROM plays\nWHERE build_kind = 'release' AND day >= date('now', '-90 days')\n"
         "GROUP BY game_mode\nHAVING COUNT(*) >= 3\nORDER BY finished_pct ASC\nLIMIT 8")
NOTE = "Preview: the notes marked Claude were written from the rows by the preview script; on the live console Claude writes them."


def _run(agg, sql):
    out = NB.run_sql(agg, sql)
    return out, out["rows"]


def add(store, agg, get, owner="preview"):
    rematch, r = _run(agg, REMATCH)
    modes, m = _run(agg, MODES)
    said = (f"{LABELS.get(r[0][0], r[0][0])} brings players back most: {r[0][2]}% of its {r[0][1]} completed matches "
            f"were followed by another within 10 minutes. {LABELS.get(r[-1][0], r[-1][0])} is lowest at {r[-1][2]}%."
            if r else "No completed release matches in the sample.")
    worst = (f"{m[0][0]} is finished least often ({m[0][2]}% of {m[0][1]} plays); {m[-1][0]} most often, at "
             f"{m[-1][2]}%." if m else "No release plays in the sample.")
    launch = [
        {"id": "intro", "kind": "text", "source": "# Launch week read\nWhat the first players did, for the Monday "
         "call. **Sample data**: every number here is invented.\n\n- Rematch by play type\n- Where matches are "
         "left unfinished\n- Matches by platform, week by week"},
        {"id": "q1", "kind": "ask", "source": "Which play type brings players back for a rematch?"},
        {"id": "a1", "kind": "text", "by": "claude", "source": said + "\n\n" + NOTE},
        {"id": "s1", "kind": "sql", "by": "claude", "source": REMATCH, "output": rematch},
        {"id": "g1", "kind": "chart", "by": "claude", "chart": {"from": "s1", "form": "bars", "x": "play_type",
                                                                "y": "rematch_pct"}},
        {"id": "q2", "kind": "ask", "source": "And which modes are left unfinished?"},
        {"id": "a2", "kind": "text", "by": "claude", "source": worst},
        {"id": "s2", "kind": "sql", "by": "claude", "source": MODES, "output": modes},
        {"id": "g2", "kind": "chart", "by": "claude", "chart": {"from": "s2", "form": "bars", "x": "game_mode",
                                                                "y": "finished_pct"}},
        {"id": "q3", "kind": "ask", "source": "Now matches by platform, week by week"},
        {"id": "g3", "kind": "chart", "by": "claude", "chart": {"from": "series", "measure": "matches",
                                                                "by": "platform", "over": "week", "form": "bars"}},
        {"id": "todo", "kind": "text", "source": "## To follow up\n1. Ask Caleb whether the FTUE drop matches the "
         "unfinished modes\n2. Re-read this after the closed beta's first week"},
    ]
    filters = {"build": "release", "days": 90, "play": "all"}
    made = [NB.save(store, {"name": "Toybox week by week", "visibility": "readers", "filters": filters, "cells": [
                {"id": "t", "kind": "text", "source": "Shared by Garrett: the Toybox over the last 90 days."},
                {"id": "c", "kind": "chart", "chart": {"from": "series", "measure": "freestyle_entries",
                                                       "by": "vessel_class", "over": "week", "form": "bars"}}]},
                    "garrett", True),
            NB.save(store, {"name": "Launch week read", "visibility": "readers", "filters": filters, "cells": launch},
                    owner, True)]                    # saved last, so it is the newest and the preview opens it
    get["/v1/notebooks"] = {"notebooks": NB.notebooks(store, owner, True)}
    for book in made:
        get["/v1/notebooks?id=" + book["id"]] = book
