#!/usr/bin/env python3
"""Fill the Launch Console artifact's store from the repo: the pipeline board, the quality summary, the changelog,
and (once) the launch checklist and the runbook timeline.

The console (https://claude.ai/artifact/XwF8FQrHhUTvNv1gE8kws2, source Docs/LAUNCH/console/index.html) cannot reach
GitHub itself, so a Claude session copies the data in with the ArtifactData tool. This script prepares that data as
one JSON file per document plus a `writes.json` listing them, and the session sends `writes.json` as one batch:

    python3 Tools/Launch/console_sync.py board   --out <dir>   # status/board from the launch-status branch
    python3 Tools/Launch/console_sync.py seed    --out <dir>   # checklist/* and runbook/* (first fill only)
    python3 Tools/Launch/console_sync.py self-test

`board` is safe to run any time: it writes only `status/board` (admin-only in the page's rules). `seed` writes the
checklist and runbook rows the team then edits on the page; run it once. Re-running it would overwrite the team's
status and "taken by" fields, so it refuses unless `--force` is given, and a later re-seed should send only new rows.

The checklist rows are READINESS.md section 10 ("Ranked blocker list") and its "Needs a human" steps, as of the
2026-11-05 audit. Owners are who DECIDES in that area (Docs/TEAM.json), never an assignee: an item is nobody's
until someone takes it on the page.
"""
from __future__ import annotations

import argparse
import datetime as _dt
import json
import subprocess
import sys
import tempfile
from pathlib import Path

DOC = "https://github.com/froglet-studio/Cosmic-Shore/blob/bleeding-edge/Docs/LAUNCH/READINESS.md"

