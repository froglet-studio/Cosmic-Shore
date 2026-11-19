# The Code Atlas: zoom from a loop into the code, down to every value

Garrett, 2026-11-19: "the fractal structure combines with semantic zooming containers allows for unlimited information.
lets get the entire codebase, and every value, inspectable by zooming into the right place. one trick is to have non
unique locations i.e. equivalent locations. this allows you to need no links because you can zoom in instead ... a
graph can always be forced into a tree if you split up its nodes into non unique nodes that appear in more than one
place on the tree. e.g. a loop can be unraveled into a chain of infinite length, but that chain is contained on a
fractal structure so it is held in a finite area."

And on pictures: "onezoom also uses imagery [that] follows the player up or splits on the way down. our game has a lot
of images already so those images could be used in a similar fashion that act as imagery for the highest levels."

This is how the Loop Atlas map (https://claude.ai/artifact/8XkvFKuTFeEpRoLfp8QQpw) does that.

## What you see

Every loop on the map that some code implements has one more ring: **Code**. Zoom into it and you are in the code
tree. Each ring is one node of the repository (a folder, a file, a type, a member, an asset, a GameObject, a component,
a serialized value, a doc section, a picture) and holds:

- what it is made of (a folder's files, a type's members grouped as serialized fields, constants, methods..., an
  asset's objects, an object's values);
- a box per kind of **neighbour**: the field's type, the type's base and derived types, the assets made from a script,
  what an asset refers to and what refers to it, the types a type mentions and is mentioned by, the loops it serves,
  and **Lives in**, the node that contains it;
- its card (the top slot), which says what the node is: kind, name, value, type, signature, tooltip, header, path:line.

A neighbour in a box is **the node itself**, not a link: zooming into it shows the same record and the same rings as
every other place it appears. So nothing on the map needs an edge, and every path through the graph is a path down the
tree. A cycle (a type holds a field whose "Lives in" is the type) becomes a chain that goes on for ever, and it fits,
because each ring is a fixed fraction of the ring around it. Try it: Squirrel > Code > a type > Fields > a field >
Lives in > the type > Fields > ...

Every ring's loop also holds **The whole codebase** (Folders, Namespaces, Loops), so all of the repository is reachable
from any loop, and every loop's Code ring holds the loop above and the loops inside, so the loop tree is reachable
from inside the code too.

**Pictures**: every first-party picture (2,366 of them: card backgrounds, pilot portraits and scenes, icons, UI art) is
a node, and is the imagery for whatever uses it. An asset shows the picture it references, a type shows its instances'
pictures, a loop shows its code's, and a ring with no picture of its own borrows its children's, a few from each in
turn, best first (big illustrations before interface bits and placeholders, `image_score` in the tool). On the way up
a picture follows its node into the parent; on the way down the parent's pictures (split in quarters when the ring is
big) fade as the children come up showing their own.

## The data model: canonical nodes, placements made while you zoom

`Tools/Build/code_atlas.py build` stores the graph **once**. A record per node:

| Field | Meaning |
| --- | --- |
| `k`, `n` | kind and name |
| `p`, `c` | containment parent and ordered children (folders, files, types, members, objects, doc sections, loops) |
| `ck`, `cn` | one letter per child's kind (`S` serialized field, `M` method, `t` type, ...) and, where the id does not say it, the child's name, so a ring is grouped and labelled before its children load |
| `r` | relations, each a list of node ids, stored on both ends (`base`/`derived`, `type`/`usedBy`, `uses`/`usedByType`, `mentions`/`mentionedBy`, `script`/`instances`, `refs`/`refBy`, `assetRefs`/`assetRefBy`, `source`/`instancedIn`, `namespace`/`types`, `code`/`loops`) |
| `f` | an object's serialized values: `[key, value, referenced node?]` rows, every value of the YAML (lists past 64 items are counted, not dropped) |
| `im` | the node's pictures, indexes into the thumbnail sheets |
| `path`, `l`, `ty`, `v`, `sig`, `tip`, `hd`, `ser`, `mods`, `tk`, `uc`, `wh`, `lines` | where it is, its type, value, signature, tooltip, header, serialized or not, modifiers, member kind, Unity class, picture size, line count |

Ids are canonical and readable, the prefix is the kind: `d:` folder, `f:` file, `n:` namespace, `t:` type, `m:` member,
`o:<file>#<fileID>` Unity object, `v:<file>#<fileID>|<key>` value, `s:<file>#<slug>` doc section, `L:<loop>` loop,
`code` the root. A value is a row of its object, so the 1.45 million values cost no record of their own.

