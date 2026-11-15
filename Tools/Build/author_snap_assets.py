#!/usr/bin/env python3
"""
Authors every serialized asset Snap needs (GameModes.Snap = 114, hint toast 189 - the ids
Docs/ElementalLoop/GAME_KIT_ROSTER.md row 24 reserved for the Goby & Pistol's Charge game).

Snap is a hunt through Kelpwood's ball of kelp: the Goby LOCKS a rival; while that rival is on the Pistol's screen a
vortex ring the Pistol fires homes on them, and every RING HIT (Vortex Sting, a MissileDirect-class combat hit) is one
point. First domain to the target wins. The arena is Kelpwood's four cells, referenced (the kelp is the perches and the
neutral mass the Goby AI routes the Pistol's islands through); the scene is Kelpwood's, cloned. No mode AI: the pair's
own crew AI locks the nearest rival and snaps when the lock is on screen. Nothing about the pair changes.
See Assets/_Scripts/Controller/Arcade/SNAP.md; the shared half is goby_games_lib.py.

    python3 Tools/Build/author_snap_assets.py [--check]
"""
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import arcade_mode_lib as lib  # noqa: E402
import goby_games_lib as gg  # noqa: E402

MODE_ID = 114
TOAST_HINT = 189
HIT_TARGET = 5            # first cut, unplayed: the ring reloads in 4 s and homes only while the lock is on screen
RING_HIT_CLASS = 1        # CombatHitClass.MissileDirect - GobyPistolConfig.ringHitClass
COMEBACK_RATE = 0.8       # a quarter of the target (1.25 hits) behind buys one element level
COMBAT_CHANNEL = "45fb43a0cddb5cc3cc4dc448df994152"   # the game's combat-hit ScriptableEvent

errors = []
config = open(os.path.join(lib.ROOT, gg.GOBY_CONFIG), encoding="utf-8").read()
if f"  ringHitClass: {RING_HIT_CLASS}\n" not in config:
    errors.append(f"GobyPistolConfig.ringHitClass is no longer {RING_HIT_CLASS} (MissileDirect) - SnapScoringRule pays "
                  f"that class; re-author ringHitClass below")
if f"  onCombatHitLanded: {{fileID: 11400000, guid: {COMBAT_CHANNEL}" not in config:
    errors.append("GobyPistolConfig.onCombatHitLanded is no longer the game's combat-hit channel - ring hits would score nothing")
if f"MissileDirect = {RING_HIT_CLASS}," not in open(os.path.join(lib.ROOT, "Assets/_Scripts/Data/Enums/CombatHitClass.cs")).read():
    errors.append(f"CombatHitClass.MissileDirect is no longer {RING_HIT_CLASS}")

gg.author(gg.Spec(
    mode_id=MODE_ID, name="Snap", display="Snap", doc="SNAP.md", toast_hint=TOAST_HINT, element="Charge",
    donor="Kelpwood", donor_monitor="KelpwoodBiteTurnMonitor", donor_vessel_id=lib.VESSEL_CLASS_ID["Angler"],
    monitor="SnapHitTurnMonitor", metric=8, golf=1,
    rule_fields=f"""  ringHitPoints: 1
  ringHitClass: {RING_HIT_CLASS}
  otherHitPoints: 0
  hitsLeftTemplate: '{{count}} Hits Left'
  hitsLandedTemplate: '{{count}} ring hits'
  victoryHeader: VICTORY
  victoryLabel: SNAP TIME
  defeatHeader: DEFEAT
  defeatLabel: HITS LEFT
""",
    end_key="snapHitTarget", end_value=HIT_TARGET,
    comeback_rate=COMEBACK_RATE, comeback_reference=HIT_TARGET,
    min_players=2, min_domains=2,   # a RULE: a ring never scores on a team-mate
    description=("Goby and Pistol only - a hunt through a ball of kelp. From the Goby, dart from strand to strand and "
                 "lock a rival pilot. From the Pistol, ride the islands the Goby lays and fire a vortex ring when the "
                 "locked rival is on your screen - it homes on them, and every ring hit is a point that takes their "
                 "petals. Higher intensities thicken the kelp, thin the herds and crystals, and add danger tips and "
                 "collars. First team to the target wins."),
    toasts=(lib.toast(81, "<b>{0}</b> landed a vortex ring on a rival!", tint_domain=1, domain_names=0)
            + lib.toast(128, "{0} is a quarter of the way to {2} ring hits", tint_domain=1, domain_names=0)
            + lib.toast(129, "{0} is halfway - {1}/{2} ring hits", tint_domain=1, domain_names=0)
            + lib.toast(130, "{0} takes the lead - {1}/{2}", tint_domain=1, domain_names=0)
            + lib.toast(TOAST_HINT, "Lock a rival from the Goby - fire a ring from the Pistol while they are on screen "
                        "and it homes on them", domain_names=0)),
    preview_notes=("Kelpwood's kelp ball at each intensity, flown by the Goby and Pistol. OPEN-ENDED - it scores on "
                   "ring hits on rival pilots and a solo preview has none; what it teaches is the kelp to perch on and "
                   f"the islands the Goby lays. Vessel pinned to Goby({gg.GOBY_ID})."),
    objective_text="Lock a rival from the Goby, then fire a ring from the Pistol while they are on screen",
    kit_needles=[
        ("var hitClass = Config.RingHitClass;", "a ring hit is admitted with the config's class, which the rule pays"),
        ("Config.OnCombatHitLanded.Raise(new CombatHitStats", "a ring hit must be raised on the combat-hit channel to score"),
        ("if (LockedInView()) run.Target = _lockVessel;", "a ring homes on the Goby's lock while it is on screen"),
        ("void AiLockNearestRival()", "the AI Goby locks the nearest rival - the game's only AI"),
    ],
    extra_errors=errors,
))
