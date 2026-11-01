#!/usr/bin/env python3
"""Fair-play gate (Threat Board R14): no fair-play measure may ever lock content a player paid for.

    python3 Tools/Build/check_fairplay_paid_content.py            # scan, exit 1 on any finding
    python3 Tools/Build/check_fairplay_paid_content.py --check    # same (CI spelling)
    python3 Tools/Build/check_fairplay_paid_content.py --self-test

The rule (Docs/AntiCheat/README.md section 4.6; EU consumer authorities, March 2025; the FTC's Epic order): a
restriction covers only play WITH or SEEN BY other people - ranked, public matchmaking (the Undertow pool),
leaderboards, and rewards earned through the breach. Solo and AI play are never restricted in any mode, so a
purchase is never cut off and the fair-play code never needs to know what was bought.

This gate holds that line in the code, both ways:
  1. FAIR-PLAY code never names the paid-content layer (episode tokens, owned episodes, the store, order
     receipts), so no detector, trust record or Undertow can gate a purchase;
  2. PAID-CONTENT code never names a fair-play type or record, so ownership and token spending can never be
     made to depend on a flag.

What is fair-play code: the files listed in FAIR_PLAY, every Assets/_Scripts .cs whose file name contains one of
FAIR_PLAY_NAME_PARTS (so phase B's FairPlay*, Undertow* and TrustRecord* files are covered the day they are made),
everything under FAIR_PLAY_DIRS, and the Cloud Code scripts. What is paid-content code: PAID below. Comments are
removed before scanning (C# // and /* */, JS // and /* */); string literals stay, because a Cloud Save key name
is a string.

What it does NOT prove: that a shared screen (a mode launcher, the arcade grid) never combines the two. Phase B's
mode lock goes in a fair-play file and asks only how the player is playing (with people or alone), never which
mode or episode; review that change against this rule.
"""
import os
import re
import sys

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))

FAIR_PLAY = [
    "Assets/_Scripts/Controller/Arcade/MatchReporter.cs",
    "Assets/_Scripts/Controller/Multiplayer/FairPlaySignals.cs",
    "Assets/_Scripts/Controller/Multiplayer/MovementPlausibilityMonitor.cs",
    "Assets/_Scripts/ScriptableObjects/MovementPlausibilityConfigSO.cs",
    "Assets/_Scripts/ScriptableObjects/PlayerReportConfigSO.cs",
    "Assets/_Scripts/System/CloudData/FairPlayAge.cs",
    "Assets/_Scripts/System/CloudData/ServerEconomy.cs",
    "Assets/_Scripts/System/CloudData/ServerMatchReports.cs",
    "Assets/_Scripts/System/CloudData/ServerScores.cs",
    "Assets/_Scripts/Utility/DisplayName/DisplayNameValidator.cs",
]
FAIR_PLAY_NAME_PARTS = ["FairPlay", "Undertow", "TrustRecord", "AntiCheat"]
FAIR_PLAY_DIRS = ["Assets/_Scripts/System/Moderation", "Assets/_Scripts/System/Steam", "Assets/_Scripts/System/PlatformSignIn",
                  "Tools/UGS/CloudCode"]

PAID = [
    "Assets/_Scripts/System/Monetization",
    "Tools/UGS/Commerce",
    "Assets/_Scripts/System/IAPManager.cs",
    "Assets/_Scripts/ScriptableObjects/SO_EpisodeTokenConfig.cs",
    "Assets/_Scripts/ScriptableObjects/SO_EpisodeData.cs",
    "Assets/_Scripts/ScriptableObjects/SO_CommerceAvailability.cs",
    "Assets/_Scripts/UI/Screens/EpisodeScreen.cs",
]

# Names of the paid layer that fair-play code must never use (whole words, case-sensitive for C#).
PAID_NAMES = [
    "EpisodeTokenService", "EpisodeTokenController", "IEpisodeTokenPurchaseProvider", "SO_EpisodeTokenConfig",
    "SO_EpisodeData", "SO_CommerceAvailability", "IAPManager", "EpisodeScreen", "OrderReceipt",
    "OwnsEpisode", "TryUnlockEpisode", "CanUnlockEpisode", "GrantTokens", "RedeemedOrderIds",
    "LifetimeEpisodeTokensPurchased", "LifetimeEpisodeTokensSpent", "OwnedEpisodes", "EpisodeTokens",
]
# Also refused in fair-play Cloud Code: any key or field about episodes, tokens bought, or orders.
PAID_JS = re.compile(r"episode|RedeemedOrder|orderId|purchase", re.IGNORECASE)

# Names of the fair-play layer that paid-content code must never use.
FAIR_PLAY_NAMES = [
    "FairPlaySignals", "FairPlaySignal", "FairPlaySignalKind", "FairPlayAge", "MatchReportGate", "ServerMatchReports",
    "MatchReporter", "MovementPlausibilityMonitor", "PlayerReports", "ServerEconomyGate", "ServerScoreGate",
    "Undertow", "TrustRecord", "FAIRPLAY_CONDUCT", "FAIRPLAY_SIGNALS", "FAIRPLAY_AGE", "FAIRPLAY_TRUST",
]


