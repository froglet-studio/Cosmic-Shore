#!/usr/bin/env python3
"""Audit hardcoded colour literals in Assets/_Scripts/UI against Docs/STYLE_FOUNDATION.md §11.

Reporting only -- writes nothing, changes nothing.

    python3 Tools/Build/audit_ui_color_literals.py            # summary
    python3 Tools/Build/audit_ui_color_literals.py --full      # per-literal table
    python3 Tools/Build/audit_ui_color_literals.py --check     # non-zero exit on an unclassified literal

WHAT COUNTS AS A LITERAL (carry this definition with any number you quote -- a colour-literal
count is meaningless without it, which is how a prior 165 became a later 184):

  * new Color(...) / new Color32(...) with ALL-CONSTANT arguments
  * the eleven Color.<named> statics
  * #RRGGBB[AA] inside string literals

Deliberately NOT counted: new Color(c.r, c.g, c.b, alpha) and friends (an alpha edit on a colour
that came from elsewhere -- 8 of these), and new Color[n] array allocations (3 of these).

VERDICTS are a hand-curated table keyed by file:line, because the classification is semantic and
not derivable from the value. Color.white is textLight on a TMP_Text and multiply-identity on an
Image, and no amount of colour-distance maths can tell those apart. Adding a literal to the tree
without adding a row here fails --check, which is the point.
"""
import os, re, sys
from collections import Counter, defaultdict

ROOT = os.path.join("Assets", "_Scripts", "UI")

NAMED = {
    "white": (1, 1, 1, 1), "black": (0, 0, 0, 1), "red": (1, 0, 0, 1),
    "green": (0, 1, 0, 1), "blue": (0, 0, 1, 1),
    "yellow": (1, 0.9215686, 0.01568628, 1), "cyan": (0, 1, 1, 1),
    "magenta": (1, 0, 1, 1), "gray": (0.5, 0.5, 0.5, 1), "grey": (0.5, 0.5, 0.5, 1),
    "clear": (0, 0, 0, 0),
}

# Not player-facing UI: the style foundation governs the game's UI, not the Editor's or the console's.
# `ActiveGameModesWindow.cs` and `LeaderboardConfigSOEditor.cs` were here until the per-mode
# leaderboard path was deleted (2026-09). An exclusion naming a file that no longer exists never
# matches and never complains, so it reads as a live carve-out forever - removed rather than left.
OUT_EDITOR = {
    "UniversalStatsProviderEditor.cs", "Model/MinigameHUDInspector.cs",
}
OUT_DEBUG = {
    "Modals/ArcadeGameConfigureModal.cs", "Screens/PartyInviteNotificationPanel.cs",
    "TestMiniGameEvents.cs",
}

# The token definitions themselves. §11's values have to be written down exactly once, and this is
# where. Excluded BY NAME rather than by a regex that happens not to match 0xE6 -- an exclusion you
# cannot see is indistinguishable from a bug, and this one hid 20 literals until it was checked.
OUT_TOKENS = {"UIThemeSO.cs", "UITheme.cs"}

TOKENS = {
    "textLight", "textInactive", "inactiveLight", "surfaceBlack", "surfaceVeryDark",
    "surfaceDark", "surfaceLight", "neutralLightest", "cta", "danger",
}

