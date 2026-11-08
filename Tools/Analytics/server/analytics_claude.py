"""Claude's two jobs on the analytics server: the overnight read (three notes a night) and the Ask box.

Claude only ever sees aggregates: digest_facts() and the aggregates.db schema. It never receives a player id, a
display name or a raw event, because it is never given a door to them (analytics_views.run_readonly_sql opens
aggregates.db alone, read-only, SELECT-only).

Needs the official SDK on the server (`pip install anthropic`) and ANTHROPIC_API_KEY in the service's environment.
Without either, the read is written by rules (`rule_notes`) and the Ask box says it is off; nothing else changes.
"""
import json

import analytics_store as S
import analytics_views as V

DEFAULT_MODEL = "claude-opus-5-5"

NOTES_SCHEMA = {
    "type": "object",
    "properties": {
        "notes": {
            "type": "array",
            "items": {
                "type": "object",
                "properties": {
                    "title": {"type": "string"},
                    "body": {"type": "string"},
                    "severity": {"type": "string", "enum": ["problem", "info"]},
                },
                "required": ["title", "body", "severity"],
                "additionalProperties": False,
            },
        }
    },
    "required": ["notes"],
    "additionalProperties": False,
}

SQL_SCHEMA = {
    "type": "object",
    "properties": {
        "sql": {"type": "string"},
        "can_answer": {"type": "boolean"},
        "why_not": {"type": "string"},
    },
    "required": ["sql", "can_answer", "why_not"],
    "additionalProperties": False,
}

ANSWER_SCHEMA = {
    "type": "object",
    "properties": {"answer": {"type": "string"}, "caveat": {"type": "string"}},
    "required": ["answer", "caveat"],
    "additionalProperties": False,
}

DIGEST_SYSTEM = """You write the overnight read for Froglet's game analytics dashboard. The reader is the team's \
analyst, who opens the dashboard once a day to answer one question: which kinds of play (solo, solo against AI, \
co-op, versus) bring players back.

You get one JSON object of aggregates per build: release (real players), editor (the team playtesting in the Unity \
editor), port (a Unity build stamped -port), development. Write at most three notes about what changed or what is \
wrong, most important first. A note is a short title (under 60 characters) and one or two plain sentences with the \
numbers that support it. Severity "problem" only for something broken (rows_per_play above 1.05 means events are \
sent twice; a build that starts matches but rarely finishes them; rejects that keep coming), otherwise "info".

Rules: quote only numbers that are in the data. Say when a number rests on too few matches to mean anything (under \
about 20). Release builds are what players do; say so when a finding comes only from the editor. No advice about \
marketing. No greetings, no sign-off."""

SQL_SYSTEM = """You turn a question about Froglet's game analytics into one SQLite SELECT query over the tables \
below, and nothing else. The tables hold aggregates only; there are no player ids.

{schema}

Notes on the data: build_kind is release (real players), editor (team playtests in the Unity editor), port, or \
development; answer for release unless the question names another. play_type multi_human_unknown means a \
multi-human match recorded before the team fields existed. rematched is NULL for plays that were not completed. \
Days are 'YYYY-MM-DD' strings in UTC; today is {today}.

Return can_answer false, an empty sql and a one-sentence why_not when these tables cannot answer the question. \
Keep the result small: aggregate, and LIMIT 50 when listing."""

ANSWER_SYSTEM = """You answer a question about Froglet's game analytics from the rows a query returned. Two or \
three plain sentences that lead with the answer and quote the numbers. In caveat, one sentence on what the reader \
should keep in mind (too few matches, editor data, a play type that cannot be split yet), or an empty string."""


def _client():
    try:
        import anthropic
    except ImportError:
        return None, "the anthropic package is not installed on the server (pip install anthropic)"
    try:
        client = anthropic.Anthropic()
    except Exception as exc:  # no credentials configured
        return None, f"no Claude credentials on the server ({type(exc).__name__})"
    # Newer SDKs build a client with no key and fail only when a request is sent: treat that as off, here.
    if not (getattr(client, "api_key", None) or getattr(client, "auth_token", None)):
        return None, "no Claude credentials on the server (ANTHROPIC_API_KEY is not set)"
    return client, None


