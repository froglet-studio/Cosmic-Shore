#!/usr/bin/env python3
"""Smoke report: turn the Unity per-mode smoke test's summary into launch-status records `smoke-<mode>`.

The smoke test (`CosmicShore.Editor.ModeSmokeLauncher`, Docs/LAUNCH/SMOKE_TEST.md) writes `summary.json`: one
entry per planned mode with the FACTS it saw (scene loaded, seats, AI seats, seats that moved, the game-over event,
errors, exceptions). This tool judges each mode again from those facts, independently of the status the editor
wrote, so a summary that says "passed" next to an exception still publishes a failure. Then it builds one record
per mode for `Tools/Launch/launch_status.py` (platform `quality`, kind `gate`), validated against the contract.

Error text from the game log is never put in a record: a record carries counts and the first failed check only.

Subcommands:
    records SUMMARY            print the records as JSON (validated; exit 1 if any record is invalid)
    check SUMMARY              print a table; exit 1 if any mode failed or never ran (the CI gate)
    publish SUMMARY [--dry-run] publish every record to the launch-status branch (dry run: print, no push)
    self-test                  prove the verdict, including the negative control (an exception -> a failure)

Stdlib only. Exit 2 = the summary is missing or unreadable.
"""
from __future__ import annotations

import argparse
import copy
import json
import os
import re
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
import launch_status  # noqa: E402  (the contract's validator and publisher, same folder)

SOURCE = "Mode smoke"
PASSED, FAILED, SKIPPED, NOT_RUN = "passed", "failed", "skipped", "not_run"


def component_id(mode: str) -> str:
    """'SkimRace' -> 'smoke-skim-race'; the same rule as ModeSmokeResult.ComponentId in the editor."""
    out = []
    for i, c in enumerate(mode):
        if c.isupper() and i > 0 and (mode[i - 1].islower() or mode[i - 1].isdigit()):
            out.append("-")
        out.append(c.lower() if c.isalnum() else "-")
    return "smoke-" + "".join(out)


def _int(m: dict, key: str) -> int:
    v = m.get(key, 0)
    return v if isinstance(v, int) and not isinstance(v, bool) else 0


def _bool(m: dict, key: str, default: bool = False) -> bool:
    v = m.get(key, default)
    return v if isinstance(v, bool) else default


def verdict(m: dict) -> tuple[str, str]:
    """(passed | failed | skipped, the first failed check in words). Recomputed from the facts; the editor's
    `status` is only trusted for `skipped` (with a reason) and `not_run`."""
    status = m.get("status")
    if status == SKIPPED:
        reason = str(m.get("skipReason") or "").strip()
        return (SKIPPED, "") if reason else (FAILED, "skipped without a reason")
    if status == NOT_RUN:
        return FAILED, "never ran (the run stopped before this mode)"
    if status not in (PASSED, FAILED):
        return FAILED, f"unknown status {status!r}"
    required = ("reachedMenu", "sceneLoaded", "turnStarted", "seats", "aiSeats", "movingSeats", "gameEnded",
                "errors", "exceptions")
    missing = [k for k in required if k not in m]
    if missing:
        return FAILED, "the result is incomplete (missing " + ", ".join(missing) + ")"
    seats, ai, moving = _int(m, "seats"), _int(m, "aiSeats"), _int(m, "movingSeats")
    errors, exceptions = _int(m, "errors"), _int(m, "exceptions")
    if not _bool(m, "inBuild", True):
        return FAILED, f"scene {m.get('scene', '?')} is not an enabled build scene"
    if str(m.get("harnessFault") or ""):
        return FAILED, "the run stopped early (see the mode's notes)"
    if not _bool(m, "reachedMenu"):
        return FAILED, "the boot never reached the menu"
    if not _bool(m, "sceneLoaded"):
        return FAILED, "the scene never loaded"
    if seats <= 0:
        return FAILED, "no seat had a vessel"
    if not _bool(m, "turnStarted"):
        return FAILED, "the first turn never started"
    if ai < seats:
        return FAILED, f"{seats - ai} of {seats} seats not flown by the AI"
    if moving < 1:
        return FAILED, "no AI vessel moved"
    if not _bool(m, "gameEnded"):
        return FAILED, "the game-over event never fired within the budget"
    if exceptions > 0:
        return FAILED, f"{exceptions} exception(s) logged"
    if errors > 0:
        return FAILED, f"{errors} error(s) logged"
    return PASSED, ""


_SAFE = re.compile(r"[^A-Za-z0-9 ._()/:;,'+-]")


def _safe(text: str, limit: int) -> str:
    """One short line of known-safe characters (mode, scene and vessel names, a check in words)."""
    return _SAFE.sub("", str(text)).strip()[:limit]