# rank, id, title, platform, decides, risk, size, needs_human, blocks, steps, readiness anchor
CHECKLIST = [
    (0, "activation", "Activation ready: the gate for the mobile release", "mobile",
     "Shombith (leads activation; Caleb heads FTUE)", "Very high", "M-L", True, True,
     "Docs/ACTIVATION.md section 6, in order: play the training guide once in the editor; declare the activation "
     "events in UGS and build the funnel; watch three to five first-timers; decide the rest of the Main Quest; fill the "
     "Mentor tips and card Tips; then flip DefaultAllUnlocked and ProgressionBackendGate.CloudEnabled. Target: D1 >= 35 % "
     "and D7 >= 15 % across two invite waves. (Added 2026-11-06: mobile ships when activation is ready.)", "#0"),
    (1, "ugs-production-env", "Release builds talk to UGS production", "liveops", "Shombith", "Very high", "S", True, True,
     "Code: release builds pick `production` through the build stamp (PR claude/launch-build-stamp).\n"
     "UGS dashboard, production environment: create the weekly_challenge board (same sort and reset as development), "
     "declare the 28 analytics events and 67 parameters, add the identity providers you ship (Tools/UGS/README.md), "
     "turn on Moderation, turn on Vivox or hide the Port tab, turn on Cloud Diagnostics, set spend alerts.", "#6"),
    (2, "repo-security-settings", "Repo security: trunk review, Environments, old secrets, rotated token", "launch",
     "Shombith (admin)", "Very high", "S", True, True,
     "Settings > Rules > 'Protect trunks': stop routine bypass, require the guard's checks (PR 1693).\n"
     "Settings > Environments: create ugs-production (required reviewer) and move UGS_CLI_SERVICE_KEY_ID and "
     "UGS_CLI_SERVICE_SECRET_KEY into it; limit testflight and toybox to bleeding-edge with a required reviewer.\n"
     "Settings > Secrets > Actions (repository): delete IOS_*, ASC_*, APPLE_ID_EMAIL, APPLE_APP_SPECIFIC_PASSWORD, "
     "ASC_APP_ID, ASC_ITC_PROVIDER if present; revoke the app-specific password at appleid.apple.com.\n"
     "Unity dashboard: rotate the symbol-upload token behind USYM_UPLOAD_AUTH_TOKEN; replace the Dropbox export zip; "
     "write the date in Docs/IOS_BUILD.md.\n"
     "Settings > Actions > General: require approval for all external contributors.\n"
     "Move agent sessions to a bot account (frogletagent-builder) without code-owner or bypass rights.", "#9"),
    (3, "steamworks-account", "Steamworks partner account, fee, base app and Playtest app exist", "windows",
     "Garrett + Shombith", "Very high", "S", True, True,
     "Gather entity, EIN, address and a bank account whose holder name matches the entity exactly.\n"
     "Create the partner account on a company-controlled Steam account; an officer signs the agreements.\n"
     "Pay the $100 Steam Direct fee; complete tax and banking (2-7 days).\n"
     "Record the base app id and depot id (STEAM_APPID / STEAM_DEPOTID on the build machine only).\n"
     "Create the Playtest child app (its customer-visible name is permanent): Limited signup, 1-3 invites, Not "
     "Playable until review. Record STEAM_PLAYTEST_APPID / STEAM_PLAYTEST_DEPOTID.\n"
     "Set appId and signInWithSteam in Assets/Resources/SteamConfig.asset; enable the Steam provider in UGS.", "#1"),
    (4, "unity-compile-ci", "A Unity compile and EditMode run gate trunk", "launch", "Shombith", "High", "S", True, True,
     "Merge PR 1695 (GameCI compile on GitHub-hosted runners).\n"
     "Create a Unity ID for CI; put UNITY_EMAIL and UNITY_PASSWORD (and UNITY_SERIAL for a Pro seat) in the unity-ci "
     "Environment, limited to bleeding-edge and development.\n"
     "Run the guard once on bleeding-edge and check the job says 'Licence activated'.", "#7"),
    (5, "build-identity", "Every build shows its version, build number and commit", "launch", "Shombith", "High", "S",
     False, True, "PR claude/launch-build-stamp. Then check a Development and a release build's settings line.", "#1"),
    (6, "protocol-in-matchmaking", "Old and new clients never meet; minimum-version gate", "backend",
     "Yash (multiplayer) / Shombith", "High", "S-M", False, True,
     "PRs claude/launch-protocol-filter and claude/launch-flags. Then the two-editor test in "
     "Docs/UNITY_VERIFICATION_CHECKLIST.md.", "#6"),
    (7, "ugs-deploy-gated", "Production UGS deploys need approval, show a diff, can roll back", "liveops", "Shombith",
     "High", "S", False, True, "PR claude/launch-ugs-deploy, then the ugs-production Environment (item 2).", "#6"),
    (8, "windows-il2cpp-player", "A Windows IL2CPP player reaches the menu and plays a match (R1)", "windows",
     "Shombith", "Very high", "M-L", True, True,
     "On a Windows build machine: Tools/Build/build_windows.sh, launch, sign in, reach Menu_Main, open and close the "
     "pause menu, play one match, read Player.log end to end. Then upload to the Playtest app's internal branch "
     "(Tools/Steam/upload.sh --target playtest).", "#1"),
    (9, "consent-and-children", "Consent and children: COPPA posture applied", "launch", "Counsel + Shombith", "High",
     "M", True, True,
     "Counsel rules on COPPA: social features (sign-in, Cloud Save, presence, Friends) currently start before the age "
     "gate, and age is checked by year only.\nSet submitAnalytics to 0 in ProjectSettings; the consent copy names "
     "crash reports; sign the PostHog DPA; accept Unity's DPA; set retention periods.", "#8"),
    (10, "licences-clean", "Licences clean: FMOD tier, SOAP, a font, sound-library files", "launch", "unknown", "High",
     "S-M", True, True,
     "FMOD: check the budget and revenue thresholds; buy a tier if exceeded.\nObvious SOAP: file the receipt and "
     "licence.\nElectronic Highway Sign font (ships via Resources): find its licence or remove it.\nThe sound-library "
     "named .wav files in the public repo (e.g. 'Cosmic Shore/Assets/AEROJet_Airplane Jet_S6k01-50.wav'): check the "
     "licence; remove from the public tree (and history if the licence forbids redistribution).\nAdd Vivox, "
     "Steamworks.NET, PostHog and Moderation to Docs/THIRD_PARTY_REGISTER.md.", "#8"),
    (11, "kill-switches", "Remote kill switches for the server gates and the mode list", "liveops", "Shombith", "High",
     "M", False, True, "PR claude/launch-flags (Cloud Code flags, no new package).", "#6"),
    (12, "persistence", "Persistence decided; Cloud Save refuses newer schemas", "backend", "Shombith", "High", "M",
     True, True, "Decide whether CloudEnabled turns on for the invite build. If yes: make Cloud Save refuse or preserve "
     "records with a newer SchemaVersion, and add write locks.", "#6"),
    (13, "web-domain-pipeline", "Web on its own domain, with staging and a rollback", "web", "DNS owner + Shombith",
     "High", "S-M", True, True,
     "Cloudflare: move the froglet.games DNS, connect play.froglet.games to the R2 bucket, set CORS.\n"
     "Landing repo: _data/toybox.yml build_root: https://play.froglet.games/toybox; add a staging page.\n"
     "UBA: create the Web target (WebToyboxBuild.PreExport). Add the secrets to the toybox Environment.\n"
     "PR claude/launch-web-pipeline for staging, promote, rollback and the manifest.", "#5"),
    (14, "ios-reproducible", "iOS builds from a commit, with a privacy manifest", "ios", "Shombith", "High", "L", True,
     True, "UBA: create the iOS target. PR claude/launch-player-builds feeds its export to ios-testflight.yml.\n"
     "Add PrivacyInfo.xcprivacy with the required-reason APIs the game uses (UserDefaults, file timestamps, boot time).",
     "#3"),
    (15, "in-app-deletion", "In-app account deletion", "backend", "Shombith", "High", "M", False, True,
     "Delete in the app: UGS DeleteAccountAsync, Cloud Save and the PostHog person. Required by App Store 5.1.1(v) and "
     "Google Play.", "#3"),
    (16, "ios-store-forms", "App Store Connect forms match the build", "ios", "store admin (not named in any doc) + counsel", "High", "S-M",
     True, True, "App Privacy label (analytics, crash data, user id, display name, gameplay, birth year); age rating "
     "questionnaire; an external TestFlight group, Beta App Review, and a new public link (README.md still links the "
     "old bundle id's).", "#3"),
    (17, "android-store-forms", "Play Console forms match the build", "android", "store admin (not named in any doc) + counsel", "High",
     "S-M", True, True, "Data safety (incl. the RECORD_AUDIO and BLUETOOTH_CONNECT permissions Vivox adds, or strip them "
     "if chat is text-only); IARC rating; target audience; ads; privacy policy and deletion URLs; confirm the account "
     "type (a Personal account needs a 12-tester, 14-day closed test).", "#4"),
    (18, "android-reproducible", "Android is reproducible and the upload key is safe", "android", "Shombith / Yash",
     "Medium", "L", True, True, "Store the upload keystore and passwords in the shared password manager with two "
     "holders. Upload the 32-bit build (ticket 1515). UBA: create the Android target; PR claude/launch-player-builds "
     "uploads to the internal track behind approval.", "#4"),
    (19, "crash-reporting", "Crash reporting proven on every platform", "launch", "Shombith", "Medium", "S", True, True,
     "Unity dashboard: Cloud Diagnostics on for production. Force a crash from each platform's build and see it "
     "arrive.", "#7"),
    (20, "test-state", "Measured test state for the launch path", "launch", "the team", "High", "M-L", False, True,
     "EditMode Run All (free once item 4 lands); the launch-path QA P0s; the per-mode smoke run in Unity (PR "
     "claude/launch-mode-smoke).", "#7"),
    (21, "multiplayer-holds", "Multiplayer holds: B36, B35, B31, R13, R16", "backend", "Yash / Shombith", "High", "M",
     True, True, "Fix B36 (host dropped after a 10 s freeze); retest B35 and B31; make friend requests reachable (R13); "
     "the 4-player two-machine pass (R16 / H10).", "#7"),
    (22, "analytics-live", "Analytics live: events declared, launch views built", "launch", "Shombith", "Medium", "M",
     True, True, "UGS Event Manager: declare the 28 events and 67 parameters per environment. Build D1/D7, crash-free, "
     "session length and mode-share views. Tag the website's store links (Docs/Analytics/utm-conventions.md).", "#8"),
    (23, "runbook-comms", "Launch-day runbook and incident steps written", "launch", "Shombith", "Medium", "S", False,
     True, "Docs/LAUNCH/RUNBOOK.md and Docs/LAUNCH/LIVE_UPDATES.md (this plan's Phase 4).", "#8"),
    (24, "perf-budgets", "Performance and memory budgets per device tier", "launch", "the team", "Medium", "M", False,
     False, "Budgets per tier and one low-end phone memory measurement.", "#7"),
    (25, "store-pages", "Store pages: capsules, screenshots, copy, trailer", "windows", "Caleb", "Medium", "L", True,
     False, "Not needed for the invite build. Five Steam capsules, screenshots, short and long copy, a trailer; iPhone "
     "6.9\" and iPad 13\" screenshot sets.", "#8"),
    (26, "lofelt-16kb", "Replace or update the 4 KB-aligned haptics library", "android", "unknown", "Low", "S-M", False,
     False, "arm64 liblofelt_sdk.so is 4 KB-aligned; Play blocks updates without 16 KB support from 2027-02-01 "
     "(ticket 1710).", "#4"),
]

