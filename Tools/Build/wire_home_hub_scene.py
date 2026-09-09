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
    "preview": "the live toy window",
    "navigateButton": "Navigate",
    "backButton": "Back",
    "crystalClickHandler": "the freestyle toggle",
    "screenSwitcher": "the screen switcher",
}

# Optional by design, and reported rather than required. `categoryText` names the FUNDAMENTAL a
# toy changes (Pilot / World / Creation) - which the Toy Box GRID card already shows, and which
# ToyConfigureModal null-guards. The window's own authoring deleted the arcade `Header` this used
# to bind to, so demanding it back would be a gate arguing with the design; the tool accepts a
# label named Header, Category or Toy Category and binds whichever exists.
OPTIONAL_CONFIGURE_SLOTS = {
    "categoryText": "the fundamental it changes (optional - add a 'Category' label to show it)",
}

# The variants list, added after this window shipped with Navigate alone. These are reported
# rather than REQUIRED, and the trigger is the group itself: the scroll view they bind to is
# hand-authored UI, so on a checkout where it has not landed yet an empty group is the honest
# state and must not fail the gate. The moment any ONE of them is filled the designer has wired
# it, and a HALF-wired list is a defect rather than a pending item - the window would draw rows
# into nothing, or draw them with no way to commit a selection - so from then on the group is
# required in full. It arms itself; nobody has to remember to switch it on.
VARIANT_SLOTS = {
    "variantsRoot": "the variants scroll view",
    "variantContent": "the variants content",
    "variantCardPrefab": "the variant card template",
    "switchButton": "Switch",
}

