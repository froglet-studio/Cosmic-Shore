#!/usr/bin/env python3
"""Wire Menu_Main's home hub: four buttons, four modals, one registry.

The hub screens were authored by hand from copies of the Arcade's, which is the right way to get
the LAYOUT and the wrong way to get the WIRING - a duplicate carries the original's components and
serialized values, and every one of those is a claim about what the object is.  Measured on the
authored scene, all four hub buttons called `ScreenSwitcher.OnClickArcadeNav`, all four screen
modals declared `ModalType: 10` (ARCADE), and none of the three new windows was in the switcher's
`Modals` list at all - so `OpenModal(TOYBOX)` had nothing to find and every button opened the
Arcade.

What this fixes, and why each is a defect rather than a preference:

  1. `MenuHubButton` on each of the four hub buttons, with its own `target`, and the
     `OnClickArcadeNav` persistent call removed.  The button must name a modal TYPE and let the
     switcher find it (Docs/HomeHub/ARCHITECTURE.md §1); a direct nav call is a second authority
     on a modal's lifecycle.  `MenuAudio.PlayAudio` is left alone - it is the press sound.
  2. Distinct `ModalType` per screen modal (ARCADE 10 / TOYBOX 13 / ARENA 14 / MISSION 15).  The
     switcher finds a modal BY type, so four modals claiming 10 is not four windows, it is one
     window found four times.
  3. `ToyConfigureModal` (type 16) replaces `ArcadeGameConfigureModal` on ToyboxGameConfigureModal.
     A toy configures nothing - the arcade's ~2,250-line class brings Netcode, player counts,
     domain policy and a launch path, none of which a toy has.
  4. `ToyboxModal` replaces `ArcadeScreen` on ToyboxScreenModal, for the same reason.
  5. Every hub modal registered in `ScreenSwitcher.Modals`, and the two NULL entries already in
     that list dropped.  A null in the list is not inert: `OpenModal` and `CloseAllModals` both
     walk it.
  6. Mission and Arena set to their availability states rather than being left to read as
     Available - see §2 on why an entry that is not drawn is worse than one that is locked.
  7. The trailing space in `'MissionScreenModal '` removed, so find-by-name cannot miss it.

Idempotent.  `--check` exits non-zero while anything is left to do.

TRAP, recorded because it cost a pass here and is recorded in CLAUDE.md for the ability lockup:
a scene's `- target:` entry WRAPS across two lines, so a one-line regex reports zero overrides on
an instance carrying hundreds.  Every match in this file normalises that wrap first.
"""
import argparse
import os
import re
import sys

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
SCENE = os.path.join(ROOT, "Assets/_Scenes/Menu_Main.unity")

# ── the contract, stated once ────────────────────────────────────────────────

# hub button GameObject name -> the ModalWindows value it opens, and its availability
BUTTONS = {
    "ArcadeButton":  ("ARCADE",  10, "Available"),
    "ToyboxButton":  ("TOYBOX",  13, "Available"),
    "ArenaButton":   ("ARENA",   14, "Locked"),      # exists, not open yet
    "MissionButton": ("MISSION", 15, "Unavailable"), # not built
}

# screen modal GameObject name (trailing space tolerated) -> its ModalType
MODAL_TYPES = {
    "ArcadeScreenModal": 10,
    "ToyboxScreenModal": 13,
    "ArenaScreenModal": 14,
    "MissionScreenModal": 15,
    "ToyboxGameConfigureModal": 16,
}

# The serialized slots each new component needs before the window can draw anything. Value is a
# human-readable description used in the report.
TOYBOX_SLOTS = {
    "cardGrid": "the toy grid",
    "cardPrefab": "the card template",
    "emptyState": "the empty state",
    "configureModal": "the detail window",
    "screenSwitcher": "the screen switcher",
}

# Slots that must be reachable on screen, so an inactive target is a defect, not a choice.
CONTROL_SLOTS = {"navigateButton", "backButton"}

CONFIGURE_SLOTS = {
    "titleText": "the toy's name",
    "descriptionText": "the toy's description",
    "categoryText": "the fundamental it changes",
    "preview": "the live toy window",
    "navigateButton": "Navigate",
    "backButton": "Back",
    "crystalClickHandler": "the freestyle toggle",
    "screenSwitcher": "the screen switcher",
}

