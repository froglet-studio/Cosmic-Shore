"""
materials.py - the four material slots and what each one means in the game.

A vessel's renderer wears FOUR materials, by slot INDEX, and the game writes slots by index:

    slot 0  DOMAIN   `ShipHelper.ApplyShipMaterial` paints the pilot's domain material into
                     slot 0 of every SkinnedMeshRenderer (VesselHelper.cs); `FlareBody` flashes
                     materials[0]. This is the team colour the pilot reads across a cell.
    slot 1  HULL     the body: the dark base the artist models ship as 'Body' / 'BASE'
                     (BlueBaseVesselMaterial in the older prefabs).
    slot 2  WINDOW   cockpit glass / eyes / screens ('Window' / 'windsheild';
                     ScreenVesselMaterial).
    slot 3  JET      engines and nozzles: `FlareEngine` sets materials[3]._ColorMultiplier
                     to 5 while boosting ('Engine' / 'accent'; LimeVesselMaterial).

The shipped Squirrel measures Domain 1804 / Body 4400 / Window 186 / Engine 440 polygons: the
domain is a third of the hull's faces, the glass a handful, the engines a tenth. A new model
keeps that balance so a vessel reads as "a dark hull with a strong band of team colour" at
the chase camera's distance rather than a team-coloured blob.

The FBX material NAMES are what a vessel's .meta remaps to .mat assets (externalObjects), so a
replacement written in place keeps the names the live file uses (Squirrel: Domain, Body,
Window, Engine). `slot_names_for()` returns them per vessel; a new vessel uses the generic
set. In Blender each slot also gets a viewport colour so the contact sheet shows the slots.
"""

import bpy

GENERIC = ("Domain", "Body", "Window", "Engine")

# viewport/render colours for the studio contact sheet (linear RGB)
STUDIO_COLOURS = {
    "Domain": (0.95, 0.56, 0.22, 1.0),   # the peach/orange accent the artist models use
    "Body": (0.07, 0.07, 0.085, 1.0),    # near-black hull
    "Window": (0.25, 0.85, 0.95, 1.0),   # glass cyan
    "Engine": (0.35, 0.95, 0.15, 1.0),   # lime jets (the CTA lime of the game)
}
# The older artist files name their slots differently; measured against the prefabs (the
# Dolphin dossier): accent = the DOMAIN panels (slot 0), BASE = body, windsheild = window,
# N = the engine bells (slot 3).
ALIASES = {
    "BASE": "Body", "Hull": "Body", "accent": "Domain", "Jet": "Engine", "windsheild": "Window",
    "N": "Engine", "Domain.001": "Domain", "BASE.001": "Body", "accent.001": "Domain", "windsheild.001": "Window",
    "N.001": "Engine",
}

SLOT_DOMAIN, SLOT_HULL, SLOT_WINDOW, SLOT_JET = 0, 1, 2, 3


def slot_names_for(vessel):
    """The FBX material names a vessel's live file uses, in slot order."""
    return {
        # measured from the shipped files (fbx_probe): Squirrel/Serpent/Sparrow use the generic set
        "Squirrel": ("Domain", "Body", "Window", "Engine"),
        "Serpent": ("Domain", "Body", "Window", "Engine"),
        "Sparrow": ("Domain", "Body", "Window", "Engine"),
    }.get(vessel, GENERIC)


def canonical_slot(name):
    """'accent.005' -> 'Domain', 'BASE.001' -> 'Body', 'N' -> 'Engine' ... for renders and reports."""
    import re
    base = re.sub(r"\.\d+$", "", name or "")
    return ALIASES.get(name, ALIASES.get(base, base))


def studio_material(name):
    """A Principled material in the studio colour for this slot (for renders only)."""
    canonical = canonical_slot(name)
    mat = bpy.data.materials.get(name) or bpy.data.materials.new(name)
    mat.use_nodes = True
    bsdf = mat.node_tree.nodes.get("Principled BSDF")
    col = STUDIO_COLOURS.get(canonical, (0.5, 0.5, 0.5, 1.0))
    if bsdf:
        bsdf.inputs["Base Color"].default_value = col
        bsdf.inputs["Roughness"].default_value = 0.35 if canonical != "Window" else 0.08
        bsdf.inputs["Metallic"].default_value = 0.15 if canonical == "Body" else 0.0
        if canonical == "Engine":
            bsdf.inputs["Emission Color"].default_value = col
            bsdf.inputs["Emission Strength"].default_value = 1.5
        if canonical == "Window":
            bsdf.inputs["Emission Color"].default_value = col
            bsdf.inputs["Emission Strength"].default_value = 0.6
    mat.diffuse_color = col
    return mat


def assign_studio_materials(obj, names=GENERIC):
    while len(obj.data.materials) < len(names):
        obj.data.materials.append(None)
    for i, n in enumerate(names):
        obj.data.materials[i] = studio_material(n)
    return obj


def slot_report(obj):
    """Polygon count per slot index, the number fbx_probe reports back."""
    counts = {}
    for p in obj.data.polygons:
        counts[p.material_index] = counts.get(p.material_index, 0) + 1
    return dict(sorted(counts.items()))
