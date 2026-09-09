#!/usr/bin/env python3
"""Remove the Menu_Main UI that nothing can reach.

Two subtrees, each dead for a reason the scene itself states:

  * `DailyChallengeModal` (ModalType 2) — its ONLY opener is `DailyChallengeButton`
    under `PortScreen`, and PORT is in `ScreenSwitcher.disabledScreens`, so no input
    can reach it. It is also the PlayFab-era modal CLAUDE.md records as superseded by
    the weekly challenge ("do not wire both"); its two views read through the inert
    `LeaderboardManager`/`AuthenticationManager.PlayFabAccount` path.

  * `ModePreviewHUD` — `ModePreviewSession` holds it only to call `Hide()` on it, in
    three places, and never binds or shows it. That is the beside-the-window preview
    HUD `Docs/ArcadeLaunch/ARCHITECTURE.md` records as RETIRED in favour of the
    objective box and the micro toast.

Deleting a subtree from a scene is not just dropping its documents: every reference
INTO it from outside has to go too, or Unity keeps an unresolvable `{fileID: N}` that
no inspector can show and no sweep can attribute. So this also rewrites, and reports:

  * `ScreenSwitcher.Modals` — the entry for the deleted modal;
  * UnityEvent persistent calls whose target was inside (the whole call item, not just
    the target — a call with a null target is exactly the broken binding this repo
    already carries three of);
  * any other list entry or scalar field pointing in.

Deliberately NOT touched: `ProfileModal` and its two openers, whose targets are
already `{fileID: 0}`. That is a broken reference on a LIVE screen — a regression to
fix, not dead weight to sweep (see the audit in the PR body).

Idempotent. `--check` exits non-zero while anything is left to remove.
"""
import argparse
import os
import re
import sys

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
SCENE = os.path.join(ROOT, "Assets/_Scenes/Menu_Main.unity")

# The two roots, by name and by the parent that disambiguates them.
TARGETS = [("DailyChallengeModal", "ModalWindows"), ("ModePreviewHUD", None)]

# Scripts that become unreferenced once the subtrees are gone. Each was checked to be
# referenced ONLY from these subtrees plus the already-doomed migration prefab.
DEAD_SCRIPTS = [
    "Assets/_Scripts/UI/Modals/DailyChallengeModal.cs",
    "Assets/_Scripts/UI/Views/DailyChallengeGameView.cs",
    "Assets/_Scripts/UI/Views/DailyChallengeLeaderboardView.cs",
    "Assets/_Scripts/UI/TooltipHandler.cs",
    "Assets/_Scripts/UI/View/ModePreviewHUD.cs",
]


class SceneDoc:
    """Menu_Main split into its YAML documents, addressable by fileID."""

    def __init__(self, path):
        self.path = path
        self.raw = open(path).read()
        self.header, *chunks = re.split(r"^--- ", self.raw, flags=re.M)
        self.order = []
        self.docs = {}
        for c in chunks:
            m = re.match(r"!u!(\d+) &(\d+)", c)
            if not m:
                self.order.append((None, c))
                continue
            self.order.append((m.group(2), c))
            self.docs[m.group(2)] = (m.group(1), c)

    def utype(self, fid):
        return self.docs[fid][0] if fid in self.docs else None

    def body(self, fid):
        return self.docs[fid][1] if fid in self.docs else ""

    def name(self, go):
        m = re.search(r"^  m_Name: (.*)$", self.body(go), re.M)
        return m.group(1).strip() if m else "?"

    def go_of(self, fid):
        if self.utype(fid) == "1":
            return fid
        m = re.search(r"m_GameObject: \{fileID: (\d+)\}", self.body(fid))
        return m.group(1) if m else None

    def components(self, go):
        return re.findall(r"component: \{fileID: (\d+)\}", self.body(go))

    def transform_of(self, go):
        for cid in self.components(go):
            if self.utype(cid) in ("224", "4"):
                return cid
        return None

    def parent_transform(self, go):
        t = self.transform_of(go)
        if not t:
            return None
        m = re.search(r"m_Father: \{fileID: (\d+)\}", self.body(t))
        return m.group(1) if m and m.group(1) != "0" else None

    def path_of(self, go):
        parts, seen, cur = [], set(), go
        while cur and cur not in seen:
            seen.add(cur)
            parts.append(self.name(cur))
            pt = self.parent_transform(cur)
            cur = self.go_of(pt) if pt else None
        return " <- ".join(parts)

    def children(self, go):
        t = self.transform_of(go)
        if not t:
            return []
        m = re.search(r"m_Children:\s*\n((?:  - \{fileID: \d+\}\n)*)", self.body(t))
        if not m:
            return []
        return [self.go_of(x) for x in re.findall(r"\{fileID: (\d+)\}", m.group(1))]

    def subtree(self, go):
        out, stack = [go], [go]
        while stack:
            for c in self.children(stack.pop()):
                if c and c not in out:
                    out.append(c)
                    stack.append(c)
        return out

    def find(self, name, parent_hint):
        hits = []
        for fid, (t, _) in self.docs.items():
            if t != "1" or self.name(fid) != name:
                continue
            if parent_hint is None or parent_hint in self.path_of(fid):
                hits.append(fid)
        return hits