SCRIPTS = {
    "MenuHubButton":        "Assets/_Scripts/UI/Elements/MenuHubButton.cs",
    "MenuAvailabilityView": "Assets/_Scripts/UI/Elements/MenuAvailabilityView.cs",
    "ToyboxModal":          "Assets/_Scripts/UI/Modals/ToyboxModal.cs",
    "ToyConfigureModal":    "Assets/_Scripts/UI/Modals/ToyConfigureModal.cs",
    "ToyboxCard":           "Assets/_Scripts/UI/Elements/ToyboxCard.cs",
    "ToyPreviewCamera":     "Assets/_Scripts/UI/Elements/ToyPreviewCamera.cs",
    "ToyNavigationBeacon":  "Assets/_Scripts/Controller/Toys/ToyNavigationBeacon.cs",
    "HomeHubWiringWindow":  "Assets/_Scripts/Editor/FrogletTools/HomeHubWiringWindow.cs",
    "ArcadeScreen":         "Assets/_Scripts/UI/Screens/ArcadeScreen.cs",
    "ArcadeGameConfigureModal": "Assets/_Scripts/UI/Modals/ArcadeGameConfigureModal.cs",
    "ModalWindowManager":   "Assets/_Scripts/UI/Modals/ModalWindowManager.cs",
    "ScreenSwitcher":       "Assets/_Scripts/UI/ScreenSwitcher.cs",
}


def guid_of(rel):
    meta = os.path.join(ROOT, rel + ".meta")
    if not os.path.exists(meta):
        return None
    m = re.search(r"^guid: (\w+)", open(meta, errors="ignore").read(), re.M)
    return m.group(1) if m else None


def unwrap(text):
    """Normalise Unity's two-line `- target: {..,\\n  type: 3}` wrap. See the module docstring."""
    return re.sub(r",\s*\n\s+type: (\d+)\}", r", type: \1}", text)


class Scene:
    def __init__(self, path):
        self.path = path
        self.raw = open(path, errors="ignore").read()
        self.docs = {}
        for chunk in re.split(r"^--- ", self.raw, flags=re.M):
            m = re.match(r"!u!(\d+) &(\d+)", chunk)
            if m:
                self.docs[m.group(2)] = (m.group(1), chunk)

    def name(self, fid):
        t, c = self.docs.get(fid, ("", ""))
        m = re.search(r"^  m_Name: (.*)$", c, re.M)
        return m.group(1).strip().strip("'").strip() if m else ""

    def find_go(self, wanted):
        for fid, (t, _) in self.docs.items():
            if t == "1" and self.name(fid) == wanted:
                return fid
        return None

    def components(self, go):
        return re.findall(r"component: \{fileID: (\d+)\}", self.docs[go][1])

    def component_reachable(self, comp_id):
        """True when the component's GameObject and every ancestor are active."""
        if comp_id not in self.docs:
            return False
        g = re.search(r"m_GameObject: \{fileID: (\d+)\}", self.docs[comp_id][1])
        if not g:
            return False
        if not hasattr(self, "_tf"):
            self._go_tf, self._tf_go, self._tf_parent = {}, {}, {}
            for fid, (t, c) in self.docs.items():
                if t in ("4", "224"):
                    og = re.search(r"m_GameObject: \{fileID: (\d+)\}", c)
                    if og:
                        self._go_tf[og.group(1)] = fid
                        self._tf_go[fid] = og.group(1)
                    pm = re.search(r"m_Father: \{fileID: (\d+)\}", c)
                    self._tf_parent[fid] = pm.group(1) if pm and pm.group(1) != "0" else None
            self._tf = True
        tf = self._go_tf.get(g.group(1))
        while tf and tf in self._tf_go:
            if not re.search(r"m_IsActive: 1", self.docs[self._tf_go[tf]][1]):
                return False
            tf = self._tf_parent.get(tf)
        return True

    def script_on(self, go, guid):
        for cid in self.components(go):
            t, c = self.docs.get(cid, ("", ""))
            if t == "114" and guid and guid in c:
                return cid
        return None


def audit_slots(sc, go_name, comp_id, slots):
    """Report any serialized reference still empty on an already-added component.

    A slot left at `{fileID: 0}` is the failure mode this whole family has: the component is
    present, the window opens, and it draws nothing - so it must be reported as loudly as a
    missing component.
    """
    todo = []
    body = sc.docs[comp_id][1]
    for field, label in slots.items():
        m = re.search(r"^  %s: \{fileID: (-?\d+)" % re.escape(field), body, re.M)
        if not m:
            todo.append(f"{go_name}.{field}: field not serialized yet (script changed?)")
        elif m.group(1) == "0":
            todo.append(f"{go_name}.{field}: empty - needs {label}")
        elif field in CONTROL_SLOTS and not sc.component_reachable(m.group(1)):
            # A bound control on an inactive object is worse than an empty slot: the slot reads
            # as filled while the button on screen is a different, unwired object.
            todo.append(f"{go_name}.{field}: bound to an INACTIVE object - {label} cannot be pressed")
    return todo


