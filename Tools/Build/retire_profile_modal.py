#!/usr/bin/env python3
"""
Retire the older ProfileModal and make PlayerDataSelectModal THE profile modal.

Two overlapping profile editors shipped (Docs/UI_ARCHITECTURE_AUDIT.md §2.10.3) and NEITHER was
reachable: both screen-level avatar buttons - ShowPopupButton on ProfileScreen and OnlineIndicator
on HomeScreen - carried a persistent ModalWindowIn whose m_Target was {fileID: 0}.

The survivor is PlayerDataSelectModal (ProfileIconSelectView): a self-contained modal with its own
Avatar / DisplayName tabs that saves to cloud, no dead code, and intact wiring. ProfileModal is the
older one - larger, with commented-out email login/registration - and its GameObject was ALREADY
m_IsActive: 0 in the scene. It stays in the scene switched off (the art, including a name generator
the survivor has no equivalent for, is worth keeping) but loses every live path to it.

So the survivor takes ModalWindows.PROFILE (3), which ProfileModal held; PROFILE_ICON_SELECT (4)
is reserved-not-reused in the enum, per the same stale-ReturnToModal-pref rule the file already
records for the deleted DAILY_CHALLENGE.

The openers are repaired to ScreenSwitcher.OnClickProfileModal - a parameterless wrapper around
OpenModal(ModalWindows.PROFILE) - rather than a direct ModalWindowIn, per
Docs/HomeHub/ARCHITECTURE.md §1: the switcher owns the modal stack, the return-to-modal pref and
the close sweeps, so a button reaching past it would be a second authority. A UnityEvent persistent
call cannot pass an enum, which is why it is a wrapper and not OpenModal itself.

Idempotent: re-running prints "already retired" and exits 0.

    python3 Tools/Build/retire_profile_modal.py [--check]

--check exits 1 if the retirement has not been applied (for CI).
"""
import re, sys, pathlib

SCENE = pathlib.Path("Assets/_Scenes/Menu_Main.unity")
PREFAB = pathlib.Path("Assets/_Prefabs/UI Elements/Profile.prefab")

SCREEN_SWITCHER = "4216411968520269688"          # ScreenSwitcher component, on GameObject "Screens"
SURVIVOR        = "8675064058193410727"          # PlayerDataSelectModal / ProfileIconSelectView
RETIRED         = "2899345459383930630"          # ProfileModal
OPENERS         = ["291271916486792912",         # ShowPopupButton  (ProfileScreen / AvatarDisplay)
                   "1431731438122436423"]        # OnlineIndicator  (HomeScreen / Main_Menu_Panel / AvatarIcon)
SCENE_CLOSE     = "2899345458740611223"          # ProfileModal's own CloseButton
PREFAB_CLOSE    = "6960199310942333857"          # Profile.prefab's CloseButton
PREFAB_ICON     = "6960199312717497857"          # Profile.prefab's ProfileIconButton

METHOD = "OnClickProfileModal"
TYPENAME = "CosmicShore.UI.ScreenSwitcher, Assembly-CSharp"

# One persistent call: from its "- m_Target:" line to the first m_CallState line that closes it.
CALL = re.compile(
    r"      - m_Target: \{fileID: (?P<target>-?\d+)\}\n"
    r"(?:        .*\n|          .*\n)*?"
    r"        m_CallState: \d+\n")


def split_docs(path):
    raw = path.read_text(encoding="utf-8")
    parts = raw.split("\n--- ")
    return raw, parts[0], parts[1:]


def join(header, docs):
    return header + "".join("\n--- " + d for d in docs)


def doc_index(docs, fid):
    for i, d in enumerate(docs):
        if re.match(rf"!u!\d+ &{fid}\b", d):
            return i
    raise AssertionError(f"component &{fid} not found")


def onclick_region(body):
    """(start, end) of the m_Calls listing inside m_OnClick. Scoped so a sibling UnityEvent on the
    same component - m_OnCullStateChanged, m_OnValueChanged - can never be edited by accident."""
    i = body.index("m_OnClick:")
    j = body.index("m_Calls:", i) + len("m_Calls:\n")
    # the listing ends at the first line indented less than a call entry
    k = j
    for m in re.finditer(r"^(?!      [-\s])", body[j:], re.M):
        k = j + m.start()
        break
    else:
        k = len(body)
    return j, k


def edit_calls(docs, fid, fn):
    """Rewrite the m_OnClick call listing of one component through fn(listing) -> listing.

    The listing is normalised to end in a newline before fn sees it and de-normalised after, so
    every call entry has the same shape whether or not it is the last one in the block. Without
    that the final entry is the only one a line-terminated pattern cannot match - which is exactly
    the entry these edits are usually about."""
    i = doc_index(docs, fid)
    body = docs[i]
    a, b = onclick_region(body)
    before = body[a:b]
    padded = before if before.endswith("\n") else before + "\n"
    after = fn(padded)
    assert after != padded, f"&{fid}: edit was a no-op"
    if not before.endswith("\n"):
        assert after.endswith("\n")
        after = after[:-1]
    docs[i] = body[:a] + after + body[b:]