def record_for(m: dict, summary: dict) -> dict:
    result, why = verdict(m)
    mode = _safe(m.get("mode") or "unknown", 40) or "unknown"
    gs = m.get("gameSeconds")
    game_s = f"{gs:.0f}" if isinstance(gs, (int, float)) else "?"
    if result == PASSED:
        line = (f"{mode}: game over after {game_s} game s, {_int(m, 'aiSeats')}/{_int(m, 'seats')} seats AI, "
                f"{_int(m, 'movingSeats')} moving, 0 errors")
    elif result == SKIPPED:
        line = f"{mode}: not smoked - {_safe(m.get('skipReason', ''), 200)}"
    else:
        line = f"{mode}: {_safe(why, 200)}"
    record = {
        "schema": launch_status.SCHEMA,
        "id": component_id(mode),
        "platform": "quality",
        "kind": "gate",
        "result": {PASSED: "success", FAILED: "failure", SKIPPED: "skipped"}[result],
        "updated_at": launch_status.now_iso(),
        "source": os.environ.get("GITHUB_WORKFLOW") or SOURCE,
        "summary": line[:launch_status.MAX_TEXT],
    }
    commit = str(summary.get("commit") or os.environ.get("GITHUB_SHA") or "")
    if launch_status.SHA_RE.fullmatch(commit):
        record["commit"] = commit
    if os.environ.get("GITHUB_RUN_ID") and os.environ.get("GITHUB_REPOSITORY"):
        server = os.environ.get("GITHUB_SERVER_URL", "https://github.com")
        record["run_url"] = f"{server}/{os.environ['GITHUB_REPOSITORY']}/actions/runs/{os.environ['GITHUB_RUN_ID']}"
    details = {
        "scene": _safe(m.get("scene", ""), 60),
        "vessel": _safe(m.get("vessel", ""), 40),
        "intensity": _int(m, "intensity"),
        "seats": _int(m, "seats"),
        "ai_seats": _int(m, "aiSeats"),
        "moving_seats": _int(m, "movingSeats"),
        "game_ended": _bool(m, "gameEnded"),
        "errors": _int(m, "errors"),
        "exceptions": _int(m, "exceptions"),
        "unity": _safe(summary.get("unityVersion", ""), 30),
        "time_scale": summary.get("timeScale", 0) if isinstance(summary.get("timeScale"), (int, float)) else 0,
        "end_conditions_match_build": _bool(summary, "endConditionsMatchBuild", True),
    }
    for key in ("gameSeconds", "wallSeconds"):
        v = m.get(key)
        if isinstance(v, (int, float)) and not isinstance(v, bool):
            details["game_seconds" if key == "gameSeconds" else "wall_seconds"] = round(float(v), 1)
    record["details"] = {k: v for k, v in details.items() if v != ""}
    return record


def records(summary: dict) -> list[dict]:
    modes = summary.get("modes")
    if not isinstance(modes, list) or not modes:
        raise ValueError("the summary lists no modes")
    out, seen = [], set()
    for m in modes:
        if not isinstance(m, dict):
            raise ValueError("a mode entry is not an object")
        r = record_for(m, summary)
        if r["id"] in seen:
            raise ValueError(f"two entries for {r['id']}")
        seen.add(r["id"])
        out.append(r)
    return out


def _unreadable(message: str) -> SystemExit:
    print(message, file=sys.stderr)
    return SystemExit(2)


def load(path: str) -> dict:
    p = Path(path)
    if p.is_dir():
        p = p / "summary.json"
    try:
        data = json.loads(p.read_text())
    except FileNotFoundError:
        raise _unreadable(f"no smoke summary at {p} (did the Unity run start?)") from None
    except json.JSONDecodeError as e:
        raise _unreadable(f"{p} is not JSON: {e}") from None
    if not isinstance(data, dict):
        raise _unreadable(f"{p} is not a summary object")
    return data


def table(recs: list[dict]) -> str:
    return "\n".join(f"{r['result']:8} {r['id']:34} {r.get('summary', '')}" for r in recs)


