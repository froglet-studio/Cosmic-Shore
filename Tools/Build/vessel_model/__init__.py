"""
vessel_model - build Cosmic Shore vessel models headlessly with Blender-as-a-module (bpy).

A vessel model here is ONE skinned mesh on an armature, with four element shape keys
(charge / mass / space / time) that really move the hull, the takes its animator or puppetry
code binds by name, and four material slots (Domain / Body / Window / Engine, or the names the
vessel's prefab already consumes), exported to binary FBX in the conventions the shipped artist
files use, then verified by three independent readers (the repo's FBX codec, assimp, and a
Blender re-import) and rendered to a contact sheet so a session can LOOK at it.

Modules:
  fbx_probe   - measure any FBX (hierarchy, rest TRS, skin, shapes with travel, takes, slots)
  forms       - the modelling library: lofts, lathes, wings, pods, panels, mirroring, finishing
  rig         - armatures from a bone spec, rigid/blended skinning from part vertex groups
  morphs      - element shape keys as deformation functions over vertex-group masks
  takes       - pose and motion takes (one Blender action each, exported as one stack each)
  materials   - the four slots and the per-face painting rules
  export      - the FBX recipe + the blend-shape-curve strip + the three-reader verification
  render      - Cycles contact sheets (views, morph extremes, pose takes) in a studio clay look
  build       - the CLI: spec -> model -> FBX -> verify -> render

Everything runs with the plain system python3 that has bpy installed (`import bpy`), never a
Blender GUI. The skill that drives it: .claude/skills/vessel-model/SKILL.md.
"""