def drop_call(target):
    def fn(listing):
        hits = [m for m in CALL.finditer(listing) if m.group("target") == target]
        assert len(hits) == 1, f"expected 1 call targeting {target}, found {len(hits)}"
        out = listing[:hits[0].start()] + listing[hits[0].end():]
        # Never leave an empty listing shaped as a block - Unity writes `m_Calls: []`.
        assert CALL.search(out), "dropping this call would empty the listing; emit [] instead"
        return out
    return fn


def retarget_opener(listing):
    hits = [m for m in CALL.finditer(listing) if m.group("target") == "0"]
    assert len(hits) == 1, f"expected 1 dead (fileID 0) call, found {len(hits)}"
    block = hits[0].group(0)
    assert "m_MethodName: ModalWindowIn" in block, "the dead call is not the ModalWindowIn opener"
    assert "m_Mode: 1" in block, "opener is not a void call - OnClickProfileModal takes no argument"
    fixed = (block
             .replace("m_Target: {fileID: 0}", f"m_Target: {{fileID: {SCREEN_SWITCHER}}}", 1)
             # The match starts AT the key, not at its indentation - so the replacement must not
             # carry any of its own, or the line ends up doubly indented and the YAML is invalid.
             .replace(re.search(r"m_TargetAssemblyTypeName: .*\n", block).group(0),
                      f"m_TargetAssemblyTypeName: {TYPENAME}\n", 1)
             .replace("m_MethodName: ModalWindowIn", f"m_MethodName: {METHOD}", 1))
    return listing[:hits[0].start()] + fixed + listing[hits[0].end():]


def already_done():
    return METHOD in SCENE.read_text(encoding="utf-8")


def main():
    check = "--check" in sys.argv

    if already_done():
        print("already retired")
        return 0
    if check:
        print("NOT RETIRED: the profile-modal openers still target {fileID: 0}")
        return 1

    # ---- scene -------------------------------------------------------------------------
    raw, header, docs = split_docs(SCENE)

    # 1. The survivor becomes THE profile modal.
    i = doc_index(docs, SURVIVOR)
    assert re.search(r"^\s*ModalType: 4$", docs[i], re.M), "survivor is not ModalType 4 (PROFILE_ICON_SELECT)"
    docs[i] = re.sub(r"^(\s*ModalType: )4$", r"\g<1>3", docs[i], count=1, flags=re.M)

    # 2. Unregister the retired one, so OpenModal can never find it again.
    ss = doc_index(docs, SCREEN_SWITCHER)
    line = f"  - {{fileID: {RETIRED}}}\n"
    assert line in docs[ss], "ProfileModal is not in the ScreenSwitcher Modals list"
    docs[ss] = docs[ss].replace(line, "", 1)

    # 3-4. Repair both openers.
    for opener in OPENERS:
        edit_calls(docs, opener, retarget_opener)

    # 5. The retired modal keeps no wiring - this is the ProfileModal.ModalWindowOut row the
    #    persistent-listener audit reports for the scene.
    edit_calls(docs, SCENE_CLOSE, drop_call(RETIRED))

    out = join(header, docs)
    assert out.count(f"m_MethodName: {METHOD}") == 2
    # Indentation is the failure this edit can silently produce, and Unity answers an
    # over-indented key by dropping the whole document. Assert the exact repaired shape.
    assert out.count(f"      - m_Target: {{fileID: {SCREEN_SWITCHER}}}\n"
                     f"        m_TargetAssemblyTypeName: {TYPENAME}\n"
                     f"        m_MethodName: {METHOD}\n"
                     f"        m_Mode: 1\n") == 2, "repaired opener has the wrong shape"
    SCENE.write_text(out, encoding="utf-8")
    print(f"scene: survivor -> PROFILE, ProfileModal unregistered, {len(OPENERS)} openers repaired")

    # ---- prefab ------------------------------------------------------------------------
    # Profile.prefab IS the retired modal's prefab (it carries ProfileModal.cs) and is instanced
    # only by MIgration_Prefabs (DELETE LATER)/ModalWindows.prefab. Its wiring goes for the same
    # reason: a retired modal keeps none.
    raw, header, docs = split_docs(PREFAB)
    edit_calls(docs, PREFAB_CLOSE, drop_call("6960199311510088752"))   # ProfileModal.ModalWindowOut
    edit_calls(docs, PREFAB_ICON, drop_call("0"))                      # dead ModalWindowIn
    PREFAB.write_text(join(header, docs), encoding="utf-8")
    print("prefab: ProfileModal.ModalWindowOut and the dead ModalWindowIn removed")
    return 0


if __name__ == "__main__":
    sys.exit(main())
