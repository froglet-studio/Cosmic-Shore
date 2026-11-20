#!/usr/bin/env python3
"""crew_accounts.py - give each tester's accounts their dealt crew with nothing for them to do (Garrett, 2026-11-19:
"I want us to do the work and have it just work for them ... find all the accounts for each person").

Three steps, the middle one Garrett's:

  1. find    reads a Cloud Save export (Tools/Analytics/export_cloud_save.py: items.jsonl) and writes the review list:
             every account whose saved display name looks like one of the seven testers, with its id, name and dates.
               python3 Tools/Build/pilot_art/crew_accounts.py find --export <dir with items.jsonl>
  2. review  Garrett ticks the accounts that are really each person's (the list is a private file, never committed).
  3. apply   hashes the ticked ids into Docs/Pilots/art/roster/crew_accounts.json, which
             author_pilot_scene_assets.py writes into PaintedCrew.asset; the game then claims each account's crew on
             its next sign-in (PaintedCrewSO.CrewForAccount).
               python3 Tools/Build/pilot_art/crew_accounts.py apply

The repository is public: player ids, display names and who holds which crew stay in the private folder
(/mnt/project-files/pilot-crews/ or --private). Only SHA-256 hashes of the ids are committed.

  python3 Tools/Build/pilot_art/crew_accounts.py --self-test
"""
import argparse
import hashlib
import json
import os
import re
import sys

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", "..", ".."))
PUBLIC = os.path.join(ROOT, "Docs", "Pilots", "art", "roster", "crew_accounts.json")
PRIVATE = "/mnt/project-files/pilot-crews"
ABOUT = ("Testers' accounts that hold a dealt crew automatically (Garrett, 2026-11-19): SHA-256 (hex) of "
         "cosmic-shore/crew-account/ + the UGS player id, and the dealt crew index (roster.py deal order). Hashes only: "
         "the ids and whose they are stay private.")


def account_hash(player_id):
    """PaintedCrewSO.AccountHash, bit for bit."""
    return hashlib.sha256(("cosmic-shore/crew-account/" + player_id).encode("utf-8")).hexdigest()


def names_in(value):
    """Display names found in one saved item's value (a JSON string): any DisplayName / display_name field, at any
    depth."""
    found = []
    try:
        data = json.loads(value) if isinstance(value, str) else value
    except (ValueError, TypeError):
        data = value

    def walk(node):
        if isinstance(node, dict):
            for k, v in node.items():
                if isinstance(v, str) and re.fullmatch(r"(?i)display_?name(_norm)?", k) and v.strip():
                    found.append(v.strip())
                else:
                    walk(v)
        elif isinstance(node, list):
            for v in node:
                walk(v)

    walk(data)
    return found


def accounts(items):
    """{player_id: {"names": set, "first": str, "last": str, "keys": set}} from items.jsonl rows."""
    out = {}
    for row in items:
        pid = row.get("player_id") or row.get("playerId")
        if not pid:
            continue
        a = out.setdefault(pid, {"names": set(), "first": "", "last": "", "keys": set()})
        a["keys"].add(row.get("key", ""))
        if row.get("key") == "display_name_norm" and isinstance(row.get("value"), str):
            try:
                v = json.loads(row["value"])
            except ValueError:
                v = row["value"]
            if isinstance(v, str) and v.strip():
                a["names"].add(v.strip())
        a["names"].update(names_in(row.get("value")))
        for stamp in (row.get("created"), row.get("modified")):
            stamp = stamp.get("date") if isinstance(stamp, dict) else stamp
            if isinstance(stamp, str) and stamp:
                a["first"] = min(a["first"], stamp) if a["first"] else stamp
                a["last"] = max(a["last"], stamp)
    return out


def match(names, tester):
    """True when any saved name looks like the tester's: their name, or one of the aliases in the private crews file."""
    wants = [w.lower() for w in [tester["name"]] + tester.get("aliases", []) if w]
    for n in names:
        low = re.sub(r"[^a-z0-9]", "", n.lower())
        if any(re.sub(r"[^a-z0-9]", "", w) in low for w in wants):
            return True
    return False


