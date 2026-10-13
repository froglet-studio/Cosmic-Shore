# Authoring: editing Unity content without the Unity Editor

**Status (2026-10-02):** the port can WRITE the project's scenes, prefabs and assets, not only
read them. The command-line tool (`cs-asset`) creates, edits and deletes GameObjects, and adds
and removes components: project scripts, Unity built-ins (colliders, renderers, Rigidbody …)
and package components (uGUI, TextMeshPro …). It also edits **through nested prefab
instances** — overriding fields, removing and adding objects and components inside a placed
prefab, and placing new prefabs — written as the instance's overrides, the way the Unity Editor
writes them — and **applies** an instance's changes to its prefab. There is no visual editor yet (no hierarchy panel, inspector or gizmos); see "Not
built yet" below.

Before this, the port was a runtime only. It read `Assets/` and played the game, but the one
thing it could write was trained AI exported as ScriptableObjects (`UnityYamlWriter`). Every
scene, prefab and asset still had to be authored in Unity.

## The property everything rests on

**An untouched file, or an untouched part of an edited file, is written back byte for byte.**

The port and the Unity Editor share one set of files. If the port reformatted what it saved,
every save would show up as a diff against Unity's own output, and the two tools would keep
rewriting each other's files. So the writer only re-emits what an edit actually changed.

Measured over the whole project (`cs-asset roundtrip`, also asserted by
`UnityYamlEditingTests.EveryProjectYamlFileRoundTripsByteForByte`):

| | result |
|---|---|
| YAML files in `Assets/` (.unity .prefab .asset .mat .controller .anim .mixer …) | 2030 files, 79 MB |
| parse → write is byte-identical | **2030 / 2030** |
| full re-emit (ignoring the source) reads back as the same content | **2030 / 2030** |
| full re-emit byte-identical to disk, files Unity last wrote | 1325 / 1507 (88%) |
| full re-emit byte-identical to disk, files the repo's Python generators wrote | 0 / 523 (expected) |

The last two rows measure the **canonical** emitter, which is used only for nodes an edit
touched. It reproduces Unity's own output exactly on 88% of the files Unity wrote. The 523
tool-written files never match, by construction: the `Tools/Build/author_*.py` generators
format differently from Unity (no trailing space on an empty value, different line wrapping).
They are identified by the `m_EditorClassIdentifier:` line without Unity's trailing space.
Lossless copy means neither source of difference ever reaches a file you did not edit.

## How it works

`Content/Yaml/UnityYamlFile.cs`. A source-preserving parse (the same `UnityYaml` reader the
game uses, with one optional argument) records two things for every map entry and sequence
item:

- its span of source lines;
- a hash of the value it parsed.

On write, an entry whose value still hashes the same is copied from source. Anything else is
emitted canonically. Change detection is by content, not by tracking API calls, so edits made
straight on `YMap.Entries` / `YSeq.List` are caught too. An untouched document is copied
whole, including malformed lines the reader skipped. Three such files exist: tab-indented
lines in three Sparrow captain assets.

The canonical emitter follows libyaml's rules (Unity's serializer is libyaml-shaped), with the
differences measured against Unity-written scenes:

| rule | stock libyaml | Unity (measured) |
|---|---|---|
| flow mapping wraps | after `,` when column > 80 | same |
| scalar wraps | at a space when column > 80 | at a space when column **≥** 80 |
| non-ASCII text | written raw (unicode mode) | double-quoted with `\uXXXX` |
| value ending in `]` | plain | **single-quoted** (`'m_Materials.Array.data[0]'`); keys stay plain |
| empty value | `key:` | `key: ` (trailing space) |
| quoted value ending in line breaks | closing quote indented | closing quote at column 0 |
| non-empty sequence | either style | always block (`- item`), never `[a, b]` |

Each rule is pinned by a case in `UnityYamlEditingTests.CanonicalOutputMatchesUnity`.

## The editing API

`Content/Editing/UnityAssetEditor.cs`, over a `UnityYamlFile`:

- `Get` / `Set` / `Remove`: any field of any document by path (`m_LocalPosition.x`,
  `m_Component[1].component.fileID`). Values are YAML text (`5`, `Hello`, `{x: 1, y: 2, z: 3}`).