SCRIPTS = {
    "MenuHubButton":        "Assets/_Scripts/UI/Elements/MenuHubButton.cs",
    "MenuAvailabilityView": "Assets/_Scripts/UI/Elements/MenuAvailabilityView.cs",
    "ToyboxModal":          "Assets/_Scripts/UI/Modals/ToyboxModal.cs",
    "ToyConfigureModal":    "Assets/_Scripts/UI/Modals/ToyConfigureModal.cs",
    "ToyboxCard":           "Assets/_Scripts/UI/Elements/ToyboxCard.cs",
    "ToyPreviewCamera":     "Assets/_Scripts/UI/Elements/ToyPreviewCamera.cs",
    "ToyVariantCard":       "Assets/_Scripts/UI/Elements/ToyVariantCard.cs",
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


def enum_blob(text):
    """Unity writes a `List<SomeEnum>` as a packed little-endian int32 hex blob, not a YAML list.

    `ActiveModalWindows: 01000000` is one entry with the value 1, and an empty list is an empty
    string. A `- 1` style regex reads every such list as empty, which would make this audit pass
    on exactly the scene it exists to catch.
    """
    text = (text or "").strip()
    if not text or len(text) % 8:
        return []
    return [int.from_bytes(bytes.fromhex(text[i:i + 8]), "little")
            for i in range(0, len(text), 8)]


def locate(basename):
    """The repo path of a script by file name - for the two components this audit names but the
    wiring tool only ever matches by TYPE NAME, so they are deliberately absent from SCRIPTS."""
    for base, _, files in os.walk(os.path.join(ROOT, "Assets/_Scripts")):
        if basename in files:
            return os.path.relpath(os.path.join(base, basename), ROOT)
    return None


SCRIPT_PATHS = {
    "WeeklyChallengePlayButton": locate("WeeklyChallengePlayButton.cs"),
    "ControllerButtonPress": locate("ControllerButtonPress.cs"),
}


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

    def subtree(self, root_go):
        """Every GameObject fileID at or under `root_go`."""
        self.component_reachable("0")          # builds the transform maps
        root_tf = self._go_tf.get(root_go)
        if not root_tf:
            return []
        kids = {}
        for tf, parent in self._tf_parent.items():
            kids.setdefault(parent, []).append(tf)
        out, stack = [], [root_tf]
        while stack:
            tf = stack.pop()
            if tf in self._tf_go:
                out.append(self._tf_go[tf])
            stack.extend(kids.get(tf, []))
        return out

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


def audit_group(sc, go_name, comp_id, slots, pending):
    """A group of slots that is required only once the designer has started on it.

    Returns the outstanding items. An entirely empty group is not outstanding: it appends one
    line to `pending`, which is printed as information and does not fail the check.
    """
    body = sc.docs[comp_id][1]
    filled, missing = 0, []
    for field, label in slots.items():
        m = re.search(r"^  %s: \{fileID: (-?\d+)" % re.escape(field), body, re.M)
        if not m:
            missing.append(f"{go_name}.{field}: field not serialized yet (script changed?)")
        elif m.group(1) == "0":
            missing.append(f"{go_name}.{field}: empty - needs {label}")
        else:
            filled += 1

    if filled == 0:
        pending.append(f"{go_name}: the variants list is not wired yet "
                       f"({len(slots)} slots) - author the scroll view, then run "
                       f"FrogletTools > Interface > Home Hub Wiring")
        return []
    return missing


def audit_inherited_arcade(sc):
    """The two arcade components the duplicated launch button brought with it.

    Neither is visible as an empty slot, and both are live: `WeeklyChallengePlayButton` writes
    `Button.interactable` from the weekly-challenge service, so it fights ToyConfigureModal for
    the same property and switches Navigate off whenever there is no valid challenge;
    `ControllerButtonPress` declared ARCADE_GAME_CONFIGURE, so a pad press inside the ARCADE's
    modal invoked THIS window's Navigate - a teleport and a freestyle entry from a window the
    player is not looking at. The wiring tool deletes the first and retargets the second; this is
    the half that says whether it stuck.
    """
    todo = []
    modal = sc.find_go("ToyboxGameConfigureModal")
    if not modal:
        return todo

    inside = sc.subtree(modal)
    want = MODAL_TYPES["ToyboxGameConfigureModal"]
    weekly = SCRIPT_PATHS.get("WeeklyChallengePlayButton")
    hints = SCRIPT_PATHS.get("ControllerButtonPress")
    weekly = guid_of(weekly) if weekly else None
    hints = guid_of(hints) if hints else None

    for go in inside:
        for comp in sc.components(go):
            body = sc.docs.get(comp, ("", ""))[1]
            if weekly and weekly in body:
                todo.append(f"{sc.name(go)}: still carries WeeklyChallengePlayButton - it writes "
                            f"Button.interactable and fights the toy window for Navigate")
            if hints and hints in body:
                m = re.search(r"^  ActiveModalWindows: ?(\S*)$", body, re.M)
                values = enum_blob(m.group(1) if m else "")
                if values != [want]:
                    todo.append(f"{sc.name(go)}: ControllerButtonPress answers to modal(s) "
                                f"{values or '<none>'}, not TOYBOX_CONFIGURE ({want}) - "
                                f"a pad press in another window fires this one")
    return todo


# What the tool authors on the toy window's type and layout. The BAND is the contract, not the
# numbers: every one of these labels carries content of no fixed length, so a fixed size is a
# promise the content cannot keep and it breaks by clipping. See Docs/HomeHub/ARCHITECTURE.md
# §5.4.1.  (path-under-the-modal, min, max, what it is)
# The numbers are author_toybox_layout.py's (which writes them) and HomeHubWiringWindow's (which
# writes the same ones from the editor) - halved from the first pass's 42-58 / 22-44 / 22-34 /
# 15-21, which read as a poster on a window whose whole left column is three labels.
TYPE_BANDS = [
    ("GameView/Game Name", 28.0, 36.0, "the toy's name"),
    ("Game Description", 16.0, 22.0, "the toy's description"),
    ("ToyVariantTemplate/GameTitle", 16.0, 22.0, "the variant name"),
    ("ToyVariantTemplate/GameDetail", 12.0, 14.0, "the variant detail line"),
]


def tmp_on(sc, go):
    """The TextMeshPro component on a GameObject, found by the fields it alone carries.

    Matched on `m_fontSizeMin` rather than on TMP's script GUID: the GUID is a package's and
    would be one more constant to keep true across an upgrade, while the field is the very thing
    being audited.
    """
    for cid in sc.components(go):
        t, c = sc.docs.get(cid, ("", ""))
        if t == "114" and "m_fontSizeMin:" in c:
            return cid
    return None


def go_at(sc, root_go, path):
    """A GameObject named by a '/'-separated path under `root_go`, by NAME at each step.

    Deliberately not a plain name search: this window carries two labels called `Game Name` (the
    designer built the variants column by duplicating the one beside it), so a search by name
    alone answers with whichever is earlier in the hierarchy - a fact about sibling order rather
    than about the labels.
    """
    names = path.split("/")
    for cand in sc.subtree(root_go):
        if sc.name(cand) != names[-1]:
            continue
        # Walk back up and check every named ancestor in turn.
        tf = sc._go_tf.get(cand)
        ok, i = True, len(names) - 2
        while i >= 0:
            tf = sc._tf_parent.get(tf)
            if not tf or sc.name(sc._tf_go.get(tf, "")) != names[i]:
                ok = False
                break
            i -= 1
        if ok:
            return cand
    return None


def audit_variants_layout(sc, tgc, pending):
    """The type scale and the one layout value that decides whether the list can scroll at all.

    Reported as PENDING rather than outstanding: these are values the editor tool authors, and a
    scene that has not been through it yet is un-run, not broken.
    """
    for path, lo, hi, label in TYPE_BANDS:
        go = go_at(sc, tgc, path)
        if not go:
            continue
        comp = tmp_on(sc, go)
        if not comp:
            continue
        body = sc.docs[comp][1]
        auto = re.search(r"^  m_enableAutoSizing: (\d+)", body, re.M)
        lo_m = re.search(r"^  m_fontSizeMin: ([\d.]+)", body, re.M)
        hi_m = re.search(r"^  m_fontSizeMax: ([\d.]+)", body, re.M)
        if not (auto and lo_m and hi_m):
            continue
        if auto.group(1) == "1" and abs(float(lo_m.group(1)) - lo) < 0.01 \
                and abs(float(hi_m.group(1)) - hi) < 0.01:
            continue
        pending.append(f"{path}: {label} is not on its {lo:.0f}-{hi:.0f} band "
                       f"(autosize={auto.group(1)}, {lo_m.group(1)}-{hi_m.group(1)})")

    # The one that is not a look question: with the fitter unconstrained the Content's height is
    # zero however many rows the grid lays into it, so every row past the viewport is clipped by
    # the mask - which also eats the press. Invisible AND unpressable, from one cause.
    for go in sc.subtree(tgc):
        if sc.name(go) != "Content":
            continue
        for cid in sc.components(go):
            t, c = sc.docs.get(cid, ("", ""))
            if t != "114" or "UnityEngine.UI.ContentSizeFitter" not in c:
                continue
            m = re.search(r"^  m_VerticalFit: (\d+)", c, re.M)
            if m and m.group(1) != "2":
                pending.append("Scroll View/Content: ContentSizeFitter vertical fit is "
                               "unconstrained - the variants list cannot scroll, and every row "
                               "past the viewport is clipped and unpressable")


def audit(sc):
    """Everything still to do, as a list of human-readable lines, plus what is merely pending."""
    todo, pending = [], []
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
            todo += audit_group(sc, "ToyConfigureModal", comp, VARIANT_SLOTS, pending)
            body = sc.docs[comp][1]
            for field, label in OPTIONAL_CONFIGURE_SLOTS.items():
                m = re.search(r"^  %s: \{fileID: (-?\d+)" % re.escape(field), body, re.M)
                if m and m.group(1) == "0":
                    pending.append(f"ToyConfigureModal.{field}: empty - {label}")
            audit_variants_layout(sc, tgc, pending)
            todo += audit_inherited_arcade(sc)

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

    return todo, pending


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--check", action="store_true", help="report only; non-zero while work remains")
    args = ap.parse_args()

    if not os.path.exists(SCENE):
        print(f"scene not found: {SCENE}")
        return 2

    sc = Scene(SCENE)
    todo, pending = audit(sc)

    for line in pending:
        print(f"  ~ {line}")

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