def find(export, private):
    crews = json.load(open(os.path.join(private, "crews.json"), encoding="utf-8"))
    rows = [json.loads(line) for line in open(os.path.join(export, "items.jsonl"), encoding="utf-8") if line.strip()]
    found = accounts(rows)
    review = {"about": "Tick (keep true) each account that is really this person's; then run crew_accounts.py apply. "
                       "Private: never commit this file.", "testers": []}
    for index, tester in enumerate(crews["members"]):
        mine = [{"id": pid, "names": sorted(a["names"]), "first": a["first"], "last": a["last"], "keep": True}
                for pid, a in sorted(found.items(), key=lambda kv: kv[1]["last"], reverse=True)
                if match(a["names"], tester)]
        review["testers"].append({"name": tester["name"], "crew": index, "accounts": mine})
    path = os.path.join(private, "accounts_review.json")
    with open(path, "w", encoding="utf-8") as f:
        json.dump(review, f, indent=1)
    unnamed = sum(1 for a in found.values() if not a["names"])
    print(f"{len(found)} accounts read ({unnamed} with no saved name); review list: {path}")
    for t in review["testers"]:
        print(f"  {t['name']}: {len(t['accounts'])} account(s)")
    return review


def apply(private):
    review = json.load(open(os.path.join(private, "accounts_review.json"), encoding="utf-8"))
    rows, seen = [], set()
    for t in review["testers"]:
        for a in t["accounts"]:
            if a.get("keep") and a["id"] not in seen:
                seen.add(a["id"])
                rows.append({"hash": account_hash(a["id"]), "crew": int(t["crew"])})
    rows.sort(key=lambda r: (r["crew"], r["hash"]))
    with open(PUBLIC, "w", encoding="utf-8") as f:
        json.dump({"about": ABOUT, "accounts": rows}, f, indent=1)
        f.write("\n")
    print(f"{len(rows)} account hash(es) written to {os.path.relpath(PUBLIC, ROOT)}; "
          "now run python3 Tools/Build/author_pilot_scene_assets.py")


def self_test():
    fails = []

    def expect(what, ok):
        if not ok:
            fails.append(what)
            print("FAIL", what)

    expect("the hash is PaintedCrewSO.AccountHash's",
           account_hash("abc") == hashlib.sha256(b"cosmic-shore/crew-account/abc").hexdigest())
    rows = [
        {"player_id": "P1", "key": "PLAYER_PROFILE", "value": json.dumps({"Identity": {"DisplayName": "GarrettF"}}),
         "created": {"date": "2026-10-01"}, "modified": {"date": "2026-11-18"}},
        {"player_id": "P2", "key": "display_name_norm", "value": json.dumps("shombith"), "modified": {"date": "2026-11-02"}},
        {"player_id": "P3", "key": "PLAYER_PROFILE", "value": json.dumps({"Identity": {"DisplayName": "Pilot4821"}})},
        {"player_id": "P4", "key": "ACCOUNT_INVENTORY", "value": "{}"},
    ]
    a = accounts(rows)
    expect("names are read from a profile at any depth and from display_name_norm",
           a["P1"]["names"] == {"GarrettF"} and a["P2"]["names"] == {"shombith"})
    expect("dates span created to modified", a["P1"]["first"] == "2026-10-01" and a["P1"]["last"] == "2026-11-18")
    expect("an account with no name has none", not a["P4"]["names"])
    expect("a tester matches their name and aliases, case and punctuation aside",
           match({"GarrettF"}, {"name": "Garrett"}) and match({"b.e.l.a.c"}, {"name": "Caleb", "aliases": ["belac"]}))
    expect("a stranger does not match (negative control)", not match({"Pilot4821"}, {"name": "Garrett"}))
    print("crew_accounts self-test:", "ok" if not fails else f"{len(fails)} failure(s)")
    return 1 if fails else 0


def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("step", nargs="?", choices=["find", "apply"])
    ap.add_argument("--export", help="the folder export_cloud_save.py wrote (holds items.jsonl)")
    ap.add_argument("--private", default=PRIVATE, help="the private folder holding crews.json")
    ap.add_argument("--self-test", action="store_true")
    args = ap.parse_args()
    if args.self_test:
        return self_test()
    if args.step == "find":
        if not args.export:
            ap.error("find needs --export")
        find(args.export, args.private)
    elif args.step == "apply":
        apply(args.private)
    else:
        ap.print_help()
    return 0


if __name__ == "__main__":
    sys.exit(main())
