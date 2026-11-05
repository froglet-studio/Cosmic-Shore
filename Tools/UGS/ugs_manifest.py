#!/usr/bin/env python3
"""What a UGS deploy would upload, and what changed since the last deploy to that environment.

    python3 Tools/UGS/ugs_manifest.py manifest [--root .]            # the files deploy.sh uploads, with hashes (JSON)
    python3 Tools/UGS/ugs_manifest.py plan --env development \\
        --history last.jsonl [--ref bleeding-edge] [--sha <commit>] \\
        [--summary-md plan.md] [--github-output "$GITHUB_OUTPUT"]     # the diff against the last deploy
    python3 Tools/UGS/ugs_manifest.py self-test                      # proves it, with its negative controls

The list of files is deploy.sh's own `SCRIPTS=( ... )`, read from the tree being deployed, so there is one list and a
rollback to an older commit deploys that commit's list. Each file is hashed (sha256, first 12 hex characters: enough to
tell versions apart, short enough that the launch-status secret filter never mistakes it for a token).

The baseline is the last SUCCESSFUL `ugs-<env>` record on the `launch-status` branch (history/ugs-<env>.jsonl): every
deploy from `.github/workflows/ugs-deploy.yml` publishes the hashes it uploaded as `h_<script>` details, so "what
changed" means "what changed since the last deploy this pipeline recorded". A deploy made by hand from a terminal is not
recorded; the deploy jobs therefore also print the CLI's own `ugs deploy --dry-run` against the live environment.

Stdlib only. Never reads, prints or needs a secret.
"""
from __future__ import annotations

import argparse
import hashlib
import json
import os
import re
import sys
import tempfile
from pathlib import Path

ENVS = ("development", "production")
SCRIPTS_RE = re.compile(r"^SCRIPTS=\(([^)]*)\)", re.M)
HASH_LEN = 12


def deploy_list(root: Path) -> list[str]:
    """The files deploy.sh uploads, in its order, read from `<root>/Tools/UGS/deploy.sh`."""
    deploy = root / "Tools" / "UGS" / "deploy.sh"
    if not deploy.is_file():
        raise SystemExit(f"no Tools/UGS/deploy.sh under {root}")
    m = SCRIPTS_RE.search(deploy.read_text(encoding="utf-8"))
    if not m:
        raise SystemExit("Tools/UGS/deploy.sh has no SCRIPTS=( ... ) list; the manifest cannot know what is deployed")
    files = m.group(1).split()
    if not files:
        raise SystemExit("Tools/UGS/deploy.sh's SCRIPTS list is empty")
    return files


def detail_key(path: str) -> str:
    """`Tools/UGS/CloudCode/match-report.js` -> `h_match_report` (the record's lower_snake_case detail key)."""
    stem = Path(path).name
    stem = stem[:-3] if stem.endswith(".js") else stem
    key = "h_" + re.sub(r"[^a-z0-9]+", "_", stem.lower()).strip("_")
    if not re.match(r"^[a-z0-9_]{1,40}$", key):
        raise SystemExit(f"{path}: cannot make a detail key from this name")
    return key


def manifest(root: Path) -> dict:
    scripts = []
    keys = set()
    for rel in deploy_list(root):
        f = root / rel
        if not f.is_file():
            raise SystemExit(f"deploy.sh lists {rel}, which does not exist")
        key = detail_key(rel)
        if key in keys:
            raise SystemExit(f"two deployed files share the name {key[2:]}; UGS names a script by its file name")
        keys.add(key)
        test = f.with_name(f.name[:-3] + ".test.cjs") if f.name.endswith(".js") else None
        scripts.append({
            "path": rel,
            "key": key,
            "sha": hashlib.sha256(f.read_bytes()).hexdigest()[:HASH_LEN],
            "bytes": f.stat().st_size,
            "has_test": bool(test and test.is_file()),
        })
    policies = sorted(p.relative_to(root).as_posix() for p in (root / "Tools" / "UGS" / "AccessControl").glob("*.json"))
    body = json.dumps(scripts, sort_keys=True).encode()
    return {"scripts": scripts, "policies_in_repo": policies, "digest": hashlib.sha256(body).hexdigest()[:HASH_LEN]}


