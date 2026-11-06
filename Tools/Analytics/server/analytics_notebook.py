"""The Notebook: a chat, a report and a notebook in one page of the analytics console (Shombith, 2026-11-07: "a chat
session or report data session or notebook inside the analytics tool").

A notebook is an ordered list of cells:
    text    a note in a small markdown subset (headings, lists, bold, code), written by a person or by Claude
    sql     one read-only SELECT over aggregates.db (the Query Studio's own door), with its result table
    chart   a drawing of a sql cell's result (from: <cell id>, form, x, y) or one /v1/series chart (from: series,
            measure, by, over) under the notebook's filters
    ask     a question to Claude. The answer comes back as new cells: a text cell, and when Claude wrote a query, a sql
            cell already run and a chart cell of it.

Claude sees the question, the notebook's filters, the aggregates schema and the notebook's earlier cells: their text,
their queries and the totals those return, RE-RUN HERE through the read-only door (never a result the browser sent),
a few hundred rows at most. It never sees events.db, a player id or a display name: every string of the prompt is
checked against the players events.db knows and any match is cut out, as is any long id-shaped token.

Stored in events.db, table `notebooks` (made here, on first use). A notebook holds no player ids: a save is refused
when a cell's result has a player-id column (distinct_id, player_id, install_id, ...) or when any of its text or
values is the id of a player events.db knows.

Routes (mounted by analytics_server.py; `h` is its request handler):
    GET    /v1/notebooks                 the notebooks this key may see (summaries)
    GET    /v1/notebooks?id=             one notebook, whole
    POST   /v1/notebooks                 {name, visibility, filters, cells} makes one; {duplicate: <id>, name?} copies one
    PUT    /v1/notebooks                 {id, name?, visibility?, filters?, cells?} changes one of the key's own
    DELETE /v1/notebooks?id=             the owner (or a key in admin_keys, for a shared one) deletes
    POST   /v1/notebooks/run             {cell, filters} runs one cell: sql (admin, as Query Studio) or a series chart
    POST   /v1/notebooks/ask             {question, cells, filters} -> {cells} Claude's answer as cells; counts toward
                                         the reader's daily questions (ask_daily_limit), as the Ask box does
Visibility is me | readers | admins, as saved queries and dashboards: a private notebook is never shown to another key.
"""
import hmac
import json
import re
import secrets

import analytics_claude as C
import analytics_store as S
import analytics_views as V

KINDS = ("text", "sql", "chart", "ask")
FORMS = ("bars", "line", "pie", "table", "number")
CLAUDE_FORMS = FORMS + ("none",)
AUTHORS = ("me", "claude")
MAX_NOTEBOOKS = 200
MAX_CELLS = 80
MAX_TEXT = 8000
MAX_SQL = 8000
MAX_QUESTION = 500
MAX_COLUMNS = 50
MAX_VALUE = 500
MAX_STORED = 1_500_000           # one notebook's cells as JSON, bytes
CONTEXT_CELLS = 16               # the earlier cells Claude is shown (the latest ones)
CONTEXT_ROWS = 300               # result rows across all of them
CONTEXT_ROWS_PER_CELL = 40
CONTEXT_TEXT = 1500
CELL_ID = re.compile(r"^[A-Za-z0-9_-]{1,40}$")
NOTEBOOK_ID = re.compile(r"^nb_[a-z0-9]{1,37}$")
# A column that names a player: distinct_id, player_id, install_id, user_id, inviter_id, ... A notebook keeps totals.
PLAYER_COLUMN = re.compile(r"(?:^|_)(?:distinct|player|install|user|inviter|invitee|person|ugs|account|device)_?id$",
                           re.IGNORECASE)