# file:line -> (verdict, note).  Verdict is a §11 token name, or a/b/c:
#   a = missing token   b = belongs in a feature-level SO   c = never designed
VERDICTS = {
    "ConnectingPanelController.cs:160": ("b", "SO_ColorSet — domain accent fallback"),
    "Controller/MantaVesselHUDController.cs:22": ("b", "Manta HUD — overcharge state; danger candidate"),
    "DomainVolumeHexGraphic.cs:84": ("b", "SO_ColorSet — hardcoded domain triad; SEE FLAG F1"),
    "DomainVolumeIndicator.cs:178": ("c", "alpha-0 hide, not a colour"),
    "DomainVolumeIndicator.cs:340": ("b", "SO_ColorSet — domain fallback"),
    "DomainVolumeIndicator.cs:352": ("b", "SO_ColorSet — domain fallback"),
    "DomainVolumeIndicator.cs:82": ("b", "SO_ColorSet — jade/ruby/gold fallback"),
    "Elements/DomainInfoData.cs:29": ("textLight", ""),
    "Elements/DomainInfoData.cs:30": ("inactiveLight", ""),
    "Elements/GameCard.cs:31": ("a", "locked-card tint — §10.6 says 'grey', gives no value"),
    "Elements/GameCard.cs:34": ("c", "multiply-identity white (untinted sprite)"),
    "Elements/IconRotator.cs:25": ("c", "decorative rotator palette — violates §1"),
    "Elements/IconRotator.cs:26": ("c", "decorative rotator palette — violates §1"),
    "Elements/IconRotator.cs:27": ("c", "decorative rotator palette — violates §1"),
    "Elements/IconRotator.cs:28": ("c", "decorative rotator palette — violates §1"),
    "Elements/IconRotator.cs:49": ("c", "multiply-identity white"),
    "Elements/InputDeviceIconSetSwitcher.cs:73": ("textLight", "§10.5 active icon"),
    "Elements/InputDeviceIconSetSwitcher.cs:74": ("inactiveLight", "§10.5 muted icon"),
    "Elements/LoadoutCard.cs:20": ("inactiveLight", "SEE FLAG F2 — deselected is currently white"),
    "Elements/OnlineInfoEntry.cs:39": ("cta", "§3 player online status; SEE FLAG F3 — currently white"),
    "Elements/OnlineInfoEntry.cs:48": ("c", "multiply-identity white"),
    "Elements/QuestItemCard.cs:42": ("c", "multiply-identity white"),
    "Elements/RequestInfoEntry.cs:29": ("cta", "§2 attention reuses CTA; currently white"),
    "Elements/ScoreNumberAnimator.cs:131": ("a", "positive/gain green — §2 gap table proposes no gain hue"),
    "Elements/ScoreNumberAnimator.cs:132": ("b", "HUDAnimationSettingsSO — value is danger (Δ0.008)"),
    "GameToastSystem/GameToastAPI.cs:59": ("b", "SO_ColorSet — domain fallback"),
    "GameToastSystem/GameToastController.cs:147": ("textLight", "non-domain toast text"),
    "GameToastSystem/GameToastController.cs:205": ("b", "SO_ColorSet — domain fallback"),
    "HUDAnimationSettingsSO.cs:34": ("a", "positive/gain green"),
    "HUDAnimationSettingsSO.cs:36": ("b", "HUDAnimationSettingsSO owns it; value is danger"),
    "HUDAnimationSettingsSO.cs:46": ("b", "HUDAnimationSettingsSO owns it; value is danger"),
    "MiniGameHUD.cs:119": ("surfaceBlack", "§10.3 scrim @50%"),
    "MiniGameHUD.cs:137": ("textLight", ""),
    "MiniGameHUD.cs:572": ("b", "SO_ColorSet — domain fallback"),
    "Modals/GameSettingsPanelController.cs:53": ("textLight", "§10.8 active label"),
    "Modals/GameSettingsPanelController.cs:54": ("inactiveLight", "§10.8 inactive label"),
    "Modals/HangarTrainingModal.cs:76": ("cta", "selection; SEE FLAG F4"),
    "Modals/ModalWindowManager.cs:99": ("c", "Color.clear raycast target, not a colour"),
    "ObjectiveArrowGraphic.cs:26": ("b", "SO_ColorSet — §3 objective arrow is TEAM colour; SEE FLAG F5"),
    "ObjectiveArrowGraphic.cs:29": ("b", "SO_ColorSet — §3 objective arrow is TEAM colour; SEE FLAG F5"),
    "ObjectiveArrowGraphic.cs:32": ("b", "SO_ColorSet — §3 objective arrow is TEAM colour; SEE FLAG F5"),
    "Privacy/PrivacyConsentOverlay.cs:109": ("surfaceBlack", "scrim @88% (Δ0.02)"),
    "Privacy/PrivacyConsentOverlay.cs:133": ("danger", "E65C6B vs FF4B3A — SEE FLAG F6"),
    "Privacy/PrivacyConsentOverlay.cs:138": ("c", "bespoke teal accept — pre-palette overlay"),
    "Privacy/PrivacyConsentOverlay.cs:156": ("inactiveLight", "474C59 vs 5C5F70, Δ0.08 — SEE FLAG F6"),
    "Privacy/PrivacyConsentOverlay.cs:158": ("c", "bespoke teal accept — pre-palette overlay"),
    "Privacy/PrivacyConsentOverlay.cs:256": ("c", "bespoke neutral dark — pre-palette overlay"),
    "Privacy/PrivacyConsentOverlay.cs:295": ("textLight", "F0F2F7 vs E6E9FF, Δ0.04 — SEE FLAG F6"),
    "Privacy/PrivacyConsentOverlay.cs:302": ("a", "secondary body text — §11 has one text colour"),
    "Privacy/PrivacyConsentOverlay.cs:309": ("a", "tertiary/muted text — §11 has one text colour"),
    "Privacy/PrivacyConsentOverlay.cs:336": ("c", "bespoke neutral dark — pre-palette overlay"),
    "Privacy/PrivacyConsentOverlay.cs:348": ("textLight", "F0F2F7 vs E6E9FF, Δ0.04 — SEE FLAG F6"),
    "Privacy/PrivacyConsentOverlay.cs:356": ("a", "input placeholder text — §10.2 does not spec it"),
    "Privacy/PrivacyConsentOverlay.cs:383": ("textLight", ""),
    "Privacy/PrivacyConsentOverlay.cs:400": ("c", "alpha-0 click target, not a colour"),
    "Privacy/PrivacyConsentOverlay.cs:407": ("a", "hyperlink — no link colour in the palette"),
    "ResourceDisplay.cs:39": ("a", "gauge normal fill — §11 has no gauge token"),
    "ResourceDisplay.cs:40": ("a", "gauge full/threshold — not the same idea as danger"),
    "Scoreboard.cs:592": ("b", "SO_ColorSet — domain fallback"),
    "Screens/LeaderboardsMenu.cs:214": ("a", "local-player row highlight — §10.10 specs only a '*'"),
    "Screens/LeaderboardsMenu.cs:215": ("a", "local-player row highlight"),
    "Screens/LeaderboardsMenu.cs:216": ("a", "local-player row highlight"),
    "Screens/LeaderboardsMenu.cs:220": ("textLight", "§10.10 row text"),
    "Screens/LeaderboardsMenu.cs:221": ("textLight", "§10.10 row text"),
    "Screens/LeaderboardsMenu.cs:222": ("textLight", "§10.10 row text"),
    "ThumbPerimeter.cs:23": ("c", "multiply-identity white"),
    "ToastNotification/ToastNotificationManager.cs:152": ("a", "toast surface — 1A1A26 is neutral; both §11 surfaces are blue-tinted"),
    "ToastNotification/ToastNotificationManager.cs:163": ("textLight", ""),
    "MaelstromRoundCard.cs:67": ("b", "SO_ColorSet — domain fallback"),
    "View/ControllerButtonIconReferences.cs:51": ("c", "multiply-identity white (fade reset)"),
    "View/DolphinVesselHUDView.cs:122": ("c", "multiply-identity white (rest)"),
    "View/DolphinVesselHUDView.cs:139": ("c", "multiply-identity white (rest)"),
    "View/DolphinVesselHUDView.cs:169": ("c", "multiply-identity white (rest)"),
    "View/DolphinVesselHUDView.cs:92": ("b", "SO_ColorSet — team crystal fallback"),
    "View/DolphinVesselHUDView.cs:94": ("b", "Dolphin HUD — armed flash state"),
    "View/MantaVesselHUDView.cs:17": ("c", "multiply-identity white (rest)"),
    "View/MantaVesselHUDView.cs:18": ("c", "Color.yellow highlight — never designed"),
    "View/RhinoVesselHUDView.cs:27": ("c", "multiply-identity white (rest)"),
    "View/RhinoVesselHUDView.cs:28": ("b", "Rhino HUD — crystal activated state"),
    "View/RhinoVesselHUDView.cs:29": ("c", "multiply-identity white (rest)"),
    "View/RhinoVesselHUDView.cs:30": ("b", "Rhino HUD — line activated state"),
    "View/RhinoVesselHUDView.cs:31": ("c", "multiply-identity white (rest)"),
    "View/RhinoVesselHUDView.cs:32": ("b", "Rhino HUD — debuff active state"),
    "View/SerpentVesselHUDView.cs:21": ("c", "multiply-identity white (pip full)"),
    "View/SerpentVesselHUDView.cs:22": ("b", "Serpent HUD — pip consuming state"),
    "View/SerpentVesselHUDView.cs:23": ("c", "white @25% = pip empty, an alpha not a hue"),
    "View/SparrowHUDView.cs:202": ("c", "multiply-identity white"),
    "View/SparrowHUDView.cs:49": ("b", "Sparrow HUD — blocked input; danger candidate"),
    "View/SparrowHUDView.cs:75": ("c", "multiply-identity white"),
    "View/SquirrelVesselHUDView.cs:115": ("c", "multiply-identity white"),
    "View/SquirrelVesselHUDView.cs:230": ("c", "Lerp toward white = desaturation, not a colour"),
    "View/SquirrelVesselHUDView.cs:247": ("c", "Lerp toward white = desaturation, not a colour"),
    "View/SquirrelVesselHUDView.cs:283": ("b", "Squirrel HUD — double-drift state"),
    "View/SquirrelVesselHUDView.cs:284": ("b", "Squirrel HUD — single-drift state"),
    "View/SquirrelVesselHUDView.cs:42": ("c", "multiply-identity white (rest)"),
    "View/SquirrelVesselHUDView.cs:44": ("b", "Squirrel HUD — joust flash state"),
    "View/SquirrelVesselHUDView.cs:46": ("b", "Squirrel HUD — crystal flash state"),
    "View/SquirrelVesselHUDView.cs:52": ("b", "Squirrel HUD — tube cooling state"),
    "View/SquirrelVesselHUDView.cs:54": ("b", "Squirrel HUD — tube ready state"),
    "View/SquirrelVesselHUDView.cs:63": ("c", "multiply-identity white (slam flash)"),
    "View/SquirrelVesselHUDView.cs:71": ("b", "Squirrel HUD — overheat hot state"),
    "View/SquirrelVesselHUDView.cs:73": ("b", "Squirrel HUD — overheat flash state"),
    "View/SquirrelVesselHUDView.cs:91": ("b", "SO_ColorSet — player domain fallback"),
    "View/SquirrelVesselHUDView.cs:92": ("c", "multiply-identity white"),
    "View/SquirrelVesselHUDView.cs:93": ("c", "multiply-identity white"),
    "View/UrchinVesselHUDView.cs:26": ("c", "multiply-identity white (rest)"),
    "View/UrchinVesselHUDView.cs:27": ("b", "Urchin HUD — ammo full state"),
    "View/UrchinVesselHUDView.cs:33": ("b", "Urchin HUD — riding state"),
    "View/VesselHUDView.cs:288": ("b", "ElementalBarsConfigSO.whiteColor already owns it"),
    "Views/ArcadeLoadoutView.cs:200": ("c", "reset-to-identity; SEE FLAG F7"),
    "Views/ArcadeLoadoutView.cs:201": ("c", "reset-to-identity; SEE FLAG F7"),
    "Views/ArcadeLoadoutView.cs:239": ("c", "reset-to-identity; SEE FLAG F7"),
    "Views/ArcadeLoadoutView.cs:240": ("c", "reset-to-identity; SEE FLAG F7"),
    "Views/ArcadeLoadoutView.cs:88": ("c", "reset-to-identity; SEE FLAG F7"),
    "Views/ArcadeLoadoutView.cs:89": ("c", "reset-to-identity; SEE FLAG F7"),
    "Views/ArcadeLoadoutView.cs:90": ("c", "reset-to-identity; SEE FLAG F7"),
    "Views/ArcadeLoadoutView.cs:91": ("c", "reset-to-identity; SEE FLAG F7"),
    "Views/DailyChallengeLeaderboardView.cs:100": ("textLight", "§10.10 row text"),
    "Views/DailyChallengeLeaderboardView.cs:101": ("textLight", "§10.10 row text"),
    "Views/DailyChallengeLeaderboardView.cs:93": ("a", "local-player row highlight"),
    "Views/DailyChallengeLeaderboardView.cs:94": ("a", "local-player row highlight"),
    "Views/DailyChallengeLeaderboardView.cs:95": ("a", "local-player row highlight"),
    "Views/DailyChallengeLeaderboardView.cs:99": ("textLight", "§10.10 row text"),
    "Views/HangarCaptainsView.cs:113": ("surfaceBlack", "Color.black → 00010A"),
    "Views/HangarCaptainsView.cs:99": ("c", "multiply-identity white"),
    "Views/HangarVesselDetailView.cs:197": ("textLight", "button label"),
    "Views/HangarVesselDetailView.cs:202": ("textLight", "button label"),
    "Views/PortSquadCaptainSelectionView.cs:13": ("inactiveLight", "SEE FLAG F8 — 'Selected' is grey"),
    "Views/PortSquadCaptainSelectionView.cs:14": ("surfaceBlack", "Color.black → 00010A"),
}