def last_success(history: Path | None) -> dict | None:
    """The newest successful record with hashes in a history/ugs-<env>.jsonl, or None."""
    if not history or not history.is_file():
        return None
    best = None
    for line in history.read_text(encoding="utf-8").splitlines():
        line = line.strip()
        if not line:
            continue
        try:
            rec = json.loads(line)
        except json.JSONDecodeError:
            continue
        details = rec.get("details") or {}
        if rec.get("result") == "success" and any(k.startswith("h_") for k in details):
            best = rec
    return best


def last_record(history: Path | None) -> dict | None:
    if not history or not history.is_file():
        return None
    lines = [l for l in history.read_text(encoding="utf-8").splitlines() if l.strip()]
    try:
        return json.loads(lines[-1]) if lines else None
    except json.JSONDecodeError:
        return None


def plan(man: dict, env: str, history: Path | None, ref: str, sha: str) -> dict:
    base = last_success(history)
    latest = last_record(history)
    old = (base or {}).get("details") or {}
    rows = []
    changed = []
    for s in man["scripts"]:
        prev = old.get(s["key"])
        if prev is None:
            state = "new" if base else "no deploy recorded"
        elif prev == s["sha"]:
            state = "unchanged"
        else:
            state = "changed"
        if state != "unchanged":
            changed.append(Path(s["path"]).name[:-3] if s["path"].endswith(".js") else Path(s["path"]).name)
        rows.append((s["path"], s["sha"], prev or "-", state))
    listed = {s["key"] for s in man["scripts"]}
    dropped = sorted(k[2:] for k in old if k.startswith("h_") and k not in listed)

    short = (sha or "")[:12]
    n = len(man["scripts"])
    if base:
        what = f"{len(changed)} of {n} scripts changed" + (f" ({', '.join(changed)})" if changed else "")
    else:
        what = f"{n} scripts, no earlier deploy recorded for {env}"
    summary = f"Cloud Code {short or ref} to {env}: {what}"[:280]

    md = [f"### UGS plan: {env}", ""]
    md.append(f"Deploying `{short}` (ref `{ref}`).")
    if base:
        md.append(f"Last successful deploy to {env}: `{(base.get('commit') or '?')[:12]}` at {base.get('updated_at', '?')}"
                  + (f" ([run]({base['run_url']}))" if base.get("run_url") else "") + ".")
    else:
        md.append(f"No successful deploy to {env} is recorded on the launch-status branch: every script counts as new.")
    if latest and base is not latest and latest.get("result") != "success":
        md.append(f"**The newest {env} record is a {latest.get('result')}** at `{(latest.get('commit') or '?')[:12]}` "
                  f"({latest.get('updated_at', '?')}): the server may hold part of that deploy.")
    md += ["", "| File | This deploy | Last deployed | |", "|---|---|---|---|"]
    md += [f"| `{p}` | `{h}` | `{o}` | {st} |" for p, h, o, st in rows]
    md.append("")
    md.append(f"**{what}.**")
    if dropped:
        md.append(f"Deployed before but not in this deploy's list (left on the server as they are; deploy never deletes): "
                  f"{', '.join(dropped)}.")
    md.append(f"Access policies: not applied by this pipeline ({len(man['policies_in_repo'])} in the repo; "
              "Tools/UGS/README.md says when to apply each by hand).")

    details = {s["key"]: s["sha"] for s in man["scripts"]}
    details.update({"scripts": str(n), "changed": str(len(changed)) if base else str(n),
                    "policies_applied": "0", "manifest": man["digest"]})
    if ref and re.match(r"^[A-Za-z0-9._/-]{1,60}$", ref):
        details["ref"] = ref
    rollback_to = (base.get("commit") or "")[:12] if base else ""
    return {"summary": summary, "markdown": "\n".join(md) + "\n", "details": details, "changed": len(changed),
            "rollback_to": rollback_to, "digest": man["digest"]}