RUNBOOK = [
    ("T-7", 1, "Freeze: tag the release candidate; the release pipeline makes notes and builds", "whoever takes it"),
    ("T-7", 2, "Every platform's pre-release channel carries the candidate (Steam internal, TestFlight, Play internal, web staging)", "whoever takes it"),
    ("T-7", 3, "Smoke every mode in Unity; EditMode green", "whoever takes it"),
    ("T-1", 1, "Go/no-go on this console: every pill Go or an accepted risk", "the launch lead (decides)"),
    ("T-1", 2, "UGS production deploy of the flags and Cloud Code (approved), minimum version set", "Shombith"),
    ("T-0", 1, "Promote each platform (Steam Playtest default, TestFlight external group, Play closed track, web /play)", "area owner per platform"),
    ("T-0", 2, "Send the invite wave under the signed wave policy (A6); post on Discord", "Garrett (wave policy)"),
    ("T-0", 3, "Watch crash reports and join failures for 4 hours", "whoever takes it"),
    ("T+1", 1, "D1 read-out: crashes, join failures, retention; decide on a hotfix", "the team"),
    ("T+7", 1, "D7 read-out; plan the next wave", "the team"),
]


def write_doc(out: Path, collection: str, doc_id: str, data: dict, writes: list) -> None:
    path = out / collection / f"{doc_id}.json"
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(data, indent=2, sort_keys=True) + "\n")
    writes.append({"op": "set", "collection": collection, "doc_id": doc_id, "file_path": str(path)})