# Claude is shown no id of any kind: a column ending in id is left out of what it sees (match_id included).
ANY_ID_COLUMN = re.compile(r"(?:^|_)id$", re.IGNORECASE)
TOKEN = re.compile(r"[A-Za-z0-9_.:@-]{6,128}")
ID_SHAPED = re.compile(r"\b(?=[A-Za-z0-9]*[0-9])(?=[A-Za-z0-9]*[A-Za-z])[A-Za-z0-9]{20,}\b")
CELL_KEYS = {"id", "kind", "source", "by", "chart", "output", "at_ms"}
CHART_KEYS = {"from", "form", "x", "y", "measure", "by", "over", "title"}
OUTPUT_KEYS = {"columns", "rows", "truncated", "ms", "error", "ran_ms"}

TABLE = """
CREATE TABLE IF NOT EXISTS notebooks(
    id TEXT PRIMARY KEY, name TEXT NOT NULL, owner TEXT NOT NULL, visibility TEXT NOT NULL, filters TEXT NOT NULL,
    cells TEXT NOT NULL, created_ms INTEGER NOT NULL, updated_ms INTEGER NOT NULL
);
"""

REPLY_SCHEMA = {
    "type": "object",
    "properties": {
        "text": {"type": "string"},
        "sql": {"type": "string"},
        "chart": {
            "type": "object",
            "properties": {"form": {"type": "string", "enum": list(CLAUDE_FORMS)}, "x": {"type": "string"},
                           "y": {"type": "string"}},
            "required": ["form", "x", "y"],
            "additionalProperties": False,
        },
    },
    "required": ["text", "sql", "chart"],
    "additionalProperties": False,
}

NOTEBOOK_SYSTEM = """You are the analyst inside the Notebook of Froglet's game analytics console. A person asks a \
question in a notebook; you are shown its earlier cells (notes, earlier questions, queries and the totals those \
returned), so a follow-up ("now split that by platform", "only Joust") builds on what is already there.

Reply with JSON:
- text: when you write a query, one plain sentence saying what it computes (the numbers are written after it runs). \
When the earlier cells already answer the question, or these tables cannot, two or three plain sentences that answer \
from those cells or say what is missing; then sql is "".
- sql: one SQLite SELECT (or WITH ... SELECT) over the tables below, or "". Aggregate, and LIMIT 50 when listing.
- chart: how to draw the query's result. form is bars, line (a day or week column as x), pie (2 to 6 parts), \
table, number (one value) or none; x and y are column names of your result ("" when form is none).

The tables hold aggregates only; there are no player ids, and none may be asked for.

{schema}

Notes on the data: build_kind is release (real players), editor (team playtests in the Unity editor), port, or \
development. play_type multi_human_unknown means a multi-human match recorded before the team fields existed. \
rematched is NULL for plays that were not completed. Days are 'YYYY-MM-DD' strings in UTC; today is {today}.

The notebook's filters: {filters}. Apply them unless the question names other ones."""

RETRY = ("Your query was not run: {error}. Write a corrected query (the same JSON reply), or set sql to \"\" and say "
         "in text why these tables cannot answer it.")


class Refused(S.Invalid):
    """A notebook or cell the server will not keep (400 with the reason)."""


# ---------------------------------------------------------------- storage

def ensure(store):
    """Make the notebooks table in events.db (once per store)."""
    if getattr(store, "_notebooks_ready", False):
        return
    with store.lock:
        store.db.executescript(TABLE)
        store.db.commit()
    store._notebooks_ready = True


def _row(r, whole=True):
    cells = json.loads(r[5])
    out = {"id": r[0], "name": r[1], "owner": r[2], "visibility": r[3], "filters": json.loads(r[4]),
           "created_ms": r[6], "updated_ms": r[7], "cell_count": len(cells)}
    if whole:
        out["cells"] = cells
    return out


def notebooks(store, name, admin):
    """The notebooks a key may see: its own, the readers ones, and (an admin's key) the admins ones."""
    ensure(store)
    with store.lock:
        rows = store.db.execute("SELECT id, name, owner, visibility, filters, cells, created_ms, updated_ms "
                                "FROM notebooks ORDER BY updated_ms DESC").fetchall()
    return [_row(r, whole=False) for r in rows if S._visible(r[3], r[2], name, admin)]