def _write_outputs(path: str, result: dict, prefix: str = "") -> None:
    detail_args = " ".join(f"{k}={v}" for k, v in sorted(result["details"].items()))
    with open(path, "a", encoding="utf-8") as fh:
        fh.write(f"{prefix}summary={result['summary']}\n")
        fh.write(f"{prefix}details={detail_args}\n")
        fh.write(f"{prefix}changed={result['changed']}\n")
        fh.write(f"{prefix}digest={result['digest']}\n")
        fh.write(f"{prefix}rollback_to={result['rollback_to']}\n")


def self_test() -> int:
    failures = []

    def expect(cond: bool, what: str) -> None:
        print(("PASS " if cond else "FAIL ") + what)
        if not cond:
            failures.append(what)

    def refused(fn, what: str) -> None:
        try:
            fn()
        except SystemExit:
            expect(True, what)
            return
        expect(False, what)

    with tempfile.TemporaryDirectory() as tmp:
        root = Path(tmp)
        (root / "Tools/UGS/CloudCode").mkdir(parents=True)
        (root / "Tools/UGS/AccessControl").mkdir(parents=True)
        (root / "Tools/UGS/AccessControl/project-policy.json").write_text("{}")
        (root / "Tools/UGS/CloudCode/economy.js").write_text("module.exports = 1;\n")
        (root / "Tools/UGS/CloudCode/economy.test.cjs").write_text("")
        (root / "Tools/UGS/CloudCode/match-report.js").write_text("module.exports = 2;\n")
        deploy = root / "Tools/UGS/deploy.sh"
        deploy.write_text("set -e\nSCRIPTS=(Tools/UGS/CloudCode/economy.js\n"
                          "         Tools/UGS/CloudCode/match-report.js)\nugs deploy \"${SCRIPTS[@]}\"\n")

        man = manifest(root)
        expect([s["key"] for s in man["scripts"]] == ["h_economy", "h_match_report"], "reads deploy.sh's list in order")
        expect(man["scripts"][0]["has_test"] and not man["scripts"][1]["has_test"], "notices which scripts have tests")
        expect(man["policies_in_repo"] == ["Tools/UGS/AccessControl/project-policy.json"], "counts the policies")

        first = plan(man, "development", None, "bleeding-edge", "a" * 40)
        expect("no earlier deploy" in first["summary"] and first["rollback_to"] == "", "first deploy: all new")

        hist = root / "hist.jsonl"
        old = dict(first["details"])
        old["h_match_report"] = "000000000000"
        hist.write_text(json.dumps({"result": "success", "commit": "b" * 40, "details": old}) + "\n"
                        + json.dumps({"result": "failure", "commit": "c" * 40, "details": {}}) + "\n")
        p = plan(man, "development", hist, "bleeding-edge", "a" * 40)
        expect(p["changed"] == 1 and "(match-report)" in p["summary"], "one changed script is named")
        expect(p["rollback_to"] == "b" * 12, "rollback_to is the last SUCCESSFUL deploy, not the failed one")
        expect("newest development record is a failure" in p["markdown"], "warns that the last deploy failed")

        hist.write_text(json.dumps({"result": "success", "commit": "b" * 40, "details": first["details"]}) + "\n")
        same = plan(man, "development", hist, "v1.2", "a" * 40)
        expect(same["changed"] == 0 and "0 of 2 scripts changed" in same["summary"], "an unchanged deploy says so")

        # The details must pass the launch-status contract's validator (no value may look like a credential).
        launch = Path(__file__).resolve().parents[1] / "Launch"
        if (launch / "launch_status.py").is_file():
            sys.path.insert(0, str(launch))
            import launch_status  # noqa: E402
            rec = {"schema": 1, "id": "ugs-development", "platform": "liveops", "kind": "deploy",
                   "result": "success", "updated_at": "2026-11-05T00:00:00Z", "source": "Deploy UGS scripts",
                   "channel": "development", "commit": "a" * 40, "summary": p["summary"],
                   "rollback_to": p["rollback_to"], "details": p["details"]}
            expect(launch_status.validate(rec) == [], "the record it feeds launch_status.py is valid")

        out = root / "out.txt"
        _write_outputs(str(out), p)
        expect(all("\n" not in line for line in out.read_text().splitlines()) and "details=" in out.read_text(),
               "writes one-line step outputs")

        # Negative controls: a list naming a missing file, and a deploy.sh with no list, are refused.
        deploy.write_text("SCRIPTS=(Tools/UGS/CloudCode/economy.js Tools/UGS/CloudCode/gone.js)\n")
        refused(lambda: manifest(root), "refuses a list that names a missing file")
        deploy.write_text("ugs deploy Tools/UGS/CloudCode/economy.js\n")
        refused(lambda: manifest(root), "refuses a deploy.sh with no SCRIPTS list")
        deploy.write_text("SCRIPTS=(Tools/UGS/CloudCode/economy.js Tools/UGS/Other/economy.js)\n")
        (root / "Tools/UGS/Other").mkdir()
        (root / "Tools/UGS/Other/economy.js").write_text("x")
        refused(lambda: manifest(root), "refuses two deployed scripts with one name")

    print(f"\n{'FAILED' if failures else 'OK'}: {len(failures)} failure(s)")
    return 1 if failures else 0


