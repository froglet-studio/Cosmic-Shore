# Lessons (append one block per round; the owner's words first)

## 2026-10-24 - the audit, and round 1 (Manta, Urchin)

Garrett: "the current icons range from decent to terrible." "Angular is probably good." "The flower
probably owns it. I don't see what value the redundancy could provide." "[Hull silhouettes] are fair
game where appropriate. Dolphin uses its jaw silhouette to good effect in its space ability icon."
"We will improve your icon development skill by raising the floors in our existing icons."

1. **Fifty-five icons came from twelve scripts and three drawing engines**, and the ability row
   disagreed with the style foundation on its most basic question (round vs angular). The language
   was never written down; each script cited "the house icon language" as one sentence in the
   previous script's docstring. Writing it down (ICON_LANGUAGE.md) and putting one engine under it
   is most of the fix.
2. **The gates had to be calibrated on the fleet before they meant anything.** First draft (12 px
   margin, coverage ≥ 0.14, 7 px thin threshold) failed 54 of 56 including everything Garrett called
   decent. The fleet draws to a ~4 px margin because the lockup scales by authored size; the Thresher
   set covers 0.12-0.16; round-capped strokes are ~5 px. Calibrated: margin 6, coverage 0.10-0.45,
   thin < 5 px share ≤ 0.35, tight-gap boundary share ≤ 0.25. Now the Thresher fails only on what is
   really wrong with it (chain-link and spike gaps, debris specks), the Shrike on being too thin
   (coverage 0.08-0.10), the Starlings' cards on looking alike at 24 px.
3. **Gates are a floor, not a verdict.** Urchin Slip (three circles) and Track Projector (a ladder)
   passed every gate and named nothing. The sheet at 40 px, read by a person, is the judgement.
4. **`Urchin_ChargeRing.png` is a gauge, not an icon.** The audit called a bare ring "generic"; it is
   the hold-charge ring the HUD draws round the Charge card. Read `author_<hull>_hud.py` before
   judging a sprite in the icon folder.
5. **Prisms in a row fuse into a zigzag at 40 px** unless the gap is ≥ 6 px (Yastri round 1: two
   prisms overlapped by 3 px and read as a chain). Two big prisms beat three small ones.
6. **The smallest feature of a silhouette is what gets named.** The Manta's cephalic lobes, drawn
   tall, made the Soar icon a fox's head at 40 px (ears + snout). Flattened to short lobes on a blunt
   head, it is a ray. Remove or flatten; shrinking keeps the read.
7. **Sleepers across a receding track read as a stacked tree.** A single-track rail is a solid taper
   with a centre groove cut out - the negative-space rule.
8. **The sheet's BEFORE must come from HEAD, not disk**, or the first write erases its own comparison
   (the first `--check` after writing drew new vs new). `author.py committed()` reads `git show`.
9. **A spent generator must stand down on the files the kit now owns and keep the rest**:
   `author_manta_icon_placeholders.py` keeps the metas and folder metas; `author_urchin_hud.py`
   keeps the metas, the HUD variant and the charge-ring gauge. Both `--check` clean, and a re-run
   cannot put the placeholder back.
10. **Soar is the weakest of the eight** (a ray at 60, a delta at 40); it is the one to ask about
    first. Kabloom (blast with a hollow core), Sting (octagon bomb), Track Projector (road to the
    horizon) and Trail Rider (ball on the ribbon) are the ones that moved most.