**Placements** are never stored. The page makes them as you zoom: a placement is (node id or box, parent placement,
position in the parent). Many placements hold the same id; they are equivalent. A box with more than 18 things is split
into boxes by name, so every ring stays readable at any size.

## What is extracted (2026-11-19, bleeding-edge via PR 2272's branch)

186,652 nodes: 3,943 C# files (6,067 types, 95,533 members), 2,311 assets, 670 prefabs, 112 scenes (13,328
GameObjects, 48,741 components, 1,048 prefab instances with their overrides), 682 docs (10,650 sections), 2,366
pictures, 177 loops, and 1,447,568 serialized values. 120 MB of JSON, 24 MB gzipped with the thumbnails. About three
minutes on a laptop core.

How it reads each part, and what it does not do yet:

- **C#**: a light structural parser (comments and strings masked, braces tracked): namespaces (block and file-scoped),
  types with generics, bases and nesting, fields (serialized or not, initial values, `[Header]`, `[Tooltip]`),
  constants with their literal, properties, methods with signatures, events, enum values. Third-party folders are
  files only. Not a compiler: a type name is resolved to the closest namespace when two share it, and method bodies
  are read only for which first-party types they mention.
- **Unity YAML** (`.asset`, `.prefab`, `.unity`): every document; GameObjects nest through their transforms;
  MonoBehaviours resolve their script through the `.cs.meta` guid; every reference resolves to the object or file it
  names; prefab instances keep their source prefab and every override. Documents over 2 MB (baked meshes) are
  summarised, not read.
- **Loops**: the Loop Atlas model's loops and every vessel and verb. Linked to code automatically from every repo path
  a loop cites (`src`, a verb's `source`, a vessel's doc), each vessel's class asset, ability map and the types named
  after it, and the arcade cards the map names; and by hand in `Docs/LoopAtlas/code/links.yaml` (globs allowed). A
  hand link that matches nothing fails `build --strict`.
- Not yet: Shader Graph, materials' properties beyond their YAML values, animation curves read as curves, the
  Amoebius port (`Port/`), `Tools/`. Each is one more pass in the tool.

## Loading: nothing until you look at it

`index.json` (a few KB: shard count, root, each loop's pictures, which loops have code) loads with the page. Records are
in 140 shards by `fnv1a(id) % shards`; a shard loads the first time a ring inside it is big enough to draw (gzip as
base64 text, since claude.ai serves no `.gz`; the page decodes and inflates it with `DecompressionStream`). A thumbnail sheet loads
the first time a picture on it is drawn. Zooming one path from a loop down to a value touches a handful of shards.

## The camera: no floor

The loop map keeps one world camera. Inside a Code ring, once it covers the view, the page switches to an **anchored**
camera: the ring the view is in and that ring's circle on screen. Every other circle is computed from the anchor (down
by multiplying, up by dividing), and the anchor moves to the child that covers the view, or up to the parent when
nothing does. Numbers stay near screen size however deep you go, so an unrolled cycle can be followed for as long as
you like. Zoom out past the Code ring and the loop map's camera takes over again where it was. Clicks fly to a ring,
arrow keys walk the tree (Up to the ring above, Down into the first child, Left and Right through siblings), and the
breadcrumbs show the path from the loop.

## Building and publishing

```
python3 Tools/Build/code_atlas.py self-test        # the parser, linker and hash refuse what they must
python3 Tools/Build/code_atlas.py build --strict   # writes Docs/LoopAtlas/code-out/ (ignored by git)
python3 Tools/Build/loop_atlas.py build --html /tmp/LoopAtlas.html
```

Publish the page with `code-out/` beside it as `code/` (the `/loop-atlas` skill, section 6, has the call). CI
(`.github/workflows/code-atlas.yml`) runs the self-test and a strict build on changes to the tool, the links or the
page, and every night on bleeding-edge, and keeps the output as a workflow artifact; publishing stays a session's step,
because only a session can write to claude.ai.

## Next

- Search the code from the map's find box (`names.json` is already written) and fly through the tree to the hit.
- More automatic loop links: a game mode's controller and scene from its card, a vessel's prefab and HUD from its class.
- Shader Graph nodes, the Amoebius port and `Tools/` as more passes; method bodies as call edges.
- Pictures rendered for nodes that have none (a prefab's look, a vessel's model), the way `/cardart` renders cards.
