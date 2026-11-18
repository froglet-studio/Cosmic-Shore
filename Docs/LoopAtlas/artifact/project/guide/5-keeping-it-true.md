# Keeping it true

The atlas is only useful while it matches the game and the plan.

- The loops above the kit, the three layers, the holes and the proposals are written by hand in `Docs/LoopAtlas/loop_model.json`.
- The kits come from `Docs/FleetCodex/abilities.json`, and each vessel's games, steps and arenas from `Docs/ElementalLoop/STATUS.md`. Fix those at their source.
- `python3 Tools/Build/loop_atlas.py` validates the model and rebuilds these files; `--check` fails when they are stale.
- When a decision lands: move the loop's `in` from `x` to `px`, or from `px` to `npx`, and move its note to the layer it now belongs to. Delete a hole when it is closed, with the source that closed it.
- A proposal that is accepted becomes part of the plan; one that is thrown out is deleted, not parked.
- Garrett asked for the atlas on 2026-11-18. The Proposed layer is Claude's and nobody has accepted any of it yet.