def main(argv: list[str] | None = None) -> int:
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    sub = ap.add_subparsers(dest="cmd", required=True)
    m = sub.add_parser("manifest")
    m.add_argument("--root", default=".")
    p = sub.add_parser("plan")
    p.add_argument("--root", default=".")
    p.add_argument("--env", required=True, choices=ENVS)
    p.add_argument("--history", help="history/ugs-<env>.jsonl from the launch-status branch (missing = none yet)")
    p.add_argument("--ref", default="")
    p.add_argument("--sha", default="")
    p.add_argument("--summary-md", help="append the markdown plan to this file (e.g. $GITHUB_STEP_SUMMARY)")
    p.add_argument("--github-output", help="append step outputs to this file (e.g. $GITHUB_OUTPUT)")
    p.add_argument("--output-prefix", default="", help="prefix for the step output names (e.g. dev_)")
    p.add_argument("--expect-digest", help="fail unless the manifest digest equals this (the plan job's)")
    sub.add_parser("self-test")
    args = ap.parse_args(argv)

    if args.cmd == "self-test":
        return self_test()
    root = Path(args.root).resolve()
    man = manifest(root)
    if args.cmd == "manifest":
        print(json.dumps(man, indent=2))
        return 0
    if args.expect_digest and args.expect_digest != man["digest"]:
        print(f"::error::The files to deploy (manifest {man['digest']}) are not the ones the plan showed "
              f"({args.expect_digest}). Nothing was deployed.", file=sys.stderr)
        return 1
    missing = [s["path"] for s in man["scripts"] if not s["has_test"]]
    if missing:
        print(f"::error::Every deployed script needs a .test.cjs beside it; missing for: {', '.join(missing)}",
              file=sys.stderr)
        return 1
    history = Path(args.history) if args.history else None
    result = plan(man, args.env, history, args.ref, args.sha)
    print(result["markdown"])
    if args.summary_md:
        with open(args.summary_md, "a", encoding="utf-8") as fh:
            fh.write(result["markdown"])
    if args.github_output:
        if not re.match(r"^[a-z_]{0,12}$", args.output_prefix):
            raise SystemExit("--output-prefix: lower-case letters and _ only")
        _write_outputs(args.github_output, result, args.output_prefix)
    return 0


if __name__ == "__main__":
    sys.exit(main())