def strip_comments(text):
    text = re.sub(r"/\*.*?\*/", lambda m: "\n" * m.group(0).count("\n"), text, flags=re.S)
    return re.sub(r"//[^\n]*", "", text)


def rel(path):
    return os.path.relpath(path, ROOT).replace(os.sep, "/")


def walk(entry, exts):
    full = os.path.join(ROOT, entry)
    if os.path.isfile(full):
        yield full
    elif os.path.isdir(full):
        for d, _, files in os.walk(full):
            for f in sorted(files):
                if f.endswith(exts):
                    yield os.path.join(d, f)


def is_test(path):
    return "/Tests/" in rel(path) or path.endswith(".test.cjs")


def fair_play_files():
    seen = set()
    for entry in FAIR_PLAY + FAIR_PLAY_DIRS:
        for p in walk(entry, (".cs", ".js")):
            if not is_test(p):
                seen.add(p)
    for d, _, files in os.walk(os.path.join(ROOT, "Assets", "_Scripts")):
        for f in files:
            if f.endswith(".cs") and any(part in f for part in FAIR_PLAY_NAME_PARTS):
                p = os.path.join(d, f)
                if not is_test(p):
                    seen.add(p)
    return sorted(seen)


def paid_files():
    out = []
    for entry in PAID:
        out.extend(p for p in walk(entry, (".cs", ".js")) if not is_test(p))
    return sorted(set(out))


def words(names):
    return re.compile(r"\b(" + "|".join(map(re.escape, names)) + r")\b")


def scan_text(label, text, is_js, forbidden):
    """Findings in one file's text: (label, line, what)."""
    found = []
    body = strip_comments(text)
    for i, line in enumerate(body.split("\n"), 1):
        for m in forbidden.finditer(line):
            found.append((label, i, m.group(0)))
        if is_js and forbidden is PAID_RE:
            for m in PAID_JS.finditer(line):
                found.append((label, i, m.group(0)))
    return found


PAID_RE = words(PAID_NAMES)
FAIR_RE = words(FAIR_PLAY_NAMES)


def scan():
    findings = []
    fair, paid = fair_play_files(), paid_files()
    overlap = set(fair) & set(paid)
    for p in sorted(overlap):
        findings.append((rel(p), 0, "is listed as BOTH fair-play and paid-content code"))
    for p in fair:
        with open(p, encoding="utf-8", errors="replace") as fh:
            findings += [(l, n, "fair-play code names the paid layer: " + w)
                         for l, n, w in scan_text(rel(p), fh.read(), p.endswith(".js"), PAID_RE)]
    for p in paid:
        with open(p, encoding="utf-8", errors="replace") as fh:
            findings += [(l, n, "paid-content code names the fair-play layer: " + w)
                         for l, n, w in scan_text(rel(p), fh.read(), False, FAIR_RE)]
    return findings, len(fair), len(paid)


def self_test():
    cases = [
        # (text, is_js, forbidden, expected count)
        ("if (EpisodeTokenService.OwnsEpisode(id)) Lock();", False, PAID_RE, 2),
        ("// never call EpisodeTokenService here", False, PAID_RE, 0),
        ("/* OwnsEpisode */ var x = 1;", False, PAID_RE, 0),
        ("var owned = MyEpisodeTokenServiceWrapper;", False, PAID_RE, 0),  # whole words only
        ("const k = \"PLAYER_EPISODES\";", True, PAID_RE, 1),               # a key name is a string
        ("const k = \"FAIRPLAY_CONDUCT\";", True, PAID_RE, 0),
        ("if (FairPlaySignals.All.Count > 0) return false;", False, FAIR_RE, 1),
        ("bool flagged = MatchReportGate.Enabled && Undertow.Contains(p);", False, FAIR_RE, 2),
        ("// the Undertow never touches this", False, FAIR_RE, 0),
    ]
    bad = 0
    for text, is_js, forbidden, want in cases:
        got = len(scan_text("case", text, is_js, forbidden))
        if got != want:
            bad += 1
            print("SELF-TEST FAIL: %r -> %d finding(s), expected %d" % (text, got, want))
    # The real trees must resolve: a renamed folder would silently scan nothing.
    for entry in FAIR_PLAY + FAIR_PLAY_DIRS + PAID:
        if not os.path.exists(os.path.join(ROOT, entry)):
            bad += 1
            print("SELF-TEST FAIL: listed path does not exist: " + entry)
    print("self-test: %s (%d cases)" % ("OK" if bad == 0 else "%d FAILED" % bad, len(cases)))
    return 1 if bad else 0


def main(argv):
    if "--self-test" in argv:
        return self_test()
    findings, nf, np_ = scan()
    for label, line, what in findings:
        print("%s:%d: %s" % (label, line, what))
    print("fair-play / paid-content check: %d PROBLEM(S) (%d fair-play files, %d paid-content files)"
          % (len(findings), nf, np_))
    return 1 if findings else 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
