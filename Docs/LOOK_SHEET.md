# The look sheet: the real game, seen without a person

**What it is.** A session cannot open the Unity editor, so every look in this game has been judged by a person, in
rounds, after the commit: 169 of the 242 sections of `UNITY_VERIFICATION_CHECKLIST.md` are red, 60 menu-redesign
sections were never run in the editor, and six hulls wait for a flight (`Docs/SKILL_TRAINING_PLAN.md` 2.2). Vessel
models and ability icons got better because each got a SHEET a session looked at before anyone else did. The look
sheet is that sheet for the rest of the game: the real renderer, captured to PNGs headless, gated (a blank frame
fails), laid out on one contact sheet, attached to the branch instead of "NOT RUN IN EDITOR".

**Status (2026-11-07): built, not yet run in Unity.** The C# was read for the APIs it calls (the soak launchers', the
smoke test's, `ScreenSwitcher`'s) and could not be compiled in the session that wrote it (no .NET SDK reachable; the
checklist entry says so). The first run on a machine with Unity is the proof, and the two capture paths are compared
there (section 4).

## 1. The pieces

| Piece | What | Where |
|---|---|---|
| The capture | Renders every screen-drawing camera in depth order into one RenderTexture, then every Screen Space Overlay canvas (the menus, the HUD) through a temporary UI camera, and writes the PNG plus a `manifest.jsonl` record. No Game view needed; batchmode with a graphics device is enough | `Assets/_Scripts/Editor/Look/LookSheetCapture.cs` |
| The launcher | `-executeMethod CosmicShore.Editor.LookSheetLauncher.Run`: boots from Bootstrap offline (the privacy overlay closed unanswered, the username step skipped, as the soak runners do), then either opens menu screens through `ScreenSwitcher`'s own navigation and captures each at PC size and phone scale, or starts a mode's shipped soak runner (Switchback, Thresher Time: every seat the AI, real time) and captures the game camera at given game seconds after the turn starts. Also `FrogletTools > Build > Look Sheet` | `Assets/_Scripts/Editor/Look/LookSheetLauncher.cs` |
| The gate | `verify.py`: the file exists and decodes, the size matches the record, the frame is not blank (97 % one colour, black, or white fails), every manifest record without an error names a passing file. Negative controls in `--self-test` | `Tools/Build/unity_look/verify.py` |
| The sheet | `sheet.py`: every capture on one labelled contact sheet, two columns, `--beside <image>` for a picked Frogma board or an approved earlier sheet | `Tools/Build/unity_look/sheet.py` |
| The driver | `look.py menu HOME,HANGAR` / `look.py mode switchback --times 5,15,30` / `look.py verify <folder>` / `--print` for the command alone | `Tools/Build/unity_look/look.py` |
| The CI job | `workflow_dispatch`: GameCI's image (the digest the hosted-compile branch pins), the `unity-ci` environment's licence, Xvfb, the Library cache shared with the guard, the sheet uploaded as an artifact | `.github/workflows/look-sheet.yml`, `Tools/CI/unity_look_in_container.sh` |

Output: `Logs/LookSheet/` (git-ignored): `menu_home_pc.png`, `menu_home_phone.png`, `mode_switchback_t5.png` ...,
`manifest.jsonl`, `summary.json`, `sheet.jpg`. A sheet that is kept goes under `Docs/Look/<what>_<date>.jpg` with
the branch, the way the vessel sheets live under `Docs/VesselModels/sheets/`.

## 2. Run it

**In the editor:** `FrogletTools > Build > Look Sheet > Menu screens` (Home, Hangar, Port, Profile, Codex) or
`> Switchback, the Dolphin's AI` / `> Thresher Time`. Leave the editor alone while it runs; the files land in
`Logs/LookSheet/`. Then `python3 Tools/Build/unity_look/look.py verify Logs/LookSheet` and
`python3 Tools/Build/unity_look/sheet.py Logs/LookSheet`.

**Headless, on a machine with Unity:**

```
python3 Tools/Build/unity_look/look.py menu HOME,HANGAR,PORT,PROFILE,CODEX
python3 Tools/Build/unity_look/look.py mode switchback --times 5,15,30 --intensity 1
```

`look.py` finds the editor (`UNITY=`, `/opt/unity/<ProjectVersion>/Editor/Unity`, `unity-editor`, `Unity`), runs it
with `-batchmode` (never `-nographics`: nothing would render; never `-quit`: Unity would exit before Play), then the
gate and the sheet. On a machine with no GPU: `xvfb-run -a -s "-screen 0 1920x1080x24" python3 ...` (Mesa's software
renderer draws a frame in seconds). One Unity per project: close the editor or use another checkout.

**In CI:** Actions > Look sheet > Run workflow (kind `menu` or `mode`, what, times, intensity). It needs
`UNITY_EMAIL` and `UNITY_PASSWORD` (optionally `UNITY_SERIAL` / `UNITY_LICENSE`) in the `unity-ci` environment, the
same secrets the hosted compile on `claude/ci-hosted-unity-compile` needs (`Docs/BUILD_AND_DELIVERY.md` 10.1 on that
branch). Without them the job stops with a warning. The sheet is the run's artifact `look-sheet-<kind>-<run>`.