def _structured(client, model, effort, system, user, schema, max_tokens=4000):
    """One structured-output call. Returns (parsed dict, None) or (None, reason). Refusals and API errors become a
    reason the page can show, never an exception that takes the server down."""
    import anthropic
    try:
        response = client.beta.messages.create(
            model=model,
            max_tokens=max_tokens,
            system=system,
            messages=[{"role": "user", "content": user}],
            output_config={"effort": effort, "format": {"type": "json_schema", "schema": schema}},
            betas=["server-side-fallback-2026-07-01"],
            fallbacks="default",
        )
    except anthropic.RateLimitError:
        return None, "Claude is rate limited right now; try again in a minute"
    except anthropic.APIStatusError as exc:
        return None, f"Claude returned an error ({exc.status_code})"
    except anthropic.APIConnectionError:
        return None, "the server could not reach Claude"
    except Exception as exc:  # anything else (credentials, the SDK) is a reason too, never a crash
        return None, f"Claude could not be asked ({type(exc).__name__})"
    if response.stop_reason == "refusal":
        return None, "Claude declined this question"
    if response.stop_reason == "max_tokens":
        return None, "Claude's answer was cut off"
    text = next((b.text for b in response.content if b.type == "text"), "")
    try:
        return json.loads(text), None
    except json.JSONDecodeError:
        return None, "Claude's answer was not valid JSON"


# ---------------------------------------------------------------- the overnight read

def rule_notes(facts):
    """The read without Claude: the health checks a person would make first."""
    if facts.get("empty"):
        return [{"title": "No data yet", "body": "Nothing has been received. Point a build at this server.",
                 "severity": "info"}]
    notes = []
    release = facts.get("release", {}).get("last_7_days", {})
    rpp = release.get("rows_per_play")
    if rpp and rpp > 1.05:
        notes.append({"title": "Events arrive more than once",
                      "body": f"game_started read {rpp} rows per play in the last 7 days. 1.0 is healthy.",
                      "severity": "problem"})
    for row in facts.get("match_health_90d", []):
        if row["starts"] >= 10 and (row["finished_pct"] or 0) < 20:
            notes.append({"title": f"The {row['build']} build rarely finishes a match",
                          "body": f"{row['finished_pct']}% of {row['starts']} plays in 90 days sent game_completed.",
                          "severity": "problem"})
    if release:
        notes.append({"title": "Last 7 days, release builds",
                      "body": f"{release.get('players', 0)} players, {release.get('matches', 0)} matches, "
                              f"{release.get('finished_pct')}% finished.", "severity": "info"})
    return notes[:3]


def write_digest(store, agg_path, model=DEFAULT_MODEL, effort="medium"):
    facts = V.digest_facts(agg_path)
    client, why = _client() if not facts.get("empty") else (None, "no data")
    if client is None:
        notes, author = rule_notes(facts), "rules"
    else:
        user = "Aggregates (JSON):\n" + json.dumps(facts, separators=(",", ":"))
        parsed, why = _structured(client, model, effort, DIGEST_SYSTEM, user, NOTES_SCHEMA)
        notes, author = (parsed["notes"][:3], "claude") if parsed else (rule_notes(facts), "rules")
    store.save_digest(S.day_of(S.now_ms()), author, notes)
    return {"author": author, "notes": notes, "why_rules": why if author == "rules" else None}


# ---------------------------------------------------------------- the Ask box

def ask(agg_path, question, model=DEFAULT_MODEL, effort="medium"):
    """Question -> Claude writes SQL over the aggregates -> the server runs it read-only -> Claude answers from the
    rows. Returns {answer, caveat, sql, columns, rows} or {error}."""
    question = (question or "").strip()
    if not question:
        return {"error": "Ask a question first."}
    if len(question) > 500:
        return {"error": "Keep the question under 500 characters."}
    client, why = _client()
    if client is None:
        return {"error": "The Ask box is off: " + why + "."}
    system = SQL_SYSTEM.format(schema=S.AGG_SCHEMA.strip(), today=S.day_of(S.now_ms()))
    plan, why = _structured(client, model, effort, system, question, SQL_SCHEMA, max_tokens=4000)
    if plan is None:
        return {"error": why}
    if not plan["can_answer"]:
        return {"error": plan["why_not"] or "These tables cannot answer that.", "sql": ""}
    try:
        result = V.run_readonly_sql(agg_path, plan["sql"])
    except V.UnsafeQuery as exc:
        return {"error": f"The query was not run: {exc}.", "sql": plan["sql"]}
    user = json.dumps({"question": question, "sql": plan["sql"], "columns": result["columns"],
                       "rows": result["rows"], "truncated": result["truncated"]}, default=str)
    answer, why = _structured(client, model, effort, ANSWER_SYSTEM, user, ANSWER_SCHEMA, max_tokens=2000)
    if answer is None:
        return {"error": why, "sql": plan["sql"], **result}
    return {**answer, "sql": plan["sql"], **result}