def notebook(store, nb_id, name, admin):
    """One notebook this key may see, whole; None for no such id and for someone else's private one."""
    ensure(store)
    if not isinstance(nb_id, str) or not NOTEBOOK_ID.match(nb_id):
        return None
    with store.lock:
        r = store.db.execute("SELECT id, name, owner, visibility, filters, cells, created_ms, updated_ms "
                             "FROM notebooks WHERE id=?", (nb_id,)).fetchone()
    return _row(r) if r and S._visible(r[3], r[2], name, admin) else None


def check_filters(filters, store=None):
    if filters is None:
        return {}
    if not isinstance(filters, dict) or set(filters) - set(S.PRESET_FILTERS):
        raise Refused(f"filters must be an object with only {list(S.PRESET_FILTERS)}")
    out = {}
    if filters.get("build") not in (None, ""):
        if filters["build"] not in S.BUILD_KINDS:
            raise Refused(f"filters.build must be one of {list(S.BUILD_KINDS)}")
        out["build"] = filters["build"]
    if filters.get("days") not in (None, ""):
        days = filters["days"]
        if isinstance(days, bool) or not isinstance(days, int) or days not in V.WINDOWS:
            raise Refused(f"filters.days must be one of {list(V.WINDOWS)}")
        out["days"] = days
    if filters.get("play") not in (None, ""):
        if filters["play"] not in S.PRESET_PLAYS:
            raise Refused(f"filters.play must be one of {list(S.PRESET_PLAYS)}")
        out["play"] = filters["play"]
    if filters.get("era") not in (None, ""):
        if not isinstance(filters["era"], str) or not S.ID_PATTERN.match(filters["era"]):
            raise Refused("filters.era must be an era id or empty")
        if store is not None and store.era(filters["era"]) is None:
            raise Refused(f"no era with id {filters['era']}")
        out["era"] = filters["era"]
    return out


def _short(value, field, limit):
    if value is None:
        return None
    if not isinstance(value, str) or len(value) > limit:
        raise Refused(f"{field} must be text of at most {limit} characters")
    return value


def check_output(output, where):
    """A sql cell's result as the browser sends it back to be kept: columns, rows of plain values, no player column."""
    if output is None:
        return None
    if not isinstance(output, dict) or set(output) - OUTPUT_KEYS:
        raise Refused(f"{where}: output must be an object with only {sorted(OUTPUT_KEYS)}")
    out = {}
    if output.get("error") is not None:
        out["error"] = _short(output["error"], f"{where}.output.error", MAX_VALUE)
    columns = output.get("columns", [])
    if not isinstance(columns, list) or len(columns) > MAX_COLUMNS or \
            not all(isinstance(c, str) and 0 < len(c) <= 80 for c in columns):
        raise Refused(f"{where}: output.columns must be at most {MAX_COLUMNS} names")
    player = next((c for c in columns if PLAYER_COLUMN.search(c)), None)
    if player:
        raise Refused(f"{where}: its result has a player id column ({player}); a notebook keeps totals only")
    rows = output.get("rows", [])
    if not isinstance(rows, list) or len(rows) > V.MAX_ASK_ROWS:
        raise Refused(f"{where}: output.rows must be a list of at most {V.MAX_ASK_ROWS} rows")
    for row in rows:
        if not isinstance(row, list) or len(row) != len(columns):
            raise Refused(f"{where}: every row has one value per column")
        for v in row:
            if not (v is None or isinstance(v, (bool, int, float)) or (isinstance(v, str) and len(v) <= MAX_VALUE)):
                raise Refused(f"{where}: a value must be a number, null or text of at most {MAX_VALUE} characters")
    out.update({"columns": columns, "rows": rows})
    for k in ("truncated",):
        if k in output:
            if not isinstance(output[k], bool):
                raise Refused(f"{where}: output.{k} must be true or false")
            out[k] = output[k]
    for k in ("ms", "ran_ms"):
        if output.get(k) is not None:
            if isinstance(output[k], bool) or not isinstance(output[k], int) or output[k] < 0:
                raise Refused(f"{where}: output.{k} must be a whole number")
            out[k] = output[k]
    return out