RE_CTOR  = re.compile(r"\bnew\s+Color(32)?\s*\(([^)]*)\)")
RE_NAMED = re.compile(r"\bColor\.(" + "|".join(NAMED) + r")\b")
RE_HEX   = re.compile(r"#[0-9A-Fa-f]{6}(?:[0-9A-Fa-f]{2})?")
RE_NUM   = re.compile(r"^(?:[0-9.]+f?|0[xX][0-9A-Fa-f]{1,2})$")


def hex_rgb(h):
    return tuple(int(h[i:i + 2], 16) / 255 for i in (0, 2, 4))


def collect():
    """Yield one record per constant colour literal under ROOT."""
    for dirpath, _dirs, files in os.walk(ROOT):
        for name in sorted(files):
            if not name.endswith(".cs"):
                continue
            path = os.path.join(dirpath, name)
            rel = os.path.relpath(path, ROOT).replace(os.sep, "/")
            with open(path, encoding="utf-8", errors="replace") as fh:
                for lineno, line in enumerate(fh, 1):
                    src = line.strip()
                    for m in RE_CTOR.finditer(line):
                        args = [a.strip() for a in m.group(2).split(",") if a.strip()]
                        if not args or not all(RE_NUM.match(a) for a in args):
                            continue  # derived from a variable -- not a literal
                        vals = [
                            float(int(a, 16)) if a[:2].lower() == "0x" else float(a.rstrip("f"))
                            for a in args
                        ]
                        if m.group(1) or any(a[:2].lower() == "0x" for a in args):
                            vals = [v / 255 for v in vals]
                        yield rel, lineno, tuple(vals[:3]), (vals[3] if len(vals) > 3 else 1.0), src
                    for m in RE_NAMED.finditer(line):
                        v = NAMED[m.group(1)]
                        yield rel, lineno, v[:3], v[3], src
                    for m in RE_HEX.finditer(line):
                        h = m.group(0).lstrip("#")
                        a = int(h[6:8], 16) / 255 if len(h) > 6 else 1.0
                        yield rel, lineno, hex_rgb(h[:6]), a, src


