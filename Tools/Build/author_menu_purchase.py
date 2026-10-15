#!/usr/bin/env python3
"""
Author ITEM 10 of the style-B menu redesign - the PURCHASE CONFIRMATION window - as a prefab and mount
it in Menu_Main's PurchaseItemConfirmationModal (Docs/MENU_REDESIGN_B.md section 19; the approved
PurchaseConfirm board, drawn blind under decision D4: every buy is refused before this window opens
while SO_CommerceAvailability.catalogPurchase is Locked, so it can only be seen by unlocking that).

    Assets/_Prefabs/UI Elements/Main Menu Screens/Purchase/PurchaseConfirmView.prefab
        The dim; the balance pills (crystals, tickets) top right; the window: "Confirm", the spend line
        ("Spend [crystal] 400 to unlock Manta?"), the balance bar (what you keep in Light, the spend in
        Gold) with "You keep 850" / "Spend 400 of 1,250", the item tile (captain / game / ticket art);
        SPEND 400 (Purchase) and CANCEL (Cancel) on the window's bottom edge.

    Assets/_Scenes/Menu_Main.unity (PurchaseItemConfirmationModal, a scene object)
        Content's old Border and Content List subtrees are deleted (with the ControllerButtonPress that
        closed the window on the same B press as the modal's own B handler); one PurchaseConfirmView
        instance is mounted under Content, which stretches. The PurchaseConfirmationModal component keeps
        its fileID - StoreScreen and HangarCaptainsView still resolve - and its fields, and the
        IconEmitter's, are re-pointed into the view; the buttons are wired in code (wireButtonsInCode),
        because a prefab's button cannot carry a persistent call to a scene component.

STANDING DOWN: once the old subtrees are gone the mount is re-derived in place; a re-run is a no-op
(--check).

    python3 Tools/Build/author_menu_purchase.py              # write
    python3 Tools/Build/author_menu_purchase.py --check      # fail if any output drifted
    python3 Tools/Build/author_menu_purchase.py --self-test  # planted defects must all be caught
"""

import argparse
import importlib.util
import os
import re
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
_spec = importlib.util.spec_from_file_location("author_menu_profile_window", os.path.join(HERE, "author_menu_profile_window.py"))
W = importlib.util.module_from_spec(_spec)
_spec.loader.exec_module(W)

P, C, A, K, T, H, M = W.P, W.C, W.A, W.K, W.T, W.H, W.M
REPO = K.REPO
SCENE = "Assets/_Scenes/Menu_Main.unity"
OUT_DIR = "Assets/_Prefabs/UI Elements/Main Menu Screens/Purchase"
VIEW = "PurchaseConfirmView"
N, STRETCH, R, tok = K.Node, K.STRETCH, K.rect, K.tok
TL, TR, ref, mono = C.TL, C.TR, C.ref, C.mono
label, le = H.label, H.le
image_node = A.image_node

S = dict(modal=C.script("PurchaseConfirmationModal"), emitter=C.script("IconEmitter"), store=C.script("StoreScreen"),
         button=K.kit_script("KitButton"))
TMP, IMG = K.TMP_UGUI, K.IMAGE
TICKET_SPRITE = "7834bfe433c51904599575f6ec1786a3"       # the ticket art the old window used

# Menu_Main, measured 2026-10-15.
SC = dict(root_go="3411776012154435581", modal="5393631321266948390", emitter="5968134327380731010",
          store="5340227408031961627",
          content_go="1300031345922158688", content_rt="5236562998019744391",
          retired={"4916061089592537233": "Border", "3461868959933754924": "Content List"})

WIN_W, WIN_H, PAD = 1000, 360, 48


def asset_guid(name):
    return P.asset_guid(name)


def prefab_path():
    return f"{OUT_DIR}/{VIEW}.prefab"


def F(key):
    return K.FileIds(VIEW)(key)


# --------------------------------------------------------------------------------------------
# The view
# --------------------------------------------------------------------------------------------

def sprite(guid):
    return f"{{fileID: 21300000, guid: {guid}, type: 3}}"


def glyph(parent, key, name, size, token, spr, active=True):
    n = parent.child(N(key, name, R(size=(size, size)), active=active))
    n.add("cr", 222, K.canvas_renderer()) \
        .add("img", 114, K.image(tok(token), spr, preserve=True)).add("le", 114, le(pw=size, ph=size))
    return n


