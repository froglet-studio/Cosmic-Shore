# FrogletTools, natively in Prisma

Every FrogletTools menu item shows on Prisma's EDITOR > TOOLS page. A tool with an entry in
`tools.json` here has a native version, a `cs-asset` command that does its job on the project
files without Unity, and its card shows **RUN**. Any other tool shows **BUILD**: it opens an agent
chat that builds the native version, under a scope that may change only
`Port/src/CosmicShore.AssetTool`, `Port/tests/CosmicShore.AssetTool.Tests` and this folder
(`ClaudeChat.Scope.Tool`, enforced by deny rules).

`tools.json` entry:

```json
{ "menu": "FrogletTools/Validation/Example", "args": ["example-audit"], "writes": false, "summary": "one line" }
```

`args` are the `cs-asset` arguments RUN passes. A writer's changes stay in Prisma's workspace
until they are committed on the GIT page.

## Recipes

One section per native tool: what it checks or changes, how it maps to the Unity tool, and what
it cannot do without the editor.