def self_test() -> int:
    good_mode = {
        "mode": "SkimRace", "modeId": 33, "scene": "MinigameSkimRace", "vessel": "Squirrel", "intensity": 1,
        "players": 3, "status": PASSED, "inBuild": True, "reachedMenu": True, "sceneLoaded": True, "turnStarted": True,
        "seats": 3, "aiSeats": 3, "movingSeats": 3, "gameEnded": True, "errors": 0, "exceptions": 0,
        "harnessFault": "", "gameSeconds": 84.2, "wallSeconds": 61.0,
        "firstErrors": [],
    }
    skipped = {"mode": "Maelstrom", "modeId": 36, "status": SKIPPED, "skipReason": "chains the other modes"}
    summary = {"schema": 1, "commit": "1ac6f87ae0", "unityVersion": "6000.3.17f1", "timeScale": 4,
               "endConditionsMatchBuild": True, "modes": [good_mode, skipped]}

    problems: list[str] = []
    recs = records(summary)
    for r in recs:
        errs = launch_status.validate(r)
        if errs:
            problems.append(f"{r['id']} invalid: {errs}")
    by_id = {r["id"]: r for r in recs}
    if by_id.get("smoke-skim-race", {}).get("result") != "success":
        problems.append("a clean mode did not publish success")
    if by_id.get("smoke-maelstrom", {}).get("result") != "skipped":
        problems.append("an excluded mode did not publish skipped")

    # The negative controls: each spoils one fact of the clean mode; every one must publish a failure, even when
    # the editor's own status still says "passed".
    spoilers = {
        "an exception (the required negative control)": {"exceptions": 1},
        "an error": {"errors": 3},
        "the game never ended": {"gameEnded": False},
        "no AI vessel moved": {"movingSeats": 0},
        "a seat a human flew": {"aiSeats": 2},
        "the scene never loaded": {"sceneLoaded": False},
        "a harness fault": {"harnessFault": "Play mode did not start"},
        "not in the build": {"inBuild": False},
        "a mode that never ran": {"status": NOT_RUN},
        "a skip with no reason": {"status": SKIPPED, "skipReason": ""},
        "an incomplete result": {"__drop__": "gameEnded"},
        "an unknown status": {"status": "green"},
    }
    for name, change in spoilers.items():
        m = copy.deepcopy(good_mode)
        if "__drop__" in change:
            m.pop(change["__drop__"])
        else:
            m.update(change)
        rec = record_for(m, summary)
        if rec["result"] != "failure":
            problems.append(f"{name}: published {rec['result']}, must be failure")
        if launch_status.validate(rec):
            problems.append(f"{name}: the failure record is itself invalid: {launch_status.validate(rec)}")

    # Error text never reaches a record, even when it carries a token.
    leaky = copy.deepcopy(good_mode)
    leaky.update(exceptions=1, firstErrors=["[match] token ghp_" + "a" * 36], harnessFault="")
    rec = record_for(leaky, summary)
    if "ghp_" in json.dumps(rec):
        problems.append("error text leaked into a record")

    # A summary with no modes, or two entries for one mode, is refused rather than published as nothing.
    for name, bad in {"no modes": {"modes": []}, "duplicate": {"modes": [good_mode, good_mode]}}.items():
        try:
            records(bad)
            problems.append(f"{name}: accepted")
        except ValueError:
            pass

    if component_id("WildlifeLiberation") != "smoke-wildlife-liberation":
        problems.append("component ids are not kebab-case")

    if problems:
        print("SELF-TEST FAILED:\n  " + "\n  ".join(problems))
        return 1
    print(f"self-test OK: a clean mode -> success, an excluded mode -> skipped, {len(spoilers)} spoiled results -> "
          "failure (incl. the exception negative control), no error text in a record")
    return 0


def main(argv: list[str] | None = None) -> int:
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    sub = ap.add_subparsers(dest="cmd", required=True)
    for name in ("records", "check"):
        p = sub.add_parser(name)
        p.add_argument("summary", help="summary.json, or the directory holding it")
    p = sub.add_parser("publish")
    p.add_argument("summary")
    p.add_argument("--dry-run", action="store_true", help="validate and print every record; push nothing")
    p.add_argument("--remote", default="origin")
    p.add_argument("--repo", default=".")
    sub.add_parser("self-test")
    args = ap.parse_args(argv)

    if args.cmd == "self-test":
        return self_test()

    summary = load(args.summary)
    try:
        recs = records(summary)
    except ValueError as e:
        print(f"refusing the summary: {e}", file=sys.stderr)
        return 1
    invalid = {r["id"]: launch_status.validate(r) for r in recs}
    invalid = {k: v for k, v in invalid.items() if v}
    if invalid:
        for k, v in invalid.items():
            print(f"{k}: " + "; ".join(v), file=sys.stderr)
        return 1

    if args.cmd == "records":
        print(json.dumps(recs, indent=2, sort_keys=True))
        return 0
    if args.cmd == "check":
        print(table(recs))
        bad = [r for r in recs if r["result"] == "failure"]
        print(f"\n{len(recs) - len(bad)} of {len(recs)} modes did not fail ({sum(r['result'] == 'skipped' for r in recs)} skipped)")
        return 1 if bad else 0
    # publish
    if args.dry_run:
        print(json.dumps(recs, indent=2, sort_keys=True))
        print(f"dry run: {len(recs)} record(s) valid, nothing pushed")
        return 0
    for r in recs:
        launch_status.publish(r, Path(args.repo), args.remote)
    return 0


if __name__ == "__main__":
    sys.exit(main())
