<!--
Fill the headings in; delete the comments. The Verification status section is read by the
/qa-backlog scan to find work nobody has opened in Unity, so say plainly what did NOT run.
-->

## What and why

<!-- One paragraph. What a player or a contributor gets, and the defect or gap it closes. -->

## Systems touched

<!-- One line per system (vessel / arcade mode / ecology / party-presence / scoring / prism
     animation / tooling / docs). Name the LOCKED systems explicitly: they get a closer review. -->

## Verification status

<!-- Be literal. "Compiles" is not "runs". Three lines, keep whichever apply:
     - Ran in the Unity editor: <what you opened and saw, scene and mode>.
     - Ran offline: unity_refcompile (player config), edit-mode suites, Tools/Build gates,
       generator --check, sim harness. Name each with its result.
     - NOT run (a human must open Unity): <the exact scene, mode and thing to look at>.      -->

## Tool output

<!-- Only if this branch adds or changes an editor tool, wirer, migration or generator.
     Did the tool's OUTPUT (scene / prefab / SO) land on this branch, or is it still in a working
     tree? See CLAUDE.md "A tool's OUTPUT is the deliverable". Delete this section otherwise. -->

## Follow-ups

<!-- Named, with the doc or backlog row that carries each one. Not "TODO". -->