- `GameObjects()` and `FindGameObject("Canvas/Panel/Button")`.
- `CreateGameObject(name, parent)`: GameObject + Transform with Unity's defaults and a random
  64-bit fileID (Unity's own scheme since 2018.3, so two people's additions don't collide on
  merge). The new object is linked into the parent's `m_Children` or `SceneRoots`, and
  documents are inserted in fileID order.
- `DeleteGameObject(id)`: removes the object, its components and its whole child hierarchy,
  including nested prefab instances (the `PrefabInstance` document, its stripped stand-ins, and
  objects added onto it). Unlinks it from the parent and `SceneRoots`. Any other field in the
  file that pointed at a removed object is cleared to `{fileID: 0}` (Unity's "None") and
  reported. References into other assets (those carrying a `guid`) are left alone.

Creating an object and then deleting it restores the original file exactly; this is asserted
by a test.

## Components

`Content/Editing/ComponentAdder.cs` is "Add Component". A component comes from one of three
places, depending on what it is:

| kind | where its fields come from | measured |
|---|---|---|
| **project script** (anything under `Assets/` the game compiles) | its C# type, through `ComponentSerializer`: Unity's field rules, Unity's YAML shapes, the values a fresh instance holds | see below |
| **Unity built-in** (BoxCollider, MeshRenderer, Rigidbody, Canvas …; 30 class IDs) | a template from the project's own Unity-saved scenes and prefabs (`ComponentTemplates`) | layout = the most common one Unity wrote |
| **package / plugin script** (uGUI, TextMeshPro, Netcode, FMOD, SOAP …) | the same templates, by script guid | same |

Package and plugin scripts can't be read off their C# type, because the port runs its own
**stand-ins** for them, and a stand-in's fields are not the original's serialized layout. A
script is a stand-in when its .NET type isn't the class Unity records
(`ScriptInfo.IsStandIn`).

**A template is MEASURED.** It takes the most common field layout among every saved instance
of that component, which is the layout this Unity version writes; a BoxCollider comes out
with Unity 6's `m_IncludeLayers` / `m_ProvidesContacts`. For each field it takes the most
common value, which for a field nobody touches is Unity's default. Two things are then
corrected:

- References to other objects in the sample's own file are cleared, because they would point
  at nothing in yours.
- A short table pins defaults that a "most common in this project" vote could get wrong: a
  BoxCollider's size, an Image's sprite, a text's string.

Files written by the repo's Python generators are never used as samples.

### Unity's component rules, kept

- `[RequireComponent]`: what's required is added first.
  - A requirement already met by a subclass counts as met.
  - A requirement cycle (Prism ↔ PrismScaleAnimator) is satisfied by the add already in
    progress.
  - An abstract requirement defaults the way Unity does (Collider → BoxCollider) or is
    refused.
  - An interface requirement is ignored, with a note.
- One per object: Transform, Rigidbody, MeshRenderer, Canvas and the other built-ins Unity
  limits; `[DisallowMultipleComponent]` scripts; and **one Graphic per GameObject**, because a
  CanvasRenderer draws exactly one.
- A UI component turns the object's Transform into a RectTransform in place, keeping the same
  fileID so every reference to it stays valid. This is what Unity does when you add an Image
  to a plain object.
- `m_EditorClassIdentifier` is written the way Unity 6 writes a newly added component:
  `Assembly-CSharp::CosmicShore.UI.TrapezoidGraphic`, `UnityEngine.UI::UnityEngine.UI.Image`.
  The assembly is the nearest `.asmdef` above the script.
- `RemoveComponent` unlinks the component and clears any references to it. A Transform cannot
  be removed; delete the GameObject instead.

### How faithful the script serializer is (`cs-asset schema`)

`cs-asset schema` regenerates every saved script component and ScriptableObject in the project
from its C# type and compares the field LAYOUT (names, order, YAML shapes) with what Unity
wrote. It does not compare values: a saved component holds tuned values, a new one holds
defaults.

| | result |
|---|---|
| saved script instances in Unity-written files | 13,563 (732 types) |
| field layout identical to Unity's | **9,436** |
| stale: the file predates a field being added, removed or renamed (`[FormerlySerializedAs]`) | 4,070 |
| pure reorder of the same fields (a field moved in source, file not re-saved) | 57 |
| types with at least one instance identical to Unity's output | **550 / 732** |

Every remaining disagreement was checked and is a file that hasn't been re-saved since its
script changed. The remaining 182 types have no instance saved since their last code change,
so there is nothing current to compare them against.

The rules the measurement pinned down, all asserted in `ComponentAuthoringTests`:

- Base-class fields come first, then declaration order.
- `[field: SerializeField]` auto-properties are serialized under their compiler name
  (`<Level>k__BackingField`).
- An array of ints, enums or bools is ONE little-endian hex scalar; a float list is a block
  list.
- A null `[Serializable]` class is written as its default instance.
- Built-in structs are flow maps (`{x: 1, y: 2, z: 3}`); Rect / LayerMask / Color32 /
  AnimationCurve / Gradient are blocks with `serializedVersion`.
- A script deriving from a package class starts with that class's fields: MaskableGraphic's
  six; NetworkBehaviour's `ShowTopMostFoldoutHeaderGroup: 1`.

`cs-asset addall` adds every component in the project to a fresh GameObject, one at a time:
**937 components, 900 added, 37 refused by a rule, 0 failures.** The refusals are:

- an abstract requirement Unity also can't satisfy (VesselStatus needs *some* VesselAnimation);
- a built-in or package component with no saved instance to measure (SpriteRenderer, LODGroup …);
- a name that is an asset type (ScriptableObject), not a component.

For anything without a template, `add --like &fileID` copies an existing component instead.

### How faithful the script loader is (`cs-asset serialization-audit`)

`schema` checks the writer. `serialization-audit` checks the other direction: for every key Unity
wrote into a script component or ScriptableObject (recursing into nested `[Serializable]` classes
and lists), does Unity's serializer read it, and does Amoebius's loader?

| Class | Meaning |
|---|---|
| DROPPED | Unity reads it, Amoebius does not: the value silently never arrives (the worst kind) |
| EXTRA | Amoebius reads it, Unity ignores it: Amoebius loads data Unity never would |
| STALE | Neither reads it: left over from an older version of the script; harmless |
| MANAGED | A `[SerializeReference]` (`references:`) block, which Amoebius does not load yet |

It exits 1 on any DROPPED or EXTRA key, so it can gate a build. Unity's rules, as Amoebius applies
them to script types (`Content/Serialization/UnitySerializationRules.cs`): public or
`[SerializeField]` fields, not `[NonSerialized]`, readonly or const; the field's type must be one
Unity serializes (no `Dictionary`, interface, abstract type or nested collection, and a custom
class needs `[Serializable]`); `[field: SerializeField]` auto-properties load from
`<Name>k__BackingField`; `[FormerlySerializedAs]` names still load; properties and `m_` spellings
never do; `OnAfterDeserialize` runs after the read. Engine and package built-ins keep their native
`m_` layouts.

Measured on 2026-10-06 over 14,319 script instances (112,941 keys): before the rules, 52 DROPPED
(the vessels' `ResourceSystem` levels) and 78 EXTRA; after, **0 DROPPED, 0 EXTRA**, 0 MANAGED and
7,070 STALE keys over 630 fields.

### Two bugs this found in existing code

- **`ScriptTypeMap` lost components whose file name differs from the class only in case.**
  `MinigameHUDView.cs` holds `MiniGameHUDView`. Unity accepts that, but the port's resolver
  didn't, so the HUD view component silently never loaded in the Player. Resolution now falls
  back to a case-insensitive match.
- **`YMap` returned stale values after a write through `Entries[i]`.** A map of 8+ keys
  answers lookups from an index, which a direct list write doesn't update. `YMap.SetAt` is the
  safe write; the editing code uses it.

## Prefab instances

An object inside a placed prefab has no document of its own in the scene or prefab that
places it. The file holds a `PrefabInstance` naming the prefab, plus that instance's changes in
its `m_Modification` block. `Content/Editing/PrefabInstanceEditor.cs` edits those objects the
way the Unity Editor does: every change goes into `m_Modification`, and every read comes from
`PrefabGraph`, the same expansion the game loads, so what an edit writes is checked against
what the file then loads as.

In `cs-asset`, paths and names reach inside instances (`GameCanvas/ConnectingPanel` is an
object of `GameCanvas.prefab`), and every command routes there on its own:

| edit | written as |
|---|---|
| change a field | one `m_Modifications` entry per leaf (`m_LocalScale.x`, `.y`, `.z`); a list as `.Array.size` then `.Array.data[i]`; a reference in `objectReference` with an empty `value` |
| remove a component / GameObject | a source reference in `m_RemovedComponents` / `m_RemovedGameObjects`; the object's overrides and stand-ins go with it |
| add a component / child GameObject | an ordinary document of this file hung off a "stripped" stand-in for the instance object, listed in `m_AddedComponents` / `m_AddedGameObjects` (`insertIndex: -1`) |
| place a prefab (`instantiate`) | a new `PrefabInstance` with the overrides Unity writes on placement: the root's `m_Name`, then its transform (10 paths for a Transform root, 20 for a RectTransform) |
| delete an instance's root | the whole instance: its document, its stand-ins, and anything added to it or placed under it |
| `revert` | drops an object's overrides, all of them or one field's |

A reference to an object inside an instance — from an override or from any field of the file —
goes through that object's stand-in, which is created when missing, with the id Unity derives
for it (`(instance ^ source) & 0x7FFF_FFFF_FFFF_FFFF`) when that id is free.

**Ordering and shapes, measured over the project's Unity-written files:**

| rule | measured |
|---|---|
| overrides grouped by target, groups in ascending (signed) target fileID; within a target, the order the changes were made | 488 / 518 instances (Unity left the other 30 unsorted; existing entries are never moved) |
| a list's `Array.size` precedes its elements | 85 / 85 |
| an override's `target` carries the instance's source-prefab guid | 17,113 / 17,114 |
| a reference override has an empty `value` | 873 / 873 |
| a prefab placed under an object of another instance is listed in that instance's `m_AddedGameObjects`, naming the new instance's stripped root Transform | 23 / 23 |
| an added object is a Transform/RectTransform (stripped when it is a placed prefab's root) | 71 / 71 |
| a script's stand-in keeps `m_GameObject: {fileID: 0}`, `m_Enabled`, `m_EditorHideFlags`, `m_Script`, `m_Name`, `m_EditorClassIdentifier` | 435 / 435 |

**What an instance cannot do is refused**, as Unity refuses it:

- Overriding a field that links the prefab's objects together (`m_GameObject`, `m_Father`,
  `m_Children`, `m_Component`, `m_Script`, the prefab bookkeeping). Place or delete objects
  instead.
- Overriding a field the prefab doesn't have. `--force` writes it anyway: Unity keeps such an
  override and ignores it.
- Adding a UI component to an instance object whose prefab gives it a plain Transform. Only the
  prefab can make it a RectTransform.
- Placing a prefab inside itself, directly or through nesting; placing at a prefab file's root
  (a prefab has exactly one root).

A delete also removes any stand-in whose last reference it removed (the parent stand-in a placed
prefab needed, say), and leaves stand-ins that were already unreferenced alone.

### How it's verified

- **Replay.** Every override list in every Unity-written file is stripped and re-made through
  the API, its targets in shuffled order: **122 / 131 files byte-identical**. The other 9 are
  exactly the files in which Unity itself left a list unsorted
  (`PrefabInstanceEditingTests.UnityWrittenOverrideListsReplayByteForByte`).
- **The loader agrees with every edit.** Three random trials on each of the 131 files (all but
  the few over 3 MB), each re-reading the result through `PrefabGraph`: scalar overrides
  389 / 389, structure overrides 390 / 390, references through stand-ins 111 / 111, removed
  components 360 / 360, added children 390 / 390, removed objects 300 / 300, deleted instances
  390 / 390. After every trial the file reads back as written and holds no dangling local
  reference (390 / 390).
- **Full restore.** Placing two prefabs in a scene (one under an object inside `GameCanvas`),
  adding a child and a component inside an instance, then removing all four gives back the
  original scene byte for byte.
- `PrefabInstanceEditingTests` pins each operation on a miniature project (one prefab, one
  scene) written to a temp folder.

### A loader bug this found

`Dolphin.prefab` gives its jet instances sequential ids (`3901220100012000001`, `…002`, …).
Unity derives an instance object's id as `instance ^ source`, and with ids this close two
objects of different jets derive the same number: jet 1's GameObject (`…001 ^ …806`) and the
file's own stand-in for jet 2's Transform (`…002 ^ …805`). `PrefabGraph` registered the
stand-in over the real object, so the loader linked components of two jets to the wrong
GameObject. An id the file gives a stand-in now belongs to the object that stand-in names, and
the colliding derived object is renamed. Across all 85,226 component links in the 149 files
that place prefabs: **4 wrong before, 0 after.** Two tests pin it; both fail without the fix.

## Apply to prefab

`PrefabInstanceEditor.Apply` / `ApplyAll` (`Editing/PrefabInstanceEditor.Apply.cs`) is Unity's
"Apply": an instance's changes become its prefab's own and leave the instance. It applies to
the **outermost** prefab, the one this file placed, as Unity's default does.

| instance change | what the prefab gets |
|---|---|
| property override | the value written into the prefab's object; when that object comes from a prefab nested in it, the prefab's own override of that nested instance |
| added component / GameObject | the object itself (its whole hierarchy, nested prefabs included) moved into the prefab, under the prefab's copy of the object it was added to; references to it in this file follow it, through a stand-in |
| removed component / GameObject | deleted from the prefab |

What stays on the instance, as in Unity:

- **The root's name and placement.** Its position, rotation and euler hint, plus a UI root's
  pivot, anchors, size and anchored position. They say where this copy is, not what the prefab
  is.
- **A change that refers outside the prefab**, such as another object of the scene. A prefab
  cannot name it.
- **A stale override**, for a field or object the prefab no longer has.
- **A change to a packed number list**, one element of a hex-encoded array. Apply it in Unity.

Each refused change is reported with its reason.

**Every apply verifies itself.** Before anything is returned, the file is re-read against the
edited prefab, and every object of the instance's hierarchy is compared with how it loaded
before: its place, type and every field, with references written as what they point at. Any
difference throws, and nothing should be saved. Two loads compare equal when they differ only in
the order a file lists a map's keys, or in a missing reference versus an empty one. Neither
affects what loads: fields are found by name, and both references are null.

Measured: apply-all on **every one of the 498 placed prefabs with changes** in the project's
Unity-written files.

| | result |
|---|---|
| applied, and the instance loads identically afterwards | **498 / 498** |
| changes applied | 7,376 |
| root name/placement overrides left on their instance | 8,160 |
| left on the instance: the prefab no longer has that field / that object | 555 / 529 |
| left on the instance: refers to an object outside the prefab | 110 |
| left on the instance: packed number list | 16 |

`PrefabInstanceEditingTests` pins each kind of apply on the miniature project and runs every
fourth project instance.

### Added-component order: a loader bug the check found

Unity shows a GameObject's added components in the order of its instance's
`m_AddedComponents` list, a prefab's own additions before the ones a file placing it adds on
top. Both the editor and the game loader (`SceneInstantiator`) attached them in file order, so
with two of one type, `GetComponent` could return the wrong one. It surfaced on
`TeamCrystal.prefab`: two FMOD emitters, max distance 750 and 250, swapped. Both now order by
`PrefabGraph.AddedComponentRank`.

## The tool

```bash
cd Port && dotnet build src/CosmicShore.AssetTool
T=src/CosmicShore.AssetTool/bin/Debug/net10.0/cs-asset   # cs-asset.exe on Windows

$T roundtrip                                          # verify the whole project survives a rewrite
$T list   Assets/_Scenes/Authentication.unity         # hierarchy paths + fileIDs
$T get    <file> "Canvas/Safe Area/Status Text" m_Name
$T set    <file> &144074275 m_text "HELLO: world"     # quoted correctly for you
$T set    <file> Child --component Transform m_LocalPosition "{x: 1, y: 2, z: 3}"
$T create <file> "New Object" --parent "Canvas"
$T delete <file> "Canvas/Safe Area/AuthPanel"         # --dry-run to preview

$T components <file> Box                              # list a GameObject's components
$T add    <file> Box BoxCollider                      # built-in
$T add    <file> Label TextMeshProUGUI                # package: adds CanvasRenderer, makes a RectTransform
$T add    <file> Box CosmicShore.Utility.NetcodeHooks # project script (short name works when unique)
$T add    <file> Box --like &7131408450299668755      # copy an existing component
$T remove-component <file> Box --component BoxCollider#2

# Inside placed prefabs (written as that instance's overrides)
$T overrides <file>                                   # what each placed prefab overrides, removes, adds
$T set    <file> GameCanvas/ConnectingPanel m_IsActive 0
$T set    <file> GameCanvas/ConnectingPanel connectingCamera "@GameCanvas/ConnectingPanel/Camera:Camera" \
              --component ConnectingPanelController     # @object[:Type[#n]] is a reference to it
$T revert <file> GameCanvas/ConnectingPanel m_IsActive
$T add    <file> GameCanvas/ConnectingPanel BoxCollider  # an added component
$T delete <file> "GameCanvas/ConnectingPanel/Safe Area/Level Preview"   # a removed object
$T instantiate <file> Assets/_Prefabs/Spacevessels/Components/Jet/VesselJet.prefab \
              --parent Holder --name Jet --position 0,1,0

# Apply to prefab (writes the prefab AND this file; --dry-run shows both diffs)
$T apply  <file> GameCanvas/ConnectingPanel m_IsActive       # one field
$T apply  <file> GameCanvas/ConnectingPanel --component BoxCollider   # an added component
$T apply  <file> GameCanvas/ConnectingPanel                  # that object's overrides
$T apply  <file> GameCanvas --all                            # everything the instance changes

$T schema                                             # measure the script serializer against the project
$T serialization-audit [path...] [--json]             # what Unity reads vs what Amoebius's loader reads
$T addall                                             # smoke-test adding every component
```

Every write prints the line diff it made. Before saving, it parses its own output back and
refuses to write unless that output reads back as the edited content. **Close the scene in
Unity before editing it from here**: Unity does not merge external edits into a scene it has
open, and saving from Unity afterwards would overwrite them.

## Not verified yet

- **Nothing written by this path has been opened in the Unity Editor.** The checks are
  structural: Unity's own files round-trip exactly, and edits follow the same file invariants
  and formatting. A file whose edits Unity loads, re-saves and leaves unchanged would be the
  real proof. That is the first thing to do with an editor open:
  `create` + `add` + `set` on a copy of a scene, open it, save it, diff it. The same goes for
  prefab instance edits: `instantiate`, an override, an added component and a removed object,
  then open, save and diff. And for apply: `apply --all`, then reserialize both the scene and the
  prefab in Unity and diff them, and check the Overrides dropdown shows nothing left but the
  placement.
- `.meta` files are not covered (the single-document format). That doesn't matter yet,
  because no operation here creates a new asset file. Creating new assets will need it.

## Not built yet (in rough dependency order)

1. **`[SerializeReference]` fields.** These are skipped today. They need the `references:`
   block Unity writes (`rid`, `type: {class, ns, asm}`).
2. **New asset files.** A new prefab, material or ScriptableObject needs a `.meta` with a
   fresh guid.
3. **Live editing in the Player.** Select an object in the running game, change a field, save
   back through `UnityAssetEditor`. This needs a fileID ↔ live-object map at scene load. The
   instantiator already knows both sides.
4. **Apply to an inner prefab, and unpack.** Apply writes to the outermost prefab only (Unity's
   default). Unity can also apply to a prefab nested deeper, or unpack an instance into ordinary
   objects; neither is built.
5. **Editor UI.** Hierarchy, inspector (reflection over `[SerializeField]`), selection,
   transform gizmos, undo (snapshot `UnityYamlFile` documents, or invert operations).