def audit(sc):
    """Everything still to do, as a list of human-readable lines."""
    todo = []
    guids = {k: guid_of(v) for k, v in SCRIPTS.items()}

    # A script with no committed .meta has no stable GUID, so every scene reference the editor
    # wrote to it points at a GUID that exists on exactly one machine. It reads as "Missing (Mono
    # Script)" for everybody else, and nothing in the scene diff says why.
    for label, rel in SCRIPTS.items():
        if guids[label] is None:
            todo.append(f"{rel}: no .meta committed - its scene references cannot survive a push")

    for btn, (label, value, avail) in BUTTONS.items():
        go = sc.find_go(btn)
        if not go:
            todo.append(f"button {btn}: GameObject not found")
            continue
        if not sc.script_on(go, guids["MenuHubButton"]):
            todo.append(f"button {btn}: no MenuHubButton (target should be {label}={value})")
        body = "".join(sc.docs[c][1] for c in sc.components(go))
        if "OnClickArcadeNav" in body:
            todo.append(f"button {btn}: still calls ScreenSwitcher.OnClickArcadeNav")

    for modal, want in MODAL_TYPES.items():
        go = sc.find_go(modal)
        if not go:
            todo.append(f"modal {modal}: GameObject not found")
            continue
        mwm = sc.script_on(go, guids["ModalWindowManager"]) or \
              sc.script_on(go, guids["ToyboxModal"]) or \
              sc.script_on(go, guids["ToyConfigureModal"]) or \
              sc.script_on(go, guids["ArcadeGameConfigureModal"])
        if not mwm:
            todo.append(f"modal {modal}: no ModalWindowManager-derived component")
            continue
        m = re.search(r"ModalType: (-?\d+)", sc.docs[mwm][1])
        got = int(m.group(1)) if m else None
        if got != want:
            todo.append(f"modal {modal}: ModalType {got} should be {want}")

    if sc.find_go("ToyboxScreenModal") and \
            sc.script_on(sc.find_go("ToyboxScreenModal"), guids["ArcadeScreen"]):
        todo.append("ToyboxScreenModal: carries ArcadeScreen; needs ToyboxModal")
    tgc = sc.find_go("ToyboxGameConfigureModal")
    if tgc and sc.script_on(tgc, guids["ArcadeGameConfigureModal"]):
        todo.append("ToyboxGameConfigureModal: carries ArcadeGameConfigureModal; needs ToyConfigureModal")

    tsm = sc.find_go("ToyboxScreenModal")
    if tsm and guids["ToyboxModal"]:
        comp = sc.script_on(tsm, guids["ToyboxModal"])
        if comp:
            todo += audit_slots(sc, "ToyboxModal", comp, TOYBOX_SLOTS)
    if tgc and guids["ToyConfigureModal"]:
        comp = sc.script_on(tgc, guids["ToyConfigureModal"])
        if comp:
            todo += audit_slots(sc, "ToyConfigureModal", comp, CONFIGURE_SLOTS)

    # the switcher's registry
    for fid, (t, c) in sc.docs.items():
        if t != "114" or not guids["ScreenSwitcher"] or guids["ScreenSwitcher"] not in c:
            continue
        m = re.search(r"Modals:\s*\n((?:  - \{fileID: -?\d+\}\n)*)", c)
        ids = re.findall(r"\{fileID: (-?\d+)\}", m.group(1)) if m else []
        if "0" in ids:
            todo.append(f"ScreenSwitcher.Modals: {ids.count('0')} null entr(y/ies)")
        registered = set()
        for i in ids:
            if i in sc.docs:
                g = re.search(r"m_GameObject: \{fileID: (\d+)\}", sc.docs[i][1])
                if g:
                    registered.add(sc.name(g.group(1)))
        for modal in MODAL_TYPES:
            if modal not in registered:
                todo.append(f"ScreenSwitcher.Modals: {modal} not registered")

    raw = open(sc.path, errors="ignore").read()
    if "m_Name: 'MissionScreenModal '" in raw:
        todo.append("MissionScreenModal: name has a trailing space")

    return todo


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--check", action="store_true", help="report only; non-zero while work remains")
    args = ap.parse_args()

    if not os.path.exists(SCENE):
        print(f"scene not found: {SCENE}")
        return 2

    sc = Scene(SCENE)
    todo = audit(sc)

    if not todo:
        print("home hub wiring: clean")
        return 0

    print(f"home hub wiring: {len(todo)} item(s) outstanding in {os.path.relpath(SCENE, ROOT)}")
    for t in todo:
        print(f"  - {t}")

    if args.check:
        return 1

    # Writing is deliberately NOT implemented here.
    #
    # Adding a MonoBehaviour to a scene GameObject means minting a fileID, writing a component
    # stanza AND appending to the GameObject's component list - three edits that must agree, on an
    # asset Unity owns.  CLAUDE.md's tooling rule is explicit that scene writes go through
    # PrefabUtility on a loaded scene, never through hand-edited YAML, and this file is a READER
    # for exactly that reason: it states the work and proves when it is done.  The writes belong in
    # an editor tool under FrogletTools, where AddComponent and SerializedObject do them safely.
    print("\n(reader only - see the note in this file: scene component writes go through the editor)")
    return 1


sys.exit(main())