def pill(parent, key, name, spr, token, value_key):
    p = parent.child(N(key, name, R(size=(0, 48))))
    H.framed(p, key, 10, "SurfaceBlack", "TextLight")
    p.add("hlg", 114, K.hv_layout(False, (18, 18, 10, 10), 12, 4, (1, 1), (0, 0))).add("fit", 114, K.fitter(2, 0))
    glyph(p, f"{key}/Glyph", "Glyph", 20, token, spr)
    v = p.child(N(f"{key}/{value_key}", value_key, R(size=(0, 28))))
    label(v, "0", "Data", "TextLight")
    return p


def build_view():
    root = N("root", VIEW, STRETCH)
    image_node(root, "Dim", "Dim", STRETCH, "SurfaceBlack", alpha=0.82, raycast=True)

    bal = root.child(N("Balances", "Balances", R((1, 1), None, (-40, -108), (420, 48), (1, 1))))
    bal.add("hlg", 114, K.hv_layout(False, (0, 0, 0, 0), 12, 5, (1, 1), (0, 0)))
    pill(bal, "Balances/Crystals", "Crystals", K.chrome_sprite("chrome_crystal"), "Purchase", "CrystalBalanceText")
    pill(bal, "Balances/Tickets", "Tickets", sprite(TICKET_SPRITE), "TextLight", "TicketBalanceText")

    win = root.child(N("Window", "Window", R((0.5, 0.5), None, (0, 30), (WIN_W, WIN_H))))
    fill = win.child(N("Window/Fill", "Fill", STRETCH))
    fill.add("cr", 222, K.canvas_renderer()) \
        .add("img", 114, K.image(tok("SurfaceBlack"), K.kit_sprite("kit_sliver14_fill"), True, raycast=True)) \
        .add("th", 114, H.themed("SurfaceBlack"))
    bord = win.child(N("Window/Border", "Border", STRETCH))
    bord.add("cr", 222, K.canvas_renderer()) \
        .add("img", 114, K.image(tok("TextLight"), K.kit_sprite("kit_sliver14_line1"), True)).add("th", 114, H.themed("TextLight"))
    M.bracket_nodes(win, "Window/Bracket", inset=14)

    label(win.child(N("Window/Eyebrow", "Eyebrow", TL(PAD, 64, 400, 18))), "Confirm", "Label", "NeutralLightest")
    line = win.child(N("Window/Line", "SpendLine", TL(PAD, 90, 640, 44)))
    line.add("hlg", 114, K.hv_layout(False, (0, 0, 0, 0), 10, 3, (1, 1), (0, 0)))
    label(line.child(N("Window/Line/Spend", "Spend", R(size=(0, 44)))), "Spend", "H2", "TextLight")
    glyph(line, "Window/Line/Glyph", "CrystalIcon", 26, "Purchase", K.chrome_sprite("chrome_crystal"))
    label(line.child(N("Window/Line/Price", "PriceLabel", R(size=(0, 44)))), "400", "H2", "Purchase")
    label(line.child(N("Window/Line/Unlock", "UnlockText", R(size=(0, 44)))), "to unlock Manta?", "H2", "TextLight")

    bar = win.child(N("Window/Bar", "BalanceBar", TL(PAD, 170, 640, 8)))
    bar.add("cr", 222, K.canvas_renderer()).add("img", 114, K.image(tok("SurfaceDark"))).add("th", 114, H.themed("SurfaceDark"))
    keep = bar.child(N("Window/Bar/Keep", "Keep", R((0, 0), (0.68, 1), (0, 0), (0, 0), (0, 0.5))))
    keep.add("cr", 222, K.canvas_renderer()).add("img", 114, K.image(tok("TextLight"))).add("th", 114, H.themed("TextLight"))
    spend = bar.child(N("Window/Bar/Spend", "Spend", R((0.68, 0), (1, 1), (0, 0), (0, 0), (0, 0.5))))
    spend.add("cr", 222, K.canvas_renderer()).add("img", 114, K.image(tok("Purchase"))).add("th", 114, H.themed("Purchase"))
    label(win.child(N("Window/Keep", "KeepText", TL(PAD, 188, 320, 22))), "You keep 850", "BodySmall", "NeutralLightest")
    label(win.child(N("Window/SpendOf", "SpendOfText", TL(PAD + 320, 188, 320, 22))), "Spend 400 of 1,250", "BodySmall",
          "Purchase", align="right")

    tile = win.child(N("Window/Tile", "ItemTile", TR(PAD, 80, 200, 200)))
    tile.add("cr", 222, K.canvas_renderer()) \
        .add("img", 114, K.image(tok("SurfaceDark"), K.kit_sprite("kit_sliver10_fill"), True)).add("th", 114, H.themed("SurfaceDark"))
    for key, spr, active in (("CaptainImage", "{fileID: 0}", True), ("GameImage", "{fileID: 0}", False),
                             ("TicketImage", sprite(TICKET_SPRITE), False)):
        n = tile.child(N(f"Window/Tile/{key}", key, R(size=(150, 150)), active=active))
        n.add("cr", 222, K.canvas_renderer()).add("img", 114, K.image(K.WHITE, spr, preserve=True))

    bw, bh = K.button_size("SPEND 1,250")
    cw, _ = K.button_size("CANCEL")
    row = win.child(N("Window/Buttons", "Buttons", R((0, 0), None, (PAD, 0), (bw + 16 + cw, bh), (0, 0.5))))
    row.add("hlg", 114, K.hv_layout(False, (0, 0, 0, 0), 16, 3, (0, 0), (0, 0)))
    for key, variant, cap, w in (("Confirm", "Purchase", "SPEND 400", bw), ("Cancel", "Cancel", "CANCEL", cw)):
        holder = row.child(N(f"Window/Buttons/{key}", key, R((0, 0.5), None, (0, 0), (w, bh), (0, 0.5))))
        holder.add("le", 114, le(pw=w, ph=bh))
        holder.children.append(P.Inst(f"{key}Button", "KitButton", f"{key}Button", STRETCH,
                                      K.button_mods("KitButton", variant, True, cap)))
    return root


