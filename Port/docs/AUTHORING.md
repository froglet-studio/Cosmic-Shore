# Authoring: editing Unity content without the Unity Editor

**Status (2026-10-01):** the port can WRITE the project's scenes, prefabs and assets, not only
read them. The command-line tool (`cs-asset`) creates, edits and deletes GameObjects, and adds
and removes components: project scripts, Unity built-ins (colliders, renderers, Rigidbody …)
and package components (uGUI, TextMeshPro …). There is no visual editor yet (no hierarchy
panel, inspector or gizmos); see "Not built yet" below.

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

### Two bugs this found in existing code

- **`ScriptTypeMap` lost components whose file name differs from the class only in case.**
  `MinigameHUDView.cs` holds `MiniGameHUDView`. Unity accepts that, but the port's resolver
  didn't, so the HUD view component silently never loaded in the Player. Resolution now falls
  back to a case-insensitive match.
- **`YMap` returned stale values after a write through `Entries[i]`.** A map of 8+ keys
  answers lookups from an index, which a direct list write doesn't update. `YMap.SetAt` is the
  safe write; the editing code uses it.

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
$T schema                                             # measure the script serializer against the project
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
  `create` + `add` + `set` on a copy of a scene, open it, save it, diff it.
- `.meta` files are not covered (the single-document format). That doesn't matter yet,
  because no operation here creates a new asset file. Creating new assets will need it.

## Not built yet (in rough dependency order)

1. **`[SerializeReference]` fields.** These are skipped today. They need the `references:`
   block Unity writes (`rid`, `type: {class, ns, asm}`).
2. **Prefab instance edits.** Changing a field on an object inside a nested prefab means
   writing an `m_Modifications` override, not editing the stripped stand-in. `PrefabGraph`
   already applies these overrides when reading, so the inverse lives next to it.
3. **New asset files.** A new prefab, material or ScriptableObject needs a `.meta` with a
   fresh guid.
4. **Live editing in the Player.** Select an object in the running game, change a field, save
   back through `UnityAssetEditor`. This needs a fileID ↔ live-object map at scene load. The
   instantiator already knows both sides.
5. **Editor UI.** Hierarchy, inspector (reflection over `[SerializeField]`), selection,
   transform gizmos, undo (snapshot `UnityYamlFile` documents, or invert operations).