def main():
    full = "--full" in sys.argv
    check = "--check" in sys.argv

    raw = list(collect())
    scope, skipped = [], Counter()
    for rec in raw:
        f = rec[0]
        if f in OUT_TOKENS:
            skipped["§11 token definitions"] += 1
        elif f in OUT_EDITOR:
            skipped["editor-inspector chrome"] += 1
        elif f in OUT_DEBUG:
            skipped["debug/console markup"] += 1
        else:
            scope.append(rec)

    verdict = Counter()
    tokens = Counter()
    buckets = defaultdict(list)
    unclassified = []
    table = []

    for f, ln, rgb, a, src in scope:
        key = "%s:%d" % (f, ln)
        hexv = "%02X%02X%02X" % tuple(round(c * 255) for c in rgb)
        if key not in VERDICTS:
            unclassified.append((key, hexv, src[:100]))
            continue
        v, note = VERDICTS[key]
        if v in TOKENS:
            verdict["MAPPED"] += 1
            tokens[v] += 1
        else:
            verdict[v] += 1
            buckets[v].append((key, hexv, note))
        table.append((f, ln, hexv, a, v, note))

    print("Colour literals under %s: %d" % (ROOT, len(raw)))
    for k, n in skipped.most_common():
        print("  out of scope -- %-26s %d" % (k, n))
    print("  in scope                          %d" % len(scope))
    print()
    print("  MAPPED onto a §11 token           %d" % verdict["MAPPED"])
    for t, n in tokens.most_common():
        print("      %-18s %d" % (t, n))
    zero = sorted(TOKENS - set(tokens))
    if zero:
        print("      zero C# call sites: %s" % ", ".join(zero))
        print("      (authored in prefab/scene YAML, which C# never touches --")
        print("       as are ALL of §11's spacing, geometry and motion tokens)")
    print()
    for b, label in (("a", "missing token"), ("b", "feature-level SO"), ("c", "never designed")):
        print("  (%s) %-22s %d" % (b, label, verdict[b]))
        for note, n in Counter(n for _, _, n in buckets[b]).most_common():
            print("        x%-3d %s" % (n, note))
        print()

    if full:
        print("%-46s %5s %8s %6s  %s" % ("FILE", "LINE", "HEX", "ALPHA", "VERDICT / NOTE"))
        for f, ln, hexv, a, v, note in table:
            print("%-46s %5d  #%s  %5.2f  %s -- %s" % (f[:46], ln, hexv, a, v, note))

    if unclassified:
        print()
        print("UNCLASSIFIED -- add a VERDICTS row for each:")
        for key, hexv, src in unclassified:
            print("  %-52s #%s  %s" % (key, hexv, src))
        if check:
            return 1
    elif check:
        print("OK -- every in-scope literal is classified.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