def strip_persistent_calls(text, doomed, report):
    """Drop every UnityEvent call item whose m_Target was inside a deleted subtree."""
    lines, out, i = text.split("\n"), [], 0
    while i < len(lines):
        m = re.match(r"^(\s*)- m_Target: \{fileID: (\d+)\}", lines[i])
        if not m:
            out.append(lines[i])
            i += 1
            continue
        indent, target = m.group(1), m.group(2)
        j = i + 1
        while j < len(lines):
            nxt = lines[j]
            if not nxt.strip():
                j += 1
                continue
            cur = len(nxt) - len(nxt.lstrip())
            if cur < len(indent) or (cur == len(indent) and nxt.lstrip().startswith("- ")):
                break
            j += 1
        if target in doomed:
            meth = next((re.match(r"^\s*m_MethodName: (\S+)", l).group(1)
                         for l in lines[i:j] if re.match(r"^\s*m_MethodName: \S+", l)), "?")
            report.append(f"    UnityEvent call -> {meth}() on a deleted object")
            i = j
            continue
        out.extend(lines[i:j])
        i = j
    return "\n".join(out)


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--check", action="store_true")
    args = ap.parse_args()

    sd = SceneDoc(SCENE)
    report, doomed_go, roots = [], set(), []

    for name, hint in TARGETS:
        for go in sd.find(name, hint):
            st = sd.subtree(go)
            roots.append(go)
            doomed_go.update(st)
            report.append(f"  {name}: {len(st)} GameObjects  ({sd.path_of(go)})")

    if not doomed_go:
        print("Menu_Main dead UI: already clean")
        return 0

    doomed = set(doomed_go)
    for go in doomed_go:
        doomed.update(sd.components(go))

    # Detach each root from its parent's child list before the documents go.
    text = sd.raw
    for go in roots:
        t = sd.transform_of(go)
        pt = sd.parent_transform(go)
        if t and pt:
            text = text.replace(f"  - {{fileID: {t}}}\n", "", 1)

    # Drop the documents themselves.
    kept = [sd.header]
    for fid, chunk in sd.order:
        if fid in doomed:
            continue
        kept.append("--- " + chunk)
    # Re-apply the child-list detach on the rebuilt text.
    text = "".join(kept)
    for go in roots:
        t = sd.transform_of(go)
        if t:
            text = text.replace(f"  - {{fileID: {t}}}\n", "", 1)

    # Now every remaining reference INTO the deleted set.
    text = strip_persistent_calls(text, doomed, report)

    removed_entries = 0
    lines, out = text.split("\n"), []
    for line in lines:
        m = re.match(r"^\s*- \{fileID: (\d+)\}\s*$", line)
        if m and m.group(1) in doomed:
            removed_entries += 1
            continue
        out.append(line)
    text = "\n".join(out)
    if removed_entries:
        report.append(f"    {removed_entries} list entr(y/ies) (ScreenSwitcher.Modals, layout groups)")

    nulled = 0

    def null_ref(m):
        nonlocal nulled
        if m.group(2) in doomed:
            nulled += 1
            return f"{m.group(1)}{{fileID: 0}}"
        return m.group(0)

    text = re.sub(r"(\w+: )\{fileID: (\d+)\}", null_ref, text)
    if nulled:
        report.append(f"    {nulled} scalar reference(s) nulled")

    print(f"{'WOULD STRIP' if args.check else 'stripped'}: {os.path.relpath(SCENE, ROOT)}")
    for r in report:
        print(r)

    if not args.check:
        open(SCENE, "w").write(text)
        for rel in DEAD_SCRIPTS:
            for p in (os.path.join(ROOT, rel), os.path.join(ROOT, rel + ".meta")):
                if os.path.exists(p):
                    os.remove(p)
                    print(f"  deleted {os.path.relpath(p, ROOT)}")

    return 1 if args.check else 0


sys.exit(main())