def check_chart(chart, where):
    if chart is None:
        return None
    if not isinstance(chart, dict) or set(chart) - CHART_KEYS:
        raise Refused(f"{where}: chart must be an object with only {sorted(CHART_KEYS)}")
    out = {}
    source = chart.get("from", "series")
    if not isinstance(source, str) or not (source == "series" or CELL_ID.match(source)):
        raise Refused(f"{where}: chart.from is series or the id of a sql cell")
    out["from"] = source
    form = chart.get("form")
    if form is not None:
        if form not in FORMS:
            raise Refused(f"{where}: chart.form must be one of {list(FORMS)}")
        out["form"] = form
    for k in ("x", "y", "title"):
        v = _short(chart.get(k), f"{where}.chart.{k}", 80)
        if v:
            out[k] = v
    if source == "series":
        measure, by, over = chart.get("measure", "matches"), chart.get("by", "none"), chart.get("over", "day")
        try:
            V.check_series(measure, by, over)
        except V.BadRequest as exc:
            raise Refused(f"{where}: {exc}") from None
        out.update({"measure": measure, "by": by, "over": over})
    return out


def check_cells(cells):
    if not isinstance(cells, list) or len(cells) > MAX_CELLS:
        raise Refused(f"cells must be a list of at most {MAX_CELLS}")
    out, seen = [], set()
    for i, cell in enumerate(cells):
        where = f"cell {i + 1}"
        if not isinstance(cell, dict) or set(cell) - CELL_KEYS:
            raise Refused(f"{where} must be an object with only {sorted(CELL_KEYS)}")
        kind = cell.get("kind")
        if kind not in KINDS:
            raise Refused(f"{where}: kind must be one of {list(KINDS)}")
        cell_id = cell.get("id") or "c_" + secrets.token_hex(4)
        if not isinstance(cell_id, str) or not CELL_ID.match(cell_id) or cell_id in seen:
            raise Refused(f"{where}: id must be unique text matching [A-Za-z0-9_-]{{1,40}}")
        seen.add(cell_id)
        limit = {"text": MAX_TEXT, "sql": MAX_SQL, "ask": MAX_QUESTION, "chart": 200}[kind]
        source = _short(cell.get("source", ""), f"{where}.source", limit) or ""
        by = cell.get("by", "me")
        if by not in AUTHORS:
            raise Refused(f"{where}: by must be one of {list(AUTHORS)}")
        clean = {"id": cell_id, "kind": kind, "source": source, "by": by}
        if cell.get("at_ms") is not None:
            if isinstance(cell["at_ms"], bool) or not isinstance(cell["at_ms"], int):
                raise Refused(f"{where}: at_ms must be a time in ms")
            clean["at_ms"] = cell["at_ms"]
        if kind == "chart":
            clean["chart"] = check_chart(cell.get("chart") or {}, where)
        elif cell.get("chart") is not None:
            raise Refused(f"{where}: only a chart cell has a chart")
        if kind in ("sql", "ask"):
            output = check_output(cell.get("output"), where)
            if output is not None:
                clean["output"] = output
        elif cell.get("output") is not None:
            raise Refused(f"{where}: only sql and ask cells keep an output")
        out.append(clean)
    for cell in out:                                   # a chart of a cell names a sql cell of this notebook
        source = (cell.get("chart") or {}).get("from")
        if cell["kind"] == "chart" and source != "series" and \
                not any(c["id"] == source and c["kind"] == "sql" for c in out):
            raise Refused(f"cell {cell['id']}: chart.from names no sql cell of this notebook")
    return out


def _strings(value):
    if isinstance(value, str):
        yield value
    elif isinstance(value, dict):
        for v in value.values():
            yield from _strings(v)
    elif isinstance(value, list):
        for v in value:
            yield from _strings(v)


