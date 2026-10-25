"""The Dolphin's ability-row icons (Resources/ElementalAbilityMaps/Dolphin.asset).

Three of the four cards are LIVE READOUTS the HUD generates or drives and carry no sprite to redraw (measured
2026-10-24, Docs/Icons/sheets/dolphin_before.png):
  Charge  Echo Sight          KEPT - BlastProfileGraphic, a generated stadium = the next blast's cross-section
  Mass    Crystal Seeding     KEPT - the omni crystal mark with the recharge wipe; lime, or the domain colour at 5
  Space   Echo Obliteration   KEPT - the two jaw halves (DolphinTopJaw / DolphinBottomJaw) opened to the next
                              blast's gape, lime when the bank is full; the hull's own silhouette as the act
  Time    Charge Fill Rate    the Dolphin seen from ABOVE (the live mesh, nose right) in the domain colour, turned
                              every frame by the signed angle between its forward and its course - a drift IS that
                              divergence, and the drift is what banks the boost. The BANK is the card's plate
                              gauge (AbilityIconBinding.gauge: the fleet's one linear fill), never a ring. Garrett
                              2026-10-25: "i like the top down view, but the ring now reads as a skimmer. lets just
                              have the whole lower totem fill with the consistency of other resources."
                              (before: the authored eleven-step dashed ring, Boost Display, stepping through
                              Dolphin_UI/Charges*.png - a gauge with nobody in it)
"""
import mesh_silhouette

HULL = "Dolphin"
VARIANT = "Assets/_Prefabs/UI Elements/VesselHUD/DolphinHUDVariant.prefab"
FBX = "Assets/_Models/Vessel Models/dolphin_shapekey_with_animations.fbx"   # the LIVE model (Docs/VesselModels/Dolphin.md §1)
# The mesh's own frame is Blender's: +Z up, the nose at -Y (the dossier's §1.1), so the plan view is pitch 0 and the
# nose comes to the RIGHT with mirror=True. The hull is left-right symmetric, so its bbox centre is on its axis
# and the icon turns about the hull when the HUD rotates it.
VIEW_TOP = dict(pitch=0, yaw=0, dilate_px=1.6, mirror=True)


def dolphin(box=112, cx=64.0, cy=64.0, roll=0.0):
    return mesh_silhouette.silhouette(FBX, box=box, cx=cx, cy=cy, roll=roll, **VIEW_TOP)


def charge_fill():
    """The Time card's one part: the Dolphin along its course, nose right, fitted to its canvas."""
    return dolphin()


OLD_RING = "Assets/_Graphics/Design Assests/HUD UI/New_Dolphin/Dolphin_UI/Charges-9.png"   # step 0 of the retired ring

ICONS = [
    dict(name="Dolphin_ChargeFill", element="Time", act="Charge Fill Rate: the Dolphin from above, turned by the live drift angle; the bank is the plate gauge",
         draw=charge_fill, before=OLD_RING),
]

REBIND = [
    # The Time card's icon (the old Boost Display ring Image) becomes a transparent container over two children: the
    # hull part the view tints with the domain colour and turns by the drift angle, and a bare gauge Image the lockup
    # adopts as the card's plate fill (the abilityIcons entry for Time names it). The eleven step sprites are
    # unbound: the list is emptied, the PNGs stay where the artist put them.
    dict(file=VARIANT, pair=dict(container="4926312112814601564", view="7878827982810303965", after="driftBoostIcon",
                                 children=[("ChargeFillHull", "Dolphin_ChargeFill", "driftHullIcon"),
                                           ("ChargeFillGauge", None, "driftBoostIcon")],
                                 binding=dict(element=4, gauge="ChargeFillGauge"),
                                 fields={"chargeSteps": "[]"})),
]