def prefab_outputs():
    return {prefab_path(): P.prefab_text(build_view(), VIEW),
            prefab_path() + ".meta": K.PREFAB_META.format(g=asset_guid(VIEW)),
            OUT_DIR + ".meta": K.FOLDER_META.format(g=asset_guid("folder/Purchase"))}


# --------------------------------------------------------------------------------------------
# Menu_Main: the mount and the re-pointing
# --------------------------------------------------------------------------------------------

def scene_ctx():
    return P.Ctx("Menu_Main/PurchaseConfirm")


def pids():
    ctx = scene_ctx()
    return str(ctx.fid("View#inst")), str(ctx.fid("View#stub"))


def nested(inst_key, src_key):
    """A view-level fileID for an object inside one of the view's KitButton instances."""
    return C.derived(K.source_fid("KitButton", src_key), F(f"{inst_key}#inst"))


# field -> (view-level fileID, stub class, script guid or None)
def modal_refs():
    return {
        "PriceLabel": (F("Window/Line/Price#tmp"), 114, TMP),
        "UnlockText": (F("Window/Line/Unlock#tmp"), 114, TMP),
        "CrystalBalanceText": (F("Balances/Crystals/CrystalBalanceText#tmp"), 114, TMP),
        "TicketBalanceText": (F("Balances/Tickets/TicketBalanceText#tmp"), 114, TMP),
        "ConfirmButton": (nested("ConfirmButton", "root#button"), 114, S["button"]),
        "CaptainImage": (F("Window/Tile/CaptainImage#img"), 114, IMG),
        "GameImage": (F("Window/Tile/GameImage#img"), 114, IMG),
        "TicketImage": (F("Window/Tile/TicketImage#img"), 114, IMG),
        "keepText": (F("Window/Keep#tmp"), 114, TMP),
        "spendOfText": (F("Window/SpendOf#tmp"), 114, TMP),
        "keepFill": (F("Window/Bar/Keep#rt"), 224, None),
        "spendFill": (F("Window/Bar/Spend#rt"), 224, None),
        "confirmLabel": (nested("ConfirmButton", "Label#tmp"), 114, TMP),
        "cancelButton": (nested("CancelButton", "root#button"), 114, S["button"]),
    }


def emitter_refs():
    return {
        "Container": (F("root#rt"), 224, None),
        "Source": (F("Balances/Crystals/Glyph#img"), 114, IMG),
        "Target": (F("Window/Line/Glyph#img"), 114, IMG),
        "ImageTemplate": (F("Balances/Crystals/Glyph#img"), 114, IMG),
    }