def known_players(store, texts, limit=5000):
    """The player ids events.db knows that appear in these texts, as whole values or as tokens in them."""
    tokens = set()
    for text in texts:
        if 6 <= len(text) <= 200:
            tokens.add(text)
        tokens.update(TOKEN.findall(text))
        if len(tokens) > limit:
            break
    found, tokens = set(), list(tokens)[:limit]
    for i in range(0, len(tokens), 400):
        chunk = tokens[i:i + 400]
        marks = ",".join("?" * len(chunk))
        with store.lock:
            rows = store.db.execute(f"SELECT distinct_id FROM people WHERE distinct_id IN ({marks}) UNION "
                                    f"SELECT DISTINCT distinct_id FROM events WHERE distinct_id IN ({marks})",
                                    chunk + chunk).fetchall()
        found.update(r[0] for r in rows)
    return found


def _refuse_players(store, cells):
    if known_players(store, _strings(cells)):
        raise Refused("a cell holds a player's id; a notebook keeps totals only (take the id out and save again)")


def save(store, body, owner, admin, moderator=False):
    """POST (make or duplicate) and PUT (change one's own). Returns the whole notebook."""
    ensure(store)
    if not isinstance(body, dict):
        raise Refused("send {name, visibility, filters, cells}")
    unknown = set(body) - {"id", "name", "visibility", "filters", "cells", "duplicate"}
    if unknown:
        raise Refused(f"unknown fields: {', '.join(sorted(unknown))}")
    now = S.now_ms()
    if body.get("duplicate") is not None:
        source = notebook(store, body["duplicate"], owner, admin)
        if source is None:
            raise Refused("no notebook with that id that this key may see")
        body = {"name": body.get("name") or ("Copy of " + source["name"])[:80], "visibility": "me",
                "filters": source["filters"], "cells": source["cells"]}
    nb_id = body.get("id")
    existing = None
    if nb_id is not None:
        if not isinstance(nb_id, str) or not NOTEBOOK_ID.match(nb_id):
            raise Refused("id must be a notebook id (nb_...)")
        with store.lock:
            existing = store.db.execute("SELECT id, name, owner, visibility, filters, cells, created_ms, updated_ms "
                                        "FROM notebooks WHERE id=?", (nb_id,)).fetchone()
        if existing is None or not S._visible(existing[3], existing[2], owner, admin):
            raise Refused("no notebook with that id; leave id out to make a new one")
        if existing[2] != owner:
            raise PermissionError("Only its owner can change a notebook; Duplicate makes your own copy.")
    old = _row(existing) if existing else {}
    name = body.get("name", old.get("name"))
    if not isinstance(name, str) or not name.strip() or len(name) > 80:
        raise Refused("name must be text of 1 to 80 characters")
    visibility = body.get("visibility", old.get("visibility", "me"))
    if visibility not in S.VISIBILITIES:
        raise Refused(f"visibility must be one of {list(S.VISIBILITIES)}")
    filters = check_filters(body["filters"], store) if "filters" in body else old.get("filters", {})
    cells = check_cells(body["cells"]) if "cells" in body else old.get("cells", [])
    stored = json.dumps(cells, separators=(",", ":"))
    if len(stored) > MAX_STORED:
        raise Refused(f"this notebook is too large to keep ({len(stored) // 1000} kB of at most {MAX_STORED // 1000}); "
                      "split it or trim its results")
    _refuse_players(store, [name, cells])
    with store.lock:
        if not existing and store.db.execute("SELECT COUNT(*) FROM notebooks").fetchone()[0] >= MAX_NOTEBOOKS:
            raise Refused(f"at most {MAX_NOTEBOOKS} notebooks; delete one first")
        nb_id = nb_id or "nb_" + secrets.token_hex(5)
        created = old.get("created_ms", now)
        store.db.execute("INSERT OR REPLACE INTO notebooks VALUES(?,?,?,?,?,?,?,?)",
                         (nb_id, name.strip(), owner, visibility, json.dumps(filters, sort_keys=True), stored,
                          created, now))
        store.db.commit()
    return notebook(store, nb_id, owner, admin)