def now() -> str:
    return _dt.datetime.now(_dt.timezone.utc).replace(microsecond=0).isoformat().replace("+00:00", "Z")


def seed(out: Path) -> list:
    writes: list = []
    for rank, cid, title, platform, owner, risk, size, human, blocks, steps, anchor in CHECKLIST:
        write_doc(out, "checklist", cid, {
            "rank": rank, "title": title, "platform": platform, "owner": owner, "risk": risk, "size": size,
            "needsHuman": human, "blocks": blocks, "status": "open", "steps": steps, "takenBy": None, "due": None,
            "evidence": None, "source": DOC, "section": anchor, "seededAt": now(),
        }, writes)
    for phase, order, step, owner in RUNBOOK:
        write_doc(out, "runbook", f"{phase.lower().replace('+', 'p').replace('-', 'm')}-{order}",
                  {"phase": phase, "order": order, "step": step, "owner": owner, "status": "todo"}, writes)
    write_doc(out, "status", "quality", {
        "qa_open": 181, "qa_p0": 80, "editmode_failures": 48, "verification_red": 580,
        "source": "Readiness audit (Docs/LAUNCH/READINESS.md section 7)", "as_of": "2026-11-05",
    }, writes)
    return writes


def board(out: Path, remote: str) -> list:
    subprocess.run(["git", "fetch", "-q", remote, "launch-status"], check=True)
    sha = subprocess.run(["git", "rev-parse", "FETCH_HEAD"], check=True, text=True, capture_output=True).stdout.strip()
    data = json.loads(subprocess.run(["git", "show", "FETCH_HEAD:status.json"], check=True, text=True,
                                     capture_output=True).stdout)
    data["synced_at"] = now()
    data["branch_commit"] = sha[:12]
    writes: list = []
    write_doc(out, "status", "board", data, writes)
    return writes


def self_test() -> int:
    global CHECKLIST
    ids = [row[1] for row in CHECKLIST]
    assert len(ids) == len(set(ids)), "duplicate checklist id"
    platforms = {"windows", "ios", "android", "web", "liveops", "backend", "launch", "mobile"}
    bad = [row[1] for row in CHECKLIST if row[3] not in platforms]
    assert not bad, f"unknown platform on {bad}"
    with tempfile.TemporaryDirectory() as tmp:
        writes = seed(Path(tmp))
        assert len(writes) == len(CHECKLIST) + len(RUNBOOK) + 1
        assert len(writes) <= 50, "one ArtifactData batch holds at most 50 writes"
        doc = json.loads(Path(writes[0]["file_path"]).read_text())
        assert doc["status"] == "open" and doc["takenBy"] is None, "a seeded item must be open and nobody's"
    # Negative control: an item on an unknown platform must be caught.
    saved = CHECKLIST
    CHECKLIST = saved + [(99, "x", "x", "macos", "x", "x", "S", False, False, "", "#")]
    try:
        bad = [row[1] for row in CHECKLIST if row[3] not in platforms]
        assert bad == ["x"]
    finally:
        CHECKLIST = saved
    print(f"self-test OK: {len(CHECKLIST)} checklist rows, {len(RUNBOOK)} runbook steps, negative control caught")
    return 0


def main() -> int:
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("cmd", choices=["board", "seed", "self-test"])
    ap.add_argument("--out", type=Path)
    ap.add_argument("--remote", default="origin")
    ap.add_argument("--force", action="store_true")
    args = ap.parse_args()
    if args.cmd == "self-test":
        return self_test()
    if not args.out:
        ap.error("--out is required")
    args.out.mkdir(parents=True, exist_ok=True)
    if args.cmd == "seed" and not args.force and (args.out / "SEEDED").exists():
        print("refusing: already seeded from this directory; re-seeding overwrites the team's edits (--force)")
        return 1
    writes = seed(args.out) if args.cmd == "seed" else board(args.out, args.remote)
    if args.cmd == "seed":
        (args.out / "SEEDED").write_text(now())
    (args.out / "writes.json").write_text(json.dumps(writes, indent=2) + "\n")
    print(f"{len(writes)} writes -> {args.out / 'writes.json'}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