def stub(src, cls, script):
    inst, _ = pids()
    fid = str(C.derived(src, inst))
    if cls == 114:
        return fid, T.stub_mono(fid, src, asset_guid(VIEW), inst, script)
    return fid, (f"--- !u!224 &{fid} stripped\nRectTransform:\n  m_CorrespondingSourceObject: {K.ref(src, asset_guid(VIEW))}\n"
                 f"  m_PrefabInstance: {{fileID: {inst}}}\n  m_PrefabAsset: {{fileID: 0}}\n")


def set_or_add(doc, field, value, after):
    if re.search(r"^  " + re.escape(field) + r":", doc, re.M):
        return C.field_set(doc, field, value)
    pat = re.compile(r"^(  " + re.escape(after) + r":.*\n)", re.M)
    assert len(pat.findall(doc)) == 1, f"anchor field {after} not found"
    return pat.sub(lambda m: m.group(1) + f"  {field}: {value}\n", doc)


def scene_transform(text_):
    sc = T.Scene(text_)
    assert sc.field(SC["root_go"], "m_Name") == "PurchaseItemConfirmationModal", "the purchase window moved"
    assert sc.script(SC["modal"]) == S["modal"], "PurchaseConfirmationModal moved"
    assert sc.script(SC["emitter"]) == S["emitter"], "the window's IconEmitter moved"
    assert sc.field(SC["content_go"], "m_Name") == "Content" and sc.rect_of(SC["content_go"]) == SC["content_rt"], \
        "the window's Content moved"
    for go, name in SC["retired"].items():
        if sc.has(go):
            assert sc.field(go, "m_Name") == name, f"retired root {go} is no longer {name}"
            for fid in dict.fromkeys(C.subtree(sc, go)):
                if sc.has(fid):
                    sc.remove(fid)
    inst, vstub = pids()
    stubs = {}
    for refs in (modal_refs(), emitter_refs()):
        for field, (src, cls, script) in refs.items():
            fid, doc = stub(src, cls, script)
            stubs[fid] = doc
    for fid in [inst, vstub, *stubs]:
        if sc.has(fid):
            sc.remove(fid)
    out = []
    K.emit_instance(scene_ctx(), K.Instance("View", VIEW, asset_guid(VIEW), VIEW, STRETCH, ()), SC["content_rt"], out)
    out += list(stubs.values())
    anchor = SC["content_rt"]
    for d in (K.unity_empty_scalars(d) for d in out):
        sc.insert_after(anchor, T.doc_fid(d), d)
        anchor = T.doc_fid(d)
    sc.replace(SC["content_rt"], C.stretch_rt(T.set_block(sc.docs[SC["content_rt"]], "m_Children",
                                                          K.refs_list("m_Children", [vstub]))))
    md = sc.docs[SC["modal"]]
    prev = "TicketImage"
    for field, (src, cls, script) in modal_refs().items():
        md = set_or_add(md, field, ref(stub(src, cls, script)[0]), prev)
        prev = field
    md = set_or_add(md, "wireButtonsInCode", "1", prev)
    sc.replace(SC["modal"], md)
    ed = sc.docs[SC["emitter"]]
    for field, (src, cls, script) in emitter_refs().items():
        ed = C.field_set(ed, field, ref(stub(src, cls, script)[0]))
    sc.replace(SC["emitter"], ed)
    # The (disabled) StoreScreen hands the confirm button to its cards, which enable / disable it.
    assert sc.script(SC["store"]) == S["store"], "StoreScreen moved"
    src, cls, script = modal_refs()["ConfirmButton"]
    sc.replace(SC["store"], C.field_set(sc.docs[SC["store"]], "PurchaseConfirmationButton", ref(stub(src, cls, script)[0])))
    return sc.text()


def outputs():
    files = prefab_outputs()
    files[SCENE] = scene_transform(open(os.path.join(REPO, SCENE), encoding="utf-8").read())
    return files


# --------------------------------------------------------------------------------------------
# Validation
# --------------------------------------------------------------------------------------------