def delete(store, nb_id, name, admin, moderator=False):
    """True when deleted; None when this key may not see it; PermissionError for someone else's shared one unless
    `moderator` (a key in admin_keys)."""
    ensure(store)
    found = notebook(store, nb_id, name, admin)
    if found is None:
        return None
    if found["owner"] != name and not moderator:
        raise PermissionError("Only its owner (or a key in admin_keys) can delete a notebook.")
    with store.lock:
        store.db.execute("DELETE FROM notebooks WHERE id=?", (found["id"],))
        store.db.commit()
    return True


# ---------------------------------------------------------------- running a cell

def run_sql(agg_path, sql):
    """A sql cell through the Query Studio's door (aggregates.db, read-only, SELECT only, 200 rows). Raises
    V.UnsafeQuery, or Refused when the result names a player."""
    out = V.run_readonly_sql(agg_path, sql)
    player = next((c for c in out["columns"] if PLAYER_COLUMN.search(c)), None)
    if player:
        raise Refused(f"the result has a player id column ({player}); a notebook keeps totals only")
    return {"columns": out["columns"], "rows": out["rows"], "truncated": out["truncated"], "ms": out["ms"],
            "ran_ms": S.now_ms()}


def _scope(store, filters):
    filters = check_filters(filters, store)
    era = store.era(filters["era"]) if filters.get("era") else None
    days = 365 if era else filters.get("days", 90)
    return filters.get("build", "release"), days, filters.get("play", "all"), era


def run_series(store, agg_path, chart, filters):
    build, days, play, era = _scope(store, filters)
    chart = check_chart(dict(chart or {}, **{"from": "series"}), "chart")
    return V.series(agg_path, chart["measure"], chart["by"], chart["over"], build, days, play, era, store.eras())


# ---------------------------------------------------------------- Claude's turn

def scrub(store, text):
    """The prompt as sent: any player id events.db knows, and any long id-shaped token, cut out."""
    for found in sorted(known_players(store, [text]), key=len, reverse=True):
        text = text.replace(found, "[player id removed]")
    return ID_SHAPED.sub("[id removed]", text)


def _rows_for_claude(out, room):
    keep = [i for i, c in enumerate(out["columns"]) if not ANY_ID_COLUMN.search(c)]
    rows = [[r[i] for i in keep] for r in out["rows"][:room]]
    return [out["columns"][i] for i in keep], rows


def describe_filters(store, filters):
    build, days, play, era = _scope(store, filters)
    span = (f"the era {era['name']} ({era['from']} to {era['to']}, source {era['source']})" if era
            else f"the last {days} days")
    plays = {"all": "every play type", "solo_vs_ai": "play_type = 'solo_vs_ai'", "solo": "play_type = 'solo_alone'",
             "multi": "play_type IN ('coop', 'adversarial', 'mixed', 'multi_human_unknown')"}[play]
    return f"build_kind = '{build}', {span}, {plays}"


def context(store, agg_path, cells, filters):
    """The earlier cells as Claude sees them: text, questions, queries and the totals they return when run HERE."""
    if not isinstance(cells, list):
        raise Refused("cells must be the notebook's cells, as a list")
    cells = cells[-CONTEXT_CELLS:]
    room, out, index = CONTEXT_ROWS, [], {}
    for n, cell in enumerate(cells, 1):
        if not isinstance(cell, dict):
            continue
        kind, source = cell.get("kind"), cell.get("source")
        source = source if isinstance(source, str) else ""
        index[cell.get("id")] = n
        if kind == "text":
            out.append({"cell": n, "kind": "note", "by": "claude" if cell.get("by") == "claude" else "person",
                        "text": source[:CONTEXT_TEXT]})
        elif kind == "ask":
            out.append({"cell": n, "kind": "question", "text": source[:MAX_QUESTION]})
        elif kind == "sql" and source.strip():
            item = {"cell": n, "kind": "query", "sql": source[:MAX_SQL]}
            try:
                result = run_sql(agg_path, source)
                columns, rows = _rows_for_claude(result, min(CONTEXT_ROWS_PER_CELL, room))
                room -= len(rows)
                item.update({"columns": columns, "rows": rows, "rows_returned": len(result["rows"]),
                             "rows_shown": len(rows)})
            except (V.UnsafeQuery, Refused) as exc:
                item["error"] = str(exc)
            out.append(item)
        elif kind == "chart":
            chart = cell.get("chart") if isinstance(cell.get("chart"), dict) else {}
            if chart.get("from", "series") == "series":
                try:
                    series = run_series(store, agg_path, chart, filters)
                except (S.Invalid, V.BadRequest) as exc:
                    out.append({"cell": n, "kind": "chart", "error": str(exc)})
                    continue
                points = [[p["x"], p["key"], p["value"]] for p in series["points"]][:min(CONTEXT_ROWS_PER_CELL, room)]
                room -= len(points)
                out.append({"cell": n, "kind": "chart", "measure": series["measure"], "by": series["by"],
                            "over": series["over"], "columns": ["x", "key", "value"], "rows": points,
                            "rows_shown": len(points), "points": len(series["points"])})
            else:
                out.append({"cell": n, "kind": "chart", "of_cell": index.get(chart.get("from")),
                            "form": chart.get("form"), "x": chart.get("x"), "y": chart.get("y")})
    return out


