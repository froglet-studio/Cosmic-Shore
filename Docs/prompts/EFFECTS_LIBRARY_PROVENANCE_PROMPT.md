# Prompt — identify `Assets/Effects Library/`, then salvage the 1 % of it that is live

Paste everything below into a fresh session.

---

`Assets/Effects Library/` is **8.2 MB of VFX content with no vendor, no version and no licence**, and
it ships. Its only child folder is named `Froglet Stuff/`, which reads first-party — but the parent is
named like an imported pack and the render assets inside carry an `FE_` prefix
(`FE_ForwardRenderer`, `FE_UniversalRP-HighQuality`, `FE__GlobalVolumeProfile`,
`FE_UniversalRenderPipelineGlobalSettings`) that looks like an **import** prefix rather than a Froglet
one.

**The repository cannot answer where it came from.** This clone is shallow (`.git/shallow`, 741
commits, history begins 2026-08-12), so `git log --diff-filter=A` reports the graft-boundary commit.
A full clone would answer *when and by whom*; it would still not answer *was it paid for*.

Read `Docs/THIRD_PARTY_REGISTER.md` §6 and `Docs/THIRD_PARTY_DECISIONS.md` §4 (row 7).

## Part 1 — the human question (do this first; it may end the job)

Ask, in order:

1. **Whoever added the vessel jet FX.** `vfx_Projectile_02.prefab` is nested inside
   `_Prefabs/Spacevessels/Components/Jet/VesselJet.prefab` — that person imported this.
2. **The Unity Asset Store account holder.** If it is a purchased pack, the order history names it and
   the whole question collapses to "vendor the licence and rename the folder".
3. **Whoever set up URP on this project.** The `FE_` settings assets are a render-pipeline
   configuration; if they are ours, `FE` stands for something and the folder is first-party.

If the answer is *"it's a purchased pack"* → vendor its licence text, record the vendor and version in
the register, and skip to Part 3 (the unused 8 MB is still worth removing).
If the answer is *"it's ours"* → rename it out of the third-party neighbourhood and record why.
If **nobody knows** → that is a real answer. Record it as unknown, name who was asked, and take
Part 2, because an unattributable 8 MB drop should not be in a Steam build.

**Do not invent a provenance.** An invented explanation is worse than a gap.

## Part 2 — a full clone will narrow it

Run on a machine that can fetch:

```
git fetch --unshallow
git log --diff-filter=A --follow -- "Assets/Effects Library"
```

That gives the commit, author and date of the import. Combined with the Asset Store order history for
that month, it usually names the pack. It still does not prove entitlement — only a receipt does.

## Part 3 — what is actually live (measured, and it is almost nothing)

Guid-ownership check across every `.unity`, `.prefab`, `.asset`, `.mat` and `.shadergraph`:

| Asset | External references |
|---|---|
| `Froglet Stuff/Prefabs/vfx_Projectile_02.prefab` | **1** — `_Prefabs/Spacevessels/Components/Jet/VesselJet.prefab` |
| `Froglet Stuff/Prefabs/vfx_Projectile_01.prefab` | 0 |
| `Froglet Stuff/Prefabs/vfx_Electricity_01.prefab` | 0 |
| `Froglet Stuff/Prefabs/vfx_Lightning_01.prefab` / `_02.prefab` | 0 |
| `Froglet Stuff/Models/Mountains01.fbx` (860 KB) | 0 |
| `Froglet Stuff/Settings/FE_ForwardRenderer.asset` | **0** |
| `Froglet Stuff/Settings/FE_UniversalRP-HighQuality.asset` | **0** |
| `Froglet Stuff/Settings/FE_UniversalRenderPipelineGlobalSettings.asset` | **0** |
| `Froglet Stuff/Settings/FE__GlobalVolumeProfile.asset` | **0** |

**The four `FE_*` render-pipeline assets are not the live pipeline.** They look exactly like the
project's URP configuration and are referenced by nothing — not by `ProjectSettings/`, not by any
quality level. Confirm that independently (`ProjectSettings/GraphicsSettings.asset`,
`QualitySettings.asset`, and any `Build Profiles` override) before removing them, because a
render-pipeline asset is the one class of file whose absence fails loudly and late.

`vfx_Projectile_02.prefab` pulls exactly two materials — `Flare00_AB_2.mat`,
`DistortedFlare01_AB.mat` — and their textures. **That is the whole live surface: one prefab, two
materials, their textures.** Sizes: `Textures/` 5.4 MB, `Prefabs/` 1.8 MB, `Models/` 860 KB,
`Materials/` 124 KB, `Settings/` 40 KB.

**Salvage, then remove.** `git mv` the live prefab, its two materials and their textures (file **and**
`.meta`, so guids survive — a rename is what breaks references, a move is not) into a first-party
home, verify `VesselJet.prefab` still resolves, then remove the rest in a commit carrying its
reference proof. That is ~8 MB of a shipping build for one jet effect.

Note `VesselJet.prefab`'s plumes are `scalingMode: Hierarchy` particle systems, so their size comes
from the **transform chain**, not from `VesselFXWidth` — CLAUDE.md's `VESSEL_TAIL_AND_JETS.md` entry
records that trap. Moving the prefab must not change its scale; verify the jets on the Urchin (the
hull the trap was found on) as well as a hand-tuned one (Dolphin or Squirrel).

## Definition of done

1. A recorded answer — vendor, or a full-clone import commit, or an explicit **unknown** naming who
   was asked.
2. If unknown or unlicensed: the live subtree salvaged to a first-party path, the remainder removed
   with a guid-count proof, and the `FE_*` pipeline assets confirmed unused against
   `ProjectSettings/` before deletion.
3. `Docs/THIRD_PARTY_REGISTER.md` §0 row 7, §2 and §6 updated with whichever answer came back.
4. Jets verified in the editor on at least two hulls (`/verify-unity`) — or stated plainly that they
   were not.
