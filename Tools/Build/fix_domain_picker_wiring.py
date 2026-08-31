#!/usr/bin/env python3
"""
Re-wire the domain picker in Menu_Main.

`ArcadeGameConfigureModal.prefab` wires `domainInfoItems` to its three real tiles
(Jade / Ruby / Gold). Menu_Main OVERRIDES all three entries to {fileID: 0}, and the
scene-local MaelstromGameConfigurationModal authors its list as three nulls. Both
listener-attach sites skip nulls:

    if (!item || !item.Button) continue;
    item.Button.onClick.AddListener(() => HandleDomainSelected(captured));

So no click ever reaches HandleDomainSelected, RequestSetDomain_ServerRpc is never
sent, and NetDomain keeps its initial value (Jade) no matter which tile is pressed.
Jade's authored palette is teal-and-blue, which is why it was reported as "always the
fallback blue domain".

This is the `Docs/GAMECANVAS.md` rule in its purest form: A SCENE OVERRIDE ALWAYS BEATS
THE PREFAB, so a correctly-wired prefab reference can be silently emptied by a scene and
nothing in the prefab says so.

Two different fixes, because the two modals differ:
  * arcade modal - a PREFAB INSTANCE, so DELETE the three null overrides and let the
    prefab's own (correct) wiring apply. Nothing to re-author, nothing to keep in sync.
  * Maelstrom modal - SCENE-LOCAL, no prefab to fall back on, so its three references
    are authored against the tiles that already exist under it.

    python3 Tools/Build/fix_domain_picker_wiring.py [--check]
"""
import re, sys, pathlib

SCENE = pathlib.Path("Assets/_Scenes/Menu_Main.unity")
MODAL_GUID = "1cc3c5b34dfd8074b9e2f436955062b3"
TILE_GUID  = "8377878a0e884704ba3c40235cb5fc37"     # DomainInfoData
# Measured from the scene: the Maelstrom modal's own tiles, in ActiveDomains order.
MAELSTROM_TILES = ["1381140838", "1909922841", "1447108470"]   # Jade, Ruby, Gold


def tile_domains(txt):
    """domain value per DomainInfoData component fileID, read from the scene itself."""
    out = {}
    for m in re.finditer(r'--- !u!114 &(-?\d+)\n(.*?)(?=\n--- !u!|\Z)', txt, re.S):
        if f"guid: {TILE_GUID}" not in m.group(2): continue
        d = re.search(r'^  domain: (-?\d+)$', m.group(2), re.M)
        out[m.group(1)] = int(d.group(1)) if d else 0
    return out


def fix(txt):
    doms = tile_domains(txt)
    # the tiles we are about to wire must exist and be the domains we think they are
    assert [doms.get(t) for t in MAELSTROM_TILES] == [1, 2, 4], \
        f"Maelstrom tiles are not Jade/Ruby/Gold: {[doms.get(t) for t in MAELSTROM_TILES]}"

    # --- 1. drop the three null overrides on the arcade modal instance ---------------
    pattern = re.compile(
        r'    - target: \{fileID: \d+, guid: ' + MODAL_GUID + r',\n'
        r'        type: 3\}\n'
        r"      propertyPath: 'domainInfoItems\.Array\.data\[[012]\]'\n"
        r'      value: \n'
        r'      objectReference: \{fileID: 0\}\n')
    txt, dropped = pattern.subn('', txt)

    # --- 2. author the scene-local Maelstrom modal's list ----------------------------
    wired = 0
    def wire(m):
        nonlocal wired
        wired += 1
        return "  domainInfoItems:\n" + "".join(f"  - {{fileID: {t}}}\n" for t in MAELSTROM_TILES)
    txt = re.sub(r'  domainInfoItems:\n(?:  - \{fileID: 0\}\n){3}', wire, txt)

    return txt, dropped, wired


def main():
    txt = SCENE.read_text()
    if "--check" in sys.argv:
        bad = len(re.findall(r"propertyPath: 'domainInfoItems\.Array\.data\[\d\]'\n      value: \n"
                             r"      objectReference: \{fileID: 0\}", txt))
        bad += len(re.findall(r'  domainInfoItems:\n(?:  - \{fileID: 0\}\n){3}', txt))
        print(f"domain picker: {bad} unwired list(s)/override(s) remaining"
              if bad else "domain picker: every domainInfoItems entry is wired")
        sys.exit(1 if bad else 0)

    new, dropped, wired = fix(txt)
    if dropped == 0 and wired == 0:
        print("already wired - nothing to do"); return

    # ---- validate before writing ----------------------------------------------------
    d = lambda t: re.findall(r'^--- !u!\d+ &(-?\d+)', t, re.M)
    assert d(new) == d(txt), "document set changed"
    refs_before = set(re.findall(r'\{fileID: (-?\d+)\}', txt))
    refs_after  = set(re.findall(r'\{fileID: (-?\d+)\}', new))
    assert not (refs_after - refs_before - set(MAELSTROM_TILES)), "unexpected new references"
    assert "domainInfoItems.Array.data" not in new, "a null override survived"
    assert not re.search(r'  domainInfoItems:\n(?:  - \{fileID: 0\}\n){3}', new), "a null list survived"
    for t in MAELSTROM_TILES:
        assert new.count(f"- {{fileID: {t}}}") >= 1, f"tile {t} not referenced"

    SCENE.write_text(new)
    print(f"Menu_Main: dropped {dropped} null override(s) (arcade modal falls back to the "
          f"prefab's wiring), authored {wired} scene-local list(s)")


if __name__ == "__main__":
    main()