def validate(files):
    guids = T.known_guids() | P.PACKAGE
    minted = {re.search(r"^guid: (\w+)$", t, re.M).group(1) for p, t in files.items() if p.endswith(".meta")}
    view = files[prefab_path()]
    A.check_file(prefab_path(), view, guids, minted)
    for key in ("Window/Line/Price#tmp", "Window/Keep#tmp", "Window/SpendOf#tmp", "Window/Tile/TicketImage#img",
                "Balances/Crystals/Glyph#img", "Window/Bar/Keep#rt", "Window/Bar/Spend#rt"):
        assert A.doc_of(view, F(key))[1], f"PurchaseConfirmView: {key} is missing"
    assert view.count(f"m_SourcePrefab: {{fileID: 100100000, guid: {C.prefab_guid('KitButton')}") == 2, \
        "PurchaseConfirmView needs exactly two KitButtons (Spend, Cancel)"

    sc_text = files[SCENE]
    sc = T.Scene(sc_text)
    for go in SC["retired"]:
        assert not sc.has(go), f"the old {SC['retired'][go]} subtree survives"
    assert sc_text.count(f"m_SourcePrefab: {{fileID: 100100000, guid: {asset_guid(VIEW)}") == 1, \
        "Menu_Main must mount exactly one PurchaseConfirmView"
    md = sc.docs[SC["modal"]]
    for field, (src, cls, script) in modal_refs().items():
        C.wired(sc_text, md, field, script if cls == 114 else 224, "PurchaseConfirmationModal")
        assert re.search(r"^  %s: \{fileID: %s\}" % (field, stub(src, cls, script)[0]), md, re.M), \
            f"PurchaseConfirmationModal.{field} does not point into the view"
    assert "  wireButtonsInCode: 1\n" in md, "the view's buttons carry no persistent call; wireButtonsInCode must be on"
    ed = sc.docs[SC["emitter"]]
    for field, (src, cls, script) in emitter_refs().items():
        C.wired(sc_text, ed, field, script if cls == 114 else 224, "IconEmitter")
    C.wired(sc_text, sc.docs[SC["store"]], "PurchaseConfirmationButton", S["button"], "StoreScreen")
    anchors = set(re.findall(r"^--- !u!\d+ &(-?\d+)", sc_text, re.M)) | {"0"}
    for m in re.finditer(r"\{fileID: (-?\d+)\}", sc_text):
        assert m.group(1) in anchors, f"Menu_Main: dangling local fileID {m.group(1)}"


def drift(files):
    return [p for p, t in files.items()
            if not os.path.exists(os.path.join(REPO, p)) or open(os.path.join(REPO, p), encoding="utf-8").read() != t]


def self_test(files):
    def expect(label_, mutate):
        bad = dict(files)
        mutate(bad)
        try:
            validate(bad)
        except AssertionError as e:
            print(f"  caught: {label_} ({e})")
            return True
        print(f"  MISSED: {label_}")
        return False
    price_stub = stub(*modal_refs()["PriceLabel"])[0]
    cases = [
        ("buttons left to the inspector", lambda f: f.__setitem__(SCENE, f[SCENE].replace("  wireButtonsInCode: 1\n", "  wireButtonsInCode: 0\n"))),
        ("the price label unwired", lambda f: f.__setitem__(SCENE, f[SCENE].replace(
            f"  PriceLabel: {{fileID: {price_stub}}}\n", "  PriceLabel: {fileID: 0}\n"))),
        ("the emitter left on a deleted object", lambda f: f.__setitem__(SCENE, re.sub(
            r"(  Target: )\{fileID: -?\d+\}", r"\1{fileID: 7754888825117220970}", f[SCENE], count=1))),
        ("a missing Cancel button", lambda f: f.__setitem__(prefab_path(), f[prefab_path()].replace(
            f"m_SourcePrefab: {{fileID: 100100000, guid: {C.prefab_guid('KitButton')}", "m_SourcePrefab: {fileID: 0", 1))),
    ]
    ok = all(expect(l, m) for l, m in cases)
    print("OK: every negative control fires" if ok else "FAIL: a negative control did not fire")
    return ok


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--check", action="store_true")
    ap.add_argument("--self-test", action="store_true")
    args = ap.parse_args()
    files = outputs()
    validate(files)
    print("OK: PurchaseConfirmView and the Menu_Main purchase window validate")
    if args.self_test:
        return 0 if self_test(files) else 1
    stale = drift(files)
    if args.check:
        for p in stale:
            print(f"DRIFT: {p}")
        print("OK: no drift" if not stale else f"FAIL: {len(stale)} file(s) drifted")
        return 1 if stale else 0
    for path in stale:
        full = os.path.join(REPO, path)
        os.makedirs(os.path.dirname(full), exist_ok=True)
        with open(full, "w", encoding="utf-8", newline="\n") as fh:
            fh.write(files[path])
        print(f"wrote {path}")
    if not stale:
        print("no files written (already current)")
    return 0


if __name__ == "__main__":
    sys.exit(main())