def _cell(kind, source, **extra):
    return dict({"id": "c_" + secrets.token_hex(4), "kind": kind, "source": source, "by": "claude",
                 "at_ms": S.now_ms()}, **extra)


def ask(store, agg_path, question, cells, filters, model=C.DEFAULT_MODEL, effort="medium", sent=None):
    """One turn of the chat. Returns {cells: [new cells]} or {error}. `sent` (a list) collects every prompt, for
    the tests that check what Claude was shown."""
    question = (question or "").strip() if isinstance(question, str) else ""
    if not question:
        return {"error": "Ask a question first."}
    if len(question) > MAX_QUESTION:
        return {"error": f"Keep the question under {MAX_QUESTION} characters."}
    filters_text = describe_filters(store, filters)
    client, why = C._client()
    if client is None:
        return {"error": "Claude is off on this server: " + why + "."}
    earlier = context(store, agg_path, cells, filters)
    system = NOTEBOOK_SYSTEM.format(schema=S.AGG_SCHEMA.strip(), today=S.day_of(S.now_ms()), filters=filters_text)
    user = scrub(store, json.dumps({"question": question, "earlier_cells": earlier}, default=str))

    def call(sys_text, user_text, schema, tokens):
        if sent is not None:
            sent.append(sys_text + "\n" + user_text)
        return C._structured(client, model, effort, sys_text, user_text, schema, max_tokens=tokens)

    reply, why = call(system, user, REPLY_SCHEMA, 4000)
    if reply is None:
        return {"error": why}
    sql = (reply.get("sql") or "").strip()
    result, error = None, None
    if sql:
        try:
            result = run_sql(agg_path, sql)
        except (V.UnsafeQuery, Refused) as exc:
            error = str(exc)
        if error:                                    # one retry, with the reason
            again, why = call(system, user + "\n\n" + scrub(store, RETRY.format(error=error)), REPLY_SCHEMA, 4000)
            if again is not None:
                reply, sql, error = again, (again.get("sql") or "").strip(), None
                if sql:
                    try:
                        result = run_sql(agg_path, sql)
                    except (V.UnsafeQuery, Refused) as exc:
                        error = str(exc)
    text = (reply.get("text") or "").strip()
    if result is not None:
        columns, rows = _rows_for_claude(result, CONTEXT_ROWS)
        answer_in = scrub(store, json.dumps({"question": question, "sql": sql, "columns": columns, "rows": rows,
                                             "truncated": result["truncated"]}, default=str))
        answer, _ = call(C.ANSWER_SYSTEM, answer_in, C.ANSWER_SCHEMA, 2000)
        if answer:
            text = answer["answer"].strip() + (("\n\n" + answer["caveat"].strip()) if answer.get("caveat") else "")
    new = [_cell("text", text[:MAX_TEXT] or "Claude wrote no answer.")]
    if sql:
        sql_cell = _cell("sql", sql[:MAX_SQL], output=result if result is not None else {"error": f"Not run: {error}"})
        new.append(sql_cell)
        chart = reply.get("chart") or {}
        if result is not None and chart.get("form") in FORMS and result["rows"]:
            spec = {"from": sql_cell["id"], "form": chart["form"]}
            for k in ("x", "y"):
                if chart.get(k) in result["columns"]:
                    spec[k] = chart[k]
            new.append(_cell("chart", "", chart=spec))
    return {"cells": new, "sql": sql or None, "ok": error is None}


