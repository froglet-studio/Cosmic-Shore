# Authoring: editing Unity content without the Unity Editor

**Status (2026-10-01):** foundation landed. The port can now WRITE the project's scenes,
prefabs and assets, not only read them. There is a command-line tool (`cs-asset`) for
creating, editing and deleting objects. There is no visual editor yet (no hierarchy panel,
inspector or gizmos); see "Not built yet" below.

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
  `create` + `set` on a copy of a scene, open it, save it, diff it.
- `.meta` files are not covered (the single-document format). That doesn't matter yet,
  because no operation here creates a new asset file. Creating new assets will need it.

## Not built yet (in rough dependency order)

1. **Components.** `AddComponent(go, script guid)` writes a MonoBehaviour with the script's
   serialized defaults. The field list comes from the C# type via
   `UnityYamlWriter.SerializedFields`, which already mirrors Unity's field rules. Built-in
   components (Collider, Renderer) need per-class templates harvested from existing scenes.
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
