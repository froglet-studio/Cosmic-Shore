# Prompt — the 662 MB nothing can reach, starting with a 360 MB texture pack

Paste everything below into a fresh session.

---

The launch-blocker index's folder-by-folder pass sized the shipping problem at **~17 MB**. A
transitive reachability sweep from the real build roots sizes it at **662 MB**, and the single
largest item in the project turns out to be a texture pack **nobody had catalogued**.

Read `Docs/LAUNCH_BLOCKER_INDEX.md` **§E** first — it carries the method, the validation, and the
three limits that stop the number being over-read. Re-measure before acting:

```
python3 Tools/Build/measure_build_reachability.py --self-test
python3 Tools/Build/measure_build_reachability.py
python3 Tools/Build/measure_build_reachability.py --list Assets/_Graphics --top 30
```

## The two findings

### 1. `Assets/_Graphics/Texture/Noise Texture Collection (Angelo)` — 360.6 MB, 109 files, ZERO reachable

4K noise tiles (Cells, Vines, Swirls, Waves, Geometric, Boxes). **Not one file is reached from any
enabled build scene, any `Resources/` folder, or any preloaded asset.** It is **26% of `Assets/`**.

It carries **no licence, no readme, no vendor** and a folder name naming a person. That makes it a
`Docs/THIRD_PARTY_REGISTER.md` §6 item as well — the register missed it because it scoped
`_Graphics` out as first-party.

**Two questions, in this order, before anything is deleted:**

1. **Who is Angelo and what were the terms?** Contractor work-for-hire, a purchased pack, or
   something someone downloaded? This is the same provenance question §6 asks of `Effects Library`,
   and the same answer applies: *an invented explanation is worse than a gap.* The clone is shallow
   (`.git/shallow`), so `git log --diff-filter=A` reports the graft boundary — a `git fetch
   --unshallow` on a machine that can reach the remote is what answers *when and by whom*.
2. **Is anyone about to use it?** An unreferenced texture library is a normal state for art that has
   not been wired yet. Ask the art lead before treating 360 MB of someone's work as garbage.

**If the answer is "nobody knows and nobody is using it":** it is repository weight rather than
build weight (nothing reaches it, so nothing ships it), which makes this a **git-history** problem —
deleting the files leaves them in every clone's history. Removing 360 MB properly means either
accepting the history, or a filter-repo rewrite that every collaborator must re-clone after. **That
is a decision for whoever owns the repo, not a side effect of a cleanup branch.** Say so explicitly
rather than deleting and calling it done.

### 2. `Assets/_Graphics/Video` — 165 MB reached only through a RETIRED serialized field

The sweep reported 110.7 MB of video as shipping. **It almost certainly does not, and the reason is
worth more than the number.**

40 `SO_ArcadeGame` assets still carry a serialized **`PreviewClip:`** key pointing at a
`*Preview_Prefab.prefab`. **No script declares that field any more** — it was deleted when the arcade
preview became a live satellite arena (`Docs/ModePreview/ARCHITECTURE.md`, which states the window
*"must never fall back to a video"*). Unity never prunes an unresolvable serialized key, so the YAML
still names the guid, a text-based sweep still follows it, and the asset looks live.

What is actually live, measured:

| Path | Status |
|---|---|
| `SO_ArcadeGame.PreviewVideo` (a `VideoClip`) | declared, read by **one** consumer, `MaelstromLaunchPanel`; **1 of 40** cards has a non-null value |
| `SO_ArcadeGame.PreviewClip` (in YAML) | **field does not exist in code** — 40 cards carry the dead key |
| `SO_VesselAbility.PreviewClip` (a `VideoPlayer`) | declared, read by `HangarAbilitiesView` — **not verified here** |

**Do this:** establish what the Hangar ability path still needs (that is the open question — start
at `HangarAbilitiesView.cs:44`), keep those clips plus Maelstrom's, and remove the rest **together
with the dead `PreviewClip:` keys** so the next sweep stops reporting them.

**Note `SO_VesselAbility.PreviewClip` is typed `VideoPlayer`, not `VideoClip`** — a ScriptableObject
holding a reference to a scene/prefab *component*. Whether that even resolves at runtime is worth
checking before deciding which videos are load-bearing.

## The general rule, which is the reusable part

**A retired serialized field is invisible to the compiler, invisible to the inspector, and still
visible to every text-based tool** — including guid sweeps, migration scripts, and this project's own
audits. CLAUDE.md already records the same shape for Cell overrides ("Unity never prunes an
unresolvable modification, so retired fields linger for years pointing at guids no asset carries").
**When a sweep says an asset is referenced, check that the field naming it still exists.**

## The rest of the 662 MB

`--list` each of these before forming an opinion; several are legitimately work-in-progress.

| Folder | Not reached | Note |
|---|---|---|
| `_Graphics` | 483.1 MB | 360.6 MB of it is finding 1; `Video` is finding 2 |
| `_Prefabs` | 44.8 MB | 178 files — expect real orphans and real WIP |
| `_Audio` | 44.0 MB | 24 files, incl. **7 full-quality MP3s** that no `AudioSource` or FMOD bank reaches |
| `NiceVibrations` | 43.3 MB | covered by the two NiceVibrations prompts — do not duplicate |
| `_Models` | 19.6 MB | overlaps the 8 vessel-model vestiges (index §D1, already gated) |

## Constraints

* **Delete nothing first-party without asking.** `_Graphics` and `_Audio` are the studio's own
  content and "unreferenced" is the normal state of unfinished work. The salvage-before-delete gate
  at the top of the index applies here more than anywhere.
* **Do not quote the sweep's raw total.** Code compiles regardless of references and native plugins
  ship by platform importer settings, which is why `_Scripts` and `Plugins` are excluded — the
  docstring says so and the number changes if you include them.
* **Removing files does not shrink the repository.** Say which problem a removal actually solves —
  build size, clone size, or nobody-can-find-anything — because for this branch's biggest item it is
  the second, and that one needs a history decision.

## Definition of done

1. A recorded answer on the noise pack — vendor/terms, or an explicit **unknown** naming who was
   asked — and a recorded decision on whether history is rewritten.
2. The video question resolved: what the Hangar path needs, what Maelstrom needs, what goes, and the
   40 dead `PreviewClip:` keys removed.
3. `Docs/LAUNCH_BLOCKER_INDEX.md` §E1/§E2 updated, and the noise pack added to
   `Docs/THIRD_PARTY_REGISTER.md` §6 whichever way the provenance question lands.
4. `measure_build_reachability.py` re-run, before/after quoted.
5. Verified in the editor (`/verify-unity`) — the Hangar, the arcade screen and the Maelstrom launch
   panel all still draw — or stated plainly that it was not.