Launcher arguments: `-lookScreens HOME,HANGAR,PORT,STORE,CODEX,ARK,PROFILE,ARCADE,TOYBOX,ARENA` (any subset),
`-lookMode switchback|thresher-time`, `-lookTimes 5,15,30`, `-lookIntensity 1`, `-lookOut Logs/LookSheet`,
`-lookSize 1920x1080`, `-lookPhone 1600x900` (two times the phone's 800x450 scale), `-lookSettle 1.0` (seconds a
screen gets before its capture), `-lookQuit` (exit 0 when every capture was written, 1 otherwise, 3 when the run
could not start).

## 3. What it does and does not show

It shows the real renderer: the shaders, the post-processing, the HUD, the hull at the size the game draws it. It
does not show feel, motion between frames, audio, a player build (this is the editor, Mono), a phone's real layout
(the phone-scale capture is the PC layout at the phone's aspect: the game picks its phone canvases by platform, not
by resolution), or a URP camera stack's overlay cameras (`Camera.Render()` draws the base camera only). A sheet
never replaces the verify rule or the checklist's hand test; it replaces the rounds in which the owner was the first
to see a thing at all.

## 4. The first run (the checklist entry)

1. `FrogletTools > Build > Look Sheet > Menu screens` in the editor. Expect five pairs of PNGs and, beside each, a
   `*_gameview.png` from `ScreenCapture`. **Compare the two**: if the render-to-texture capture lacks the menu's UI,
   the overlay-canvas retargeting needs the canvas layer on its children too; if it lacks the world, the menu camera
   is a stacked camera (see section 3). The Game view capture is the yardstick.
2. The same headless: `python3 Tools/Build/unity_look/look.py menu HOME` with the editor closed. Expect the gate
   `ALL OK` and `sheet.jpg`.
3. **Negative control:** run with `-nographics` added by hand. Expect every capture to fail the `not-blank` gate
   (black frames) and exit 1. A gate that passes a black frame is broken.
4. `look.py mode switchback --times 5,15,30`: three frames of the Dolphin's race. Expect the hull, the prisms and the
   HUD at the game's camera distance.
5. In CI: dispatch `Look sheet` with `menu` / `HOME,HANGAR`. Expect the artifact with the sheet. If the frames are
   black under Xvfb, the image's Mesa cannot run URP's shaders at that GL level: then a GPU runner is the answer (the
   Compute VM, or a self-hosted runner that meets `BUILD_AND_DELIVERY.md` 10's security rules).

## 5. The rounds (Docs/SKILL_TRAINING_PLAN.md 3.1)

| Round | What | Status |
|---|---|---|
| R0 | The real editor compiles every PR: `claude/ci-hosted-unity-compile` (the guard on GameCI's image) plus the licence secrets | that branch is written and recorded its first hosted runs; it waits on the secrets and a merge (Shombith) |
| R1 | One headless screenshot of Menu_Main's Home, read by a session | built 2026-11-07, not yet run (section 4) |
| R2 | A vessel in its Time game flown by its AI, six frames | built for Switchback and Thresher Time through their shipped soak runners; every card once the per-mode smoke test (PR #1754) lands and its driver is called here instead |
| R3 | The UI parity sheet: the built screen beside its picked Frogma board | `sheet.py --beside` is the layout; the element-box diff is `/ui-redesign-tracker`'s next step |
| R3b | The same from an Android development build on a named phone | not started |
| R4 | A sheet is a `/ship` gate for a PR touching a visual path | after R1-R3 have run a few times |

## 6. Built on, not beside

- `claude/ci-hosted-unity-compile` (2026-11-01): the guard's Unity compile on hosted runners, GameCI's image,
  licence activation in `Tools/CI/unity_compile_in_container.sh`. The look-sheet container script repeats that
  activation; when the branch lands, the two should share one helper.
- `claude/launch-mode-smoke` (PR #1754): `ModeSmokeLauncher` / `ModeSmokeDriver` play every rostered card from
  Bootstrap with the AI on every seat. The look launcher's boot-gate handling is the same as theirs (a fourth copy
  of what the smoke PR already proposes to share); its mode case uses the two shipped soak runners today and should
  call the smoke driver for any card once it lands.
- `SwitchbackSoakLauncher`, `ThresherTimeSoakLauncher` (Yash): the `-executeMethod` shape, the offline preference,
  the exit-after-Play watchdog.
- `Docs/claude/VIDEOS_AND_PREVIEWS.md`: Amoebius's clip recipe stays the way to show motion and order of events; the
  look sheet shows the final look, which Amoebius cannot.

Affects: Shombith's tooling and CI (new files only; the `unity-ci` environment's secrets are his decision, D1 in the
plan); Yash's soak runners (read, called, never changed).