# ---------------------------------------------------------------- routes (h: analytics_server's request handler)

def _moderator(h):
    supplied = h._bearer()
    return any(supplied and key and hmac.compare_digest(supplied.encode(), key.encode())
               for key in h.app_ref.config["admin_keys"].values())


def _who(h):
    return h._reader(), bool(h._admin())


def _get(h):
    store = h.app_ref.store
    name, admin = _who(h)
    nb_id = h._params().get("id")
    if nb_id:
        found = notebook(store, nb_id, name, admin)
        return (200, found) if found else (404, {"error": "No notebook with that id that this key may see."})
    return 200, {"notebooks": notebooks(store, name, admin)}


def _post(h):
    name, admin = _who(h)
    return 200, save(h.app_ref.store, h._body(), name, admin)


def _put(h):
    body = h._body()
    if not isinstance(body, dict) or not body.get("id"):
        raise Refused("send {id, name?, visibility?, filters?, cells?}")
    name, admin = _who(h)
    return 200, save(h.app_ref.store, body, name, admin)


def _delete(h):
    name, admin = _who(h)
    nb_id = h._params().get("id") or ""
    if delete(h.app_ref.store, nb_id, name, admin, _moderator(h)) is None:
        return 404, {"error": "No notebook with that id that this key may see."}
    return 200, {"deleted": nb_id}


def _run(h):
    app, body = h.app_ref, h._body()
    if not isinstance(body, dict) or not isinstance(body.get("cell"), dict):
        raise Refused("send {cell: {kind, source, chart?}, filters?}")
    cell = body["cell"]
    if cell.get("kind") == "sql":
        if not h._admin():
            return 403, {"error": "Running a query needs an admin key, as in the Query Studio."}
        sql = cell.get("source")
        if not isinstance(sql, str) or not sql.strip() or len(sql) > MAX_SQL:
            raise Refused(f"source must be one SELECT of at most {MAX_SQL:,} characters")
        return 200, {"output": run_sql(app.agg, sql)}
    if cell.get("kind") == "chart":
        chart = cell.get("chart") or {}
        if chart.get("from", "series") != "series":
            raise Refused("a chart of a sql cell is drawn from that cell's result; run the sql cell")
        return 200, {"output": run_series(app.store, app.agg, chart, body.get("filters"))}
    raise Refused("only sql and chart cells run (an ask cell goes to /v1/notebooks/ask)")


def _ask(h):
    app, body = h.app_ref, h._body()
    reader = h._reader()
    if not isinstance(body, dict):
        raise Refused("send {question, cells, filters}")
    limit = app.config["ask_daily_limit"]
    used = app.store.asks_today(reader)
    if used >= limit:
        return 429, {"error": "Today's questions are used up; the limit resets at 00:00 UTC."}
    result = ask(app.store, app.agg, body.get("question"), body.get("cells") or [], body.get("filters"),
                 app.config["claude_model"], app.config["claude_effort"])
    if isinstance(body.get("question"), str) and body["question"].strip():   # an empty question costs nothing
        app.store.log_ask(reader, ("[notebook] " + body["question"].strip())[:500], result.get("sql"),
                          "error" not in result and result.get("ok", True))
        used += 1
    result["asks_left"] = max(0, limit - used)
    return 200, result


GET = {"/v1/notebooks": _get}
POST = {"/v1/notebooks": _post, "/v1/notebooks/run": _run, "/v1/notebooks/ask": _ask}
PUT = {"/v1/notebooks": _put}
DELETE = {"/v1/notebooks": _delete}
