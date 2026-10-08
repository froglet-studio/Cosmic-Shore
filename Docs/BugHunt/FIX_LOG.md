# Fix Log

One report per shipped fix, **newest first**. Dates are IST. The workflow and the other docs are
described in [`README.md`](README.md); the problem-class guides are in [`PLAYBOOK.md`](PLAYBOOK.md).

The `Editor-2026-09-26-0447.log` and `Editor-2026-09-26-0510.log` names below are local copies
of Yash's `Editor.log`. The 0510 copy contains the whole 0447 session plus the later sessions.

---

## Overnight sweep 2026-10-08 — handoff rows 23-79

- **Branch:** `cece/loving-shannon-hxpdy0`, one fix per commit (each commit message carries the
  symptom, root cause and fix in full - read it with `git show <hash>`).
- **How they were found:** the open §2.4 / §3.1 / §4 items, then seven parallel read-only hunts
  (vessels, arcade/scoring, ecology, UI, multiplayer/party, data/economy, input/audio/AI), then a
  second round of three (projectiles/AOE/toys/assemblers, the newer modes, FTUE/weekly/menus). Every
  finding was re-traced against the code before it was changed; the ones that were deliberate,
  unprovable without a playtest, or design calls were NOT changed and are listed in the handoff
  (§2.5-§2.7, §3.2-§3.4, §4).
- **Verification:** NOT run in Unity. `bash Tools/Build/unity_refcompile/run.sh` reports 0 errors
  and 0 unverified in project code for the player config, and 0 errors for `--config editor` (which
  compiles the four changed test files; its 4 unverified entries are pre-existing editor-only
  members). Negative control: a planted call to an undefined method in a changed file was reported
  as **1 unverified**, not as an error (the tool buckets CS0103 while some package references are
  unavailable) - so read both numbers. Seven changed files `using` the unobtainable
  `Unity.Services.Multiplayer` (`HostConnectionService`, `PartySessionService`,
  `PresenceLobbyService`, `LobbyPropertyWriter`, `AcceptanceSignalService`, `MultiplayerSetup`,
  `GameDataSO`), so their diagnostics are bucketed, not gated; the report was intersected with
  the diff and **no diagnostic lands on any changed line** in them. The five textual gates
  (`check_enum_member_references --check`, `check_using_directives --check`,
  `check_conditional_compilation`, `check_self_referential_locals --all`,
  `check_console_logging`) pass. Playtest steps are in the handoff under "Playtest items for rows
  23-79".
- **Prefab edit:** row 35 removed an added component from `GameCanvas.prefab` by YAML (the
  `m_AddedComponents` entry, its MonoBehaviour block and the now-unreferenced stripped GameObject
  stub). No scene references either fileID. Open the prefab once in the Editor to confirm it
  re-serializes without a missing-script warning.

| Commit | Fix |
|---|---|
| `e15f000e` | fix(app-state): allow MainMenu → Authenticating for the reconnect boot chain |
| `24c5dd63` | fix(scoring): gate client report RPCs on a running turn and reject NaN volume |
| `4b3a2c24` | fix(flora,names): roll BranchingFlora trunk count once; name generator reaches the last word |
| `e6565c7c` | fix(scoring): an empty domain no longer wins an all-zero tie |
| `afb66abd` | fix(ui): suspended thumb cursor/perimeter no longer throw on their first frame |
| `ada75753` | fix(trail): index the trail with int so a 65,536th prism does not wrap |
| `fe9c2fee` | fix(replay): Play Again no longer misses its fade-in on a scene reload |
| `c2825eb4` | fix(party,net): backend waits use unscaled time so a paused menu cannot stall them |
| `ceabacdd` | fix(presence): a boot-time presence-lobby join failure is retried |
| `a0b01f82` | fix(menu-swap): refuse a vessel swap for a player the sender does not own |
| `1b5c4522` | fix(arcade): cancel InitializeAfterDelay when the controller is destroyed |
| `7f6981f1` | fix(net): clear a human Player's DontDestroyWithOwner when the scene will not adopt it |
| `e68f204f` | fix(end-game): every match paid its placement crystals twice |
| `0c0332f2` | fix(end-game): an in-place rematch shows its end screen again |
| `c95f4046` | fix(pause): pausing a match other players are in no longer freezes time |
| `02b96504` | fix(comeback): keep the card's per-hull starting elements when the turn starts |
| `7511d95e` | fix(results): IsLocalDomainWinner only answers true for the top domain |
| `4708c810` | fix(turn-monitor): only the server's clock ends a networked timed turn |
| `ded3ee7f` | fix(turn-monitor): timed rounds run their full duration |
| `f579cc76` | fix(regatta): placement order agrees with the winner on a tied team total |
| `8c420702` | fix(elements): petal loss no longer reads one level low from float drift |
| `ce645d19` | perf(vessel): edge-trigger the engine flare and the slowed-ship broadcast |
| `7f20a5af` | fix(serpent): the cloak ghost frees its baked mesh and material clones |
| `68bf94d0` | fix(squirrel): tube cleanup never recycles a prism that now belongs to someone else |
| `7e5c950e` | fix(input): a release stops the actions its press started |
| `c848e3d9` | fix(cell): the OnInitializeGame pass no longer wipes a cell the first crystal already bootstrapped |
| `5b2e98c2` | fix(cell): a destroyed cell retires its colony books |
| `e2eaebf4` | fix(prisms): Unity fake-null no longer defeats ??= in the Editor (spindle links, super-shield) |
| `1b7be83c` | fix(ui): GetComponent() ?? AddComponent() replaced where the Editor's fake null defeats it |
| `8a0ccadb` | fix(swarm): stop the swarm loop on destroy; complete the pose job before releasing its inputs |
| `b76e7ab0` | fix(audio): vessel audio follows the pilot when a live hull changes hands |
| `2817eb28` | fix(input): pad and keyboard face buttons always send their release |
| `90ff29f3` | fix(touch): lifting the last thumb releases what the touch was holding |
| `a6480b5a` | fix(audio): an empty drift-event slot warns once instead of erroring every frame |
| `a674ebc4` | fix(profile): an avatar picked before the profile loads is applied, not dropped |
| `d4162289` | fix(friends): a party invite gets its row even when the sender has a pending friend request |
| `9b2ecdb9` | fix(ui): VolumeUI destroys its per-instance material |
| `979dd6ce` | fix(progression): a failed immediate save is retried instead of forgotten |
| `a64d0f6e` | fix(profile): follow the profile repo when it adopts the real cloud record |
| `954b15ed` | fix(cloud-data): progression and stats follow their repositories when the data object is replaced |
| `b11b9448` | fix(persistence): DataAccessor saves atomically, keeps a corrupt file, writes UTF-8 |
| `31a12e09` | fix(hangar): never spend persistent crystals on an unlock that cannot persist |
| `0929fea7` | fix(toys): the daily toy reward is not claimed before the profile has loaded |
| `a7754133` | fix(episodes): refuse a paid token grant until the profile has actually loaded |
| `196bb175` | fix(episodes): do not start a token purchase before the profile has loaded |
| `9c90cc7b` | fix(prefs): SetAvailableProfiles writes under its key, not under the value |
| `d0395c12` | fix(duel): Cellular Duel's round swap is applied on every peer, not only the host |
| `9429df4a` | fix(scoring): legacy metric scorers keep a value per player, not one shared value |
| `52e9ef9e` | fix(stats): LifeFormsInCell has one writer, so a flora death is counted once |
| `52824cb1` | fix(ui): two more non-ASCII UI glyphs that the ALDRICH font renders as tofu |
| `2bb134f6` | chore(enums): give seven serialized enums explicit values (repo rule) |
| `b8ccdb3c` | fix(undertow): the winner banner names the teammate who contributed most |
| `20236b4e` | docs(party): correct the IsPartyClient comment about IsPartyHost |
| `e9ee0c4fa` | Add .gitignore and .gitattributes. |
| `d066ec1f5` | Update .gitignore |
| `5bc28d910` | Initial Commit by JVZ |
| `50d4e1499` | GetGyroHome added |
| `54b3fe76f` | Gyro reset button works, Added Audio Manager, trails and some ScriptableObjects |
| `ff9284e9c` | Update to sync with Garret |
| `16b95199a` | Added GryoShipController JVZ |
| `b8ae2d1e3` | testing push for garret |
| `e5b141466` | Gyro and Ship sync with garret |
| `f5d677543` | minor clean up (altitude) |
| `dd9f14f00` | bunch of tweaks to gyro, camera, and code cleanup |
| `2264b6d0e` | Added UI Design Scenes |
| `63ec44047` | Revert "Added UI Design Scenes" |
| `cc6931e23` | Revert "Revert "Added UI Design Scenes"" |
| `c3b4a9ced` | Trying to merge |
| `c74f4afcd` | working gyro |
| `233197286` | bunch of feeling and diagnostic tweaks and tests |
| `7ed5c9bb1` | project settings git ignore |
| `8b37fbf41` | Revert "project settings git ignore" |
| `50861aef1` | Small code cleanup |
| `4aa0948a2` | coordinate test |
| `7f228a1ea` | diagnostic test, take 2 |
| `45721f2e2` | touch controls |
| `8399f32ca` | aesthetics and controls tweaks |
| `d7bf33c05` | Update gitignore to exclude UserSettings folder |
| `d06122bb0` | Remove files from ignored folder UserSettings from the repo |
| `9a44b8a03` | added a controls switcher |
| `b68674559` | Small cleanup in ShipGyroInputs |
| `ce2d8185b` | added a camera swap |
| `f85e2f3ca` | UI Design 1 and background Music added |
| `27b076c16` | increased field of view and adjusted camera |
| `d2fb2259a` | sync |
| `8ad886b82` | Fixed Audio null ref |
| `dc4507d27` | camera tweaks |
| `e377b2568` | jaws effect |
| `b91842089` | Option Scene Design 1 |
| `a0744b22d` | saved off the old input and cleaned up the new made tank controls the default and changed the button to a jaws effect |
| `66b88bbb0` | fixed the close cam wonkyness |
| `8e9c8998b` | fixed the frame rate glitches by moving everthing from fixed update to update |
| `fe8e79c82` | fixed some tail and camera bugs, added audio to the main scene removed overlay in opening scene |
| `8e52a9e12` | Clean up |
| `3e24d4a3e` | Created Persisent BGAudio and some UI Scene clean up |
| `3f4620f6f` | Created Game scene with Player and Camera Prefabs.  Needs camera work |
| `677480f74` | Add URP package. Put bloom on the menu scene, with Orange just like Grace asked. |
| `6beb948cd` | sync for urp upgrade |
| `3fc12ca67` | caged manta |
| `637d21925` | a ball to play with |
| `67f34fa0a` | slicable random pills fixed the gyro jitter ditched the controller script |
| `e6b7787d2` | material tweaks |
| `de9233d4a` | sync to pull |
| `d6cde5834` | Added AI Enemy and cleaned up |
| `779411eee` | project settings? |
| `4d8dc8c8e` | new shadergraph shader turned off global lighting nixed y offset on cameras visual tweaks on pills |
| `7df1e99b5` | lots more shader graphs |
| `10080c70f` | Score counter |
| `ff07e9063` | cleanup |
| `f527c9870` | button changes |
| `1b12ab2f5` | shader changes and text tweaks |
| `e577fd1c1` | shader tweaks |
| `369b80ba7` | Monday Sync |
| `939bfabcb` | shader tweaks |
| `7917992c4` | cleanup |
| `75186054c` | sync |
| `0f6b90058` | new skybox ship animations |
| `0f08c7c3e` | Get rid of tracked UserSettings file |
| `30273480e` | Some more ProjectSettings.asset bs |
| `26918ed81` | Added Event driven Scoreboard, Mutons, Trails and Player for Intesity System |
| `70874e610` | ship animations |
| `bdc868de9` | Tutorial sync |
| `ae99bd08a` | Tutorial Lastest Sync for Ig |
| `2dd878fae` | block behaviour, collisions and wait timers anti-ailiasing |
| `515a38ed1` | Add nice vibrations. Update icon, splash page, main menu graphics. Haptics test scene. |
| `9d194fd16` | Re-attach post processing profile to game scene. Fix a null pointer. |
| `bc1ccfb0f` | sync |
| `fd7208884` | sync for Garett |
| `a93f3513c` | Added Ai bokeh post process score board |
| `8ca0c02e9` | sync |
| `fdc8391bb` | menu screen bot zoom double tap fix |
| `4941fd034` | Added Final Scene and  high score |
| `cf74c36cd` | Delete Bin directory |
| `feea61be5` | Delete TailGlider_BurstDebugInformation_DoNotShip/tempburstlibs/armeabi-v7a directory |
| `4620ca817` | Delete TankControls_BurstDebugInformation_DoNotShip/tempburstlibs/armeabi-v7a directory |
| `9f7be86c9` | Delete cagedmanta_BurstDebugInformation_DoNotShip/tempburstlibs/armeabi-v7a directory |
| `3cb2cf619` | Delete fruitNinja_BurstDebugInformation_DoNotShip/tempburstlibs/armeabi-v7a directory |
| `d1025c34c` | Delete playball_BurstDebugInformation_DoNotShip/tempburstlibs/armeabi-v7a directory |
| `fa0a1fb24` | Add Intensity Bar. Remove some vestigial code. Add Arial font. Implement Pause/Quit. |
| `5a0d53134` | Tutorial System not fully wired syncing High Score Scene |
| `5f9b9c768` | main menu tweaks |
| `bb7cc8fdf` | Tutorial Code |
| `9eef64f07` | Refined opening scene AI Added the camera change with phone flip wired up the new ship with roll animations |
| `b2bd3c8a6` | throttle scaling |
| `aa28668d8` | Minor fixes post merge |
| `664cd6b64` | new material |
| `b3970e4b1` | syncing |
| `17534f1b6` | Delete UserSettings directory |
| `9e4550a79` | fixed some main scene stuff |
| `a6c8e6447` | Update unity project version |
| `921981f24` | Remove NiceVibrations OlderVersions |
| `46ed5e248` | Quick scene cleanup for WorkingScene |
| `47d735ddf` | Fixing console errors and warnings for WorkingScene |
| `011004d4c` | Small tighting tweaks |
| `5a1d61acf` | Fixing console errors and warnings |
| `39fa1d980` | Various refactors to clean up code and errors. |
| `463ad023b` | Make MutonPopUp not create more procedural prefab changes |
| `b0dffabf8` | better gyro and turn rate |
| `839c59bfc` | input script fixes |
| `5057b48b9` | Zero out transforms for environment and trail prefabs. Fix a prefab reference error. |
| `efe9e18ce` | All spawnable items are now in containers. |
| `d729a400a` | added primitive plus asset Upadated the muton model built shaders and scripts to animate mutons fixed the flight controls tuned the main menu |
| `05b273b6d` | Change arial SDF.asset for atlas from dyamic to static to hopefully stop it from always changing on us |
| `21617d48e` | UI Sizing Main Menu |
| `8e5146b16` | minor fiddle |
| `ee7973c26` | manin menu fiddle |
| `71b9fb32b` | Play Button Fade In Main Menu |
| `7704fec4f` | collider, input, and shader tweaks |
| `cf17870f0` | Restore player prefab. Fix malformed meta file: AiBlockMaterial.mat.meta |
| `402512712` | Sync |
| `ba773c035` | velocity shader input tweaks, scene organization |
| `d59986e08` | Tutorial Wired to Flight Controls  //TODO Garett wire gyro only case up |
| `9c6026517` | Added tutuorial ship manta |
| `705bac013` | UI For Main Menu |
| `d501ea87a` | Tutorial Design Pass |
| `ea091b7d8` | colliders, and muton tweaks |
| `cfbbbf722` | clean up sync |
| `eb2077da1` | Sync |
| `d634fc7af` | Options menu updated |
| `e05829b77` | weird file stuff |
| `acecf0d90` | Tutorial UX UI changes, moved to official |
| `0c5441745` | sync with grace |
| `e46de831b` | Moved to folder |
| `1e1d99bc0` | tutorial tweaks |
| `2709222bb` | Wireup desired haptic presets in haptics controller. Connect muton collision haptics. |
| `b47ad1a6e` | Clean up folders and added Pause menu panel to game scene |
| `949952fe2` | Add a phone flip animation for gyro tutorial |
| `ba4d9cf32` | Zoom Panel,  Gyro Placeholder, Score Transform |
| `fb2fd6211` | main scene and menu scene muton collision fixes |
| `080e2b72a` | Implemented ScoreManager and IntesityManager linked with muton, trails and the IntensityBar |
| `e8bf612d1` | color tweaks and timing tweaks cleanup |
| `c07f3f6c8` | Scoring and Intensity bar fully wired up |
| `7b5e6643a` | Pause and Options UX Format |
| `0aa089fd0` | Syncing UI |
| `25949b28f` | new blocks materials and prefabs |
| `73fa34da1` | lots of shader, muton, and tail tweaks |
| `c59cb78ff` | Sync |
| `87b1af66a` | sync |
| `d52a5c084` | Tutorial Input Controller Revamp |
| `3c5645624` | block tweaks |
| `d50807119` | tutorial updates |
| `6f0acd194` | Main Menu Resolution Changes |
| `c0c18d82f` | Collision.Clear() in Tutorial Muton and minor Changes |
| `37d3c48b8` | shader tweaks fade in and trail scripts |
| `f1b4f0702` | Replacing missing Main Menu Design 1 |
| `e2ed2ed8c` | changed ship material |
| `00b276c79` | Syncing |
| `5eb27bc65` | delta time added to input controllers |
| `f17e6030c` | Sync |
| `e5bb5bfc8` | UI scripts, fixed Singletons, gamesetting is its own gameobject |
| `abb2ecd8f` | New Button Assets |
| `ce26586d7` | file cleanup and input reparameterization |
| `232c300df` | Clean up and wired lost ref |
| `078b69f02` | minor tweaks to input |
| `754a31865` | chris's' camel case fix |
| `e29e8c6e4` | Trail Player and Ship SO updates |
| `640e362ec` | UI Final Changes removed excess fonts |
| `1616612f7` | Adding PhoneGyro to Tutorial |
| `4610b5101` | Added New Font |
| `d6164c51e` | GameOver Event Added and lots of tweaks |
| `05cc29b90` | Re referenced Pause button |
| `d8aa44267` | UI Tweaks |
| `0b11547a8` | Removed Cameras from InputController Implemented CameraManager |
| `7c92ac78e` | End Camera tweak Syncing and Good Night |
| `6b7e52025` | Sync |
| `f510cca91` | Update app icon and splash image locations |
| `053fc9287` | Sync Broken Cameras |
| `8fd90671e` | Cameras Better |
| `6da7382c1` | a bunch of halfbaked "fixes" |
| `de857d504` | i can hear |
| `923c6ebc9` | Readability and cleanup pass |
| `3acdb0921` | Broke player trail collisions. Fixing. |
| `36f0d5e30` | Destroy the gameobject, not the transform |
| `6db3db2b6` | Kludgy trail container reset |
| `3df7b24a6` | wait timer |
| `bf221129e` | sync |
| `21c3aff49` | fix main menu camera |
| `613135959` | made it so the toggle buttons move messed with every scene tried to fix the game manager to enable the tutorial minor changes to pause.cs |
| `c0e14232c` | Sync |
| `fca003e08` | SwitchToggle |
| `4ba4acdfd` | Music Mute and AudioManager rework |
| `c215c6093` | sync |
| `410aac94e` | unlinking images in pause menu |
| `6ff564011` | sync |
| `52c1be858` | Gyro Toggle Event |
| `708c4edc4` | Clean up |
| `ad5f586bc` | Toggle Music Pause Menu |
| `c6a078abe` | Wire |
| `ab6a13a70` | gyro toggle working end game ui flip with the phone |
| `c4c56f5d2` | muton impact moved to time.deltatime |
| `ff1608081` | a few conditions on pauses and experimenting with a brighter skybox |
| `c447182de` | Cleaned up naming to be more SOP compliant and added Comments to Menus |
| `a6b74490d` | Removed TutorialInputController and wired in inputController |
| `b51a64e61` | sync |
| `b755fcde1` | sync |
| `0471e56d2` | minor scene changes |
| `b87f67aef` | Bugs Fixing |
| `68d198ac5` | Pause game fixes |
| `8399a5bbf` | sync |
| `3ab74c29c` | Replay and Play buttons all work |
| `17e22f012` | ship materials |
| `f37cbf2f9` | removed ai ship block collisions added the pause menu to the phone flip |
| `4a7ee3eb3` | saved the scene |
| `58ff9a8c4` | fixed the muton offset functionality |
| `ff82cede6` | better muton offset values |
| `3106a3c77` | removed more dandruff made blue muton explosions more distinct |
| `ec709002b` | Added Pause Menu Into Tutorial Canvas Created Pause Button |
| `b762d705c` | Tweaks |
| `bf8c21dcb` | sync |
| `2b036d0fc` | Swinging Sign Asset |
| `0d903be66` | Updated Swinging Model |
| `430179367` | Add Swinging Sign |
| `4f24fa5ce` | end score position script |
| `4db0fd2b8` | sync |
| `e7df10c45` | Gyro and music toggle sync GameSetting revamp |
| `141ad59ca` | Swinging Sign animation |
| `0e9b45b78` | bug fix |
| `a8d7c4a5c` | sync |
| `15c791b5e` | grace material collab kinda fixed the sign |
| `181359897` | Audio and Gyro Toggle - Still issue with first time toggling |
| `b6bdffa27` | Grace Shadergraph Trails in Testing |
| `75b0cb9f6` | sync |
| `206241108` | Grace Shader Graph Trail Blocks |
| `712222ed4` | Trail and Muton Shader Adjustments |
| `72e99928f` | swapping things out for versions updated by grace lots of renaming tails now use a queue |
| `3e4d49b7a` | minor tweaks and made a test scene |
| `cc9a4bc5d` | Code Clean Up music and Gyro sync still off |
| `4120c1072` | Fixed issue caused in sync |
| `d8616663b` | UI Changes, End Scene, Health Bar |
| `2a72efee6` | richardGalaxies |
| `7b7716985` | Fixed various bugs with music and gyro toggles |
| `d45280f93` | Fixed music/gyro in the game scene as well. Made them prefabs. |
| `3d74ca5c0` | rewiring |
| `1501c66bf` | Working on Sprite Final Scores |
| `5770f4008` | Sync |
| `4b201f180` | High Score tweak |
| `02993928f` | moved players reduced cage opacity |
| `3f869936d` | sync |
| `8a51f3642` | Galaxy Images |
| `d0bac695f` | Changing Numbers and end scene Assets |
| `7a2d26d66` | sync |
| `0ec0608e0` | Fix gyro toggle? Get rid of some vestigial shizz. Misc cleanup. |
| `c34a88ed7` | initial rotation partial fix |
| `3861c6cdf` | sync |
| `68cfc865a` | fixed the gyro reset issues |
| `9e14f1fac` | sync Health bar introduction |
| `072ca43a0` | Sync |
| `95506ae59` | sped up muton explosions fixed input gyro reset bugs changed the color of main scene ships fixed the score board |
| `bbb5c6247` | sync |
| `7f1537c3f` | reseting deleted |
| `31313749b` | Tutorial assets |
| `b9e56e454` | Tutorial Spawer added and Tutorial overhaul |
| `671d5949f` | Flashs through Tutorial Stages |
| `7b1eb8faa` | Sync |
| `57be44596` | sync |
| `95099214a` | fixed the camerflip flicker |
| `e8f0df7d5` | Tutorial Sync |
| `656c29e66` | sync |
| `325fda911` | sync |
| `7a84e347c` | sync |
| `e30842846` | Tidying And sprite adds |
| `e109772f9` | sync |
| `9f61fde92` | Reconnect the pause button to the pause menu |
| `254654094` | Tutorial works. Some other stuff works now too. |
| `29cbdb0f0` | Did an ad thing |
| `362013187` | Tutorial Stages Values sync |
| `eff7c224c` | Sliced Textbox Sprite |
| `bc962ac03` | added movement to the tutorial panels added a gyro enabled flag to the tutorial scriptable object added rotation to the jailblock wall modified jailblock wall lots of tweaks to scriptable object values in the tutorial panels |
| `a3ede5253` | Fuel Bar and System changes |
| `9815d5efc` | Removed intensity from projects code |
| `11ec09c87` | UI/UX pass on Main Menu and Game scenes |
| `a5067df12` | textmesh, textbox to tutorial |
| `42daaf3cd` | Game and Tutorial UI |
| `12b7073bb` | Tutorial Fuel system clean up |
| `cb253558d` | Getting the app ready for publication |
| `94d671b89` | fixed collider scene deletion bug in tutorial fixed camera flip bug in tutorial minor scene tweaks |
| `1b946daba` | deleted Vcams in tutorial to fix camera flip |
| `58cd65282` | changed player settings to reenable patching |
| `f77174fad` | Changed scoring points from 5 to 1 Sync |
| `d83624d57` | Changed Muton Bonus and fixed Fuel System Bug |
| `69ccbc1e0` | Fixed gyro stuff. Fixed menu buttons. Fixed FadeIn effect. |
| `ce946220c` | Okay. Nobody move a muscle |
| `0d7a9330c` | The rest of the UI things |
| `8847e61e2` | Dialog Box in its crudest form |
| `b14bcc24e` | Health Bar |
| `94df7b5cb` | Health Bar |
| `ba81a5668` | Make finger indicators show up again as tutorial stages progress |
| `adf3e94e8` | Lots of changes everywhere new shaders and models for skybox exploration tweaked fade in on muton added muton explosions to tutorial added an additional chek for inverse rotation on gyro attempted fix for tutorial stage retry |
| `89d61327b` | Modify fuel bar |
| `198956355` | Added Jukebox and Mixers |
| `568296887` | Fixed Muton Audio conflict |
| `bb92cd2c2` | tutorial UI fixes minor muton/fuelbar bug fixes in game scene |
| `ea3571be1` | Bug Fix: touch images are not appearing in tutorial |
| `d1c613960` | sync |
| `1fe170302` | Removet he white square on tutorial fuelbar display. Stop the breaking glass noise on tutorial start. |
| `11ed730ad` | Probably made the JBW register collisions |
| `6d930cb69` | Updated Logo |
| `71d593383` | Audio stuff |
| `6c3b2e3fe` | File Clean Up, Phone Flip refactor, changed restart to lobby |
| `2078ea972` | Tutorial tweaks |
| `94267d75d` | Second half of pause menu hiding tutorial controls. |
| `25e6783a1` | Resolve some TODOs |
| `3676f2ca4` | audio files |
| `f4a1c3b41` | New Options Sprite |
| `a93c124a1` | Audio System and Jukebox |
| `a8914875b` | Wired up new music songs and folder refinement |
| `b6d9b5be2` | New Skybox Geometry |
| `6bc1bdd03` | Assets, skybox fallback |
| `b3296fdd6` | Convert music to mp3 instead of wav. Switch around fuel images. Made fuel drain again after pausing the game. |
| `fed940547` | Tutorial UI |
| `b0d9e9e36` | PhoneFlip UI Stage6 |
| `46d6ff630` | Tutorial UX/UI |
| `6d428c4c3` | Updated ships with a distance based glow similiar to mutons Updated the skybox to one that is finally awesome |
| `eaa154327` | Star tweaks |
| `84062dd5d` | change background with fuel amount |
| `38a762a56` | minor tweaks to blue ship distance shader |
| `520b828fd` | made BigCage into prefab and added to main menu and tutorial |
| `53b86f2ca` | sync |
| `af0590248` | Removed Skyfire gameobject for main Main |
| `a088305bb` | minor code cleanup |
| `9f5b539d0` | the gear no longer moves to the bottom when you flip |
| `715cad491` | added gear flip fix to tutorial |
| `01a5bc4f6` | Updated Assets |
| `d5c71ac01` | increased background opacity and desaturated colors |
| `7fc2b1efd` | UI Glow Stuff |
| `5178f35b1` | whoops |
| `481436837` | Swapped one more asset out |
| `7fb23abe7` | took background out of main menu scene |
| `0b380643a` | Updated tutorial narration lines |
| `d25251c52` | Hacking in... A fix for the end camera not working after tutorial. Remove references to amoebius namespace. Start getting project ready for iOS builds |
| `ef35872ec` | Sync |
| `886200d1b` | Sync |
| `1ab15551b` | Added Native Share and some Screenshot  methods to Utility use SnsShare.cs Share()  for Platform sharing |
| `2aacd595a` | Sync |
| `88dd4564b` | performance tweaks |
| `d62d9cf87` | Cosmic snow |
| `57c48ff49` | cosmic snow? |
| `37a113457` | snow tweaks |
| `82ba71868` | Syncing Ads and Screenshots |
| `063d94746` | -cage -big cage ++Snow |
| `107059d03` | End Game Events Added |
| `dd30780b1` | tweaks on snow |
| `079956cba` | Sync |
| `b3a84be04` | Sync |
| `b58505e51` | Sync End Game menus and flow changes |
| `486503387` | Fresh install from repo and these changes were autogenerated |
| `ca4ffb382` | sync |
| `59734ae94` | Revert "Fresh install from repo and these changes were autogenerated" |
| `6cc616358` | making a build to show off around town disabled the stuff in progress centered the fuel bar |
| `44aabc612` | Added Disoriented.mp3 and more end game scene changes |
| `8988a0f07` | Added listeners to Ad and SnsShare Buttons |
| `121a1e02b` | sync |
| `45f4a166c` | Sync |
| `8b5eb2c1d` | More End game refinements Sync |
| `a3f6d1a1f` | New Assets for Game End Scene |
| `4882d6cc4` | Sync |
| `906a30d89` | sync |
| `e3bb88144` | sync |
| `4fac2a613` | sync |
| `7c87e65a3` | Fixed Assets for Ad popup and end game scene |
| `42644d96c` | Add Panel Changes |
| `1c09d66c9` | sync |
| `cf4aa87ab` | Ad Button changes |
| `aa86d82fe` | sync |
| `cd5242441` | sync |
| `0a22c433c` | sync |
| `d7c2b6ff7` | sync |
| `c50d1d0d4` | sync |
| `f24ada01b` | sync |
| `2ad2f3518` | Updated Assets End Scene |
| `d365a2079` | sync |
| `e625b1ff9` | Jukebox update logic changes |
| `e11b1e1c9` | Move ad screen text behind play ad button |
| `5e6497c6e` | Make ad text not a raycast target |
| `cb4eab9b8` | Got an ad to play on my phone! |
| `e938bc2d2` | Some code cleanup. |
| `42c69c60a` | fixed the epilepsy bug when the game is idle in the far cam |
| `f28d31c08` | tweaks to the tail fade |
| `7679510ce` | Added Test Scene |
| `11d2a5ad0` | sync |
| `07e743953` | Game end scen update |
| `5bbdc32c1` | Refactored ad stuff to be more to my liking. First try! |
| `721a13939` | Added More Assets, Glitch files, On Off States for Sprites |
| `2ff36d923` | You can now watch an ad and extend your game play session. Aftr the second death you no longer have the option to watch. Replay button works again. No more multiple collision events registered when you crash into a tail. Share button works again. Fuel Bar can be reset without restarting the scene. Rearranged the ad code into an ad manager class. Changed end of game event structuring. Various code cleanup. Probably some other stuff. |
| `ccf8426c6` | Updated Number Sprites |
| `62af65207` | Audio is back on and not terrifyingly loud. Tiny UI tweaks. |
| `982d9d861` | Fuck that flag. |
| `aadc0ba4b` | Tightening up some bits. |
| `53d2da19a` | green block material tweak |
| `f15e60ab0` | Added Time Manager |
| `a64136ba4` | Re added Time Manager and commented out AdsManager.adShowComplete -= OnAdShowComplete; in GameManager... no ref found |
| `d5c7c9e9f` | Sync |
| `8c1177fd9` | Ship Explosions added |
| `c0a792ec0` | ship explosions |
| `640e260cb` | End Scene Changes |
| `acbac66d9` | sync |
| `994da76c8` | Make explosion rate based off time scale |
| `c019263bb` | Fix console warnings |
| `c95187110` | Added Ui Flicker effects to Scoreboard |
| `f6308ebfc` | Update for Number Sprite |
| `3ca2024be` | Sync |
| `9f4eb6619` | UI changes |
| `6e629b208` | Expose Mixer volume parameters to script. Make the normal ad watch button have reasonable dimensions and placement. |
| `fb2e0a2e9` | Fix music and gryo toggle buttons in pause menu. Silence an invalid warning in adsmanager |
| `4ba2ddedf` | Fixed some bugs in audio system code. Some code cleanup. |
| `5c78fbb73` | minor changes to tail and player introduce death shockwave |
| `cfd4978b7` | bedazzle script, fixed a bunch of ui flips, fixed some camera bugs with extend game |
| `844dba5c1` | anchors should be more tablet friendly |
| `6eea8561a` | bedazzle tweaks |
| `43431cf3e` | Jukebox audio system changes |
| `7b6637e39` | fixed the bug where AI were getting trapped in mutons |
| `0078d661b` | growing blocks |
| `d54de5d52` | tail taper attempt |
| `883b81e11` | Fix some bugs. Code cleanup. |
| `8fdc0148b` | Digital Score and Digital High Score |
| `b1263347c` | Adjustments of digital score and high score |
| `309b06886` | Some snow optimizations |
| `4f058a15a` | dissable the gyro system check |
| `ea4c8f562` | End Game music on Death |
| `d451fb2e4` | Sync |
| `9417edfdf` | Perf stuff: changing project and lighting settings. Turn down audio on death audio. |
| `0f81cfa01` | reduced snow count and made controls less jarring |
| `d43918342` | Digital Score HUD Assets |
| `79731fedf` | Fix audio issues. Remove unity splash screen. Fix score carry over bug. Starting work on retooling events. |
| `12354c487` | Increase audio level to be in closer parity with unity ad audio level. Convert muton glass noise to mp3. Fix ad window showing too often. |
| `c13ade97f` | tweaked input to scale speed with clock |
| `d47d9532c` | Add invert Y toggle. Disable ship animations while paused. Starting to implement a performance monitor |
| `460dab6b5` | work in progress commented out in ship explosion |
| `70fbd142b` | Updated HUD White Background Behind Letters |
| `aa0385593` | More Updates to HUD |
| `7d486b116` | ship reformation |
| `0a6613190` | unexplosion implemented |
| `ebcdbd6f0` | Turn off flickering for now. Only show "new" image for high score when the high score is new |
| `88ad8442b` | Fix merge conflicts |
| `6643c9259` | scaling collider with block and updating 1 touch controls |
| `814a927cd` | split block shader into exploding and non exploding shaders |
| `05d227ceb` | fix "While score board is up, player keeps dying" |
| `a3f59bad5` | Ironing out the end game flow |
| `fe2dbe44f` | block shader optimizations: pulled out color and spread |
| `abcf8b30f` | new materials |
| `3f55a516d` | changed lerp amounts on ship animations |
| `c1425cd10` | Heavy duty changing of how events are wired up, named, and what they do. Full game loop is almost good, but the score is always zeros after watching an ad and dying. |
| `9ffc8bea1` | Invert Y assets |
| `c8dea4904` | Invert Y and splice glow box |
| `a6d4958ce` | Invert Y and Music Button Updates |
| `c186a6af7` | Invert Y Toggle fix Main Menu Scene |
| `3953c6289` | Blind changes to the toggle buttons. Hope the work ¯\_(ツ)_/¯ |
| `324bcc924` | death cam bug and free play scene |
| `6616a2b38` | fixed build settings |
| `bb591a3cb` | build settings |
| `35c5a739c` | Tutorial in game scene Structure |
| `4f1b6f50a` | Hanger  SYnc |
| `b97765776` | GameData works, Debugging HangerData null ref stiff Does not interact with the current build |
| `53b39c113` | Sync Ship and Pilot assets |
| `39fe9962b` | Fix some menu visibility and UI stuff |
| `bc2c15a61` | Restore audio. Apply all overrides to the player prefab. |
| `a6e1ab1f1` | Controls added to game scene |
| `b897aaf10` | Now we don't change the green material everytime the ship un/explodes |
| `850195583` | Fix muton sound too quiet - increase amplitud of audio file and adjust attenuation in mixer |
| `bfe8aef25` | Toggles, now with working initial states! |
| `c5cc0dc9e` | Reduce hit box of Invert Y option and get the toggle checkbox aligned in the game pause menu |
| `63525c2b1` | Accidentally left the pause menu turned on. Change some orientation stuff so that  the splash screen always shows in the correct direction. |
| `3ad0a9316` | Disabled controls in end game screen |
| `1f15bb18a` | Controls no longer interfere with settings button |
| `777139dbc` | Controls UI pas |
| `80b479af0` | tweaks to snow, red ship ai, and muton popup radius |
| `4660c98ea` | Restore AI trails on game over screen. Replace tutorial scene with stubbed in static controls page. Half assed attempt at fixing upside down ads. |
| `beaf28c0c` | Updated App Thumbnail |
| `9a95263ab` | control bug fix |
| `3f7b2e09d` | Update app icon |
| `1ce2e9cab` | Update app icon. Bug fix for death song continuing to play after next round started |
| `c7a0abeb3` | Watch ad button bedazzles now |
| `74b46701d` | Add the ability to skip showing the ad in development build. |
| `73d50c74b` | Bug fix - edge case where watch ad button was not displaying after returning to main menu |
| `4be67695e` | Get share menu and screenshot to orient and render correctly when phone is flipped. Attempt to fix ad video rotation. |
| `bf74a832e` | Properly bedazzling |
| `cb850b294` | Fix "camera isn’t properly oriented after extending play" |
| `5812cb13d` | Fix missing reference exception on trail blocks. Resolve some TODO items |
| `923a99295` | Control Panel and UI Update |
| `36501a9e2` | Roughed in controls screen with scroll |
| `50af47c86` | Add controls screen to game menu |
| `f74d7a1c5` | Roughed in animated muton in controls screen |
| `b8e0c38f6` | Game over screen was not showing after game over. |
| `37f5b19f1` | Not sure, but I think this is adding the splash screen back in |
| `040fe2638` | Control cursor stuff for maja |
| `4391d5358` | Control cursor bug when only one finger is touching. |
| `10833a095` | Add svg support using the vector graphic package |
| `c80ed5140` | Higher res control panel UI |
| `17864cbe5` | HIde menu muton in game scene. Maybe fixed an issue with ads failing when network is turned off |
| `93aa0e308` | scripts and materials to support muton pulsing in controls menu |
| `ee6668b45` | Remove and cleanup the 3d crystal implementation. Update the crystal button. Add pressed state icons to all the control buttons. fuck about with phone flip stuff. |
| `4bffbf076` | fixed remaining ui flip bug |
| `b9b0e04ba` | Attempting to merge.  Go back one for the release build. |
| `43faebc7f` | Turn off testmode flag for ads and unity logo for splash |
| `1a932f43a` | Add shader graph assets to always include shader list |
| `c725aa394` | Updates for publishing |
| `512cb13fd` | ignore recording folder |
| `4f91b1756` | Sync |
| `ac08935b9` | Add Firebase |
| `b087c0d8d` | Fix for three touches cancelling out flight controls (phat finger syndrome) |
| `92fe4f2d6` | scene organization cleanup |
| `a0cc70392` | Add 'Disoriented' song. Update end game song. Duplicate game scene as a placeholder for timed round. |
| `118aca7a8` | Stop playing music immediately upon player death - don't wait for explosion animation to complete. Switch back to debug signing. |
| `d41a9d94a` | Sync |
| `ccb69221f` | Bump max volume up a little bit |
| `88fb511d5` | Show version on pause and options menus |
| `8d9d6c9fa` | recorder package |
| `02c4c4af4` | tetrahedral shards |
| `1119c4416` | toned down confusing or eclipsing muton explosions |
| `e07b33778` | Show version on share screenshot. Decouple version font style from other fonts in the project. Some cleanup of player prefab and main game scene. |
| `65ef72d48` | Version text was turned off in main menu |
| `b7f5ca6a6` | changed the name of the snow to shards, and rehooked up the tetrahedral model |
| `b1f85b35a` | shards model and version typography |
| `888bce0d4` | Kinda lame fix to "God Mode" bug |
| `6e5d828a8` | fixed time manager |
| `1c609b37c` | game scene changes |
| `a48d82d48` | unsaved game scene changes from last fix |
| `50d923bd5` | UI updates for "How to Play" |
| `d578334f5` | Asset swaps |
| `72e614898` | sync |
| `fdcf3a867` | Updated Version Rect Transform |
| `c02a04b57` | How to play now appears correctly |
| `3c517b5b1` | Add plist for firebase on iOS |
| `b1b3ea522` | Replicate the version string font and placement updates into the pause and share screens |
| `5fcd1fa21` | Fixed scoll area starting halfway down |
| `722d6a106` | Shard prefab and snow updated |
| `16f9f8023` | Shard update, removed incorrect shards |
| `8e8d59fe1` | fixed the shards |
| `535bdb46b` | fix the aiblockmaterial error |
| `126334d17` | added a skimmer, 1 touch controls, a boost, and material tweaks |
| `3828c0704` | Added Adaptive Icon |
| `b082a4e69` | Added a Race mode with Flow fields lots of cleanup put speed into shipdata broke the skimmmer |
| `f61da35c7` | tweaks to the racing field and decrese the boost penalty |
| `572e61482` | Project settings |
| `892414f70` | Hacked in controller support |
| `2c0382d73` | halfbaked oval |
| `f53b5d08d` | gamepad tweaks |
| `36f72c13f` | gamepad tweaks |
| `1ae50102f` | Split out test scenes and map to weird play buttons from main menu |
| `6ac3e523c` | oval tweaks |
| `092a4ae28` | Make game restarts aware of the active game play mode |
| `f72d4e6d4` | Add new test mode scenes to build settings |
| `195011e89` | fuel tweak |
| `194fb7b4d` | Janko play buttons |
| `9de92d7bf` | fixed skimmer running out of fuel is decoupled from death deleted time manager |
| `b65bcba0e` | made new test mode with a timer that ends the game attempted to make block collisions add to tail owner's score |
| `fee0a6368` | Preliminary work for ai scoring and tail collision giving points to other players |
| `4107e5b18` | updates to skimmer and fixed some main scene bugs |
| `b7f1f17a7` | skimmer tweaks |
| `2dfbbbf50` | Code cleanup. Add placeholder particles to blocks. Display player names, scores, and round timer. |
| `dec0e41ac` | Move skimmer.cs into controls folder |
| `a18316b17` | Show a winner. Round off the timer. No more scoring or timing updates after round ends. |
| `8e12eb867` | Fix some null references in trail blocks. |
| `b3607c521` | Scoreboard and Timer |
| `6ffe8f17f` | Scoreboard now semi-transparent |
| `4666615a6` | tails respond to speed and ai update ship data |
| `1b160e693` | game manager checks for gamepad befor using it |
| `35c597477` | controls tweak |
| `78b505f13` | raycastying ai and audio error messeges in mutonPopUp |
| `cd936f9e6` | Ai object avoidance working |
| `b10bbeff1` | game scene tweaks |
| `83690adfc` | moving speed out of ai controller and onto shipdata |
| `a36189639` | boost requires fuel, burns faster and is gained slower |
| `c5226ac49` | Turn AI ships into prefabs. Applying some overrides to ship manta prefab. |
| `73a90139e` | Prefabs to go with the last change |
| `af2763b7f` | Minor refactor for avoidance behavior |
| `63b184940` | round time |
| `da15ec185` | ai uses delta time |
| `69ee78c00` | Cleaned up folder names, Particle FX to Test Scene Two |
| `d35df4643` | Data persistence refactor. Code cleanup and error resolution. Introduction hangar scene |
| `2b7dbb3d3` | Added VFX Graph and additional particle effects |
| `5b98544c1` | skimming = static electricity |
| `ccb148829` | new test scenes wired up. scene 3 testing volume control |
| `cdcd58afe` | mostly the introduction of scene 4 space time warping minor tweaks to other scenes like the volume of blocks in scene 1 and 3 also main scene tweaks to ai |
| `50da317bb` | made block explosions move, made a scene 2, preliminary gunner |
| `eed9804e1` | preliminary drift, and gunner update |
| `1487844d7` | Introduce AOE Explosion. Refactoring for Crystal Impact Effects. Started renaming Muton to Crystal. Start leveraging Ship Scriptable Objects. Introduce a Ship class and start migrating player functionality to it. |
| `d175052ae` | Mutons and Tutorials fucked off. |
| `13be5319c` | drift update |
| `9fad7ead9` | Boost particle effect in TestModeGrace, cleaned up and organized Design Assets |
| `153a34498` | like a smooth gunner |
| `5f00534fa` | Updates Main Menu UI buttons for Modes |
| `1e09a54ce` | Added Dolphin and Shark Models, Hooked up Prefabs |
| `134dad0f9` | gunners shoot. input cleaned up |
| `3bd9a014b` | scene 2 tweaks |
| `3bcba46e4` | added animations back in with gamepad, fireing the gun is no longer an event |
| `d05dc1043` | Refactoring and cleanup. Use layer masks for 3d entity interactions. Starting work on Ship Selection and Team Support |
| `346c62777` | Fixed an ID10T error. |
| `2e796c6a5` | Add a projectile layer mask. Make a Game Scene main menu prefab. Get rid of vestigal player objects in node interior scene |
| `88b50e5c7` | Introduce TrailBlockProperties and begin to define TrailBlockImpact behaviors |
| `96004e95d` | Added Trail Shadergraph and Texture |
| `b00f51786` | blocks explode when hit by AOE and projectiles |
| `14c580c20` | Tail Shader reanamed to Wisp, Added Explosion Shader, applied to AOE Sphere |
| `38571d5c7` | Dynamic ship stuff |
| `dc0937e50` | Volume scoring with destruction and restoration of blocks. AI has a gunner. Team scoring. Dynamically load in RedAI ship. Broke AI loading into main menu. |
| `47357451b` | hooked up the red player in the inspector. added explosions speed multiplier, and a TODO to fix the block explosions |
| `925e69adb` | Freeze rotation and position of Explosions. Projectile layer can only interact with blocks. |
| `ef4316a66` | Added RippleGraph Shadergraph, reimported Cage (renamed Node) NodeV2 fbx and created NodeV2 prefab |
| `d10a02530` | added a node |
| `d3e9f1887` | player.cs grabs the ShipData and the speed debuff is ready |
| `6f9ba44a6` | speed debuff/buff |
| `6cffb6513` | shipAnimation base class and manta animation subClass |
| `87e3c9e28` | refactored mantaAnimation.cs with animate part method |
| `a5f4fc9d4` | Ripple Explosion ShaderGraph added and tested in Grace Test Scene, added alt animation script for testing as well. |
| `5568518d4` | added idle to shipanimation and cleaned up the ordering of parameters |
| `fd6a20e1b` | Flipped that dang manta ship, again. Removed incorrect models. Manta_flip correct orientation. |
| `48f1b61ca` | reworked the manta prefap to no longer need weird scaling and rotations |
| `d02a68eef` | Additional work for hot swappable ships |
| `566c062cf` | added the beginnings of a brake animation |
| `2712f821f` | circle scoreboard asset first pass |
| `bac21d073` | Circle Scoreboard UI assets |
| `36437dbf2` | RippleGraph material update, added Serialized Fields to MantaAnimation script scalers |
| `bda56bbf9` | Attached RippleMaterial to prefab to replace explosion shader |
| `0d63436f7` | Explosions have a fixed duration and an easing function. Fixed compiler warnings. |
| `e91729dc8` | No more console warnings or errors! |
| `707ebbdcf` | Parent fossil blocks to their container. Parent projectiles to the gun. Add AIPilot to MantaV2 to support teams/ship selection |
| `a575e96a3` | SO_Ship is out. Straight up Ship is in. Players can play as red or green manta. Cleaned up ship prefabs. Moved ship models into a Models folder. |
| `0dddb29d9` | SO_Ship is no more. Continue cleaning up existing ships. |
| `945dbebcc` | Hanger reads active ships from SerializedFields |
| `c98853e5a` | AOE Explosions (ripple) colors changed to match player color, animation script pitch adjustments |
| `07d8b8c13` | animation tweaks |
| `02fd9d9cf` | Hammerhead_split updated with new rotation points |
| `08176b1cc` | animationa tweaks |
| `8ac98ce06` | tardis tweak and dolphin start |
| `a46dc4d40` | dophin in hanger |
| `bfa0e8a36` | More work on dynamic ships/teams. Rename enums to be plural. Bug alert: Red player is currently dropping green blocks, but life goes on. |
| `c8526a4a4` | Dolphin and Hammerhead model and prefab maintenance, new dolphin prefab is dolphinV2 |
| `6e4d2a131` | ship abilities BABAYYYYYY! |
| `303df709a` | drift and boost refactored to be ship abilities |
| `7a6dcb3c2` | added passive abilities, block thief, and explosion fade |
| `f802824b8` | Code Cleanup. |
| `334220747` | broken dolphin |
| `e3d1fb12d` | Building Sharks, mantas, and dolphins |
| `70fcee708` | fixed invulnerability |
| `0a1d2844a` | ship tweaks |
| `ed877f352` | TODO items added |
| `7d6d34039` | AI Pilot is working again (though, has the wrong block type). No more errors in the console. Comment out overly verbose score debug log. |
| `11621e12a` | green manta ai reseets agression |
| `83d85d49a` | explosion tweaks |
| `83f1d5c13` | added geeometry scripts to dolphin parts |
| `951ae9bac` | Trail Block material respect team color. |
| `484a6c583` | camera manager tweaks |
| `c5d12cb63` | Create stub for AOEBlockCreation |
| `e46d73798` | Graph changes, Wisp, Ripple, Ship |
| `f5ec92c99` | Two players on each team. |
| `5ab13527e` | Move the ai so they're not on top of each other. |
| `b13fd4438` | Now with more shark! |
| `b4669246e` | AOE creation |
| `3d2bc4ee0` | Projectiles are no longer parented to the gun |
| `d3eb8eb4a` | rad looking AOEBlockCreation |
| `9774d9995` | Fixed ship selection on return to main menu. Ported new player to endless mode. Beginning work on node control. |
| `7768d129d` | Make the hostile AI and AI type ship again. |
| `2e3323228` | Fixed ship selection |
| `fccda161a` | Updates to main menu to be more playtest friendly |
| `731679291` | Dolphin now has an explosion of 50 even with 0 fuel |
| `2d095bb45` | Dolphin Tweaks |
| `bc3a8a516` | Shark close camera now usable |
| `278178897` | Shark boost more efficient, new crystal effect that does not work yet |
| `3e1ca5ea6` | Revert "Shark boost more efficient, new crystal effect that does not work yet" |
| `582838d82` | tightened up game UI for playtesting |
| `3cc0cc77a` | Shark efficiency changes |
| `dd1f88fbb` | AI have smaller explosions, shark leaves bigger blocks |
| `b371f2f0e` | Cleaned up magic numbers on AoEExplosion and made Red explosion useable by AI |
| `90586dab3` | Added basic stat tracking into the scoring manager. |
| `603e8c2a0` | I tried to make the shark's skimmer smash things |
| `ad5919b91` | Exploration in Grace's Test Area |
| `66d5023dc` | Shark Skimmer now destroys blocks but doesnt add fuel, started creating a boost effect for crystal impact for manta to use but it doesn't work, normalized trail block volume and debuff values |
| `cda82bce6` | Crystal values are applied in-game,  shark works as intended, manta boosts on crystal impact, some explosion radius tweaks |
| `ba413db66` | Re-enabled end game screen, normalized block volumes (all have volume of ~12) |
| `602fd3167` | Fix play again button. Fix end of round UI not displaying. Fix score tracking bug. |
| `b3ecb0136` | Simple implementation of block stealing |
| `129b18e81` | Plug in a basic, UI-less implementation of node control into Node Interior scene |
| `7a87782cb` | Phone flip and gyro stuff: GameManager is no longer mediator for phone flip stuff. Gyro as an option is gone. Dolphin has gyro flip ability. InputFlowController is gone. |
| `c12b8589d` | Dolphin ship: Fuel fills proportionate to skimming proximity Dolphin ship: multiSkim, increasing returns for skimming multiple blocks at once |
| `b0cd7b5d2` | Fix haptic meltdown bug on end of round when playing on the phone |
| `8c43688fc` | Dolphin gains fuel via skimming with a multiplier of .01 |
| `923e51619` | Additional end of round stats tracking. Display some stats on UI at the end of the round. Add a minimap. Fix phone flip broken for gamepad. Add tags to selectively render shards and the cage to different cameras. |
| `7dd7bd970` | Block volume should now be calculated based on the trail blocks actual dimensions. Added additional meta data to AOE created blocks. Minor block creation refactoring/cleanup. |
| `62fc5949e` | GraceTest Skybox Testing (geobox), geobox shader/mat, shader cleanup |
| `81de5a2bb` | Some cleanup and removal of overly verbose logging. Ground work for DensityBasedBlockSize and Flipping ship model on phone flip. |
| `b79f5849c` | Fix upside down model on phone flip. Maybe fix missing materials on blocks from AOECreation |
| `32f7ebebe` | Shark gets fuel from skimming again |
| `351b5d395` | File organization and namespacing cleanup |
| `77d886067` | setting up for drift camera |
| `b0116960c` | ship script comment |
| `c71ec8602` | Refactored main menu to get rid of all main menu specific assets (main menu crystal, etc). Removed a bunch of unused scripts. Moved scripts into more sensible locations. |
| `ac294114d` | fixed a console error fro a bad renaming |
| `22f20f9cc` | added GunManta and KnifeFish |
| `2e15424a8` | Dolphin balances |
| `5d4255eda` | get rid of embiggen in trailspawner |
| `677e731b2` | renamed trail script variables and removed embiggen |
| `6328d5325` | saved the scene with new ships |
| `eda2cafcb` | scene save |
| `37a093ed0` | Main Menu AI are less intense and have matching color explosions |
| `278eccbb4` | Order of operations fix on AI crystal impact |
| `1bc1681fa` | Bedazzled Begone. Rebrand ScoringManager as StatsManager. Remove garbage prefabs. |
| `b399e8981` | Normalized AI explosion radii and made end game font smaller so as to not run into each other |
| `2d0939614` | refactor of drifting |
| `1672ea34f` | AOEExplosions and AOEBlockCreations colors now properly represent team affiliation |
| `dfa5ed1bd` | Major Housekeeping |
| `6d130a0a0` | Added all the stats. Data look incorrect though. |
| `31762b2e5` | Added TestDesign Scene, MainMenu Button, Trail Assets, Skybox Camera |
| `336c5cc99` | made some copies toi work on |
| `ec7e9bcee` | star graph for big cage commented |
| `e154df0aa` | Material Cleanup, Fixed Manta Model to have Quads/Tri - use Manta_split |
| `c2223de3d` | Fix stat tracking for blocks/volume remaining and blocks/volume restored. fix bug in ai gunner restoring the wrong block. fix bug where AOE explosion material was being changed on disk instead of in memory |
| `fc8eb4f5b` | exploding block test work |
| `7e05c0914` | UI for End of Round Stats |
| `3e4327a45` | made full speed straight effects trigger a change of state instead of continuously  sending messeges. |
| `f32a4c061` | blocks explode better and ship boost is simpler but broken |
| `5b891e4bd` | Track duration of ship ability activations instead of count |
| `3a111fbd3` | Stub some stuff out for ai difficulty levels and add another node why not. |
| `f49190d75` | deleted weird using statement i keep making in the camera script |
| `b127ac4ba` | Scoreboard PNG |
| `cb2762031` | Okay the temp scoreboard is in again. |
| `8f797ade5` | Bunch of end game score formatting |
| `e3660b06e` | Formatting tweaks to statsmanager output. stop recording stats when not game not running. Other mostly inconsequential cleanup work. |
| `04a737254` | A little derp a moment ago. |
| `c8af2d61d` | Showing stuff at end of game bug fixes |
| `0b0fdb46b` | Removed Unchecked Time elements from fuel bar |
| `14806d29c` | Clean up on Input script |
| `e21a73f4c` | input changes |
| `5ddb68f6a` | fixing things i broke |
| `928182f7d` | Live scoreboard, disabled gunners temporarily for diagnostic clarity, minor book keeping |
| `0a46e9c66` | PostProcessing for TestDesign (Local), Created Caustic Noise Shader |
| `792475df4` | Manta blocks scale with number of blocks in proximity. made explosions smaller. Ships are random by default. |
| `983d2a2ad` | Hiding minimap for the time being |
| `d2cbbdc42` | scoreboard cleanup |
| `c7b6f2737` | forgot to toggle it back off sorry |
| `5c78c7c94` | minor shark and block tweaks |
| `745cc5062` | test |
| `bc649cb4e` | Renamed blocks that are in use |
| `5a7b2b1f8` | final scores board commenting out sprite displays |
| `7d63f00cd` | Can no longer steal own blocks, impact reported in end scene, AOE block creation now takes a dimensions vector |
| `b52ccca90` | Shark balancing pass 1 |
| `926e1d249` | made wait time have less magic numbers and respect the inspector |
| `7fd0b6c23` | tweaks to stats manager |
| `080a29877` | Made shark wait time a function of trail and skimmer size |
| `5dc7a9b5a` | AOEExplosion doesnt use manta blocks |
| `ab06ce564` | renamed GreenManta to Manta |
| `9c1224a0a` | manta scales in 1D, fixed the divide by zer bug that was preventing block scaling and cleaned some depricated their stuff |
| `ce93a243a` | fixed drift boosting |
| `70d9d71d2` | deleted deprecated bool inputController |
| `2182161b7` | Moved skimmer FX from trail.cs to skimmer.cs. added bool to turn off skimming FX and made FX timer scale off velocity. |
| `dee568774` | turned off skimming FX on shark (manta large proximity skimmer FX turned off last check in) |
| `ec46b5cde` | ajusted colors (mostly reducing brightness), rotation speed, and fading of exploding blocks. all ships use the saame exploding block AOE explosions have a minimum radius end cam rotates again. |
| `7d76a7cc5` | saved main scene |
| `8467084b7` | saved interior scene |
| `29ed322ae` | Dolphin buffs???? |
| `cfb9f645e` | TODOing some TODOs - jukebox and audio system cleanups. |
| `cf82b3cbb` | Cleaning up TODO items. Fixing some, removing others that seem obsolete. |
| `d747629f0` | Refactor FinalScoresBoard.cs to be able to show scores of arbitrary length |
| `46cd062db` | Manta and Dolphin AI balancing - Also added MenuDolphin and MenuManta for later |
| `c83ecc989` | Rework FuelSystem into ResourceSystem that lives on the ship and tracks multiple types of resources - ammo, health, charge, etc. Remove extendGamePlay code paths. Lots of TODO and code cleanup. |
| `49c0e8bc3` | Some balance things and scoreboard tweaks |
| `db6a893b1` | Scoreboard tells you who won |
| `e7cced60b` | Add static values to enums. Ship class cleanup preparing for refactor and multi-skimmer formalization |
| `1a643b93b` | Removing unused stuff and adding ResourceSystems to the current fleet of ships |
| `22a5fbcb6` | Refactor ship geometry to remove ShipGeometry.cs, instead use a serialize field on the ship |
| `5af35956f` | Fixed a compiler error. |
| `9776a588f` | attempted conic explosion |
| `1a9f574d5` | cleanup of comments |
| `da139a208` | fix after merging |
| `4e6bc566c` | dolphin zooms out while drifting |
| `a23cfc1b8` | renames |
| `4c3d01aa4` | AOE explosion progress |
| `b6932b3c3` | Update physics collision matrix |
| `09cec60fc` | There's a new conic explosion in town. refactor AOE explosion base and structure to support containers for reorienting asymetric geometry |
| `6c14c8718` | dolphin conic explosion tuned |
| `edc10728e` | Ship refactors. Manta has levels and skimmers scale when level changes. |
| `a7ddc847c` | no more blur, bad block wait time, and infinite speed buff duration |
| `031d4ebab` | shark scales skimmer with boost |
| `42133f7ff` | turned off the test renderer on the shark |
| `3b4c50ac4` | mild refactors drift, zoom out, reset camera |
| `17d138843` | Fix SpeedModifiers not decaying |
| `08756cfff` | manta proximity cam stub |
| `8d116d27d` | gave shark a skimmer material and conic crystal creation, manta cam distance scales with blocks, ship prefab tweaks |
| `fdfbad854` | shark skimmer shrinks continuously |
| `25971ba7d` | made zoom out a shipaction, throttle a passive ability, mild renaming, lots of ship tweaks, stud in the trailspawner for general tail morphing |
| `fe7dc451f` | the dolphins trail tells a story now (blocks morph and change wavelength as you drift) |
| `06f47e8a5` | slowed down the skimmergraph |
| `14d23cde5` | changes so it would build |
| `2a1bef850` | grow skimmer is now a ship ability, and a coroutine handle zoom out has been refactored to work in lockstep |
| `9c2142f85` | manta and shark tweaks |
| `088db69ca` | Trails have gaps |
| `262185f05` | Trails have gaps, fixed a bug with throttle scaling, |
| `15e801f02` | main menu was flipping the wrong way |
| `9296f4d30` | removing toggle camera on flip from manta and shark |
| `429923e3b` | stubing in changes needed for gunner exploration |
| `c9074ba2b` | In Design Scene - Local post processing, caustic noise shader on biome bubble |
| `a4bf25e00` | Pulled ship controls out of input controller and into their own script on the ship prefab. Made an Idle Input event. Ship Animations pull from input controller instead of getting pushed from it. |
| `244f293a9` |  playable GunFish that shoots and strafes with is own ShipController subclaass |
| `45ef5541e` | scale volume calculations off outerDimensions and collider as halfway betweeen inner and outeer for all blocks |
| `4755cad2e` | manta fix and main menu tweaks |
| `77af0059d` | Return to Main Menu Option |
| `83e058b9d` | shipController is now Idle aware |
| `e2bbcc67e` | Decoupled charging boost and drifting as separate ship actions |
| `b18487347` | expanding shark trail |
| `1c471a067` | a start to attaching and detaching on trails |
| `8f4c58232` | Laser Graph created in TestDesign for Dolphin |
| `2868b12a9` | Blocks spread is now scale dependent respecting inheretence better in gunshipcontroller |
| `8d15b6062` | drift fix |
| `5b990fe3c` | smoother sliding when attached |
| `023d53cc7` | When attached move down the direction you are looking |
| `845808942` | parameterized the ring in AOE creation, made Guns create blocks after projectiles expire, simplified gun controller and added padding at the ends |
| `4355f2a73` | fixed compiler error |
| `c3b610f18` | - Multinode progress - 3 nodes each with their own crystals and snow - Migrate Trail to be TrailBlock - Cleanup unused and misnamed prefabs, scripts, and materials - Cleanup errors, warnings, and debug statements |
| `e3430e788` | Resolve merge issue |
| `05f033db8` | unbroke AOE creation, material tweaks, main menu tweaks |
| `f3fe864af` | gunship deetach on idle, trailspawner disable on attach, gun fish blocks and projectile resizing, turned down the brightness of projectile material |
| `c3a3185d2` | gun manta uses charge to shoot and gains it while attached, mild projectile tweaks |
| `c3b09ed6a` | ship reorganization, added a level effects system, projectile and projectile block level scaling on gunfish |
| `a0531192a` | Skimmer and resource refactors. SKimmers can charge different resources. Boost, level and ammo all have their own resource bars |
| `42e018ff5` | the GunFish now uses all three resource bars |
| `d422c4447` | gunfish tweaks and scene saves |
| `d895f329a` | created a "minimum speed and straight" Input event and used it for a new "pause guns" ability. |
| `ca16dcd57` | moved idle to left shoulder on controller |
| `34b5fa5f8` | implemented temporarily leveling up while on a trail and stealing restored enemy blocks. moved turning the trail off to the trailspawner |
| `410316663` | Fix to Main Menu button |
| `00737f4f0` | Share button kinda half works as a toggle for a few end game game objects |
| `1aa62431f` | Trails are a thing. Add controller menu controls for buttons and dropdowns |
| `b11397391` | Fix post merge |
| `1446386bf` | Trail following works again |
| `d91678825` | Ship selection and replay from main menu, pause screen, and game over screen. Ship selection is remembered across game plays |
| `7c004b40a` | Vertical Layout Group on fuel bars |
| `d22ce7c0c` | shipdata no longer updates, but leveraages properties. Gunship steals blocks it restores, and shipdata for course and speed are updates while sliding |
| `c03aaa6d3` | Prettify end of round stats. Fix some runtime errors. |
| `19dab25ca` | wired up new charge displays, and turned direction change while sliding back on |
| `a964c27bb` | gunfish can shoot crystals. Projectiles handle their own trailblock collisions. |
| `a16b4b7a8` | saved scenes and fixed a crystaal bug in the main menu |
| `0e5a62873` | Better looking gun trails that are now ridable. stub for tail growth while sliding |
| `06c23d0be` | AOE trail riding kinda works, but the looping is broken |
| `12c0dadac` | Gun Fish uses fullspeedstraight to use new "fire big gun" ability |
| `646495b53` | new gunfish animation and big gun ignores cooldown |
| `03a44cec7` | big gun fires faster |
| `fdd991fcf` | The game looks different now |
| `f12d9ace4` | Replay and Main Menu button fixes |
| `bb1f01826` | Comment out a line of code causing an error that prevents the end game screen from playing on android |
| `8d8158c5e` | Fix the error that I commented out the code for earlier |
| `96556f494` | fixed a bunch of resource bugs and made the meters configurable from the ship. Stub for block growth, and some experimental ship tweaks. |
| `50f070482` | FIx for screen resizing and UI clipping |
| `3689d609e` | Font Update |
| `932e53383` | Quick Font thing |
| `05650d97a` | gunfish change: big gun uses left and right finger instad of full speed straight |
| `4371a3c87` | made a dart board |
| `bf42a6344` | tweaked dartboard |
| `e8d36f290` | dartboard prefab with tweaks, and a red block |
| `702d0740c` | swapped the conic mesh collider for a sphere collider on the conic explosion |
| `5d2213296` | left the dart board in the scene |
| `7b85c32e0` | ripple tone down |
| `55e66184d` | lots of dartboard improvements |
| `95463b952` | UI Updates |
| `026a7c4e7` | WIP Mini-game engine. Can launch game directly from scene. Little cleanups. |
| `d681cc9d1` | flip threshold for gunfish |
| `5d8e3296b` | Nav bar and Hangar template |
| `3863ea18e` | Crippled initial dartboard minigame |
| `16ea1d1d6` | Some weak ass navigation enabled |
| `b67e00df2` | The start of some toggle stuff in Hangar |
| `5888a5407` | Add Score Tracker class for mini games. Minor cleanup |
| `160d45159` | enable changing default ship from inpector |
| `6708c3f83` | Toggle improvement in Hangar |
| `cee65abab` | Cleaning up my mess |
| `78aca42d3` | Dartboard is a complete minigame |
| `79faac519` | Extend mini game engine. Add very primitive, but functionally complete Flight School mini game |
| `22f715648` | Teenie tiny bug fix. |
| `a3839c15f` | Add a little ambiance (cage and snow) to the two mini games |
| `5f011ef6b` | Lots of abs |
| `5e3f12152` | Nav Bar Update |
| `1957f5c07` | Main Menu and some Hangar |
| `2423b5955` | Spawnables. |
| `a128cf4bd` | Hangar refactor |
| `5c56602fa` | Stub in basic positioning logic for spawnables |
| `4eae603ed` | Hangar mostly done now |
| `b9c660100` | Minigame Panel & Records Panel |
| `5e58d6e68` | Fixed Menu Navigation and some color adjustments |
| `5fb9576b6` | More Big menu changes |
| `a97f7fa28` | Okay Last round of fixes for now |
| `e32c0cd8c` | Toggle Synch Added Back |
| `1055d7398` | Some End Game UI work |
| `b6140a9bd` | segmentified the dartboard; added positioning shemes to the segment spawner and a segment spawner to the dartboard minigame |
| `a7982004f` | segmentified the dartboard; added positioning shemes to the segment spawner and a segment spawner to the dartboard minigame |
| `230344501` | Spawner should change spanwed objects' positions, not its own transform when positioning spawned objects. |
| `da8dc93d4` | Change improt settings of a couple images (POC to fix aliasing). Minor code cleanup. |
| `d6f52000a` | Minigame Select and Settings Update |
| `515873740` | Small mess cleaned up |
| `eb3173d8a` | Assets Clamp instead of repeat |
| `bbf57f2b0` | Hangar UX consistency |
| `c2ddcd862` | Toggles |
| `b8427d944` | Settings cleanup |
| `7ff482eb7` | Fuel Bar Resolution Update |
| `8bd0acbfd` | Beginning to wire up MiniGames Menu. New scriptable object types for ships/games/abilities. Placeholder video clips. |
| `a4ee11403` | Add return to main menu buttons to all the mini games. Add in the flight school mini game and the manta ship. |
| `281ccf346` | Minigame Menu is mostly functionally complete. Minigames have dynamically set player ships. Fixed trail spawning in flight school. Reset speed and orientation after each round. Fixed wrong starting orientation. Fixed key not dound exception in score tracker. Minigames can have 1-4 players. Made a yellow player. Updated shader graphs to allow any color combination. Better placeholder preview videos |
| `0f6a5e901` | Add countdown timer to minigames |
| `5a51058e7` | Uncomment a line of code. |
| `babce27c3` | Nav Bar toggle group fix |
| `18133aa91` | Layout group fix for minigames |
| `2a3f4473d` | Some cleanup and placeholder improvement |
| `4307b9fd7` | Minigames Adjustments |
| `787f239d7` | Derby now kind of resembles a game. |
| `384a41b6d` | Placeholder hangar menu is partially wired up |
| `5293506d8` | Radically simplify toggle synchronizer implementation |
| `aa41e7c6a` | Play button launches main game instead of mini game |
| `41a13d00e` | Abilities/Ship Overview toggles and added gunfish to hangar |
| `e618c2efb` | You want placeholder assets? I'll give you placeholder assets! |
| `4bed8f28b` | More SOs for abilities. |
| `dcaac23e4` | remove unsued scenes. Add a 'random' ship. Starting to wire up preplay ship selection menu. |
| `c89772b9b` | gunfish bullet rails turned into an unused ship ability, skimmers and ships can collide |
| `6329a5168` | saved |
| `305a1a791` | Ship Select layout group |
| `56a8b5b06` | cage graph unbaked colors |
| `531a363cc` | Connect up the pre play ship selection menu. remove dartboard from the main game |
| `2f943345c` | Ship Select fixes |
| `2c9b5782a` | saved new cage material |
| `c8cc0f6cb` | fixed console error |
| `6c4404ba4` | shark skimmer turns of other ship's trail spawners |
| `3c69694dd` | Minigaems Settings no longer overides other screens |
| `7207e318d` | Gif size normalization |
| `3a77f514a` | Gif and preview image aspect ratio normalization |
| `fcf2c02a0` | Close minigames settings panel when navigating away from it. Fix ship selection for main game play. Show Player Count and Difficulty selections when screen first loads. Show correct sprites for Player Count and Difficulty selections. |
| `ed806fa53` | toned down green ship brightness, made an AOE Slow Debuff Explosion, Made AI debuffable, moved explosions out of trail blocks and into the explosion base class, made a modify speed method |
| `fb17c73e9` | First pass implementing high score menu - working with fake data. Add four player score slots on end of game screen for flight school |
| `6539e0f68` | End game score structure |
| `8e0c6cff4` | Records menu scaling and formatting |
| `8d0c78dc8` | I always forget to toggle off screens I was working on |
| `ccdc3484d` | More Settings Scaling |
| `8e6cf71dc` | End of Game screen is a prefab. Laying ground work for leaderboard stat tracking |
| `695dfec63` | Toggled settings off again.. |
| `fd2e66e22` | Fixed button toggles in settings |
| `67f110270` | Records Panel scroll rect for game selection |
| `05c49d5c6` | Ai pilot speed debuff fixed, sharks slow AOE fixed, clamped the waittime on the trailspawner |
| `ec700d3d7` | High score screen is dynamically data driven. Highscores are logged at end of game Hide empty entries for scores on end of game screen. |
| `84a2640a1` | shark tweaks |
| `97522c1b7` | added haptics to the shark ship impact |
| `95400e7aa` | added a lerp to the mantas tail, and a position to the AOE creation id |
| `8582dc438` | cleanup |
| `d853b992b` | AOE flower creation |
| `c085b6417` | Asset Update for settings toggles and trail visualization |
| `e70624d61` | Hangar Screen Responsive |
| `24b9cd7c8` | Everybody has scroll rects and everybody plays nice on any phone. I think. |
| `79e23c8ef` | small catch on the hangar scroll rect |
| `4db021a1e` | I missed ship selection (it now has the aforementioned as well |
| `4ac480dd7` | laser fx, astrofoil graph, material, texture |
| `1af645b21` | Rudimentary proof of concept shooting gallery |
| `883fc3d91` | Small tweaks to a scroll rect and remove vestige highlighted sprites |
| `ae05d038a` | Button raycast target issue resolved. Some minigame UI refactoring. |
| `5e835f0a8` | Node interior player HUD scaling |
| `453c0dab6` | Added UV model, updated texture, material and simplified ShipTextureGraph. Added Color Pallet Ship Colors in project |
| `2bf510185` | Major buttons should no longer disappear on small screens |
| `490ac5b5f` | Coming Soon panel toggles off when navigating away from home |
| `e7b1f542d` | Settings toggle layout size issue resolved |
| `d0f08a35f` | A couple new mini games. New scoring mode (volume stolen). New turn monitor (ammo accumulated). Rebrand destruction derby as RAMPAGE. Shitty zigzag spawner. |
| `e40ce4bfe` | Minor minigame menu script refactor. |
| `3c72ebdaf` | tuned AOE flower, and manta trail/camera to make trail visible from far cam, and not self skim |
| `cddd2e834` | AOE objects share a common parent. Actually get a random ship if shiptype set to random. Fix a null pointer i introducted |
| `b819c3679` | simplified ship script with less overides, added separate scaling for pitch, yaw, and roll, fixed gap bug by clamping block size, manta tuning |
| `a975ab6c6` | half baked fake crystal ability |
| `4c7fabdd5` | finished fake crystal |
| `fccb151df` | made the fake crystal a bit more jank |
| `5dcfaacd9` | Hangar Abilities Rework and Records fix |
| `5e0f8faf6` | Squashed some errors coming out of minigames and coming soon |
| `8a94b62bf` | Placeholder assets for minigames, Visual pass on minigame end game UI, refactor scaling on some scroll rects to avoid clipping (hopefully), maybe something else |
| `9bdb6f799` | Attached end game UI to all minigames |
| `485c0ad56` | Big clean up of node interior end game UI |
| `e6ab49ca4` | fake crystal polish and haptics |
| `ea3571107` | renamed an object |
| `8d73f58c0` | Rewired minigames to the end game panel |
| `e363e6c4b` | menu fix |
| `6ad60ff52` | Abilities/overview toggle fix |
| `4e563f3ca` | my bad |
| `27d6ddd07` | Little refactor for ship and game lists. |
| `34f45caab` | Fixed end game buttons in testnodeinterior |
| `2227b225f` | Attatched All_Ships to the ship select panel and reinstated the share button in TestNodeInterior |
| `fa67f0049` | Blip can slide backwards and only shoots on trails |
| `7ebb71aa3` | Swipe in the menus and/or click navigation to navigate (part 1) |
| `99c8e9059` | Overview/Abilities toggle resizing |
| `dc6fb3bd8` | Shark Bubble Sheild FX, Graphs, Decals |
| `e03bafe35` | Hangar Icons, Shark Sheild |
| `f0d19b067` | Ability Icons first pass |
| `8edea742f` | Left and Right arrow navigation |
| `443b833be` | Large ship hangar icons, added selected sprites. Updated Graphs for shark sheild testing |
| `dadbaf587` | Hanger Ability Icons part dos |
| `d936a8fbe` | Nav Bar Updates |
| `3f0be8ca1` | button clean up |
| `ea1d41832` | Tighten up main menu navigation. No more disappearing nav links. |
| `5205c36fc` | HUD and End Game UI update for TestNodeInterior |
| `7e06f6885` | Test Node Interior UI fixes and spinning |
| `99db4b73f` | Shark Icon Update |
| `5a16fd9e1` | sliding fixes and exploration |
| `a29821e2c` | created projectile impact effects and tuned shark |
| `408c80883` |  projectile inheritance is easier to aim |
| `dedf52971` | created AOE ship impact effects and gave them all to the dolphin blast |
| `14d87dc0b` | Groundwork for unlockable content. Added a text object loader to be able to spawn content from a picture (prototype) A bunch of cleanup refactors. Removed all build warnings. |
| `98123c130` | Small bug fix |
| `3a9c4b5b7` | darts progress |
| `c7f3476d2` | scenes save |
| `425fe7580` | code cleanup |
| `477af2c7c` | dartmini game scoring and tuning |
| `cf60b796b` | lots of floor raising in darts and flightschool minigames: Added more customizability to crystals |
| `4e1b05976` | Overhaul on thee thief course. Addedf difficulty scaling on seegmnet spawners mainly the helix spawner. |
| `ad4520ea7` | gunship fix |
| `3dd297ef0` | downgradewd render textures to work on mobile self skim is now respected across the skimmer performance tweaks to steal game helix spawner has a scale parameter |
| `b11a54fcb` | shards can now take dynamic volume and align or point at axes instead of crystals. updated thief coarse to use new shards |
| `282449be7` | Add score and round time to HUD in thief and shooting gallery. Make prefab of HUD. Move countdown timer into HUD. Refactor other UI elements to use new MVC pattern. |
| `78fb41da0` | Created an AOEBlockSpawner derved class and prefab Fixed snow and performance tweaks in theif coarse |
| `be0530891` | Made a tools struct and extended the lerper to the camera manager. tuned theifcoarse |
| `8ff304352` | dart minigame performance and aesthetics tweaks |
| `09b0d45b3` | fake crystals handle their own debuffs instead of ships |
| `9a694e2b7` | clean up |
| `7d7010757` | spawnable flowers |
| `f16155847` | Don't modify prefabs, it leads to difficult to debug issues. Changed spawnable flower block id strings. |
| `dd4ff5692` | stub for difficulty scaling |
| `178a46d06` | Stubbed in a test for flora. Made properties dynamically visible in Ship. Modified enums to eliminate 0. |
| `62095d7fb` | ship tweaks |
| `e9ff2e537` | ship tweaks |
| `ca311ba51` | separate scripts for shipeditor and showIfAttributes |
| `34d3af4e3` | REFACTOR: Make all ship actions classes that encapsulate their behavior. Update ships to use the new system. |
| `6dfc9fb96` | bufo beginnings including explodable projecitles |
| `22b9c6f19` | Moved speed modification from Ship.cs and ShipData into ShipController.  Added Velocity modification. Stub for action to detonate projectiles. |
| `233c03432` | AI pilots spoof inputs now. And Bufo explosions cause knockback to all ships. |
| `60cbc29e6` | better autopilots, move the sign change from the pitch method to the input controller, |
| `054c20ad2` | Cleanup getting ready for Effects refactor. |
| `bff4a801a` | Explosions everywhere |
| `0219b7536` | block impact animations are more physical and fade out with time instead of distance. More control over explosiverotation and explosive spread. AOEExplosions calculate their own speed. |
| `ee2646a7b` | explosion tuning |
| `1c44a081b` | created a shield property on blocks that prevents one steal or explode. AOECreation now comes out shielded by default. |
| `d06827425` | fixed mobile controls bug |
| `d2ba9d4e5` | made corroutines to activate timed shields. stup for portrait mode. created final block slide effects to better time trail slide effects. made a stop guns action for blip detaching. fixed riptide boost. tuned knockback on projectiles. made explosions activate team shields brielfy. fixed animation in gunfish. added a prefab for a 3 action button panel. added the ability to gain resources over time in the resource manager. |
| `6a41a70e1` | fixed the distortion while explosion fragments rotate |
| `ce4a153f2` | pilot and material tweaks |
| `02c1c25da` | some fixes that were prreventing android build |
| `7a1ca4e4a` | bufo can now detonate projectiles |
| `60ba5d7f0` | new ability: chargedFireGun. lots of bufo tweaks |
| `25ce12e2f` | Added Riptide fbx |
| `44dd493c5` | material tweaks |
| `d0f353d40` | charge guage on the resource system that bufo uses to charge up his explosion. New ability : spin 180, mapped to bufo's flip action |
| `1acd87f69` | bug fixes and tuning on bufo |
| `ac57e7068` | improved support in the input controller for portrait controls. modified controls ui to better support 1 thumb |
| `6ca23ef57` | minimal swap of dolphin to riptide models in the ship prefab with some animation |
| `d06e24c70` | riptide animations 1.0 |
| `dc8f524d2` | Bufo animations |
| `0ab525eb3` | shark nerf by introducing gap scaling with block scaling. this keeps a large profile with less volume. |
| `472ea3f74` | same as before |
| `f6789c6d7` | added a PortraitUI script that can position the UI in portrait mode. Made controls for portrait mode including second touch corrections. |
| `81edc3553` | changed blocks to static so that occlusion culling can be applied. fixed portrait landscape UI |
| `d16976948` | Remove empty folders |
| `fb891c043` | Fix console warnings. WIP effects refactoring. |
| `b394014f3` | three button panel 1.0 and other bufo improvements |
| `67e78c7f3` | note |
| `654a3e288` | riptide aesthetics |
| `6e89cc131` | extended implementation for conic explosions in hanger |
| `3be0f7262` | Added a rear view mirror charged fire now switches to detonate if there is a live projectile. |
| `ba84f0cd0` | Made a Stationary mode for bufo. consolidated charge, fire, and detonate into one action. lots of manta fixes and tweaks. |
| `a54ba7ef0` | Use number of nearby blocks, or closest non-owned block for skimmer camera distance |
| `5df06a557` | AI ships and player ship convergence |
| `b9083368e` | Rework: Manta camera and tail scaling from block proximity skimmer Added time created to block properties Added normailized camera controls added a destructive bool to AOE |
| `541bb10ae` | saved some scripts |
| `f729d22d6` | camera cleanup removing the far cam and adding clipping planes to lerping coroutines modified lerping tool added transparency to ship materials |
| `63bdbbf61` | shipgraph tweaks for manta |
| `46ddfac40` | Added a sandbox minigame and some assets |
| `3fd8bc570` | added sandbox to build |
| `9753e8377` | Controls refactor from absolute to relative |
| `57f64b0c6` | joystick size is always an inch |
| `ae5ecd7a0` | skyboxes everywhere input fix |
| `6ac532b06` | updates to ripplegraph |
| `036d5a85a` | updated shaders |
| `e3c0fa6d4` | Revert "skyboxes everywhere" |
| `b860f8411` | Revert "updated shaders" |
| `7beea1a69` | controls change and bug fix |
| `48a4bb53d` | learning how to revert properly |
| `f63577db9` | still learning reverts |
| `1f9f9c63a` | learning to cherry-pick |
| `f707fec95` | ripple update |
| `4749d0498` | menu updates and material saves |
| `6f67fb18e` | Build size optimizations |
| `8a2bb9c69` | Get rid of TailGlider namespace. Delete vestigial classes. Misc cleanup |
| `7567bdace` | Upgrade project to editor version 2021.3.26f1 |
| `f5d4ee37d` | Remove unused code and assets. Refactor ship animation classes for better encapsulation and consistency. |
| `1ac44dabb` | Add Pilots. Add elemental levels. Refactor Some of resource system to accomodate new additions. |
| `2b038f090` | Unity fuckery |
| `f06f06f4b` | Minigames don't set a ship's pilot for the time being. |
| `1e85147a2` | Critical performance issue fix - 'this' should have been the gameObject. |
| `8ae8d62c4` | MANTA CLASS |
| `aef5ba971` | wings fixed for quick bounding box |
| `dc45f4d3a` | manta fix |
| `0884e627d` | triple check |
| `0eff772fa` | difficulty scaling and manta ship model explorations |
| `a37dadc40` | z forward |
| `91f95f60b` | lol rotation hacks |
| `309207364` | yehs |
| `dc511e47a` | -y forward |
| `b7fc4e76a` | Add files via upload |
| `8ec02c785` | new ship wired up |
| `f41e2db59` | Einstein wept. Separated SpaceTime into Space and Time |
| `e87842073` | Null pointer fix + cleanup |
| `f2f3fd021` | Main game implemented as a mini game with bugs. |
| `78cf59459` | cute lil urchan |
| `8d14e2e89` | reduced the post processing. removed tinting. shifted material colors. tweaked AI |
| `5532ceba9` | oopsie urchan |
| `b3c0b083b` | manta tweaks and urchin half wired up |
| `eb11e81d4` | Remove haptics in MM. Fix null pointers. |
| `29f251ae2` | Urchin animation and color refactor |
| `2de774e33` | Introduce a benchmark scene. Exploratory work to get player canvas off of player. |
| `97dad79c0` | urchin updates: animation. trailViewer script w material swaps and lline rendering fixed a bug in the trailspawner assigning the wrong index |
| `407760013` | materials and progress on trailviewing |
| `76d4b3dec` | Add Odin Serializer. Minor refactor to ship controller and charged boost action. Fix urchin destroying first block when attaching to a trail. |
| `6be79c146` | gun bug fix |
| `04b2f0a0e` | added a devastation flag for the dolphin to destroy shielded blocks permanently. bufo is now playable in landscape with the controller buttons. renamed some files |
| `3a30aeee4` | Fixed the some bugs in the trail viewer. working as intended now Tweaks to ammo and charged boost added the bufo speed buff Lots of tunings to all ships, but manta |
| `786de5cd4` | fixed camera bug by diasabling camera notification on autopilot manta renamed shipData->shipStatus, Controls -> ThumbstickUI simplified UI and added boundaries |
| `18c3fcbd4` | tuning |
| `bcda72e09` | advanced single stick controls imported new Grizzly model fixed bugs in thumbstick UI Made a screen material |
| `a6da728e5` | lots of changes to exploding projectiles and bufo import vertices testing |
| `5ef14b88b` | Changes to Bufo, input, singlestick, and thumbstick UI making bufo a single stick class and fixed some bugs with AI |
| `c9b905ec3` | Rhino_Test |
| `4e5ffdeb9` | Scene setup prep for playtest build. |
| `b7f1db3bc` | Rhino fix |
| `fe8b03756` | oopsie |
| `ce9474674` | rampage updates |
| `7c7e8a3ef` | Rebrand ThiefCourse as BlockBandit |
| `5c71b9ab8` | Rebrand single biome team match to Cellular Duel |
| `459d461f7` | More Launch Party rebranding. Things named GunFish are now named Urchin |
| `2c2955c26` | material changes |
| `03d9d125e` | tweaked materials changed yellow to player 2 improved rampage limited haptics |
| `108d17f60` | Boosting AI Rhino Action cam in manta stal coarse for promo videos |
| `ff9fd02a6` | Make the camera rotate in minigames like it does in node control game mode. |
| `59d8bf02c` | Added music back in. Fixing exceptions. Main menu buttons launch freestyle. Rebrand sandbox as freestyle. |
| `774ca634d` | Material changes, final score board "fix" |
| `63fde063d` | Make nodes aware of items spawned within them. AIPilots hunt fake crystals |
| `e969f4c99` | attempt at a team aware skimmer color |
| `a945f8835` | difficulty scaling on rampage mode |
| `3cd624667` | Make things work a little better |
| `9f12a29db` | stripped out difficulty tied to spawnables. added better difficulty scaling to BlockBandit |
| `7478644af` | Fixes on block bandit and freestyle |
| `ef8e9e262` | Minigame buttons added |
| `d81bb0aa7` | Normalizing in game UIs and wiring them up |
| `c382f8104` | Notify hangar of selected minigame difficulty for AI |
| `ca940d703` | manta tuning |
| `3752d6bac` | Bug fix so camera does zoom from aI temp fix on close cam AI bug don't understand why some demos got deleted |
| `adc1c0e16` | Fixed the extra trails coroutines fixed some camera bugs |
| `bc5877de8` | Fixing Exceptions. Removed Odin plugin. |
| `09528068d` | rhino skill tweaks |
| `6dfccc298` | manta drops crystals Ai tuning |
| `7794bb3f4` | Cleanup, fixing exceptions and bugs. |
| `a28c1ef3c` | fake crystals that the player drops use a different material |
| `7e26fb627` | Add some menu navigation with gamepad |
| `121436d0a` | Added ripple materials for blue and yellow |
| `97dcc9d13` | Manta buttons added, Rhino in TestDesign |
| `1d837aabc` | Manta shouldn't chase it's own decoy crystal |
| `79c07b008` | Smash and Soar Buttons Added Main Menu |
| `162eb2161` | Added intensity levels to freestyle tweaked crystals so they are easier to spot |
| `e9184a53e` | Updated App Icon to Cosmic Shore |
| `09db43217` | UI bug fixes. Scriptable asset configuration |
| `83ea765ea` | UI fixes and video stubs |
| `db5175b41` | saved the scene . . . |
| `963bd619c` | Replaced MANTA buttons, deleted old ones |
| `0b0977aa3` | reordered minigames |
| `872a134b5` | updated and hooked up ship buttons in minigames |
| `6ff809925` | menu tweaks and material tweaks |
| `1e800fdbd` | Massive changes to UI and scenes. Added Videos, Copy, and images modified fake crystal material |
| `a6d71c597` | Assigning to two separate reder textures for hanger and minigames tuned manta to have a range of difficulty |
| `a19a88513` | videos were skipping so i slowed them down |
| `6a9a860d9` | iOS build prep |
| `75097d800` | Video Tuning for faster playback |
| `152a63288` | Pause menu UI Fixes and video tweaks |
| `79bd81290` | changed skimmer to blue for all players |
| `f4d550638` | Updated App Icon |
| `498baa3f1` | seaweed, flora,  and elemental cyrstal beginnings |
| `97d452101` | saved the seaweed scene |
| `799dc1d47` | Update menu implementation in the four alpha scenes and make a game menu prefab. Bunch of prefab organization and code cleanup. Added a placeholder "Go" button for hot seat multiplayer game rounds. Bug fix - turn timer doesn't start until round starts. Pull resource displays off of player and into game canvas |
| `3287eb4b5` | updates to the way devastating works (doesn't destroy blocks)and more progress on seaweed |
| `c0d155032` | Put new game canvas into dolphin darts |
| `2cfc97333` | added dlphin to launch party |
| `44719f632` | new dolphin is out |
| `6de3610df` | new materials for ships and bringing seaweed into freestyle |
| `0c03774ae` | dolphin fix |
| `c0051d5a1` | check |
| `fa1d7b1e3` | texture fix |
| `bb7b3d202` | texture fix |
| `cc9b32c64` | texture fix |
| `893fca381` | manta fix |
| `8dfe376fc` | Add haptics and SFX control options. Unify in game and settings view of settings. Add icon fields to SO_Pilot. Some refactor cleanup of audio system. |
| `9e1076aba` | Show winner player's color - i think. Pull main menue scenes into prefabs. |
| `dc7f27e94` | Fix bug with placing thumbs before minigame round starts. Make main menu navigation snappier. Change label of Yellow team to Gold team |
| `94e5036cc` | Custodial work in input controller |
| `714cf04e5` | Add pilots to the hangar |
| `75d5f1cef` | Create a basic 2x2 single cell match scene. Not added to UI yet. |
| `e4a4bcc5b` | wiggly bones |
| `b75a129d5` | text changes |
| `4011e9252` | test on a rigged manta with shapekey transforms |
| `79ed4aaa1` | Swap game select and ship select. Add sport mode button. Pilots associated with mini games. Ship leveling and crystal level effects. FTUE screen. |
| `2b642aa22` | rampage score renormalization ship preparing for new mesh renderers fiddling with seaweed |
| `07278880c` | FIxing bugs in minigames, input controller, segment spawner added a preview dolphin and colored ships for screenshots. |
| `ec72df236` | Fix dolphin darts, fixed ship assignment in minigames. Fixed main menu game buttons |
| `fb9abf340` | Project cleanup. Refactor materials in Hangar. Ship action mapping refactor. Pause the main menu scene when not on the home view |
| `97e15f31a` | Project cleanup. Refactor materials in Hangar. Ship action mapping refactor. Pause the main menu scene when not on the home view |
| `f0a224985` | So. Many. Buttons! |
| `089961bac` | Added Cosmic Shore Color Library |
| `c5ddbb591` | Add sounds to a bunch of the UI. Implement Leveling parameters (untested). Prototype of an animated background on the hangar. Cleanup a bunch of prefabs to make more sense. Fix segment spawners for minigames reseting between players. |
| `d1a073e39` | Replaced Elimination buttons |
| `aed455295` | rigged fix |
| `106825181` | just a quick reupload |
| `5d589f8c8` | one more |
| `3d12344a7` | a |
| `db073c285` | Rigged shape key manta is coughing blood. Check out soaring to see overtuned block scaling with levels. |
| `405975dbd` | replaced pilots button with vessels |
| `cdc227c76` | Wire up more audio in UI |
| `0570c4d9e` | Animated background on all the main menu screens. Fix settings screen to be configured like the others. |
| `8f8c192a6` | manta animations progress |
| `c51e0ba43` | Color Changes to Backgrounds for variety, pilots to vessels |
| `243d3f737` | Adjusted UI color backgrounds, replaced two player GO button |
| `bda4c9930` | manta progress |
| `6469ae05c` | Polishing manta animation and applying materials to manta and rhino |
| `639366c10` | animated and painted dolphin |
| `7982a9f7f` | Lots of leveling up progress |
| `cd4deec99` | Tighten up the app for launch party |
| `969c18091` | build fix |
| `1234826e2` | normailized pilot elemental levels |
| `4f8f604a7` | Fix elimination game mode. Last minut ui tweaks |
| `997d2fcf6` | Text and images |
| `c91ea428e` | No more points for friendly volume in dolphin darts. |
| `555da5147` | Fix soar and smash buttons in FTUE screen + add audio. Apply prefab overrides in main menu buttons. |
| `9c9fed769` | Created VolumeDisplay shader, materials, script and Added UI to 3 scenes |
| `dd054e0d3` | Add Playfab SDK |
| `34a08589c` | Generalized Rearview cam into Pip (picture in picture) gave it to manta for far cam, and toned dow the distance scaling on the manta. |
| `c3899a348` | Update IAP package and add playfab android example |
| `f08638fa3` | Some test code for playfab api |
| `861620654` | Precomiler directives for auth + iOS device id auth |
| `0b37c1587` | volume display polish. and urchin loadiung |
| `dac68df40` | urchin and volume display anitialias |
| `3e7144e93` | Lame-o accordion animation on records screen. check it out |
| `f6554e8e1` | manta animations fix |
| `4d8041189` | stub for urchin barrage and fix a bug in the volume shader |
| `bd8e0d4bf` | refactor projectiles to handle their own momentum and detonate to be a method. urchins have spikes |
| `1acb74014` | Fun with menus |
| `efed8cb55` | changes to line rendering on projectiles and added a spike material |
| `2b516dd1b` | Small UI tweaks |
| `6895de259` | Straightened the Wedge dividers on volumeDisplay (made lines) |
| `14cdc5515` | Ai balance. Ai drifting. Ai pilots on crystal impact. tuning conic explosion |
| `5b075dc41` | Maybe resolve firebase exception "DllNotFoundException: FirebaseCppApp-9_4_0" |
| `04c2df09b` | Fixing exceptions |
| `52652c557` | Playing with video import settings |
| `743bbf3ad` | More monkeying with video import settings |
| `fb7afb319` | Add account manager and a catalog manager |
| `d8f84c9d6` | Show player display name on test scene |
| `3fa08a88b` | Test out functions in the test scene |
| `f7cb8b45e` | Add button to send a bug report email - placeholder placeholder values, needs design pass |
| `436bcdbcb` | Fix graphic rebuild loop exception |
| `28e007a7a` | Email by Native Share |
| `869fd845f` | Anonymous login and unlink tests |
| `262cf3426` | More test buttons for player profile and title data |
| `1177241d3` | Hacked in a fix for the graphic update loop error i've been banging my head against |
| `24de4799d` | Experiment with email login |
| `25fa280c4` | sync |
| `32b0dcd33` | Added Bug Reporting and Email Share |
| `d4ad4cb4b` | Authentication mvc model revamp |
| `0419aca15` | Screen swiper was deactivated |
| `fcd9777a0` | Random name generation works |
| `50d7bcd05` | Fix a null pointer on AI Pilots. Set default target platform to android. |
| `10c67e8d2` | Fixed name list fetching async |
| `9c68ab952` | Partial rebranding from MiniGames to Arcade |
| `b5be8a0a4` | Additions to playfab interfaces. |
| `1c5b47e14` | Apped Pip with scaling added overload for vector3 to lerping coroutine |
| `24da68714` | Pip changes |
| `95e3c3796` | merging |
| `c9b3b004c` | cleanup |
| `d3c1daa80` | Tested out new input system |
| `b7eaddb85` | Update platform input handler setting |
| `7abb2c63a` | Update right joystick scheme |
| `3ad5183b5` | Added a busy indicator to the text auth view. Starting to implement CosmicShore virtual item model and catalog fetching. |
| `77944e3e1` | Add placeholder email and password input fields and buttons to playfab test scene |
| `de81dd22b` | Add email login and register view checks |
| `e3bb551cd` | Add an email validator class. Thanks GPT4 |
| `0ef980357` | mass crystal |
| `9e7d5b733` | Little tweaks to email registration |
| `983f87971` | ai adjustmenes. fixed the normalization issue in the input controller. ship tweaks. pip fixes |
| `6ec99d423` | Changed register with email request |
| `3993ca7b4` | Added a busy indicator to the text auth view. Starting to implement CosmicShore virtual item model and catalog fetching. |
| `7546de8e2` | Add placeholder email and password input fields and buttons to playfab test scene |
| `6dfff1f65` | Add an email validator class. Thanks GPT4 |
| `4e6034ff9` | Add email login and register view checks |
| `ad2da0a6b` | Little tweaks to email registration |
| `495d6de82` | Changed register with email request |
| `129f9ca86` | Tested out new input system |
| `3edc81b6e` | Improve register methods (TBC) |
| `c243ceb99` | Migrating leaderboards over to Playfab with support for offline |
| `da88a648c` | Added error handler for anonymous login |
| `3599c2afc` | Added Favorite and Clout Systems - not linked to UI |
| `8dd37ebff` | Renamed VesselClout class to Clout  and added notes |
| `074b8e764` | FlightSchool rebranded as Elimination. Setup app for Playfab testing. |
| `1f5c61057` | Connect playfab SDK to new Cosmic Shore playfab title |
| `5a3c07670` | modified elimination game mode to create better intensity scaling |
| `e917c28ab` | restored the ability for crystals to explode which was used in flight school |
| `103b94c2a` | Introduce ability to pause games in between turns. Small refactors. |
| `da84292ad` | Changes to Favoites Struct Enums and Modal widow Prefab created |
| `11314d5f1` | Turn some more main menu singletons into prefabs. Fix some exceptions. |
| `d1817cc52` | Options modal is back baby |
| `d71ebb435` | stripped out color changing block explosions and added exploding block colors to material sets |
| `592d1fb04` | fixed merge errors |
| `565762b38` | Added leaderboard manager tooltips |
| `f1fe8e946` | Stuff a lamo display name input into the options screen. Add playfab title-data json file to the repo. |
| `de7afaea2` | Dumb typing effect when rolling a new call sign |
| `95f664acc` | Added mirroring functionality to Pip and a notion of team crystals and crystal theft. |
| `e5f9de4df` | Added Get Leaderboard Request to test scene |
| `3d672f91f` | Get Leaderboard by name |
| `d7104b44f` | Add callback to GetLeaderboard |
| `b9b1c85aa` | Segment code sections |
| `9cfc85487` | Added friend leaderboard, null check for result |
| `b410fe5ec` | Woops |
| `03a38ca8b` | innerDimensions is gone growing blocks are in. |
| `a4b39cac1` | Fix multiplayer round logic. WIP Playfab Leaderboard menu |
| `89e2fda7b` | Fix scoring in Dolphin Darts and Rampage |
| `10bd8e0c7` | Bug Fix: Scores Display shows scores at start of round |
| `0acd0a5ea` | resource events, energize action, firing patterns |
| `660836f8a` | Playfab Leaderboards! Major cleanup of ships/shiptypes && games/pilots/ship SOs |
| `e81c85c38` | Hide "Any" option in leaderboards if only one vessel class is available |
| `3ea249d95` | Added login remembered conditions and forget me |
| `4cea0aae3` | CatalogManager purchase item test and other stuff |
| `3ee1f7416` | Fix Duel for the Cell crystal |
| `3611d3dde` | the manta with anims and shapekey forms |
| `57a1b96e6` | Tweaks to urchin |
| `83fea9d18` | dolphin with shapekeys and anims |
| `1cd28a954` | Move front end test function to views |
| `f9b905cff` | Catalog Test view |
| `1cdeeccc3` | Bunch of cleanup. Fixed exceptions. Got rid of death events. |
| `a8d7be642` | urchan with anims |
| `d74ab7f52` | Rename FlightSchool scene to Elimination. "Fix" Elimination mode by not having the crystal explode. Minor code cleanup. |
| `647c91ed3` | Hacked in an anonymous explosion for crystal AOE effects. |
| `ae5d2f9e2` | spike progress |
| `7781e23fb` | rhino anims finished |
| `4becf0d85` | Rework catalog and inventory item requests |
| `26d5c1b3c` | mass crystal start |
| `e32a3075b` | Change Inventory item to its reference |
| `088574cf9` | Add currency (shards) test and inventory loading |
| `45c2ad035` | Regroup playfab scripts |
| `d2bc2a7fd` | recursive projectiles stub |
| `202c66110` | Moving Network Monitor and Virtual Item |
| `84e88adb2` | recursive spikes and material changes |
| `b9802309d` | Embiggen the Arcade game list. Block bandit preserves the previous player's stolen blocks. |
| `3bc89a085` | fixed blue block |
| `72224cc8d` | Stolen block colors are preserved in block bandit |
| `c9b99b85f` | Broke hex ring progressed chain reactions |
| `1eb12d723` | gemtrimass |
| `7a35d390a` | new mass crystal |
| `7373f4fa7` | Arcade icons in new ARCADE folder |
| `1dbb77726` | sppaaaaaaacceeee |
| `d279b20a4` | Made a loadout card prefab & corresponding monobehavior |
| `d0d13a520` | Rename ShipActionAbstractBase to ShipAction |
| `9a1175dad` | chain reaction progress |
| `a0c1cde1b` | FIX URCHAN |
| `a372e777e` | Loadout System added |
| `705e5707a` | sync |
| `9d54ecea8` | Plug maja's icons into loadoutcard. Rename levelawareshipactionabstractbase to levelawareshipaction |
| `572662c1c` | sync |
| `f4c7caa2f` | blendtree setup |
| `83d151202` | fully glorious chain reactions (death of northern lights) |
| `39a832f2c` | Fix compiler error |
| `821171903` | urchin animation stub |
| `bbfa0f73a` | Fix the build |
| `daf7242f5` | material tweaks and urchin test |
| `bff378a75` | Update New Animator Controller.controller |
| `5a00f4ade` | animation progress and old model cleanup |
| `460f5af0f` | Add background cards to games. Make a gamecard element for explore ,menu. Fix displayname input field in options menu |
| `9890daf78` | Manta animations wired up. Beginning work on explore view in arcade. |
| `f5febcf52` | WIP Arcade Explore view |
| `359eb86b4` | Explore View almost done |
| `a174c0186` | Spike update, with team materials, |
| `df1071ba7` | Photobooth + ship icons |
| `999e1987b` | swapped inputs for 1 thumb left and right stick actions to reflect gamepad. Energize swapped to guns from barrage spiking player progress,enhanced trial block bookkeeping Urchin full ammo on crystal added dash and grab minigame |
| `b1ae438b1` | Continued work on Explore menu |
| `5db4b18a2` | Cleanup in GmaeManager. Backup of arcade screen. |
| `25fae5edf` | LoadoutMenu |
| `144fdd819` | Fix Urchin mini game startup camera |
| `5a407d7c0` | Loadouts mostly working |
| `fdc54d6a8` | New Arcade menu mostly working |
| `eb44380e0` | Only show loadouts by default on Arcade menu |
| `bd86c2579` | Bugfix: manta notify nearby blcok count |
| `fe074470f` | super energized and energize spikes, fixed camer bug on urchin and manta ai trail, made resource events for ammo |
| `316d763c0` | animation manta fix |
| `8148eb6f5` | bugfix trail riding |
| `d5ac1eeaf` | Working on arcade screen. Fewer bugs overall, I think. |
| `b7a0db14f` | trail viewier material upgrade |
| `8c3bdb25d` | urchin can charm, steal crystals, material tweaks |
| `8aabbec93` | swapped rhino for dolphin in elimination, Added ability text for urchin in hangar |
| `f858a7e36` | urchin tweaks |
| `92bfc3074` | juiced up shielded blocks |
| `d968d9e96` | ammo gain is faster when attached to shielded blocks |
| `37d634d63` | ammo tweak and more contrast on  shielded materials |
| `be87bd2e1` | Remove Firebase from project. Add a stat to count gameplays. |
| `c2fdee474` | iOS project settings |
| `fa36969ac` | Improvements to loadout and explore |
| `18899b1d7` | reverted 1 thumb input for dolphin and urchin |
| `c55645838` | Fix crystal in battle mode. Fix party time when AIPilot is outside of a node when a crystal is hit. |
| `77cb9c2ee` | Dash in some DASH AND GRAB placeholder buttons. Cosmic Sans. |
| `fc403c693` | Last minute fixes to Arcade menu |
| `2bbfe7723` | Grab And Dash Button added |
| `9c5254546` | dash and grab video |
| `408e9b3a4` | Loadout menu tweaks |
| `821cc4e1a` | Fonts are sometimes blue. |
| `38262cb44` | Exclude elimination from the leaderboards |
| `3ecaf0cdb` | roll back to previous animation paradigm for manta |
| `aea9b1aa9` | Fix to loadout system |
| `e39d455c2` | Add preview clips back onto game SOs. |
| `0ff66d40f` | 12 -> 12 seconds on elimination |
| `234c21bf8` | Remember last gameplay settings. Update the DashAndGrab preview image |
| `e2e607425` | returning ship transformers all but manta |
| `78a144878` | minor changes |
| `07575b986` | Layout improvements to leaderboard menu. Highlight active player's leaderboard entry. |
| `479c7aee1` | Introduce ElementalFloats && ElementalShipComponents. Cleanup in ResourceSystem |
| `6abd99332` | Test Implementation of ElementalFloat on Rhino skimmer. |
| `9225fe5b7` | User custom data update, query and delete |
| `64c9ceeef` | Grizzly prepwork |
| `1181d7976` | Change circular trail back to trail block list |
| `05e7db682` | Generalise error handler |
| `ee135de76` | Added player event wrapper and player event post request |
| `3369a5520` | Fixed some Bug Reporting Bugs |
| `10249cd4a` | charged projectile fix and stub for disengaging turret on grizzly |
| `a2ba6b0b0` | elemental progress on ship actions |
| `c690003bf` | rewiring charge rhino abilities |
| `b3b1e1f57` | Added requests for playstream events |
| `55ffda00a` | Added write playstream events and playstrem events model |
| `f1b5faaf1` | Added tooltips to profile menu and bugfix |
| `cea421e88` | all 3 buttons self activate independently on mobile added look controls to grizzly fixed elemental float bugs |
| `fb7f6d1e2` | block materials fade when stealing, shielding, or deactivating shield, Exploding materials set back to transparent grizzly tuning |
| `b0e5841e3` | stop coroutines in trailblock |
| `43d222cf4` | grizzly fixed energy display and added stopping projectiles |
| `6e4068431` | Bugfix: authentication profile display name null check |
| `a9d77a4ab` | public eased inputs forr grizzly. Rampage no control segment count but does control spawn distance. made a spawnable pumpkin. made ellipsoid multicolored. ship and material tweaks. |
| `d4245f176` | minor cleanups and introducing DriftTrailAction to fix dolphin trail |
| `d65a51832` | Bugfix: display player name after return to main |
| `96b479dda` | Removed "DON'T PLAY 1 PLAYER" text in the Elimination object |
| `34c38cc10` | changed hud button sizes uder direction from WIll |
| `02bdeaa83` | ai retuning, fixed ai drift, toned down urchin chain reactions and pumpkins |
| `26daa0519` | Fixing build warnings. |
| `fd43b55f9` | Fixing build warnings. |
| `9c6d7501d` | Added creating group and deleting group |
| `cde6a08eb` | Delete a group |
| `cc6e24783` | ammoGainRate is now Elemental Float |
| `c462a5b8c` | Rhino Elemental Floats added |
| `12ac2084b` | Urchin DefaultThrottleScaler and ProjectileTime |
| `6d808e8b5` | Added min max association function in tools |
| `09de47016` | introduced object pooling for grizzly and urchin added elemental float for mass urchin |
| `ba9a30aa7` | minor elemental fix |
| `8f77c8b29` | Bugfix: Player profile null check |
| `1828c8032` | fixed object pooling |
| `cffa75da0` | fix build errors |
| `415a281d0` | Added firebase analytics and auth sdk |
| `e0bbbd200` | Update firebase project settings |
| `5dd8868b1` | Remove redundant configs and move relevant script |
| `a5672946d` | Bugfix: high score container null check |
| `96ed6c9c8` | Firebase helper for resolving dependencies |
| `5d9f1da15` | Added Firebase realtime database |
| `0a81be22a` | Added Firebase Crashlytics SDK |
| `b11fb5872` | Added Firebase RemoteConfig SDK |
| `0868bc28c` | Rename LeaderboardEntryV2 to LeaderboardEntry. Starting ground work for inventory stuff. |
| `db17ef192` | More Firebase authentication |
| `c78194e9a` | Added tooltips, login and register account |
| `d759ff529` | Re-arrange playfab test scene |
| `fcd41ac90` | Updated project settings, more Firebase stuff |
| `d0df1b2a1` | Firebase auth with different async and coroutine |
| `e93513271` | Firebase account creation and sign in |
| `9664148cd` | Firebase on auth state changed |
| `132549a2a` | Code cleanup and organization. |
| `7a2da7dab` | Firebase user profile stuff |
| `4b07c5d5e` | Firebase user verification and password stuff |
| `be53042d5` | Firebase event logging |
| `662102ebb` | Squads - WIP |
| `01e248aba` | Revert 132549a2 |
| `2a847b51a` | Reorganizing arcade game classes |
| `2fdf60c1a` | Added a Fancy Cam prefab to gather footage with. |
| `43234c52f` | Firebase Analytics Helper and Controllers |
| `06d134be8` | Added 1000 fish to a 3 scenes |
| `7deaeeed3` | Unity analytics sdk and initial setups |
| `aff1ffc34` | Integrate Unity Analytics to Firebase events |
| `609cf240a` | Rebrand Pilots to Vessels |
| `204e76305` | Squads work in progress (also fix the build since vessel changes weren't picked up in last checkin) |
| `7e3ef8465` | Null check on Firebase Helper Event |
| `b3142a5dd` | Unity Analytics checking user consent for data collection |
| `1f84ccb50` | Add function for forcing data upload |
| `be117ea3e` | Handle Hanger Menu Exceptions for Selecting Urchin and Bufo |
| `0e958f9ac` | updates to boids, intruduced stubs for a compute shader boid solution and a new block materials |
| `9611c80b4` | Added Grizzly icons |
| `cbb040673` | Saved icons to Grizzly |
| `036423176` | reworked grizzly controls |
| `804f8a2e8` | input simplification |
| `edd200ace` | Material tweaks and fixed an urchin boid bug, toned down boid count |
| `9d9b46ce4` | Squad building UI v1 |
| `85a73a7ff` | Add totally dope final image assets for elemental icons |
| `66c2a9b29` | Load squads into cellular brawl. |
| `7edd7923e` | new elemental images from grace |
| `add363e75` | Get rid of weird include in SquadSystem |
| `311204c6b` | Update SO_Vessel_Arcade_Default_Dolphin.asset |
| `0ca4956c6` | Update SO_Vessel_Dolphin_Slingshot.asset |
| `547fa4c36` | Update SO_Vessel_Urchin_Time.asset |
| `e748701d5` | Update SO_Vessel_Rhino_Dozer.asset |
| `afe3d18f7` | Update SO_Vessel_Rhino_Dozer.asset |
| `97e62f6c8` | Update SO_Vessel_Dolphin_Echo.asset |
| `1807b691b` | Update SO_Vessel_Dolphin_Echo.asset |
| `f0abf944e` | Update SO_Vessel_Dolphin_Maestro.asset |
| `b0db8eb97` | Vessel meta data update |
| `9ca2950b5` | Attach analytics helper for resolving dependencies |
| `44a423a58` | Changes to Vessel SO's and Vessel UI tag in Hangar |
| `d7098d479` | reordering game list |
| `e523f0ec0` | Error handling |
| `a4e6a277e` | Added PlayFab Unit Test Setups |
| `bdda331d4` | dolphin fix, commented out exploration perimeter aware touch ui |
| `31d1be6f9` | PlayFab classes namespace and file refactoring |
| `2d3d6bc7e` | BusyIndicator null check upon destorying the game object |
| `b306ffd4c` | Bugfix: ship animation - reset animation index out of range |
| `074e05dbd` | Bugfix: input controller gyro actions fix |
| `9d9e260d3` | Bugfix: Cellular Brawl error fixes, added TODO tip |
| `f169bbd83` | new fishi |
| `195cdc6b8` | Roll back changes boost action |
| `e5d9c6893` | Update fish meta data |
| `7df6abec5` | Bugfix: Trail loop is working now |
| `c89e5f937` | Temp bugfix: on going looping trail fix |
| `56e2464d2` | Vessel SO and UI changes |
| `892d8060d` | smallfri update |
| `c962c68d1` | Feature: Re-arranged script property header |
| `af115c5ee` | Bugfix: fully working loop and non-loop trail |
| `5bbb507f6` | Feature: In-Editor tool for opening scenes |
| `302e89847` | Refactor: Haptic Controller code clean up |
| `3ecedfce0` | Feature: update froget in-editor tools |
| `134123488` | Refactor: a lot of code rearrangement |
| `d9ae3d551` | Pumpkin container spawner changes |
| `331a9d9c1` | Initial work on the Recording Studio Tool |
| `63bd6aa8a` | Updated MIT Lisence and README for repository |
| `d199eb249` | Updated Froglet Tools and added Recording Studio |
| `ba87426cf` | README: minor changes on preview gif and stuff |
| `498f73049` | Update: remove redundant calculations |
| `009b616a2` | Catalog inventory and virtual item mapping |
| `74853ac42` | Prep work for Clout |
| `248e71c50` | smoothed gyro controls. renamed referenced to shipcontroller -> shipTransformer. added models and features to tadpoles. new materials and crystals |
| `8b7f77363` | post merge fixes |
| `a23290c3f` | Test: vessel upgrade purchasing test |
| `546f47eb2` | Added vessel accessor |
| `473d4a12a` | Optimize skimmer script performance |
| `e3a660136` | break coroutine returns better result |
| `0d5ea7e21` | Better changes on previous code clean up |
| `123f3d948` | keep skimmer conditional returns logic consistency |
| `ea6d5124f` | Vessel data accessor and catalog error mapping |
| `a22ba6338` | tadpole tweaks, icon mock up, squirrel stub |
| `cfc601d71` | squirrel progress |
| `86acf2bf7` | Reorganizing files in scripts folder - pass 1. |
| `5590583ce` | Purchasing Shards with Crystals |
| `aac3ba9b0` | Catalog test view update |
| `0cb2dbbe2` | Remove inventory collection tests |
| `2ee264c79` | building a squirrel |
| `e81eef55b` | Added Vessel Behavior and minigame |
| `da987150f` | Basic Call to Action system. |
| `33fa54d9c` | Replaced currency with item price model |
| `b826a52a5` | Changes to Recording Studio Scene |
| `16256bf14` | WIP Quest/Daily Reward System |
| `1617ab80d` | hangar bug fix |
| `4bc851a44` | commenting out play fab breakage |
| `4bef15a46` | Thumb Perimeters added |
| `1f0bf8143` | Minor bug fixes sync |
| `e73b27892` | Fix console warnings. |
| `8a7eceaf0` | Rewarded quests. Groundwork for SO_ArcadeGame supporting CTAs. |
| `0fce2b86e` | Some changes on catalog manager |
| `6f8fb8246` | Squirrel updates |
| `9b309a547` | Add CTA type to all arcade games |
| `f0c8d87cc` | Refactor - All First Party folders in Assets begin with an underscore |
| `f44313f5e` | Reorganized script folders |
| `10945d801` | Change root namespace to CosmicShore |
| `09419e2b7` | File organization and Namespace refactors for Scripts/App/* and Scripts/Editor/* |
| `4e6acb819` | Script and namespace refactoring - Game/AI/*, Game/Arcade/*, Game/Projectiles/* |
| `5543bd20d` | Moved Input to IO. Removed all instances of _Core namespace. |
| `84257c2e4` | File and namespace organization - Game/IO/*, Game/UI/*. Other namespace untangling. |
| `3059640ae` | File and namespace organization Game/Animation/* |
| `fba4204a9` | Game play quest rewards work based off user action labels |
| `a013da133` | bug fix and sync |
| `b00b458b0` | sync correction |
| `62f3bf4b7` | Commit for Will. |
| `eb5d0dc5c` | Update Froglet tools |
| `48e2c71ab` | Skimmer refactor and addition of features for squirrel, changed fixed update to .04 seconds. Fixed lots of ship bugs, fixed material speads, removed some impact logic from trialblock, generalized grow trail action, fixed some a haptic bug, added tadpoles to scenes |
| `8ccb08f76` | fixed a null ref |
| `52470411d` | cursor fix |
| `ec11c070a` | Update SO_Vessel_Dolphin_Charge.asset |
| `8423fc681` | Callbacks re-registered across scene loads |
| `f21a5322d` | Store placeholder. |
| `b5de15360` | Update PlayFab Sandbox |
| `ca07f84cc` | Thumb perimeter changes |
| `db2b260c2` | Changed tube visualization from rings to shards, Improved squirrel bounces, made alignement and visualization the default for squirrel, used awake in crystals to initialize their collision list and onenable for addself to node, crystals can increment level, commented out fliping ship upside down as temporary "fix" for squirrel animation, improving boids toward delivering element crystals |
| `dec49aacc` | Adding PlayerDataController - will support backend concepts of player clout and shards |
| `cc11f8b19` | Update player shard data and clout data backend |
| `f0f840ac8` | playfab client instance auth change logic |
| `3f14310f4` | Update SO_Vessel_Dolphin_Charge.asset |
| `bbbcca73e` | Update SO_Vessel_Dolphin_Mass.asset |
| `9e43a3640` | Update SO_Vessel_Dolphin_Space.asset |
| `a5c001622` | Update SO_Vessel_Dolphin_Time.asset |
| `11b3d4143` | Update SO_Vessel_Dolphin_Space.asset |
| `36b6c9e6b` | Update SO_Vessel_Manta_Charge.asset |
| `333dc9019` | Update SO_Vessel_Manta_Mass.asset |
| `25c6a2d08` | Update SO_Vessel_Manta_Charge.asset |
| `262e3aa0b` | Update SO_Vessel_Manta_Space.asset |
| `febaa0fdb` | Update SO_Vessel_Manta_Time.asset |
| `a07182fc6` | Update SO_Vessel_Rhino_Charge.asset |
| `398c7ca59` | Update SO_Vessel_Rhino_Mass.asset |
| `7fcb2fa79` | Update SO_Vessel_Rhino_Space.asset |
| `937908aa8` | Update SO_Vessel_Rhino_Time.asset |
| `38c99af0f` | Small project cleanup |
| `f5bf66e90` | Graphics optimizations. Project cleanup/reorganization |
| `362ba459f` | More image optimizations. |
| `5d98e8d17` | Image and audio compression. |
| `57065e4c7` | Last image optimizations for today |
| `01e653ef8` | Console logging for NodeItems |
| `ea087dd51` | Update clout system and stuff |
| `8f5318fe4` | Rework on clout system, sync clout data to PlayFab |
| `e5307e491` | Clout online and offline state |
| `246fd3b0f` | Update corresponding data controller changes |
| `661140f7c` | boid and material tweaks |
| `6b366e512` | Vessel upgrades upon vessel changes |
| `227fa8d0b` | Logging vessel upgrade to the console |
| `de3826910` | Update firebase related events |
| `0f2cd50df` | Firebase: user action events on screen view log |
| `ae9421885` | Resolve firebase dependency in one file |
| `83a1150e0` | New State Machine for ships and other systems |
| `ebec85fa5` | Move state system to app/systems |
| `44cd8b105` | Pushed Flavor and Behavior values |
| `269079b6a` | added a call to action prefab and a pulse script for it |
| `9ab67cd90` | updated thumb perimeters |
| `d7e75ccf0` | inverted throttle stubbed the logic for reverting in the settings |
| `f806df560` | switch pulse.cs to unscaled time so it will work when paused in the other panels |
| `b512fb283` | Learn about CTA system |
| `3ab4eba94` | Small changes to CTA |
| `1d0d60e85` | Store UI mock up |
| `cd5ed228b` | CTA on hangar ship tabs |
| `5e73aea19` | Changes to catalog manager and vessel model |
| `eb964fe0d` | Detailed warning message for catalog manager |
| `5d25f8c09` | returned crystal to old material and cleaned up thumb perimeter |
| `f8cb7db31` | Introduce ability to change player ship for minigames from the inspector |
| `83cd99be3` | setting up drift course |
| `cbe6769df` | Added Joystick Visuals Toggle |
| `998e9727d` | bug fix |
| `7e57bb8a6` | blocks are player aware and user player to retrieve player name. boids die when blocks are detroyed. stolen boid follow the stealing player, boids don't destroy blocks of the same team. |
| `b5275d714` | Rhino stun is now more of a slow, new minigame: denial, made a hostile manta in the hangar, added a slowed status for later use of rhino vision for capture, added a hostile volume created turn monitor |
| `316f8ac1e` | Update gitignore filter and remove build files |
| `3f3205535` | Minor changes, further tech design needed for VK |
| `f0c2bb42a` | Updates to the Recording Studio, Darts Preview2 asset, and camera controls within the recording studio |
| `9d0d34363` | Added, updated and/or removed Preview videos for main game modes that will make 12/15 release |
| `43786b197` | Update gitignore |
| `e9857acfd` | made a slow ship viewer for the rhino and othe tweaks to denial and materials |
| `c8c73938a` | Use hashset for ship transforms instead of list |
| `e683cdbab` | Create a UserActionTrigger for use on buttons |
| `d2a55d2ff` | Work in progress - New Hangar Menu |
| `e8b9d0015` | removed unecessary null check, fixed a null reference on ship when the ai was looking for hud buttons. |
| `ac103be1f` | fixed replacement shader on fake crystal not showing, made slow viewer only visible to rhino if player, setup vessels for training games, made a cat'n'mouse minigame, made a shipcollsions round stat, added the notionion of display to all turn monitors |
| `42af2dd83` | Drift Course aka Risky Drifness |
| `23401152c` | Intensity scaling on CellularDuel derived games, and tuning on CatNMouse training game |
| `850643ccd` | fixed turn monitor display in cat'n'mouse |
| `cee8a0948` | Polish on Denial and CaTNMouse, Added SlipNStride, PumpNDump, and Master Exploder minigames, call to action targets and minigame listings for all alpha training games, modified crystal so it won't double count crystals, made pumpkins blue, tuned rampage iintensity scaling, consolidated hostilevolume created and volumecreated Turn monitors. |
| `33272c45b` | audioassets |
| `56498524e` | lots of new training games, scriptable objects, polish on wip training games, assets and scripting for a dynamic dolphin silhouettes, and a contrianer on the hud, made a few events for ammo and drifting. made ammo accumulation turn monitor work with 100 percent, |
| `8a03bfab1` | KickinMass MiniGame and Spawnable Comet |
| `7b3691d50` | Explore menu display mini game cards correctly |
| `f9991b405` | Fix leaderboard display |
| `87a3c7759` | Display MiniGame cards based on card number |
| `d0bb10063` | Store menu no hiccup now |
| `31b559dd8` | Update some economy models |
| `bcf6029ce` | Update packages |
| `ad7e7454c` | Update PlayFab SDK, admin users reassign title id |
| `0d11b3378` | Sort out inventory and catalog changes. |
| `a2330b3b0` | Added flags to CourseMiniGames for intensity scaling, Added team aware silhouettes (using material sets) and trail displays to all 4 ships,  dolphin starts with no ammo except in darts, added a new cylidrical positioning scheme, added radii to the helix, attempt at fixing some chargeboost issues, renamed usesX to "displayX" in resource manager, dolphin and squirrel no longer use ammo display, added events forr block creation, reordered launch party games to showcase new games in explore |
| `ff80a6628` | Minor additions to targets and types in Call to Action system |
| `e51abb06a` | Added 6 minigame preview videos |
| `35e08c12c` | Spawnable Comet sync |
| `2585533bf` | dolphin Silhoette can pitch down,  wired up one of wills movies, AAll trail vizualizations display a total blocks area proportional to VPS/speed, made spawnable comet into a teardrop, tweaks to manta and rhino |
| `68ecbc2c8` | Added video previews, game card backgrounds, and prefabs for 7 of the training game modes (CatnMouse, DashandGrab, Denial, master Exploder, PumpandDump, Risky Driftness, and SlipandStride) |
| `aea7bbfc3` | KickinMass MiniGame Changes and added PositioningScheme.KinkyLine |
| `2d3c34f2a` | Clear local player inventory on loading |
| `a4d5996fe` | Added new class icons for sihouettes, resized blocks, tweaked ship prefabs, repositioned top display, tweaked rampage and SlipNStride minigames. added squirrel app shell scriptable objects, and rhino arcade vessels, and modified rhino sihouette to swing out |
| `6718624d6` | Sync |
| `8a11d2661` | Fortnite Launch Party  UI changes |
| `fea784392` | Support limited intensity ranges for games. |
| `242110abc` | squirrel images, prefabs tweaks, minigames tweaks, score tracking changes, fixed the node item double add bug, restricted player counts, and classes SOs |
| `f09ab5d42` | Replaced Squirrel Button |
| `014325ce5` | so intensity changes |
| `12e5d53e6` | Added the correct prefab to the slipnstride game mode |
| `ee46f7b48` | UI Tweaks for special fortnite event. |
| `605da86f0` | spawner changes to recording studio |
| `3ce079c73` | Rapid changes to game mode and scriptable objects for the limited release |
| `7ba4d2ea3` | Added Slip N Stride Button for records |
| `2faebfe44` | Golf rules support for leaderboards. Remove excess warnings. Remove test quests and CTAs. |
| `04481d17d` | Updated store nav bar button |
| `ad0dd8186` | Clear out all test calls to action. New image icons for ship classes in hangar. |
| `6a3d23d4d` | Cut slip&stride block count in half. |
| `9f4779ed4` | bumped green block shillouette opacity to 100 |
| `6d7e553d3` | Disable soar/smash first time app experience for fortnite event |
| `f9fccf78a` | New App thumb |
| `1ce1cf89a` | Change snow opacity |
| `5614cefa0` | Rid of debug log error |
| `dd4af428d` | Various last minute tweaks for fortnite event. |
| `d8a38898c` | activated denial |
| `ba5f47f06` | serpent stub |
| `1318f50d2` | minor fix for hangar menu ship counts |
| `39fc8c7d5` | saving wall progress in source control |
| `e5339891d` | different approach to self assembly |
| `785d2bfa5` | self assembly progress. this is a fun state where th serpent makes little clusters during flybys |
| `59eeefc91` | self-assembly progress |
| `e911fcb0e` | Massive progress on self-assembling walls |
| `186670a2c` | continued development of the serpent including boost and wall building actions |
| `787a0e599` | performance tweak on boids, got rid of tatpole tempest, blocks steal mor gracefully, little cleanupp of wallassembler |
| `1c2e97a9c` | Added SuperShields |
| `0653909a7` | Fixing bugs and tweaking performance in wallAssembly |
| `dc612f6ed` | Termite beginnings, Saving and loading game state set up for elimination game mode to save blocks and diffs in blocks, got ri of shioControlOverrides in favor of its own camera customizer component which can control follow targeet as well, |
| `7716a05a5` | Termite beginnings, Saving and loading game state set up for elimination game mode to save blocks and diffs in blocks, got rid of shipControlOverrides in favor of its own camera customizer component which can control follow targets and set orthographic as well, |
| `6a34cc832` | Added save and load to StateTracker |
| `8ced7feab` | Suggestion for StateTracker |
| `57d9f0bb5` | fixed a mistake |
| `0c1bf7e15` | Dependency Injection and useful framework imports |
| `9796be269` | Update firebase event parameters |
| `2b0b1b844` | Update NLog framework and scene test |
| `19701b833` | Test nlog with zenject |
| `4f5528409` | Base installer for dependency resolving |
| `b420e5f84` | Serializable on setting wrapper for installers |
| `ea75eff45` | IIntializable binding, but not recommended |
| `9a02f1ae4` | stub for resource UI refactor,  stub for termite/commander Camera and controls, expanded boids to eenable drone behaviors, fixed some wall block bugs, flags for min and max sized blocks, fixed gyro bug, created a post processing manager, |
| `5f6b8d8c7` | pushing autodeletes |
| `2670545a5` | Added MiniGame Editor and Template |
| `74959846f` | sync CODE ASSIST |
| `775c29254` | Add gitignore for autogenerated meta |
| `8bfa97991` | Added SO_Arcade changes |
| `198c351e8` | Remove zenject for vcontainer |
| `b1d88e068` | Update vcontainer package |
| `c9df4bf06` | Update relay and app-purchasing package |
| `acb38f403` | CreateNewClass Added |
| `39591c2e4` | Implement new settings design from figma in main menu. |
| `798ad1290` | Sync |
| `a7daeffbd` | Command pattern scripts and tests |
| `3b621559b` | Code clean up for wall assembler |
| `b4c048d15` | sync |
| `d24271ce7` | WIP Adding profile screen |
| `53223f597` | VContainer tests phase 1 |
| `0000497c3` | VContainer tests 2 |
| `b3e344bd1` | Multiple entry points and interface binding |
| `0e2f56cd7` | Multi interface and fix resolving issues |
| `a2657cb5d` | Recording Studio tweaks for Will. |
| `f2aab9429` | life time scope exception handler |
| `b29c933bc` | switch loxodon framework to package manager |
| `af0c70cf0` | Update loxodon framework package through manager |
| `4c53bc5e0` | mvvm progress bar |
| `e8e127eb7` | sync and async messages subscription |
| `0dc804411` | register via delegates |
| `f8df2c5c1` | factory registration |
| `11fa836aa` | Package updates |
| `94c95c437` | Update playfab sdk |
| `b4dba5b20` | register scriptable objects |
| `71a29066d` | register collections |
| `af51f9618` | turn on vcontainer diagnostic |
| `428be7fcb` | changed serpants wall to a prototype gyroid |
| `8610c6c96` | Object serializations |
| `0d0de1c06` | gyroid refactor |
| `ca7612d9a` | remove loxodon framework, too much overheads |
| `fc9fafbd9` | temp fix for seed wall action |
| `a2383d5b2` | Minor fixes and add game object extension |
| `92d67f584` | GYROID!!!!!!! |
| `4536dc3fd` | Ai Updates |
| `1c0fcb2d0` | scene save |
| `aa1082e8d` | materials and gyroid tweaks |
| `5402466d3` | scene saves |
| `6c77f09b3` | fancy cam got extra fancy. some gyroid classes enums etc. pulled out |
| `48ce2d6ef` | Some work on player data handler |
| `4a5914d29` | super extra fancy cam |
| `1bca9d49d` | super mega extra fancy cam |
| `03f74ee13` | some changes on namespace |
| `0a670aeb3` | change bond mate to gyoid specific |
| `701d79e07` | Helping boids gains the ability to make termite mounds and assembers have depth |
| `13a619d03` | progress toward termite mounds |
| `73c8e96da` | Random changes |
| `f6a6de43e` | sync |
| `61035da7a` | Avatar loader for player profile images |
| `ddd6a4d16` | Slow migration on controllers |
| `e4d69aada` | Support for updating and retrieving player avatar url |
| `0f4b19c31` | Try new rewind system |
| `a046a9dca` | experimental materials. progress on termites, generalized gyroid, fixed some stats bugs |
| `21ee1d7ff` | Add save load support class |
| `ca697e3c5` | new materials and blocks |
| `f5a6b12d9` | Rewind base class and circular buffer |
| `14e7a267d` | Complete circular buffer |
| `1bb5ff34d` | rest of rewind system |
| `8880e6d1c` | Add generic rewind |
| `452a5b3d0` | Add optional particle settings |
| `f9b5ccfce` | Firebase controller fixes |
| `f270e318e` | Rewind system adoptions |
| `db68aad82` | rewind system remove redundant directives |
| `05ec68e0e` | Added transform extension for rewind system |
| `423f50a10` | Add optional drawer |
| `08c630083` | BIG PlayFab services transfer to vcontainer |
| `bf5707146` | Resolve menu injections |
| `c49da964d` | PlayFab SDK update |
| `48f16bdd7` | Main scene udpate |
| `81d078445` | Remove redundent functions for leaderboard manager |
| `5cbe629d1` | Remove profile script on settings menu |
| `6b7e52002` | WIP for autonomous bot battles. Renamespace hangar. |
| `19031c031` | Fix main menu issue |
| `e9edb8331` | Fixes for Rewind Drawer and Leaderboard Manager |
| `b30181aed` | Add quantum console to display logs for server |
| `cd4d5566b` | Add ParrelSync for multiplayer testing |
| `83239d680` | Fixed bugs in tracking block size changes. Added a button panel class that can handle multiple button configurations. Stub for experimenting with blockscape experiments. Fixed boid bugs. |
| `e85045429` | Update screen view to custom event |
| `8d18cb060` | Add lobby handler and debug extensions |
| `cc8a7e5bc` | Update rider and vcontainer package |
| `e8b1537c9` | Restore playfab managers to persistant singletons |
| `944a4f0ac` | Clean up code for recording studio |
| `16b0a2394` | Fix null instances for data models |
| `4492ef85e` | Fixing missing instances |
| `6ade9d7cf` | Update playfab sdk |
| `7a9f3ff84` | added node and ship tap actions, reworked commandship movvement and UI setting up for cards. Made mound and queen creaion and transfer actions with a boid controller that extends from the boid manager but is a ship component. |
| `da2d41961` | fix on drone transfer |
| `53cd64d91` | Simple client manager call aws api results |
| `7178f0034` | Hangar Graphics |
| `5c8af7f9c` | Falcon framework and UI tabs |
| `43ee1d390` | ship HUD and termite cards |
| `3d665a107` | wormy worm |
| `19ae8c494` | Added linux as a build target |
| `412977665` | Added linux as a build target |
| `c55e1eb00` | Adding missing meta fiile for worm fbx. |
| `586360a69` | Upgrade PlayFab API |
| `0cb6a20c7` | Get server time and get daily reward store |
| `ec5f9e486` | Retrive daily rewards from store and store locally |
| `bf738ac51` | Daily reward info and playfab utility |
| `64c08ed4e` | falcon class, Lifeforms, branching flora, worm fauna, brighter crystal falcon guns, abilities etc. Gun transformer, friendly fire on projectiles Made more ships playable on more games, |
| `34e186e99` | branchingFlora progress |
| `0fa9d59d9` | Move main App Nav to right hand side. Restore Hangar functionality. |
| `72a05225e` | Tighten up daily challenge card timer logic |
| `d315688e9` | Rebrand Vessels as Guides |
| `37b2be28c` | Put something in port so the folder will show up in source control |
| `8d4fc00a5` | Port asset upload 1 |
| `d44803eb2` | Port asset upload 2 |
| `fd2ee2bbe` | Port asset upload 3 - faction tab |
| `b482eaeb9` | Port asset upload 4 - friends tab |
| `0f715d353` | branching flora progress |
| `7efb84f76` | Update PlayFab SDK |
| `283fbceca` | Update ui assets and meta files |
| `f442d22d1` | Cancel button correction |
| `fb4bb262a` | Set playfab title id to stop exception. Catch exception in CallToActionTarget and instead log warning with GameObject name misssing CTA indicator. Introduce a vertical InfiniteScroll menu item for use in the hangar. Add .meta files for graphics files Cameran added for the Port screen. |
| `54bbae50e` | scary push of objecs being auo creaed. theres no way out but through. |
| `4dbc7c474` | scary push: the sequel |
| `602747970` | scary push 3: electric boogy |
| `bfd3931a5` | merge |
| `b36903bdd` | merging again |
| `cefa265ff` | Branching Flora progress |
| `d98dc085d` | more branching progress (now in 3d!) |
| `5f8c1e8f6` | Let me just remove this |
| `c4a0d9a7f` | flora updates |
| `94596aa86` | Continued work on Hangar. |
| `f276d8da8` | fix for fauna faux |
| `9aba52cf6` | Iterative progress on hangar |
| `25f6fe912` | shrike creation |
| `0dc5dd426` | Cloud script test |
| `a877105e6` | elemental shards container and new nav icons |
| `d8116bb7b` | ability button assets |
| `c7ae020bb` | hangar asset reupload -bigger |
| `dc3f72d9d` | hangar overview button placeholders |
| `8a19f3833` | Spritify some UI assets. Some file organization. Use new nav icons. |
| `5e099fc0d` | Add new nav icons to main menu |
| `f66dcfb5d` | xp icon asset |
| `c3e17c644` | UI Tweaks |
| `555c2e113` | Update icon xp meta data |
| `2f1785a3d` | Minor code cleanup |
| `a1334ecd2` | Cleanup some console errors and warnings. Minor UI tweaks. |
| `2b8857412` | updated buttons for DMs |
| `f19a21748` | worm head |
| `964c817cd` | worm body |
| `3f15bf021` | medium fish |
| `d2b81d39c` | Added new minigames, Population, Assembler abstract classes |
| `5422ca2f3` | Test on cloud script, return message with playfab id |
| `bae93ec81` | added AssembledFlora. reverted serpent to wall building. made a gyroid flora. |
| `e1edde616` | Update PlayFab SDK |
| `119a9f924` | Test to PlayFab Sandbox, handle error through utility |
| `e394e83dc` | Namespace fix |
| `e6585c6df` | Switch entity id and type to static fields |
| `6972a4595` | Add save daily reward cloud script |
| `df1af4eb9` | Update PlayFab Editor Extensions |
| `a6d2125f4` | Update PlayFab sdk |
| `888ac100c` | Rename to DailyRewardHandler |
| `f82898e00` | Add handlers to the main scene |
| `0082fe46b` | Refactor explore menu to pass scriptable object references instead of indexes for selection callbacks. Continued updates to MainMenu UI Continue renaming Guide to Captian |
| `049fcf14b` | Add Rhino as a default Captain for Rampage. |
| `991d0e73a` | Use entity key for auth, add docs |
| `f4231c4f9` | Bundle related requests and testing |
| `9ec988180` | Purchase bundle tests |
| `0e58795d9` | Small changes |
| `ac42ebf52` | Fix exceptions and warnings. Arcade game view scrolls. Convert icons to sprite 2d. |
| `6137583b8` | Add docs to Catalog Manager |
| `a856f6f8b` | progress toward growing assemblers. |
| `ddd0ea8c6` | Refactor DataAccessor to make it a static class. Create a HangarShipSelectCard class and prefab. Tighten up Hangar UI |
| `931e10f9d` | Add doc to daily reward handler |
| `280ceaa99` | MiniGamePool |
| `c49e56ad6` | MiniGamePool Update! |
| `690b045a2` | Add bundle through server |
| `576e2711f` | spindle materials and quadfish |
| `81b06cbb9` | fixed missing reference |
| `4a46278b2` | Remove Code Assist and Quantum Console |
| `f07d2e0a2` | Remove QS prefab in the main scene |
| `ead48860c` | VContainer update for future DI |
| `2672ee1a6` | Remove null assignments from OnDestory |
| `a457d52fc` | Remove redundant registries |
| `d2602bd3e` | jet fx testing |
| `943fd7f35` | Jet FX and spindle testing |
| `c36ca19e5` | Minigame_Pool Update |
| `7977cb5f8` | Working on Showing Captains View of Hangar and related project cleanup. |
| `0b7b1f820` | Updated spindles, added sparrow class prototype |
| `bb47e0e96` | Label ElementalFloat properties in the inspector with the name of the property in script |
| `9affb6a0d` | Rhino effects |
| `8f431afc1` | sparrow and skyburst progress, modifications to effect property modifcation scripts |
| `dbcc72d2d` | Iterating on hangar menu |
| `977449ff9` | Add cloud script runner for funtions |
| `7296571b0` | Simple gui for testing |
| `b91ec0723` | Remove errors for compiling |
| `391092349` | Add claim button |
| `44dda86d0` | WIP refactoring and updating hanger UI |
| `b7c1faf0f` | Update gitignore to exclude some folders |
| `9c7996705` | Try out event bus |
| `04e489492` | Login UI for testing |
| `bf856b740` | Publish login events from authentication manager |
| `de2b3f822` | Clear events on object being collected by GC |
| `3db177c95` | Worm Progress; spindles now evaporate on lifeforms. |
| `1484811fd` | Add Observer and Subject base |
| `33637821d` | resized crystal models, made a condence function for spindles on start, moved activate() from lifeform to the crystal and added a color transition. healthblocks can now reparent. |
| `2696aa1cf` | fixed a scaling problem with block explosions, scene and script cleanup |
| `955bc08e9` | previous commit contains messege |
| `b09ebc721` | fix to the  ondestroy bugs during scene closing |
| `fca4a9bfe` | Sparrow progress, projctile end effects, assmbler flora progress witth gyroid assembler |
| `0a0bdc64a` | Some code improvement |
| `f37787b77` | gyroid progrss |
| `2094f4c69` | gyroid flora progress, branching fauna is now properly crystaltropic, |
| `e884f20cd` | A bit code improvement |
| `f62e260e3` | Test out chain of responsibilities |
| `5dfb51fa4` | Command pattern for executing individual actions |
| `1520633d3` | Remove redundant prefabs |
| `8f56d0306` | Functional gyroid plant |
| `99098c5fa` | Gyroid plant improvements |
| `d45241f08` | Assembler fully bonded check simplied |
| `9a4d0c3e0` | Remove redundant code |
| `bfae4ce22` | Update playfab sdk |
| `c2a026740` | wallFlora progress |
| `a49fecc08` | fixes to wall, updaes to teams on flora |
| `86a45c263` | Remove redundant code |
| `9fb61945d` | Made space crystals animate, gave lifeforrms a unique id, Population and fauna spawning stubs stub, Elemental crystals don't move, gyroid tweaks, drifting incrases turn speed, fixed a skimmer bug produceing negative counts which breaks the dolphin charge up |
| `17c965985` | A framework for a basic message system |
| `84b39c173` | nodes instantiate populations on intervals, randomly team plants, crystals can now grow (used by assembledFlora), fixed lifeform team bugs, added an overload for Lifeforrm.AddApindle(), continue turning boids into lifeforms, cleeaned a lot of log statements, modified many prefabs |
| `87186b03c` | Cleanup in node.cs and using the node to spawn main menu fauna |
| `e5e5f7407` | minor fixes to flora |
| `7391141fd` | Update package |
| `988907dbb` | Package updates |
| `8fce92d64` | Update PlayFab SDK |
| `0db92dd0d` | Refactoring UI so we separate between minigame and ship UI, implmented on rhino, dolphin, serpent; Various changes to resource system and tweaks to srpent; buff the squirrel |
| `4bcb8284f` | Minor refactor for shielded and super shielded state |
| `59ad07b80` | Assign trail blocks to TrailBlock layer |
| `073a0cdde` | Add ObjectResolver to get instances from prefabs |
| `f6518bd39` | Activate shield when crystal hits trailblock |
| `f50875737` | Collider layer changes |
| `e7765acb6` | Made ship hud buttons work |
| `6d29ddfc6` | fixes to hud bus and build issues |
| `07c6f33fe` | Added nerve flora which spawn a secondar plant to form a synapse and other new flora |
| `1d515a0ba` | freestyle uses random flora from a new floraCollection scriptable object |
| `db0e19e25` | put a rigidbody on the crystal so that crystals can briefly shield trailblocks |
| `b5cb63952` | new mass crystal stub and file org |
| `c0573bf92` | Add monochrome octree structure that updates on block creation |
| `35fcd96bf` | Revert factor of two scaling in Node.ContainsPosition |
| `c4f9c4772` | mass crystal progress, and spawanable pumpkins can select color |
| `b0b89b3c4` | Debug (temp changes) |
| `5695e91ad` | saved scene |
| `84564883a` | Mass crystal progress |
| `5d7cda470` | Address Iggy's feedback |
| `f210e4c05` | Use float, not double |
| `a8e108f0c` | Add TODOs |
| `d7d57735b` | Tune params for performance; create lightweight script to search for densest nodes only once, in test harness |
| `81eb4c3c8` | Fix settings modal half off screen in main menu. Extend NavLink to support select/toggle type views |
| `ca077b895` | Fix bug with profile image selection modal window placement |
| `60042865b` | Rename files |
| `567d5a2a8` | Triplicate existing octree so there's one per team |
| `68aaac660` | Try another version of messaging system |
| `1dc52c606` | Unsubscribe from message system |
| `682338be0` | Mass crystal |
| `cc4de23ae` | Add docs |
| `f306679dc` | Put changes in separate files |
| `3d946da34` | Move state machine to experimental architectures |
| `bb5669b76` | Finish message system doc |
| `eb19cbcc5` | Implement disposable subscription |
| `ea6aac3af` | Bunch of work on the hangar. 95% complete for Overview nad Abilities views. Re-encode ability preview videos to fix console warnings. Import a bunch of UI assets. Placeholder Captain images. Placeholder profile images. Ship classes extended to include Gameplay Parameters (e.g Casual to Challenging). |
| `163f523de` | wired up to boids |
| `6495185b7` | using public team |
| `4eb8511f2` | fixes to manta crystal blocks not shielding or growing. and dolphin blocks not growing |
| `72833f042` | completed mass crystal. moved the node model into the node prefab so node children wouldn't have weird scaling issues. |
| `f5d032aff` | follow up scene saves |
| `b4b3d0880` | Connect profile icons to Playfab AvatarUrl. Add some placeholder icons for variety and create a view to select your avatar. |
| `f17fca154` | Daily challenge is the same for all players based on today's date. Currently only client side. |
| `7b1bb2655` | worm stup, jet testing, main menu fix, added depth to block shader |
| `064ed0fbb` | tweaks to block graph and fixes on many scenes |
| `fea81bbcf` | blockgraph tweak |
| `93835c51c` | Re-organize clout system |
| `b9a055a4e` | Rehydrate on player data controller |
| `1bc16f5bd` | worm progress, block mateerial updates, |
| `a61635c5c` | Add docs |
| `457153d2f` | Add self-managing disposables and test setups |
| `f20726433` | Daily Challenge is playable and logs to it's own leaderboard. Created Arcade.cs to manage game related app functionality. Centralize game launch logic. |
| `dd592b03f` | worm progress, split explosion into damage and eexplode, added null check to oct tree |
| `f1e9968be` | Add leaderboard to Daily Challenge. Limit number of attempts to 3/day. |
| `d7a75aeef` | Trying out testing framework |
| `a8845d425` | Normalize all fonts to use Aldrich. Start adding profiling tools and look into performance optimizations. Cleanup vestiges in Main Menu |
| `ec84b129d` | mass and space flora and fauna, scene fixes |
| `00751300d` | one more scene save |
| `733e7eab3` | Made my own Recording Studio |
| `2473942cf` | Material and scene changes, new domain specific danger blocks |
| `551227571` | Changed some things in my recording studio for recording the broken mass crystal |
| `2c3e8f04f` | DailyChallenge is mostly feature complete. |
| `55d876c48` | Clean up button graphics - deleted a bunch of buttons with text embedded and made blanks, moved stuff to legacy folder. |
| `e2e2f07a3` | Delete unused buttons. |
| `ca3ea0e99` | Delete unused button. |
| `e14f64160` | More button cleanup, start of store UI. |
| `d642085d0` | Daily Challenge and Faction Tickets are available for purchase in the store! Misc other bug fixes. |
| `42eeafaf2` | Daily Reward wired up and works with Ads. |
| `50d964c97` | Bugs in store UI - overly big buttons and couldn't scroll. |
| `501752468` | Continued work on Stove UI. Clean up errors and warnings in the console. |
| `64e29d1fb` | Add a loading screen to the main menu to give it time to inititize network connects |
| `8cdff7dfb` | Add a timeout and offline message to network init screen |
| `d70d36b88` | Update build setting |
| `eef364417` | Initialization bumper was malformed after merge |
| `92ae0d0ff` | Initialization Bumper was malformed after merge |
| `de3f83712` | Fix exceptions. |
| `4ca4c9984` | Re-encodegame preview videos for size. Add re-encoder batch file to source control. Resize game preview cards for compression optimization. Add better placeholder graphics where needed. Fix exceptions and warnings. |
| `c88154065` | Remove parell sync - not currently in use |
| `5091aba88` | Preparing to implement Game Luanch UI as a modal. |
| `74f7920ff` | Games can be favorited (stored locally) and are sorted by favorite status then alphabetically. Remove some unused code. |
| `89e4c0591` | Turn arcade game loadout screen into a modal and get it closer to Figma UI. Turn game cards into prefabs. Other small refactors and cleanup in related areas. |
| `808bde627` | Clean up test scene missing scripts |
| `8a0cc44d9` | Repurpose clout for captain xp, remove legacy code |
| `9a6aef50d` | Dust off Cellular Brawl preparing it as the proto mission level |
| `8d3c23516` | Add XpHandler, generalize get player data logic |
| `04a157ba7` | Minor improvement for xp handler |
| `ad6a6d158` | Code review pull request for the animation recorder. (#25) |
| `216b5ad0b` | Hey look - a Port. Also, remove some old cruft. Some file organization. |
| `34f768c31` | Lots of worm progress, and implementted danger blocks, gave serpent wall danger blocks. |
| `4e54dade5` | Fix bug in squad member configuration view. Arcade supports filtering out un owned ships |
| `1f400cbef` | These keep getting auto generated when I go into Unity. Checking them in I guess. |
| `3be176581` | SParrow: pulsefire lasers and skyburts rockets, |
| `d505fff1e` | Create a Captain Class and Captain Manager class to support dynamic data (in addition to the static configuration provided by the SO). Arcade supports filtering out unowned games and ships. XP is loaded from the backend and can also be issued. Starting Inventory Items are granted if a player's inventory is emtpy. Converted Granting Shards to Granting Elemental Crystals. With both elemental crystal and xp being issued, Captain Upgrades coming soon (tm) |
| `9062caeed` | worm progress, introduced inertia and momentum for block collision. Adjested sparrow camera |
| `2d1b156dc` | Add a item id in assertion, you know where to look |
| `ac4547b60` | WIP refactor - pull captains viewout of hangar menu into own class. Create class for managing captain select buttons. XP and Crystal requirements are data driven. Captain level display is data driven. More movement toward captain instances and away from SO_Captains. Failed attempt at making a Product Generation tool for playfab. |
| `a9483eca1` | Fix a null pointer in store |
| `0c2afdfbb` | Disable product generator tool and remove playfab admin api for the time being |
| `3b581e09d` | Captain upgrade button works |
| `9ef0f8b4b` | Upgrade PlayFab SDK |
| `5bf5c3d8e` | PlayFab http? |
| `004e87a7b` | Replace octree with simpler grid |
| `37f8bea36` | Rename test harness |
| `3c230d86f` | Clean up |
| `3935e7c71` | Captain purchases have a finished modal. Juice when redeeming daily reward and buying captains. Upgrade cards reflect captains level. Catalog product loading is now recursive to get past 50 result limit from PlayFab. |
| `55ecf1833` | Plug sparrow into UI. Misc cleanup |
| `2fc9f2503` | Adding placeholder content into app shell |
| `99bc13079` | Add missing meta file |
| `f90350c62` | A bit code improvement on daily challenge |
| `b1ff9d8cb` | worm refactor, import SSU shader and supporting components, Fixed skyburst rocket |
| `753b7f48c` | All the placeholder captains except Mason added to the app. Larger captain images in Hangar and Port |
| `97cdbe851` | UI code file organization, naming, and namespacing. Convert void void Delegates to Actions. |
| `2d9bbfeaa` | Fix captain aspect ratio in store. Show captain image in purchase confirmation modal |
| `6fc065bed` | Add a lame-o Mason placeholder image |
| `62425f7c9` | some code cleaning |
| `61a77a6dd` | Juicier currency balance updates. Larger inventory pagination size. Filter out owned captains from store. Filter out unencountered captains from the store. Store supports a dynamic number of captains. Store scroll view hieght is dynamic based on content size. Fix catalog view not initializing after playing a game. |
| `b23b858db` | Games available in the store. Misc little bug fixes. |
| `3498dce2b` | Tidy up store layout spacing. Better support for multiple rows of games in store. |
| `cd0ef9d9d` | Hangar training view v1 - supports playing the game + everything is data driven. Still need final intensity icons. |
| `354491e96` | Proper intensity buttons in hangar training view. Misc bug fixes. |
| `f4d44c429` | Add Save to Pref subscription for future references |
| `86d711b8d` | Added Maze Runner to enums |
| `c5148c237` | Changed system random to Unity's. |
| `bd7cb34c5` | App shell audio pass. Centralize audio file configuration. Scene transition into/out of game. Touchup in game UI. Cleanup so audio bugs. |
| `24e50f834` | Delete and organize some of the older design assets. Fix a bug in squad configure. |
| `c5c195c8c` | serpent slither progress and new LSystem spawnable |
| `6c915b8e2` | Convert all modals to inherit from ModalWindowManager. Address canvas inconsistencies across Main menu vs in game. New intensity buttons in arcade configure modal |
| `d133efe4c` | Serpent clear prisms feeature |
| `ca49bb76b` | Fix Profile window not showing up |
| `ce216ccd6` | New intensity buttons in arcade configure work better |
| `0a277fd3f` | clear blocks respects team color. updated material sets to include transparent block materials |
| `3d7c1a14c` | undid changes to main scene |
| `2ccd3c273` | added clear blocks to dolphin, some new positioning schemes and segments |
| `cbda80f82` | Some modals weren't showing after inheritance refactor. |
| `186010e8b` | Show player avatar correctly in selection panel |
| `efe53c3dc` | Quick pass on Settings and Profile view UIs |
| `05b6ecf70` | Show player's name on eng game score board |
| `8cdd1a915` | New player count selection buttons in arcade. Refactor selection button architecture a little. |
| `8d9896abb` | Delete unused funcions |
| `79e30e148` | Hilbert Maze Spawner |
| `d8e1e6eda` | Move ship selection view setup into Arcade modal (out of arcade screen). Ship selection for gameplay is remembered again. Fix bug in port not showing correct squad memeber details on first load. |
| `6a0b76630` | Update daily reward handler |
| `80609b8ed` | Add intensity icon and class image to daily challenge view. |
| `8b17402bd` | added new stat tracking for omin crystals and elemental crystals, and a new minigamee: maze runner that uses elemental scoring |
| `c877aab26` | Change logs and docs |
| `cdd01d2c0` | First pass - migrating to new script dedicated to showing end of game scoreboard, Split single and multiplayer scoreboard views |
| `a9dac6525` | Remap team names  - Green->Jade, Red->ruby |
| `60e2a8535` | Finish cloud script runner |
| `6f0cab839` | Tweaking end game UI. Fix a null pointer |
| `903c16cbc` | Show crystal value on daily reward card |
| `fd6702a86` | Bounds check in BlockDensityGrid to fix console errors |
| `3ce0e1c8f` | Return to last view when exiting game. Add juice to collecting Daily Challenge rewards. Fix bug where ship can't fly if player name has never been set. Remove overly verbose logging. |
| `d4849be35` | Remove not used scripts |
| `bf7375b59` | Daily Challenge Tickets have a confirmation modal. Daily Challenge plays require tickets. More stuff updates in realtime when purchasing or using. |
| `f52bf66f1` | ClearPrisms fades now. Sparrow has extra an extra spherical explosion, added some geometry utiliities, made danger blocks in the maze |
| `754b8435b` | Remove testing hook that made Daily Challenge reward buttons active after claiming |
| `fcc5ba231` | Add confirmation modal to captain upgrades. Upgrading updates the Captain selection card immediately. Fixed XP overflowing when 500 xp reached or crystals greater than 1000. Show actual price of the upgrade in the requirements view. Fix a bug where upgrades skipped a level. Fixed a bug showing the wrong upgrade level. |
| `41a9acb8d` | Switch over to new PlayDailyChallenge endpoint. WIP on training game progress |
| `1b7005d41` | Dust off elemental float system. |
| `b9f2344ba` | Use generalized cloudscript runner for functions |
| `c1473c638` | Buggy version of encountered captains. Bug fix coming after merge |
| `affbc876e` | Captain encountering is a go |
| `f455b4337` | Dusting off elemental flaots and resource system. Introduce placeholder elemental crystals for each element. Test scene for tuning elemental parameters. |
| `6c51141dd` | Having fun with placeholder crystal visuals |
| `56110efad` | Give placeholder charge crystal an explosion |
| `fa7cb409a` | Granting Charge Crystals at the end of mission gameplay! Also, fixed a bad bug I introduced with AI players. Update DX version to DX12 to see if it will help mitigate Unity crashing |
| `5a37b6cd7` | Primitive versions of Captain Encounters, earned XP and earned Elemental Crystals awarded at end of Mission play. |
| `5d1095478` | made starting membrane invisible in block bandit. Made ClearPrisms not create colliders for autopilots. |
| `2ace3fe7e` | Show XP, crystals, and encountered captains on end game screen |
| `608f4ca7f` | updated to new LTS version 2022.3.44 |
| `3378aa4e5` | Fix build issue from firebase plugin from redundant .Net dlls. |
| `c4e14c160` | Resolve compiler warnings |
| `ffec5b12c` | Turn off dedpendency validation for Firebase so we don't generate errors when iOS build support is not added to a windows machine. |
| `bb0f84a4d` | serpent model - blendshapes |
| `1e2ac62cc` | Fixing console errors and warnings. Remove some overly verbose logging. |
| `eb0d8e0e8` | Don't return to previous game launch screen on fresh app launch. |
| `e5d9aebb4` | Serpent model test |
| `d6e0571f5` | Add model conversion services |
| `031728ab6` | Package upgrade and unity analytics changes |
| `510303d74` | Update PlayFab SDK and extension |
| `265820328` | Fix null references on the last two blocks |
| `567bfbcde` | Theme management prototype implemented into hangar. New pastel color set. transparency now works for all block types. renamed shielded -> IsShielded |
| `2993cdf15` | Some code improvement |
| `55ca8f743` | moved thngs to a theme manager. made a shadeer for changes to the rhino force field, removed some debug statements. |
| `395379b24` | MIsc UI cleanup. Ship unlock based on captain unlock. Work in progress on Training Game progression and rewards. |
| `0fa07f128` | Fix console errors in main menu |
| `e88491b7e` | Fix off by one error in training game progression unlock/claim logic. |
| `e96e9d67f` | Training game progression works except for final Arcade Game grant |
| `24ba12c92` | Rename DailyChallengeRewardButton to GameplayRewardButton and DailyChallengeReward to GameplayReward |
| `53cc64782` | Rename MiniGames to GameModes |
| `f772baca3` | Introduce Missions as a new game type. Create a MissionProtect scene as a duplicate of cellular brawl. |
| `42e7f4902` | Added the squirrel model to the squirrel prefab |
| `69e47dd89` | Added spawnable prefabs and tweaks for use in rampage training game and protect mission, created node control turn monitor, added background color colorset and theme/camera mangemnt, fixed rhino slowed ships bug |
| `d5b2d430b` | added scripts for cell modificaton with a new  one: extraOmniCrystals. added the creation of cell types with a brain cell. new nucleus prefab. pulled  serialized prefabs out of node  prfabs and into cell type SO. |
| `9802e21e9` | Applied all the Blue/Green/Red/Gold Material Set object colors to the Original Color Set SO object. There are still numerous bugs remaining, but all known bugs have been mentioned in the Sprint Review notion doc. |
| `d5adcc266` | Update android logcat |
| `46ae93acf` | Cleanup unused fields in HangarTrainingModal |
| `7146326ba` | Update vcontainer |
| `d02fb9198` | Hanger preview auto generated file? |
| `5e5241d58` | Added fully functional squirrel ship with basic animations, set up animation controller. Squirrel needs to be adjusted in prefab (size decreased/moved forward/something else. currently too close to camera or too big in-game) -Angelo |
| `7568ecfd1` | Get Android builds working again. |
| `e09a63484` | Overhaul to resource system. Moved from names (ammo, boost, energy) to a list using a new serializable resource class. |
| `93aa8603c` | Upgrade PlayFab SDK |
| `47d456c4f` | Update packages |
| `6a9062336` | Added Sparrow with animations and shapekeys. Cockpit root is too far forward, needs fixing. Among other things, such as hooking up new ship with firing, missile launching, etc etc. |
| `42ccbeea0` | more cell types and som placeholders to support them, dolphin and sparrow resource work, heat, added overheating action wrapper. single stick animation fix. |
| `dadc98aec` | Changes to game play settings to support pass and play for dolphin darts and block bandit. Nerf the NerveFlora spawn rate for performance on Mobile. |
| `20ac80c0e` | Quickly updated sparrow model blendshapes + now using MediumPoly Sparrow (about 10k) instead of HighPoly Sparrow (about 40k) |
| `77e144d48` | Add warning to ship actions and warning to invalid button number |
| `1a795cec2` | Update code on ship |
| `ae81f3dee` | color tweaks, sparrow and seprent resopurces and UI progress |
| `fc0d445c6` | Update gitignore |
| `90eb65867` | Update the Hangar a bit |
| `8e2e0d9c0` | rebuild sparrow and serpent UI after some merging errors |
| `e040f8140` | Adding ring/gradient texture for animating creatures and such (2 file pngs) |
| `056ae5a53` | Remove redundant braces |
| `f28b5076f` | Added clawfish + created animated texture for lifeforms + added png assets for use in shaders |
| `03b8583f7` | Slight pulse to creature shader/material |
| `3c59dbe82` | Some code improvement |
| `f55b30d2e` | Uploaded many noise textures for VFX/Background/Animation purposes. Created 12 shader graphs + materials for testing. Created test scene (angelo's). Uploaded a few FBXs for testing. Fixed the sparrow HUD (per garrett's instruction). -Angelo |
| `ab8d586ff` | Update Gyroid Assembler |
| `444d25cef` | Some Gyroid Assembler changes |
| `0e31693aa` | Fix a variety of UI bugs |
| `001c3f3fb` | Add makeshift colliders to Sparrow and Squirrel so they're playable |
| `5178825f5` | Update PlayFab SDK and editor extension |
| `240561d3f` | Adjusted scaling for Squirrel model to attempt to fix 100x scale issue on armature specifically |
| `1b1a6dcc9` | Turn off verbose logging on dolphin resource system. Updated script for Captain creation in Playfab and added remainder of captains. Minor bug fixes. |
| `7e882ed30` | Remove redundant lines |
| `b20a9101a` | Testing crystals with ig -Angelo |
| `6d2c64a2d` | Try to fix score tracker player score null reference |
| `1f20b04b1` | input controlller refactor, resource check on AOE, ship materials draw both sides, colliders on squirrel and sparrow |
| `47ba34435` | Small additions to testscene. Updated squirrel to fix scale issue, but still has rotation issue. Lesser of two evils, but still fix later. -Angelo |
| `0866681f3` | Upgrade linux tool chain package |
| `0b750fc19` | Update rider plugin |
| `57e80e6ee` | Added fully animated time crystal and prefab + added transparent ship materials (they make it look glassy, which looks cool. consider keeping crystals transparent) |
| `dacfaf39f` | -Fully added geonodes animation to Unity using alembic filetype. Recorded steps to reproduce as well. Also added the package so our Unity project can read alembics. |
| `5b32c4302` | Meta data cleanup - update Mason's profile pic. rename timebomb to fiest, add sparrow ability SOs. |
| `3122604f1` | Fix null refs in InputController when no gyro is available. Remove unused 'Action' property from SO_ShipAbility |
| `117fb16cb` | updated squirrel, added new animations and fixed the controller a bit. still needs shapekeys, but flies well now. |
| `511df4612` | Tidy up Hangar UI - tabs and infinite scroll. Normalize naming of Icons in SO objects. Update Ability SOs with new text and rename to match thier classes. Force reimport of scriptable objects and some other project files. |
| `10aa681ec` | Fix infinite scroll bug. |
| `aa5046d41` | add squirrel with shapekeys and animations. medium poly. |
| `7c7068989` | Fix to end game scoreboard not displaying. Minor refactor for readability and tidyness in MiniGame.cs |
| `ed058f1c5` | Daily challenge UI bug fixes |
| `9c19619df` | Preview Videos for Rhino abilities |
| `87628f646` | modular Input refactor, removed dolphin gyro controls, changes to turn monitor elimination and pool manager initialization |
| `58d04efd5` | merge |
| `f37a9133b` | -Added "animation rigging" package for procedural animation of flora/fauna -Added shark fauna with animations set up -Deleted huge cell background tests to save space |
| `64d48259a` | added shark animation with health. added the dynamic health blocks changed spindle script barely |
| `eccb9440c` | Precompile remove firebase code from WebGL. Bug fix in catalog. Addional null guards and some cleanup in score tracker. Cleanup in StatsManager. UI fix to daily challenge leaderboard. |
| `560195a45` | Added looped swimming anim for shark Added Maw open/close trigger for shark |
| `765d33e3e` | Started creation of brittlestar prefab |
| `a42d167f6` | Update cinemachine package |
| `9e69302c2` | Uploaded full brittlestar + procedural bones set up + health blocks parented (health block scripts currently disabled, enable when script is ready) |
| `ff8715437` | Updated AbilitySquirrelMachCone asset name from AbilitySquirrrelMachCone (typo with 3 r's in squirrel), added folders for videos on release ships, and copied some video preview prefabs for those ships |
| `f49e60f59` | Added more ability prefabs and folders for ship videos, no new videos yet |
| `c8fc1c89f` | shark progres |
| `62618b155` | Some fixess for the shark, simplified crystal respawn, fixed bug in clear prisms |
| `a37d9fe50` | Brittlestar progree, fixed input puppetry |
| `2c919a2bc` | -Slight changes to shark and brittlestar |
| `bb299e6d3` | Shark improvement, and updated lifeform spindle shader |
| `db597ce40` | tiny fix |
| `12dfe1147` | Updated serpent with new model + rig (no shapekeys yet) |
| `4f4655898` | quick serpent fix |
| `72aa2640d` | Fix null pointer in flora health blocks |
| `e5bb691fe` | Added serpent (mediumpoly) + shapekeys + animations |
| `acb9a9bd6` | Updated sparrow with shapekeys + mesh, fixed odd root (head) behavior. |
| `b1d41d669` | Added new time crystal. |
| `e0ee3438e` | Added all manta preview videos to preview prefabs |
| `850da4555` | fixes to spindles, team color change, moved transparency logic to trailblock, changes to fauna prefabs |
| `f31028cca` | sparrow fixes |
| `69de47017` | Added, removed, or updated ability videos and prefabs for both Manta and Rhino |
| `01c5e28ba` | Update instrumentation controllers |
| `a8c9222f7` | Remove unused background video from main menu hangar |
| `ed98b76f5` | Allow CosmicShore.Integrations.Firebase.Controller to exist in webgl build, but precompile away the class implementation |
| `24c6cd266` | Convert tooltip flyout to use new input system |
| `814bfc5a8` | Editor tool to find assets by their unity GUID |
| `c7399f249` | Fix DailyReward particle emitter balance update value |
| `63344e13c` | Convert main menu to use L/R Trigger to move between screens. Remove unused field from SO_Captain. Whitespace cleanup. |
| `478460e79` | wildlife blitz minigame, modified nodes to have vaariable floraa and fauna counts |
| `ea09036bd` | Refactor + Bug Fix: Transparency and block material lerping play nicely together. |
| `ab45d0103` | Testing trails and such |
| `dc2c8ea54` | fixed squirrel animations |
| `dbad52ef2` | fixed pitch in ship puppetry |
| `5d40e2b64` | Reworked simple trail |
| `311b07d45` | rotated brittlestar, fixed materrial bugs, modified cell components, retuned sparrow, fixed turning on 1 thumb controls, wildlife blits stub, made projectiles able to gain ammo on sparrow, made a redirect trail collision, |
| `aa3931b2d` | Toggle fullscreen in windows with 'esc' key. Unlock cursor on windows. |
| `1b12affc7` | Upgrade Firebase SDKs |
| `723dcfd56` | Remove warnings from the console output |
| `99b51916c` | refactored scoring to breeak Modes into classes, created all lifeeforms turn monitor and node stats, fixd stat tracking bugs, extended plant block sizes, sparrow tuning, crstal element set to omni, reset transparent materials |
| `3ff2f7f20` | merge |
| `8000bf6a5` | saved scene |
| `2401004bd` | score fix |
| `81252cedf` | Add some interfaces and data collectors |
| `8502981be` | Fix a null pointer in trailblock when ActiveMaterial hasn't been initialized yet |
| `1c1e1463a` | fixed single stick bugs, simplified resources, added resource for serpent boost, tuned serpent and sparrow |
| `9b35ae9db` | Add data collectors and rename analytics manager |
| `1372b624f` | added speedup animations +blendtree for sparrow added test trails on sparrow |
| `c1538a822` | progress on time crystal |
| `b8f839573` | Creating boost transitions + boolean parameter |
| `42573a8c3` | sparrow animation |
| `ba934e85d` | Speed of transition went from 1x to 0.25x |
| `615113706` | sparrow fix |
| `a10ab6619` | Update instrumentation a bit |
| `ffeb8dcff` | Fixed silhouettertss and trail displays, disabled touch, fixed squirrel pitch animations, |
| `867c1af81` | prisms darken after closing with increased distance. graph clean up. sparrow ui twaak |
| `57a4fda8a` | remove redundant component on analytics manager |
| `299a3747c` | Add PC dependency check for Firebase |
| `36472c27a` | Rough WIP protect mission - threats spawning |
| `bb371b6a1` | Updates on instrumentation |
| `2e9144997` | Dolphin Jaws opening |
| `112645597` | removed serializefield ship |
| `fde52df50` | Update Sparrow, Serpent, Squirrel UI images. |
| `821ab8731` | Hangar preview images for new ships |
| `e7a37fbbe` | wildlife blits cell changer, score tracking weights and removed individual golf scoring, progress on time crystal, sship bug fixes on squirrel explosion |
| `48afba2a1` | fixed bugs with blobs (cytoplasm) and dolphin animation |
| `e5d60cae3` | Continued work on mission. Get rid of extra cylinder in fauna branches. Prevent all spindles from having individual material instances. |
| `f1d956e54` | Brittlestar fixed. double cell fixed. gun barrel set to zero. intensity 1 enabled on wildlife blitz |
| `67a02087d` | post merge fix |
| `127f43066` | Update device and store data collector |
| `49c2dc89a` | finished elemental variance forr gyroids, added stun effect on plants |
| `9ceb28e6e` | Convert farCamDistance to Elemental Float |
| `d387a6c12` | tweaks |
| `abc9ee323` | Threats in the protect mission belong to the Ruby team. |
| `7659e9e58` | Sparrow: add Elemental Floats for ChargeBoostAction and FullAutoAction |
| `330ab91ac` | [To debug] Sparrow: add Elemental Float to AOEConicSkyBurst |
| `466aa0dc6` | [To debug] Sparrow: convert resourceGainRate to (multiple) Elemental Floats |
| `65f64a86b` | Added Manta Crystal HUD UI |
| `3158b8bd9` | Update instrumentations |
| `8bdc2ec8f` | new shieelding effect on AOE for serpent crystal explosion. toned down gyroid crystals, tweaked ships |
| `f7e1b5357` | ai aabilities |
| `8c3076ffc` | maze progress |
| `e92562323` | Revert '[To debug] Sparrow: convert resourceGainRate to (multiple) Elemental Floats' |
| `d348709cd` | Finished with instrumentation |
| `6e5b69d1c` | bug fixes, and crystal vacuum |
| `919ae2a64` | resource adjustments |
| `e7fe69989` | removing default on resource gain rate |
| `de5bfda07` | maze progress, sparrow tuning, added trails to dolphin and squirrel |
| `3a2e97c99` | Added HUD UI Assets: Sparrow Boost, Manta Crystals, Serpent Fuel |
| `506dde8bc` | added max time to fossil crystal, fixed trailspawner skimmer bug on singlestick ships, maze minigame wip, ship tunings, ship UI icons |
| `671365de0` | sparrow and serpent UI changes. overheating ends silence after penalty |
| `92c6ebbda` | Threats have teams. Hangar preview images for new classes. Better handling of end of mission. |
| `8deafa119` | Updated ship icons. Display selected ship's name in arcade. |
| `9c7f1357e` | San changes in the menu scene for ship names. |
| `38ce5454e` | Change Sparrow Charge from heat gain to heat loss |
| `59367abd5` | Fix Squirrel Time (ship speed) |
| `c9a3f7254` | Wire up Serpent Time (Supercharge speed) |
| `e89b7f14f` | Rework Sparrow Charge |
| `744340962` | Set parameter ranges |
| `7bd3c8681` | Update some keyword adjustments |
| `ab2f5c289` | Fix some obscure exceptions |
| `b30f5205f` | CHanges to many minigames and ships, new utility for clamping vectors used on blockimpact velocity, scoring mode for crystal sizes, tuned crystal vacuum, crystals have minimum impact velocity, minigames have proper intensity values |
| `e5aa41e9c` | Wildlife progress, retuned ships, add maturity and minimum health for lifeforms. tuned wildlife blitz cells |
| `a1cb8f006` | Wire up training games. Difficulty Slider for mission. Misson on main menu. Controller button hint images in App shell. Navigate arcade using controller dpad. |
| `d9a0daefa` | fixed build capability |
| `d0edbaa39` | Update SO_Captain_Dolphin_Charge.asset |
| `183ba5485` | Generalize CrystalTransform to TargetPosition |
| `264aa127a` | Change AI targeting |
| `1590011c9` | Style |
| `624c6c560` | Changed cacti branch radius and protect misison duration. |
| `3218a5f02` | protect mission only spawns fauna over a certain volume |
| `d921532db` | tunings on protect mission |
| `e6992af4b` | Fixed skim efects and ship transparency acrross game and in block bandit. |
| `ec72d1753` | reduced spread on danger block, sharks have teeth and attack players,skyburst rewok, projectiles dont affect crystals, serpent supershield seeds, public growth rate on blocks, fixed bugs on serpent boost, fixed some dying bugs, blitz displays fauna not time, removed charge crystal in protect mission, more curvy slip,n, stride; sparrow is now affectede by danger blocks |
| `cc79d901d` | fixed main menu bug from port squad button missing, minigame blitz not loading, fixed lifeforms modifying a block lisst while iterating over it. |
| `4d824c1cf` | squirrel and maze tweaks |
| `331f27cfe` | turn off strail stutter with low fps and fixed stack overflow on wallssembler |
| `8cd096b7b` | Bug fixes |
| `cad81321b` | Port view is a little more intuitive |
| `db5832417` | Squad view on home. Don't show mission info on other game modes. Better reward reqs for sparrow training game. More controller navigation. Misc bug fixes |
| `b010cef9e` | Balanced gyroids to be more difficult. Extended mission time to 5 minutes. Increased block size of nerve flora blocks |
| `d19701f98` | Lowered the difficulty of space and time gyroids |
| `670304ac9` | twaks to wildlife |
| `910dcfcb1` | wildlife tweaks |
| `06cb7414d` | Removed fauna from wildlife blitz 4. Buffed charge gyrouds. |
| `91a7a3700` | IAnalyzable InitSDK() is now async. Fixes menu navigation in game build. |
| `d920aad66` | Fix squad configuration UI bugs. |
| `be49fe2c7` | Disable ads on unsupported platforms |
| `7decd86e3` | subgrph exploration, stub for quadfish population, squirrel nerf |
| `4573f9758` | Fix exceptions in lava lamp. Hide menu until network initialization. Update build version to 0.2.0 |
| `4db74a63b` | Refine NPC pilot targeting logic |
| `8f6c59998` | Updated with Will's videos. Initizing screen doesn't show on return to main menu. |
| `bc1815e55` | Adjusted charge and mass gyroid balancing |
| `eb9debfd2` | skimmr performance refactor magnitude -> sqrMagnitude, squirrrel bugs. |
| `1fd13c7ad` | color tweaks, fixeed volume calculation bug, tuned space gyroids, added stub for charge lifeforms to shield blocks, removed charge flora/fauna in prep for party |
| `197925180` | new system to save off positioning scheme solutions and reuse them as a new positioning scheme. Made maze runner not crash on load |
| `1c6b9276c` | maze tweaks |
| `35033b5b4` | temporary fix for volume discrepency, breaks multinode |
| `b4012f396` | tuned rampage scoring, increased manta skim radius, flipped the direction of spawned blocks, tweaked colors, adjusted threats |
| `8562dfa07` | Update SO_Class_Dolphin.asset |
| `5425a6847` | Update SO_Class_Manta.asset |
| `b1fb6b64c` | Update SO_Class_Dolphin.asset |
| `3446fd417` | Update SO_Class_Rhino.asset |
| `bdc153861` | Update SO_Class_Squirrel.asset |
| `aeb05d158` | Update SO_Class_Serpent.asset |
| `4c54cb2c4` | Update SO_Class_Sparrow.asset |
| `9601c04a8` | Update SO_Captain_Manta_Mass.asset |
| `4ee41eb40` | Update SO_Captain_Dolphin_Mass.asset |
| `619a5be9c` | Update SO_Captain_Dolphin_Space.asset |
| `98f60cdf7` | Update SO_Captain_Dolphin_Time.asset |
| `b9effa655` | Update SO_Captain_Manta_Charge.asset |
| `fb6e3fe3b` | Update SO_Captain_Manta_Mass.asset |
| `46545a6d3` | Update SO_Captain_Manta_Space.asset |
| `a4c56ff39` | Update SO_Captain_Manta_Time.asset |
| `6d9425f0e` | Update SO_Captain_Rhino_Charge.asset |
| `38b1c11ba` | Update SO_Captain_Rhino_Mass.asset |
| `9fe5d4889` | Update SO_Captain_Rhino_Space.asset |
| `47b2ee3ef` | Update SO_Captain_Rhino_Time.asset |
| `7a00696fd` | Update SO_Captain_Squirrel_Charge.asset |
| `264ef1820` | Update SO_Captain_Squirrel_Mass.asset |
| `b3def943a` | Update SO_Captain_Squirrel_Space.asset |
| `a1d0df6ad` | Update SO_Captain_Squirrel_Time.asset |
| `752804cb3` | Update SO_Captain_Serpent_Charge.asset |
| `d66d36d55` | Update SO_Captain_Serpent_Mass.asset |
| `cc9f5255a` | Update SO_Captain_Serpent_Space.asset |
| `dfdda24dd` | Update SO_Captain_Serpent_Time.asset |
| `330acf2e4` | Update SO_Captain_Sparrow_Charge.asset |
| `80c0db8a0` | Update SO_Captain_Sparrow_Mass.asset |
| `a8b4bba12` | Update SO_Captain_Sparrow_Space.asset |
| `c79dfb31a` | Update SO_Captain_Sparrow_Time.asset |
| `a1577c610` | Update ArcadeGameDarts.asset |
| `4809f11f0` | Update ArcadeGameRiskyDriftness.asset |
| `f8f43218a` | Update ArcadeGameSlipNStride.asset |
| `58e770479` | Update ArcadeGameMasterExploder.asset |
| `752809455` | NPC logic: Make Sparrow and Rhino more likely to target the crystal |
| `9af51535a` | fixes |
| `5388a859b` | Change ship classes in main menu scene |
| `0f728d276` | Fix malformed captain SOs. "Correct" the "Inititizing" "typo". |
| `42b78900c` | Mission difficulty is wired up. Initializing text fix. |
| `a04518e50` | Clamp score to zero as a minimum. Update arcade game list. Show DEFEAT text on game end scoreboard. |
| `15dcea4c8` | tunings |
| `3a5b0942a` | Add frog splash to app startup. Fix initial ship selection bug. |
| `4784a132d` | Songs are streaming to reduce memory footprint |
| `8f2b69a8d` | Node organization for Block and ExplodingBlock Graphs. |
| `975ef6773` | FInished removing sqr roots frrom explosions |
| `2a0b4c75f` | introduced super stealing to fix the walls not stealing shielded blocks, continued simplifying blocks and explosions |
| `d5e06fd6f` | creating stubs for next updates |
| `37b754c68` | Basic perf improvements. |
| `d7b879dd1` | Organize exploding block graph. Improvements to initialization sequence |
| `42f816eeb` | Massive chanes to trailblocks |
| `2b58ed2e8` | merge |
| `3a3d746df` | fressnel power 4 implemented |
| `156333147` | Trail block changes: Material handling, materials, and graph changes |
| `67107ba02` | straggler from last push |
| `ad9c4cbed` | Moved leaf size from branching flora -> flora. Continued performance refactor of trail block animations. |
| `9d540a304` | Upgrade project to Unity 6 |
| `3e88e447e` | performance tuning and list clean up |
| `7582375b4` | updated Unity |
| `930e2e839` | marker rework on squirrel shards and explosions to use object pools |
| `a68998123` | Add Multiplayer Services and Netcode For GameObjects, Multiplay |
| `dfc955ae1` | Add Lobby |
| `806c0fa3c` | Settings change in project settings |
| `26a192273` | Add ITransform interface |
| `eaa147e07` | Add IShip interface |
| `b52f1299c` | Create Folder for player scripts |
| `694d6183b` | Add IPlayer interface |
| `64d9860f9` | Remove Lobby Package - Deprecated in Unity 6 |
| `851db3559` | Fixed issue with exploding block inheriting from blockgraph. added materials to explosion object pool on a persistent pool manager. |
| `32532f492` | Update Player.cs |
| `6c1f988cb` | Add IInputStatus and InputStatus |
| `a3b83ecc5` | Update Hangar.cs |
| `987d0892c` | Update IShip.cs |
| `dac764fb2` | Update WarpFieldController.cs |
| `6fb3b6850` | Update BoidSimulationController.cs |
| `8ac22134d` | Update VCamRecorderController.cs |
| `ef5b37e41` | Update CameraManager.cs |
| `065b3eeaa` | Update ShipTransformer.cs |
| `f3dc87007` | Update GunShipTransformer.cs |
| `f2e08d391` | Update BlockTeamManager.cs |
| `094d61ea4` | Update TrailBlock.cs |
| `32363e02f` | Revert "Update ShipTransformer.cs" |
| `85bf38f07` | Update ShipTransformer.cs |
| `c0984dd95` | Refactor with IPlayer and IShip and use it instead of Ship class |
| `2ddde502d` | Add Multiplayer Related scripts |
| `c9f8bea7e` | Configure MainMenu for Multiplayer |
| `8c834bc37` | Add Multiplayer Freestyle Game Mode in Arcade UI |
| `cf2e32283` | Add Network Related Scripts |
| `550f85979` | Create Room creation scene and gameplay scene |
| `c3a4627f6` | scene save with pesistent |
| `96b58ac6b` | Fix TrailBlockManager component |
| `8fea0bc05` | Update UnityPlayerAccountSettings.asset |
| `65f270066` | Don't check if it's ship geometry on every trigger enter of a trailblock |
| `bd4c0afd9` | Ships are back in the main menu. Fix console errors. |
| `aa4eaa96c` | AOE block creation moved to a block buffer |
| `d99db55a5` | Update Player.prefab |
| `10466d3df` | In Progress: To Make:  Mini Games to work |
| `35ae0395a` | [Error] Ship Animation's Ship reference becoming null |
| `eb73f18d2` | [Error persists] Refactor Ship -> IShip , Reference replaced in other scripts |
| `4159bd6e0` | Fix BlockBandit Minigame |
| `c54a5183f` | Fix Ship Animation and Controls |
| `ed4079d75` | Fix Dolphin Darts mini game |
| `76a3c5217` | Update MinigameFreestyle.unity |
| `bb46ddf59` | Fix more minigames |
| `ad715918a` | fixed missing particle effect error on danger blocks |
| `c37a26ead` | fixed manta trail |
| `9a2627fe2` | Added Network Manta Prefab |
| `6998b9a36` | Refactor GetShipType -> ShipType |
| `225898f78` | Update ShipTypes.cs |
| `9c0ad2e24` | Update DefaultNetworkPrefabs.asset |
| `e90ef133b` | Update Netcode For GameObject Package |
| `06a4491ce` | Configure MiniGameFreestyleMultiplayer_Gameplay for multiplayer |
| `f33d70b9f` | Removed NetworkPlayerService (Not needed) |
| `bf87a85c3` | Create NetworkPlayer Prefab |
| `35ee333b7` | Update EditorBuildSettings.asset |
| `141c64b14` | Added Parrelsync |
| `b5ea2af0a` | reduced interactions between aliased block edges |
| `bfb17a0fb` | clean up |
| `8307db106` | fixed missing particle effect error on danger blocks |
| `63bc1adbc` | fixed manta trail |
| `054135108` | reduced interactions between aliased block edges |
| `a16a9afd2` | clean up |
| `7a1ffff89` | fixed dolphin trail |
| `4f8f62c97` | manta uses trigger to nudge left and right |
| `c423826aa` | nudge shard, squirrel shard, trail disable action for manta, changes to how velocity nudges affect drifting |
| `37d686637` | Configured and Tested Multiplayer - Host |
| `518d73fce` | squirrel and gyroid tweaks. |
| `b42bff9ff` | Squirrel  tube refactor |
| `de45cc2eb` | squirrel crystal power change |
| `a00faeade` | Reintroduce players in main menu via PlayerLoader.cs. Work needed to generalize with Hanger.cs and Minigame.cs |
| `408fe0461` | hot fix for console red |
| `b18167528` | Added Manual Throttle in Ships |
| `1fedaa266` | squirrel and material tweaks: toned down spread. grouped shards, only steal when hitting shard. scaled alignment to speed |
| `9a9d5952a` | Added Serialize Interface |
| `3fcdfd55e` | tuned freestyle and squirrel |
| `8cdb76e74` | Bug Fixed: Main Menu Scene Null Reference of IShip |
| `2b1956897` | IShip reference removed from IImpactEffect |
| `6017600b6` | Refactor InputController |
| `3d3cb8158` | Removed IShip reference from AIGunner |
| `30854d552` | Get project building again |
| `7e55d3d2d` | Mission allows player to pilot their ship again |
| `7a018a225` | Fix null pointers on guns. Fix crystal flickering when it teleports. |
| `32d6c2c6d` | Refactoring with IShip and IShipStatus |
| `69531392a` | Create Ship Selection Scene and add Network Rhino |
| `81ebd45e2` | fix sparrow camera |
| `013bffaa3` | Restore Player Colors in Main Menu. Update AIPlayer Prefab. Remove vestigual code and objects from Hangar.cs and MainMenu.scene |
| `e12251732` | Cellular Duel is playable again. Normalize scene structure of Arcade Game Scenes |
| `69b25fc61` | Fix profile icon display and selection. Fix MainMenu warnings. Update RenderPipeline to no longer use compatability mode. |
| `6caa59b4f` | TimeBasedTurnMonitor games pulse the timer when nearing the end of the round |
| `af2fb67c7` | HUD Position Indicator |
| `3821bfa3e` | Protect Mission works again. NodeControlTurnMonitors have a delay and warning before the turn ends. NodeControlTurnMonitors now extend from TimeBasedTurnMonitors. |
| `9cecfb9ff` | Create CharacterSelect Scene for Multiplayer |
| `41e631929` | working bigger blocks |
| `fa2959a3b` | hud tweaks |
| `60ebb4b01` | Zero out the transform on some of the population prefabs |
| `16c3aa3f4` | Integrate Character Select with Gameplay Scene in Multiplayer |
| `cb02e23dc` | /.utmp added to gitignore; fixed bugs with sparrow and serpent UI, fixed touch controls, tamed hud indicator, |
| `89630cf83` | Reduce object pool sizes. Remove missing or unused prefabs from select scenes. |
| `f66745710` | Add logging to poolManagerBase |
| `824f8b62d` | Add Yash's Serializable Interface solution. |
| `5664d64b7` | Create SceneBootstrapper to load main menu scene automatically while testing |
| `41a51e3b1` | Character Select FIrst Build Test |
| `3bce466cd` | Blame Garrett: sparrow icon update |
| `47ca994a4` | Reduce return to main menu time from over 11s to under 2 seconds Reduce initial load time |
| `94be9c31d` | Minor cleanup |
| `0f14854aa` | Big in game pause menu improvements. Music/SFX/Haptics options now work. In game Pause Menu and Main Menu Settings Menu implementations are shared. Fix the bug when opening the pause menu during the countdown sequence. Prefab overrides are applied. |
| `ac3fa3af0` | new distance effect on block shader |
| `84727ca6f` | tweaks to hud indicator, squirrel haptics, rhino maneuvering |
| `071d411d9` | App launch improvements: Performance improvements Remove Haptic buzz on scene load Custom splash screen with scene preloading |
| `e053c1729` | Distribute some of the heavier app startup load across splash and main menu |
| `b634736c0` | Fix explosions. |
| `2d6264406` | Fixed WildLifeBlitz timer. Adjusted play time to 90 seconds. |
| `f91acc237` | Squirrel doesn't draft it's own trail |
| `3f2dd6321` | pushed back distance for color dimming |
| `fc5bbae95` | Add some sounds. Roll back app launch perf to fix explosions. |
| `b68a44422` | [T] Add Team Select and sync in character select screen |
| `279339c0b` | Revert "Merge From master Branch to Unity-6_Ys" |
| `aa7f996f3` | Add Network Sparrow |
| `9ee548e96` | Network Ships Added and Modified |
| `76673a9b0` | New splash screen |
| `6b44249f4` | Ships load in main menu and some ships are playable again |
| `5cb6dff85` | Resolve several P1 TODOs. Delete unused classes. |
| `d62024427` | Cleanup. Resolve TODOs. Fix console warnings. |
| `e366630a8` | jet |
| `f877dd5e8` | Fix corrupted meta files. |
| `808417f92` | Fix some of the broken ship actions. Mute a bunch of console warnings. |
| `19a6722b3` | Dolphin blasts are back baby! |
| `cc1ba171b` | Change team colors in main menu |
| `4d11455f0` | Multiplayer Package Upgrades |
| `8f7dc054c` | Create Base scripts + Add Demo UI + Create Initial Flow |
| `b9be6a189` | Squirrel boosters changes: now spaced out chevrons |
| `b40fa743a` | Squirrel updates to boost chevrons |
| `9eeade16f` | material animations coupled to ship movements like boosting on squirrel. changed engines to fire color. fixed some squirrel bugs, |
| `e2e884d45` | making squirrel opaque |
| `527ea0a5a` | Tutorial Demo Updated |
| `8e0d95765` | Tutorial Cleaned Up |
| `f75e75100` | Introduced Phase to FTUE |
| `86af9d45b` | Add Quick Scene Pro + Refactor Scripts + Folder refactor + Add Events |
| `724dc8f7c` | Implement Quick Join mode with new Multiplayer SDK and MdalWindow |
| `48b474632` | Integrate Ship Choice from Modal Windows in Multiplayer Freestyle |
| `e9f24c663` | Auto Assign Teams for players in Multiplayer Freestyle |
| `fbea43a1c` | updated squirrel with 5 rings on crystal hit and player overtake |
| `51ae2daf2` | Console Error Fixes. |
| `8e5393c17` | got rid of unnecessary agrgression |
| `0cf78c65b` | Add Duel for the Cell to Arcade Screens |
| `88ce7b2d1` | Set AI Type |
| `2cbe52f35` | Add Online Duel Cell Arcade game mode with Game Card and Gameplay Scene |
| `7797ef0b0` | Update MinigameDuelForCellMultiplayer_Gameplay.unity |
| `e5c9ee2a5` | fixed network squirrel |
| `841ac49e4` | Untoggled move button |
| `140da99ef` | Made spawnable AOE team functional, and updated squirrel |
| `eb69630eb` | squirrel changes (third time?) |
| `a8e70223a` | overtake now uses 5 rings |
| `ba9d91ce5` | Add EventChannel Systems and integrate in StateManager and TrailBlocks |
| `0a83f6195` | less double blocks on AOE explosions |
| `8ad970711` | Five rings scale with energy |
| `0ae114d43` | Refactor -> StatsManager |
| `fcfab62f1` | Update NetworkShip.cs |
| `145ccb6bf` | Update TrailBlock.cs |
| `8a3f8b962` | Update Menu_Main.unity |
| `8883b9219` | Update TrailBlock.cs |
| `673c66fd3` | Update NetworkPlayer, IPlayer and Player |
| `82c140068` | Overview Modal Screen |
| `b2fa4228f` | five rings block count scales with energy |
| `1ce7ee35c` | Add  NetworkRoundStats, IRoundStats, RoundStats |
| `86cdbdaf9` | Clean up old multiplayer scripts |
| `5449ea7fd` | Modify DebugLogExtensions |
| `760b9dfd5` | Create Dialogue System Edtior Tool |
| `08879698e` | Visual Change made to editor tool |
| `d54174ea2` | Sync Player Stats among all clients |
| `7e0941377` | Add DOTS Packages |
| `a42266795` | Update Node.cs |
| `d20c406fe` | Refactor to separate IPlayer from GameCanvas |
| `cdada31c6` | Camera fix for sparrrow by making camera manager support a vector3 offset from a only scalar solution |
| `bc4e17452` | Update MainMenuDependencyLoader.prefab |
| `21eeef20b` | Fix Pause System |
| `1b27d15bd` | Fix Block Spawning by removing IPlayer reference from block mechanics |
| `8c0a01170` | made prefab variants of dolphin and manta |
| `27a883191` | follow up for new prefab variants |
| `e659b8fa9` | Pause menu refix (validated) |
| `668e5d35c` | Animation change |
| `875425b4c` | Fix editor changes |
| `9e802dd3e` | projectile progress |
| `da20fa78a` | Fix Sparrow Projectile |
| `f9eab9d13` | sparrow fire moved to trigger |
| `499df0f51` | Revert "sparrow fire moved to trigger" |
| `3de1b93ca` | switched shooting to trigger on sparrow |
| `cf1a5893b` | tool upgrade to GUID finder with file id finder |
| `6b9b21112` | push menu change trying to fix unstable main menu scene |
| `1a744bcfa` | scene sanitized camera manager |
| `49d3aeeec` | Add Copy Tool to Master |
| `0ec85565f` | Built remaining vessel prefabs |
| `3ba36e616` | Sparrow skyburst restoration progress |
| `10a3da0fa` | Dialogue System Complete |
| `1c976d365` | Bug Fix: |
| `269e93140` | Create Shader + Updated Menu Scene |
| `47a51d77d` | Sparrow stop ability WIP |
| `50c64eb41` | Dewormed freestyle minigame |
| `0213e9c8c` | Add Reference of TrailBlock Event Channels inside trail block prefabs |
| `0c07187f2` | Move and Rename NetworkPrefabs asset |
| `bc864067a` | Move EventChannels Folder inside SO_Assets |
| `2a0f43522` | Remove Unwanted Scripts |
| `0fbf46f2e` | Create TrailBlockEventChannelWithReturn EventChannels |
| `bb275e783` | Create ThemeManager Data Container |
| `a123259a6` | Serpent changes: updates to stop ability |
| `94b867842` | Refactor Scripts |
| `ce77d7a08` | Add Reference of ThemeManagerData and others to Prefabs |
| `c6b145372` | Cleanup Main_Menu |
| `ca85c1f8b` | Add DependencySpawner prefab |
| `5f3b7c907` | Add DependencySpawner to MinigameFreestyle |
| `7bc98de3a` | Add Jade team support to team color pool |
| `ce987015d` | Add Reference of ThemeManagerData inside prefabs |
| `bf988efc7` | Replace StatsManager with DependencySpawner in CellularDuel |
| `a1776f83d` | Fix missing ThemeManagerData reference in Crystal prefab |
| `b49b471ee` | ShipHUD changes |
| `97099de9d` | Update EntitiesClientSettings.asset |
| `0167a20f2` | Update DependencySpawner.cs.meta |
| `694e26c34` | Update TeamColorPersistentPool.cs |
| `d4772dd75` | Update MinigameFreestyle.unity |
| `a2f67c8d5` | Update MinigameCellularDuel.unity |
| `97179a8ea` | Update Menu_Main.unity |
| `887c2199e` | Update ExplodableProjectile.prefab |
| `99b0213b7` | Update AOEExplosion.cs |
| `83718025e` | Update PoolManagerBase.cs |
| `73a139965` | Add team-specific FossilPrism variants and update pool manager |
| `99f2db2c6` | Add ThemeManagerData references to more projectile prefabs |
| `39bcaa731` | Minigame HUD Refactored and Modularized + Fix Bugs in ShipHUD |
| `02e9d95f5` | Fix Sparrow SkyburstProjectile spawn issue |
| `99199f0c0` | Fix Team Data of Blocks when exploding |
| `ba8af3369` | Fix Sparrow Gun points and refactor |
| `c8101c86d` | Fix missing daily reward pref key |
| `609994f30` | fix: correct length spelling in singleton logs |
| `b16388d0b` | fix: destroy child objects instead of transforms |
| `a6f94b94c` | sparrow projectile tweaks |
| `25a034c2f` | Update README for cross platform |
| `20cce7357` | Add SpawnableSingleTrailBlock |
| `0b51c4782` | sparrow progress |
| `ad7187d0b` | Fix Resources References + HUD finalized for ships + Serpent UI activated |
| `8c22e8fd8` | Organize AnimationController assets |
| `00de12ddd` | Revert edit-mode guards from non-utility scripts |
| `5b3516587` | Add NetworkShip component to Network Manta prefab |
| `3c911a886` | Fix NetworkManta |
| `406a9e66e` | Fix NetworkPrefab asset |
| `1b7afacc0` | Update Network Dolphin.prefab |
| `1f61a0045` | Add R_Ship and R_NetworkShip classes |
| `1ab360a60` | Remove duplicate action mapping structs |
| `3f76cec42` | Add Meta Files of new scripts |
| `42c7292e6` | Extract ship input logic |
| `bbe7e1d9c` | Clean up ship refactor |
| `232eb6e86` | Refactor Ship Architecture |
| `ce6dd2422` | Update  DependencySpawner.prefab |
| `1759ab2f0` | Update GenericEventChannelSO.cs |
| `71cf4634b` | Add New Serpent + Refactor |
| `a0f4c8f68` | Modify ShipHUDController to integrate with IShip and others |
| `a49ac30da` | Add Dolphin New prefab variant |
| `911c6701c` | Adjust Dolphin New prefab |
| `ea708f49b` | Add NetcodeHooks and client cache to new ship prefabs |
| `d804f5e07` | Add new network-ready ship prefab variants |
| `e60a1fcb4` | Update Ship Prefab Reference holding Prefabs |
| `97121b6be` | Add all new ships prefabs with new scripts |
| `876f1fed4` | Refactoring IShipHUDController and IShipHUDView |
| `be73a15e9` | Update Ship Prefabs |
| `6cc9b46a9` | Create All Impact Type Class SOs |
| `23c42b726` | Create default EffectSO assets |
| `96faa6711` | Fix Serpent HUD |
| `9efa02a4b` | Refactor IShipStatus.AutoPilotEnabled |
| `1688018d8` | Add ScriptableObjects for CrystalImpact effects |
| `f6b9b1d89` | Refactor Impact Effect logics of ShipImpactHandler |
| `b16827ebd` | Refactor IShipStatus and R_ShipController to separate out R_ShipElementalStatsHandler |
| `6d4050471` | Refactor Impact Effects logic inside Crystals.cs |
| `998ecdca8` | Sparrow UI Fix |
| `179374dd4` | Add custom camera controller and integrate into camera manager |
| `d99ee653b` | refine camera controller and runtime attachment |
| `f545be5d9` | Allow camera offset without roll |
| `3bb8ef3bd` | Refactor Impact Effect Mechanics |
| `5e5855b0c` | Camera Configuration |
| `010fa5460` | Fix Bug Report : Ship Initialization, Skimmer Refactor for Impact Effect |
| `56d15947f` | Camera Bug fixes |
| `678b6702e` | Add  Vessel Prefab References |
| `4be22c242` | Refactor and Rename |
| `64283324d` | Add SOAP Package |
| `a925a9be7` | Replace PipEventChannel with SOAP_Version |
| `a40daa999` | Add Icons + Add Sparrow UI |
| `1b2ae02b3` | Refactor to fix AI |
| `fd65de215` | Add Dolphin UI Icons + Edit Ship HUD View |
| `f5401f532` | Improve camera follow and startup |
| `b648a5c18` | Make camera follow ship roll and extend far clip |
| `d09516eed` | Update Player V Cam |
| `90c13870e` | Gamepad Debugger Add |
| `5b9219345` | Refactor to Fix AI |
| `3804200bd` | Restrict Reference of InputController and replace with IInputStatus |
| `591b71c01` | Refactor Impacts Explosions and Ship Prefabs |
| `5f6107f8e` | Fix UI bugs + Sparrow Camera setup |
| `f260b447d` | Adjust camera offset handling |
| `c9d1a11c3` | Finalize Sparrow Camera |
| `94575af22` | UI Updates |
| `cb2d73d87` | Added charge and space crystal - Angelo |
| `d42f17259` | AI Pilot of Manta Working |
| `cf7017acd` | Add Vessel Collider to remove Rigidbody dependency |
| `8b8ad43a5` | Fix Merge Conflict from FTUE branch |
| `d9164baba` | Initialize LifeForms from Cell |
| `c0fbcd317` | Refactor Player and CellControlManager |
| `867879eaa` | Update Manta.prefab |
| `6dbc733a9` | Update Rhino.prefab |
| `f4275f9f1` | Update Sparrow.prefab |
| `10d05eb0f` | Update Squirrel.prefab |
| `3194fbd2a` | Refactor Snow Changer with Cell, Cell Item |
| `cb812f5ec` | Update Menu_Main.unity |
| `9ac7ed498` | Refactor and Fix All Ships to run AI Mode in Main Menu |
| `9dafd9f12` | Refactor Effects and ShipHelper |
| `0fc1866dc` | Add ScriptableSelectedShipVariable |
| `e63e1c683` | Refactor and upgrade Player Spawn System |
| `e0ff4300f` | Added option to disable Diagonal lerp |
| `2b32bb297` | Refactor camera system to use ScriptableObject settings |
| `4b6448aec` | Refactor InputController and AIPilot |
| `7c88f165c` | ShipTest Scene created. [Camera of Player Not Following Ship] |
| `c5ed0ee55` | Change Singleton Persistent to Singleton |
| `ce99dad66` | Added SO feature for all ship camera types |
| `e4a7a9d59` | Add ship-specific camera settings and update controller |
| `7955a745d` | Configuration set up |
| `d2319dff3` | Refactor Core System with SOAP and Freestyle Test [Camera not following player] |
| `45caaba42` | Refactoring scripts |
| `545636817` | Refactoring fixes |
| `059e6defc` | Update Cinemachine VCams |
| `21c32595e` | Fix Merge Conflict with FTUE Child |
| `9d7cb94a0` | Update Prefab reference and Camera Manager Setup |
| `c1010b1b3` | Update MiniGameData.cs |
| `3b1474c9c` | Fix Sparrow FullAutoAction |
| `095051414` | Removed floatiness from camera |
| `aafe17cea` | camera tweaks |
| `51dc2a119` | new crystal shader |
| `afa8a6ab5` | Simplified CameraSettingsSO + Fixed Rhino Bug + Made dynamic camera more subtle [only playable in manta] |
| `b76864a94` | Reset MiniGameData on return to main menu |
| `ab08aa99e` | Add Tooltip to IPlayer and PlayerSpawner |
| `a821b1b17` | Updating the timecrystal - BlendShapeAnim2 is the current quick fix. (Looks good) |
| `cb117137a` | Cleaned Follow Target and Fixed Offset Position |
| `37b953c1e` | Angelo's changes |
| `861db7981` | camera tweaks and dolphin tail fix |
| `89e384bd8` | Added Zoom effect in rhino + Added Wrapper Class |
| `34b781012` | Made changes to rhino camera + added adaptive distance in SO |
| `282175dc3` | Rhino camera movement along with Skimmer |
| `e2ed7e971` | Camera Refactoring [MINOR] |
| `31a219a12` | Dolphin Team Crystal Ability [Need Feedback] |
| `459c4dbde` | Tuned Team Crystal Values + Changed Action Button for Crystal |
| `69263cd75` | Added cooldown |
| `a1e1abbf0` | Refactored Ship HUD + Fixed Dolphin Boost UI + Fixed Dolphin Boost |
| `a215f75c0` | Add New Scripts for Impact Effects |
| `3dee9d8b8` | Refactor ImpactEffectSOs and other classes based on New Impact Effect Archtiecture |
| `9741c4bb7` | Fix Explode Prism Effect |
| `9850750e7` | Manta and Serpent Confiuguration |
| `f83f7972a` | Angelo's Changes FBX meta files |
| `3b7a6d308` | Update MantaCameraSettingsSO.asset |
| `aa36e7cea` | Fix Prism Impact Effects for Ships |
| `a6be9b8dc` | Fix All prism impacts |
| `c5b2ac77f` | Refactor Impact Effects with Generic Base Impact Effect Class - 1 |
| `ab4ae2534` | Refactor Impact Effects with Generic Base Impact Effect Class - 2 |
| `14f250a99` | Refactor Impact Effects with Generic Base Impact Effect Class - 3 |
| `f9c9548b3` | Add Impact Effect components to Crystal |
| `c459abe66` | Remove Deprecated Prefabs and Scripts from Vessels |
| `695af8adc` | Remove Deprecated Prefabs and Scripts from Player |
| `059a78fed` | Refactor CrystalImpactor |
| `ca95da241` | Fix Skimmer and Crystal Impact Effect |
| `bdbff7e03` | Created manta decoy and overcharge effect + dolphin's shard effect |
| `dfd0e84db` | Remove extra components from Omni Crystal in Main Menu Scene |
| `4d7939ffb` | Fix Sparrow Projectile Impact Effect |
| `4a4de2216` | Fix FullAutoProjectile and SkyburstProjectile of Sparrow |
| `2b3fc821e` | Fix LightFauna Initialize |
| `3635b8a52` | Fix AOE Radial Blocks Spawn Points |
| `7e3f8fcad` | Separate Crystal Explosion to effect SO from Crystal + Fix Ship Decoy |
| `bc2f1024d` | Update SerpentCameraSettingsSO.asset |
| `a7b32330b` | Replace Old OnBottomEdgeButtonsEnabled event channel with SOAP |
| `134a6c3ce` | Added Shard Action for Dolphin |
| `8a0934729` | Restructure Effects inside Ship Effects folder |
| `f801964ca` | Fix Shard on dolphin action + crystal respawn after fake crystal |
| `f9196b2eb` | Fix Overcharge Skimmer Action +  Dolphin Need UI Fix |
| `6cd220a21` | Fix AOE Explosions |
| `73f7e7ec6` | Replace Prism Event Channels with SOAP |
| `16a3780b5` | Add Rhino Arch Burst Effect [FEEDBACK REQUIRED] |
| `e267c4885` | Replace All Event Channels with SOAP |
| `88ba09522` | Refactor Impact Effects |
| `d50a6720e` | Add Submenus for Impact Effects SO |
| `5e1edd580` | Shards Point to Mass Centroids |
| `e8c5e6dff` | Fix Impact Effects with Garret |
| `9161e26bd` | Restore MiniGameCellularDuel [Camera of Player need to be fixed] |
| `bcb993a8f` | Fix Camera Follow |
| `4500bd949` | Refactor ship hud + create dolphin hud |
| `8b02ca5c8` | Fix Sparrow HUD |
| `f2aa27b5b` | Refactor CameraManager and GameManager |
| `c0fffdc0e` | Add event on Sparrow Ship Camera Customizer |
| `9cfe7bfae` | Fix Sky Burst Missile + Refactor Resource System |
| `df2c254bc` | Refactor folders + Create Manta overcharge block counter text |
| `a26483e34` | Clean Project  Files for Ship HUD+ Add AOE Explosion when Dolphin impacts crystal |
| `b11276d05` | Add Mine Prefab + Add Mine Impactor and Mine class |
| `0ebf4c383` | Imported new mass and omni crystal. 8-21-25 |
| `569c72929` | Fix Ship Action Bugs + Add Mine references + Remove Fake Crystal Scripts + Add SceneName List SO to Vessels |
| `7a03a6a6b` | Serpent Cloak Action + Serpent Stop Action |
| `f29ffe5b0` | Refactor Turn System, Score System, StatsManager |
| `b3c873b89` | Completed cloak and seed feature of Serpent |
| `68d722498` | Changes for Linux build. |
| `8362fcdaa` | Serpent Cloak Action System + Serpent Stop system |
| `aec09ea2a` | Remove IImpactEffect |
| `4c01b4c14` | Connect Stats System - Score System and Turn System with GameDataSO |
| `99d1fd21c` | Ghost ship follows actual ship rotation |
| `0157c4814` | Refactor StatsManager with EventChannels of SOAP |
| `58b7cded8` | Add EventChannels SOAP to all prism prefabs |
| `386381eda` | Refactor PlayerSpawnerAdapter system and MiniGameData |
| `0b75ce736` | Fix UI for Manta +Sparrow + Serpent and Dolphin |
| `070d268f7` | Update Ship Prefabs with event channels |
| `308df930c` | Refactor SkimmerPrismStay effects and assign missing event channels to lifeforms |
| `ac830987c` | Modify Turn Monitors and fix TimerUI display for TimeBasedTurnMonitor |
| `e7870403e` | Fix Duel For Cell Singleplayer Game End Conditions and Scoreboard |
| `2408a07da` | Modify Ship Prefab Container and Default Network Prefabs SO Assets |
| `709ebb0ff` | Delete MainMenuGameData assets |
| `f657f3ccf` | Refactor Arcade |
| `7618897ba` | Update MinigameFreestyle.unity |
| `0faa1f3b9` | Update Skimmer.prefab |
| `5a3d2f38e` | Update Dolphin.prefab |
| `67dca0a13` | Rename Fake Crystal to Mine |
| `cc5792ef5` | Update ExplosionImpactor.cs |
| `662e300ed` | Deleted Old HUD Scripts |
| `c6aeb91af` | Restructure folders + Missiles recharged when hit crystal |
| `0c0bd1ab6` | Serpent modifications |
| `6c81efb21` | Silhouette UI + Trail block UI for each ship |
| `f0cf3bd15` | Fix Bugs |
| `6c8c35028` | Modify Base Classes to have fixed Effect Types |
| `14c6300d5` | Remove ShipExplodeCrystalEffect |
| `1fa066016` | Assign and Restore Vessel Impactor Impact Effects with SOs |
| `49642c782` | Reorganize Project Folders for Impact Effects |
| `172f04f2e` | Reorganize Impact Effect SO List in editor |
| `f06135bc6` | Canvas Scaling Issue Fixed |
| `8d9760193` | Restore Multiplayer Freestyle Scene 1 |
| `a07f49852` | Restore Session creation and joining |
| `e4409ed83` | Add Initialize bool to all vessel scripts [rename Ship -> Vessel] |
| `9843f8afb` | Impact Effects |
| `4ed6c4896` | Update ImpactorBase.cs |
| `ef85cfb51` | Rename Game Data SO Asset |
| `55ebca380` | Update VesselDeviationByPrismEffectSO.cs |
| `056933803` | Sparrow Ship Actions Refactored |
| `cdb453074` | Refactor Dolphin Ship Action |
| `371796a30` | Rename IShip -> IVessel |
| `8f5f943d5` | Update ChargeBoostActionExecutor.cs |
| `3504179d0` | suction shader and refactor of pool management for use in implosion effects |
| `9f95236b7` | Update Sparrow.prefab |
| `d04ebcbd1` | Fix VesselTransformer  Null Reference bug |
| `cb3aba3c6` | Fix Team Sync issue |
| `0a4d557e2` | Update VesselController.cs |
| `897e38819` | Add AI Pilot Ability |
| `57269e908` | Rename Classes from Ship to Vessel |
| `35dfaa18b` | Sync Team and Vessel Type in Multiplayer |
| `da2ecfd9e` | Reconfigured Player Spawning System |
| `dd8f6a34d` | Major Bug Fix (Duel for the Cell and Freestyle) |
| `9e9b121e2` | Restrict Listening to VesselActionHandler's Events if Vessel is AI |
| `6e090876b` | Update Multiplayer setup to 4 max player |
| `a046afd27` | Crystal Bug Fix + Initialize Action Registry for AI Vessels |
| `d3ef817cb` | Load Main Menu scene on host disconnect |
| `fe51baf69` | Manta Ship Refactor |
| `d20a19cc7` | Serpent Ship Action |
| `efc472e80` | Update Pre Processor Directives |
| `38700fe6d` | Minor Bug Fix |
| `375753a27` | Increase Team Crystal Distance |
| `e6929977d` | Add End Effects |
| `c748a8220` | Update MultiplayerSetup.cs |
| `e8414950c` | Update Network Protocol Version to 2 |
| `aebd744da` | Update SkimFxRunner.cs |
| `be60ad7d2` | Update VesselController.cs |
| `ce7b71097` | Update GameManager.cs |
| `2f5be4990` | Update NetworkShipSpawner.cs |
| `615732541` | Update MultiplayerSetup.cs |
| `0e1369d21` | Modify Multiplayer Session System to create session and join in the gameplay scene, |
| `03193eff5` | Manta Overcharge Ally Shield and Pop Enemy Shield + Yawstery Action |
| `6d6ea7bc2` | Dolphin Block Impact Effect |
| `e3a15854e` | Improve Yawstery + Partial Rhino Ship Action Refactor |
| `5a1849887` | graph tweaks |
| `995d0e99d` | Restructure Folders + replace mine model |
| `9f6a9b29a` | better colors |
| `ffd6eaf32` | Fix Error |
| `9267d1ecb` | Refactor Pool System and create PrismExplosion and PrismImplosion |
| `b494cc8d6` | Fix Material issue in PrismClone prefabs |
| `0ddb8528e` | Fix Full Auto Projectile Max Pool Size limit reach |
| `3f7a3c049` | Update MultiplayerSetup.cs |
| `d3431424a` | Create Prism System with new Pool System / Factory and Explosion / Implosion Effects |
| `def4f9830` | Create Grow Prism Effect |
| `e9efc4391` | Update PrismExplosion.cs |
| `e1698af16` | Update PrismImplosion.cs |
| `2e585a77b` | Update TrailBlock.cs |
| `b32a9ce09` | Rename TrailBlock to Prism |
| `c3e3bfcf7` | Restore Prism Creation with Scaling |
| `10b41c041` | Fix Squirrel Stay Effects + Fix Bugs + Fix Dolphin UI |
| `602c912d6` | Handle host leaving multiplayer session |
| `d80ea3587` | Revert "Merge pull request #19 from YsKhan61/codex/implement-session-end-when-host-leaves" |
| `bc9cecd11` | Handle Host Leaving Multiplayer Session with Client go back to Main Menu |
| `8a66a7ea1` | Activate Vessel Actions in Multiplayer |
| `bf2302a42` | Refactor Sprint Tasks |
| `bbfa57880` | Serpent Ship Actions Refactor |
| `b1356b9a9` | Dolphin and Sparrow changes |
| `75c936023` | Update R_VesselActionHandler.cs |
| `31efbad79` | Update MiniGameDataSO.cs |
| `68efc7921` | Rename Block Stolen to Prism Stolen |
| `238f230ce` | Update Scoreboard.cs |
| `a7911175d` | Update VesselController.cs |
| `689c2552e` | Update PrismSpawner.cs |
| `e1f98b851` | Remove Unwanted PrismSpawner |
| `200c7dca7` | Update Serpent |
| `2ed50eb6b` | Update PrismScaleAnimator.cs |
| `597c2756f` | Diagnose and restore Prism Volume Tracking |
| `b0e76d9b0` | Update Skimmer |
| `d8d9236f2` | Activate Skimmers for Multiplayer |
| `84f01fdbb` | Initialize HealthPrism along with renaming from HealthBlock |
| `dd8a2539f` | Fix Manta Overcharge Effect |
| `ce451212f` | Update MinigameCellularDuel.unity |
| `3ac063871` | Update NetworkShipSpawner.prefab |
| `e80c5abda` | Update Game Scene Main Camera.prefab |
| `bd6cf063b` |  Update MinigameDuelForCellMultiplayer_Gameplay |
| `da00fa3d1` | Fix GameDay Bugs |
| `7eecae1b0` | Unstable prism and material |
| `58119adb1` | Fix Serpent Boost Bug, Wall Seed Speed |
| `04a8486f0` | Increase Serpent Wall Speed |
| `c7c1485fc` | Rhino Boost Toggle |
| `cf4c8b284` | Manta Collect Prism Buff |
| `cae7e7cd6` | Serpent Cloaking Effect |
| `c021beb18` | Fixed Double Kink |
| `f7768745e` | new overcharge effect |
| `df1de404a` | Update TeamAssigner.cs |
| `aec024d34` | Update MultiplayerSetup.cs |
| `d9dddafce` | Update SlowShipViewer.cs |
| `2a62aaa44` | Modify Game Start Sequence |
| `18eb1441e` | Modify Player and Vessel Spawning system in multiplayer |
| `0d1e04143` | Update IVesselStatus.cs |
| `64fb2e8d1` | Implement All Players Ready for Multiplayer Cellular Duel |
| `ec3b88d88` | Remove PlayerLoader - Deprecated |
| `baadca397` | Update Player.prefab |
| `068e45d5e` | Create Multiplayer Cellular Duel Game End System with Scoring |
| `6fe420c24` | Sync Time Display UIs for Game Rounds between all clients |
| `a398c0dfa` | Refactor Volume Display System |
| `53e775040` | Implement Play Again System |
| `61906268a` | Spawn Prism Prefabs under their pool parent |
| `622c84a89` | Update VesselStatus.cs |
| `d5817cb84` | Remove Pause System from MiniGameController and add to Game Manager |
| `ea33f5cd8` | Update MiniGameData asset to serialize IPlayer |
| `569e55dc2` | Update MiniGameHUD.cs |
| `8a5889a67` | Implement ResetOnReplay Functionality to Prism Explosion and Implosion |
| `2fddc497e` | Create Pool System for Interactive Prisms |
| `5a64adc9b` | Rename PrismSpawner to VesselPrismController and fix Freestyle initialzation |
| `fd2e4835d` | Create Vessel Prism Pools and modify Game Execution Flow |
| `a9f3da833` | Fix Multiplayer Duel Cell replay related issues |
| `a1f2d4063` | Update MultiplayerCellularDuelController.cs |
| `d5537bcd0` | Update MultiplayerFreestyleController.cs |
| `810a61ee2` | Implement Host/Client Leave Session |
| `0c5ee42d5` | Fix OnlineDuelForCell initialize issue |
| `26e80eb18` | Update PrismFactory.cs |
| `246eda97b` | Reset Team Assignment Cache when session ends, and Refactor MiniGameController System |
| `de8e1d199` | Update Multiplayer Freestyle Session Exit |
| `a7b540443` | Update VesselPrismController.cs |
| `3fe925841` | Update Multiplayer Duel For Cell Session Exit |
| `62cf40d96` | Refactor MiniGame System -> Stop Rotation if IsStationary |
| `db466b5ac` | Create NetworkTurnMonitor | Update Reset Vessel Transformer Logic | Update Domain Assign Logic |
| `828ea7f00` | Update DomainAssigner |
| `07d8c2aa9` | Update FreestyleController.cs |
| `417a2a299` | Add Session Queries in multiplayer setup |
| `1a2a8539b` | Cloak Wall Seed Script Fix |
| `70138da24` | Bug Fix: Late joiners in Multiplayer Freestyle need to be activated in other clients. |
| `68df9f94a` | Update MiniGameDataSO.cs |
| `308ac144a` | Reduce dependency of Crystal |
| `903fb884f` | Add Ship Translation Restricted Feature |
| `57f1ea449` | Translation Restricted Dependency in Action Classes |
| `689e748b0` | Translation Restricted Dependency in Action Classes (2) |
| `50a5a9092` | Hud Controller Cleanup |
| `34a3bf5e3` | Add Translation Bug Fix |
| `d3b8474d2` | Sparrow Charge Boost Fix |
| `ec5167640` | Sparrow Full Auto Gun Try Fix |
| `215771cbc` | Fix Sparrow Prism Deviation Effect |
| `ab2073ab0` | Detonate End Effect Try Fix |
| `81f3a7dd1` | Sparrow Prism Gun Feature |
| `76895d386` | Dolphin Setup |
| `4182de41e` | Toggle Mode UI Dependency |
| `d60f59b81` | Rename Stationary -> Translation |
| `35218b10a` | Create Notification System |
| `c7292dfe7` | Tweak Notification Setting |
| `970a096c7` | Add Notification UI in all vessels |
| `220eb498e` | Fix Auto Projectile Of Sparrow Part 1 |
| `a6745cd1a` | Fix Auto Projectile of Sparrow Part 2 |
| `968810c7f` | Fix Auto Projectile of Sparrow Part 3 |
| `98c4af84e` | Detonate Sparrow Projectile End Effect modified |
| `965ca5689` | Fire Gun action Follow Ship Course |
| `d2d9071d1` | Squirrel Skimmer Align Effect Fix |
| `c702fc11e` | Rhino Camera Bug Fix Part 1 |
| `053d405d0` | Refactor Cell and Crystal with CrystalManager - Main Menu working |
| `389874fa2` | Refactor Cell and Crystal by CrystalManager - Other scenes working |
| `9c2cd6946` | Rhino Camera Break Issue |
| `6a20b4b33` | Rename MiniGameDataSO to GameDataSO |
| `3eaed11d0` | Initialize CrystalManager from game data initialize event |
| `73f675085` | Modify CrsytalManager initialization and SnowChanger - Part 1 |
| `66741e66a` | Adding time crystal and testing crystal with new normals - angelo |
| `9cc7d713c` | sending new tangent space mass crystal test - angelo |
| `5a7f8c7d2` | Revert "Modify CrsytalManager initialization and SnowChanger - Part 1" |
| `0e7efffaa` | Sync Crystal position and impact with vessel through network |
| `56d5327bb` | Update NetworkCrystalManager.cs |
| `e2d61ee11` | Add CellData asset |
| `6de47f814` | Reapply "Modify CrsytalManager initialization and SnowChanger - Part 1" |
| `3a72c6d4d` | Update GameDataSO.cs |
| `a62e6b723` | Rename MultiplayerMiniGameControllerBase |
| `32aa698c6` | Implement 2 rounds for Duel For Cell - Singleplayer |
| `b35613b8e` | Rename Velocity Dandruff to Crystal Explosion Dummy |
| `2de54a9b0` | Implement 2 rounds for Duel For Cell - Multiplayer |
| `2e9764d99` | Update MinigameCellularDuel.unity |
| `15f2338a3` | Update MinigameDuelForCellMultiplayer_Gameplay.unity |
| `a8c62fe15` | Implement Vessel Change mechanics |
| `eaad75d85` | Re initilalize Methods for Camera and HUD |
| `b24312030` | Implement Vessel Swap for Singleplayer Duel Cell |
| `827456b74` | Fix Bugs of Vessel Swap in both Singleplayer and Multiplayer Duel Cell |
| `98ab8df28` | Rename CellularDuelController to SinglePlayerCellularDuelController |
| `365a58d92` | Sparrow Projectile Bugs |
| `637790a12` | Remove Rhino FX Particle |
| `1209c3805` | Multiplayer UI Sync FIx |
| `ec5d5cdc9` | Refactor VesselHUDController |
| `ff58e797c` | Remove Rhino Resource Dependency |
| `26451c9f8` | Auto Projectile Wider Spawn Points |
| `d600d1c08` | Fix both Vessel UIs showing in client (non-server) and Singleplayer Duel For cell |
| `2a95cd6ef` | Update SinglePlayerMiniGameControllerBase.cs |
| `7a35dd7f0` | Implement Score UI display |
| `67bf29f2a` | Cleanup MiniGameHUD |
| `cb309ecd2` | Cleanup StatsManager and NetworkStatsManager |
| `ca15decb1` | Update StatsManager.prefab |
| `b1a199f42` | Update NetworkStatsManager.prefab |
| `bc9664fb7` | Update FreestyleController.cs |
| `e4b459f68` | Update ScoreTracker.cs |
| `d41cdb599` | Update MinigameFreestyle.unity |
| `c7616b765` | Create VolumeTest Scene |
| `4706dc4f7` | Update Menu_Main.unity |
| `80eb9e038` | Create Vessel Volume Test Scene |
| `fa0152ca6` | Fix AOE Slow Explosion Spawn Point |
| `bf4b7da69` | Create Vessel Explosion Effects Container and VesselSlowByExplosionEffect for AOESlowExplosion |
| `1845d3edf` | Update GameDataSO.cs |
| `db2a3c511` | Update SlowShipViewer.cs |
| `21025b33d` | Update ExplosionImpactor.cs |
| `9932d6ce6` | Add AOEConicExposionImpactorDataContainer |
| `94750ff26` | Update MinigameCellularDuel.unity |
| `52016bb10` | Update DefaultNetworkPrefabs.asset |
| `97cc3925d` | Skyburst Missile Explosion End Effect Fix |
| `aba00c8e8` | Refactor Detonate End Effect |
| `2a686c15c` | Vessel Deviation Fix |
| `f11036f13` | Fix For Build (Deleted Obscured prefab Player) |
| `35084131b` | Change Detonate End Effect Asset |
| `45a53f991` | Change Vessel Deviation value |
| `0b17e78d4` | Change Sparrow Guns Activation Text |
| `2b62886b8` | Update MinigameCellularDuel.unity |
| `d212126dd` | Update MinigameDuelForCellMultiplayer_Gameplay.unity |
| `8b9fc692f` | Update RhinoVesselExplosionByCrystalEffect.asset |
| `1a2e0a46f` | Update NetworkManager.prefab |
| `8ac24fd6b` | Update EditorBuildSettings.asset |
| `b0855e80c` | Update DefaultNetworkPrefabs.asset |
| `a258330aa` | Update Projectile.cs |
| `c2240daf1` | Update MultiplayerSetup.cs |
| `aadeaf2f9` | Update NetworkManager.prefab |
| `ca5df6e8d` | Move SparrowExhaustProjectile Prefab in prefab folder |
| `8f13726df` | Delete ExplosionPoolConfiguration.asset |
| `4c7de6fe9` | Move PrismFactory script to Prism Folder inside Script folder |
| `515aafb82` | Move OnLifeForDestroyed event to SOAP Asset Folder |
| `7a2423080` | Remvoe Old Pool System Scripts |
| `cb8608be4` | Update DefaultNetworkPrefabs.asset |
| `e166317d9` | Delete ExplosionPoolConfiguration.asset.meta |
| `9b62d6e90` | Update AOEConicExplosion.prefab |
| `d95164d35` | Refactor PlayerCount and Intensity Select System |
| `ae9868e5f` | Update Prism.cs |
| `1de363df1` | Update ArcadeExploreView.cs |
| `d43d92da8` | Handle Session Full situation in multiplayer |
| `b3349b318` | Cleanup VesselImpactor |
| `758652551` | Update OmniCrystalImpactor.cs |
| `ffdd2352d` | Update Dolphin.prefab |
| `2b0d54a14` | Intensity Setup |
| `83b01d4a9` | Sending new (hopefully fixed) expanded triangles. Origin point should be fixed |
| `7ee119802` | Fix Sparrow SkyBurst Missile Follow Parent |
| `3d91f348f` | Add Separate Container for  SkyBurst Projectile |
| `cf474cad2` | Working with "shield" tutorial mesh + new fixed triangles |
| `8dce1958a` | Manta Overcharge Gradual Switch |
| `1c26cedfc` | Rhino Camera Bug |
| `af72d24ae` | Shard Toggle Debugs Closed |
| `a003f94ce` | Attach Team Crystal To Vessel |
| `ba3ba365e` | Testing with new shield/membrane shader - not finished |
| `654f831d4` | Update PlayerCountButton.cs |
| `5229c70da` | Clean VesselController |
| `a5eb40d6b` | Rename ShipAnimation to VesselAnimation |
| `231be0947` | Cleanup VesselStatus |
| `f4c01f0c6` | Update Vessel Initialization Logic |
| `f8285c076` | Fix Animation not working in multiplayer |
| `d1a3022ca` | Update Scoring System |
| `d882b232b` | Refactor Skimmer Overcharge SO script |
| `acec23487` | Update Score Board for Duel For Cell |
| `1fb925112` | Update MiniGameHUD views |
| `625d27b50` | Update GameDataSO.cs |
| `ab932e91c` | Update DefaultNetworkPrefabs.asset |
| `7708e8613` | Update NetworkRoundStats.cs |
| `7a09cd61b` | Refactor RoundStats |
| `9bf65468c` | Update VolumeCreatedScoring.cs |
| `d2f06a1e6` | Delete NetworkRoundStats |
| `8fbaa7ea8` | Update ClientPlayerVesselInitializer.cs |
| `e2d9f3cb5` | Update VolumeCreatedScoring.cs |
| `e39128cd9` | Serpent Seed Wall Fix |
| `718299ffa` | Add Volume Destroyed Scoring to Duel For Cell |
| `6a080b8b9` | Cleanup Scoring System |
| `3db290879` | Fix Prism Spawning in client on second round |
| `31b52c903` | Update Player.cs |
| `13366d60d` | Update VesselController.cs |
| `645bd1b40` | Update VesselStatus.cs |
| `f80d1c5fa` | Update VesselController.cs |
| `d762e00c8` | Update MinigameDuelForCellMultiplayer_Gameplay.unity |
| `23b5a0caa` | Update VesselController.cs |
| `4e50feff2` | Fix Back To Main Menu system |
| `fb5093db7` | Fix Animation Sync issue in Multiplayer |
| `dbef7079e` | Update MiniGameHUD.prefab |
| `2ed89ec7f` | Turn off Full AutoActionExecutor on Turn End |
| `52954732c` | Fix Cloak Seed Action |
| `1d2cbafcf` | Seed Assembler refactor |
| `dc183ef77` | Consume boost action fix |
| `325221661` | Remove Ship FX |
| `4df865c0a` | Restructure folders |
| `579f3d879` | Restructure Folders (1) |
| `f1f74d108` | Refactor Charge Boost Action |
| `71ce938bf` | Refactor ConsumeBoostAction |
| `1d70946e4` | Refactor Deploy Team Crystal Action |
| `36cdb0436` | Refactor Drift Trail Action |
| `4de8b8f49` | Refactor Fire Gun Action |
| `57e7353c7` | Refactor Full Auto Block Action |
| `18faea632` | Refactor Grow Skimmer Action |
| `4ff353d7f` | Refactor Grow Trail Action |
| `34090a525` | Refactor Overheating Action |
| `f6d2d5033` | Refactor Seed Assembler |
| `140b2a1b0` | Refactor Shard Toggle |
| `1d13bf000` | Refactor Toggle Translation |
| `b055650cb` | Refactor Yawstery |
| `44e66aade` | Refactor Zoom Out Action |
| `0577bf339` | Restructure Folder + Refactor Cloak Seed Wall |
| `2cae2532b` | Update Dolphin |
| `d32e1d291` | Update Manta |
| `596b29177` | Update Rhino |
| `3e65c9a6a` | Update Serpent |
| `ba0dae4c0` | Update Sparrow |
| `5e3e51e2d` | Add Tun End Event on Sparrow Full Auto Projectile |
| `7afcd8062` | Overview Panel Configuration |
| `4e5342981` | Refactor Vessel HUD Controller into Trail Pool UI and Silhouette Scripts |
| `0f84d36b8` | Script Cleanup |
| `e8c6e9c3c` | Update Dolphin Silhouette |
| `e6fbc2d42` | Update Manta.prefab |
| `4daba6dc6` | Update Rhino.prefab |
| `ebd8e93d2` | Update Serpent.prefab |
| `7f3608189` | Update Sparrow.prefab |
| `d96643419` | Update MinigameDuelForCellMultiplayer_Gameplay.unity |
| `62c2c9abc` | Update TimeBasedTurnMonitor.cs |
| `4e7fc44d9` | Update OmniCrystalImpactor.cs |
| `a3d8596e4` | Update OmniCrystalImpactor.cs |
| `554c4680e` | Update Menu_Main.unity |
| `3de103094` | Fix Multiple Crystal Explosion Bug in Multiplayer |
| `0c5f5a126` | Update Crystal.cs |
| `019d264fe` | Refactor the Fix of Multiple Crystal Explosion Bug in Multiplayer |
| `e0dc0f43d` | Fix Coroutine based trail container |
| `62a38c45c` | Update VesselController.cs |
| `0f799b177` | Sparrow Multiplayer Translation Fix |
| `40e3201e9` | Cleanup AOEExplosion |
| `f1618d486` | Fix Rhino AOE Explosion |
| `08c4a511c` | Update Rhino.prefab |
| `7868d1dd9` | Rhino Bounce Back Super Shielded block |
| `7487b4449` | Implement FriendlyVolumeDestroyed and HostileVolumeDestroyed in duel for cell scoring |
| `c2c9f3555` | Fix Prism not spawning in non owner vessels |
| `67d380f3d` | Delete Hud Profiles |
| `6801479be` | Update VesselStatus.cs |
| `d1c77478a` | Fix Serpent Consume Boost |
| `1af46b474` | Update RhinoForceFieldSkimmerImpactorDataContainer.asset |
| `556c3472e` | Tune Cooldown Time |
| `88f6ef62c` | Update MiniGameHUD.prefab |
| `ec939d85c` | Clean Countdown UI after each turn end |
| `e3f15a533` | Update MinigameHUDView.cs |
| `a5dd1cff1` | Remove NetworkBehaviour inheritance from ImpactorBase, add NetworkVesselImpactEffect |
| `daa9edd3f` | Update MiniGameHUD.cs |
| `b165eaf1b` | Refactor Silhouette, Refactor VesselController |
| `1cdcefff4` | Fix Grow Skimmer Action and Zoom Out Action |
| `4a7843b3b` | Fix Rhino Slow Vessel Explosion Effect |
| `db2e6bcb9` | Remove Toggle for Manual Throttle |
| `d677ab938` | Update Silhouette.cs |
| `c662f466d` | Update Manta.prefab |
| `b2f6205f8` | Update Rhino.prefab |
| `a8670bcfb` | Update Sparrow.prefab |
| `402b0396d` | Fix Sparrow Projectile Client Bug |
| `038565e13` | Update Rhino Trail Reference |
| `28ebaef9f` | Implement RoundStats for Duel For Cell and Multiplayer Duel For Cell |
| `4c81df708` | Fix Skyburst Projectile No Domain Found |
| `bf4406c6e` | Rename ModeSwitchingFireSO to SparrowModeSwitchingFireSO |
| `55d32b857` | Replaced the top menu name for FrogletTools with a constant that can be changed centrally. |
| `ece2ec68e` | Change Volume Test Scene |
| `dba12e58c` | Update SkimmerOverchargeCollectPrismEffect.asset |
| `0a9113813` | Restore Silhouette Script |
| `ed7fa21ae` | Update Rhino Silhouette |
| `610bfa783` | Update Sparrow Silhouette |
| `f2131ac1a` | Update Sparrow.prefab |
| `7894b35ef` | Update Manta.prefab |
| `6f14a4520` | Update Sparrow Overcharge Feature |
| `8fd4dc81a` | Update R_VesselActionHandler.cs |
| `3295fab7e` | Update Silhouette and Domain Color Palette Script |
| `b68c3425c` | End Danger Production At Decay End |
| `5c1298087` | Update Sparrow.prefab |
| `7de12c5e4` | Increase Manta Coroutine Based Swap |
| `bda2b847b` | Fix: Multiplayer Freestyle: Only owner client should start it's vessel on pressing Ready Button |
| `f907b3090` | Fix: Highlight on Vessel Action UIs not working |
| `4278b4b92` | Update VesselController.cs |
| `6979dfea9` | Modify Serpent Cloak Action |
| `def4bb2ad` | Fix: Null Reference Vessel Status on destroying Vessel |
| `fac73d362` | Increase Timer to 120 seconds |
| `cf70cd28d` | Fix Sparrow prism projectile behavior |
| `c58178f08` | Update DomainColorPalette.asset |
| `9384a9f31` | Update SparrowSilhouetteConfig.asset |
| `dfe89c499` | Fix: Turn Monitors need to be stopped if user leaves the game in the middle |
| `c848a54ad` | Update Dolphin Silhouette |
| `1508d6e46` | Update Serpent Silhouette |
| `4eb5d7e46` | Update Squirrel Silhouette |
| `35d63ba7e` | fixed gapless trails |
| `b9170fde6` | Create Vessel Damage By Skimmer, Modify Handler and Impactor Classes |
| `3ef3f736f` | Fix Input Block Bug |
| `b18a7236f` | Create Vessel Spin By Skimmer |
| `614117aaf` | Reflect Mute Input in HUD |
| `29f7852f2` | Update Rhino.prefab |
| `30776923e` | Fix Sparrow Prism Projectile Behavior |
| `63c0c7f79` | Fix Sparrow Boost |
| `c16966bfc` | Update Rhino (Disable Notification Presenter) |
| `7135b0609` | Create Vessel Shrink Skimmer Effect |
| `52308d184` | Update VesselSpinBySkimmerEffectSO.cs |
| `80281ac31` | Update Silhouette.cs |
| `9bb727cc0` | Update GameData.asset |
| `e3b911317` | Add Delay to calculate the winner after round ends to sync accurate multiplayer score |
| `6900a26d3` | Fix Sparrow Missile as per documentation |
| `63a48ca5a` | Refactor Vessel HUD View |
| `a017d0982` | Update SparrowSkyBurstProjectileImpactContainer.asset |
| `6ac3d818d` | Disable Old Notifications |
| `5e30329d7` | Update SkyBurst Missiles |
| `a04c0dc05` | Fix Prism Collider Issue |
| `3a2257062` | Add Text Packs |
| `a72c14cdb` | Add Connecting Panel in Multiplayer |
| `bd578126d` | Restore Dolphin |
| `86c44be38` | Remove Ship HUD Container Dependency |
| `0abcefe84` | Add Booster Icon |
| `b4e5070b4` | Fix UI Anchors Rhino |
| `89895f130` | Update DolphinSilhouetteConfig.asset |
| `204a0b757` | Update DolphinSkimmerImpactorDataContainer.asset |
| `e603007ab` | Update Sparrow UI |
| `20a767ed7` | Fix Bug |
| `b7cd5f972` | Fix : Multiplayer Freestyle Trails of Non-Owner Clients not spawning |
| `6701487e7` | Update DefaultNetworkPrefabs.asset |
| `0065f3de3` | Update MinigameFreestyleMultiplayer_Gameplay.unity |
| `dd2d9dc06` | Fix: Double Invokation of OnClientReady |
| `1f94aab84` | Update GameData.asset |
| `1a4f521cb` | Refactor GameData Events for accurate execution order |
| `422fd9790` | Update Sparrow.prefab |
| `b83601c61` | Update Dolphin Silhouette |
| `4793c1991` | Update Manta, Implement Toast System |
| `2a39fbdec` | Modify Dolphin HUD |
| `afbe5f38a` | Sparrow Boost Bug |
| `3b6f6de5a` | Update Sparrow UI |
| `005a30328` | Fix Serpent Cloak Bug |
| `5523cd895` | Update DolphinSilhouetteConfig.asset |
| `1832c4fb5` | Fix Rhino Zoom Out Camera Issue |
| `4557f0abe` | Update Project to Unity Version 6000.0.62f1 |
| `d4dd8e99c` | Refactor Camera |
| `69b2f9b24` | Disable initialization sequence (temporary change, faster initial loads). Remove broken games from arcade. |
| `fd092bb40` | Disable Multiplayer Pause System |
| `49c493041` | Get rid of unused material sets |
| `e85f48931` | Fix hostile volume destruction being counted as friendly volume destruction. Fix bug and simplify how volume destruction scoring works. |
| `598bf463b` | Fix clear prisms (setting prisms to transparent if they occlude the ship) |
| `00fba3e59` | Unity INSISTS these materials need to be updated. |
| `13a3fab01` | Delete old unused (redundant) materials, normalize material naming for prism related materials. |
| `fdaed3528` | Update FriendlyVolumeDestroyedScoring.cs |
| `7e961ce69` | Restore Block Bandit, minus scoring, intensity, and pass and play support. |
| `365a9c68c` | Update GameDataSO.cs |
| `e511ab610` | Update SinglePlayerMiniGameControllerBase.cs |
| `ad64e5769` | Upgrade Pause Menu |
| `63b053821` | Disable Multiplayer Pause System |
| `38a4b479e` | Fix Sparrow Turret Bug |
| `a3c4b6b0b` | Fix Sparrow Deviation after translation restricted |
| `4c2028f2b` | Update Prism to default layer collision matrix setting, Expose layername from Prism Properties |
| `87f3132bd` | Update VesselDeviationByPrismEffect.asset |
| `a38894bf4` | Update Omni Crystal System |
| `2a5e15e3f` | Update GameDataSO.cs |
| `805f0d405` | Update PauseMenu.cs |
| `5a883a6eb` | Rename IVesselStatus Properties |
| `6b426b713` | Remove Reflection Code, Refactor Ship Action |
| `4212fc06f` | Implement Reduced Sparrow Prism Scaling due to boosting |
| `478c36748` | refactor actions |
| `da4318fcb` | Revert "Merge branch 'master' into OrganicRematchPolishYS" |
| `b952579eb` | Remove Extra Connecting Panel from Game Canvas in Multiplayer Freestyle scene |
| `3a4953498` | remove idle input from left shoulder on gamepad input strategy |
| `dd17efef0` | Update VesselCrystalImpactEffects |
| `41a12663d` | Clean Prism.cs |
| `99d17e377` | Clean Prism.cs |
| `cfdea0592` | Implement Correct Scale to Prism Explosion based on Interactive Prism |
| `22200d7ac` | Implement AOE Explosion Cancel and Destroy on Turn End in Minigames |
| `e94b7a2ae` | Refactor Dialogue System, Upscaled Images, Refactored Tool |
| `eca6477e8` | Introduce SafeLookRotation to guard against NaN quaternion rotations. |
| `13dae0d50` | Update AOERadialBlocks.cs |
| `f48147011` | Polish Menu Main Phase 1 |
| `b1d185388` | Polish Menu Main Phase 2 |
| `36145c9db` | Clean ShipHelper |
| `866dad6ef` | Update AOEExplosion.cs |
| `09bb89d5c` | Modify SparrowFullAutoProjectile Vessel Impact to restrict which vessels to act on. |
| `2da77bc78` | Modify SparrowFullAutoProjectile Impact Effect to Reduce Rhino Near Field Skimmer size |
| `21be0139b` | Remove VesselSpinBySkimmerEffect from Sparrow Impactor to RhinoForceFieldImpactorDataContainer |
| `75bb09f90` | Reduce SkimmerSize change multiplier of Rhino Near Field Skimmer by Sparrow Full Auto Projectile |
| `e49cff48d` | Manta Scout Trail Prisms |
| `7471611a9` | Update SafeLookRotation.cs |
| `76000784a` | Create Vessel Danger Block Formation By Skimmer [RHINO] |
| `22b5ae269` | Fix Overview Panel and Pause Menu For Singleplayer Freestyle |
| `1fa190470` | Create UnTask Extension |
| `96b0fd7a8` | Update MinigameHUDView.cs |
| `7d224311a` | Update MiniGameHUD.cs |
| `cf6049884` | Update AOEConicExplosion.cs |
| `b8140e9c5` | Update InputController.cs |
| `783ed0519` | Update R_VesselActionHandler.cs |
| `33a6c623a` | Update Rhino HUD |
| `7c74343da` | ability fix |
| `fc3b3ef33` | Update GameDataSO.cs |
| `f47d7f5c8` | Rename PlayerVesselInitializerHelper to VesselInitializerHelper |
| `d97c56ca0` | Update VesselController.cs |
| `d9bda703c` | Update R_VesselActionHandler.cs |
| `99732ab3d` | Update ControllerButtonPress.cs |
| `0edbc392f` | Update SinglePlayerMiniGameController Game flow |
| `99126f4e2` | Refactor OverviewPanelController |
| `764611318` | Revert "Update SinglePlayerMiniGameController Game flow" |
| `497caa564` | Fix Singleplayer Duel For Cell Pause System |
| `b05940ae7` | Update GameCanvas and Pause_MenuPanel Prefab for PauseSystem and Overview System |
| `19e5d05d9` | Replace Danger Material to Theme Manager Danger Material Data |
| `ba6013f42` | Fix Sparrow Boost Issue |
| `75496d239` | Fix Sparrow Destroying Prisms by Stopping |
| `46436763f` | Refactor and Tune AOE Danger Hemisphere Blocks |
| `b0d02d3c9` | Tune Full Auto Block Action |
| `4e835b228` | Comment out unused spindle code |
| `f171b534a` | Added Tangent Separation to Prisms |
| `1088ff38b` | Update Duel Cell Stats UI to support more datas |
| `364f2557e` | Update Panel Heading - Stats Cell.prefab |
| `c9e1ca04b` | Restrict Volume modified to StatsManager |
| `13c4b83ea` | Fix Scoring System with New Scoring modes for Cellular Duel |
| `6b913c482` | Update MultiplayerDuelCell with new Score System |
| `a2a5debb8` | Revert "Restrict Volume modified to StatsManager" |
| `ff12d4751` | prism material tweaks |
| `20a1bb317` | Update Volume Scoring Mechanics |
| `57ad00b6d` | Update Duel Cell Stats Panel - Player and Round Row.prefab |
| `0d0b73c4d` | Update Singleplayer Cellular Duel  Scoring with Volume Tracking |
| `3e330edd1` | Update Multiplayer Cellular Duel  Scoring with Volume Tracking |
| `e9a6a1299` | Update VolumeUI upper bound to 100000 |
| `c8d88895f` | Align Sparrow Full Auto Guns |
| `5562857dc` | Fix Sparrow Trail UI |
| `9e4a9cfb9` | Update ProjectileImpactor.cs |
| `55a795b92` | Update Prism Interactive.prefab |
| `66899913c` | Fix Sparrow SkyBurst Missile Bug on High Velocity |
| `46f06ebe1` | Fix Danger Blocks Domain, Remove Shield Property |
| `b146f106f` | Fix Rhino AI HUD Bug + Modify Rhino Yaw, Pitch and Scale |
| `edfe8697d` | Update SkyBurstProjectile.prefab |
| `0cb0cac95` | Fix Sparrow Auto Prism not registered in Scoreboard, Add Complete Ability Set for AI Sparrow |
| `6636d7d5f` | Fix Stationary blocks don’t shrink shields as intended |
| `b6b1d9dcb` | Fix Rhino Spawning Multiple AOE Explosions |
| `3223dc270` | Increaase Cooldown Tuning |
| `ab3deb605` | Update Danger Block Color |
| `58f59f562` | Add new Icon for Rhino Debuff Ability |
| `fd0f1b9c1` | Update Vessel Change Size by Skimmer only affects Max Scale of Force Field Skimmer |
| `48bbe7c3a` | Fix Shooting Carry Over to Next Round |
| `4b2980f62` | Fix hiding inherited members warnings |
| `93cc3a0ed` | Prism shader tweaks |
| `7d508c937` | Update StatsManager.cs |
| `c5782fe1e` | Revert Block Executor Turn Code |
| `eaffc9f7b` | Convert Sparrow Update Code Block to Event Usage |
| `5e2632271` | Implement Friendly Fire on Prism Projectile |
| `03114d61f` | Update Rhino HUD Display |
| `fc61c4592` | Update Duel Cell Stats Panel - Player and Round Row.prefab |
| `65a0e144b` | Fix variable  is assigned but its value is never used warnings. |
| `1e7d56ae7` | Remove "PortraitUI" and fix an explicit cast warning. |
| `946e8aeaf` | Modify Vessel Slow Ship to only affect Boost, Modify Projectile Prisms to match Auto Projectiles Fire rate and destruction |
| `6d6687353` | Create Sparrow Debuff by Rhino Danger Block Formation Impact Effect |
| `0e9555d28` | Update Duel Cell Stats Panel.prefab |
| `0ba3c727b` | Fix Prism Destruction Player Name and Attacker Name |
| `be4e2d54a` | Update DuelCellStatsRoundUIController.cs |
| `053cd28cb` | Fix Sparrow Missile UI Update Logic |
| `e67e969ef` | Updated prism explosions |
| `06e6f1fc7` | Fix explosion bug |
| `7fe7e356d` | Add flower effect to manta |
| `0341699ce` | Null pointer guards. Ignore VS .editorconfig |
| `5ce613c40` | Remove Ship HUD Container Dependancy |
| `30cbe689b` | Update Squirrel.prefab |
| `eee15df32` | Refactor Dolphin to reduce tech debt |
| `61dd8008e` | Refactor Serpent to reduce tech debt |
| `adfbeaf88` | Refactor Manta to reduce tech debt |
| `7b85c1fb2` | Refactor Sparrow to reduce tech debt |
| `e2c769de5` | Refactor Rhino to reduce tech debt |
| `304aea4d2` | Convert static events to SOAP based events |
| `3e9af6766` | Remove unused scripts |
| `f1c051533` | Remove unused scripts |
| `2661174e9` | Update Manta.prefab |
| `9e7c7a2f0` | Update Serpent.prefab |
| `f6ce60626` | fix(prism-pools): prevent explosion spike by increasing pool capacity |
| `4e939cd8d` | Refactor Silhouette |
| `a59428477` | Update Dolphin to refactored silhouette |
| `b37d5e73f` | Update Manta to refactored silhouette |
| `d3b3fa2fb` | Update VesselExplosionByCrystalEffectSO.cs |
| `10fb2fb52` | Expose random seed to block bandit spawners |
| `6e6ab0341` | Checking in "Electronic Highway Sign SDF.asset - unity keeps updating this file, so hopefully if we commit it, it will stop getting changed. |
| `987210a1e` | Limit hangar ships to just vertical slice ships. Fix some minor UI bugs there. |
| `447241744` | refactor: rename runtime data assets for clarity |
| `45d244370` | refactor: rename FreestyleController to SinglePlayerFreestyleController |
| `e6dc14c04` | feat: add WildLifeBlitz game mode |
| `f9b6835ab` | refactor: remove deprecated MissionGames asset |
| `217cdb74a` | feat: implement scoring and turn end conditions for wildlifeblitz |
| `918887586` | feat: Enable spindle logic in HealthPrism |
| `0d4bd0754` | chore: update MassBrittlestarFauna prefab properties |
| `69051e693` | feat: modify wildlifeblitz scene to support mini game features |
| `4e75a0190` | chore: update editor build settings to support wilidlifeblitz |
| `655f881fa` | refactor: crystal prefabs for new domain and cell data |
| `8bd084c38` | refactor: remove commented CreateBlock call in Prism.cs |
| `7f3581674` | fix: Call Initialize on new health block in BranchingFlora |
| `4b6458c43` | reverses mass crystal direction |
| `6fdcfe750` | Updates spindle shader and mass crystal materials |
| `23e7c6614` | Modify Rhino Shield Skimmer Feature |
| `5d514c30b` | Fix Shield Skimmer Grow Issue |
| `ab21beab8` | Spindles add their own property block, wildlife blits uses intensity wise cell selection, |
| `291c4802f` | Add 2v2 CoOp vs AI multiplayer arcade game mode |
| `2c0636924` | updates materials and shaders for prisms and spindles. keeps face sealing but reverts to unsealed edges, and more tuned explosions. |
| `e6abaa3be` | Refactor Arcade Modal + Add UI Assets |
| `a14d1e177` | Update Main Menu |
| `816329a3b` | Update Ship Selection and Arcade Scripts |
| `8a8f94329` | Update Arcade Game Configuration Script |
| `c07896885` | makes sparrow's trail green. adds danger prisms to gyroids, makes crystal explosions more pleasant, adds alpha clipping to spindle material |
| `592d37d72` | Remove unused effect from RhinoForceFieldSkimmerImpactor |
| `c7abc2252` | Refactor SOAP namespace to Soap throughout project |
| `9a428ee8e` | Create Co-op 2v2 gamemode and Add AI support to multiplayer vessel initialization |
| `900982177` | Refactor Profile Modal (Part 1/2) |
| `b63b66e13` | Update Menu_Main.unity |
| `2b34f7db3` | Add UGS Authentication |
| `e56654051` | Import Graphic Assets |
| `721b6b39c` | Import Unity Cloud Save |
| `aaa41244d` | Integrate Player Data Service using UGS |
| `891c81572` | Move Vessel Selection UI |
| `331bcf6c9` | Fix namespace casing for CosmicShore.Soap imports |
| `80416e839` | Refactor ship selection and UI in ArcadeGameConfigureModal |
| `c4dd9a501` | Add focus check before toggling fullscreen on Windows |
| `08cf21dcf` | Update Sparrow prefab |
| `4267ce617` | Refactor Vessel HUD Controller + View, Refactor  SilhouetteController + View, Remove HUD References for VesselStatus  IVesselStatus |
| `895571721` | Refactor Auth Controller |
| `2bcf40546` | Update ArcadeGameConfigureModal.cs |
| `540bce6ad` | Add Minigame Slip n' Stride |
| `b9ffec7fa` | Configure Hex Race |
| `7f9bdbb3c` | Add HexRace and Co-Op2v2withAI |
| `2ca59623e` | restores fixed updates in Skimmer stay effects |
| `80fff53bd` | Add Minigame Wildlife Blitz End Game Condition |
| `d50cafabb` | Fix End Game In Wildlife Blitz Game Mode |
| `df8a5cb67` | Update MultiplayerMiniGameControllerBase.cs |
| `80ca1aecc` | adds Keyboard controls |
| `f8ee769dc` | Refactor Vessel Actions and AI Initialization |
| `fad1040be` | Update Player.cs |
| `765c1ae00` | Restores alignment on Squirrel |
| `81bf4d39d` | adds missing meta file |
| `163445a3b` | Begins a squirrel redesign |
| `16afd16a5` | Adds more squirrel refinement |
| `0f234e3fb` | Add SpawnPoints prefab with four spawn locations |
| `72f16e852` | Implement Online Co-Op Players and AIs spawning |
| `02afe0682` | gives squirrel drifting |
| `d5ce8001a` | Add Squirrel HUD HUD View |
| `e39533175` | Add Multiplayer Wildlife Blitz Game (1) |
| `91729ed70` | Purify drifting for squirrel |
| `53cae57d3` | tune squirrel; enable boost UI to update the decay; clean up a meta file |
| `6569b9390` | Tune Squirrel and add FX |
| `221e35051` | Give squirrel snappier block collision with haptics and a boost on the right trigger |
| `5a8f1bfff` | Configure Arcade Game Multiplayer Wildlife Blitz Player Spawning |
| `b22d63528` | Add 3 input hardpoints to distinguish between either or both triggers; Add two levels of drift for squirrel. |
| `24d165c6a` | updte squirrel prefab |
| `2134e1486` | Fix Fauna Prism Spawning and Dead Condition |
| `c4f2dd640` | Add Lifeform Update Counter, Increase WIldlifeBlitz Time Counter |
| `8b3f9f30f` | Update Wildlife1Cell.asset |
| `4b2c0fb5d` | Update MinigameHUDView.cs |
| `be8fbedbc` | Fix Space Crystal Collision Bug |
| `3f7985803` | Update SparrowImpactorDataContainer.asset |
| `60a528409` | Update VesselHapticsByCrystalEffect.asset |
| `914dcd0ce` | Fix Life Form Counter, Refactor Cell Script, Introduce Life Form Configuration in SO_CellType, Fix Fauna Size and Behavior, Fix Health Blocks |
| `033c16bac` | Update DynamicHealthBlock.prefab |
| `1c2d1799a` | Update Fauna Prefabs |
| `b60786bc6` | Update CactiFlora.prefab |
| `86a683361` | Update MinigameWildlifeBlitz.unity |
| `79086af65` | Update Wildlife1Cell.asset |
| `d77f875bc` | Add SO Based Data to Light Fauna And Light Fauna Manager |
| `4887c70a3` | Add Flora And Fauna Data SO |
| `0010f11f3` | Update Mass Shark Population |
| `a7e8dede1` | Update Mass Brittle Star |
| `e072c28f2` | Update Flora and Fauna Configurations |
| `90c9ff912` | Update Wildlife2Cell.asset |
| `5d4026631` | Update Wildlife3Cell.asset |
| `dfa8ab961` | Update Wildlife4Cell.asset |
| `196960d5b` | Fix Boids and Tadpole Configurations |
| `fc7c95ebc` | Update Wildlife3Cell.asset |
| `0a72b57dc` | Implement CoOp , add End Game and Score Board |
| `4f62b27dd` | Refactor Cell Script + Extend Cell to Random and Intensity wise spawner + Add Cell Profile Data SO class |
| `021ca0cb4` | Add Random Spawn Profile |
| `606cea412` | Include Wild life Cell Domain |
| `4a1308851` | Update Menu_Main.unity |
| `39120df05` | Update MinigameCellularDuel.unity |
| `0b19f39ac` | Update MinigameFreestyle.unity |
| `fcd0448f8` | Update MinigameSlipNStride.unity |
| `5aa38b3c4` | Update MinigameWildlifeBlitz.unity |
| `04d6f1f7a` | Update MinigameDuelForCellMultiplayer_Gameplay.unity |
| `0dd44514e` | Update MinigameFreestyleMultiplayer_Gameplay.unity |
| `8d4f8941c` | Update MinigameWildlifeBlitzMultuplayerCoOp.unity |
| `c06928b84` | Rename MainMenuRandomSpawnProfile to RandomSpawnProfile |
| `26cc2bd95` | Update ArcadeGameMultiplayer2v2CoOpVsAI.unity |
| `87f9d38c8` | Update Projectile.cs |
| `2b06f77a6` | Refactor and Fix Elemental Crystal Collision |
| `9a1856be2` | Flora Elemental Crystal Impactor Update |
| `1011c984d` | Fauna Elemental Crystal Impactor Update |
| `6b3033cfa` | Update SerpentImpactorDataContainer.asset |
| `5da4d1a7c` | Update ArcadeGameMultiplayer2v2CoOpVsAI.unity |
| `89d3fc65e` | Update spawn point positions in 2v2 Co-Op Vs AI scene |
| `949b52652` | Add nucleus scale multiplier to Cell |
| `854e88f5f` | make progress on hex race environment |
| `d37c9103d` | give squirrel drift prisms, jets, and remove the player's visibility of their own tails |
| `54b4f7681` | Restore Lifeform Withering Bug + Restore Lifeform Prism Scaling |
| `1e9aa9798` | Rename TryGetActivePlayerStats to TryGetLocalPlayerStats |
| `029a8fefa` | Refactor HexRace scene and scoring logic |
| `39560174a` | Rename MainMenuDependencyLoader - Deprecated prefab |
| `6f200dcd7` | Delete Domain Picker |
| `3157d3908` | Flora Fauna Prism Explosion Scaling Bug + Local Domain Flora Setup + Flora Death Condition Updated |
| `f28747782` | Update all Wildlife Cell Levels |
| `5b3007e88` | Fix Elemental Crystal Impact Animation + Life Form Count fix + Configure Gyroid Growth interval |
| `497fecb3a` | drift |
| `a254dc75c` | Update Sparrow Skimmer Size + Crystal Collision + Fix Withering Issues |
| `11c5346d4` | Update crystal manager and spawnable rings to better suit racing |
| `34e40f5e9` | Update Race with Bigger crystal and cell, visible shards, and an octagon track. |
| `552a05c41` | align cytoplasm in hexrace |
| `67190e2ab` | Refactor event subscription in TimePlayedScoring |
| `fd035df02` | Update TimePlayedScoring.cs |
| `384208b3c` | Resore intensity scaling on spawning to add tracks to hexrace. Remove Squirrel boosters |
| `d14a4b364` | Restore squirrel overtake ability with faster requirement |
| `64f9c0c24` | Update MinigameFreestyleMultiplayer_Gameplay.unity |
| `6e10946bb` | shield squirrel's prisms when overtaking |
| `664fd4562` | Update SinglePlayerHexRaceController.cs |
| `256999f58` | Update MinigameHexRace.unity |
| `7636ec4ab` | Create Multiplayer Hex Race Game Mode |
| `65637d756` | Update EditorBuildSettings.asset |
| `03ffcaef3` | Update MinigameHexRaceMultiplayer.unity |
| `1c208fcd1` | Make Squirrel overtake prisms wider |
| `d11d7d880` | Delete Old Scipts + Add Dotween Class |
| `7fbba5332` | Import Connecting Screen PNG |
| `8a535495e` | Add End Game Screen on Wildlife Blitz + Add Resources + Fix Invert Y and Invert Throttle + Refactor Pause Menu UI + Add Cinematics |
| `e92d1b28a` | Update MinigameHexRaceMultiplayer.unity |
| `96b1aae4f` | Import sprites |
| `168cf1761` | Add minigame end event to AOEFiveRingSpawner |
| `fd16f9160` | Update and refactor all minigame classes to have consistent common game conditions |
| `e7c2605f0` | Update MinigameBlockBandit.unity |
| `6a030714e` | Update MinigameCellularDuel.unity |
| `f672e18ab` | Update MinigameHexRace.unity |
| `fd4e42c10` | Update MinigameWildlifeBlitz.unity |
| `1ee69ef7b` | Update LocalCrystalManager.cs |
| `b07b8ec89` | Remove GameCanvas dependency + Add Endgame Cinematic Feature + Update Gamecanvas and MinigameHUD Prefab + Update Endgamepanel prefab + Add Endgamecinematic effect for HexRace + Fix EndGameCondition for Hexrace |
| `68009832b` | Refactor EndGameCinematicController with EndGameCinematicView + Refactor CinematicDefinationSO |
| `a1d84d4e4` | Update SinglePlayerHexRaceController.cs |
| `ab711e9e7` | Update GameCanvas.prefab |
| `f154204aa` | Update MinigameHexRace [Final Endgame Awaiting Feedback] |
| `65653779f` | Implement Team Crystal in Multiplayer Hex Race |
| `941c63ab0` | Refactor crystal collection and spawning logic |
| `77dfdf068` | Refactor crystal spawn logic and anchor management |
| `c46811be5` | Update RoundStats.cs |
| `072144c39` | Refactor turn monitors and crystal manager |
| `43ab4a8f5` | Scale Squirrel rings with speed |
| `c9155100b` | make squirrel rings into quarter rings |
| `45f47908b` | reverse ring direction |
| `016eabf0e` | Update OmniCrystalImpactor.cs |
| `d461e79ce` | Replace ChangeSnowSize with ChangeSnowOrientation |
| `90ad226f5` | Redesign overtake and crystal abilities for squirrel |
| `078557b6c` | Make squirrel overtake and collision AOE spawn with player over time and tune so they dont colide with it. split danger and shielded overtake AOE into prefab variants. |
| `560a068a1` | Update GameDataSO.cs |
| `ffa6c0532` | Refactor crystal handling and snow system |
| `da807b263` | make crystal transparent, make Big crystal a prefab variant, use spike shards in race |
| `ff97ef948` | Update MinigameHexRaceMultiplayer.unity |
| `52bf73213` | Update Runtime CellData.asset |
| `bd3e34bf2` | Make IsDomainMatching virtual; add TeamImpactor |
| `371f12d90` | Update Menu_Main.unity |
| `457748c49` | Update Minigame End game condition |
| `eb7dca763` | Update Wildlife Blitz Endgame Score Tracker + Create WildlifeBlitz specific Cinematic controller, Controller and Score Trackers + Update Base Score Tracker to accomodate new end game conditons |
| `d78ca2a0a` | Add WildlifeBlitz Stats Missing |
| `a3c5dca51` | Update CellDataSO.cs |
| `79f1c3191` | Configure WildlifeBlitz Endgame Definition |
| `73e274d15` | Update MinigameWildlifeBlitz.unity |
| `171329c0e` | Update Crystal Masses in all lifeforms |
| `09f33769c` | Spawn crystals using player domain |
| `72aa33c04` | fix player spawn positions logic |
| `d7bbc7c54` | Remove ApplyHelixIntensity() call during setup |
| `14497fa9d` | Create JOUST GameMode |
| `750afee70` | Stop warning when no boost on idle manta animation contoller |
| `3f838dabb` | Fix Singleplayer Endgame Fix + Fix Correct Score Tracking + Fix end game score format |
| `d60fb8d75` | Fix dependencies |
| `3b10696bd` | Score Target set from Cell Type |
| `e3663d537` | Update Wildlife Blitz Endgame Conditions |
| `259ae26a6` | Add Game Data SO to Gyroid Flora |
| `27536d02c` | Update Services Core, Services Analytics + Add Services Leaderboard to Project |
| `4165e15f3` | Update Settings.json |
| `23751e6c5` | Add UGS Stats System for End of Game statistics + Update Best score to Leaderboard + Create Stats UI + Add Analytics Event |
| `ee2105b33` | make all 4 intensity level 4 laps in hex race. fix positions. make crystals spawn closer to corner |
| `c4daa81aa` | Modify code to fix client environment spawning issue |
| `7ab5cc82d` | Configure Multiplayer Hex Race UI + Create Player Score Card Code |
| `94e4bba6d` | Update Hex Race SP to match development conflict |
| `e03fce706` | Sync Network Crystal Score |
| `36bfaa38e` | Add Crystal Capture multiplayer minigame |
| `28909555d` | Inject IScoreTracker into BaseScoring |
| `7a9b496b2` | add ai to hexrace |
| `1c8ab3f2c` | Apply Hex Race End Game Stats to SinglePlayer + Update Leaderboard + Update Multiplayer Crystal Modifier Bug |
| `0303f4fe6` | Use Players.Count instead of SelectedPlayerCount |
| `5acc473f6` | refactor vessel helper to enable ship domain materials |
| `e7c020dcd` | Use GameData.ThemeManagerData for ships |
| `a11ba7d0a` | Apply Hex Race End Game Stats to Multiplayer |
| `952647a31` | Fix Mini Game Hex Race Multiplayer End Game (1/2) |
| `a05cc81c3` | fix squirrel domain materials |
| `25d13421d` | fix hex race single player tracks |
| `f14566b62` | restore track specific crystal collision count |
| `9413f0cb1` | Add Intensity Wise Leaderboard |
| `3991d147e` | Create UGS.meta |
| `3dee030f8` | Fix Time difference bug |
| `99db23bec` | Import assets |
| `e79e2a9db` | Refactor Game-over Panel UI |
| `c212a4ce2` | Import fonts |
| `4eeea8a66` | Fix Multiplayer End Cinematic Screen View + Fix Multiplayer Hex Race EndGame Scene + Fixed Multiplayer Different intensity leaderboards |
| `8c2d673cd` | Update MinigameHexRaceMultiplayer.unity |
| `05f0e0f92` | Update CrystalCollisionTurnMonitor.cs |
| `5dcee2e24` | squirrel trail tuning |
| `800c092c9` | Enable Multiplayer Joust End Game CInematic, Player Score Cards, Scoreboard |
| `764ce263d` | Update MultiplayerHexRacePlayerStatsProfile.cs |
| `e277abffa` | Refactor Minigame HUD View to support Multiplayer HUD Views + Add HexRace HUD + Add Joust HUD |
| `ad3b27484` | Update MinigameJoust_Gameplay.unity |
| `e1d93e845` | Update Squirrel.prefab |
| `e386e4732` | Add editor window to Leaderboard Config SO |
| `c8e8f302c` | Update MinigameJoust_Gameplay.unity |
| `013ab4e24` | Add MinigameTournamentMultuplayer scene |
| `b950380e2` | Change crystal prefab |
| `1582f10fb` | update omnicrystal look including explosion effect |
| `3d655c4f9` | Update BaseScoreTracker.cs |
| `a5abe2ec4` | Update MinigameWildlifeBlitz.unity |
| `6bc1a6d55` | Update OrganicRematchGames.asset |
| `80edf38fd` | Update ArcadeGameBlockBandit.asset |
| `652ffbbe6` | make progress toward consistent team crystals visuals |
| `87edca5c3` | Update MinigameHexRaceMultiplayer.unity |
| `54f8d18d7` | Use OnCellItemsUpdated instead of OnCrystalSpawned |
| `a7a139194` | Update RandomLifeSpawner.cs |
| `c5b123a80` | Update BlobCell.asset |
| `1431c9dee` | Update MinigameWildlifeBlitz.unity |
| `05a8c9e0f` | Update MinigameFreestyle.unity |
| `09a6de1e6` | Update GameDataSO.cs |
| `47b760caa` | tweak the colors |
| `74e484bd3` | fix colors and prisms of hexrace tracks |
| `a1a45f223` | Fix Joust End Game + Fix Joust Scoreboard + Fix Joust Score update |
| `591945c7b` | clean up waypoint code in crystal collision tun monitor |
| `f308b1e5e` | Update BlobCell.asset |
| `1c5a3effe` | Update Menu_Main.unity |
| `709a70522` | Use GetCrystalCollisionCount and remove event |
| `28e540a96` | Add Crystal Capture EndGameCinematic, Player Cards, Scoreboard + Add Cinematic Defination + Update UGS Stats Manager |
| `798e84d3f` | Add intensity scaling to joust and crystal capture with a spawnable gyroid and hopf |
| `7c68095a2` | make theme manager respect color set danger color |
| `4f810c918` | update intensity levels of joust and crystal capture |
| `a18aba9cd` | tone down danger color |
| `abaee8799` | use spawn initialization on crystal capture |
| `ebcbda795` | set joust spawner to initialize on start |
| `2f51e2363` | refactor pause menu screen |
| `c36650fbe` | Update MinigameJoust_Gameplay.unity |
| `a9ba3bfb0` | Update MinigameCrystalCaptureMultiplayer_Gameplay.unity |
| `cf8a7b64f` | Update MinigameHexRaceMultiplayer.unity |
| `c4a3c33b0` | Implement Reset for Replay for all players |
| `492738d8c` | Implement custom reset in Joust |
| `75fb257f9` | Add EventOnMiniGameTurnEnd event |
| `97430f66c` | Update MultiplayerCrystalCaptureController.cs |
| `14c13f3fa` | Cleanup Joust Controller |
| `724fe6640` | Move Reset Cards to Multiplayer HUD Base Class |
| `e04d44c7d` | Update Squirrel UI |
| `6c0c3175f` | Update Squirrel.prefab |
| `42fe520d4` | Refactor AOE Explosion for Reset Game |
| `d8a84b47a` | Refactor Segment spawner to increase efficiency |
| `c31d98961` | Update Squirrel Prism.prefab |
| `93b87ca7e` | Update Play Again Button |
| `66937a2fd` | Update HUD Scripts to properly cleanup UI Cards |
| `a92567557` | Refactor Prism Pool Manager Script |
| `71b732334` | Add GamedataSO to Vessel Prism Pool |
| `36925fd71` | Remove Event Listener from respective Prisms |
| `80de5e209` | Update Ring Spawners |
| `06c517c96` | Update AOEBlockCreation.cs |
| `221ba9144` | Fix Game Reset on All Multiplayer Game Modes |
| `2bcc7b99e` | Update MinigameJoust_Gameplay.unity |
| `e6b7a03eb` | Update MinigameHexRaceMultiplayer.unity |
| `8b1b3e99b` | Update MinigameCrystalCaptureMultiplayer_Gameplay.unity |
| `7a0b3381d` | Fix Joust Scoreboard and Endgame Data |
| `4e1fd70ad` | Refactor cell SOs, update prefabs & ships |
| `5e6a535c9` | eneable testing more vessels in hex race |
| `f766e1d53` | Tune squirrel AOE spawners to avoid high speed drift collision |
| `dedbeff43` | Refactor Cell System by removing CellControlManager |
| `4eb80b441` | Fix Hex Race End Game Issue |
| `f1445d431` | Update MultiplayerHexRaceScoreboard.cs |
| `e6be93296` | Fix Ready Button Issue |
| `96a1b1722` | Update Game Canvas with new UI |
| `0f9c5b493` | Update MinigameHexRace.unity |
| `db74c3807` | Update MinigameWildlifeBlitz.unity |
| `17267c6f1` | Update MinigameCrystalCaptureMultiplayer_Gameplay.unity |
| `0082f1487` | Update MinigameHexRaceMultiplayer.unity |
| `158aaf7c3` | Update MinigameJoust_Gameplay.unity |
| `b4859b275` | Add Stats Provider Rework + Add IStatExposable |
| `768f0e965` | Add IStatExposable Members |
| `7d52f3784` | Create Stats SO Asset Modules |
| `20172acc7` | Update MinigameHexRaceMultiplayer.unity |
| `e76d8d355` | Update MinigameHexRace.unity |
| `a02f1f1d0` | Refactor Mini Game HUD to support Player Cards for AI |
| `4a1c10eb7` | Edit Assets |
| `a0481b37d` | Update MinigameHexRace.unity |
| `dad1b4ab7` | Update Goodies.prefab |
| `7cb987296` | Fix explosions by respecting projectile velocity and unifying inertia and momentum |
| `ff431eddd` | Update DailyChallengeModal.cs |
| `5209b6057` | Update ControllerButtonPress.cs |
| `827ec014b` | Add reference of CellData to Crystal Prefabs |
| `a09f4bb79` | Update MultiplayerMiniGameControllerBase.cs |
| `7c77f118e` | Update GenericEventChannelWithReturnSO.cs |
| `66be74377` | Update MinigameHexRaceMultiplayer.unity |
| `a5140d63b` | Standardize event asset names and update drift logic |
| `4e17e6a9f` | Update SO_ColorSet.cs |
| `d62f8f5e0` | Enable Squirrel 2D Trail |
| `78e14d68a` | Update SkimmerOverchargeCollectPrismEffectSO.cs |
| `b087e1cc4` | Update SO_ColorSet.cs |
| `22af096dd` | Replace intensity parameter scaling with distinct level configs for Hopf and Gyroid spawners |
| `d2df34193` | Add 6 new spawnable structures and intensity routing for Joust/Crystal Capture |
| `6a6f62a09` | Centralize player/vessel tracking in GameData |
| `3dadd8f53` | Update NetworkManager.prefab |
| `da8adece2` | More crystals in crystal capture |
| `6ee01c9a3` | Add 6 new spawnable structures and intensity routing for Joust/Crystal Capture |
| `08d03c5f8` | add meta files |
| `3dbfa7c5f` | add material files |
| `6ace50168` | Add spawnable meta files and add to scene |
| `65ea946e7` | Fix Main Menu Mouse Hover Issue |
| `2e82e12f5` | make level tweaks |
| `35cba0577` | Update NetworkManager.prefab |
| `558199f88` | update environments |
| `2a5ca1f76` | Import Sprites |
| `0221bec6e` | Create Initial Party Manager |
| `0c282a484` | Create Online Player Entry |
| `b8c351697` | Update Menu_Main.unity |
| `aeeeb9029` | Fix Minigame End Game Race Condition |
| `2d1f77c9d` | Update Game Canvas to fix Player Name Size |
| `16b2bcbf2` | Replace Stats Prefab |
| `4b6ed7ef8` | Fix Crystal Capture End Game Winner Determination |
| `b15ccf992` | Update Joust End game stats + Fix High Speed Client Joust Registration |
| `b2cab8973` | Restore elemental comeback system for minigames |
| `27ca38206` | Clean up Crystal Collision Turn Monitor Class |
| `5814a53f6` | Make Crystal Collision Turn Monitor inherit intensity values |
| `8649fb450` | Reset Crystal Spawn positions during reset game |
| `755480668` | Remove deprecated startup prefab and script |
| `524b0b79d` | Imported Assets |
| `e7d1d8b88` | Update GameCanvas.prefab |
| `9ece1b25e` | Update Invitation Feature |
| `32f96d8d8` | Update MinigameJoust_Gameplay.unity |
| `ccaa121ca` | Update MinigameHexRaceMultiplayer.unity |
| `f5add0ba1` | Update MinigameCrystalCaptureMultiplayer_Gameplay.unity |
| `f8a1188fb` | Add SceneLoader system with network support |
| `5f45777d1` | Add empty BootstrapController MonoBehaviour |
| `92951b82f` | Update MinigameHexRaceMultiplayer.unity |
| `a978dcb1e` | Update MinigameCrystalCaptureMultiplayer_Gameplay.unity |
| `eaf45f2fd` | Add AuthenticationFacade & Scriptable AuthData |
| `cb39d201f` | take jade out of schwarz |
| `96264b611` | update jet effects |
| `92b34cd42` | Add instance NetworkMonitor + ScriptableData |
| `3383ca4af` | Add Bootstrap - Dont Destroy On Load prefab |
| `2784ef43f` | Add Bootstrap scene and register in build |
| `8391cbf5a` | set things up for testing |
| `1662ed62f` | Fix comeback system to use CrystalsCollected instead of Score |
| `734674530` | Add Reflex Dependency Injection package |
| `a49638157` | Remove UnityServicesUIHandler and VContainer entry |
| `e0fae6fd6` | set elements |
| `897377c8a` | Rework hex race track intensities with spline support and new intensity 4 |
| `57e3ab086` | Add missing sounds across UI and gameplay systems |
| `a84d891d9` | Add sounds to gun fire, boost, explosions, and creature death |
| `fccbc6a85` | Add sounds to UI elements: cards, toggles, rewards, and profile icons |
| `2e0ae8dff` | Add squirrel experience audio juice: drift, energy gain, speed burst |
| `16600e42d` | Unify multiplayer game modes to support solo play with AI |
| `d81dabb27` | Make AI opponent ship and behavior configurable at runtime |
| `ef18f9aad` | Add synthesized AudioClip assets for squirrel SFX and wire them in prefab |
| `91292bff6` | Update AuthenticationServiceFacade.cs |
| `afe75f2ed` | Use ScriptableEvent in GameData and update refs |
| `29748b479` | configure available AI ships |
| `f7f80d046` | Move App scripts to Systems; update Arcade |
| `b6d66e771` | Refactor namespaces: App.Systems -> Systems |
| `503cfd42b` | Add OnSessionEnded and use injected GameDataSO |
| `b7f441c7c` | Remove legacy Arcade prefab and related scripts |
| `ba27343bd` | Update AppManager.prefab |
| `abbc2f86d` | Update MultiplayerMiniGameControllerBase.cs |
| `4123a7b24` | Update MultiplayerSetup.cs |
| `9644443bd` | Update GameDataSO.cs |
| `02ade971a` | Update Bootstrap.unity |
| `a13988f2a` | Rename 'D I' resources folder to 'Reflex' |
| `d0cbe5c94` | Remove Reflex folder .meta |
| `22531be96` | Update GameDataSO.cs |
| `448bd243b` | Update DefaultNetworkPrefabs.asset |
| `0e89502d3` | Update NetworkManager.prefab |
| `80cbc2f53` | Update AppManager.cs |
| `02e035505` | Update SceneLoader.cs |
| `d9b1ea655` | Update Bootstrap.unity |
| `fcde038bf` | Fix camera position not resetting when starting a new game session |
| `6a093ea63` | Wire all 15 missing gameplay SFX clips in AudioSystem prefab |
| `cd4bbbde7` | Fix scoreboard and replay flow for solo-with-AI mode |
| `1981d50fc` | Fix Joust Endgame Calculation |
| `f8621a5b1` | Update GameCanvas.prefab |
| `6eeeb7d68` | Snap player camera at all game transition points |
| `c9c9e6f50` | Remove legacy singleplayer HexRace files and drop 'Multiplayer' prefix |
| `3358a0030` | Spawn MaxPlayers-1 AI opponents in solo mode instead of hardcoded 1 |
| `e97862f16` | Fix AI targeting to only pursue own-domain crystals |
| `83ef3091a` | Fix stale data causing camera/initialization failures on multiplayer re-entry |
| `0c3d7c8aa` | Fix AI spawn overlap in Joust by adding third spawn point and fixing index bounds |
| `22640d3f4` | Update GameCanvas.prefab |
| `81722794f` | Update Squirrel.prefab |
| `afe046cbf` | Update Goodies.prefab |
| `97c2f0b4e` | Change Universal Stats Provider to EventDrivenStatsProvider |
| `8d7633373` | Add VesselStats SO |
| `16441ce0c` | Update MinigameJoust_Gameplay.unity |
| `c314a6942` | Update MinigameHexRace.unity |
| `74bfce35f` | Update MinigameCrystalCaptureMultiplayer_Gameplay.unity |
| `69b75ddb8` | Expand cinematic toast to separate victory/defeat string arrays |
| `7afc299ca` | Update MinigameCrystalCaptureCinematicDefinition_Multiplayer.asset |
| `a68dca71f` | Update MinigameHexRaceCinematicDefinition.asset |
| `304bac3c2` | Update MinigameJoustCinematicDefinition_Multiplayer.asset |
| `afb1e08dd` | Update GameCanvas.prefab |
| `aa76017ee` | Add in-game notification feed system for GameCanvas |
| `653d76703` | Update GameCanvas.prefab |
| `29a9bc247` | Update MinigameCrystalCaptureMultiplayer_Gameplay.unity |
| `e7788d4de` | Update MinigameHexRace.unity |
| `be11c2056` | Update MinigameJoust_Gameplay.unity |
| `d0dd7d5b2` | [FIX] Use LocalPlayer Name for Game Feed |
| `09d2bc245` | Update ElementalComebackSystem.cs |
| `e18ffc90d` | Update MultiplayerSetup.cs |
| `90bdcab37` | Update GameDataSO.cs |
| `7349a2da3` | Refactor GameSetting and AudioSystem to use Reflex DI instead of singleton access |
| `aad5a2d75` | Improve loading screen with animated text, pre-game cinematic, and early track spawning |
| `4a19b2e08` | Wire up loading screen animations and cinematic in prefabs with auto-bootstrap |
| `c4b231b46` | Fix cinematic camera not playing and skip button not appearing |
| `5c21d6152` | Fix connecting panel text, VesselHUD visibility, and GameFeed spacing |
| `1c8aa0199` | begin building new manta |
| `df0924850` | Add comprehensive prism system performance audit |
| `7c22ee4cf` | Fix Dolphin crystal explosions using wrong resource and wrong effect order |
| `0105fc117` | Fix game feed text overlap by ensuring VerticalLayoutGroup on pre-assigned containers |
| `7dcfc8848` | Add authentication scene flow, arcade profile widget, and avatar ID in multiplayer HUD |
| `3a8f86981` | Fix conic explosion positioning and persistent rendering |
| `8d1a29432` | Fix 3-player domain assignment and scoreboard crashes |
| `d55e2a687` | fix squirrel hitting own rings |
| `f76b848f8` | Fix widescreen UI: canvas scaling, screen switcher, and aspect ratio support |
| `41ff79ba6` | Refactor: unify spawner, positioning scheme, and spawnables into SpawnableBase |
| `b342e2618` | Fix HexRace track not showing for late-joining clients |
| `a5f889fa3` | Set up Scenes |
| `a793c3c48` | Add meta files |
| `85c9be540` | Remove email login/register, fix profile data null-refs and validation |
| `39d684cd9` | Update dependencies |
| `25d2e5b11` | Fix profile display, name sync, and avatar support across all HUDs |
| `050ecac92` | Update dependencies |
| `aeea10e61` | Fix Username Panel Position |
| `8e2ea23e8` | Persist PlayerDataService across scenes and wire profileIconList to all game modes |
| `4e9b1939c` | Fix CS1061: rename intenstyLevel → intensityLevel and make it public |
| `a6965fbb4` | Add debug logs across player name/avatar data pipeline |
| `3362c4d0a` | Add procedural HyperSea skybox shader |
| `4e5b06437` | Fix player name/avatar not updating in multiplayer by adding GameDataSO fallback |
| `a8b5af408` | Remove legacy PositioningScheme enum and switch from SegmentSpawner |
| `74c8b5e82` | Update Menu to fix scroll view |
| `448e7990e` | Update Scene to adjust Scoreboard (Crystal capture) |
| `39f6c21ca` | Update Scene to adjust Scoreboard (Joust) |
| `e935cd2c9` | add meta files |
| `8b201c7a3` | Fix Hex Race crystals all blue + add AI profile system for score cards |
| `3718008fe` | Fix SpawnableCrystal not applying domain color to spawned crystals |
| `8014e5cc9` | Add AI Profile List |
| `cc02a143d` | Update MinigameCrystalCaptureMultiplayer_Gameplay.unity |
| `6d4bbef21` | Update MinigameHexRace.unity |
| `12af92b5b` | Update MinigameJoust_Gameplay.unity |
| `200a8ca09` | Fix DomainAssigner not initialized in solo-with-AI mode |
| `ef49f0356` | Add XP tracking system, Episode Screen, and IAP stub |
| `d2f1deba0` | Upload supported changes for claude |
| `a4cfb899c` | Rework XP track into ProfileScreen and simplify EpisodeScreen |
| `6970eefba` | add using statement for trail in segment spawner |
| `673edaa06` | Add segment layout fields back to SegmentSpawner |
| `119c26391` | Fix orientation issue in Joust/CrystalCapture and add ConcentricLayersGenerator |
| `d862ad051` | add nesting to level 2 crystal capture |
| `a3f7348b2` | Fix 3-player domain assignment and scoreboard crashes |
| `c8f57d491` | Add authentication scene flow, arcade profile widget, and avatar ID in multiplayer HUD |
| `5dc94f2b5` | Set up Scenes |
| `f3f018df7` | Add meta files |
| `922fa911f` | Remove email login/register, fix profile data null-refs and validation |
| `c202a0245` | Update dependencies |
| `b2d19b0cc` | Fix profile display, name sync, and avatar support across all HUDs |
| `644bcda6f` | Update dependencies |
| `4b1ea35da` | Fix Username Panel Position |
| `90f371168` | Persist PlayerDataService across scenes and wire profileIconList to all game modes |
| `c137c7eec` | Add debug logs across player name/avatar data pipeline |
| `be81b875c` | Fix HexRace track not showing for late-joining clients |
| `1f4446628` | Fix player name/avatar not updating in multiplayer by adding GameDataSO fallback |
| `32b1c188a` | Update Menu to fix scroll view |
| `d6e4c69a2` | Update Scene to adjust Scoreboard (Crystal capture) |
| `6ece97bb5` | Update Scene to adjust Scoreboard (Joust) |
| `beda407ab` | Fix Hex Race crystals all blue + add AI profile system for score cards |
| `8215b2230` | Fix SpawnableCrystal not applying domain color to spawned crystals |
| `0fe9a1e25` | Add AI Profile List |
| `c17b24817` | Update MinigameCrystalCaptureMultiplayer_Gameplay.unity |
| `e3e1e5b40` | Update MinigameHexRace.unity |
| `56730861a` | Update MinigameJoust_Gameplay.unity |
| `803af8062` | Fix DomainAssigner not initialized in solo-with-AI mode |
| `3c14c7e50` | Optimize prism VFX: fix material leaks, add Jobs-based effects manager |
| `e20edf852` | Enable GPU instancing on all prism and VFX materials |
| `529d04c63` | Add ECS component definitions and replace coroutines with centralized timers |
| `92c099372` | Create XP Data |
| `b8a8e269d` | Create Episode Data |
| `4ca8ff605` | Update Menu_Main.unity |
| `f17bf08a9` | Fix XP tracking, revert Port screen, move Episodes to profile panel |
| `66d600bab` | Remove DontDestroyOnLoad from XPRewardService and IAPManager |
| `0d568cb20` | Add Claude Support Change |
| `532fcc4e8` | Fix XP not tracking and auth reference loss after game scenes |
| `25746f79a` | Fix XP always showing 0 — ensure CurrentProfile is never null |
| `ece2dd144` | Fix PlayerDataService destroyed on scene transition — detach from parent before DDOL |
| `4eef01653` | Extract ProfileScreen from PlayerDataService (SRP) |
| `59ebaf886` | Complete XP Change |
| `91cda6c51` | Paint by number game mode |
| `636ba0cf0` | Fix multiplayer avatar sync on score cards and HexRace scoreboard ordering |
| `1a8ab1767` | Revamp freestyle mode: lobby flow, shape painting with scoring and ghost shape |
| `7453a5687` | Audit & refactor UGS integration: disable PlayFab, centralize init, add debouncing |
| `c29e834b0` | Wire scene references, auto-create LineRenderers, add Circle shape asset |
| `f19068a15` | Strip aggregate stats from cloud save, add UGS constants, fix merging & analytics |
| `bc3730c14` | Fix runtime errors and improve freestyle mode UX |
| `3b06381b2` | Add null guard on ToggleConnectingPanel for safety |
| `45c70d04a` | Add per-vessel telemetry system with UGS Cloud Save upload |
| `06bcb7993` | Boost accelerometer domain color gradient, fix HUD visibility and activation |
| `96fc895be` | Fix prisms stuck in partially exploded state |
| `20b498c8f` | Wire VesselTelemetryBootstrapper on 5 vessel prefabs, add R_ShipActions static events |
| `4d9f50c4e` | Fix boost HUD not updating and stolen domain color not persisting |
| `2daddb33d` | Add centralized CSDebug logger with runtime log level controls |
| `f1e2c1524` | Update Squirrel.prefab |
| `576df4345` | Fix Sparrow telemetry: attach to prefab with SO refs (like Squirrel), add debug logging |
| `ac027b311` | Cap explosion VFX per frame and eliminate EventListenerBase GC allocs |
| `5ef324e77` | Add FrogletTools editor menu for log level control |
| `132bfe5cc` | Fix Sparrow telemetry event sources: use correct executors for prism blocks and danger blocks |
| `172b176db` | Restore cell membrane and move HyperSea effect to scene skybox only |
| `fcb46a231` | Replace Physics-based AOE prism damage with Burst-compiled spatial query |
| `58e52bb6f` | Remove redundant VesselTelemetryBootstrapper from Sparrow prefab |
| `1de2784ee` | Add Log Control editor window for granular log type management |
| `5c47a9de9` | Add scene shortcuts to the FrogletTools Log Control window |
| `f3280cf50` | Update ElementalCrystalsCollectedBlitzScoring.cs |
| `4a20b64a2` | Add Meta files |
| `43b23552a` | Add meta files |
| `a811515c8` | Fix in-game HUD cutoff on widescreen: use height-match scaling |
| `40dea0900` | Add CLAUDE.md with project conventions and development guidelines |
| `6bd9368f3` | Add ConnectingPanel sprite randomizer, fix track/crystal early spawning |
| `c28ef155a` | Guard AIPilot.UpdateCellContent against null vessel reference |
| `cf02debd7` | Fix NullRefs on scene transition and ensure AI picks up early crystals |
| `9c436e466` | Fix crystal spawning: use OnClientReady instead of OnMiniGameRoundStarted |
| `1fe5e2406` | Fix destroyed-Player access in OnDisable, fix concurrent sign-in race |
| `2daa042d8` | Add Connecting Panel |
| `6f6f72705` | Create Connecting Panel Data Asset |
| `dabcaea34` | Update MainAIProfileList.asset |
| `efd4f5f84` | Add meta files |
| `620b94020` | Update Menu_Main.unity |
| `d9e75c2c3` | Update MinigameCrystalCaptureMultiplayer_Gameplay.unity |
| `c68f11548` | Update MinigameHexRace.unity |
| `534e8cc72` | Assign HyperSea procedural skybox to all gameplay scenes |
| `2ab48efb5` | Tune HyperSea skybox: balanced nebulae, blue-white galactic plane, reduced bloom |
| `ea3d6b63f` | Shrink galactic core, space out nebulae, add sharp filament structures |
| `06bc3acef` | HUDF-style stars, core graded into plane, remove filaments |
| `192aa9ee2` | Add Andromeda galaxy, domain-warped nebulae, directional sky variation |
| `05a08ce08` | Fix Andromeda spiral, all-structure star field, nebula depth variation |
| `7192159d2` | Bake Andromeda to texture, wispy nebula edges, 5x bigger galaxies |
| `361733d8e` | Bridge membrane and skybox aesthetics with cellular overlay, atmosphere haze, and blue-locked palette |
| `fcd2fdd12` | Fix Merge Conflict (Not clean) |
| `eb71f0f3c` | tweak the skybox material |
| `6d9edd20e` | Fix console errors: replace Singleton.Instance with [Inject] DI, add null guards on ScriptableEvents |
| `a8f6fdb45` | Remove HyperSeaSkyboxController — inline Andromeda, scene Lighting handles the rest |
| `aba138c28` | Hot/cold split PrismAOEData with bit-packed flags for cache line packing |
| `49b763146` | Remove redundant null guards on mandatory GameDataSO events |
| `c6d3f4ea3` | Remove all if-null guards on ScriptableEvent SerializeFields — let missing refs fail loud |
| `7fe7c6235` | Fix widescreen HUD: right-justify abilities, overlap silhouette on trail, fix menu arcade button |
| `4d4f3dda9` | Refactor PlayerDataService from singleton to Reflex DI injection |
| `37ec4035f` | Fix conic explosion orientation so cone tip is at vessel, opening forward |
| `4c15ed7db` | Fix AOE explosions stuck at max scale and prism disappearing bugs |
| `e867f67a2` | Refactor UGSStatsManager from singleton to Reflex DI injection |
| `27260338e` | Fix coneContainer self-reference causing explosions at world origin |
| `64701d81f` | Update LocalCrystalManager.cs |
| `ff2cb8198` | Update NetworkCrystalManager.cs |
| `3d350f4d5` | Update PurchaseGameCard.cs |
| `29582261e` | Update GameCard.cs |
| `fea30d97a` | Update .gitignore |
| `dc2edd3a4` | tweaks |
| `ba3ca2fa2` | Expose star probability magic numbers as shader properties |
| `e69f3c3c1` | Add industry-standard Bootstrap scene infrastructure |
| `92dc0c075` | tweak material |
| `88328f360` | Add Unity .meta files for Bootstrap scripts |
| `4a9356859` | Add Authentication scene flow and unified SceneTransitionManager |
| `861dd0685` | Fix hierarchical spawning: null-safe PrismScale and child scale normalization |
| `a18b08e06` | Wire Bootstrap → Authentication → Menu_Main scene flow |
| `f98c2a6b1` | add meta files and explosion tuning |
| `4c02bfe48` | Add SOAP-based host connection and party invite system |
| `de50d72d2` | Add performance benchmark tool for before/after measurement |
| `a72882412` | Add SOAP architecture instructions to CLAUDE.md |
| `302367e87` | Add unit tests for Bootstrap scene system |
| `a058472e1` | Refactor Lifeforms system for SOLID compliance |
| `03855cb28` | Integrate performance benchmark with SOAP architecture |
| `f853df002` | Standardize SOAP CreateAssetMenu paths under ScriptableObjects/SOAP hierarchy |
| `d4c230c35` | Organize project structure: move files, add namespaces, create assembly definitions |
| `75b1e3110` | Fix SpawnableFlora assembly errors: add missing namespace and using directive |
| `16efed0d0` | Add PartyGameLauncher for Multiplayer Freestyle mode launch from lobby |
| `0b73d9b07` | Fix Flora type error in SO_FloraCollection by adding missing using directive |
| `7aa506528` | Add unit tests for performance benchmark tool |
| `d99b8e79b` | Integrate benchmark tool with project assembly structure |
| `d9b876c61` | Add missing using CosmicShore.Models.Enums across 169 files |
| `be697a529` | Fix crystal spawning not resetting to anchor 0 on replay/restart |
| `f62a15c20` | Add Unity.Netcode.Runtime and Unity.Collections references to CosmicShore.Core.asmdef |
| `e83f0642c` | Fix RoundStats properties returning stale/default values on clients |
| `325ec0c1c` | Add scene flow integration tests for Bootstrap → Authentication → Menu_Main |
| `afd581e2b` | Revamp freestyle mode: camera pan reveal, trail control, sign facing |
| `e2b9146f8` | Fix NullReferenceException in AIPilot.UpdateCellContent when cell is not yet assigned |
| `4bae838b1` | Signs toggle pre-placed GOs, trails off at spawn, debug screenshot + scoring log |
| `295d222b8` | Optimize explosions: batch damage per frame, O(1) VFX tracking, fix freestyle teardown |
| `f75cfb50c` | Fix shape orientation, add preview cinematic, fix Input System + accuracy + ghost line |
| `ac18821a1` | Fix VesselStatus access, countdown-gated drawing, player spawn, SnowChanger |
| `324829790` | Fix Netcode namespace errors by adding missing assembly references to CosmicShore.Utility.asmdef |
| `045669237` | Add missing using for CosmicShore.Models.Enums in IRoundStats and RoundStats |
| `01839dd99` | Add missing assembly references to CosmicShore.Utility.asmdef |
| `85ca5f38c` | Fix username not saving and randomize default player names |
| `f4a7aa4c9` | Remove # from default player name format |
| `05ce5f549` | Add missing assembly references to CosmicShore.Utility.asmdef |
| `5266a63cd` | Add missing asmdef references to fix type/namespace compilation errors |
| `fffeb17aa` | Update script namespaces to match folder structure |
| `f111e882b` | Fix broken using directives after namespace refactoring |
| `ce943ab72` | Fix CS0234 and CS0118 compilation errors |
| `d67c1fa64` | Revert PrismEffectsManager HashSet conversion — keep Lists, add only teardown fix |
| `dcd0de630` | Fix DataAccessor namespace corruption and add missing using directives |
| `359cf5036` | Fix CS0118 and CS0234/CS0246 compilation errors |
| `a03d1bb50` | Fix CS1061: Replace DOFillAmount with DOTween.To in BoostFillAnimator |
| `0e8cc9307` | Fix static blue prism bug: remove _instanceDestroyed pattern |
| `736ce985f` | Fix CS1513/CS1022/CS0234/CS0246 compilation errors across 7 files |
| `c7e4b44a7` | Fix CS0234/CS0101 compilation errors: remove invalid namespace references |
| `613a65c5d` | Add missing .meta files for ScriptablePartyData and HostConnectionDataSO |
| `77dd2a3b6` | Remove all custom CosmicShore assembly definitions and references |
| `87a9b17ef` | Flatten all C# namespaces to max 3 levels (CosmicShore.X.Y) |
| `8a53cf06c` | fix: resolve 20+ compilation errors from stale namespace references |
| `0b4551e9e` | Add Unity .meta files for new scripts |
| `ef09617e5` | fix: add missing using directives to FTUE handlers and AIPilot |
| `b100830ab` | fix: add missing using directives and resolve namespace conflicts |
| `5664611f2` | fix: add missing using statements to resolve CS0246 compilation errors |
| `57b5c0049` | fix: add missing using directives and test asmdef to resolve compilation errors |
| `5ba5b9471` | fix: add missing using statements across 179 scripts |
| `37ad69edd` | fix(multiplayer): fix 5 bugs in scene transition and party invitation flow |
| `dd2ed970f` | refactor: rename all Ship-named files and folders to Vessel |
| `201d4d3d7` | fix: resolve all type and namespace errors across 83 scripts |
| `c97d2cf48` | refactor: reorganize namespaces and folder structure |
| `6a589a8ad` | fix(namespaces): fix all type/namespace errors after app-shell-polish merge |
| `c87096a82` | fix(namespaces): add missing using directives to resolve CS0246 compilation errors across 30 files |
| `f2b45d86b` | fix(namespaces): resolve compilation errors from namespace consolidation |
| `4f1a14684` | fix(GameManager): add missing using directive for CosmicShore.Core |
| `285aaebf1` | fix(editor): resolve compilation errors from wrong namespace imports |
| `0c83c4742` | test(core): add comprehensive unit test suite across 9 systems |
| `ea4993f35` | Add Unity .meta files for Assets/_Scripts |
| `1328b8747` | test(core): add 7 more test suites covering critical game systems |
| `d19d54dad` | fix(tests): remove EditMode test asmdef to fix compilation errors |
| `55545b7ea` | fix(tests): remove test asmdef files that cannot reference default assembly |
| `02a072bb7` | Add Unity .meta files for test scripts |
| `9ca00c22a` | fix(tests): resolve compilation errors in test files |
| `156f7b9ff` | Optimize benchmark runner hot path for zero-allocation frame capture |
| `a067717a4` | fix(ui): remove invalid PlayerDataService.Instance references |
| `6761b8599` | fix(di): guard against null Reflex DI registrations and uninjected fields |
| `b0bdc8428` | fix(di): resolve GameDataSO null ref from Reflex injection timing |
| `a7bcd6871` | fix(bootstrap): guard against null refs and double-disposal during bootstrap scene load |
| `b1aa77d03` | fix(bootstrap): prevent Bootstrap scene from being dirtied after play mode |
| `e314ac9c2` | Update Bootstrap.unity |
| `dd15edca8` | fix(auth): remove AuthenticationController from AppManager, use SOAP AuthenticationDataVariable |
| `3cadef366` | fix(bootstrap): wire up missing Persistent Systems references on AppManager |
| `7df49ac9d` | fix: resolve all C# compiler warnings across 29 files |
| `3fad1cfd3` | fix: remove unused private fields causing CS0414 and CS0169 warnings |
| `5e5213ffc` | fix(arcade): fix HexRaceScoreTracker OnDestroy access modifier and base call |
| `8232da06d` | fix: remove unused private fields causing CS0414 warnings |
| `434284e23` | fix(auth): auto-create missing persistent services and harden auth flow |
| `1ef3afca6` | fix(editor): suppress false scene dirty state from OnValidate noise |
| `d67aade8d` | fix(auth): use DI-only resolution, serialize prefab fields, fix timing |
| `ae6427fe4` | fix(startup): resolve Play Mode prefab parenting and DI injection errors |
| `35bce2ecd` | fix(audio): fix NullReferenceException in AudioSystem.ChangeMusicLevel on auth scene |
| `3b7a9dd2d` | fix(app): resolve persistent systems from DontDestroyOnLoad before creating new ones |
| `242ca2830` | refactor(di): replace AudioSystem/GameSetting singletons with Reflex DI |
| `5ba9aeb9b` | fix(audio): resolve DI timing issue for AudioSystem and Jukebox in Play Mode |
| `cd8a56139` | fix(auth): resolve auth scene stuck on loading by fixing async deadlock |
| `1e091c850` | refactor(bootstrap): move DependencySpawner responsibilities to AppManager DI |
| `ffad3965f` | refactor(bootstrap): remove DependencySpawner from game scenes |
| `fe8015074` | chore: delete DependencySpawner prefab and script |
| `7ad808573` | refactor(bootstrap): move services from Menu_Main to Bootstrap DI |
| `fc426746c` | feat(bootstrap): add CaptainManager and IAPManager GOs to Bootstrap scene |
| `1ee4d3825` | refactor(bootstrap): register systems in AppManager, remove from Menu_Main |
| `bb1e07226` | Update Menu_Main.unity |
| `a52666355` | Update Menu_Main.unity |
| `83ba52af4` | fix(ui): move ShipSelectionSlot to own file to fix ExtensionOfNativeClass reload error |
| `1d98fe687` | feat(auth): start Netcode host after authentication before entering Main Menu |
| `09885bea2` | refactor(multiplayer): host-once architecture — start host from MultiplayerSetup after auth |
| `29506daf9` | feat(menu): rewrite MainMenuPlayerSpawnerAdapter for network host player |
| `a24469aa9` | refactor(menu): use ServerPlayerVesselInitializer pattern for Menu_Main |
| `508fe7878` | fix(ui): remove duplicate audioSystem field in modal subclasses |
| `5e901e520` | Create MainMenuServerVesselInitializer.cs.meta |
| `48743a59c` | Create ShipSelectionSlot.cs.meta |
| `71e076064` | fix(ui): resolve NullReferenceExceptions in CallToActionSystem and PortSquadView Start() |
| `f0e3357f1` | refactor(scene): replace Player and Ship Spawner prefab with multiplayer spawning on Game object |
| `6003646a0` | fix(bootstrap): prevent SetParent error on prefab asset in EnsureService |
| `16c4a84c2` | fix(di): remove redundant DI registration for singleton gameplay managers |
| `359db1934` | Add SO event to return shape prisms to pool on exit, increase shapeScale to 7 |
| `33e123ca7` | Add EndShapeDetailHUD, fix sign positions, wire end-of-shape flow |
| `32fe62ab6` | Lock sign positions, remove all VesselPrismController calls, add start event |
| `05b3b98cd` | Show/hide vesselHUD around shape mode lifecycle |
| `5f3d2d0a6` | Make shape signs billboard toward the player camera |
| `db93e69c2` | Stop shape sign rotation, don't freeze player on shape completion, add FreestyleSign |
| `71da0a617` | Change FreestyleSign to collider trigger matching ModeSelectTrigger pattern |
| `1f22ec86b` | Fix signs: collider triggers, billboard facing, and freestyle flow |
| `1416effb5` | Strip sign transform manipulation; fix freestyle spawning order |
| `f4e4ed84f` | Stop repositioning signs at runtime — use editor-placed transforms only |
| `aec9dd048` | Remove ShapeSignSpawner — signs are editor-placed, no spawner needed |
| `f66e5765f` | Strip spawn logic from ShapeSignSpawner — just enable/disable |
| `4ec581af4` | Delete ShapeSignSpawner — toggle signs parent GameObject directly |
| `a487af748` | Add .meta files |
| `3101baef0` | Add Events for Game Mode Started and End |
| `4a09c7a3b` | Updated Shape Assets |
| `42a59f5db` | Update MinigameFreestyle.unity |
| `f74f2838e` | Update Dolphin Prism.prefab |
| `733276471` | Update Manta Prism.prefab |
| `6bf80eb7d` | Update Rhino Prism.prefab |
| `986448d4e` | Update Serpent Prism.prefab |
| `32966ba00` | Update Sparrow Prism.prefab |
| `f83b14a58` | Update Squirrel Prism.prefab |
| `dbfae0bc7` | Give Squirrel New Jet Effects |
| `f3b503f2d` | fix(bootstrap): fix bugs and optimize bootstrap scene startup flow |
| `1969e839d` | refactor(auth): optimize authentication scene scripts for best practices |
| `6da4651dd` | fix(auth): fix UniTask.WhenAny return type in AuthenticationSceneController |
| `148accb9c` | refactor(ui): decouple menu screens and fix bugs in Menu_Main scene |
| `3ebb3da25` | refactor(multiplayer): deduplicate vessel initializer AI spawning |
| `3adacf446` | refactor(multiplayer): remove dead MainMenuServerVesselInitializer |
| `4dfc3760a` | fix(spawn): resolve player spawn warnings and Menu_Main initialization blocking |
| `3a5cf06ce` | Update AppInitializationModal.cs |
| `05e68c05e` | refactor(bootstrap): unify AppManager service registration as direct serialized references |
| `f360671ed` | refactor(bootstrap): replace scene references with Singleton Persistents prefab array |
| `d3f094e64` | perf(bootstrap): replace FindFirstObjectByType with TryGetComponent on spawned instances |
| `0d747ca43` | refactor(bootstrap): simplify AppManager to use direct SerializeFields |
| `f34f97e9d` | refactor(AppManager): replace prefab instantiation with direct scene references |
| `b10bf128c` | fix(AppManager): auto-add DontDestroyOnLoad component when missing |
| `ddec39edd` | fix(AppManager): resolve scene managers at runtime via FindAnyObjectByType |
| `b3b0d2a1b` | Update DefaultNetworkPrefabs.asset |
| `bca9e6cf0` | Update AppManager.cs |
| `3bd80dda8` | refactor(bootstrap): merge BootstrapController into AppManager |
| `b969eb365` | Add BootstrapConfig asset |
| `ea05c758a` | Add Unity .meta files for new scripts |
| `23e45c73b` | Update Bootstrap.unity |
| `30bbca9e8` | refactor(core): centralize scene names in SceneNameListSO with DI |
| `a222b09b2` | refactor(bootstrap): remove Persistent Root and Bootstrap Services from AppManager |
| `a7540a5b5` | chore(bootstrap): remove unused IBootstrapService interface |
| `39765c63e` | refactor(di): replace [SerializeField] GameDataSO with [Inject] across 35 files |
| `7b20bade6` | Update Bootstrap.unity |
| `2471f103c` | fix(di): use correct Reflex APIs for asset vs singleton registration |
| `d0974e586` | fix(di): add SceneContainerScopeBridge and fix OnEnable injection timing |
| `ed5e30d37` | fix(di): subscribe only in Start() to avoid double-sub risk |
| `66370a64b` | Update AppManager.prefab |
| `06e80570b` | refactor(scene): delete GameManager/NetworkGameManager, consolidate into SceneLoader |
| `427c867f3` | refactor(scene): delete GameManager.prefab, clean up AppManager.prefab reference |
| `e498c87c8` | Create SceneContainerScopeBridge.cs.meta |
| `44d42e189` | Update Bootstrap.unity |
| `4f33ff188` | Update AppManager.prefab |
| `da401c3f7` | Update Bootstrap.unity |
| `3fbc65025` | fix(di): repair broken RootScope reference in ReflexSettings |
| `f2cb97e78` | Add ContainerScope prefab and use in scenes |
| `0914266e7` | fix(di): correct ContainerScope GUID on AppManager and add scope to Menu_Main |
| `35d210220` | Update AppManager.prefab |
| `a29b5ba3a` | Update Bootstrap.unity |
| `475bb6b2f` | Update Bootstrap.unity |
| `cdbfdd79e` | Update AppManager.prefab |
| `6545d2eb7` | Update ReflexSettings.asset |
| `14856875f` | Add implementation plan for shape spawning refactor |
| `2152b16d0` | Refactor Hangar screen to grid-based vessel selection with detail view and unlock system |
| `16989a1af` | Refactor shape spawning: shapes are now spawnable 3D objects in freestyle |
| `327e97211` | feat(auth): start network host after authentication and load Menu_Main as networked scene |
| `0eaff432d` | refactor(auth): move NetworkManager lifecycle to persistent MultiplayerSetup |
| `8333e7ea5` | fix(auth): add LoginStatusText to AuthPanel and wire statusText field |
| `5c0d246f7` | feat(menu): add MenuServerPlayerVesselInitializer for autopilot vessel spawning |
| `3ba874852` | Replace XP system with game-mode quest progression chain |
| `3a4d1b3e0` | Fix missing using directive for VesselStatus in ShapeCollisionTrigger |
| `06197892a` | feat(menu): set Cinemachine menu camera to follow autopilot vessel |
| `c4c75355b` | Refactor SegmentSpawner: merge weight+spawnable, add guaranteed shapes list |
| `4db0d26ff` | refactor(menu): remove MainMenuPlayerSpawnerAdapter, consolidate into MenuServerPlayerVesselInitializer |
| `6673501f4` | Fix ambiguous Random reference in SegmentSpawner |
| `df1bf2a97` | Create MenuServerPlayerVesselInitializer.cs.meta |
| `6488d638b` | Update Menu_Main.unity |
| `1c2d6da49` | Update Bootstrap.unity |
| `bccc4ac6a` | fix(cta): guard against null CallToActionSystem.Instance in CallToActionTarget |
| `bff7c5932` | fix(camera): raise scene transition modal open event after menu camera setup |
| `8de30e342` | Fix 7 shape spawning and freestyle issues |
| `96c2f9b2e` | Update Menu_Main.unity |
| `96dbd6731` | Update Bootstrap.unity |
| `d6e540e3c` | Gradual prism spawning, domain change on shape collision, keep prisms after shape mode |
| `f1c5a9f5d` | refactor(camera): defer camera init and remove main menu camera logic |
| `aca9c2368` | fix(spawning): add Reflex DI injection for runtime-spawned vessels and replace VesselImpactor audio with SOAP event |
| `155191d14` | feat(friends): add UGS Friends service integration for friend invite system |
| `b87ab67ae` | feat(audio): create EventGameplaySFX SOAP asset and wire to prefabs |
| `902ae9705` | fix(projectiles): guard null audioSystem in AOEExplosion.Detonate |
| `adf2e0ede` | refactor(stats): decouple VesselCollisionReporter from StatsManager via SOAP event |
| `ad012fcaa` | refactor(bootstrap): remove ServiceLocator in favor of Reflex DI |
| `c16eb6b3c` | refactor(stats): revert StatsManager code-side SOAP subscription |
| `a889a90fe` | fix(menu): initialize player domain and name in Menu_Main after vessel spawn |
| `c1adb0c9f` | feat(lifecycle): add SOAP event channels for ApplicationLifecycleManager |
| `490b8d2b1` | fix(assets): wire EventOnSkimmerVesselCollision to collision effect SO assets |
| `e29e9aa0e` | Update Bootstrap.unity |
| `c664bef92` | refactor(stats): remove unused Obvious.Soap import from NetworkStatsManager |
| `8af756eca` | fix(spawning): add Reflex DI injection for AOEExplosion and replace audio with SOAP event |
| `783ef732d` | feat(lifecycle): create SOAP event assets and wire to AppManager prefab |
| `7cb3d9a8a` | feat(friends): add SO assets, .meta files, notification UIs, and PartyArcadeView integration |
| `a7981f66c` | refactor(audio): use EventListener SOAP pattern for AudioSystem SFX events |
| `dcc566213` | feat(core): add ApplicationStateMachine with SOAP state broadcasting |
| `fe2ffa412` | refactor(stats): merge StatsManager and NetworkStatsManager into unified StatsManager |
| `7ca72573f` | feat(benchmark): add history tracking, profiler counters, and beginner-friendly UI |
| `78c3b966f` | fix(prefabs): wire EventGameplaySFX asset on AOEExplosion prefabs |
| `eb38bc793` | refactor(prism): replace [Inject] AudioSystem with SOAP ScriptableEventGameplaySFX |
| `f86e7ee4a` | feat(core): create SO assets and wire ApplicationStateMachine to AppManager prefab |
| `078a3f875` | wire(prefabs): assign EventGameplaySFX asset to all 41 Prism prefabs |
| `566fa1f8a` | feat(friends): wire inspector refs, create prefabs, and place in Menu_Main |
| `31a1d42f8` | fix(benchmark): fix CS1525 and CS0246 compilation errors |
| `38edb75fd` | fix(benchmark): remove invalid '>' in interpolated string alignment specifiers |
| `2f5292371` | fix(benchmark): resolve CS0246 compilation errors in benchmark test assembly |
| `55a8468ba` | fix(compile): resolve CS0246 errors in FriendsServiceFacade, AudioSystem, and PlayFabCatalogTests |
| `149c92d70` | fix(benchmark): remove all benchmark assembly definitions |
| `8d8a35690` | Add Unity .meta files for performance benchmarks |
| `727192244` | Update packages-lock.json |
| `861c585f3` | Update FriendsServiceFacade.cs |
| `9123ab270` | Update AddFriendPanel.cs |
| `514adbb70` | fix(multiplayer): replace fragile SpawnManager lookup with SOAP event-driven player discovery |
| `350f31f3d` | Fix inflated accuracy scoring and domain not applying to shape prisms |
| `665030cd0` | fix(multiplayer): prevent host startup conflicts and transport corruption |
| `f504b32f6` | Larger shapes at intensity 1, disable pause during shape mode, richer waypoints |
| `a4f29d8dd` | Revert PauseButton.cs and use Button directly in ShapeDrawingManager |
| `9c44c52b2` | Add back missing using for CosmicShore.Game.UI |
| `59504dae9` | Add meta files |
| `fdcecba5f` | Update and Add new Shape assets |
| `e3f0c5c0b` | Update SpawnablePrism.prefab |
| `ab788db0c` | Update GyroidBlock Variant.prefab |
| `b4af57734` | Update MassGyroidBlock Variant.prefab |
| `50b3f6ff0` | Update SpaceGyroidBlock Variant.prefab |
| `f47c03f12` | Update MinigameFreestyle.unity |
| `c68a60a46` | fix(multiplayer): react to OnPlayerNetworkSpawned instead of OnClientConnectedCallback |
| `79d09ae27` | refactor(multiplayer): rename OnClientConnected to HandleNewPlayer |
| `f3ecea179` | Make squirrel the only one in hex race and crystal capture |
| `1bf5925b7` | Fix Windows build errors: wrap editor scripts with #if UNITY_EDITOR |
| `9596495b2` | Fix ResourceDisplay build error: move CSDebug using outside #if UNITY_EDITOR |
| `a788e8c35` | fix(multiplayer): eliminate spawn race condition between vessel init and ClientReady |
| `0c9b13677` | Fix one-frame explosion flash caused by frame-ordering race |
| `4ae730877` | refactor(multiplayer): simplify vessel initializer flow to direct synchronous spawn |
| `cb77f45b0` | Fix runtime crash: remove duplicate serialized fields from HexRaceHUDView |
| `6d249949b` | feat(multiplayer): add OnVesselNetworkSpawned SOAP event and support remote client joins |
| `354520d5b` | refactor(multiplayer): replace WaitUntil with OnValueChanged + RPC-driven client init |
| `40c08c180` | Update ServerPlayerVesselInitializer.cs |
| `768fc98f0` | fix(ai): guard against null Cell in AIPilot.UpdateCellContent |
| `5281e77ec` | fix(stats): guard against null cellData in StatsManager lifeform methods |
| `ea1ca5481` | fix(environment): guard against null Cell in SnowChangerManager |
| `58404aab2` | Fix game mode startup crash and RewindDrawer build error |
| `b88f716e2` | Remove stale ODIN_INSPECTOR scripting defines from Android platform |
| `ff5864157` | Add StatsManager prefab and scene instance |
| `a18705772` | fix(gameplay): guard against null references in SnowChangerManager.SpawnSnows |
| `087a90186` | refactor(gameplay): move cytoplasm spawning into Cell initialization flow |
| `d84237437` | Remove Odin Inspector attribute blocks from Soap plugin |
| `c25dc5161` | Fix scene teardown crash, track spawning, and NativeArray disposal |
| `50fdcf62d` | feat(menu): add crystal click to transition from menu to gameplay |
| `039054558` | refactor(menu): make crystal click a toggle between menu and gameplay |
| `1d30af4f5` | feat(menu): wire MenuCrystalClickHandler into Menu_Main scene |
| `dc086fb24` | Update Menu_Main.unity |
| `e3506b924` | refactor(menu): use Cinemachine retargeting for crystal click camera transitions |
| `040cd5440` | fix(scene): remove 13 duplicate object IDs from Menu_Main scene |
| `363695c0f` | Update MenuServerPlayerVesselInitializer.cs |
| `31b9be769` | Fix explosion sphere flicker: keep renderer disabled outside active animation |
| `355a2dd2b` | fix(multiplayer): ensure network host starts before loading Menu_Main |
| `990075d5a` | refactor(multiplayer): remove NetworkManager prefab instantiation |
| `9e3c5a6b4` | Fix Crystal material lerp: shader property mismatch and N² loop bug |
| `0cffb361e` | fix(multiplayer): assign name and domain before player spawn chain runs |
| `a7b417f6f` | Add QuestItemCard component, simplify QuestTrackView |
| `588b1b125` | Add netcode component to StatsManager and prefabs |
| `6f79b356c` | Start quest slider at 1/N to reflect first mode always unlocked |
| `e5ed5c486` | refactor(menu): extract menu state machine from vessel initializer |
| `27e38a24d` | refactor(menu): replace C# Action with OnMenuReady SOAP event on GameDataSO |
| `a197a1dba` | Add OnMenuReady event and menu UI components |
| `ea3e5a2e1` | Fix AOEExplosion flicker: hide mesh during ExplosionDelay |
| `3d97050ad` | Fix quest card display, slider animation timing, and add editor debug tools |
| `a50ff3131` | Auto-resolve QuestItemCard refs by child name; fix debug complete quest |
| `d242a399d` | Fix AOEExplosion flicker: use per-instance MPB for opacity |
| `7f04d4d9b` | fix(menu): defer vessel spawn until game data is configured via OnInitializeGame |
| `ff2d19e28` | Remove diagnostic debug logging from PrismExplosion and PrismEffectsManager |
| `c773e0098` | Refactor menu spawn initialization and origins |
| `d299f0e10` | fix(player): defer vessel type assignment when game data not yet configured |
| `b65d997e8` | refactor(player): unify vessel type assignment into single subscribe path |
| `17d561d31` | Revert "Merge pull request #263 from froglet-studio/claude/review-player-initialization-vluWB" |
| `b7b61f69e` | Reapply "Merge pull request #263 from froglet-studio/claude/review-player-initialization-vluWB" |
| `c56bf8df3` | fix(player): defer vessel type assignment when game data not yet configured |
| `9e5050cc6` | Update Player.cs |
| `93ff813b5` | Update ServerPlayerVesselInitializer.cs |
| `61e5ccee8` | Update ServerPlayerVesselInitializerWithAI.cs |
| `60b836b3f` | Update ServerPlayerVesselInitializer.cs |
| `55848ccb3` | fix(multiplayer): resolve vessel spawn race condition with UniTask delays |
| `e54d5874c` | fix(multiplayer): correct UniTask namespace import |
| `a26c73294` | fix(multiplayer): skip stale unspawned players in vessel spawn lookup |
| `45a6edaf8` | fix(multiplayer): process pre-existing players on initializer spawn |
| `f20fe6e68` | Update ServerPlayerVesselInitializer.cs |
| `9b7b61655` | Update Player.prefab |
| `1bd8d45bf` | Update MenuServerPlayerVesselInitializer.cs |
| `57ee41b89` | Pass IVessel to initializer; add safety checks |
| `04a8d7cfc` | Remove OnMenuReady event; use OnClientReady |
| `289c959c7` | Defer client-ready signaling and menu tweaks |
| `61bcd2d52` | feat(menu): add menu/freestyle state toggle with SOAP events |
| `6720c57bf` | Add MenuFreestyle scriptable events and scene refs |
| `d1b7093df` | refactor(menu): replace raycast input with public ToggleTransition method |
| `9a8189564` | Update Menu_Main.unity |
| `a57d8b723` | feat(menu): configure camera switching between CM Main Menu and CM PlayerCam |
| `a5f22583f` | feat(party): implement party invite system with host-to-client transition |
| `af5b9e65f` | refactor(party): remove PartyGameLauncher and game-launch plumbing |
| `067648bd5` | feat(party): add Party System UI menu screen and invite notification panel |
| `af3f423b2` | refactor(party): move party UI into Arcade screen's PartyArea panel |
| `a562a7377` | Update Menu_Main.unity |
| `487a7d8b9` | Add Friend Info prefab and friends UI to menu |
| `3b095faa0` | feat(party): implement invite lobby system for Menu_Main multiplayer |
| `8bb64d79c` | fix(party): wire inspector references and add PartyServices to Bootstrap scene |
| `b576c05c3` | fix(party): use correct ISession API for player properties |
| `be134101e` | Add PartyServices component to prefab and scene |
| `a0e6025e6` | Add party UI prefabs |
| `58ed055d4` | fix(party): remove orphaned party UI prefabs, scripts, and legacy PartyManager |
| `37eb13d7f` | Prefab-ify UI panels; rename Friend Info |
| `7c717c253` | feat(menu): integrate freestyle mode into Menu_Main state machine |
| `81a392859` | feat(arcade): sync game data before launch and use dynamic AI backfill count |
| `9ad0012c7` | feat(party): add scene wiring and FriendsPanel prefab creation to PartyPrefabSetup |
| `3124db2cd` | Stop controlling UnlockableIconBG, align slider to card positions, clamp scroll |
| `77f0f03db` | Add friends UI and test metadata |
| `796b86f35` | fix(party): enable multiplayer game launch from party invite session |
| `62a6eff76` | Update Party/Friends UI prefabs and scene |
| `de85acae6` | Update PrismManagers.prefab |
| `579f0d805` | fix(camera): set CM Main Menu camera tracking target to crystal |
| `cd8a1be71` | fix(camera): dynamically target crystal for CM Main Menu camera |
| `00f94ee1e` | test(party): add comprehensive party invite system tests |
| `578b6f465` | fix(build): add missing using directives for shape drawing system |
| `d7fb351db` | fix(spawnable-shapes): add missing using CosmicShore.Gameplay directive |
| `658a8d03b` | feat(multiplayer): add MPPM support for virtual player client role |
| `1efb38797` | fix(spawnable-shapes): add missing CosmicShore.Gameplay using directive |
| `275bd326b` | fix(camera): orbit menu camera around crystal at distance with smooth transitions |
| `616040478` | Fix null pointer crashes in arcade minigame initialization on Android |
| `a4ec91c16` | Fix CSDebug compile errors in ShipHelper — use Debug.LogError instead |
| `f2ddc6c3a` | Bump AndroidTargetSdkVersion from 33 to 35 to fix Gradle build failure |
| `3887a710f` | generate files during android building |
| `23fa42b48` | Re-enable TouchInputStrategy for handheld devices |
| `7455bc479` | pushing made changes again |
| `3027c1d12` | more post build modifications |
| `0b985b55c` | pushing post build junk again |
| `aac01c12b` | Gitignore entire .utmp/ dir and remove tracked build artifacts |
| `01056cacf` | Restore touch drift controls and add device-specific action mapping |
| `e86e40581` | feat(multiplayer): support MPPM as independent hosts with unique identities |
| `ac59d484c` | Update Menu_Main.unity |
| `1ce955709` | refactor(menu): extract camera logic into MainMenuCameraController |
| `ac0e39eb8` | fix(editor): prevent SOAP SO assets from persisting play-mode changes to disk |
| `f7402b38d` | Modify UI Prefabs |
| `bfcd98b45` | Add meta files |
| `07fb86ce8` | Add Quest List |
| `9964c0d76` | Update Menu_Main.unity |
| `fc773ce36` | Replace Unity Slider with Image fillAmount for progress bar alignment |
| `a5c1bedf1` | Remove Slider, use only Image fillAmount for progress bar |
| `0da3cbf90` | Fix scroll not moving and fill bar showing 1 on quest reset |
| `f401945ec` | fix(camera): use LockToTargetNoRoll for TPP chase camera in freestyle mode |
| `379830702` | Add touch-specific easing curve for responsive glass-surface input |
| `a43111578` | fix(camera): use per-vessel CameraSettingsSO for freestyle camera instead of custom vCam |
| `36ca9c593` | feat(ui): add InitialPanelStateApplier for configurable panel startup states |
| `0732d6578` | Update Menu_Main.unity |
| `069bfdfcc` | Create InitialPanelStateApplier.cs.meta |
| `1430b0b5f` | Create PartyInviteSystemTests.cs.meta |
| `60906511d` | Create MainMenuCameraController.cs.meta |
| `665737340` | Update Bootstrap.unity |
| `2cbe3b07b` | Update Menu_Main.unity |
| `d91cd94e8` | Update quest data assets |
| `e0fb61291` | Fix quest track scroll and progress bar |
| `c90ed014c` | feat(camera): smooth menu↔freestyle camera transition via Cinemachine priority blending |
| `86a9858d3` | Fix CS0117: IsActive is a method, not a property |
| `6fe402fa3` | refactor(ui): replace SetActive with CanvasGroup-based visibility for Menu_Main UI |
| `5549acbf5` | fix(editor): defer SO restore to run after SOAP reset callbacks |
| `8d78e9bc5` | fix(camera): set LockToTarget binding mode + damping on freestyle vCam |
| `9be6264ef` | Create PlayModeSOProtector.cs.meta |
| `bc8724775` | Create UNIT_TESTING_GUIDE.md.meta |
| `a9ef465e1` | fix(camera): use InheritPosition blend hint for freestyle vCam |
| `9652871bb` | fix(ui): call SetVisible on gameObject for MonoBehaviour button types |
| `a08775468` | fix(camera): remove non-existent CinemachineBlendHint usage |
| `2d7ba7255` | feat(editor): add Canvas Group Editor window for initial panel visibility |
| `b859456a2` | fix(camera): sync position and rotation damping for freestyle camera |
| `23dffca0c` | feat(editor): add dual active/alpha controls to Canvas Group Editor |
| `e311b2e42` | fix(camera): get VesselCameraCustomizer from VesselStatus, not follow target |
| `ffea81f08` | fix(camera): restore CinemachineRotationComposer for freestyle orientation |
| `bbccd0abd` | Update Bootstrap.unity |
| `7b13a5e4c` | Create CanvasGroupEditorWindow.cs.meta |
| `da5da80ff` | fix(camera): match vessel orientation instead of look-at aiming |
| `37d8c2da0` | fix(camera): use CustomCameraController for freestyle camera follow |
| `17beb00a0` | feat(camera): add smooth Cinemachine-blended transitions between menu orbit and freestyle |
| `f780cf826` | fix(menu): prevent click spam during camera transitions |
| `e0dcaaadf` | Revert "Merge pull request #304 from froglet-studio/claude/canvas-group-editor-window-elcvv" |
| `22fda6f0c` | Revert "Merge pull request #299 from froglet-studio/claude/canvasgroup-menu-main-5NUH3" |
| `d87acd505` | Revert "Merge pull request #297 from froglet-studio/claude/initial-panel-states-zewzl" |
| `cf777042a` | revert: remove orphaned InitialPanelStateApplier.cs.meta |
| `0707c0c40` | refactor(ui): use CanvasGroup for ArcadeScreen visibility instead of SetActive |
| `69069a50e` | fix(aoe): wire missing gameplaySFXEvent on all AOE explosion prefabs |
| `67420fd67` | Add PrismEffectsManager to PrismManagers game object in Bootstrap scene |
| `3570208a8` | fix(ui): rewire ArcadeScreen CloseButton to use Hide() instead of SetActive |
| `413a5d184` | feat(multiplayer): add network-aware vessel swap for Menu_Main freestyle |
| `48ddf7607` | fix(ui): set ArcadeScreen CanvasGroup to start hidden in Menu_Main |
| `8a581cff2` | refactor(multiplayer): integrate vessel swap into existing initializer hierarchy |
| `cbe68a832` | refactor(ui): convert all panel/modal show/hide from SetActive to CanvasGroup |
| `44ec67d56` | refactor(so): remove SOAP/Soap submenu from all CreateAssetMenu menuNames |
| `ced2326ca` | Hook ArcadeScreen reference in ScreenSwitcher |
| `04200f2dc` | Update Menu_Main.unity |
| `2ace7dbf2` | Update Menu_Main.unity |
| `c53f5b3e5` | Update Menu_Main.unity |
| `9b3c7162a` | refactor(so): unify all CreateAssetMenu menuNames under ScriptableObjects/ prefix |
| `123fa60ef` | Update Menu_Main.unity |
| `6aa18ba78` | refactor(so): remove Variables/ submenu from CreateAssetMenu menuNames |
| `181d7f0fc` | refactor(ui): rename Overview to VesselSelection across vessel swap system |
| `57ed283c3` | Update Menu_Main.unity |
| `6a6c1f9f0` | fix(menu): preserve per-panel alpha when exiting freestyle state |
| `c410d3d09` | Update Menu_Main.unity |
| `0a82ea097` | Add analog trigger support for drift — continuous intensity scaling |
| `098a301fe` | fix(camera): force bridge vCam position on freestyle exit to fix stale state |
| `b90b70870` | Update Party Slot View.prefab |
| `4e01b5501` | fix(camera): resolve freestyle-to-menu camera transition failure |
| `92d354c53` | fix(party): fix presence lobby race condition and empty OnlinePlayers panel |
| `312fa9c92` | fix(camera): allow transition preemption when durations mismatch |
| `464d74807` | Fix drift damping baseline — use full damping at zero trigger, not zero |
| `e37097641` | fix(camera): eliminate snap on menu-to-freestyle handoff |
| `9f17827c3` | Review cleanup: privatize drift fields, cache trigger sum, fix fallback logic |
| `4e6e64059` | fix(camera): force Brain to cut on freestyle→menu transition |
| `101fec1a8` | Add drift easing for non-analog trigger input |
| `292cdf792` | refactor(camera): bridge always tracks vessel, no snapshots or forced positions |
| `e5e02c983` | fix(camera): sync rotation with position during blends, poll IsBlending |
| `f8a6f440e` | Fix scroll, show locked quest info, add quest complete notification |
| `ba92e89b2` | fix(camera): yield before IsBlending poll so Brain starts blend first |
| `47d3b1d8b` | Add element pips HUD system for displaying elemental levels |
| `222fd15b4` | fix(scenes): restore missing .meta files and update class refs after Overview→VesselSelection rename |
| `f7a554393` | feat(multiplayer): sync game config from host to clients before scene load |
| `93f88b847` | fix(scene): remove standalone Vessel Selection Button from Menu_Main |
| `ba97300d1` | fix(ui): activate modal screens and add missing navigation methods |
| `dc9353837` | Update MinigameHexRace.unity |
| `8dc696d43` | feat(ui): add MenuFreestyleHUD with vessel change button to Menu_Main |
| `780e48aa9` | fix(scene): restore missing script and swap to AI spawner in HexRace |
| `6b1ac6141` | refactor(multiplayer): register MultiplayerSetup in DI, replace SerializeField with Inject |
| `a78355cd6` | Update MinigameHexRace.unity |
| `0385e492c` | feat(ui): replace MenuFreestyleHUD with MenuMiniGameHUD + Volume/Pause button |
| `9cbe52ff9` | fix(multiplayer): discover persistent Players and fire session start in game scenes |
| `d6f6c58e6` | Update Bootstrap.unity |
| `e97e7fcfd` | fix(multiplayer): discover persistent Players and fix HexRace spawn pipeline |
| `2e890736c` | refactor(multiplayer): design system around persistent Player objects |
| `f562203fb` | fix(ui): remove connecting panel, use SceneTransitionManager fade during game load |
| `7c57846d8` | feat(bootstrap): use splash screen Canvas as persistent scene transition overlay |
| `7f42bbada` | fix(party): start NetworkManager as Relay host from bootstrap, eliminate destructive shutdown on invite |
| `5f3c2a7d0` | fix(party): snapshot OnlinePlayers list before iterating in SendInviteAsync |
| `9b16fe572` | fix(ui): remove orphaned RequireClientReady override from MultiplayerHUD |
| `75816dc74` | fix(party): make Relay session failure non-fatal so refresh loop always runs |
| `290cdda81` | debug(party): add colored diagnostic logs to invite send/receive flow |
| `4e092c581` | fix(party): keep PartyInviteNotificationPanel GO active for SOAP subscription |
| `79ff3686e` | Add CanvasGroup to PartyInvite panel & scene updates |
| `9e7cbf0bb` | refactor(party): use serialized CanvasGroup for notification panel show/hide |
| `d72e1a87c` | Update Menu_Main.unity |
| `ceac1874d` | fix(party): auto-clear invite properties when invited player joins party |
| `1e4b6f920` | fix(ui): remove ConnectingPanel prefab, fix splash stuck after game load |
| `798cd9ce0` | fix(party): prevent IndexOutOfRange and Netcode write permission errors |
| `63e72e8f2` | fix(party): replace _refreshSuspended with _lobbyBusy mutex for SDK safety |
| `606408f96` | fix(player): use IsOwner || IsServer guard for NetworkVariable writes |
| `345abf858` | fix(gameplay): guard against null audioSystem in LifeForm.Die() |
| `9f56f8933` | fix(arcade): ensure at least 1 AI opponent when solo in multiplayer modes |
| `39b234a1b` | debug(hexrace): add colored flow logs across entire HexRace execution pipeline |
| `79a17071b` | fix(player): split NetworkVariable writes by permission, defer spawn event |
| `2b946e65c` | fix(multiplayer): unpause game on launch, use UnscaledDeltaTime for spawn delays |
| `1750911f8` | Wire element pips HUD into Unity assets and prefabs |
| `3b48e7b10` | fix(party): guard SaveCurrentPlayerDataAsync against SDK index error |
| `081de11b8` | fix(multiplayer): prevent NetworkManager shutdown when leaving Menu_Main |
| `89f168384` | fix(snow): guard against null crystalLattice in SnowChanger.ChangeSnowOrientation |
| `6e0049a9a` | fix(party): remove _lobbyBusy deadlock in ClearSentInvitePropertiesAsync |
| `5c2ebff91` | fix(party): wire canvasGroup on PartyInviteNotificationPanel prefab |
| `3ca8d7606` | Fix claim button, quest persistence, and editor tools |
| `7f839e80e` | fix(party): save player properties directly instead of refresh-then-save |
| `469028d7d` | fix(party): add retry with backoff for UGS rate-limit on player data save |
| `6fbbed44b` | Wire quest completion into end-game cinematic and sync SO runtime flag |
| `17279a4b0` | fix(party): refresh lobby before save to prevent player index out of range |
| `1c81e6d23` | fix(multiplayer): guard StartPlayer against null Vessel in SetNonOwnerPlayersActiveInNewClient |
| `e8cd0aea1` | fix(multiplayer): replace batch player activation with per-pair SOAP event |
| `74a63802a` | Switch QuestTrackView from Image.fillAmount to Slider with whole-number quest values |
| `ab8983c5b` | fix(multiplayer): suppress SDK LobbyPatcher index out-of-range error |
| `a9bfb19d2` | Fix slider starting at wrong value, add placeholder quest handling, complete-all debug button |
| `bdcb1ec5c` | Add AAA polish: scroll snap, parallax depth, claim fanfare, active pulse, state animations |
| `accc95776` | Update QuestItemPrefab.prefab |
| `0bb7fead6` | Update Menu_Main.unity |
| `88119a7ac` | Update FrogletTools.cs |
| `a63dcc740` | Update GameModeQuest_HexRace.asset |
| `94b49817a` | Update MinigameHexRace.unity |
| `db992e24e` | Update MinigameCrystalCaptureMultiplayer_Gameplay.unity |
| `d05c8c33e` | Fix SnapToCard to accept duration param; clamp scroll after layout; slow scroll on claim |
| `ab627d75f` | Replace Complete Current Quest with Complete All Quests in FrogletTools menu |
| `a569a4ccb` | Add index-based quest debug tool; use dialog object for quest completion HUD |
| `a35514e28` | Redesign FrogletTools into professional Froglet Toolbox panel |
| `49645e632` | Restyle Froglet Toolbox with pastel palette; consolidate all tools into panel |
| `a8d0ff94c` | Consolidate all FrogletTools into Toolbox; fix Soap ObjectEditor crash |
| `05d22baf9` | Fix compilation errors: use ExecuteMenuItem instead of direct type refs |
| `2e6340162` | Fix Toolbox buttons unresponsive after domain reload |
| `cdffa2755` | Fix unclickable buttons/toggles in Froglet Toolbox |
| `d4c269715` | Update Menu_Main.unity |
| `1596a8ece` | Update MinigameCrystalCaptureMultiplayer_Gameplay.unity |
| `c4e3eee82` | Update MinigameHexRace.unity |
| `dea662177` | Update MinigameJoust_Gameplay.unity |
| `82867f9f8` | Update QuestItemPrefab.prefab |
| `cfbb62c24` | Update game mode progression assets |
| `78f19eb16` | Fix game mode list refresh, non-quest modes always unlocked, state-based glow colors |
| `a4b184ba6` | All modes locked by default, per-mode debug toggles, fix lock overlay sticking |
| `6c7f246d1` | Disable Port and Ark screens from navigation, add PROFILE enum |
| `4f0643eba` | Add ghost slider showing next quest description with fade animations |
| `9d27559d8` | Replace ghost text with spawned description labels, fix to show current quest goal |
| `bb481ece7` | Choreograph claim sequence: text out → slider moves → card completes → next enables → text in |
| `91152ebf8` | Create Quest Description prefab |
| `68ddc5287` | Update Main Menu |
| `5eabb5d40` | Fix missing using directive for HangarAbilityCard in HangarVesselDetailView |
| `4fdfb2246` | Add grid fade animations, move unlock config to SO_Ship, decouple from Captain system |
| `49aa8b455` | Add vessel unlock debug tool, remove Captains from SO_Ship |
| `15f1d8b04` | feat(menu): add vessel HUD to Menu_Main via SOAP transition bracket events |
| `7467876db` | fix(hexrace): ensure at least 1 AI opponent spawns for solo HexRace |
| `a0e930c3d` | fix(arcade): ensure GameMode is set before any game launch |
| `0bd52b9d5` | fix(arcade): SpawnAIs calls EnsureMinimumAIBackfill as second safety net |
| `44a8280ef` | fix(bootstrap): wire MenuFreestyleEvents asset in AppManager prefab |
| `add38fa73` | Update AppManager prefab and Bootstrap scene |
| `4efb072a8` | feat(ui): write player display name to SOAP StringVariable for UI binding |
| `a7bb7292c` | refactor(soap): move username StringVariable into AuthenticationData |
| `63a50977a` | feat(soap): add SOAP StringVariable for username with HomeScreen UI binding |
| `a451b6d12` | Remove SO_Captain from active game systems, replace with SO_Ship |
| `db8e895bd` | fix(hexrace): remove DestroyPlayerAndVessel race that kills AI players |
| `ba723b40f` | Add UserName asset and link in AuthenticationData |
| `b73f9895e` | Add missing using for ResourceCollection in SO_Ship |
| `470214ca6` | Update Menu_Main.unity |
| `d91fef105` | fix(soap): push default profile to SOAP UserName on startup |
| `c31af6e1f` | Rename SO_Ship to SO_Vessel and add element fields |
| `b5f78f329` | Update Player.prefab |
| `a861745eb` | Update UserName.asset |
| `0dea1ca4e` | fix(soap): stop pushing default Pilot name to SOAP UserName on startup |
| `a3156a91f` | Update ServerPlayerVesselInitializerWithAI.cs |
| `cd42b91f0` | Remove duplicate InitialResourceLevels field in SO_Vessel |
| `2fac22414` | Fix missed Ship → Vessel rename in FactionMissionModal |
| `81092c138` | fix(multiplayer): add retry with exponential backoff for UGS lobby rate limits |
| `4b293928e` | fix(multiplayer): shut down local host before creating Relay party session |
| `913dd51e3` | Update Vessel Classes |
| `4b4baa014` | Update meta files |
| `29f509d8a` | Move vessel lock state to SO_Vessel.isLocked, remove PlayerPrefs persistence |
| `2f2af9a60` | fix(multiplayer): remove redundant AI spawn point logic |
| `69e390a8e` | Block locked vessels from all selection and launch paths |
| `88d854ea2` | Create HEXRACE.md.meta |
| `e1ac88b53` | Update Vessel Classes |
| `f951656d9` | Add UI Assets |
| `35f59aa33` | Update Menu_Main.unity |
| `cf464dabb` | Refactor cytoplasm shards from Cartesian grid to spherical shell coordinates |
| `60aae7849` | Implement hangar grid + detail panel UI overhaul |
| `659a09a4b` | fix(multiplayer): add playerSpawnPoints to ServerPlayerVesselInitializer |
| `34e11a383` | Update Menu_Main.unity |
| `bf66bf3d7` | Update MinigameHexRace.unity |
| `c99fec59e` | Update MinigameHexRace.unity |
| `9f095280c` | Add icosphere membrane system with noise-driven alpha pores |
| `f69aa389d` | Add UI Sprites |
| `a68fe3e2a` | Update Menu_Main.unity |
| `7be6b70c4` | Add crystal currency system with persistent balance and end-game rewards |
| `58873b647` | fix(multiplayer): prevent AI players from consuming domain pool in Player.OnNetworkSpawn |
| `a48b296cc` | Add crystal currency debug tools to Froglet Toolbox |
| `67908ae5b` | Fix unlock panel: use spendCrystalsDetailText, make CloseUnlockPanel public |
| `2d4482449` | Add crystalAmountText reference to HangarVesselDetailView |
| `5f66dd941` | Simplify unlock panel: remove notEnoughCrystalsPanel, disable confirm instead |
| `03e7309ee` | Disable confirm button GameObject instead of interactable |
| `41a64d63d` | Update VesselGridPrefab.prefab |
| `7cd5f6f9c` | Create CrystalCurrencyDisplay.cs.meta |
| `d5b4eaa31` | Update MinigameCrystalCaptureMultiplayer_Gameplay.unity |
| `f3dad3b0a` | Update MinigameHexRace.unity |
| `c19f8bf53` | Update Menu_Main.unity |
| `98fdd8823` | Update MinigameJoust_Gameplay.unity |
| `bd5f7180f` | Add unified toast notification system with swipe-to-dismiss |
| `8cf28f7f5` | fix(multiplayer): remove domain from InitializeData, use DomainAssigner for all paths |
| `6565b0965` | Add SOLID UGS data service with 6 new cloud data domains |
| `0055d413e` | Add DOTween-based HUD animations for minigame polish (P0-P3) |
| `194ff2159` | Wire UGS data service into all game systems (Hangar, Episodes, Settings, Stats, Progression, Profile) |
| `8d1477ca3` | Add read-only UGS Data View tab to Froglet Toolbox |
| `beb4b99c2` | Fix card stacking, add score color flash, scoreboard/vessel polish |
| `f4f55aa18` | Add HUD Animation Setting |
| `7821dbe0a` | Fix GetValueOrDefault calls for .NET Standard 2.0 compatibility |
| `6b4a05321` | Add meta files |
| `ddbcea0e8` | Fix UGS Data View not refreshing after service initialization |
| `b492f44e6` | Derive cytoplasm shell radii from Cell nucleus/membrane transforms |
| `cc1229780` | Auto-create UGSDataService at runtime via RuntimeInitializeOnLoadMethod |
| `0622867c8` | Guard eager initialization with UnityServices.State check |
| `d45466240` | Add intensity-based quest progression: unlock intensity 4 to unlock next game mode |
| `3a482bbe3` | Add sparse shard population inside nucleus at 1/4 outer density |
| `9cad3a3a7` | Grade inner shard density linearly from full at nucleus surface to zero at center |
| `0255743a5` | Uniform shard density throughout entire sphere (0 → membrane) |
| `bc9f11e15` | Naive spherical sampling — no volume correction, natural 1/r² thinning |
| `b6f2cd5dc` | Restore volume-uniform spherical sampling with cube-root correction |
| `5c3a8cd8c` | make shard pointy |
| `07701c117` | Replace failed membrane approach with GPU-instanced capsule membrane |
| `aef14cf14` | Add editor tool to generate element shape sprites |
| `91d3c0802` | Move element pips to lower-left corner of HUD |
| `90ddf00a2` | Redraw element shapes from design spec, move pips to lower-left |
| `f8ca4fb04` | Fix element shapes: Space is a kite, Charge is an irregular pentagon |
| `3f0f36fc9` | Premium element sprites: glow labels + uniform tick pips |
| `48c34394a` | wiring up comeback and changing silhouette spacing |
| `076c02558` | Fix sprite scaling artifacts: match texture size to display size |
| `4cbce1533` | fix(multiplayer): clear Player vessel references before network scene load |
| `a5a3329ea` | fix(multiplayer): despawn vessels before network scene load to prevent client errors |
| `e2e4f414f` | Fix zero-level pip coloring to match negative ticks |
| `280f0aec1` | Remove element shape generator (editor-only tool, not needed in build) |
| `918e9768a` | fix(multiplayer): use Despawn(false) to avoid Invalid Destroy on client |
| `977845bea` | debug: add color-coded logs for vessel despawn flow tracing |
| `c72edd68e` | Add IntensityInfoPanel and configurable goal descriptions for intensity unlocks |
| `f20b3c4ca` | Push graphics changes |
| `ddcce1501` | Wire intensity info panels, end-game unlock display, and debug tooling |
| `0b1f8c112` | fix(multiplayer): reset RoundStats and InputStatus on scene transition |
| `112e9ddda` | fix(arcade): sync user's total player count instead of human-only count |
| `374aa72de` | fix(multiplayer): consolidate player count to single source of truth in GameDataSO |
| `14fda3040` | fix(multiplayer): prevent forced AI when 2+ humans and remove stale SOAP write |
| `856105267` | fix(auth): prevent Menu_Main double-load on startup |
| `424511e2c` | rearrange joust intensity levels |
| `9197dbfdc` | Add editor utility for toast notification asset setup |
| `f01d5ca40` | Add Toast Notification Files |
| `1c80fe1d4` | Update Toast Notification Prefab |
| `fdf009ac9` | Replace IntensityInfoPanel with toast notifications, add vessel hangar gate and error toasts |
| `9d1a801b2` | Fix toast system, vessel hangar gate, and VesselHangarUnlock quest cleanup |
| `ae21d45da` | Refactor toast system to spawn inside UI container instead of overlay canvas |
| `d627c2e1f` | Stop toast system from touching layout — container owns all positioning |
| `7239d7372` | Update ToastNotificationItem.prefab |
| `227a64f37` | Update QuestItemPrefab.prefab |
| `3b6c361a5` | Update Game mode SO's |
| `8afdc0fb9` | Update Menu_Main.unity |
| `c71d55072` | Redesign Froglet Toolbox from foldout sections to pastel-colored tabs |
| `652980c65` | Sort unlocked items first in game mode grid and refresh vessel grid on unlock |
| `10543ad95` | Polish tab bar: bigger colored tabs, tinted content backgrounds |
| `3f465a178` | fix(hexrace): set MinPlayers to 2 so HexRace always requires an opponent |
| `80b9d8f4b` | Fix main menu navigation |
| `5fc002309` | Fix multiplayer team crystals not spawning for non-host players |
| `69a119331` | Add elemental fill bars, overtake penalty system, and HUD juice effects |
| `c2a0df0a5` | Refactor ElementalBarsView to use pre-placed prefab references |
| `890358f82` | Add auto-populate fallback from ElementPipsConfigSO, delete unused config SO |
| `3ed5d6a9d` | Add runtime scale API to ElementalBarsView |
| `07cfd862b` | Add domain-color fill on buff, white on debuff, and animated scale API |
| `599901b34` | Add live SO updating: ElementPipsConfigSO changes now rebuild UI at runtime |
| `572ffc397` | Strip auto-populate from ElementalBarsView — pre-placed mode only |
| `416070a08` | Add editor script to stamp out ElementalBarsView UI hierarchy |
| `d18c2ed38` | Fix quest progression: stat-based intensity unlocks and prevent premature quest completion |
| `92930b506` | Add sprites |
| `3889c0d75` | Update Squirrel.prefab |
| `81afea053` | Add meta files |
| `249906d9b` | Rewrite ElementalBarsView for discrete pip arrays, delete editor script |
| `1875d6fd8` | Update Squirrel.prefab |
| `f4c872c62` | Switch ElementalBarsView to SetActive for discrete pip enable/disable |
| `f183ec456` | Add juice: staggered pop-in on buff, shake + haptics on debuff |
| `2c2518a34` | Fix: subscribe to OnElementLevelChange even without elementPips |
| `8439d9c0f` | Add Start() fallback so ElementalBarsView self-initializes baseline pips |
| `5ea4a4ae7` | Fix overtake delay + remove elementPips from SilhouetteController |
| `a70a97f0f` | Floor baseline pips at 5 — only overtake penalty can go below zero |
| `ac7a9faba` | Fix: fire overtake events BEFORE slamming levels |
| `bad24366e` | Also spawn missing crystals on turn start for robustness |
| `66daef9c9` | Wire overtake penalty to joust event instead of score polling |
| `c001b5ae4` | Revert "Wire overtake penalty to joust event instead of score polling" |
| `ef16fd54b` | Revert "Fix: fire overtake events BEFORE slamming levels" |
| `e0265785c` | Update Squirrel.prefab |
| `3baf9489a` | refactor(player-count): remove EnsureMinimumAIBackfill, enforce MinPlayers on assets |
| `4b77b6d68` | Skip early crystal spawn in multiplayer, defer to turn start |
| `d10af1065` | Spawn crystals immediately as each player joins the room |
| `4dfbf097d` | Fix late-joining clients not seeing previously spawned crystals |
| `9b33ede74` | feat(player-count): simplify player count flow with stepper UI and team selection |
| `c0381c85a` | feat(multiplayer): balance AI team assignment by filling smallest teams first |
| `cc706adf4` | chore(assets): set MaxPlayersAllowed to 12 for HexRace, Joust, CrystalCapture |
| `b52a8d7ce` | Update Menu_Main.unity |
| `773b396d2` | feat(ui): wire TeamSelectionPanel into ArcadeGameConfigureModal |
| `d49991edf` | Update Menu_Main.unity |
| `dc22f9f9b` | wired up and middified spiindle graph |
| `badde356d` | Add forcefield crackle effect to replace SkimmerFXPrismEffect |
| `64ab11976` | Add Spindle-style surface to capsule membrane + placement noise |
| `1688f0702` | Update Menu_Main.unity |
| `542421f67` | Add Unity assets: shader, material, prefab setup, and SO wiring |
| `8c33f3531` | fix(ui): remove legacy playerCountButtons causing NRE in ArcadeGameConfigureModal |
| `8bb5162fc` | Expose serialized visual params for forcefield crackle effect |
| `26df837ff` | use skybox in scenes |
| `0f22c3e04` | Move radial pulse to CPU, drop custom shader — use SpindleMaterial directly |
| `e26c78e57` | feat(multiplayer): sync team/vessel selection modal to all clients via RPC |
| `8fede1cf9` | refactor(di): register SO_GameList in AppManager and replace SerializeField with Inject |
| `288df934c` | feat(multiplayer): add ready-up system for arcade game config |
| `00156072c` | fix(multiplayer): sync screen transitions, player count visibility, and back button to clients |
| `bad6262ad` | fix(multiplayer): require all human players to click Ready before countdown |
| `3500b3616` | Update Menu_Main.unity |
| `d016c79d1` | refactor(di): replace SerializeField with Inject for SO_GameList in ArcadeConfigSyncManager |
| `21f7d530c` | Update Bootstrap.unity |
| `15df1ebfa` | Update AppManager.prefab |
| `8a23df7d4` | Update Bootstrap.unity |
| `0e6ab7fbc` | Update Menu_Main.unity |
| `b71dc3578` | Update Menu_Main.unity |
| `b51ff9787` | fix(multiplayer): guard Player.StartPlayer() against null Vessel on non-host clients |
| `b07662ed0` | fix(multiplayer): guard Player.StartPlayer() against null Vessel on non-host clients |
| `7453ee689` | fix(namespaces): resolve 700+ compile errors from dev→app-shell merge |
| `d9c7d5c14` | fix(vessel): link VesselPrismController CTS to destroy token to prevent MissingReferenceException |
| `d82c46e39` | Update Menu_Main.unity |
| `b7c119a64` | Update Menu_Main.unity |
| `39c27e491` | fix(multiplayer): use Netcode connected client count for humanCount instead of stale PartyMembers |
| `ec11c5d52` | fix(multiplayer): despawn networked objects before destroy and show splash during Menu_Main reload |
| `8a6757f54` | Overtake is now an impact effect, not a system |
| `1dbb8421f` | Create Vessel Overtake Effect |
| `edac93aae` | Update SquirrelSkimmerImpactorDataContainer.asset |
| `692714b0c` | fix: add missing using directives to resolve CS0246 errors |
| `56e68fe5f` | fix: add missing using directives to resolve CS0246 compile errors |
| `ab91ff96e` | fix(ui): add missing namespace using directives to resolve CS0246 errors |
| `47f98f3ff` | fix(ui): add missing using directives to resolve CS0246 compile errors |
| `9930fe6aa` | fix(compile): resolve CS0246 missing type/namespace errors |
| `9cf69c7d0` | fix: add missing using directives to resolve CS0246 compilation errors |
| `4aa9fc015` | fix(namespaces): resolve compilation errors from wrong/missing using directives |
| `1aff3f692` | push fiddling |
| `7c227bf6a` | Fix prism ring spawning broken by zero-scale initialization |
| `3723da40b` | add Squirrel crystal haptics |
| `d73a7aa7a` | Fix snow changer shards not spawning — wire CytoplasmPrefab through Cell |
| `2f9e8824f` | Fix MembraneRadius returning 1 for CapsuleMembrane — shards now fill cell |
| `e1e75c70d` | Fix skybox not rendering in Menu_Main (bootstrap) scene |
| `52bdc1608` | Add SkyboxModel (BigMembraneVariant) to Menu_Main scene |
| `ed96278f9` | Fix skybox disappearing at runtime by removing forced SolidColor clear flags |
| `bf90cb15b` | Add Migration Prefabs |
| `2fffcd2b4` | fix(namespaces): add missing using directives to resolve CS0246 errors |
| `663ac89e4` | fix(namespaces): resolve remaining compile errors from namespace migration |
| `1732bd4f8` | fix: remove duplicate legacy NetworkMonitor class |
| `bec616b8c` | Replace voronoi crackle pattern with FBM-based electrical arc effect |
| `1055d0b26` | Add edit-mode preview for forcefield crackle visual params |
| `41ce8326e` | Pushing fiddle |
| `f4c58f1aa` | Rebuild forcefield crackle: controller owns visual params, fix fresnel |
| `f49be9d17` | Fix forcefield crackle not rendering when camera is inside sphere |
| `c275d50bd` | Fix freestyle unlock, disable daily challenge, add quick play |
| `dc98ae811` | Add Vessel3DCanvas World Space canvas system for vessel prefabs |
| `445a77500` | Add comprehensive gamepad/controller support across all UI screens |
| `ceb487bd0` | Fix daily challenge card, quickplay single-player, and vessel selection |
| `c7d90e12e` | Fix LaunchArcadeGame/TrainingGame not writing player count, intensity, and vessel to gameData |
| `86422d9c5` | Disable vessel nav buttons when only one ship available; guard intensity selection |
| `1fbba8f66` | Optimize physics settings for mobile performance |
| `949567e39` | Replace material instance creation with MaterialPropertyBlock in Spindle |
| `ae371452d` | Add MobilePerformanceManager for automatic mobile quality settings |
| `7402cd3c7` | Add feature unlock support to quest system; fix vessel hangar never unlocking |
| `383cad63e` | Add performance benchmark tool for measuring and comparing frame metrics |
| `ddd730726` | Hide quest description text when all quests are claimed |
| `c9314bdeb` | Tune level generators |
| `695ccc4b7` | Update Menu_Main.unity |
| `655de3a47` | Revamp Quest Game modes |
| `f34c94796` | Revert "Hide quest description text when all quests are claimed" |
| `23cd303da` | Add ARCADE as a modal window in ScreenSwitcher |
| `1953ca1b0` | Update Menu_Main.unity |
| `5ac9b5324` | Fix ControllerButtonPress firing across all contexts on A press |
| `7f71bd997` | Fix buttons with null ScreenSwitcher firing unconditionally on A press |
| `a2b3d0191` | Guard ControllerButtonPress against non-interactable/inactive buttons |
| `15eaeb871` | Hardcode A button on HOME screen to open Arcade modal |
| `c9f476334` | Add X→Settings hardcode on HOME + vessel validation in PlaySelectedGame |
| `ced0f65d1` | Update Menu_Main.unity |
| `8c6bfe6d3` | Update to remove red warning color in countdown |
| `60a68f9aa` | Update Gamecanvas to replace Scoreboard icon with settings icon |
| `2cc46f56d` | Add built-in TMP label to Vessel3DCanvas |
| `055058e09` | Add game tips system to connecting panel |
| `8db76f75f` | Add per-game-mode pre-game cinematic camera setups via ScriptableObjects |
| `392fc94b8` | Add Pre game setups |
| `07992f043` | Add PreGameLibrary Asset |
| `260cf80f7` | Update MinigameFreestyle.unity |
| `f17975bd5` | Update MinigameJoust_Gameplay.unity |
| `71c4114aa` | Update MinigameHexRace.unity |
| `10d8f429a` | Update MinigameCrystalCaptureMultiplayer_Gameplay.unity |
| `a0f825ab3` | Add deterministic mode to benchmark tool for repeatable results |
| `151bc9418` | Add SO Game Tips List |
| `85fde90a7` | Add meta file |
| `9ab277e60` | Update Gamecanvas to include new connecting panel changes |
| `d227dc3cb` | Add score popup system and fix Squirrel HUD local-player filtering |
| `951cc5cf9` | Refactor Vessel3DCanvas to use editor-assigned Canvas instead of runtime creation |
| `056ff2d59` | Refactor ScorePopup to use editor-assigned TMP_Text and domain color |
| `3f80cc8dd` | Remove Vessel3DCanvas — no longer needed after ScorePopup moved to 2D HUD |
| `37d23b70e` | Add automated benchmark session runner with reproducibility analysis |
| `68fcc204f` | Fix ScorePopup to track correct stat per game mode |
| `581c0dedd` | Add Score Pop up UI |
| `4f4917e93` | Add Score pop up system |
| `692df7314` | Remove buggy code |
| `e7d7c3600` | Update GameCanvas.prefab |
| `99912667a` | Add 3rd spawn point |
| `c99ea1f0f` | Add Episode Prefab |
| `77b87e859` | Simplify episode SOs and implement IAPManager with IDetailedStoreListener |
| `279603d0c` | Add auto-size and configurable font to game feed entries |
| `0c27228f0` | Add 15 diversified gaming names to AI profile list |
| `314eea3ca` | Add Debug Episode prefabs |
| `a3ef1e268` | Update GameFeedSettings.asset |
| `24225a170` | Remove sprite |
| `227b9a5c1` | Update Main Menu to add episode debug system |
| `cae607240` | Add new elemental icons |
| `b6fb6e502` | Route benchmark sessions through Arcade bootstrap flow |
| `cb699693d` | Point benchmark session at ArcadeGames SO list, cap intensity 1-4 |
| `7bf231fa5` | Use OrganicRematchGames SO instead of ArcadeGames for benchmark list |
| `18a67cc72` | Update Squirrel.prefab |
| `78915d84b` | Update Squirrel.prefab |
| `b97dafa25` | Press Go button and sample 20s of actual gameplay in benchmark sessions |
| `aa571c283` | Fix Go button timing: wait for scene init before pressing Go |
| `e617f0aea` | Fix benchmark Go button timing: increase init delay from 3s to 8s |
| `1f7e85293` | Fix vessel selection: LaunchArcadeGame now sets selectedVesselClass |
| `4708c94a5` | Move guaranteed shape spawns to membrane-edge cluster to prevent immediate shape mode activation |
| `16c2b3773` | Remove print() calls from singleton Awake methods and add OnDestroy cleanup |
| `e4e2491a2` | Optimize physics settings for mobile performance |
| `2147975d4` | Add Main Menu benchmark target to session benchmark tool |
| `acfb90736` | Remove unused UnityEngine.Rendering import |
| `1e3c07198` | Migrate Debug.Log calls to CSDebug.Log across Game/ and Utility/ scripts |
| `02f683dab` | Remove Sirenix/Odin Inspector references from Soap plugin |
| `ae26879a0` | Move editor scripts to Editor folder to fix player build errors |
| `ac63345b1` | Fully qualify Editor base class to avoid namespace conflict |
| `3c01f874d` | Remove duplicate serialized fields from HexRaceHUDView |
| `bbc5e2cb5` | Bump Android target SDK from 33 to 35 |
| `1b0bd1c03` | Pull mobile input support from development branch |
| `39d893866` | Mark BaseInputStrategy.Ease virtual so TouchInputStrategy can override |
| `9b222e994` | Add missing ActiveInputDevice to IInputStatus and HasActiveModal to ScreenSwitcher |
| `7d1a101e2` | Fix currency claim: placement-based crystal rewards for multiplayer |
| `b2f0c8e31` | Fix joust end screen: correct winner display, add menu return, filter feed |
| `7c20deb63` | Update Squirrel.prefab |
| `db96055ea` | Update OrganicRematchGames.asset |
| `6ad5c138d` | Fix intensity unlock: initialize all fields in GameProgressionRepository.OnAfterLoad |
| `59862f5a8` | Fix intensity unlock not showing until app restart |
| `6df1954f2` | Fix arcade modal reopen bug and intensity unlock display |
| `42256251c` | Fix modal reopen: guard DisableWindow and prevent double-close |
| `f210086f0` | Fix arcade modal reopen: handle external SetActive(false) bypass |
| `8a2187717` | Fix intensity unlock for Crystal Capture: read CrystalsCollected not Score |
| `030cce882` | Fix systemic HandleGameEnd never firing across all game modes |
| `c6a4f5f6a` | Fix intensity always reading 1 due to Soap ScriptableVariable scene-load reset |
| `ca176aa07` | Cache played intensity in DontDestroyOnLoad singleton before scene load resets it |
| `0f8d22aac` | Set cached intensity directly from configure modal on DontDestroyOnLoad singleton |
| `cac002a0c` | Don't let CaptureIntensityOnLaunch overwrite cached intensity set by configure modal |
| `769d4f6ea` | Restore SelectedIntensity after Soap resets it on scene load |
| `417a6801f` | Update MainAIProfileList.asset |
| `f4217aa07` | Update GameModeQuestList.asset |
| `8bccfd994` | Add Squirrel in Main Menu |
| `ab29af991` | Add new froglet.games site (design-3-hyperdrive) to docs/ for GitHub Pages |
| `b9f010f54` | Revert "Add Cosmic Shore landing page with Hyperdrive design system" |
| `9b664d2eb` | Unlock all Freestyle intensities from the start |
| `b9a19d73e` | Fix multiplayer never activating from loadout view + NullRef crash |
| `88fdce98e` | Fix multiplayer not activating: read player count from gameData, not stale MiniGame statics |
| `1e22d8ceb` | Update Menu_Main.unity |
| `1504d370f` | Add meta files |
| `eedcdd0d4` | Add initial minigame scene (Drag Scouting) |
| `f34e8e492` | Add initial minigame scene (Echo Fight) |
| `9af008574` | Add initial minigame scene (Explosive Joust) |
| `4e1d59256` | Add initial minigame scene (Needle Threader) |
| `906d3a59c` | Add initial minigame scene (Wildlife Blast) |
| `772c87206` | Add initial minigame SO (Drag Scouting) |
| `7eefdfb51` | Add initial minigame SO (Echo Fight) |
| `3581de7cb` | Add initial minigame SO (Explosive Joust) |
| `6ed8d9812` | Add initial minigame SO (Needle Threader) |
| `12e2fd878` | Add initial minigame SO (WildlifeBlast) |
| `af9c86652` | Add Drag Scouting game mode scripts and configuration |
| `cbbac6c1d` | Generalize NetworkCrystalCollisionTurnMonitor to support any crystal race controller |
| `5f482da1b` | Unlock Explosive Joust (MultiplayerJoust) and all its intensities from the start |
| `651e00894` | Unlock Drag Scouting and all its intensities from the start |
| `fbde918d1` | Update MinigameDragScouting.unity |
| `b317469b5` | Add meta files |
| `2b0758b07` | Update SO_Class_Manta.asset |
| `94b1f5f23` | Update ArcadeGameDragScouting.asset |
| `95c8f4fc6` | Update OrganicRematchGames.asset |
| `1bac46c99` | Update MinigameHexRace.unity |
| `456147297` | Update EditorBuildSettings.asset |
| `f90039acd` | Fix game not ending: move ReportLocalPlayerFinished before telemetry |
| `b3c73ffe1` | Add Drag Scouting End Game Cinematic Defination |
| `2430242c0` | Add intial game scene (Dog Fight) |
| `afd768f40` | Include initial game scene (Missile Dog Fight) |
| `78255ac9e` | Rearrange Folders and add Inital Game Mode SO for Dog Fight and Missile Dog Fight |
| `046837c2e` | Add Dog Fight and Missile Dog Fight game mode scripts and configuration |
| `cd0ba2272` | Replace shared Joust modifications with standalone Dog Fight scripts |
| `06e212f49` | Update EditorBuildSettings.asset |
| `406163151` | Update MinigameDogFight.unity |
| `0077befba` | add meta files |
| `6c19ff43e` | Update OrganicRematchGames.asset |
| `5b3cabced` | Add new event |
| `18f715823` | Modify Dog Fight Asset |
| `b91611bd1` | Update SparrowFullAutoProjectileImpactContainer.asset |
| `520ada315` | Add Dog Dight Event Effect SO |
| `9e91d8351` | Add DogFightHits stat to IRoundStats/RoundStats, decouple from Joust |
| `fff557162` | Add analog trigger controls for Manta vessel |
| `75bf7a5c2` | Update MinigameDogFight.unity |
| `b44a48b1d` | Update Missile Dog Fight |
| `6939d85d4` | Add Impact Effects |
| `331e55823` | Update AOEExplosion.prefab |
| `6b42979ff` | Consolidate hitsNeeded to turn monitor, remove intensity scaling, add missile config |
| `cc1d7858c` | Update MinigameDogFight.unity |
| `785382f03` | Update MinigameMissileDogFight.unity |
| `75dffae34` | Fix missile scoring, auto-configure missiles, fix AI for DogFight modes |
| `e9c0229bb` | Fix missile timing race condition, add explosion hit debug logging |
| `801a56603` | Update MinigameDogFight.unity |
| `cb5faf4ba` | Update MinigameMissileDogFight.unity |
| `d18e2cc07` | Update Sparrow.prefab |
| `f679a417a` | Fix DogFight scoring, hide missile icons, always unlock DogFight modes |
| `e6ceaf23e` | Fix DogFight scoring: effect SOs directly increment DogFightHits |
| `a1b16f490` | Update DetonateEndEffect.asset |
| `244b550be` | Remove all debug logs from DogFight scripts |
| `919cd89f0` | wired up the manta controls |
| `4880301ca` | simplified yawstery |
| `d90586b0c` | reduced lerp |
| `6386ce76f` | no more lerp on yawstery |
| `bc45bec79` | Replace SkyBurst missile prefab with updated version from sparrow-missile-launch-animation branch |
| `841ed711c` | Fix Arcade vessel selection always loading Dolphin |
| `be40b39ef` | fix(compile): resolve 4 critical compilation errors |
| `330f0351a` | fix(compile): resolve 18 compilation errors across 9 files |
| `4aeefbb67` | fix(compile): resolve 16 compilation errors across 6 files |
| `29b9771d1` | fix(compile): remove obsolete SO_Captain field from PlayfabProductGenerator |
| `d0c0802a0` | fix(compile): add missing using for ElementalFloat in editor script |
| `b330f9436` | fix(cloud-data): wait for UGS initialization before accessing AuthenticationService singleton |
| `b98a922b2` | refactor(core): migrate UGSDataService from singleton to Reflex DI |
| `4da161ec6` | fix(core): add missing ScriptableObjects using for AuthenticationDataVariable |
| `3b6d3ed60` | fix(core): restore UGS using directives in PlayerDataService |
| `06d305e6c` | Update Bootstrap.unity |
| `b7d7434fe` | Redesign Dialogue Editor into Tutorial Sequence Editor |
| `c07102f1d` | Update Menu_Main.unity |
| `30dcaf727` | Redesign Tutorial Sequence Editor with pastel UI, flowchart preview, and per-instruction SOAP events |
| `803e774bd` | Fix dialogue editor: dark theme, typeable text fields, instruction preview panel, left panel alignment |
| `c1b864cb4` | fix(ui): wire player name display to PlayerDataService in ProfileScreen and HomeScreen |
| `a89321021` | refactor(ui): use OnDisable for HomeScreen event unsubscription |
| `3c965aedf` | Update Menu_Main.unity |
| `204b9de83` | Adjustable UI push |
| `059e0f4fe` | Update Menu_Main.unity |
| `fc94c65e2` | Replace player count cards with +/- stepper and add domain selection UI |
| `b6597e74e` | Update Menu_Main.unity |
| `9bf0130de` | Add TeamInfoData component and fix GameCard null reference |
| `be47d861e` | Update Main Menu + Add meta files |
| `40f8d4545` | TeamInfoData: toggle avatar container visibility on selection |
| `8ad87abe1` | Update prefab and main menu scene references |
| `dd1f93d0b` | Wire startGameRequestedEvent to ArcadeExploreView.PlaySelectedGame |
| `fdf0d1603` | Update Menu_Main.unity |
| `a4d9dd2f2` | Update assets + Update Main Menu |
| `111cee1a6` | Add four-player support: raise player cap to 4 and always fill with AI |
| `591d97197` | Increase Freestyle cell membrane to 4x diameter and shrink trigger shapes |
| `86900f8d6` | Fix Freestyle cell: rebuild assets from originals for proper Unity YAML format |
| `f8ab1ac3c` | fix(scenes): fix namespace mismatches and HexRace scene setup |
| `c6de4271a` | Update MinigameHexRace.unity |
| `fe7197eb9` | Update MinigameHexRace.unity |
| `d483b10e7` | refactor(arcade): merge NetworkTurnMonitorController into TurnMonitorController |
| `f62b61748` | refactor(core): convert SceneLoader from NetworkBehaviour to MonoBehaviour |
| `dff92cbdf` | Fix Cell config deserialization by adding FormerlySerializedAs attribute |
| `a233befa8` | Updated Freestyle membrane size |
| `e4cdf4cf6` | Assigned CellConfig to prefab |
| `8a3499573` | Update Bootstrap.unity |
| `b9d4514b3` | Update Bootstrap.unity |
| `e4c268f92` | Update MinigameCrystalCaptureMultiplayer_Gameplay.unity |
| `65f269d4e` | Update MinigameJoust_Gameplay.unity |
| `3c7aa5854` | Update DefaultNetworkPrefabs.asset |
| `d89875a42` | fix(ui): use DI for GameDataSO and HostConnectionDataSO in ArcadeGameConfigureModal |
| `39c132c14` | Correcting sizing bug on Shape drawing triggers |
| `a2f8fdca4` | Update Menu_Main.unity |
| `7e4111e57` | Update ArcadeGameHexRace.asset |
| `6702e3748` | fix(hexrace): replace no-op EndGame() with HasEndGame => false |
| `8e5b72ae3` | Fix scoreboard to show actual winning team in co-op multiplayer |
| `ed8aff903` | Add team count stepper UI and integrate with domain assignment |
| `2f4c56f1f` | Update game modes to support 4 players |
| `e94244617` | Update Menu_Main.unity |
| `ec58e431a` | Add meta files |
| `faf44812e` | Add dynamic AI player spawning to fill minigames to 4 players |
| `89de28760` | fix(prism): make PrismFactory persistent DI singleton and add scene transition cleanup |
| `eae0810de` | Add team-based scoreboard for 2v2 multiplayer view |
| `3a315ec6b` | Create PrismManager prefab |
| `eb0cbbc44` | Update Bootstrap.unity |
| `283e43875` | Update MinigameHexRace.unity |
| `2771fbafb` | Fix NullReferenceException in LoadoutSystem.SaveGameLoadOut |
| `84932b17d` | Update MinigameHexRace.unity |
| `e6f055b1d` | Update GameCanvas.prefab |
| `e5e6f5325` | Update MinigameJoust_Gameplay.unity |
| `5d292104b` | Update MinigameCrystalCaptureMultiplayer_Gameplay.unity |
| `f0e02f029` | Add meta |
| `5637b8702` | Fix NullReferenceException in LoadoutSystem.LoadGameLoadout |
| `77be9b7da` | Rollback dialogue editor redesign (revert claude/redesign-dialogue-editor-S3y0D) |
| `b26a73efe` | Fix NullReferenceException in LoadoutSystem.LoadGameLoadout |
| `4758fafb3` | Fix NullReferenceException in LoadoutSystem.LoadGameLoadout |
| `ab4d2a347` | Fix LoadoutSystem initialization order — use lazy EnsureInitialized |
| `d1cd6ef21` | fix(snow): guard against NaN localScale in SnowChanger |
| `c1fbaa7a5` | fix(hexrace): add ClientRpc to ensure track spawns on non-host clients |
| `57dfabc9b` | fix(party): handle client connection timeout gracefully instead of triggering full recovery |
| `1894a7a78` | Update Bootstrap.unity |
| `4770c5423` | fix(party): prevent Menu_Main multiple reloads from Start/HandleSignedInEvent race |
| `0b9d06d98` | fix(party): fix background-thread crash in WaitForSceneLoadAsync timeout |
| `b65a99bdc` | fix(party): await in-flight session creation instead of returning early |
| `660dc0d2b` | Update DefaultNetworkPrefabs.asset |
| `82327312b` | Delete Squirrel.prefab |
| `bfd2e96f6` | fix(hexrace): add client-side polling fallback for track seed sync |
| `f9d8b500b` | fix(hexrace): add missing using System for OperationCanceledException |
| `dae5096cc` | fix(hexrace): resolve Random ambiguity between System and UnityEngine |
| `de870dc8d` | fix(hexrace): fix client track spawn race condition from migration timing |
| `fa5075c1d` | fix(hud): handle OnClientReady race condition on multiplayer clients |
| `54fc26253` | fix(party): prevent arcade panel from blocking client connections |
| `08409dfaf` | fix(pools): release all active prisms on scene change for both host and client |
| `0d5c5fa3b` | feat(hexrace): add test override for crystal target count |
| `61e01ad8a` | fix(crystals): sync crystal domain colors from server via NetworkList |
| `6590a90a2` | Update MinigameHexRace.unity |
| `8ce729b5d` | fix(crystals): write n_Domains before n_Positions to fix synchronous callback timing |
| `c007f0e62` | fix(crystals): merge n_Positions + n_Domains into atomic CrystalSlotData NetworkList |
| `c28e9e741` | fix(hexrace): add server-side winner detection for end game flow |
| `8fe8fc65c` | feat(hexrace): use full scene reload for Play Again to reset environment |
| `63db12723` | fix(hexrace): prevent client disconnect during replay scene reload |
| `8e338f1ba` | Add UI Migration Files |
| `864a931a6` | Update Menu _ Main |
| `afc87c402` | fix(ui): implement IScreen on HangarScreen for auto-population on navigation |
| `11e3a6188` | Update menu |
| `879c86d42` | fix(ui): add arcade modal support, inline steppers, domain selection |
| `461c87059` | Add relevant sprites |
| `adba3cc67` | Update Menu_Main.unity |
| `59d2f4331` | Add meta files |
| `f3d8eaade` | fix(ui): stop disabling ship Image component and nav buttons in config modal |
| `53807fb87` | Revert "fix(ui): stop disabling ship Image component and nav buttons in config modal" |
| `93e18ecad` | Update SO_Arcade Game Assets |
| `45637e908` | Update Menu_Main.unity |
| `5127498e8` | fix(ui): fix game launch in solo mode, close modals on freestyle toggle |
| `1f30c6c18` | refactor(ui): clean up ScreenSwitcher inspector fields |
| `dd2864e0c` | feat(ui): add screensCanvasGroup to ScreenSwitcher for freestyle toggle |
| `be1f820a3` | Update Menu_Main.unity |
| `543603304` | Add sprites |
| `1a310a189` | Update Menu_Main.unity |
| `3bf66f216` | feat(ui): add FriendInfoSlot and FriendsListPanel for arcade party UI |
| `22d7fba4d` | fix(ui): remove invalid SOAP namespace import from FriendsListPanel |
| `be6403f93` | feat(ui): rewrite FriendsListPanel with MVC entry components |
| `93576e952` | refactor(ui): separate OnlineInfoEntry from FriendInfoEntry |
| `826a2630f` | refactor(ui): remove old party/friend UI scripts, add ShowOnlineTab |
| `660ad8d6c` | Add sprites |
| `f05d18897` | Add meta files |
| `bf76c4c7f` | Update main menu + Add Friends Prefabs |
| `01f8f0fd6` | fix(ui): rate limit on invite, null guards with toast, modal reopen bug |
| `5560bbce8` | fix(ui): modal reopen with external deactivation detection, add Hangar nav |
| `668d01006` | debug(ui): add temporary debug logging to ScreenSwitcher navigation |
| `104517ffb` | fix(ui): restore NavActiveImages/NavInactiveImages for nav icon switching |
| `ae2ebcd35` | Update Menu_Main.unity |
| `50d996d5d` | Update Crystal.prefab |
| `2ddfe73a9` | fix(crystal): use prefab domain and respect spawnCrystalWithPlayerDomain in NetworkCrystalManager |
| `f7a9d9bb8` | fix(ai): allow AIPilot to target crystals without Cell in Menu_Main |
| `8c053fca5` | Update AIPilot.cs |
| `0358c370d` | fix(player): initialize Domain to Jade to match NetDomain default |
| `8fe5966c4` | fix(crystal): remove CanBeCollected domain check from OmniCrystalImpactor |
| `0eab7a253` | fix(vessel): add explicit Domain property to VesselStatus |
| `a4f7ccbf8` | fix(vessel): remove default interface implementations for Domain and PlayerName |
| `0268b21e7` | revert: undo VesselStatus.Domain fixes, restore AIPilot workaround |
| `f06d50b1f` | fix(ui): guard against null SO_ArcadeGame in GameCard.UpdateCardView |
| `807b2093e` | Update MinigameHexRace.unity |
| `7cfa8bd1c` | Add minimum players |
| `7d6d07588` | fix(multiplayer): team count pipeline, quick play, daily challenge, and modal persistence |
| `000d4bb45` | Create QuickPlayButton.cs.meta |
| `9f3d78fbd` | Update Menu_Main.unity |
| `2239dcde1` | fix(gameplay): crystal domain enforcement, CTA duplicates, and GameCard event bug |
| `3e2133436` | fix: remove duplicate CallToActionTargetType enum entries |
| `43009b8ab` | fix(multiplayer): AI team assignment now reads humans from NetworkManager |
| `c849d746f` | fix(ai): AI now only seeks crystals of its own domain |
| `8744b80d7` | fix(multiplayer): respect human's chosen domain for team assignment |
| `4bb375e57` | fix(hexrace): respect chosen domain for teams + add gyroids to Barren Cell |
| `b4da7776d` | revert: remove gyroid flora from Barren Cell (not wanted in HexRace) |
| `2e87fe617` | fix(hexrace): change cell type selection from Random to IntensityWise |
| `b151145a5` | fix(environment): add Start() to Cell for deferred DI event subscription |
| `d91b930a9` | fix(environment): clear stale CellRuntimeDataSO.Config on scene load |
| `b6077f8fc` | feat(crystal-capture): add server-authoritative game logic, configurable end conditions, and solo play |
| `7f8dea961` | fix(crystal-capture): cap MaxPlayersAllowed at 4 |
| `5ac830159` | refactor(turn-monitors): decouple NetworkCrystalCollisionTurnMonitor from HexRaceController |
| `56f7e51e1` | refactor(turn-monitors): use SOAP IntVariable instead of direct monitor reference |
| `15227b666` | refactor(hex-race): eliminate all cross-system direct references via SOAP |
| `1e7120c12` | refactor(game-data): unify winner/crystal-target state on GameDataSO |
| `7ee549365` | refactor(joust): decouple monitor↔controller, match Crystal Capture pattern |
| `250c69dca` | Initial changes |
| `c554e630c` | fix(ui): remove duplicate fields from HexRaceHUDView that hid base class members |
| `9506f0285` | Update MinigameHexRace.unity |
| `5d304d10c` | fix(arcade): guard CountdownTimer.BeginCountdown against uninitialized sprites |
| `845714e33` | feat(ui): add team scorecards, stats provider, and golf rules tracking |
| `d5fa2a9c8` | refactor(turn-monitors): remove duplicate crystal target override fields |
| `8ae42f2c6` | Changed some sounds |
| `a2b8cd8d7` | fix(ui): prevent phantom Duel for Cell modal on return to menu |
| `b57614aae` | Changed projectile SFX |
| `7a3889c7f` | fix(multiplayer): preserve NetworkManager when returning to menu from game |
| `2ae2a74b8` | Update MinigameCrystalCaptureMultiplayer_Gameplay.unity |
| `b965c42e3` | Update MinigameHexRace.unity |
| `0b9984606` | Update MinigameJoust_Gameplay.unity |
| `a48b80aaf` | fix(scenes): standardize 4 spawn points at 100x100 square corners |
| `17b07103b` | fix(multiplayer): recreate party session when returning to menu from game |
| `f3a8db08b` | fix(hex-race): place spawn points near track start facing +Z |
| `fb15e6f71` | fix(hex-race): restore spawn points to dev branch positions near track |
| `7ac504b11` | fix(crystals): wire strictDomainOnly into OmniCrystalImpactor domain check |
| `cb2508b38` | fix(crystals): simplify OmniCrystalImpactor — always collectible by any vessel |
| `9a157ec81` | Update MinigameHexRace.unity |
| `be3d75666` | fix(menu): prevent stale Arcade modal from auto-opening on return to main menu |
| `212230dfb` | fix(scene): break infinite Menu_Main reload loop on return from game |
| `180356cd8` | fix(party): stop camera/UI flashing from party session recreation loop |
| `a7ae4102c` | fix(ai): default crystal domain to None so all AIs target them |
| `6d08fa999` | fix(joust): fix endgame never triggering — add HasEndGame suppression and fix collision event |
| `a9b2e5363` | Update MinigameJoust_Gameplay.unity |
| `3fb2e050d` | fix(joust): break infinite recursion in collision sync handler |
| `61a022393` | Updated audio |
| `c0a0e93fd` | SFX 2 |
| `51edd78d1` | sfx 3 |
| `524c724f4` | fix(party): fix invite popup not showing on client |
| `f22cd812f` | fix(party): unpause game before invite accept to prevent LobbyPatcher crash |
| `b4f357197` | fix(party): prevent rate limit from destroying party session after invite accept |
| `f981fc301` | fix(party): fix client vessel not spawning after invite accept |
| `8289939f5` | fix(multiplayer): retry vessel spawn when host player has stale VesselType |
| `17aaabf4f` | fix(menu): remove duplicate InitializeGame call that spawns extra crystal |
| `b48109fdc` | feat(crystals): add CrystalCountMode for modular crystal count control |
| `c5c332b96` | Changed menu SFX |
| `3ee55486f` | feat(ui): port team scorecards and stats providers to Joust and CrystalCapture |
| `f64943d6b` | fix(data): restore IsGolfRules property to GameDataSO after merge |
| `1a5689f5e` | fix(multiplayer): show lobby setup panel on clients by keeping ModalWindows active |
| `b2a8754d2` | debug(multiplayer): add diagnostic logging for client-side modal open flow |
| `0441cb635` | Update Menu_Main.unity |
| `38d082aa6` | Update Menu_Main.unity |
| `0b34ad3c3` | Reset crystal count in Hex Race to Default |
| `04310b119` | feat(prism): prototype octahedron supershield system |
| `2aa760d4a` | feat(prism): wire octahedron shield into test prefab + BlueBlock |
| `06402b0af` | fix(prism): tester uses Input System instead of legacy Input |
| `4b8f0574b` | feat(prism): octahedron shield engages for every shielded prism |
| `5af113909` | fix(environment): clear stale CellRuntimeDataSO.Config in OnEnable before event subscription |
| `46812eb98` | fix(multiplayer): prevent client SceneLoader from racing server scene loads |
| `0d9111b01` | feat(prism): per-face bloom morph for octahedron shield |
| `acb70427b` | feat(prism): shatter VFX on shield disengage |
| `3bc9dc071` | Tweaks made to forcefield material |
| `75be7c574` | Add forcefield crackle effect to replace SkimmerFXPrismEffect |
| `43cd7b81a` | Add Unity assets: shader, material, prefab setup, and SO wiring |
| `1a6fcadcc` | Expose serialized visual params for forcefield crackle effect |
| `5fdf50588` | push fiddling |
| `474e6d37d` | Replace voronoi crackle pattern with FBM-based electrical arc effect |
| `4c66d6a2f` | Add edit-mode preview for forcefield crackle visual params |
| `17571c2f1` | Pushing fiddle |
| `9db7c7711` | Rebuild forcefield crackle: controller owns visual params, fix fresnel |
| `48b71a015` | Fix forcefield crackle not rendering when camera is inside sphere |
| `748144c55` | Tweaks made to forcefield material |
| `8913a539f` | fix(vessel): align crackle namespace with app-shell-polish-v2 layout |
| `d50ff1e39` | fix(multiplayer): spawn AI with destroyWithScene=false so clients see them |
| `5d9b8c47c` | Update MinigameHexRace.unity |
| `691878ca7` | feat(ui): unified per-player Scoreboard with domain-tinted cards |
| `244deb607` | fix(ui): AIProfile is a struct, not a class — remove null check |
| `3d41ce801` | fix(ui): separate in-game PlayerScoreEntry from end-game PlayerScoreCard |
| `c7c5d9b7b` | fix(stats): robust EventDrivenStatsProvider — explicit wiring + no cache wipe |
| `ed4d2a426` | Add Player Score Entry |
| `65decb006` | Add meta files |
| `4d9add280` | Update Player Score Card |
| `e0dac3d0d` | Update scenes with new UI |
| `dd5e4e960` | fix(arcade): don't show ReadyButton on first round — let cinematic finish |
| `527535c02` | fix: sync username from profile + ReadyButton starts hidden |
| `873d59bf1` | fix(player): use IsLocalUser guard to prevent AI name collision |
| `111105eec` | Disable Ready Button |
| `dd1f4de05` | fix(player): refresh username from profile on PrepareForNewScene |
| `68f5a4fc6` | fix(ui): lock ReadyButton until cinematic completes — no bypass possible |
| `c0d5eb8a7` | fix(multiplayer): domain selection, scoreboard buttons, friend name and presence |
| `07cdbff58` | Update Menu_Main.unity |
| `4c6485bea` | feat(ui): redesign FriendsListPanel with Online + Requests tabs |
| `78c1511d9` | chore(ui): drop unused per-tab refresh button fields from FriendsListPanel |
| `224ed672c` | refactor(ui): drop tab switching from FriendsListPanel — render both sections |
| `5a1df5c32` | refactor(ui): drop labelStatus, pendingState, and add-friend from list entries |
| `11e488564` | Add emergent-systems design guidance to CLAUDE.md |
| `e8509cf6e` | Refine emergent-systems guidance with canonical fundamentals |
| `aff497e67` | feat(ui): pending-invite juice + seamless accept flow + panel re-hydration |
| `4dc65074d` | Delete old prefabs |
| `e5110b41b` | Create new Friend List Prefabs |
| `3b6a37034` | Update main menu to support ArcadeLobbyList |
| `5d3f9f2df` | feat(party): ArcadeLobbyList widget + cross-panel invite sync + profile republish |
| `5f4aa49ee` | feat(party): in-match status badge + leave-party invite resolution |
| `c1fbedead` | fix(party): robust invite flow — identity fallback, re-fire guard, presence-join fallback |
| `1fc0da1e9` | fix(party): survive scene-reload wipe + auto-open invites + simplify lobby header |
| `b74a311c5` | fix(party): remove scene reload on invite accept — direct-join transport swap |
| `14c0c8961` | fix(party): resolve profile before lobby join + clear stale pending badge |
| `a41b76f58` | fix(party): activate avatar/name GameObjects on FriendInfoSlot populate |
| `996b3ac9f` | fix(party): ensure remote member display names always render |
| `5b6220f09` | Add event for On Invite Resolved |
| `c652feacf` | Add meta file |
| `6a1c550fd` | Update Menu_Main.unity |
| `bbec8740b` | fix(party): survive shared displayName/avatar refs in ArcadeLobbyList |
| `cb45ae338` | Update Menu_Main.unity |
| `08dad3dfe` | fix(scoreboard): share team victory across winning domain in scoreboard and end-game |
| `ed83b8922` | Update Menu_Main.unity |
| `6b3c65b2d` | fix(startup): recover from UGS rate limits and null deps so Menu_Main always loads |
| `fb90eff58` | fix(daily-challenge): use Try-pattern since DailyChallenge is a value type |
| `ecf9bbbc6` | fix(auth-scene): start local host fallback when Relay host times out |
| `ea2031af2` | fix(joust): wire CountdownTimer so Go button starts the game |
| `25e8dbd52` | fix(party): make invite + accept flow feel instant instead of polled |
| `767229001` | fix(replay): make Play Again host-only in multiplayer |
| `7026435ad` | refactor(replay): unify Play Again through MiniGameControllerBase |
| `a035f7a8b` | refactor(replay): require serialized gameController; drop FindAnyObjectByType |
| `1dc98377f` | Update MinigameHexRace.unity |
| `05e6fbc48` | Update MinigameJoust_Gameplay.unity |
| `4b023cc73` | Update MinigameCrystalCaptureMultiplayer_Gameplay.unity |
| `d752702b8` | chore(replay): remove stale EventOnClickToRestartButton refs from scenes |
| `27c97dce9` | fix(replay): ensure client's vessel accepts input after Play Again |
| `c231be1d8` | feat(menu): wire MiniGameHUD pause button to exit freestyle |
| `e39d7f2af` | fix(menu): toggle pause button visibility with freestyle transitions |
| `9714dd753` | Update Menu_Main.unity |
| `641d3b067` | feat(menu-camera): add runtime-switchable camera mode for menu↔freestyle |
| `744832feb` | feat(menu-camera): shorter blends, FOV punch, 2 new modes, random switching |
| `e0b24a085` | fix(menu-camera): use BindingMode.LazyFollow (Cinemachine 3 rename) |
| `cd01a05fd` | feat(menu-camera): replace VesselFixedAim with VesselTopDownPan |
| `bf6c8dfdc` | Update Menu_Main.unity |
| `b5272109b` | fix(party): wait for client scene-sync before completing invite accept |
| `52e6b3cb2` | fix(party): isolate post-accept signal + improve diagnostics |
| `cc72f60df` | fix(party): instant UI refresh on invite accept (client row + host status) |
| `bf0484610` | update capsule membrane and camera configuration |
| `b8bf3bbc9` | feat(tools): add Video Recording Tools window with modular macros |
| `4a1d35645` | enbiggen capsule membrane |
| `50d94590b` | feat(input): add dual-mouse strategy |
| `078816323` | Hexrace shielded track |
| `a40728402` | fix(input): cast Mouse.all enumeration via InputSystem.devices |
| `9d525408e` | auto create files in unity |
| `ee5af3e20` | auto create files in unty |
| `2e980c1c5` | fix(input): re-apply Mouse type filter lost in merge |
| `1841972f1` | feat(input): swap pitch and roll mapping for dual mouse |
| `ba0845f2d` | fix reparameterized values |
| `9ae225f8f` | feat(input): make dual-mouse opt-in via simultaneous LMB gesture |
| `dc5f9c48a` | tweak starting parameters for mouse controls |
| `208a1cd03` | tweak mouse parameters |
| `4647d72ea` | FMOD and everything ive done is now here! |
| `22dfce589` | small changes to audio mixing |
| `ec3042958` | fix(telemetry): make boost-multiplier threshold serialized so Squirrel's MaxBoost can register |
| `8bfb5f545` | chore(fmod): gitignore runtime editor log |
| `f2c8f97f3` | chore(fmod): mark native libs as binary in .gitattributes |
| `c742d2e69` | chore(fmod): gitignore the auto-regenerated bank-scan cache |
| `0f0a7d358` | fix(telemetry): defer SOAP subscription so [Inject] gameData is populated |
| `963e6acdf` | Create Editor.meta |
| `16f954ee0` | engine mix change |
| `0af0e9340` | feat(cell): cherry-pick LiveBlockCount + flora primitives from lifeforms branch |
| `b14847777` | feat(cell): add prism-count phase system with hysteresis + per-biome thresholds |
| `182e100da` | fix(cell): drive phase locally so it works without CellNetworkSync wiring |
| `f9789abe0` | feat(cell): bring Hypersea fauna online with aggression-driven goals + data wiring |
| `97884ee06` | fix(menu): force menu autopilot vessel into Jade domain (configurable) |
| `aa48e06c3` | fix(multiplayer): server-authoritative NetDomain + balanced AI domain assignment |
| `dffc4d8e9` | fix(menu): re-paint vessel mesh after domain swap, not just material reference |
| `6e26b431f` | fix(vfx): prevent PrismImplosion(Clone) animation from looping after the pool callback fails |
| `31a08b5fa` | fix(vfx): broaden PrismImplosion watchdog to ungate IsActive + add zombie audit |
| `ef07a2a96` | fix(party): multi-target invite slots + reliable pending-state lifecycle |
| `252d30e92` | fix(menu): make MainMenuController menuVesselClass dropdown actually control the autopilot |
| `20569c526` | diag(vfx): add periodic implosion-population stats + position-aware watchdog log |
| `ad14c29f0` | fix(prism): dedupe Damage/Consume on already-destroyed prisms |
| `3ad91e9a9` | fix(party): use IReadOnlyPlayer for TryFindIncomingInvite parameter |
| `bce49f43d` | fix(party): collapse invite slots into single composite property |
| `0790358a9` | feat(domains): collapse Domains enum to {Jade,Ruby,Gold,Blue}, live cross-client picker, deterministic AI fill |
| `b6b969589` | fix(domains): keep original Blue=3/Gold=4 ints, drop migrator tool |
| `a779ee0a7` | fix(modal): repoint stale DomainInfoData GUID in ArcadeGameConfigureModal prefab |
| `4f41ad2df` | chore(modal): delete orphan ArcadeGameConfigureModal Variant prefab |
| `19e464047` | fix(modal): retag Random tile from old Unassigned(0) to Blue(3) |
| `90b0bbf9a` | feat(ui): add off-screen objective indicator with HexRace + Joust providers |
| `98a3358d1` | chore(modal): drop dead legacy single-avatar path from DomainInfoData |
| `c956e2900` | fix(ui): auto-bootstrap objective indicator + correct anchoredPosition math |
| `af8644087` | fix(player): Player prefab NetDomain default 0 (deleted Unassigned) → 1 (Jade) |
| `336fba0df` | Add AvatarStrip and DomainAvatarChip prefabs |
| `1242dc0d0` | fix(modal): adopt pre-placed DomainAvatarChip children into the runtime pool |
| `1070f5060` | Update Menu_Main.unity |
| `d0703ef9d` | configure tool in main scene |
| `d6fc41f81` | debug(modal): keep chip Image visible on null sprite + diagnostic logs |
| `f810ad228` | refactor(modal): per-player chip ownership, surgical reparenting on pick |
| `b35806585` | fix(modal): use NetworkVariable<Domains>.OnValueChangedDelegate, not Action<T,T> |
| `2c9a6709e` | fix(picker+party): always show 4 tiles, Random rolls real domain, party-refresh circuit breaker |
| `340659ffa` | Update Player.cs |
| `3cf890ded` | Update Menu_Main.unity |
| `8d5d84568` | fix(modal-prefab): wire domainInfoItems list + modal chipPrefab; strip dead per-tile chipPrefab serialization |
| `b4710a434` | removed fmod debug menu |
| `7ad6e78a2` | fix(party): never auto-create party session in menu — only on first invite |
| `b29dbb557` | Skim sound added |
| `64412bfdc` | sfx slider issues fixed |
| `0325361d7` | skim sound mixed louder, Added Shield remove sound |
| `9549fad23` | Full SFX mixing pass |
| `19c6f3d8d` | feat(vessel): add stellated octahedron super-shield (Stella Octangula) |
| `3fe5f7ea5` | feat(segment-spawner): diagnostic toggle to super-shield track prisms |
| `4ac45f01c` | feat(hexrace): default the track to super-shielded prisms |
| `257186554` | fix(party): defer Relay session creation until first invite accept |
| `528d1be44` | fix(segment-spawner): apply super-shield to all track prisms |
| `631800f17` | fix(party): drop IsHost gate on acceptance scan; keep mutex through CreatePartySession |
| `87ccc7d39` | fix(party): add lobby refresh before republish save; add acceptance-scan diagnostics |
| `8db9d72c4` | fix(party): prevent duplicate party session creation that kicks joined client |
| `906aa819f` | refactor(party): apply SOLID principles to HostConnectionService |
| `6bb64354e` | feat(prism): super-shielded = invulnerable; HexRace consistent prism spacing |
| `5db8a1f09` | fix(prism): close two super-shield decay paths missed in the first pass |
| `663c5dc28` | feat(editor): PrismShieldPreview component for edit-mode shield preview |
| `bd7d69c87` | feat(editor): SegmentSpawnerPreview gizmo tool for track layout |
| `7a071e2b0` | feat(party): Phase 1 — add PartyStateMachine + wire into HostConnectionService |
| `8e42132c9` | feat(party): Phase 2 — add 6 service interfaces for DIP |
| `5f918b8f2` | feat(party): Phase 3 — extract LobbyPropertyWriter |
| `b824a57a0` | feat(party): Phase 4 — extract SoapPartyEventBus |
| `ccf10b094` | feat(party): Phase 5 — extract InviteService |
| `a018ed5f3` | feat(party): Phase 6 — extract LobbyRefreshScheduler |
| `b41d81452` | feat(party): Phase 7 — extract PresenceLobbyService |
| `f3cb12df7` | feat(party): Phase 8 — extract AcceptanceSignalService |
| `bffb5eeb3` | feat(party): Phase 9 — extract PartySessionService |
| `cc2843c12` | fix(editor): SegmentSpawnerPreview now spawns proxy GameObjects |
| `b1cd89653` | feat(party): Phase 10 — extract PartyMemberService |
| `690a3a99f` | feat(party): Phase 11 — extract NetworkTransitionService |
| `b2911ec26` | refactor(party): Phase 12 — register party services in Reflex DI |
| `b8439cc69` | refactor(party): Phase 13 — FriendsInitializer event-driven init + IPartyStateQuery |
| `cf69983ac` | test(party): Phase 14 — add PartyStateMachine unit tests |
| `042cdcd5b` | chore(tools): drop unused imports and unused Cell.CurrentMembrane getter |
| `84f50fdf5` | Revert "Merge pull request #514 from froglet-studio/claude/add-video-macros-tab-qCZyu" |
| `65e7a0a71` | prism tweaks in hexrace |
| `3655e25b5` | feat(party): Phase 15 — Always InParty state model |
| `bf251b1a5` | fix(party): add missing using directives for cross-namespace types |
| `967f1b9ab` | fix(party): add missing using CosmicShore.Utility in PartySessionService |
| `911c8f31d` | fix(party): resolve compile errors after Phase 15 refactor |
| `4d7ce98c5` | fix(party): await session creation in SendInviteAsync instead of hard-abort |
| `a81376585` | feat(party): splash gate — hold overlay until Relay session is live |
| `f0b9af0fb` | feat(party): preserve Relay session on game-end + remove rogue StartHost calls |
| `9cc9a4fdc` | feat(party): show splash immediately on accept-invite and leave-party |
| `2c15982d4` | test(party): update PartyInviteSystemTests for Always InParty model |
| `b3fe142dc` | fix(party): remove spurious AsUniTask() on UniTaskCompletionSource.Task |
| `c2865c8b0` | fix(party): switch to main thread before UGS CreateSessionAsync |
| `7e096d4d1` | refactor(party): migrate all UGS async entry points from Task to UniTask |
| `49b6a4186` | fix(party): clear splash screen + retry transient session errors on invite accept |
| `f413637de` | fix(party): broaden transient SessionException retry to cover error 23006 |
| `2d2b34c79` | chore: add tracked _logs/ folder for sharing Unity Editor logs |
| `7aceaa171` | Revert "chore: add tracked _logs/ folder for sharing Unity Editor logs" |
| `a6c25b9f5` | rhino sword |
| `167df05f3` | debug(ui): strip ObjectiveIndicator to always-visible magenta box at right edge |
| `2ef317fa5` | fix(ui): enable dynamic objective tracking now that rendering is confirmed |
| `123b81c0a` | feat(ui): swap magenta box for procedural glowing arrow |
| `8b29e455c` | feat(ui): chevron-hexagon arrow shape + lime-green palette + smaller icon |
| `a9aca25ac` | feat(tools): density-partition benchmark scene + design audit |
| `09abb951f` | fix(tools): decouple Toolbox Density tab from runner type at compile time |
| `bda57037b` | fix(tools): generate density benchmark scene programmatically |
| `377560701` | fix(tools): drop value-tuple constructs from benchmark + add diagnostic |
| `e71fa6373` | fix(tools): replace invalid GUIDs in benchmark .meta files |
| `0b4854c67` | feat(tools): density benchmark iteration 1 — sharper diagnostics, lower error floor, faster GT |
| `664bf666e` | feat(tools): density benchmark iteration 2 — meaningful mass%, stability metric, faster, deeper |
| `3154a1f8c` | feat(tools): density benchmark iteration 3 — real §2.3.1 test, mean-shift, jitter floor |
| `5de25183b` | refactor(multiplayer): unify PC/DC stepper, drop DomainAssigner, Jade-default + deterministic AI placement |
| `59fd8ff53` | Update Menu_Main.unity |
| `09f29509d` | Update ArcadeGameConfigureModal.prefab |
| `ba0895710` | Update Menu_Main.unity |
| `d9bb2b055` | Update Menu_Main.unity |
| `7188c0808` | fix(ui): auto-sync DomainInfoData label text to Domain enum |
| `e47d00be2` | Update Menu_Main.unity |
| `008b66d34` | Update ArcadeGameConfigureModal.prefab |
| `319ce3b8a` | refactor(ui): defer arcade config modal-open to host commit |
| `3b00c9156` | Update Menu_Main.unity |
| `75f4877ca` | feat(tools): density benchmark iteration 4 — real cell scale + production-grid-undersizing test |
| `c7cf63dd2` | fix(tools): auto-version the density benchmark config so stale scenarios self-refresh |
| `c05866396` | fix(density): size the density grid to the cell + add smoothing/interp + fix bucket staleness |
| `936a44c3f` | feat(tools): temporal ecology sim — does the density fix produce oscillation, not plateau |
| `ec6d37293` | feat(scoring): domain-aggregated end conditions for HexRace, Joust, Crystal Capture |
| `0948d722a` | fix(multiplayer): split HostConnectionDataSO.IsHost into IsPresenceLobbyHost + IsPartyHost |
| `bc9afeb42` | fix(tools): one-click heal when the benchmark scene is missing the temporal sim component |
| `8111215c2` | feat(ui): wire DomainScorePanel prefab into the shared multiplayer HUD canvas |
| `6256b3760` | merge |
| `58dd4abc7` | fix(multiplayer): split HostConnectionDataSO.IsHost into IsPresenceLobbyHost + IsPartyHost |
| `034c8a83a` | feat(ui): tone down DomainScorePanel + move ally panel to LEFT of player score |
| `b2fa96745` | feat(ui): theme-aware DomainScorePanel + vertical alignment + clip avatars |
| `5bc51cc37` | fix(party): stop benign SDK errors from respawning host vessel; add boot status UX |
| `8ef428c4e` | Update Authentication.unity |
| `06fb785f1` | Update Bootstrap.unity |
| `3ae6cf04c` | refactor(bootstatus): decouple BootStatusPanel via SOAP request + retry channels |
| `14114ecbb` | Update Bootstrap.unity |
| `8bb5383aa` | Update Authentication.unity |
| `cc93a6367` | Update Bootstrap.unity |
| `a55fb9343` | refactor(auth-scene): single canvas-level background; remove duplicate panel Images; rename confirm button |
| `1ea0592d4` | Big fmod push |
| `124d07c6c` | fix(party): switch back to main thread after UGS-SDK awaits |
| `e39c22893` | fix(party): replace UniTask.SwitchToMainThread with Yield(Update) |
| `e67cf819c` | refactor(party): single AsMainThread boundary helper for UGS awaits |
| `6a544e30e` | fix(threading): marshal AsMainThread via Unity's SynchronizationContext |
| `b63511cf9` | perf(ui): cut ObjectiveIndicator.LateUpdate cost |
| `c5b1d4e7a` | perf(ui): eliminate per-frame Canvas rebuild from ObjectiveArrowGraphic |
| `b7efb66b8` | perf(hexrace): make objective provider event-driven, kill per-frame scan |
| `8f3c6d41c` | fix(hexrace): restore missing CosmicShore.Utility using for GameDataSO |
| `af2ecc1b2` | chore(ui): drop diagnostic Debug.Log noise from objective indicator path |
| `ce1908fa9` | fix(hexrace): point objective indicator at the local player's crystal |
| `9dbd2f77a` | perf(environment): bake CapsuleMembrane wobble into a reusable preset |
| `f2f1c9f1d` | fix(crystals): tag omni crystal prefab as neutral Blue domain |
| `1b468a72c` | ccommiting baked membrane |
| `a197efb7d` | fix(party): arm splash fade-out when accepting party invite (Bug B) |
| `edfa1be59` | fix(party): never clear party session on refresh failure (Bug A surgical) |
| `97320568e` | feat(squirrel): buff allies on overtake instead of debuffing them |
| `537e309b9` | feat(elementals): standardize temporary/permanent buff-debuff API |
| `2be3c8796` | fix(joust): score joust points only on opponent overtakes |
| `be187f996` | fix(tools): temporal sim fauna sweep their goal region instead of parking |
| `c02f0697a` | chore(party): remove dead methods + lobby-patcher log filter plumbing |
| `5c206b46e` | feat(vessel): tint trail renderers per domain |
| `7b54c8492` | refactor(party): introduce IsInitialized/IsInPresenceLobby/IsHostingParty helpers |
| `eb2cb8e03` | refactor(party): EnsurePartySessionAsync as canonical create-or-no-op surface |
| `06d633353` | chore(party): improve KickPartyMemberAsync catch diagnostics |
| `74d17c8be` | refactor(party): event-driven Start + WaitForProfileInit (no polling) |
| `509137b66` | refactor(party): unify gameData.ActiveSession + PartySessionService.ActiveSession |
| `17b4acbf7` | refactor(party): cache IMultiplayerService field in PartySessionService + PresenceLobbyService |
| `f4b8e133a` | refactor(party): LeavePartyKeepHostAsync as canonical leave-to-solo surface |
| `6f586656a` | feat(party): classify definite session-gone vs transient refresh errors |
| `3655a94fd` | feat(party): auto-recover UI state on definite session-gone |
| `dbd6d587b` | fix(party): guard OnDestroy against null fields + duplicate-instance teardown |
| `fd8696941` | fix(party): resolve MultiplayerService.Instance lazily, not at construction |
| `6c6ca377d` | refactor(party): drop IMultiplayerService injection seam, use plain property |
| `9206e700d` | Update Authentication.unity |
| `a9c006fa1` | Update HostConnectionData.asset |
| `83f0c022c` | Update List_OnlinePlayers.asset |
| `b8393625f` | Update List_PartyMembers.asset |
| `ca43c7c5f` | fix(party): stop phantom "Unknown Player" in solo party panel |
| `291bdafbd` | fix(party): converge presence lobbies so MPPM clients discover each other |
| `de6e61cac` | fix(tools): cluster temporal-sim flora so the density field has real peaks |
| `540703b49` | fix(party): subscribe to OnSignedIn so party/friends init actually runs |
| `9a49dd28e` | chore(party): remove temporary [PARTY-DIAG] confirmation logging |
| `aceac4160` | fix(party): keep joining client's menu vessel alive through scene-sync |
| `63fbf19ba` | fix(persistence): open save files with shared access for concurrent readers |
| `7e241b051` | fix(party): client-pull roster bootstrap with retry + terminal watchdog |
| `6b0fef0da` | feat(party): expose party-session PlayerLeaving event |
| `27bdb87ef` | feat(party): add grace-bypassing ReconcilePartyMembersNow |
| `fb0707cd2` | fix(party): reconcile host roster + clear invite on member leave |
| `67658f13a` | fix(party): reconcile host roster on Netcode client disconnect |
| `d35608ccc` | Fmod test |
| `1cdcf9cda` | fix(party): suppress benign LobbyPatcher ArgumentOutOfRangeException (B1) |
| `cc31fd83f` | fix(party): also drop benign LobbyPatcher error on the LogFormat route (B1) |
| `68b723103` | Update NetworkManager.prefab |
| `dca3a4ae4` | fix(party): gate host→client transition on full NM reset + leave own session before join |
| `7d489ef53` | fix(party): destroy orphan vessel before failed-transition recovery reload (B8) |
| `acec3344e` | perf(camera): disable redundant CinemachineBrain camera in minigame scenes |
| `257927aa2` | chore(camera): remove vestigial Game Scene Main Camera prefab from 13 scenes |
| `0c4e977c6` | fix(perf): stop ShipAudioController re-searching for nonexistent StudioListener every frame |
| `46544db2c` | perf(vessel): stop ClearPrisms cloning prism materials every frame |
| `df0d2ad66` | perf(prism): drop per-frame ToArray allocation in MaterialStateManager |
| `6d7b33906` | perf(vessel): de-allocate TrailViewer hot loop and drop boid log spam |
| `0c5eecd53` | feat(benchmark): capture gameplay load counters per frame |
| `9ed5f5b09` | feat(benchmark): live HUD overlay + multi-scene sweep |
| `a7f426af6` | Audio optimization |
| `ca1b6bd52` | specialization update |
| `4da0ae260` | mix update |
| `4db1b9134` | feat(benchmark): CPU/GPU split, spike attribution, score + actionable hint engine |
| `9e4144a82` | feat(benchmark): pick-a-scene auto-Play start + primitive error-detection sweep |
| `cbd4726ed` | feat(benchmark): rebuild editor window — Collect/Sweep/History/Compare, pastel UI |
| `5982105a2` | test(benchmark): cover CPU/GPU stats, leak slope, score, grade, hint engine |
| `a7b92c84b` | fix(benchmark): resolve EditorUIStyles Badge name collision |
| `157cbd75b` | perf(benchmark): zero-alloc end-of-frame collector + persist Collect results |
| `e3c5fac37` | feat(benchmark): netcode (NGO) instrumentation tool-side + report schema/source |
| `b79ba3e6d` | feat(netcode): instrument central NGO hot paths with NetMarkers |
| `2f7254611` | feat(benchmark): dev-build self-capture + History import + cross-source guard |
| `4a00ee4de` | test(benchmark): netcode stats/hints + schema legacy-load + source stamping |
| `8f84c802a` | perf(logging): kill SOAP debug-log spam, throttle prism VFX audit, gate spawn logs |
| `754d1515e` | feat(tools): temporal sim now runs OLD vs MID vs NEW (mean-shift) |
| `8e6718609` | fix(party): surface + retry transient JoinSessionByIdAsync failures on invite accept |
| `d43fe0357` | feat(ui): add crisp single-petal element sprites for elemental bars |
| `a10478775` | feat(ui): per-petal elemental flowers matching the -5..15 spec |
| `443c4ffde` | perf(impact): use concrete ImpactCollider lookup in OnTriggerEnter |
| `ca598a468` | fix(tools): temporal sim NEW now tests 32³ + mean-shift, not just mean-shift |
| `80fac06e6` | feat(ui): make ElementalBarsView self-wiring (zero manual setup) |
| `a0167618f` | fix(tools): default fauna orbit radius 60m → 120m to match algorithm scale |
| `253331b3a` | fix(ui): restore CosmicShore.Gameplay using for HapticController |
| `759839c02` | feat(ui): reuse prefab-authored petals by name (no runtime duplication) |
| `fb5587666` | fix(ui): correct element petal shapes (charge pentagon, space kite) |
| `22cd232c7` | fix(ui): rebuild element petals to exact 5-fold angle spec |
| `e605fd393` | fix(ui): resize mass to match space flower; charge = 120/120/114/114/72 |
| `9a3316c42` | fix(ui): clear old elemental bars, center flower row in Squirrel HUD |
| `d2ee89a33` | refactor(ui): extract shared ElementalBarsConfigSO; make bars a reusable widget |
| `d1f08a7e8` | fix(party): guard presence-refresh from transport-swap churn (accept/leave) |
| `545ea4fe5` | reposition elemental ui |
| `494f70328` | fix(ui): keep elemental flower arranged when buff and debuff interrupt |
| `a1a8eb97f` | fix(party): close in-flight RefreshAsync race during accept transition |
| `aaba872e6` | diag(party): add NetDiag overlay for party/lobby/session/transition catches |
| `70ae31b16` | diag(party): route NetDiag + NetworkMonitor logs through CSDebug; label timeouts |
| `5b1b32a33` | diag(party): drop two cleanup-catch warnings to info (CSDebug.Log) |
| `60c076c3c` | diag(party): close NetDiag coverage gap at party-session-refresh catch + log MPPM Session 1 |
| `9df956fd1` | feat(density): Phase 2 — adaptive resolution, voxel mean-shift, result caching |
| `6b136b3e5` | perf(environment): spread initial flora/fauna spawn batches across frames |
| `64d8f0c87` | perf(menu): bound the lava-lamp autopilot trail with a ring-buffer cap |
| `5a634c878` | fix(party): silence B1+B6 SDK stale-index NRE churn at the catch (Editor + release safe) |
| `50ba868a3` | muted start up noise |
| `52e4f4a6f` | feat(ecology): flora reawakening + dispersed planting — schools get real targets |
| `09ece59d4` | feat(perf): standalone per-frame Profiler CSV logger + editor menu |
| `d2288bd7f` | fix(party): match benign SDK NRE by type+message, not stack (first attempt missed) |
| `019810752` | feat(perf): add per-frame script-marker columns to ProfilerCsvLogger |
| `06d41cc00` | feat(benchmark): script-focus + editor-noise filtering for spike attribution |
| `959c49516` | fix(party): extend benign-SDK-error matcher to cover IOOR message variant |
| `ba36af0ac` | feat(benchmark): ship default BenchmarkConfig asset + auto-load in window |
| `da0ad2cc4` | feat(benchmark): free-form (record-until-stopped) capture + live Spikes accessor |
| `a8c7208ec` | fix(party): match benign SDK SessionException by structured Error==Unknown, not message |
| `acd9cf16d` | feat(benchmark): Runtime Capture tab — free-form recording, live spikes, Copy-for-Claude |
| `b2284a63a` | perf(benchmark): move spike analysis off the game frame (kills the capture storm) |
| `f42e3b939` | feat(benchmark): Runtime Capture tab is now record-only (remove fixed-capture) |
| `480b29b86` | feat(benchmark): redesign Runtime Capture tab (state-driven, foldouts, filters) |
| `809572d2d` | feat(ecology): trail prisms feed cell density grids — fauna target ALL mass |
| `79c5a5e1c` | feat(environment): elemental crystals grant scale-based element powerups |
| `19a380d46` | diag(party): demote PartySessionService retry chatter to CSDebug.Log |
| `68afb42cb` | perf(hexrace): replace FindObjectsByType<Crystal> with a live-crystal registry |
| `db334ee7f` | feat(benchmark): low-overhead smoothness mode + throttled repaint |
| `6e2331668` | feat(perf): F7 FPS counter HUD (dev build) + rename Copy button to "Copy error log" |
| `ad474e41d` | Add meta files |
| `ac5407e84` | feat(benchmark): manual-sweep data model + runtime session (errors + F8 marks) |
| `8110f9312` | fix(environment): destroy elemental crystals after collection |
| `9afab5b69` | fix(build): guard Bootstrap editor tests with UNITY_EDITOR so player builds compile |
| `371ba2046` | feat(benchmark): Sweep tab — Manual session mode (stats + error log + F8 marks) |
| `9dc0fd04d` | fix(scenes): remove dangling SceneRoots entries (Broken text PPtr on build) |
| `aaa2505af` | fix(build): guard all EditMode tests with UNITY_EDITOR (IL2CPP nunit link failure) |
| `cb65cf313` | fix(party): B8 fix 1 — cross-check presence-scan adds against authoritative session |
| `59fda81ea` | fix(party): B8 fix 2 — await the joined_party clear before leave teardown |
| `3e0c5bce1` | fix(party): B3.b — despawn pre-reload solo vessel to prevent orphan duplicate |
| `74cde7007` | refactor(party): sequence leave-flow scene-load before session recreate (removes B3.b band-aid) |
| `67fffa789` | feat(perf): uncap frame rate (BootstrapConfig targetFrameRate -1, vSync off) |
| `850a31377` | feat(perf): uGUI in-build diagnostics overlay (replaces F7 FPS counter) |
| `b2a00febd` | feat(diagnostics): dynamic panel resize, color, ping + region in DiagnosticsHUD |
| `7bfffd394` | refactor(diagnostics): organise HUD into an aligned label/value table |
| `6ebb3850b` | refactor(diagnostics): lay HUD out as two side-by-side blocks |
| `d816fde67` | Add meta files |
| `962ce8a13` | feat(hud): restore three-wedge per-domain volume indicator on the pause button |
| `5d57ac903` | fix(hud): missing CosmicShore.Utility using in DomainVolumeIndicator |
| `a264374a2` | fix(hud): IVessel access uses ITransform.Transform, not Unity transform |
| `1a9768a8e` | feat(hud): live diagnostic readout + widen Blob phase bands for visible cycling |
| `b8039a0fd` | fix(hud): domain volume indicator is now zero-authoring + self-diagnosing |
| `5ff33403d` | feat(hud): hexagonal domain-volume gauge (restore correct shape + radial fill) |
| `b245e41bf` | refactor(scoring): remove [FLOW-*] debug log spam from MiniGameHUD |
| `cc3ec9ec4` | feat(audio): attenuate and throttle BlockDestroy SFX to stop harsh stacking |
| `3f708a5b6` | refactor(scoring): extract ScoreNumberAnimator + CardEntranceAnimator (R2/R2b) |
| `80b14de44` | refactor(scoring): R9 — scoreboard banner uses authoritative WinnerDomain |
| `a8ecae8ae` | fix(hud): theme colors from gameData, hide host button face, axis-align readout |
| `47bf46c1c` | fix(scoring): B1 — hide empty DataPanels background on score cards |
| `ed6517abc` | refactor(scoring): R3 — single source of truth for winner crystal reward |
| `3aa3b5b7d` | fix(scoring): B6 — wire secondaryStatText on PlayerScoreCard prefab |
| `610975c75` | fix(menu): stop a nav tab disappearing at runtime |
| `d65a5ec67` | fix(menu): coalesce duplicate Hangar grid loads per frame |
| `a845283ef` | fix(menu): repoint profile + XP to live UGS PlayerDataService |
| `b89cedcb1` | fix(menu): guard currency balance parsing + robust toast container lookup |
| `12a6f0ada` | fix(episodes): restore episode card content wiped by reorg field rename |
| `e7e4e564a` | feat(hud): spawn-cycle ring + fix Ruby identity color + nuke leftover button face |
| `68550228d` | refactor(scoring): R4 — centralize loser-score sentinels in GolfScoreSentinels |
| `53b973b56` | refactor(scoring): R5 — one source of truth for domain colors |
| `1f498859a` | feat(ecology): retrofit RandomLifeSpawner onto the phase model |
| `f957dc0de` | refactor(scoring): single-source domain colors in the game feed |
| `96bb5fbb9` | refactor(scoring): single-source domain colors in vessel HUD + silhouette |
| `b0d30871f` | fix(scoring): B3 — remove dead scoreboardRowStagger config field |
| `8820f8c8e` | refactor(scoring): R10 Phase A1 — ScoreResult + GameDataSO.Results foundation |
| `d3305e62a` | refactor(scoring): R10 — add ScoreResult.ScoreText + shared FormatTime |
| `05b7c71ea` | refactor(scoring): R10 A2 — HexRace produces ScoreResult list |
| `ff69baf94` | refactor(scoring): R10 A3 — Joust produces ScoreResult list |
| `7478d0d99` | refactor(scoring): R10 A4 — CrystalCapture produces ScoreResult list |
| `6d241399b` | feat(ecology): prey-linked fauna — controlling-color spawns + starvation |
| `f1c72b1a9` | feat(ecology): scale up cell density + periodic flora regrowth |
| `a2a9dd9a1` | Renamed 'Hex Race' to 'Skim Race' and 'Crystal Capture' to 'Scurry'. Renaming was done in serialized field ONLY. Old names persist in code. |
| `3f1e44777` | Add meta files |
| `193f42a80` | feat(ecology): drop indicator numeric readout; tighten gyroid spawn radius |
| `f8060df77` | test(environment): guard elemental crystal powerup tests with UNITY_EDITOR |
| `af07a1712` | refactor(scoring): per-mode ScoringRuleSO for end condition + winner/score |
| `d25625399` | Update MinigameJoust_Gameplay.unity |
| `debb3239d` | Update MinigameHexRace.unity |
| `e57066b6a` | Update MinigameCrystalCaptureMultiplayer_Gameplay.unity |
| `3014de712` | refactor(scoring): unify multiplayer HUD on the ScoringRule metric |
| `7ea5b8ae0` | refactor(scoring): results SSOT — scoreboard + cinematic read the ScoringRule |
| `21d538d3b` | refactor(scoring): Joust replay via scene reload (match HexRace/CrystalCapture) |
| `10e541fc9` | refactor(player): collapse IsLocalUser to IsMultiplayerOwner (no offline single-player) |
| `fd0dee090` | fix(scoring): rank end-game vessel podium from Results SSOT (golf-aware) |
| `fa2515f71` | fix(scoring): rebuild client domain HUD reactively against replicated state |
| `4c6289df7` | feat(ecology): retire the flora regrowth pulse (growth-side cheat) |
| `96ba7e023` | feat(ecology): predator/herbivore diet split — two-tier food web |
| `65766fc3b` | feat(ecology): wire the 3-species food web — tadpole+brittlestar prey, shark predator |
| `e25290cc7` | fix(scoring): server-authoritative per-domain HUD sums (Approach B) |
| `8104d042c` | feat(ecology): wire fauna trail-management into Skim Race (2nd test scene) |
| `8fe9484fa` | joust framework added |
| `de5a7a80e` | feat(ecology): tadpoles = larger self-sustaining forager murmuration + crystal-anchored spawn |
| `2e0b25993` | fix(ecology): restore tadpoles (no-boids regression) + forager starvation bound |
| `4dc95258a` | Update MinigameHexRace.unity |
| `352ed485f` | fix(scoring): source RoundStats.Domain from NetDomain on all peers (client icon placement) |
| `f6da29195` | fix(ecology): remove predator from test scenes — it was eating all herbivores at spawn |
| `5442d3d00` | fix(scoring): group in-game domain HUD by Player.Domain (authoritative source) |
| `aaabc1b69` | refactor(scoring): retire RoundStats.n_Domain — derive domain from Player.NetDomain |
| `906ee8f80` | fix(ecology): widen tadpole forager reach so it actually grazes prisms |
| `911a8279b` | chore(scoring): remove dead domain-target helpers + their tests |
| `0b84560ba` | fix(ecology): tadpole forager gets health prism, suction, and any-domain voracity |
| `e999a2067` | feat(ecology): predation spawn-immunity window (dormant) + overnight log |
| `11161f395` | fix(ecology): forager must not eat other fauna's body prisms |
| `3a6dfab49` | test(ecology): edit-mode tests for CellPhaseRules (phase hysteresis spine) |
| `33f7ccb48` | test(ecology): enum drift guards for FaunaDiet, CellPhase, CellAggressionLevel |
| `83d5d986c` | feat(ecology): foragers seek mass concentrations across the whole arena (no track-following) |
| `ff1529b49` | feat(ecology): aggressive early/fast forager cleanup in Skim Race |
| `4c3a383bf` | comeback sounds added |
| `a741dfaa9` | feat(ecology): concentric-phase volume indicator in Skim Race |
| `ba5172ad8` | refactor(ui): make domain volume gauge the universal in-game pause button |
| `3d4bb5004` | feat(ui): hide volume-gauge phase rings once crossed |
| `2427600c7` | feat(ecology): steady flora growth/planting until Frenzy; collapse phase ladder 6->3 |
| `6d0112d0f` | Update Bootstrap.unity |
| `78972ee51` | feat(ecology): fauna reproduction retires the fixed-period spawner cheat |
| `543ff9ede` | feat(ecology): author reproduction into test biomes; re-add shark to Blob |
| `36fcc729c` | feat(ecology): predators hunt nearest live herbivore; diet-aware seeding |
| `f33abbb69` | fix(multiplayer): keep party together on host return to main menu |
| `6b936df0e` | perf(ecology): menu 5fps fix (lower Blob prism ceiling + fauna caps) + headless tuning loop |
| `781314cfe` | fix(ui): hide Main Menu button for non-host clients like Play Again |
| `7a89ef303` | feat(impact-effects): danger prisms debuff all elements for 4 seconds |
| `9aa4b2626` | Update README.md |
| `85bd9aab4` | fix(impact-effects): danger prism elemental debuff applies regardless of domain |
| `147b7a3f3` | feat(impact-effects): triple prism slow duration on danger prisms |
| `a54a2a404` | feat(impact-effects): volume-scaled prism slow with 3x danger max, 10x danger skim energy |
| `7b00887ec` | feat(benchmark): live CPU/GPU bound verdict + memory detail in dev overlays |
| `41912dc57` | refactor(scene): drop non-networked scene-load path |
| `52c822ec2` | refactor(ui): collapse dead single-player branch in pause-menu gating |
| `7d27c08b0` | refactor(ui): remove scoreboard single-player banner + redundant guard |
| `974aaa2f3` | tune(impact-effects): make Squirrel danger prism slow perceptible |
| `42b09d36c` | chore: drop dead refs left by the host-return cleanup |
| `3f59eb155` | Update NetworkManager.prefab |
| `8b24f08df` | Update ArcadeGameMultiplayer2v2CoOpVsAI.unity |
| `e52ab68ed` | Update MinigameDuelForCellMultiplayer_Gameplay.unity |
| `297e8853c` | Update MinigameFreestyleMultiplayer_Gameplay.unity |
| `ef6488ab5` | Update MinigameHexRace.unity |
| `ee8060a4f` | fix(build): guard PartyAcceptFlowPlayModeTests with UNITY_EDITOR (IL2CPP nunit link failure) |
| `7b1c2880d` | Update MinigameWildlifeBlitzMultuplayerCoOp.unity |
| `ff74881c7` | Update MinigameTournamentMultuplayer.unity |
| `e2b61189f` | fix(vessel): stop throttle modifiers compounding into smoothed speed; overtune danger slow |
| `3fa4d2b0c` | fix(prefab): remove duplicate DontDestroyOnLoad on NetworkManager |
| `13a4da0cd` | fix(impact-effects): danger prism slow always lands at the danger max |
| `9a95a523f` | refactor(impact-effects): danger prisms affect all domains everywhere; document locked rule |
| `44bc7fea4` | balance(ecology): tame the menu food web so gyroids stay sizable (fly-through) |
| `6d99c0d1f` | feat(flora): add Schwarz P flora that self-assembles the minimal surface |
| `38c16963e` | feat(spatial-index): unify prism spatial systems into PrismSpatialIndex (phase 1) |
| `2009ae546` | refactor(arcade): remove vestigial standalone Freestyle game, document lava lamp == freestyle |
| `48a6d06ea` | resize schwarz leaf |
| `234fb247a` | feat(spatial-index): extend TryReserve occupancy to SchwarzPAssembler |
| `53294068d` | fix(vessel): repaint already-painted hulls in ShipHelper.SetShipProperties |
| `65d4da968` | fix(menu): reset NetDomain server-side when menu vessels spawn |
| `c073636ec` | fix(menu): remove client-local domain writes from MainMenuController |
| `dc3692eb3` | perf(spatial-index): ship Phase 2 — QuerySphere neighborhood views replace physics queries against prisms |
| `e2cfdfcc7` | fix(arcade): fail loud and resolve local player robustly for modal picks |
| `ec64dde26` | refactor(spatial-index): ship Phase 3 — cell density grids driven by the index lifecycle |
| `f8697b90f` | fix(ui): client loading splash on game start + retry button only on failure |
| `52923bf80` | fix(gamedata): purge stale RoundStats shadows from the client roster |
| `6400eca08` | fix(arcade): color ready feed from live Player.Domain |
| `c2256ecdf` | feat(analytics): UGS-only analytics facade; retire Firebase analytics path |
| `44a1f264f` | revert(menu): remove lava-lamp trail ring-buffer cap — mass is conserved |
| `69b7e404e` | fix(party): stop spurious retry button on invite-accept and game-launch splashes |
| `d61736c51` | fix(bootstatus): scene-independent self-heal so retry button can never orphan |
| `59f25e1b0` | refactor(bootstatus): inspector-wired only — remove runtime panel recreation and auto-find |
| `061b8ec7e` | Revert "Merge branch 'bleeding-edge' into Ys-bleeding-edge" |
| `c710eddf5` | feat(ecology): adopt epic-darwin invariant re-assertions — sealed wither-to-crystal death path + crystal invariant + locked governance |
| `d04af84dd` | feat(ecology): re-assert no-domain-asymmetry spawn — all three domains seed flora |
| `d50487362` | Reapply "Merge branch 'bleeding-edge' into Ys-bleeding-edge" |
| `621c01a1a` | fix(menu): remove stale SetMaxTrailBlocks call left by merge resolution |
| `755e4746c` | feat(skills): add /reorient skill to resync with bleeding-edge and re-evaluate session direction |
| `671e8c9da` | Update MinigameHexRace.unity |
| `e21c778a1` | fix(arcade): retarget Play Again onClick at the scene-added Scoreboard (Joust + Crystal Capture) |
| `4861801bd` | fix(multiplayer): inject Reflex deps on Netcode-replicated vessels so client drift works |
| `d3cbbabb9` | fix(scoring): purge stale RoundStats subscribers so game end survives menu-return relaunch |
| `3a021e503` | feat(arcade): host-only scoreboard nav buttons + anti-spam hide on click |
| `5c86b8d5c` | feat(arcade): rebuild Astro League as multiplayer domain game with networked ball |
| `7ff345f68` | tune(ecology): fewer flora plantings, larger grown structures (gyroid + SchwarzP) |
| `cf6bf580a` | perf(aoe): port AOE explosion batch-processing fix from development |
| `f0f6d59b8` | feat(ecology): volume is the spine — phase, dominant, prey, and HUD key off live per-domain VOLUME |
| `9cc209257` | perf(ecology): proximity collider-LOD + budget telemetry; raise the Blob canopy ceiling; author fauna food-web timers |
| `d6d3b4c12` | fix(aoe): align benchmark helpers with the Phase 3 cell density view |
| `feb42f238` | feat(perf): Checkpoint A — instanced prism rendering via Entities Graphics companion entities |
| `28a085ed8` | feat(perf): Checkpoint B — explosion/implosion VFX on the instanced render path |
| `11ae1c67a` | fix(perf): harden the instanced render path — review findings from the 7-angle audit |
| `cff56e29a` | perf(prisms): Checkpoint C — population-independent collider-LOD sweep |
| `dbc7c703a` | refactor(scoring): remove end-game cinematic; Scoreboard is the sole end-game UI |
| `e683829d8` | fix(scoring): restore AICinematicBehaviorType enum after cinematic removal |
| `0ea12370c` | feat(enums): add GameModes.Tournament = 36 |
| `ce1acedda` | feat(data): add IsTournamentMode flag to GameDataSO |
| `cda2c1876` | feat(tournament): add TournamentDataSO SOAP container + asset |
| `d4dca263f` | feat(config): propagate IsTournamentMode to minigame clients |
| `50929947b` | feat(tournament): add TournamentStateMachine |
| `e53bc8104` | feat(tournament): add TournamentController persistent service |
| `fba938c88` | feat(di): register TournamentDataSO + TournamentController in AppManager |
| `dc168a88b` | feat(ui): Scoreboard Continue button + tournament button matrix |
| `ac969ca12` | feat(ai): stable AI identities across tournament games |
| `18a9ded9f` | feat(progression): unlock Tournament mode |
| `96915755e` | feat(tournament): add TournamentSceneView (lobby/intro) |
| `920e6ef42` | chore(scenes): rename orphan scene to Tournament.unity + register in build settings |
| `ba0d821b7` | feat(tournament): add Tournament arcade card + list entry |
| `9b9835113` | fix(di): capture serialized fields for TournamentController factory |
| `b13d315be` | feat(tournament): wire TournamentData.asset into AppManager prefab |
| `c27f23e4b` | Update OrganicRematchGames.asset |
| `15832646b` | Update GameCanvas-HexRace.prefab |
| `0762adc5f` | Update EndGameStatsPanel.prefab |
| `b85501b0f` | Update Tournament.unity |
| `45c268ede` | Update Tournament.unity |
| `333df0903` | fix(tournament): complete Continue button wiring on GameCanvas-HexRace |
| `5ef5cb9d5` | Update Tournament.unity |
| `50fe68d9d` | Update Tournament.unity |
| `7cca5fd19` | fix(tournament): wire Start Button onClick to TournamentSceneView.OnHostStartPressed |
| `a66c0aa49` | Update GameCanvas-HexRace.prefab |
| `c639bf6de` | Update MinigameHexRace.unity |
| `ea917522e` | feat(arcade): add EndConditionOverridesSO + Resources asset |
| `caf8a57a7` | refactor(arcade): end-game counts come from EndConditionOverridesSO, not per-scene fields |
| `93b4896e2` | feat(editor): Tools > Cosmic Shore > End Game Conditions window |
| `981d5c38d` | feat(analytics): Phase 2 event hooks — activation, economy, social, retention |
| `488aeb1ef` | feat(tournament): Continue on every game → Tournament Summary results screen |
| `440b0de9e` | feat(cloudsave): Phase 3 infrastructure hardening |
| `1587cdcf5` | feat(cloudsave): wire Daily Challenge/Training repos + add Squad/Loadout cloud repos |
| `4366cb535` | Update MinigameCrystalCaptureMultiplayer_Gameplay.unity |
| `72ad742bf` | Update Tournament.unity |
| `adc558f30` | fix(tournament): wire TournamentSceneView.onClickToMainMenu to the main-menu event |
| `c2075d561` | Update Tournament.unity |
| `849848e78` | Update MinigameJoust_Gameplay.unity |
| `6e0a48a50` | Update MinigameCrystalCaptureMultiplayer_Gameplay.unity |
| `8f7e6d9cc` | feat(privacy): opt-in consent flow + age gate + data deletion; privacy policy template |
| `3ababb021` | chore(analytics): remove stray Firebase comments from MiniGame/DailyRewardHandler |
| `2f4c46cf1` | chore(firebase): remove Firebase SDK entirely (UGS-only) |
| `660e4d91e` | fix(ui): stop scoreboard panel drifting off-base in Joust/Crystal Capture |
| `543fc2951` | Update MinigameJoust_Gameplay.unity |
| `9f46af06d` | Update Tournament.unity |
| `1ee5cdbc0` | feat(tools): track build values in End Game Conditions tool |
| `3e036f207` | refactor(tools): simplify End Game Conditions build-values to a toggle |
| `975271aab` | fix(ai): stop Joust AI flying straight when it loses its joust target |
| `22900f8bd` | fix(joust): default player + domain count to a minimum of 2 |
| `3fa229a50` | fix(build): unblock Unity 6.4 clean compile |
| `876589600` | fix(tournament): require a minimum of 2 players and 2 domains |
| `905d3b822` | Update Tournament.unity |
| `29ae9f258` | Update dependencies for editor version upgrade |
| `04a8ae3c4` | Add Unity AI Package |
| `834495626` | Downgrade project to LTS Version [6.3 LTS (6000.3.17f1)] |
| `353aecd91` | feat(scoring): remove end-of-game omnicrystal reward |
| `0ab9bc938` | feat(arcade): show mandatory connecting panel at game start |
| `fc759bd99` | feat(input): skip pre-game cinematic with gamepad A button |
| `403a41531` | feat(menu): config-driven progression unlocks + web-checkout IAP for episodes |
| `029914eec` | refactor(arcade): use authored connecting panel UI, drop runtime build |
| `cc92e3e43` | chore(iap): point web checkout at https://www.froglet.games |
| `4e5974ee5` | fix(profile): avatar selection NRE + instant UGS save + in-game sync |
| `3dcd507fa` | friend joust push |
| `27147f7c4` | chore(shuffle): show the Tournament arcade card as "Shuffle"; docs point Shuffle → Tournament |
| `2e10c68b0` | feat(shuffle): single-source the mode display name on the card; mode-agnostic description |
| `89fe35f11` | feat(shuffle): per-domain {2,1,0} placement scoring (was per-player {10,6,3,1}) |
| `e4ff1d302` | feat(shuffle): randomized lineup + race-to-6 / cap-7 (Tournament meta) |
| `ddd99ea41` | feat(shuffle): credit per-game placement crystals {2,1,0} to the wallet |
| `de41fc4b9` | feat(shuffle): between-game loading-splash summary overlay |
| `c5395ef2e` | refactor(shuffle): reuse the existing loading-panel TMP for the summary; restore its label on clear |
| `8de9bae7d` | refactor(shuffle): decouple loading-summary + wallet via SOAP/SOLID (no UI refs in services) |
| `a7a912927` | Update Bootstrap.unity |
| `f15622da9` | Update MinigameCrystalCaptureMultiplayer_Gameplay.unity |
| `6880d688d` | Update MinigameHexRace.unity |
| `92c7bbb17` | Update MinigameJoust_Gameplay.unity |
| `e982fdc31` | Update Tournament.unity |
| `7064d5033` | Update Bootstrap.unity |
| `5bdc2cd66` | feat(shuffle): disable end-game buttons on click + show splash ASAP |
| `66dcbbfd9` | feat(shuffle): show next game mode + intensity on the between-game splash |
| `82c012ad5` | added track sounds |
| `c4a0d98fa` | feat(shuffle): rename player-facing mode name Shuffle -> Maelstrom |
| `41e264c2b` | fix(profile): make menu header avatar track the live profile avatar |
| `04bdcf6a5` | feat(shuffle): hold the between-game summary splash ~2s before next load |
| `ce85bed74` | feat(shuffle): tag the owners domain row with (You) in Maelstrom standings |
| `16333f942` | fix(shuffle): resolve local domain on summary scene for the (You) tag |
| `ca3ffc33c` | fix(perf): root-cause the instanced-render breakage — shaders missing Hybrid Per Instance + make path opt-in |
| `f69bf7127` | fix(ui): remove invalid [SerializeField] on GameCard.Favorited property (CS0592) |
| `cb17fa673` | fix(hud): filter per-vessel boost/joust/drift HUD events by source vessel in multiplayer |
| `1c2022288` | perf(ecology): cache per-prism world-volume — kill the 23ms lossyScale walk |
| `3c93606b7` | perf(prisms): single-pass collider-LOD union + central octahedron-shield ticking |
| `161632ee5` | fix(prisms): restore Prism.Grow() accidentally dropped in the volume-cache edit (CS1061) |
| `7b3ee2dd2` | perf(prisms): strip 11 dead no-op EventListenerNoParam from trail prisms |
| `30e8a8926` | perf(prisms): cap concurrent explosion/implosion VFX (96ms -> bounded) |
| `10da9961b` | feat(maelstrom): rename Tournament scene + add per-round history model |
| `fb33e145c` | feat(maelstrom): route between-rounds through the hub scene (Phase 2) |
| `b494a7a86` | feat(maelstrom): host-authoritative ready/countdown for the hub (Phase 3) |
| `86bc56b3f` | feat(maelstrom): data-driven scene view + round/domain card components (Phase 4) |
| `d99adce3e` | feat(maelstrom): lightweight connecting reveal + polish (Phases 5-6) |
| `2f36cb367` | refactor(maelstrom): nested Tournament Data Card to match the UI design |
| `00d6781a7` | cleaned up the lighning effect for the shield |
| `0ab4b67af` | feat(maelstrom): Player Data Card with Round + Total scores |
| `9b58ffe2a` | fix(maelstrom): preview/undecided round shows "WINNING DOMAIN : —" |
| `f3c12424e` | fix(party): host-loss → client bounces to its own solo menu+host (B10) |
| `d4f2a8f51` | fix(party): show bounce/host-loss notice AFTER recovery so it isn't dropped (B10) |
| `44fd913e6` | fix(party): clear stale joined_party on host-loss recovery (B10 hygiene) |
| `4cd3dda65` | feat(maelstrom): v2 UI integration — animated countdown, domain-coloured names |
| `761c32946` | feat(party-ui): disable + relabel inviting a player already in my party (Task 1) |
| `5e3e6018a` | feat(ui): SOAP 1/2-button popup system + party-invite popup (Task 0 core) |
| `fd6a27eba` | feat(party): cancel-invite affordance + 10s invite lifetime (Task 0b) |
| `a294ee9aa` | Add Maelstrom Intro Panel UI |
| `4e771d3a5` | Update Maelstrom.unity |
| `71e8813dc` | fix(maelstrom): palette colours, NEXT→summary, auto-start, chronological scroll |
| `05e612bef` | feat(party-ui): show party member count on the IN YOUR PARTY row (2/4) |
| `83a202dca` | feat(party-ui): relabel online status "IN LOBBY"→"IN PARTY", "LOBBY FULL"→"PARTY FULL" |
| `99f5fd328` | refactor(party): invite popup reuses PartyInviteNotificationPanel; remove generic popup system |
| `810441e41` | feat(party): PartyInviteNotificationPanel 3s auto-hide + docs for the invite-popup replan |
| `ee1290fef` | feat(maelstrom): summary panel — per-player cards + animated domain rank |
| `acd1a57de` | feat(maelstrom): dedicated in-game connecting panel; clean the splash |
| `67aa83743` | feat(maelstrom): consolidate connecting panel → ConnectingPanelController |
| `a40a3cd4c` | Add Connecting Panel |
| `eb6393a74` | Add Maelstrom specific player cards |
| `b5fd2262e` | Update Maelstrom.unity |
| `55bdccd4b` | Update MinigameCrystalCaptureMultiplayer_Gameplay.unity |
| `63c678a1b` | Update MinigameHexRace.unity |
| `4b9084925` | Update MinigameJoust_Gameplay.unity |
| `c89a0e312` | feat(scoring): end-game reveal toast with randomized win/lose lines |
| `0e5782efc` | Update prefabs |
| `b965e528e` | fix(scoring): don't disable persistent InputController in end-game reveal |
| `380e1db50` | Update references |
| `2faf7f0e7` | feat(settings): PC settings backend + benchmark stress scene |
| `69f8468a5` | feat(settings): restore UI binding controllers (visuals stay author-owned) |
| `5d2d25887` | feat(settings): rework panel controller to shipped 4-tab layout |
| `d9191c8ff` | feat(settings): wire FOV + post-AA to camera via CameraSettingsApplier |
| `e2f5bcd7f` | feat(astroleague): bigger, near-frictionless ball that smashes prisms on bounce |
| `b3ef48de5` | unity created files |
| `659127596` | fix(prisms): stop per-explosion NRE storm from dead pooled instances (Joust int 3) |
| `fd6e758ca` | feat(astroleague): anti-clip ball, prism fresnel look, all six ships |
| `11883685f` | feat(astroleague): zero-friction ball; collisions are the only speed decay |
| `610229923` | feat(astroleague): intensity-scaled arena+ball, scaled team resets, vessel recoil |
| `a91ec82c7` | feat(astroleague): runtime icosphere mesh + angular/mesh ball settings |
| `5b24fe0db` | feat(astroleague): domain-aware ball — color, pass/shield, impulse spin |
| `e3868859c` | fix(astroleague): robust same-color pass-through + icosphere index format |
| `23a0f0cda` | fix(astroleague): register ball as collider-LOD focus so prism contacts fire |
| `cae4a3e82` | feat(astroleague): rocket-league ball model — elastic vessels, plow-through prisms |
| `b85b4b803` | fix(astroleague): ball clears/shields prisms reliably + accurate goals |
| `44a900045` | tune(astroleague): lighter prism drag, small rotational damping |
| `b0b699603` | unity auto changes |
| `f8cb9c2c0` | Update OnlineFriendsInfo.prefab |
| `1a4c66433` | Update Menu_Main.unity |
| `ad78f5454` | feat(party-ui): add shared anti-spam cooldown to online row invite/cancel buttons |
| `dc0c5620f` | fix(party-ui): point cancelButton at the Cancel Button, not the Accept Button |
| `072b7044b` | feat(party-ui): conditional invite/kick buttons on online rows + party slots |
| `e15603d80` | Update UI.prefab |
| `125603acc` | Create FriendsInfo - 1 .prefab |
| `9f2d60456` | Create FriendsInfo - 1 .prefab.meta |
| `756d72066` | Update Menu_Main.unity |
| `f86748001` | Update Menu_Main.unity |
| `495ca5e67` | fix(maelstrom): end the tournament on race-to-6 instead of starting another game |
| `f1b0deba2` | Update FriendsInfo - 1 .prefab |
| `f1f0a703d` | Update Menu_Main.unity |
| `e1c3727fe` | Update FriendsInfo - 1 .prefab |
| `74ec00d76` | Update Invite/Accept/Decline UIs |
| `f7d724342` | chore(party-ui): remove orphaned FriendInfoEntry + duplicate invite-popup prefab |
| `546799b18` | chore(party-ui): delete dead legacy party-UI prefab generation |
| `7ecde1092` | Create PartyInviteNotificationPanel Variant |
| `8fef24e27` | chore(party-ui): delete AddFriendPanel + sync all docs to current party UI |
| `d37b843d4` | ball tuning |
| `bd9c5c0ae` | fix(maelstrom): summary buttons launching a new game + standings ordering |
| `7f6bec727` | refactor(astroleague): cap intensity at 4x, remove bespoke MatchUI |
| `fd582e2a7` | feat(endgame): add Maelstrom win target to End Game Conditions tool |
| `5d622f628` | feat(astroleague): wire the standard cell ecosystem (fauna) into the arena |
| `b396ac98a` | tune ball and remove redundant scene objects |
| `30ea0db31` | refactor(astroleague): cell owns the environment — remove bespoke plankton + edge cage, tune fauna ladder for low-volume prisms |
| `a84742281` | fix(astroleague): add NetworkCrystalManager so the cell's fauna spawner actually starts |
| `5a8cdd339` | feat(astroleague): tone fauna to ambient levels + objective indicator tracks the ball |
| `20a59d57d` | feat(astroleague): spherical arena — the cell nucleus becomes the wall the ball bounces off |
| `78c47f018` | feat(astroleague): pluggable ricochet court geometry (bank off flat walls) |
| `6bef3d6b4` | refactor(astroleague): harden boundary geometry per adversarial review |
| `10ac3aa56` | feat(astroleague): new court set (Cylinder + NotchedRing), fix late-joiner arena sync, compress scale |
| `dcc4f1a54` | fix(vfx): suction sink tracks the moving fauna instead of a stale snapshot |
| `2cbe3a915` | feat(settings): apply FOV + post-AA from CameraManager (spawn-proof) |
| `7d5a14c78` | feat(settings): self-wiring panel controller (drag controls into slots) |
| `cb57161b1` | feat(settings): ON/OFF rows as separate ON+OFF buttons (not Toggle) |
| `916319a24` | feat(settings): add OptionsMenuContent Open/Close + clean inspector headers |
| `aac186488` | feat(settings): tab navigation + ON/OFF underline & scale juice |
| `1bd6482a4` | feat(settings): menu-only lock for big perf settings + restart notice + auto-detect log |
| `50d018766` | Update Menu_Main.unity |
| `c4f745c9d` | Add Options Menu Prefab |
| `5fab8aec0` | Update SettingsModal.prefab |
| `1fbee38f6` | Create Benchmark Stress Scene (v1) |
| `4fda2aed1` | perf(fauna): eliminate per-neighbor square roots in boid/fauna behavior loops |
| `985fcd06a` | perf(input,vessel): square-distance input deadzones + drop redundant normalizes |
| `08550ca33` | perf(impact): drop redundant normalizes + squared compares in skimmer/impact effects |
| `e5373d38d` | perf(projectiles): hoist/dedup magnitude + drop redundant normalizes |
| `21ee99c40` | perf(astroleague): compute striker speed once per ball strike |
| `472318c78` | perf(env,spawn,geometry): squared compares + reuse/redundant-normalize cleanups |
| `686935ccd` | spatialization fix |
| `6e90662af` | fauna sounds added |
| `9a17fbd13` | re added music (whoops) |
| `18c5caf89` | Create PrismRenderConfig asset |
| `d247ce769` | Create PrismRenderConfig asset |
| `398f0a698` | diag(perf): surface instanced-prism render path status on DiagnosticsHUD |
| `d9b8418e0` | mix changes plus hex race sounds added |
| `ceb1201e5` | new song plus mixing |
| `2bb203296` | one more mix pass |
| `ea9e0f363` | fix(perf): bootstrap a default ECS world on demand for instanced prisms |
| `9e22ef4bf` | feat(astroleague): central shared goal on i4 (sphere); tame vessel recoil |
| `472141ad4` | another mixing push |
| `00367ae29` | fix(camera): stop Manta follow-cam jitter from the snap/smooth toggle |
| `76e252fca` | fix(astroleague,camera): disable ball recoil (runaway throwback) + camera teleport snap |
| `b0960ec02` | fix(astroleague): stop continuous wall-bounce camera shake (Manta ball jitter) |
| `2b92df56b` | feat(toys): add freestyle Toy system + vessel/domain changers, revive painting toy |
| `443ab009c` | fix(impact): don't brake the vessel on its own trail (large shielded own prism) |
| `87d3fbecc` | diag(perf): log why a prism stays on the legacy render path |
| `1113dda55` | diag(perf): surface distinct mesh/material count on the Prism Path readout |
| `d1c7c8257` | perf(flora): batch spindles by dropping the per-spindle _Phase MPB |
| `8b1605bc2` | diag(perf): let the prism stress test self-bootstrap the ECS world |
| `4177b0a1e` | test(perf): ready-to-run prism instancing stress scene |
| `c8e964758` | push post tool use changes |
| `bac634129` | feat(toys): domain/vessel changer flip-sets, mini ship models, working painting toy |
| `5ff993ad1` | fix(toys): exit-gated re-arm + slow re-grow so swap toys can't switch you back |
| `39ff7acf3` | fix(toys): re-show the vessel HUD after a freestyle vessel swap |
| `846e49ae2` | feat(menu): gamepad Start exits freestyle; stop the pad double-driving UI |
| `b9622342e` | fix(toys): render every vessel as a mini model, not just Rhino |
| `ced396f59` | chore(editor): move the bootstrap play-mode toggle under Tools/Cosmic Shore |
| `746c48b35` | feat(diag): one diagnostics surface + service-routed stress harness + F10 injector |
| `311f554d6` | perf(prisms): settled octahedron shields render instanced + batch shared meshes |
| `e17124f1e` | chore(editor): keep the Testing Multiplayer label; remove the benchmark scene creator |
| `7d51d786c` | fix(prisms): name the shared-shield-mesh cache key tuple elements |
| `6f08f5f65` | feat(diag): command console in DiagnosticsHUD; retire the F10 hotkey |
| `b823a95ab` | fix(diag): stress-cloud prisms obey the project ColorSet |
| `d37e5776a` | fix(perf): match legacy color space on the instanced prism path |
| `e89e0d21a` | mix pass |
| `d98625ab5` | fix(toys): keep the chosen domain colour through a vessel swap |
| `d578b7aa3` | feat(toys): new ship inherits the previous ship's speed on swap |
| `17587e9da` | feat(toys): recolour all vessel-changer mini ships on domain change |
| `79ccfaf4b` | feat(toys): microscene conveyor toy (Wanderway) for freestyle |
| `0a4a0b198` | fix(toys): harden conveyor after adversarial review pass two |
| `d2d4a4070` | feat(toys): conveyor play-test rework — toggle, speed-scaled belt, 16 recipes |
| `44429079d` | perf(diag): kill the audit's scene scans; instrument impact + membrane hot paths |
| `29ae2643a` | tune conveyer: smaller crystals. less spacing. larger structures. |
| `90c7649cc` | feat(toys): break the Wanderway ribbon — place scenes on the live flight line |
| `89e7c548d` | feat(toys): hybrid ribbon conveyor — bends with gentle turns, breaks on sharp ones |
| `aa724f6a1` | feat(elementals): drain overcharged levels back to the resting band |
| `a624be4d7` | feat(spawning): unify environment/microscene spawning + expand microscene diversity |
| `08cbf06a4` | fix(spawning): rename Finalize->ApplyTheming to avoid object.Finalize collision |
| `44370c357` | fix(toys): re-run Setup Freestyle Toybox wires newly-added content fields |
| `c30647ac0` | Adjust toy conveyor pool size to 8 and 'ahead target scenes' to 4 |
| `350caa10f` | perf(cell): time-slice cytoplasm shard reorientation off the pickup frame |
| `0b3854e3b` | perf(prisms): pool crystal-ring prisms through PrismFactory |
| `ead0fcd42` | perf(crystals): pool spent-crystal husks; kill per-pickup material clones |
| `b7ea692ce` | fix(impact): latch crystal impacts per crystal; drop miswired collision reporter |
| `3f2e4f655` | perf(prisms): triple the crystal-ring pool buffer |
| `2e14555ab` | perf(bootstrap): warm recorded shader variants behind the splash |
| `79cd896c4` | feat(toys): monumental multi-stroke fly-by-numbers painting toy |
| `41711c450` | fix(toys): harden painting toy from 8-angle review pass |
| `1f6b69b64` | perf(fauna): stamp body-prism ownership; desync boid ticks; deepen implosion pool |
| `fdb62e63e` | ui sounds added |
| `19b7b5a46` | perf(fauna): pace boid consume cascades across frames, throughput preserved |
| `be8b1d145` | perf(ecology): run the densest-region job async instead of stalling the frame |
| `4b29ad508` | perf(ecology): slice the collider-LOD sweep across frames with prism stamps |
| `f2b9eb3e0` | perf(ecology): slice the cell volume recompute; publish sums atomically |
| `b0373252c` | perf(prisms): fuse the animation manager passes; drop the job round-trips |
| `a372782f3` | perf(cell): cadence-gate membrane matrices; de-LINQ crystal lookups |
| `c18af4922` | Add SwordFish_A Model |
| `660854e7b` | perf(prisms): budget creation completions per frame |
| `a4d77cfd8` | feat(toys): shared shape language, prism drawing-state persistence, web share export |
| `43f183f2c` | refactor(toys): unify fly-through gate builder, guard share export write |
| `b8e467068` | fix(toys): review fixes — capture intent, restore fallback, material cache, load-log stall |
| `0caca23af` | perf(prisms): attribute animation-manager cost; cache color endpoints |
| `0d18823d4` | fix(toys): missing CosmicShore.ScriptableObjects using in ToyFactory |
| `97d4ab291` | menu mix pass |
| `1de594008` | pushing changees made by unity |
| `ac7312a64` | fix(toys): physics-safe activation, visible share on desktop, crystal-spike markers |
| `669b5ef80` | feat(editor): raycast-target audit tool for EventSystem.Update cost |
| `a0b32006d` | pushing from unity |
| `0f3b08a18` | fix(toys): PaintingToy.Update no longer shadows Toy's exit-gated re-arm |
| `27860eaa2` | perf(prisms): make the growth step dt-linear, tempo-preserved |
| `4b36b7f89` | perf(prisms): slice the growth pass under a per-frame budget |
| `5f6b497a5` | perf(gc): collect the managed heap behind the scene-load splash |
| `546e2b98c` | perf(prisms): cache the spawn-window WaitForSeconds |
| `e04d5a723` | perf(pool): attribute buffer-refill instantiates with a per-pool marker |
| `d80e7ee53` | fix(prisms): calibrate growth tempo to the real 40ms tick; scale the slice dt cap |
| `4443df839` | perf(gc): collect on every peer behind the post-load fade |
| `d09d54510` | feat(squirrel): single-trigger drift + Oak Trunk danger-tube ability |
| `c8e0dae66` | pushing automatic unity changes |
| `019eb3c0f` | perf(ecology): pace flora grow-tick instantiation across frames |
| `fa51c2d3c` | feat(squirrel): representative orientation-locked tube preview + HUD cooldown icon |
| `5da476508` | perf(ecology): drive spindle fades via MaterialPropertyBlock, not clones |
| `d4f696ae3` | perf(prisms): split-attribute the creation-completion tick |
| `481a7ad8b` | perf(ui): gate the domain-volume gauge push on real change |
| `fc9c53f3d` | fix(ecology): seal the flora spawn drain against death, stale claims, and pause |
| `2f277a81f` | feat(toys): rename Painting toy to "Connect the Dots" + add 12 grandiose 3D constructions |
| `2c91b4c5e` | fix(toys): migrate stale on-ramp painting assets to the catalog + doc nit |
| `f9c3eae28` | refactor(squirrel): pool the Oak Trunk tube + project from vessel model |
| `2655dfe0f` | feat(squirrel): tube banks/swings with flight & drift (input-derived lean) |
| `06757157d` | fix(toys): a paused Connect-the-Dots run hides its blueprint |
| `72873bb05` | fix(squirrel): tube fires out the vessel's front, not its top |
| `229715ed3` | fix(squirrel): keep tube axis on the nose — pitch/yaw lean was tilting it out the top |
| `0a61e77f8` | tweaks etc. |
| `efd9aef59` | refactor(squirrel): simplify tube to place-in-front on press, led by speed |
| `a87b4dfc3` | feat(prisms): dedicated Boost prism pool (fast-grow, collider-immediate) |
| `b3b6cea07` | tweak tube |
| `549f3b104` | feat(toys): rebuild the 7 mathematical paintings at reference grade |
| `fdf9a87f0` | feat(prisms): deterministic boost rings — one builder for omnicrystal, joust, tube |
| `ad65947b1` | feat(toys): bake Lion + Starry Night from real references (Camp B pipeline) |
| `0fabff876` | fix(vessel): parent Rhino capsule skimmer to the puppeteered hull |
| `bd46eae8b` | push tweaks |
| `004e460c2` | tweak tube placement |
| `202d930c5` | feat(input): add gamepad Y button to toggle freestyle mode in main menu |
| `956042d14` | feat(toys): checkpoint riding + ride dashes; closed pure-petal lotus; rebuilt rose; Squirrel-scale lion |
| `f542208b5` | feat(manta): analog trigger turn+boost controls for gamepad |
| `38184718c` | feat(input): add D-pad navigation to arcade game configure modal |
| `4e7a07ed6` | feat(toys): full lotus, enchanted rose, miniature gallery wall, billboarded labels, 2x star |
| `87caeacc4` | feat(input): add visual row highlight for D-pad focus in arcade configure modal |
| `e0735b2c0` | perf(prisms): instantiate render entities from prototypes; batch visibility toggles |
| `eaf107e0c` | perf(prisms): Burst the collider-LOD classification, apply transitions only |
| `75828ff07` | perf(pool): timesliced async buffer refills, PoolMiss attribution, deeper prism buffers |
| `c0b61cc9e` | feat(toys): ring milestones with sphere triggers; hide the ridden line; seal the torus knot (transport holonomy) |
| `9b891d400` | fix(prisms): seal the visibility queue, cull queue, and async incubation |
| `df8115fae` | In Main Menu, drag the Game GameObject (root-level in the Hierarchy) into the Crystal Click Handler slot — Unity will resolve it to the MenuCrystalClickHandler component on that object |
| `9b9cc453e` | feat(toys): bake the four weak paintings from real references (phoenix, peacock, matterhorn, starry night v2) |
| `4ca9f2573` | feat(ecology): nucleus control zone, voracious exterior, 30s fauna wave clock |
| `72e358444` | feat(arcade): Brood Rush (NucleusRush 38) - fauna-wave race to 3 on nucleus control |
| `e2400d743` | fix(toys): pre-PR review pass — verified findings across runner, layout, toolkit, assets |
| `e2abefd20` | fix(toys): restore CatmullRom — the dead-API prune deleted it alongside its uncalled neighbours |
| `450d2a50f` | fix(projectile): inject DI into pool-replenished projectiles |
| `7f3cd6212` | fix(projectile): fire along muzzle forward instead of Gun component forward |
| `0003a8e8e` | fix(vessels): repair real runtime bugs across the flyable fleet |
| `69250b10b` | fix(sparrow): repair the gun fire path — dud colliders, return race, cooldown latch, stale domain, bullet swerve, shared-SO mode state |
| `b45879b46` | fix(sparrow): prefab repair, turret prisms join the ecosystem, element bars revived |
| `cce0b4ee0` | feat(toys): standard ObjectiveIndicator replaces the painting guide line |
| `2d84aa7b9` | feat(elementals): quantitative element->ability layer goes live (Phase 1) |
| `f17784c71` | fix(input): populate RightNormalizedJoystickPosition from gamepad and touch strategies |
| `dc6e64738` | feat(elementals): the four level-5 qualitative upgrades go live (Phase 2) |
| `4997d7b92` | fix(elementals): BarrelRollController accesses IVesselStatus default members through the interface |
| `eb526c2c4` | fix(toys): monuments pack close (proximity-first spheres), indicator clamps to the real screen |
| `337443f0e` | perf(ecology): pace LightFauna consume cascades across frames |
| `eb7071752` | fix(ecology): rebuild the LightFauna meal queue per tick; budget only real consumes |
| `4749bbc58` | feat(ecology): intentional herbivore feeding + mouth-driven shark predation |
| `fb564b517` | new skim move |
| `8d4eb1fa0` | fix(freestyle): appshell no longer reacts to gamepad while flying a vessel |
| `06bfe2dda` | fix(freestyle): self-healing gamepad gate + Sparrow element flowers actually build |
| `34ad63312` | Fix config asset creation path in benchmark procedure |
| `8812951c8` | feat(toys): flight-continuity stroke ordering — the next stroke starts where the last one ended |
| `51d0393d3` | Update BENCHMARK_TEST_PROCEDURE.md with setup steps |
| `1ebe538cb` | Update benchmark test procedure instructions |
| `fb5e66436` | perf(cell): Burst the volume recompute — kills the 10ms DomainVolumeIndicator spike |
| `71b51a286` | perf(lod): hysteresis band + bounded drain + markers; de-phase the 0.25s ticks |
| `d9f7eb6cd` | fix(freestyle): ControllerButtonPress respects the gamepad gate; element flowers get one fleet-wide placement |
| `4ba827efd` | perf(cell): volume sum off the main thread — snapshot + worker-thread job |
| `49f7f7738` | feat(toys): rings are low-poly flat-shaded tori, not line renderings |
| `72f10f9bf` | fix(ecology): missing Domains using in Boid; flat 15u shark attack range |
| `7393c2ff2` | fix(freestyle): modal-open backstop - appshell modals refuse to open while the vessel owns the gamepad |
| `b08b38ab6` | feat(elementals): fleet-wide rollout - required display on every vessel, map-driven tuning, creation-only skyburst radial |
| `096a3528d` | fix(barrel-roll): trigger on either stick at the perimeter; correct visual roll axis |
| `6932a36f7` | feat(elementals): left-stick-only barrel roll + Squirrel Time->top-speed unified via live ElementalFloat evaluation |
| `0f26bcac8` | Removed All Em dashes and replaced them with regular dashs |
| `92d79b148` | feat(benchmark): add Load Time Insights tab with load-window recorder |
| `664ec8819` | feat(ecology): territorial sharks, herbivore centre focus, rotating spawn ring |
| `a15b63d81` | fix(sparrow): play launch SFX for stationary block-shoot guns |
| `2512e5161` | fix(sparrow): barrel roll fires once per press at full stick deflection |
| `bd888f000` | fix(sparrow): barrel roll gates on boost presses + small real root roll |
| `e97b6985f` | perf(benchmark): skip span-label allocation on hot spawn paths while disarmed |
| `1cdc55111` | feat(benchmark): end load recording at client-ready + hot-path breakdown |
| `8d5dbb761` | feat(sparrow): remove barrel roll cooldown, raise root roll to 15 degrees |
| `b3f770eb3` | perf(environment): stream heavy decorative spawnables across frames |
| `6e2b14213` | perf(prisms): O(1) Soap listener registration + lazy shield meshes |
| `37a4b2f24` | feat(arcade): hold connecting panel until the environment finishes laying |
| `b27b5040d` | perf(prisms): author the octahedron shield on the SpawnablePrism prefab |
| `7fc8dc49b` | feat(ui): live arena-build progress + clock on the connecting screen |
| `4e59768a1` | feat(benchmark): end load recording at arena-complete, not client-ready |
| `fe0cdf4d7` | fix(arena): hold connecting screen until the whole structure is laid AND grown |
| `81b135c92` | feat(ui): Canvas Upgrader editor tool + AdaptiveCanvasScaler for PC migration |
| `314b74578` | feat(squirrel): define quantitative elemental parameters + level-5 ability upgrades |
| `d2694b3b2` | feat(party): invite-chain hardening + B4 state-preserving lobby convergence |
| `1c181bdce` | fix(multiplayer): B5 candidate - don't declare client ready before the local pair resolves |
| `b456747e8` | Update VirtualProjectsConfig.json |
| `782c88a10` | fix(arena): gate connecting screen on per-prism reveal - creation queue was the leak |
| `f096b60ba` | fix(ui): Canvas Upgrader review polish — report clarity + curve dedupe key |
| `b3924aa0a` | Update Animation Settings |
| `37ef7eff4` | Update Menu_Main.unity |
| `e6aa487a8` | Update Maelstrom.unity |
| `99d8ca32a` | Update MinigameCrystalCaptureMultiplayer_Gameplay.unity |
| `c821b43dd` | Update MinigameHexRace.unity |
| `015dc288c` | Update MinigameJoust_Gameplay.unity |
| `9c5dd537d` | Create CanvasUpgrader.meta |
| `33ae67baa` | feat(party): live display-name sync across presence lobby, party slots, and in-game scoreboards |
| `364a6430a` | feat(ui): Canvas Upgrader — canvas-less fragment prefab upgrade + persistent double-run guards |
| `58e5afbd0` | feat(ecology): add predator spawn-ring config fields (groundwork only) |
| `7fd0a4335` | feat(ecology): polar shark spawn ring, dash-oscillation bugfix, brittlestar feeding consistency |
| `baeaf696d` | fix(squirrel): drift-gate Heavy Trail + own-domain joust levels fauna up (lifeform elemental contract) |
| `f1f6986fa` | feat(ecosystem): FaunaVariantTuning - hoist per-element prefab-variant diffs into config |
| `86c16c4f1` | fix(joust): owner-authoritative joust confirm - toast and score in lockstep |
| `37f617826` | Update prefabs to support scaled canvas |
| `327f6e3f9` | Update Maelstrom.unity |
| `a47b85586` | Create CanvasUpgraderUpgradedPrefabs.txt |
| `c5e1364e8` | fix(scoring): credit team games to the team with the higher total |
| `c8448ab00` | fix(maelstrom): fold standings by team totals; last place earns 0 |
| `03c955afd` | fix(scoring): reveal failure can no longer strand a finished game |
| `03e530f83` | Update feed entry |
| `ffacca46d` | feat(ecosystem): prism-scale variant expression for fauna + flora (gyroid/tadpole diffs as config) |
| `1f558502a` | Update ProjectSettings.asset |
| `1dd67ecce` | feat(ecosystem): unify lifeform variants into configs, retire vestige prefabs, add Lifeform Matrix toy |
| `5e7641630` | feat(ecology): predator hunt pulses - sharks hunt 10s of every 20s |
| `71ed2aa6a` | feat(toys): make the Lifeform Matrix toy findable - element-coloured moons + placement log |
| `ab5fbc081` | fix(toys+joust): space the matrix toy apart, layer matrices outward, base-kit joust + Shepherd upgrade, element SHAPE signatures |
| `00b03cdea` | fix(benchmark): resolve MonoBehaviour CS0246 in DiagnosticsHUD on Release builds |
| `5c0f09805` | fix(toys): root matrix-spawned flora AT the station (Plant position override) |
| `95ea96bb1` | feat(ecology): bind super-shielded structure volume-only in cell bookkeeping |
| `48d278d2d` | feat(astroleague): edge lining, 2x intensity-1 arena, on-goal reset + goal replay |
| `d0668348e` | Update OrganicRematchGames.asset |
| `ad8d3fab3` | feat(toys): multiply conveyor microscene diversity - structural painting, kinds as palette tools, 40 recipes |
| `e98a97d58` | feat(toys): full menagerie (12 species), population spawns, and honest Frenzy-freeze reporting |
| `cc4a2de4f` | fix(toys): keep Gate Run and Orchard inside the scene envelope at all budgets |
| `6af96f7be` | feat(comeback): required in every party game - equal all-element buff, per-game rate, hard level-10 ceiling |
| `c671d51dd` | fix(joust): make the Shepherd level-up landable and visible |
| `29a5af1dd` | fix(ecosystem): make runtime-provisioned lifeform crystals skim-collectable |
| `a8b71b4b4` | shielded color tweaks |
| `4f47edfaf` | feat(prisms): super-shield state engages the stellated octahedron |
| `3cdb4a4e1` | feat(astroleague): densify edge lining to 240 prisms |
| `a8b81e15c` | fix(ecosystem): prefab-relative sizing for provisioned crystals + live ColorSet tint on all crystals |
| `e146b882b` | fix(sparrow): inject async-refilled pooled projectiles — guns and missiles fire again |
| `5a84beb6d` | feat(ecosystem): crystal color signals collectability - domain / blue-white heart / lime CTA drop |
| `60cef6a5c` | feat(crystals): editor swap of the charge crystal model to ChargeCrystalExport1_7-11-25 |
| `1c1dc9886` | fix(prisms): stellated super-shield renders on instanced prisms; frame the goal replay camera |
| `572f3ba22` | feat(astroleague): broadcast pan replay camera; document the instanced-render handoff trap |
| `30d99ca0a` | fix(crystals): one scale convention for all elemental crystals + charge uses its export model |
| `31f0bacc8` | feat(toys): gate conveyor placement by min distance and removal by off-screen |
| `31e2886b7` | refactor(ui): re-unify domain color onto SO_ColorSet; fold Maelstrom tints in as UIAccentColor (S0.1) |
| `04f883f11` | fix(crystals): space crystal child scale 134 -> 1.34 (stored skinned AABB was stale evidence) |
| `561050699` | feat(haptics): two local-pilot feels — skim pulse train + prism punish thud |
| `55f49461a` | fix(arcade): re-list Astro League and Brood Rush in the party games menu |
| `2b411879b` | fix(arcade): correct Rampage vessel roster to Sparrow/Rhino/Dolphin |
| `6461a20f8` | feat(rampage): multiplayer destruction race - Scurry's destructive analog |
| `ca905c9d8` | fix(rampage): verification pass - unlock, de-legacy, on-roster AI, honest scoring docs |
| `907066f18` | feat(scoring): finish-time end-game scores for Scurry + Rampage; 2000-prism Rampage arena cap |
| `d9abd3812` | fix(arcade): gamepad dpad nav hijacked the configure modal - gate on modal, rebuild grid per populate |
| `9cd05e582` | feat(vessel): stepwise gear boost for Rhino full-speed-straight run |
| `93721c0ae` | fix(ui): port the Jul-17 scaled-canvas pass into the three skipped scenes; Rampage cap 10000 |
| `63492e492` | feat(rampage): race target 2000 prisms; comeback rate rescaled to match |
| `c0b94f959` | Update OptionsMenuContent.prefab |
| `7c76bdfc7` | Update Menu_Main.unity |
| `df27fe88c` | feat(hud): author elemental petal flowers as prefab content, stop runtime UI spawning |
| `4100b66f8` | feat(hud): squirrel ability-icon juice - press feedback, overheat gauge, tube ready pop, directional drift |
| `16eb94b0a` | feat(ui): auto-switch controller icon sets by last-used input device |
| `b308974a4` | feat(hud): holographic silhouette icon - domain-tinted body, pulsing rim, scanline shimmer |
| `1b1d09578` | feat(vessel): Rhino ramp boost with FOV+Panini quasi dolly zoom |
| `018782c03` | feat(rampage): all-Rhino AI opponents that hunt hostile mass |
| `9555d8b87` | fix(vessel): access IsLocalUser/IsInitializedAsAI through IVesselStatus |
| `2e1d46883` | fix(ui): transplant the standard GameCanvas override state into Rampage/BroodRush/AstroLeague |
| `c2af7f2ca` | fix(vessel): speed-tunnel modulates from live home values, not defaults |
| `d6e3dbb39` | feat(rhino): trigger shield swipes with camera stance rotation |
| `13ce77e03` | Testing game |
| `d4e280a5e` | fix(rhino): dead right trigger (self-skim mute), CCW roll spec, camera tune |
| `df4eebfec` | tune(vessel): stronger Rhino ramp - faster, higher, deeper tunnel |
| `0da5c90c9` | feat(rhino): hold full shield sweep, drop camera rotation from swipe |
| `438070a23` | feat(ecology): shark jaw rig driven by hunt-pulse state (open = hunting) |
| `adfbb1a5b` | feat(vessel): invert speed tunnel - FOV and Panini drop below home |
| `5249cc5a6` | feat(rhino): analog trigger reparameterization - difference swipes, sum chops |
| `b45d69b9e` | fix(meta): resolve committed conflict markers in ElementShapes.meta |
| `accc7bb1f` | fix(rhino): analog path position-tracks the triggers - no more state snapping |
| `4a1170794` | fix(rhino): read triggers straight off the gamepad for the analog pose |
| `c3fe006f4` | fix(vessel): preserve Rhino sword skimmer X/Z; Space element drives Y length only |
| `07e8a0e5e` | fix(prisms): keep shielded prisms skimmable — trigger collider + no dead window |
| `02ef4d740` | fix(prisms): shields keep the authored box trigger so trigger-skimmers register |
| `5a5801576` | old click is back! |
| `dba934109` | new skim sound |
| `a9ce8fe06` | new skim 2 |
| `9f1b57840` | skim 3 |
| `ce7cd9a11` | Update CanvasUpgraderUpgradedPrefabs.txt |
| `f279d8e77` | Update SquirrelHUDVariant.prefab |
| `796835f73` | Update Squirrel.prefab |
| `0fab6618b` | feat(ui): icon-set switcher v2 - PC text set + per-set active/inactive hint visuals |
| `48c1142c8` | fix(hud): authored elemental-bars placement wins over the config stamp |
| `44d675d6e` | feat(hud): tube cooldown reads as a missile reloading - sink, rise, slam home |
| `8e0a8b7d0` | feat(editor): canvas upgrader - find & fix faulted anchors (wrap around element) |
| `2b3639987` | tune(hud): wider smoother drift lean; tube gray-on-cooldown / red-when-ready + reload breathing |
| `0608db99f` | Update Squirrel.prefab |
| `c8ccc7ed5` | new song |
| `4ccad180e` | Update Main Menu with fixed UI anchors |
| `2fec58cc9` | Update Main Menu with fixed anchors (2) |
| `4ccb46d3f` | Update prefabs with correct scale and anchors |
| `28ebd9cf8` | Update CanvasUpgraderUpgradedPrefabs.txt |
| `f6aede007` | Update Squirrel.prefab |
| `76151fd01` | feat(tournament): show Main Menu to every peer on the Maelstrom summary |
| `0459886fa` | feat(ui): config-driven in-game toast system replacing GameEventFeed |
| `c00eadc89` | Add Graphics |
| `529696317` | Update prefabs with correct anchors |
| `bac5a3110` | Update PartyInviteNotificationPanel Variant.prefab |
| `d9d33218b` | Update Authentication.unity |
| `a7f8840b9` | Update Bootstrap.unity |
| `1fcfec5dc` | feat(ui): heartbeat loader icon on BootStatusPanel |
| `731e7f341` | Update Bootstrap.unity |
| `305fbd689` | Update Squirrel.prefab |
| `a2f3e7332` | Update prefabs |
| `6aa779afe` | Update game toast assets |
| `ce1400794` | Update Bootstrap.unity |
| `12d1d1d66` | Update MinigameCrystalCaptureMultiplayer_Gameplay.unity |
| `861fb385b` | Update MinigameHexRace.unity |
| `98c36ba78` | Update MinigameJoust_Gameplay.unity |
| `d6372afa3` | new tunes |
| `cbbd7132b` | mix changes and new skim |
| `bd6a8b405` | last mix push |
| `ae47742c5` | revert(ui): remove heartbeat icon from BootStatusPanel |
| `7efc4c2ef` | feat(ui): add IconRotator drop-anywhere 2D icon spinner |
| `eeea45b2d` | feat(ui): full-turn spin + color cycling on IconRotator |
| `c8cdf08c0` | Rhino sounds added |
| `9cda63e7f` | small rhino mixing changes |
| `777cd3144` | Update Authentication.unity |
| `f0db58bcf` | Update Bootstrap.unity |
| `e97adc9a2` | fix(arena): reveal-gate every level's spawnables, not just Crystal Capture |
| `73926eb8b` | new rhino boost sound |
| `bea53d1b7` | fix(ui): sync home screen avatar with profile changes |
| `53f5b7e9f` | fix(ui): resume game when pause modal is closed with gamepad B |
| `e091dfc69` | Upgrade Game view prefabs to support new resolution change |
| `223756d55` | Update ArcadeGameConfigureModal.prefab |
| `14422c8e1` | Update SettingsModal.prefab |
| `5a0b0ec94` | Update Menu_Main.unity |
| `7ac44267f` | Update MinigameCrystalCaptureMultiplayer_Gameplay.unity |
| `4e9959d12` | Update MinigameHexRace.unity |
| `a40b34bd3` | Update MinigameJoust_Gameplay.unity |
| `eb6128a35` | Update CanvasUpgraderUpgradedPrefabs.txt |
| `b1555bc92` | fix(ui): disable screen buttons while a modal window is open |
| `cf3be3ddf` | fix(ui): dedupe incoming friend request rows in FriendsListPanel |
| `74085be39` | feat(diagnostics): add FPS-only minimized mode to DiagnosticsHUD |
| `d6e890520` | feat(rhino): ungated energy sword — super-shield popping on contact, energy meter, crystal burst, blade heat FX |
| `bac3d4b74` | fix(ui): block clicks behind modal windows with a self-managed backdrop |
| `ac645b896` | fix(ui): stretch runtime game preview to fill its window |
| `0cf1872d7` | fix(ui): only the top modal of the stack accepts input |
| `ac5465e81` | Update GameFeedText.prefab |
| `93d630415` | refactor(clouddata): unify Cloud Save schemas - keys, casing, timestamps, versioning |
| `82e811542` | feat(analytics): flight_time_seconds and completion timestamps on game_completed |
| `b2411556c` | feat(analytics): host-stamped match envelope on game_started |
| `1987a2e18` | feat(analytics): PostHog sink, dual identity, person properties, event schema |
| `2a0cb3b3b` | feat(arcade): replace Skim Race intensity-3 track with dumbbell circuit |
| `eaf068607` | feat(arcade): triple Skim Race intensity-3 lane gap, circle radius, and straightaway length |
| `41ea35810` | feat(arcade): drop Skim Race to 3 laps at every intensity |
| `a9a9cc231` | feat(arcade): make Skim Race laps per-intensity, 2 laps at intensity 3-4 |
| `7ab2cc3b4` | feat(collision): shape-precise shielded-prism collision as a Burst spatial-index shell tier |
| `a2dc025ef` | fix(collision): retire stale shell entries on MakeDangerous + shell-tier parity hardening |
| `1a12c7ff9` | feat(ecology): living fauna hearts grant their crystal value as a domain-wide elemental buff |
| `4dac81e16` | feat(ecology): maintained mechanisms sustain at most level 10, overcharge is transient-only |
| `0acec710e` | perf(load): multithreaded batched prism cloning + stream the two heaviest builders |
| `591302a46` | fix(ecology): rotate herbivore spawn ring on the wave clock; never graze shielded mass |
| `de8ae435c` | new song, undid rhino stuff for now. |
| `ee5274267` | fix(ecology): stop herbivores freezing on arrival; Blob hatches a full wave per tick |
| `86c43fdad` | feat(build): Windows x64 build pipeline, PC platform pass, crash reporting |
| `a1cd024e3` | ci: tiered Unity build verification workflow |
| `164bb981d` | Add meta files |
| `5f8f7c7fd` | Set max Brittlestar population size to 9 from 5 and spawn timer to 15 from 30 |
| `828600b65` | ci: skip the unity job until a runner label is configured |
| `29431ebd9` | fix(prism): danger prisms no longer stop AOE explosions via stale super-shield flag |
| `4dfd8a51c` | Update ConnectingPanel.prefab |
| `2aa3ae42b` | Update MinigameHexRace.unity |
| `147fb38bd` | Update Squirrel.prefab |
| `08e59437a` | feat(ui): order the ability icon row by element and land the upgrade signal on Squirrel |
| `aacc1a9f4` | feat(dolphin): double crystal explosion range |
| `2fce03cba` | feat(dolphin): triple conic explosion cone height |
| `4fecd50f2` | fix(dolphin): conic explosion damage volume now tracks cone height |
| `39aa30f12` | fix(dolphin): conic explosion sweeps its cone with a parametrically-coupled sphere |
| `2146c29f8` | fix(ui): rearrange the Squirrel ability row's x positions at the effective layer |
| `41e0ae8c2` | feat(aoe): blast-wave impact vectors radiate from a fixed origin, normalized in-job |
| `cb3faadea` | style(ui): uniform spacing and sizing for the Squirrel ability icon row |
| `70b097a06` | refactor(hud): remove the vessel trail display |
| `12df3ce43` | feat(ui): bind control hints to the ability they label, not to a position |
| `8cdf81586` | refactor(hud): remove the vessel silhouette icon |
| `2ecad315c` | fix(environment): load-gate hard cap measures stall, not total hold time |
| `06200bd6e` | feat(environment): Atlantis organic arena replaces the Scurry intensity-4 gyroid |
| `b538fe5af` | feat(ecology): cells accept prepopulated environments; Yggdra cell joins freestyle |
| `076176eef` | fix(ui): stop the ability-hint placement from zeroing the glyphs' size |
| `3f8baed97` | feat(ecology): spawn the full element x level matrix in every cell |
| `3f16cb5b9` | feat(environment): the freestyle six - Atlantis split into Yggdra + Daedala, four new cells rival them |
| `4eb14c1ca` | polish(environment): composition pass over the freestyle six |
| `9769114ec` | fix(ui): unclamp the hint anchor fraction - Mathf.InverseLerp put every glyph below the screen |
| `2d4e55350` | fix(environment): freestyle environment builds are gated - no more live bloom |
| `865f3a0f2` | feat(ui): extend the ability-row contract to the Sparrow and add fleet-wide enforcement |
| `c80377837` | fix(environment): defer freestyle builds past scene boot; watchdog stalled clone batches |
| `5bb0c54be` | fix(ui): Sparrow labels stranded by the reorder, Serpent HUD collapsed to screen centre |
| `4c8ce19bf` | feat(toys): cell selector toy - opt-in worlds and the freestyle reset |
| `98e2263a5` | feat(rhino): model sword point velocity and feed it to destroyed prisms |
| `33770b9c8` | fix(prisms): make the sword's impact magnitude survive to the screen |
| `9cb78f082` | feat(toys): cell selector shows real scale models, matrix sits twice as far out |
| `a45548668` | fix(prisms): a parked sword now imparts exactly what the hull does |
| `b274c143f` | refactor(toys): one toy opens into many; cell models stand alone |
| `77dcdc7ec` | chore(toys): ship pass - asset hygiene, cross-toy pen note, backlog |
| `356f00987` | testing game |
| `166508949` | tune(prisms): scale debris velocities to 1/3 across every destruction path |
| `c6924dd38` | fix(prisms): debris velocity was multiplied by prism volume, damping the Rhino's own trail |
| `5b94eb359` | feat(vessel): morph ship models with element levels via labeled blend shapes |
| `b57a5bd48` | feat(prisms): Phase A clock-material infrastructure — shipped dark behind UseClockAnimation |
| `4b4a0af47` | feat(vessel): resolve animated parts by name; add Dolphin/Urchin/Rhino rig swap |
| `4d15ed77f` | fix(vessel): drive animated parts around their rest pose; make rig swap report-only |
| `04d69317c` | feat(prisms): B1+B2 clock-material call sites — grow-in + color transitions stamp instead of tick (dark) |
| `35dea4bfe` | feat(prisms): B3 clock-material explosions/implosions — one stamp, one scheduled completion (dark) |
| `562358166` | fix(prisms): C3 AOE double-growers + B4-interim stellated shield central ticker |
| `a87783779` | fix(prisms): C2 rogue material blends deleted + C12 orphan cleanup |
| `d3c99718e` | feat(prisms): per-material interlock — UseClockAnimation is now safe to flip before/while wiring |
| `590d8c8de` | feat(vessel): restore the Squirrel's element shape keys from git history |
| `664f832b2` | fix(build): clear all 16 compiler warnings from the bleeding-edge build |
| `f25381aeb` | fix(benchmark): unguard 'using UnityEngine' so Release player builds compile |
| `c91043ec8` | feat(prisms)!: STRICT clock-material mode — no legacy fallback, no toggle |
| `a5a53753e` | feat(prisms): in-editor validator + play-mode smoke test + the wiring checklist |
| `29c27e9b6` | feat(prisms): clock properties wired into the three graphs + auto-wirer tool |
| `63600f2c4` | feat(prisms): BlockGraph grow cluster wired programmatically — GPU bloom is live on the graph |
| `747cbdf2b` | fix(vessel): splice squirrel shape keys into the fixed-animation export |
| `38ded07c2` | fix(prisms): the pop-in root cause — clock domains matched by construction via _PrismClock global |
| `207f391b5` | feat(prisms): ExplodingBlockGraph + BlockGraph colors wired programmatically; validator CRLF + shader-name fixes |
| `1821cc518` | fix(prisms): explosion debris direction + culling — CPU object-space velocity stamp + flight-envelope bounds |
| `d6a7e3a06` | feat(prisms): SuctionGraph wired programmatically + suction culling envelope (Phase 4) |
| `73191b92e` | refactor(prisms): explosion world->object conversion moved INTO the shader — no CPU matrix math, one stamped vector |
| `e7319cca8` | feat(prisms): transparent-prism color cluster wired into ExplodingBlockGraph — last known snap path closed |
| `eadac8570` | refactor(prisms): D2 deletion pass — retired CPU animation managers physically deleted |
| `e71c3d97b` | chore(prisms): PhaseThresholds re-baseline + follow-up branch prompts + /asset-surgery skill + /ship retrospective |
| `f8b023868` | fix(prisms): decisive no-entity stamp diagnosis + edit-mode diagnostic mute (gyroid scale investigation) |
| `59a8b780a` | test(tools): import prism-grid explosion harness from claude/prism-grid-explosion-scene-bi74f9 |
| `0af666b42` | feat(tools): A/B FPS-envelope benchmark for the prism-grid explosion (legacy-cpu vs gpu-clock) |
| `c08024bd1` | fix(tools): prism-grid lay failures surface loudly + 'prisms N' command + empty-lattice benchmark guard |
| `3b9efbf51` | fix(tools): prism-grid scene self-provisions a ThemeManager — root cause of the zero-prism Spawn |
| `61ec33b8c` | fix(projectiles): the Dolphin cone now damages everything inside it |
| `215b8e781` | fix(projectiles): guard deferred AOE hits by slot generation, not object identity |
| `90fa35d9c` | fix(prisms): destroyed prisms animate out under load instead of vanishing |
| `02aceaae7` | feat(tools): benchmark spec — 100k cube, inscribed blast, fast rebuilds; pressure-durations ported to the clock path |
| `ca92704d5` | feat(benchmark): per-axis gaps, explosion speed, safety-throttle lifts, manager dedupe |
| `f0ddfc21b` | perf(prisms): batched pure-entity debris — no GameObject per prism death |
| `9a1fb4312` | feat(toys): scale cell selector mini-cells 2x and push the matrix twice as far out |
| `762dd2b60` | fix(prisms): environment-laid prisms lost their grow stamp to the shield engage-morph |
| `71caffbf5` | tune(vfx): raise explosion debris spin coupling 30% |
| `de88c9d18` | feat(ecology): rebuild Caldera as four inward massifs around the nucleus |
| `f7ade29b6` | feat(ecology): Hesperides — the garden cell whose world is the planting |
| `330752af3` | feat(toys): descriptive 3D icons for every selection, 2x station scale and matrix distance |
| `014db1772` | tune(vfx): raise explosion debris spin coupling another 30% |
| `c39fbb9b9` | feat(dolphin): elemental pass — skim energy, team crystal cooldown, cone blast |
| `458e2a637` | fix(dolphin): keep the cone's angle when Space scales it; re-bind gauges on enable |
| `01a4049e1` | feat(wanderway): grand scale — 30k conserved prisms, built behind a veil, transported on the clock |
| `50a3ede90` | fix(dolphin): defects from the compile-verification pass |
| `92bdaeb66` | fix(palette): give Ruby + Gold shielded prisms Jade's contrast signature |
| `c5bd0a1f1` | feat(ecology): double the Caldera massifs, add the Ourobor Möbius cell |
| `2dd2680fb` | feat(ecology): eight garden flora forms, ground-kind seeding, role-shaped prisms |
| `01962ac1a` | fix(aoe): explosion inertia reaches the screen — impulse carries its own debris ceiling |
| `f065c8f76` | feat(ecology): rebuild the worm as a colony-fauna kaiju boss |
| `c6cb0144c` | feat(toys): iconographic toy roots - core = what you are, orbit = what you could be |
| `7c3cf828f` | feat(wanderway): the run — bare canvas, finite tether, and a way home |
| `0a3145e7e` | fix(toys): emblem stream must never build on its caller's frame, and the placeholder must survive |
| `c26c26322` | feat(dolphin): author the four-icon ability row on the fleet-standard bands |
| `6e61f37c6` | fix(ecology): review-pass defects in the garden flora, plus skill capture |
| `8e2c83d23` | tune(aoe): Dolphin blast debris to 60% — Inertia 3 -> 1.8 |
| `7f24d545e` | chore: revert accidental play-mode state commit "testing game" |
| `4b89d3f3c` | ci(build): gate the #if-guard mistake that keeps breaking Release builds |
| `36656c3de` | revert(ui): stop touching Daily Challenge behaviour; disable its Update instead |
| `535a747b1` | Add meta files |
| `dd3bfa1db` | Update Cloud settings to enable diagnostics |
| `8d61c4f03` | feat(economy): episode token entitlement layer + currency audit |
| `f1e178492` | fix(ui): unwind the modal stack when a modal closes outside ModalWindowOut |
| `93066c93c` | feat(economy): apply locked pricing, unify crystal payouts, legal drafts |
| `04cb801d1` | Update Menu_Main.unity |
| `1a53b2abe` | refactor: remove player XP end to end |
| `536a3723e` | Add meta files |
| `db8979d2c` | feat(tooling): centralize editor tools under FrogletTools and add prefab-drift tooling |
| `3f2c0ab86` | fix(tooling): replace C# 10 extended property pattern with a C# 9 null check |
| `175c65307` | refactor(tooling): replace the Gantt board with a card grid, drop the migration strip |
| `29520d5a7` | feat(wanderway): rolling tether — recycle the tail, ride the way home on it |
| `9df1ebce0` | fix(ecology): worm wound clocks persist until tissue differentiates; bloom rides the segment |
| `bc059e03d` | feat(toys): scale painting-icon fidelity to the icon, and give flora growth-pattern icons |
| `1119a6c94` | feat(dolphin): real ability icons instead of blank squares |
| `9ae8e43fe` | Update MaelstromSummaryScoreCardContainer.prefab |
| `486e34b1d` | perf(macos): make settings auto-detect pixel-aware and cut Metal editor overhead |
| `ef3eb3c62` | feat(skills): add /vessel skill encoding the vessel-class contract |
| `760ff66f0` | fix(skills): correct /vessel skill claims per adversarial verification |
| `2b0c191e9` | fix(trail): cached block indices survive the tether's front-removal |
| `1c1c7cb3d` | feat(ecology): worm colony spawnable from the Lifeform Matrix toy |
| `216089b36` | feat(toys): give SchwarzP flora a real growth-pattern icon |
| `b5963ea54` | feat(dolphin): use the vessel's own jaw silhouettes and the omni crystal icon |
| `ff118e326` | feat(dolphin): adopt the existing boost gauge as the Mass slot |
| `ea8a14ac6` | merge(bleeding-edge): Ourobor cell + FrogletTools; compile-verify the garden C# |
| `192376ec4` | feat(ecology): recover the 2024 worm's authored geometry into the kaiju |
| `9fe43927e` | fix(dolphin): boost gauge was pinned off-slot; pips gate on the upgrade; jaws use the silhouette's spacing |
| `e207eb236` | fix(flora): remove duplicate LeafSize property declaration |
| `5908e2ffb` | fix(toys): use correct sceneRadius parameter name in ConveyorToy emblem plan |
| `2ab828867` | new song added |
| `62e582047` | fix(crystals): unify initial + respawn placement volume when no anchors are authored |
| `cce931898` | feat(spawning): measure crystal + player placement off the cell nucleus |
| `43cc85593` | feat(ecology): Scurry Cell with a half-scale nucleus; drop scene-placed Cell-owned visuals |
| `aabf8bddf` | perf(prisms): batch the suction path onto the entity carrier; split the death path |
| `bad89d432` | feat(prisms): restore camera-to-vessel occlusion as a shader-side corridor off global uniforms |
| `2c9d71dfe` | chore(cells): purge dead Cell overrides from every scene; add the audit that finds them |
| `3aab140a3` | fix(ecology): worm spacing measured off the model; kaiju becomes an apex omnivore |
| `553bacc6e` | fix(ecology): worms repel each other, seek mass, and hatch into the food |
| `2174bb232` | merge(bleeding-edge): AOE impulse contract + FrogletTools menu root |
| `a78ad6138` | feat(arcade): add Ribcage - the Rhino-only cage-breaking race (GameModes 39) |
| `1fc9517c7` | fix(dolphin): boost fills only while drifting, jaws open '<', skim gains feedback |
| `82f5db891` | feat(prisms): make the occlusion corridor a platform law - no vessel or mode can opt out |
| `22b5ae6f1` | fix(spawning): spawn ring raced Cell.Initialize and put players inside the nucleus |
| `580a53e56` | feat(prisms): corridor tapers fully to zero, on a short C2 gradient, with a motley screen door |
| `bd6ad641a` | feat(prisms): size the occlusion corridor from each vessel's circumscribing circle |
| `da7ef48b2` | fix(dolphin): initialize the live skimmer, restore boost fuel, calm the ring |
| `fd53ef269` | feat(prisms): the occlusion corridor is a cone, not a capsule |
| `44b4fd78c` | fix(dolphin): boost banks only on drift; make the skim readable; audit skimmer wiring |
| `9ef18bd96` | feat(prisms): bare cone with no base cap, and a narrower fully-clear core |
| `12d0e3db2` | feat(dolphin): skim crackles across the skimmer, same as the Squirrel |
| `3349def56` | feat(prisms): grade the cone's base so the corridor boundary has no seam |
| `94cc6fc6a` | chore: add the missing .meta for VesselSkimmerAudit |
| `f9348237d` | feat(prisms): corridor screen door becomes a corridor-relative spiral |
| `99f1266d6` | feat(prisms): add a Worley cellular kernel to the occlusion corridor |
| `76b960c29` | feat(prisms): morph the occlusion corridor's dither pattern over time |
| `b9aedaac2` | feat(prisms): raise the corridor morph rate to 0.12 cycles/sec |
| `59b65e3da` | fix(arcade): RibcageScoringRuleSO was missing `using CosmicShore.Utility` |
| `5614c698d` | fix(ribcage): Rhino-only enforced, spawn outside the cage, brood penned until release |
| `ec453642d` | feat(tooling): make an editor tool's output shippable, and split /ship into depth modes |
| `3db6e59a6` | Automate the Thursday build branch promotion |
| `0329bc426` | feat(ribcage): +20% arena, a full five-species brood, danger traps, smarter AI, alert shake |
| `ed67e115c` | fix(tooling): scope the tool commit to its own paths, not the whole index |
| `e2c9cf8ac` | Gate build promotion on static validation, and report every run |
| `7ab8660f2` | feat(ribcage): the race is CREATION - standing prisms; destruction only arms the fauna |
| `ace691f0f` | Drop committed Python bytecode and ignore it |
| `05fa18600` | fix(comeback): PrismsRemaining case must aggregate by domain, not read a per-player stat |
| `f34e62c3b` | feat(ribcage): brood +50% (caged 85 seed / 149 cap), and batch fauna seeding across frames |
| `82eb43c3e` | fixed audio |
| `9a4b1d351` | feat(ribcage): standing-prism target 300 -> 3000 (and rescale the comeback rate with it) |
| `5fafdf53c` | feat(ribcage): cage 3,175 -> 10,229 prisms, spent on DENSITY (48 ribs x 21 hoops) |
| `e395b8f7d` | Update md files |
| `8976f44b6` | feat(ribcage): race on destruction (2000) and grow the cage to ~15k prisms |
| `ef7fae2e7` | Create DOLPHIN_ENERGY_ECONOMY.md.meta |
| `12d462a05` | feat(ribcage): layered-orange cage, open weave, plain bars, no fauna |
| `589783f93` | chore(ship): drop the prefab self-reference, close doc gaps, capture session skills |
| `f24f3ba8e` | chore: drop a stray __pycache__ artifact and ignore it |
| `0ba651304` | fix(multiplayer): clamp vessel class server-side; retune Ribcage cage |
| `40bac9563` | fix(multiplayer): always reset RoundStats on scene entry, not just on one lookup branch |
| `c9ccbd49f` | feat(manta): add speed-tunnel dolly zoom to trigger boost |
| `96c54a6bc` | feat(sparrow): roll on prism hit instead of lateral redirect |
| `bee514633` | fix(sparrow): make the prism-hit roll a real vessel roll, not an animation |
| `e5ab847db` | feat(vessel): make the speed tunnel a strict platform law |
| `4b27d29ff` | fix(vessel): close eight defects in the speed-tunnel platform law |
| `163fcb3e3` | feat(ribcage): equatorial spawn ring, and tilt every inner rind onto its own axis |
| `459f8b5aa` | Retarget the build pipeline to the three-tier release model |
| `1b10face1` | fix(ribcage): scoreboard counts PRISMS, not "bars" |
| `240c31791` | feat(analytics): salvage attribution-branch tools/docs, harden PostHog sink, lock EU region |
| `83685522c` | fix(vessels): FalconClassSO had a blank Name, which is what wrote "" to HANGAR_DATA |
| `fc880f322` | fix(scoring): zero every player's stats when the game starts, on every peer |
| `774025641` | Testing game |
| `95c1769ef` | revert(sln): drop accidental "Testing game" solution reorder |
| `b23e71068` | Update PostHogConfig.asset |
| `d05823a61` | refactor(analytics): envelope belongs to the PostHog sink, not every event |
| `5bedfb5ce` | fix(analytics): the consent gate is closed and nothing said so - fail loud, add a dev opener |
| `2ae877144` | feat(privacy): consent dialog that ships - built at runtime, created by AppManager |
| `4245cf8fe` | refactor(camera): replace Cinemachine menu camera with vessel-framing config rig |
| `a47a257b0` | feat(profile): one validated path for display names - filter, format, no duplicates |
| `cae79931d` | perf(ui): prewarm the pause panel so the first pause tap doesn't hitch |
| `0d570e8e4` | perf(scenes): smooth the game-to-menu return - unpause, veil clients, settle behind cover |
| `fca796edf` | fix(clouddata): resolve Cloud Save 3.4 API namespaces in DisplayNameRegistry |
| `ad138beb4` | feat(prisms): the occlusion corridor's flecks are triangles, not circles |
| `735de0e64` | feat(sparrow): remove overheat, free the roll, add elemental ward |
| `590c27eda` | feat(prisms): carry SHATTER, and open the corridor dither's scale dials |
| `8b84ec37d` | chore(sparrow): harden immunity wiring, annotate audit, capture skill learnings |
| `e97a288bf` | feat(prisms): Occlusion Dither Lab - slide the corridor's unit shape live |
| `12004b78e` | feat(analytics): menu freestyle counts as flight time; starter vessel and SelectedVessel land in HANGAR_DATA |
| `84801e352` | fix(profile): DisplayNameRegistry misses the Cloud Save Models namespace |
| `a9437a53a` | fix(ci): a late scheduler must not silently cancel a build cycle |
| `387d62bf5` | feat(wildlife-liberation): Sparrow-only three-cage hunt (GameModes 40) |
| `90bc31039` | feat(prisms): ship the corridor dither as SHATTER, 16.26 px / 20 px |
| `27fdb20e4` | refactor(tools): plain concatenation for the Lab's wall-ratio label |
| `8e477a044` | fix(tests): implement LifeformsKilled on GameDataSOTests.MockRoundStats |
| `9f1bd4528` | feat(wildlife-liberation): 600->1400 wildlife, scattered spawns, complexity ramp |
| `4f0144f1d` | fix(wildlife-liberation): centre-spawn root cause, open water, no tadpoles, target 120 |
| `4948f0069` | feat(sparrow): allow strafing roll in the stationary stance |
| `e73dd0b97` | feat(vessel): triple pitch and yaw while translation-restricted |
| `5b7a4516f` | feat(dolphin): sweep the crystal blast as a capsule aligned with the jaw gape |
| `decfff93c` | tune(dolphin): 130% capsule length, 80% radius, and exact jaw angles at both ends |
| `fd9717bdf` | tune(dolphin): raise cruise +30% and charged boost +70% |
| `6324e60f1` | feat(sparrow): fire turret prisms on the bullets' terms, always piercing |
| `ff907a8e3` | tune(dolphin): skim banks 15x less energy per prism |
| `b0f79425d` | Testing |
| `99b5a0355` | feat(dolphin): arm the jaw gauge lime at full energy; drop the dead vessel silhouette |
| `cc9a1f5be` | fix(sparrow): turret stance fired invisible prisms; move the flight to the GPU clock |
| `27dc77062` | revert: undo "Testing" (b0f79425) on bleeding-edge |
| `e2ffb9622` | chore(docs): restore the WILDLIFE_LIBERATION.md.meta dropped by the revert |
| `14129ba73` | ci: guard every commit that lands on bleeding-edge, and autofix it |
| `6c74ae314` | ci(guard): separate a compile break from a red test, and document the runner |
| `b4e87d015` | ci(guard): let autofix authenticate from a subscription as well as an API key |
| `3c069a1f1` | ci(guard): report trunk health as one self-closing tracking issue |
| `917604260` | ci: stop deleting Library every run, and stop shelling out to git mid-job |
| `f397a9580` | ci(guard): guard development too, and stop compiling docs-only pushes |
| `8f38f1a63` | ci: player-build development the day before every promotion |
| `093533b6e` | ci: IL2CPP only, and time both scheduled builds as UGS pre-flights |
| `ba8e691b9` | ci: a pull request no longer takes the Unity runner |
| `39b251d4a` | ci: make a failed player build diagnosable |
| `77abf38d0` | fix(build): stop shipping the test suite into the Windows player |
| `ee74785ab` | fix(build): let the editor assembly see Assembly-CSharp internals |
| `ec61f1fdf` | fix(guard): an inherited -e killed the step before it could classify anything |
| `98c08cb80` | fix(ui): pause-menu prewarm crashed the Windows player on every login |
| `804aab5fa` | fix(profile): close the double-submit window; remove Unity Ads entirely |
| `4c4bc856f` | fix(sparrow): close review findings on the turret flight path |
| `81a611093` | fix(dolphin): say so when the jaw CTA cannot be drawn |
| `376c42777` | feat(sparrow): two live-switchable turret flight visuals; close the silent still-nothing paths |
| `5098291c8` | feat(prisms): shear the occlusion dither by view depth so stacked layers stop moiré-beating |
| `22ad32f78` | feat(sparrow): danger turret prisms, 5x suction assembly, range re-anchored on SPACE |
| `29073687d` | feat(prisms): make the screen-door dither THE prism transparency mechanism |
| `0e855b24d` | Create DOLPHIN_ENERGY_ECONOMY.md.meta |
| `3103e80bd` | feat(sparrow): shielded full-size turret shots on the plain flight; range quartered |
| `23484953c` | fix(sparrow): flying prisms rendered their spread at full distance |
| `a10e56df9` | feat(sparrow): shield moves to the SPACE-5 gate; prism shots hit like bullets |
| `526db3d7b` | feat(prisms): world-anchored SHATTER3D corridor dither + object-anchored debris erosion |
| `60324ec60` | fix(sparrow): friendly fire always on; Charge 5 spares only the skyburst |
| `dac9f0802` | fix(prisms): restore 2D SHATTER corridor dither; debris fade becomes one wipe per face |
| `9c87ee5bd` | fix(sparrow): a pilot's shots never destroy their own fired prisms |
| `ed5ba5e53` | refactor(sparrow): host-only guard - fired prisms follow plain friendly fire |
| `4e5d9fdf6` | fix(prisms): per-vessel corridor sizing via true render bounds; spin-proof UV wipe with fringe + margin |
| `b70fb03f3` | chore(prisms): commit the corridor auditor's .meta |
| `b20e0ed75` | feat(sparrow): fired prisms get a 0.2s placement-immunity window |
| `90aee0ec5` | fix(sparrow): size the bullet hit sphere to the projectile, not its z-stretch |
| `d5cc744e1` | fix(prefabs): repair two fileIDs that overflow int64, and gate the whole class |
| `cffc2f17d` | fix(prisms): exclude skimmer field volumes even when inactive; scope the radius audit to real vessels |
| `577ea5769` | fix(prisms): revert the parallax shear; answer the layered beat with a depth band phase and back-face separation |
| `e7fd91079` | fix(analytics): the "events are dropping" hint pointed at a menu that does not exist |
| `b08a35d73` | fix(ui): the Menu_Main crash is a type-punned pause-panel reference |
| `6b6b2043b` | fix(ci): sweep untracked ghosts from the runner's source tree |
| `90f73ba60` | fix(prisms): drop the graded fringe from the debris wipe — the edge is hard |
| `1f1621f62` | fix(ci): the stale-file sweep needs its own safe.directory grant |
| `94d900e1a` | fix(prisms): end the occlusion corridor short of the vessel's nose |
| `4640949c6` | feat(ui): quit the game from the options panel's Quit Game button |
| `baec72410` | Update Quit Game Feature |
| `28738ca3f` | feat(ui): lock the GENERAL tab's exit actions to the main menu |
| `cfad23a71` | revert(ui): drop the QuitGameButton binder |
| `5635cb5d5` | fix(tools): the Lab's design-mode toggle was dead on a Windows checkout |
| `afe147b09` | Update Menu_Main.unity |
| `71a81d8db` | fix(wildlife-liberation): revert to a DOMAIN race - the winner is a domain, not a player |
| `52dca4ecf` | feat(wildlife-liberation): kill target 250; fauna sync retires the divergence caveat |
| `c32fa9e13` | style(prisms): paint danger prisms with the domain's shielded base face |
| `769eeb619` | feat(astroleague): Rhino-only sword soccer — bigger court, strike feedback, smarter AI, working food web |
| `3324b9513` | feat(arcade): Dog Fight - the Sparrow-only gun duel in the Boneyard |
| `98aae4c97` | fix(dogfight): scatter the Boneyard, add the enemy marker, drop the crystal |
| `17e9116fc` | fix(astroleague): shrink the court 40%, make the ball settle, give every court the cage cover |
| `f1545505f` | feat(dogfight): target 120, Atlantis at intensity 4, elemental crystals |
| `0a94cb38b` | Merge pull request #707 from froglet-studio/claude/gold-shielded-prism-contrast-ip3zf4 |
| `df5b526a1` | feat(rhino): energize ritual as the supershield key + authored blade FX pass |
| `5b5ca6895` | fix(crystals): honour _opacity so the charge crystal still blooms in |
| `af8fef393` | refactor(ecology): put the flora wither cadence on the variant config, record follow-ups |
| `77de3cb9c` | fix(rhino): review pass — canonical supershield flag, fixed bounce window, convergent stance |
| `bd991451d` | feat(rhino): hilt-anchor the sword, one blade-spanning tracer, danger-colour energize |
| `b1d4e895d` | fix(scoring): re-base client stat mirrors on scene entry (non-host start-score bug) |
| `430dbaa39` | feat(dogfight): omni crystal back, turret prisms score, wider intensities |
| `1f360c5f2` | Add meta files |
| `cce63fca1` | feat(camera): restore the lava lamp as a fifth menu camera config |
| `e1bb8ed83` | fix(dogfight): turret muzzle, AI break-off, target 90, four crystals |
| `e0e46e99d` | fix(rhino): tone the sword tracer back to a tip streak, and the blade out of bloom |
| `80eb7567c` | feat(rampage): rebuild as the Dolphin's demolition race |
| `7f8454981` | feat(sparrow): spray accuracy — 2x fire rate, decaying cone, rising haptic |
| `fc37bfd08` | fix(prisms): death visuals wear the dying prism's tier, not just its domain |
| `b7541714e` | fix(rampage): couple crystals to the nucleus, band the flora, fix AI drift |
| `4d86b3771` | fix(projectiles): sweep the path for prism hits — bullets were missing 74% of it |
| `3c356ebe9` | fix(camera): hold the speed tunnel while the lava lamp owns the menu view |
| `d648724d0` | Set MenuCam_LavaLamp1 OrbitAxis |
| `86b0370d8` | fix(scoring): credit environment kills per-simulator, and by domain |
| `0aade737d` | feat(dolphin): hold velocity magnitude for the duration of a drift |
| `e55358d0b` | fix(rampage): objective arrow tracks only the managed omni crystal |
| `9411ef1d1` | feat(rampage): four intensities, and fix the sticky cell-config race |
| `0976c02c1` | feat(scarab): core C# foundation for the Scarab vessel |
| `924dcc6ac` | feat(rampage): intensity is scarcity — fewer crystals, more wildlife, one forest |
| `3e27d9db6` | feat(scarab): flyable vessel foundation -- prefab, assets, registrations |
| `753e733f4` | chore(menu): disable menu camera random config switching while iterating |
| `f7c8b9aa1` | feat(sparrow): rounds grow as they fly; shield returns to MASS 5 |
| `ea3a01c66` | feat(scarab): ball generation -- crystals now forge a ball |
| `46974b8e9` | fix(scarab): thrust along the nose while drifting, and a real brake |
| `d966eb023` | feat(scarab): HUD gauges -- ball energy, switch charges, juke state |
| `b29c1533a` | feat(dolphin): make crystal seeding passive, freeing the right trigger |
| `ee19169fd` | feat(dolphin): add Echo Sight on the freed right trigger |
| `8f97dbdfa` | fix(scarab): coast drag applies only when the trigger is released |
| `070182d62` | feat(prisms): splice the Echo Sight highlight into the prism graphs |
| `10db177f7` | fix(dolphin): zoom the Echo Sight from the tunnel's home, not the live camera |
| `309f5af2d` | refactor(rhino): hand the tracer's size back to the inspector, keep its top edge on the tip |
| `17484d0d2` | feat(scarab): every omni crystal forges a ball (energy gate off) |
| `ed2c979a4` | feat(qa): submit button, multi-day sessions, and frozen verdicts |
| `b456a583a` | feat(qa): machine-facing session CLI for the editor window to drive |
| `98c9d3657` | feat(qa): QA Session editor window — the Submit button, in Unity |
| `a3402358b` | feat(scarab): ball carom + super-shield death; switch is a ring that grows |
| `d1d360a7b` | chore(scarab): add the missing ScarabSwitch.cs.meta |
| `970158462` | fix(qa): the window mistook Windows' Store stub for a real Python |
| `43c5617b1` | refactor(dolphin): cut the Echo Sight's zoom, keep the highlight |
| `fbd991d08` | feat(dolphin): a prism ram costs half the banked BOOST as well as half the energy |
| `c27d310e7` | fix(dolphin): Unity fake-null guard on the boost-ram effect, document the drift case |
| `ac0d8fd10` | feat(vessel): Scarab cavitation blast + authored elemental map |
| `8371eebeb` | chore(dolphin): ship-deep review pass — doc drift, meta, and a cap ratchet |
| `2bed5b249` | feat(dolphin): prism and danger-prism collisions slow the Dolphin, on the Squirrel's numbers |
| `0d3db63ec` | feat(sparrow): prism and danger-prism collisions slow the Sparrow, on the Squirrel's numbers |
| `9347cb6f1` | tune(manta): bring the prism-collision slow in line with the rest of the fleet |
| `e652d2f3b` | fix(vessel): thrust along the nose while drifting, fleet-wide |
| `01d97ee4e` | fix(vessel): drift ceiling must bound gain, not brake; revert Dolphin |
| `f97ab47e5` | feat(input): dual-WASD keyboard flight strategy |
| `3332f226b` | feat(input): dual-WASD keyboard flight strategy |
| `5d3351050` | fix(dolphin): the crackle is its only skim visual, beam removed |
| `65df61c34` | feat(ecology): rebuild the elemental crystal capture as a 0.44s snatch-suction-absorb |
| `6add5d6d8` | fix(ecology): a living lifeform's heart is blue, not lime |
| `c75dd0ae5` | feat(crystals): put all five crystals in the CTA lime and make the omni the hero |
| `b3e7a9a9f` | fix(input): keyboard dual-WASD mix and gamepad ability bindings |
| `4dc8bd012` | feat(rhino): five hairline blade tracers, lower sword mount, swipe recovery |
| `26c4b88ce` | feat(prisms): super-shielded prisms jiggle when hit but not destroyed |
| `4a797c4ef` | fix(dolphin): drifting at max speed no longer costs speed |
| `74d1b37a4` | chore: save local Unity material and Rhino prefab edits |
| `7d09e769e` | feat(ecology): the heart's blue -> lime crossing travels on the prism clock |
| `bda357c49` | fix(ecology): the crystal pickup sound never reached a lifeform's drop |
| `8572c4311` | fix(rhino): keep tracer spacing even across widths, drop the sword mount to hull centre |
| `821e04207` | feat(scarab): its own hull, and the camera straight behind it |
| `37f9596a2` | perf(prisms): GPU-clock the shield morphs and delete the last CPU prism ticker |
| `5be5121cd` | fix(scarab): unique network hash, and stop the prefab lookup failing silently |
| `e590712ba` | fix(prisms): size the jiggle's culling envelope by scale ratio; make the exotic-visual gate real |
| `87b7f7b09` | style(crystal): rewrap a comment line |
| `58223e4e5` | feat(scarab): puppeteer the hull, roll the visible ship, and fire the blast |
| `42199f98b` | fix(tests): import CosmicShore.Gameplay in the super-shield jiggle tests |
| `8f6c19c90` | fix(scarab): kill the hull NaN and rebuild the geometry that hid the puppetry |
| `04e2ccad8` | feat(scarab): blasts move the ball and forge one out of an omni crystal |
| `e5e518ced` | tune(scarab): the cavitation punch throws 8x harder, at the wavefront |
| `10ee60fb9` | fix(impact): make an empty effect slot name itself instead of throwing |
| `b13c0f59f` | refactor(dolphin): retire the shard toggle, superseded by Echo Sight |
| `e2cf98962` | tune(astroleague): lighter plow-through, and armour that redirects for free |
| `f111d290f` | feat(astroleague): the ball settles fast once it leaves the nucleus |
| `e70e7aafa` | feat(ecology): give flora populations + reproduction, and make the gyroid a colony of unit cells |
| `4b778b96a` | fix(scarab): delete the forge RPC that could never fire |
| `226c4d4e0` | style(explosion): de-duplicate the crystal-buffer comment |
| `fd74fe08a` | feat(urchin): restore the chain-reaction spikes and the trail rider |
| `371fb67c4` | feat(urchin): author the elemental map, effect assets and registration |
| `564775bf6` | feat(urchin): add the HUD controller/view pair |
| `8d518a466` | test(urchin): lock the chain depth curve, the ghost window and volley determinism |
| `62db02d3c` | fix(urchin): running dry must not end the spike hold |
| `65a2f2d9e` | perf(urchin): ship the cascade at depth 2, not 3 - a collider-budget call |
| `b3bc963bc` | feat(urchin): wire the vessel prefab - controller, executors, impactor, pools |
| `ef5ab06ac` | feat(urchin): author the ammo meter, guard the ride, and document the vessel |
| `c8cd04c41` | feat(urchin): add the Urchin to both vessel switchers |
| `2720db4ad` | fix(urchin): the swap crash - wire the camera customizer, and unbrick the changer |
| `703239028` | feat(urchin): attach physics (Dolphin pattern), muzzle anchors, checklist record |
| `e5a06d1ab` | fix(player): remove the doubled brace my merge resolution left behind |
| `ee0a4ead1` | feat(ecology): the gyroid octagon colony - a crystal in every window, and the Gyroid Lab cell |
| `452577851` | fix(gyroid): zero seed-prism locals after SetParent - daughters grow now; Lab becomes a speed chamber |
| `55e31d297` | feat(vessel): prismscape dimension ladder + fix the frozen slide + Urchin surface roll |
| `a769818f8` | fix(gyroid): a plant reproduces only once FULLY GROWN - the maturity gate |
| `f2df0c856` | feat(gyroid): colony diagnostics - make the third playtest decisive, and block the off-lattice reseed mint |
| `52f460e9a` | fix(vessel): make the Urchin's rides smooth - latched slide direction, continuous surface roll |
| `4a9a15c40` | fix(gyroid): "fully grown" includes the BLOOM - and the overlap suspects closed or counted |
| `46e9e2166` | feat(gyroid): lattice-defect auditor - catch each twin at birth and name the path that minted it |
| `7ed556825` | fix(gyroid): release orphan reservations - the seam race that punched permanent holes at every plant boundary |
| `b65e6aaa3` | feat(vessel): rail-grind the 1D ride, marble-madness the 2D ride |
| `8c0023ff5` | fix(gyroid): the chirality corruption - 12 of 16 baked seed rotations were a z-mirror ansatz, not the measurement |
| `233e97a7b` | feat(gyroid): reproduction is a POPULATION event - one birth per fauna-wave cycle, popped at random from the colony's frontier book |
| `34702a1af` | fix(vessel): stamp wake prisms into their trails; ring-shotgun volley, depth-4 chains, twin-trail geometry |
| `3668c775c` | feat(gyroid): 5x the freestyle colony's ceiling - it was the CELL'S VOLUME LADDER, not the population cap; retire the Gyroid Lab |
| `cdd6591dd` | fix(vessel): kill the per-prism jerk - Project's crossing-frame chord lerp + Catmull-Rom rail |
| `cdb6cf1fe` | fix(gyroid): retire the frontier with its cell, and correct the docs the later passes falsified |
| `810aeb38e` | feat(vessel): trail integrity over missing prisms + junction forking by facing |
| `0d63b1e7a` | fix(vessel): ride another vessel's wake correctly - the Squirrel's trails exposed three ride bugs |
| `460cd791a` | refactor(logging): retire vestigial bring-up telemetry behind opt-in channels |
| `012337983` | feat(ecology): level is EARNED, and a heart is one size per level |
| `a566ad6c6` | refactor(vessel): remove junctions, polish the single-trail grind |
| `edf2b16fc` | refactor(logging): silence the rest of the gyroid colony telemetry |
| `08554e16a` | feat(impact): don't skim or ram your own trail while laying it |
| `4093b42e6` | feat(flora): Schwarz P grows on its own non-Euclidean tile, not a fitted grid |
| `809b1e529` | fix(ecology): Mass and Charge crystals carried no model size correction |
| `15c412b70` | fix(vessel): restore the trail ride to what shipped - on the rail, attitude is the pilot's |
| `d4397c1ad` | fix(ecology): a lattice species levels without growing its leaf |
| `c753ebaf4` | feat(flora): fit the Schwarz P prisms to the tile - flush plates, chunky Mass, skeletal Space |
| `c97481ef1` | fix(vessel): the rail was a HELIX - undo the whole lay offset and ride the flight spine |
| `18d519724` | fix(ecology): revert the Charge crystal bump; measure instead of infer |
| `ba8f4e81a` | fix(vessel): stamp the lay offset on each wake prism - the spine cannot be reconstructed |
| `148bfda8d` | fix(vessel): the wake outlives its vessel - OnDisable orphaned every despawned trail |
| `c845b9680` | feat(dolphin): re-cut the elemental map around one weapon |
| `7c5271230` | feat(flora): the Schwarz P tile colony - one plant, one tile, one crystal |
| `35e466d2a` | fix(vessel): restore OnDisable's closing brace eaten by the round-14 comment edit |
| `d80564e99` | fix(dolphin): profile winding, palette-driven slot colour, drop the reach bar |
| `4ad9c9980` | feat(flora): give Space its own lattice - a clear strut needs a wider one |
| `36d16cb5e` | fix(prisms): stamp trail membership after Initialize at six lay sites; rings loop; 0D unrideable; 2D frees aim |
| `01ec1380b` | fix(dolphin): domain signal colour, a halo that reads through mass, living tally |
| `1584fd4fd` | feat(flora): close up the Space lattice, and scale it without changing the plant |
| `78e8cfae1` | fix(dolphin): the pilot halo no longer shrinks with distance |
| `9265fc0f5` | feat(vessel): ride the prism's surface - roll walks the hull around the trail axis |
| `7417a84bc` | fix(flora): revert the gyroid lattice scale - it dislocates; keep Schwarz P's |
| `639629339` | fix(dolphin): echo sight lights whole prisms, dimmer and cooler |
| `a7ef6fa1c` | feat(vessel): ride each ribbon as its own trail; envelope for shielded and skewed prisms; fix Urchin hull opacity |
| `6bfe10c84` | feat(camera): move the lava-lamp orbit outside the nucleus, clear of the toys |
| `4d13bd8b6` | feat(flora): stretch the Space gyroid, then scale the whole lattice 2x |
| `7a949f6be` | feat(camera): lava lamp to R=686 (legacy framing), and fix the real roll cause |
| `e3d2276a9` | fix(arcade): bind ElementalComebackSystem after AddComponent, so it actually subscribes |
| `4aea3650f` | fix(dolphin): ship-deep findings — a leaked mesh and a silently-unbound kill channel |
| `129a5ca07` | fix(shader): drop a non-portable half-literal, and record the clang evidence |
| `4b176133d` | fix(urchin): spikes dwell 3x; the omni barrage chains; domain colour on the hull's submesh |
| `dd372f51e` | fix(flora): duplicate _latticeScaleCached broke the build; thin both Space struts |
| `2322b8960` | refactor(tools): generalize the Dolphin row wirer to the whole fleet |
| `fbe1c0533` | fix(urchin): paint the domain by material identity; shotgun fires from both guns |
| `9ad154506` | fix(flora): spindle scale compounded down the branch chain - prisms grew as 2^depth |
| `55b071e2f` | fix(urchin): the hardcoded jade accent is why a Ruby swap stayed cyan |
| `e92f37f6b` | fix(prisms): an AUTHORED prism size was silently clamped to [0.5, 10] per axis |
| `79b2b62fc` | fix(gyroid): widen ConvertBlock's clamp before stating the size; record the lost prefab |
| `0c792e527` | fix(urchin): an abortable steal chain, oversized muzzle rounds, and the two-tone hull restored |
| `cd26abd03` | feat(flora): open up both Space lattices - gyroid spacing x2, Schwarz x3 |
| `d16533374` | fix(tools): resolve the CS0104 Object ambiguity in the fleet ability-row wirer |
| `31b5d5501` | tune(flora): shorten the Space gyroid strut to 40 x 1 x 1 |
| `dc030ee66` | chore(menu): re-enable menu camera rotation at a 45s interval |
| `9e027cf82` | tune(flora): Space gyroid spacing to an absolute 25 (LatticeScale 3.1902) |
| `abcfae8ec` | fix(editor): resolve Object ambiguity in VesselAbilityRowWirer |
| `6bf457c39` | fix(prisms): restore a widened scale window on pool reuse; correct post-merge doc drift |
| `3db8223a4` | fix(editor): qualify ambiguous Object reference in VesselAbilityRowWirer |
| `e0641ab3c` | fix(flora): scope the clamp fix to this branch's subject; file the phyllotactic case |
| `65dc37292` | fix(urchin): the vessel's transform never replicated; re-sync the asset generator |
| `ab9a54a6a` | feat(ecology): Charge armours its mass, and fit the gyroid leaf to its shield |
| `2899bc863` | feat(flora): gyroid branch is a mirrored half-branch pair, not one branch through the prism |
| `d99c57f8d` | fix(urchin): ship-deep blocker + the ride's null/ownership guards |
| `f6baa159e` | fix(urchin): resolve Slip's hull colliders lazily; record the ship-deep backlog |
| `895989362` | feat(ecology): fit the Schwarz P Charge plate to its shield too |
| `86e5b4947` | feat(dolphin): re-scope Time 5 to Drift Ward (debuff immunity while drifting) |
| `5a613cc0e` | fix(dolphin): correct the Drift Ward's stated scope against the real containers |
| `7445106fe` | feat(toys): put every freestyle toy inside a switch ring |
| `d8ce092b2` | feat(qa): the window shows each item's steps and PASS/FAIL criteria inline |
| `512a98632` | Merge pull request #757 from froglet-studio/claude/dolphin-danger-prism-immunity-xcckxb |
| `c1a66b2d0` | feat(qa): the window is a numbered process, and file I/O is UTF-8 everywhere |
| `579e7d534` | chore(scarab): build-pace Scramble's ladder, retire the Dais Lab |
| `d11d60076` | Merge remote-tracking branch 'origin/bleeding-edge' into claude/dolphin-spotlight-prisms-cost-ldgl3n |
| `6ec9d0d5a` | feat(scarab): omni-only ball forge, no hull omni effects, per-CELL ball overload |
| `853fe878b` | fix(dolphin): set minimum speed to zero |
| `419590fb5` | feat(tools): add editor crash detector (FrogletTools > Misc > Crash Detector) |
| `0448192ee` | feat(tools): Diagnostics lane + shared Bug Ledger beside the crash detector |
| `9471b5440` | audio stuff |
| `5b8cddae8` | feat(tools): ledger archive, shared BugSignature, tool findings, severity, doc links |
| `a3086f1ad` | feat(grizzly): recover the Grizzly feature from stash + notebook fix-list |
| `5144ad269` | fix(qa): step 2 was a dead end; drop the git commands; unclip headers |
| `f1d436496` | fix(scarab): remove switch interior fill, enlarge ring 20% |
| `c9db7420c` | feat(ecology): TIME breeds faster — the second elemental law |
| `3ead61156` | feat(toys): split the Lifeform Matrix into three kingdoms and add a vessel hangar |
| `f9ba8b9e0` | fix(toys): release AI companions under way, and stop double-starting their pilot |
| `6b3a35b71` | fix(vessel): bound a two-rail trail's hole against the slab it cuts |
| `79f0843ca` | fix(ai): make StartAIPilot idempotent by clearing, not by refusing |
| `01e314789` | Update md file on Tool design and apply updates to this tool |
| `c5ae459fa` | feat(qa): Submit finishes the session -- publish, push, confirm, offer the next |
| `f3614db82` | fix(qa): the window derives what is left, so a stale backlog stops re-issuing work |
| `64ce13e7a` | feat(vessel): the vessel vision band — distance-graded domain cel shading, fleet-wide |
| `e2aa0270c` | fix(qa): honest wording when Submit is pressed with nothing new |
| `7bc874d1e` | feat(sparrow): growth is a hit volume, not a size — model fixed, charge shell grows |
| `3689678c9` | fix(qa): a finished session went permanently unsubmittable, and said "Sent" while it did |
| `462ad878f` | feat(vessel): dither the vision mark away from the centre so it stops reading as paint |
| `8a339089b` | feat(sparrow): halve the dart, pale blue model, neutral-blue + danger-red shell |
| `eb0eae36f` | Merge remote-tracking branch 'origin/bleeding-edge' into claude/sparrow-spin-cooldown-p8agtv |
| `d0b3984d4` | chore(sparrow): channel the per-roll log, record the Scarab twin's defect |
| `1b419c364` | Merge remote-tracking branch 'origin/bleeding-edge' |
| `c47e42e15` | fix(tools): let the face-pivot verifier find its own pre-change revision |
| `cafdca9ff` | feat(toys): vessel matrix stations show the actual ship, marked by the vision band |
| `b85430e7f` | fix(vessel): exclude the local pilot's ship explicitly — the fleet's cameras are not 10-40u |
| `965bce9db` | feat(vessel): make TAILS and JETS standard components, and put the Dolphin on them |
| `1fb2e85fc` | feat(vessel): put the whole fleet on tails and jets, and correct the jet contract |
| `ddb7053bb` | fix(vessel): one big Sparrow jet, six on the Rhino, and stop tinting the sword |
| `ab72f6628` | feat(ui): fuse ability icons and element indicators into one lockup |
| `46fad2055` | fix(vessel): seat the Rhino's body jets on the hull that actually renders |
| `59e5c433b` | style(ui): kern the ability lockup so both marks have air |
| `b72c3de03` | fix(vessel): pin the Sparrow's jet to the middle tail-feather ring |
| `320f7ef6d` | feat(ui): make the ability lockup structural across the fleet |
| `b044a9f03` | refactor(ui): the lockup owns the row, and retires what it superseded |
| `b76337b3e` | fix(vessel): put the Rhino's eight body jets on the nozzles it actually has |
| `2d85535e7` | fix(vessel): list the Rhino's eight body jets in their parent's m_Children |
| `3ccb3d561` | feat(ui): lockup owns the gauge, the chip, the press state and the locked slot |
| `a871ea9d2` | feat(ui): the lockup card is two borderless trapezoids |
| `f6acbef47` | fix(vessel): move the Sparrow's jet off the fuselage and onto its two nacelle mouths |
| `754c74217` | feat(ui): balance the plates, add the slant edge, add the fleet cooldown |
| `bc9ae6cf6` | unity |
| `ac1a0ca9d` | feat(vessel): mount a jet on a named BONE, and put the Sparrow's six on b_Tail1..3 .L/.R |
| `2d8c1da16` | feat(ui): clockwise cooldown, wrapped antialiased band, equal marks |
| `5ee6200b2` | fix(ui): band on gauge cards, tighter row, chip placement across device sets |
| `b4a2bd293` | feat(sparrow): give the skyburst missile a TAIL |
| `70ec4b031` | fix(ui): re-home control hints into the chip socket, sized by the lockup |
| `d67bcf1f6` | fix(ui): revert hint adoption, size the chip from the style, retire old HUDs |
| `f4b30f1c2` | fix(ui): retire root-level HUD content nothing references |
| `c52094c92` | feat(ui): lockup draws control chips from one glyph set; normalise the HUD root |
| `0bd36b3ae` | fix(ui): demote the icon-set switcher to a pure detector; retire the last competing glyphs |
| `7ab977a21` | fix(ui): clear the whole HUD root on a vessel that binds no ability icon |
| `77b42ab5b` | fix(ui): no vessel authors the lockup; correct two measured claims |
| `b13d41691` | feat(manta): remake Manta to spec — Sting bombs, Kabloom, Soar wake rings, Yastri turn trails |
| `55ef71b9f` | feat(arcade): add Bloomrush — the Manta bomb-tag party game (GameModes 45) |
| `c2f00acb7` | fix(tests): implement FusesBeaten on the two IRoundStats test mocks |
| `800b34405` | fix(manta): re-home the bomb-bay HUD into the ability row, delete the peeking old UI, add placeholder icons |
| `b00530658` | feat(manta): juice the Bloomrush loop — fuse markers, staggered cascade, joust-planting |
| `e4a977480` | fix(manta): import CosmicShore.Core for AudioSystem in the two new PlayCue methods |
| `85ddfc039` | fix(grizzly): playtest pass — turn feel, spherical shell, burn destruction, claw + scope visuals |
| `07d962de6` | fix(audio): stop FMOD leaks, harden lifecycle, make volume sliders persist |
| `e3e2d36f8` | fix(settings): audio sliders were FOV sliders that saved full volume on bind |
| `5ccb45a9f` | feat(settings): add 240 FPS option to the frame cap control |
| `d267fb353` | tune(sparrow): boost 25% faster |
| `7224723d6` | feat(weekly): a spent challenge still opens, and its card offers the leaderboard |
| `071ad2dc8` | fix(settings): make the in-game context lock two-directional |
| `e2a051bd6` | Update Menu_Main.unity |
| `e57d8f4b0` | Update WeeklyChallengeCatalog.asset |
| `5a024a500` | fix(weekly): the challenge's own clock was ending the turn and eating the attempt |
| `7f5f4f1d0` | feat(weekly): give a lost attempt back, and one place to wipe player data |
| `9b50bb042` | fix(auth): unstick the boot chain — marshal UGS continuations and stop trusting the mirror |
| `90050221e` | fix(weekly): a race's objective is the mode's own end condition, read off the live match |
| `07ca0a223` | feat(weekly): the challenge is weekly, the attempt is daily |
| `c59cbc3f8` | feat(switchback): the Dolphin-only gate race |
| `eeca8a814` | fix(switchback): apply adversarial review findings |
| `dc3dcb65c` | fix(arcade): grow the game grid so a new mode is not silently dropped |
| `d4002d8ff` | feat(vessel): add drift-hold read and external-motion mode |
| `57642d1ab` | feat(scarab): make the juke analog and sheathe the blast on a held drift |
| `6651cde38` | feat(scarab): hold the drift to grapple a ball, release to sling it |
| `fe817adfc` | feat(sparrow): missiles rearm on prism kills, crystals ward, proximity fuze + warhead |
| `9db61a89d` | feat(arcade): add Hijack (45) - the Urchin rail heist in the Switchyard |
| `1f8151058` | feat(arcade): give Hijack's metric the launch panel's objective icon too |
| `5ea727d64` | perf(hijack): stop the objective arrow walking every burr four times a second |
| `64a11503e` | chore(hijack): author HIJACK.md's .meta with the rest of the mode's assets |
| `b2b63c8eb` | fix(arcade): grow the scroll content with the grid, not just the grid |
| `0f47b1d4d` | fix(hijack): review pass - compile blocker, inert comeback, and a cancelled raid |
| `347927558` | fix(scarab): recharge switch charges so the switch is usable more than once |
| `acd8ea164` | feat(switchback): tighten the mouth with intensity, light the next gate |
| `253c59781` | fix(hijack): the missing using, and the arcade grid that would have hidden the card |
| `51e5213fb` | refactor(hijack): cede 45/9/8 to Switchback, and put the three registry IDs in one place |
| `d2f7f9399` | fix(sparrow): corpses, buffers, self-debuff and tank convergence in the missile pass |
| `88063cfc2` | feat(arcade): add Tollway (GameModes.Tollway = 45), the Scarab ring race |
| `fe4c4660d` | fix(tollway): correct the generator's base-class path and fail loud on a missing one |
| `fcd2f82b3` | fix(tollway): seed a late-arriving AI's ring timer, document per-peer detection |
| `7d1cd09e8` | fix(toys): read DarkCTA off EnvironmentColors, not off SO_ColorSet |
| `3bc474f37` | fix(scarab): blast on any flick that reaches the limit, at any speed |
| `96c3b905c` | feat(camera): anchor hold, so a spinning vessel does not spin the view |
| `612c2a8eb` | fix(sparrow): silence the warhead, and stop the ward stranding its own flag |
| `9a9320ee3` | chore(sparrow): author syncIntervalSeconds on the prefab rather than leaning on the field initializer |
| `dfdf54878` | feat(scoring): add VolumeDestroyed metric |
| `8d59985fa` | feat(crystals): add ApproachLanes placement mode |
| `9f41fc02e` | feat(environment): add the Drum, a shootable prism sphere |
| `8bd12536f` | feat(arcade): add Drumfire, the Dolphin's rhythm range |
| `446cb6ce8` | feat(arcade): author Drumfire's scene, cell and arcade card |
| `97ce58b40` | fix(arcade): take the launch panel and its preview window down on LAUNCH |
| `688df2c32` | fix(arcade): measure the scroll content to a fixed point, and say when a card is unreachable |
| `4856d1894` | fix(palette): read the CTA at signal strength, and test the switch reservation over every signal |
| `eb5683b01` | fix(ui): a modal closed while the menu is PAUSED never went away |
| `8e3d5e011` | fix(scarab): never strand a grappled ball on a fluttered drift |
| `9f9516e5d` | feat(tollway): rings go into TOLL POSTS, not wherever the nose points |
| `4ec0dbbe0` | fix(arcade): pin the scroll content's children before growing it, and report why a card cannot be pressed |
| `ccc002e2b` | feat(sparrow): rank the missile's three radii, and let a bullet actually reach a pilot |
| `77f4f632c` | tools(diagnostics): name the UI that is actually on screen, instead of guessing at it |
| `af5a16803` | diag(arcade): log the press itself, and record which slots the lock ate |
| `695485d81` | fix(arcade): inject the cloned card row, and stop a persistent listener eating the press |
| `3a1720d0e` | fix(ui): make a persistent listener unable to eat a press, and gate the class |
| `7017c745c` | fix(tools): OnScreenUIReport was missing using CosmicShore.Editor.Froglet |
| `c65fa7ec1` | fix(sparrow): repair the enum rename I missed, and gate the class of miss |
| `892a7c099` | test(sparrow): enumerate CombatHitClass instead of naming the members I knew about |
| `7be1c01a8` | fix(projectile): depth-rent the vessel sweep's buffers like the prism sweep's |
| `a099a9cc8` | test(sparrow): run the tier scoring through the latch, not around it |
| `2abd2afe3` | fix(tools): stop the spent scene one-shot from taking every Dog Fight check with it |
| `4aea0e484` | fix(ui): the Urchin's PIP painted a navy slab over half the screen |
| `d8cc73e98` | tune(hijack): halve the steal target to 750, and move the comeback rate with it |
| `ac26243f1` | fix(tests): implement SwitchesThreaded on SparrowCombatTierTests fake |
| `87c6f425b` | dolphin audio added + tweeks to squirell drift behavior |
| `3ca69c95f` | feat(scarab): align the grapple camera to the orbit axis, and aim with the left stick |
| `375fc4574` | fix(end-conditions): restore the doc-comment opener a keep-both merge ate |
| `ecdac65aa` | mixing fixes |
| `f17772906` | feat(scarab): a held drift REVERSES; retire the ball grapple |
| `b9a391a22` | feat(menu): home hub — Mission / Toy Box / Arena / Arcade, and strip the arcade two-screen path |
| `0fa5503f9` | refactor(arcade): retire configChangedEvent — a SOAP channel nothing subscribes to |
| `1cce1d3df` | refactor(menu): delete the Menu_Main UI nothing can reach |
| `72cb0c8c8` | fix(tollway): gate ring placement on the PATH, not the point 150u ahead |
| `2e6925069` | fix(menu): reset the Toy Box on every close route, not just the button |
| `52d4f4336` | feat(tools): GameCanvas unification report and post-migration gate |
| `dd26fd111` | feat(ui): per-mode scoreboard stats profile replaces the last canvas scene override |
| `46d987019` | feat(tools): GameCanvas Unifier - absorb the shipped canvas into CORE and re-point scenes |
| `148801db4` | fix(tools): GameCanvas Unifier — 1920x1080 canvas contract, one button per step |
| `e790e9d24` | fix(tools): GameCanvas Unifier strips missing scripts before saving CORE |
| `b98863d18` | Update GameCanvas.prefab |
| `4243d389b` | Update scenes with game prefab kit tool |
| `8779f0c80` | fix(ui): GameCanvas — restore the template prefab references the absorb nulled |
| `7314e9fef` | feat(ui): per-player stat toasts - crystals, rockets, bends - on every peer |
| `cc070957b` | fix(ui): GameCanvas — revert the nested override that nulled GameToastView.itemPrefab |
| `deead97cf` | Update GameCanvas.prefab |
| `5a4a003a0` | delete duplicate prefab |
| `9ea4d245e` | Update scenes |
| `7e7494eaa` | chore(tools): GameCanvas Unifier — clear the last overrides, fix a gate false positive |
| `f385b9328` | Update GameCanvas.prefab |
| `67e902bf4` | feat(ui): split every player-facing canvas into full-bleed + safe-area content layers |
| `0cb8792cc` | fix(scarab): the ball reversal fires ONCE and flings behind the pilot |
| `c90978e6b` | Update GameCanvas.prefab |
| `fd2a2feae` | Update GameFeedText.prefab |
| `4ab25a782` | feat(ui): one availability model for the hub, the nav bar and NavLink |
| `2421fd7ef` | Update GameFeedText.prefab |
| `68620fd04` | fix(ui): retire ProfileModal, promote PlayerDataSelectModal, repair the avatar buttons |
| `1a4f67357` | feat(camera): replace the PIP rear view with a full-screen look-back camera |
| `15ba51d2f` | Update GameCanvas.prefab |
| `7b05f5881` | refactor(arcade): delete RespectInventoryForGameSelection - it could never be turned on |
| `eded11e7e` | feat(scarab): a switch is grafted onto a living plant's heart |
| `19531515c` | Update Menu_Main.unity |
| `8eda07018` | fix(scarab): the reversal reads the TRIGGER, not the drift blend |
| `fb6af25ad` | feat(menu): the Toy Box's two windows, and a tool to wire the hub |
| `68a4b9e30` | Update Menu_Main.unity |
| `227a8a768` | Create HomeHubWiringWindow.cs.meta |
| `71c9736be` | Add meta files |
| `c0d114109` | feat(tollway): one anchor species per intensity, one ring at a time |
| `b78ed012f` | fix(menu): fill the Toy Box's serialized slots, and two defects the run surfaced |
| `f5cd99ef2` | fix(menu): write ModalType by VALUE, not by enum index |
| `5427017a6` | fix(scarab): latch the reverse modifier, and let a buried drift swallow a nudge |
| `36844a2dc` | Update Menu_Main.unity |
| `e6ca59879` | feat(menu): the Toy Box's second pass - grid, aspect, copy, and a Navigate that lands |
| `6cce14c72` | fix(menu): bind the Toy Box's Navigate to the button that is actually on screen |
| `73a674096` | fix(menu): modals leave the flight, the friends panel stays closed, and the pad starts on the hub |
| `1b633bf9b` | fix(menu): a modal's open/close sting can no longer take its close button with it |
| `68b5dc40a` | Update Menu_Main.unity |
| `07045bce2` | fix(homehub): smooth the Toy Box Navigate arrival |
| `ff632edac` | fix(homehub): approach the toy from inside the cell, not outside it |
| `1db50936e` | chore(arcade): gate SO_GameList cards on scene existence, prune dead rosters |
| `c1415007b` | fix(store): parse balance labels defensively on the purchase surface |
| `3becb8302` | chore(hud): prove every HUD element resolves on-screen, and gate it |
| `1543300e9` | fix(boot): the sign-in no longer loses a race to the splash timer and strand the boot |
| `41214e3ca` | chore(ui): delete the dead second game-over panel |
| `727e263e0` | Update Menu_Main.unity |
| `47c3704de` | test(ui): verify the race-rank toasts have a producer, and pin the ranking logic |
| `80d7dfcb0` | feat(ui): re-author the home hub button plate at 4x, and close its cropped echo frame |
| `fe92c8134` | chore(ui): retire the call-to-action badge surface |
| `91a24e1fb` | feat(ui): a disconnect notice that survives the scene reload it reports on |
| `5c82df252` | feat(tollway): grow a different KIND of plant at each intensity |
| `22884788f` | feat(arcade): remove the Drumfire game mode |
| `79906b36e` | fix(vessel): the Urchin's jet plumes were 8.75x oversized, and widthScale could never have said so |
| `2d1782cb1` | feat(rampage): intensity is SIZE as well as scarcity - bigger, easier arena at 1 |
| `bdfd60462` | feat(scarab): the grab gets its own button, and the plate claims its mirror image |
| `d91106005` | fix(tollway): make --check diff DISK, and three defects the ship pass found |
| `01a2e8d11` | refactor(vessel): one implementation of a jet's mount-bone lookup, shared with the audit |
| `e126c8a32` | fix(scarab): the hold is read when the ball ARRIVES, and the mirror stops cancelling itself |
| `2a09d0044` | feat(menu): the Toy Box's variants list, and Switch beside Navigate |
| `7f67ae02d` | Update Menu_Main.unity |
| `0ef6cdb34` | fix(menu): make the hub tool land on the authored Toy Box window |
| `35bbf5f8f` | Update Menu_Main.unity |
| `a98b9b0c2` | feat(ui): size the toy window's type and make its variants list scroll |
| `59501f0ef` | feat(rampage): intensity 1 grows 5x the forest |
| `fa4661533` | Update Menu_Main.unity |
| `4fb96da35` | tune(rhino): absurd ramp boost, and the turn authority that makes it flyable |
| `1c88b7935` | feat(headlong): the circuit generator, cut to the Rhino's flat-out turn radius |
| `004841eda` | refactor(racing): extract the gate-race platform so Headlong reuses it |
| `36d88ca64` | feat(headlong): the Rhino-only circuit race (GameModes.Headlong = 48) |
| `20d646698` | fix(scarab): a held phase grab must never bat a ball, and must not re-grab it |
| `0bcf0633e` | fix(progression): stop unlocking the retired Drumfire mode |
| `63f9b3e78` | feat(breakwater): the measured arena model and the pure course + station geometry |
| `b62890561` | feat(skein): the offline geometry proof for the Urchin rail race |
| `fa20e42c7` | feat(skein): claim GameModes.Skein = 48 and bump the member-count tripwire |
| `645f2cb4c` | revert(scarab): cut the ball phase grab; the mirrored plate stays |
| `c8735297e` | fix(racing): restore the subclass hook block GateRaceController never got |
| `bb642c40f` | feat(skein): the course generator, transcribed from the proven model |
| `4277fbac3` | chore(skein): deterministic .meta files for the new script, folder and doc |
| `f50f6bdb6` | feat(skein): seed the course, so two matches are not the same cable |
| `2d1f5e18f` | tune(scarab): raise the top speed 20% (180 -> 216, Time band 216 -> 324) |
| `b3d548c10` | feat(breakwater): the runtime - controller, arena, monitor, provider, tests, mode 48 |
| `27a353991` | fix(scarab): clear the residue two retirements left behind |
| `26448ab47` | feat(skein): gate MASS-5 armour against fusing two lanes |
| `20bb4a75c` | feat(breakwater): assets, scene, generator and the technical record |
| `4b30e9851` | feat(skein): wire the mode end to end - it now appears in the arcade |
| `8711b5958` | fix(skein): missing using directives, plus the gate that would have caught them |
| `c83741bf6` | tune(rhino): the ramp is a slope, and Headlong's ladder now demands its corners |
| `fcb9322db` | fix(breakwater): the eye was 16.5, stations could swallow a spawn pad, and a failed walk hung every client |
| `be9066b9b` | fix(skein): self-shadowed seed local, plus the gate for that error class |
| `bb3242989` | feat(tools): element ability table — query the fleet's element/ability/L5 state from assets |
| `5425f5870` | feat(breakwater): one-dial intensity ladder anchored on I1, and two laps flown out and back |
| `4ca82e629` | fix(breakwater): resolve BreakwaterCourseSettings from EndConditionOverridesSO |
| `180ea61e1` | fix(skein): the rings never existed; and the cable now breathes |
| `f7acfac4f` | feat(toybox): overhaul the Toy Box UI - verbs, previews, layout, plates, preview cost |
| `faebfe506` | fix(ecs): gate the DOTS world on Entities Graphics actually loading its compute kernels |
| `e56905cd7` | fix(auth): hold the sign-in flow while the first-run age gate is on screen |
| `2fbf77b86` | Update Menu_Main.unity |
| `975ca138c` | Add meta files |
| `d115e2c44` | fix(ecs): move the bootstrap gate into CosmicShore.ECS so PrismRenderService resolves it |
| `1548b1713` | feat(breakwater): replace the out-and-back with a start gate and a circuit |
| `c211ab739` | fix(skein): the card shipped mode 48 (Tollway), and the course was built one second early |
| `03656df3e` | fix(toybox): crisp cards, CTA selection glow, domain-switch preview, spent Spawn, tonemapped lifeform preview |
| `050ee6e82` | Update Menu_Main.unity |
| `e5915ab3d` | Rebake and add new codex sprites |
| `9006cf4ec` | Update Menu_Main.unity |
| `a5a6a6bfd` | Update Menu_Main.unity |
| `66e3fde6e` | feat(arena): open the Arena hub entry and give arena cards a vessel-picking launch window |
| `adb46fb0c` | Update scarab |
| `bf142afac` | MIX PASS |
| `84a4d066e` | fix(breakwater): drop the Tooltip orphaned by retiring firstStationDistance |
| `ca3750c6c` | feat(urchin): 2x rail speed, +30% cruise, a kick off the end of a ribbon |
| `61a73fde2` | fix(breakwater): make the arena build say why it stalled instead of holding the screen |
| `6a9d87b8d` | fix(breakwater): the shoal loop indexed one past the end and took the whole arena with it |
| `d409512e6` | Update Authentication.unity |
| `74ad01791` | Update Menu_Main.unity |
| `13a272d85` | Update OnlineFriendsInfo Variant.prefab |
| `4a512ac3a` | merge: bleeding-edge into Breakwater, and move Breakwater to GameModes 50 |
| `5c86f07d4` | feat(skein): intensity is how much of the race names a curve |
| `bac9a53c9` | refactor(breakwater): adopt the gate-race platform - 1,255 lines to 202 |
| `4acfca1fb` | chore(skein): review-pass fixes - a duplicate XML tag, and a wrong reason in a right comment |
| `dd4841c22` | feat(party): direct join and spectate from the friends panel, card reveal, hub button gate |
| `9c1527c30` | fix(menu): card reveal can no longer strand arcade cards invisible; hub gate also hides avatar + username |
| `ad7b15234` | fix(spectate): bind by poll with a census, isolate pair-init throws, badge the watched pilot |
| `fcf080cd4` | fix(arcade): the grid owns its rows - visibility and a uniform pitch; badge namespace |
| `b30246d05` | refactor(manta): cut the Soar wake rings — Time L5 is open again |
| `1fe052921` | fix(spectate): a machine with no local pilot can still build the roster |
| `6cdf8509e` | Update OnlineFriendsInfo Variant.prefab |
| `c6d55a238` | Update Menu_Main.unity |
| `50a5eccc2` | feat(arcade): add Redline — the Manta-only circuit race on the gate-race platform (GameModes 53) |
| `d60bea773` | feat(maelstrom): add all nine eligible new arcade modes to the pool, laddered by pick-up difficulty |
| `01685a896` | Update Menu_Main.unity |
| `98dfc9662` | fix(weekly-challenge): stamp the leaderboard's period so a stale board cannot show last week's rows |
| `9fe8b26d2` | fix(arcade): add Breakwater and Skein to the arcade grid roster |
| `e5a77fc2f` | fix(tests): implement FusesBeaten on the two remaining IRoundStats test mocks |
| `3cbc170c9` | fix(arcade): register Redline, Bloomrush, Breakwater and Skein in the Arcade grid's roster |
| `331aa92c9` | fix(manta): tighten Redline's level-4 mouths and stop the skimmer drawing |
| `992c22998` | chore(logging): silence the console - route every subsystem's telemetry through CSLogChannel |
| `40d54599a` | fix(logging): restore using directives the console sweep wrongly removed |
| `6a93230c8` | fix(docs): repair what the overcharge retirement left pointing at nothing |
| `d62673066` | fix(arcade): drop the retired CallToActionTargetType key from both new cards |
| `b8c57f0cc` | fix(settings): the whole options row opens its dropdown |
| `7b4746723` | Create SettingsRowDropdownHitArea.cs.meta |
| `a987fe068` | fix(build): re-point PC player settings defaults at the Windows target (R5) |
| `0c1f747bb` | fix(presence): converging never deletes a lobby other players are in |
| `3c0859599` | feat(analytics): cohort every player by invite wave (R9) |
| `153c86ff9` | feat(ui): de-scope the commerce surfaces for the invite build (R4) |
| `672df180d` | fix(multiplayer): a client can always leave, and can never be stranded on a black screen |
| `826ec4a9f` | feat(multiplayer): a departed pilot's ship keeps flying; clients get a rematch vote and a way out everywhere |
| `7c29a2234` | fix(multiplayer): ship-deep findings — menu orphan Player, additive-load disarm, stale ready-gate docs |
| `485f7d6d6` | fix(multiplayer): repair the two client-roster scans that would not compile |
| `2ad549bbd` | fix(tools): stop check_using_directives widening its own scope |
| `9dd3eb6d0` | Update GameCanvas.prefab |
| `da8e5393e` | fix(menu): stop returning from freestyle disabling the home hub buttons |
| `fae81572a` | feat(scoreboard): show WHO voted for a rematch, as faces under Play Again |
| `762e1e885` | fix(scoreboard): cut the vote row against the strip that is actually authored |
| `9d0b90af5` | feat(scoreboard): wire the Play Again label, and give a client a LEAVE LOBBY button |
| `9f02589d1` | feat(party): synced party seating + animated domain glow on lobby slots |
| `9cf08c830` | fix(party): make the arcade card lobby follow the host on every client |
| `f2618065b` | refactor(party): throttle the net read, pin the halo out of layout, fix the docs |
| `8961161c8` | fix(party): let the lobby reconcile wait for the freestyle blend, and close the doc drift |
| `97698d795` | refactor(party): speak the halo dialect the repo already ships |
| `aadf158ad` | feat(grizzly): bomb pump on LT/RT - pressure-sized bombs kick you forward |
| `ceb45f6c7` | fix(grizzly-charge): rebuild the scene on the shipped GameCanvas so the match can start |
| `5654763cb` | feat(toys): offer the Grizzly in the Vessel Changer and Spawn Matrix hangar |
| `91b87acde` | fix(grizzly): AI Grizzlies steer, cruise at full throttle, and pump |
| `0ae285f97` | feat(arcade): Grizzly Time (63) - the Grizzly-only bomb-pump circuit race |
| `4157694a1` | feat(grizzly): trigger bombs - LT/RT each fire, freeze and detonate a bomb that launches you |
| `d24f2e74d` | feat(grizzly): bombs throw you AWAY from them, look like bombs, small bomb / big blast |
| `5f42c01c4` | feat(grizzly): bombs launch 3x, detonate only on the trigger, light the prisms they pass, wear danger shades |
| `8d0c10d46` | fix(grizzly): reverse the hull's pitch puppetry |
| `68948352e` | chore(grizzly): restore an unreferenced clip, retire the bomb-pump wording, record the clamp trap |
| `af1a2bb48` | fix(serpent): a stopped wall seed stops searching for mates |
| `eb38f9e17` | fix(aoe): conic blasts subscribe to turn end / replay reset and free their material |
| `504557466` | fix(aoe): AOEBlockCreation retires itself and never destroys pooled prisms |
| `19e99e26d` | fix(weekly): an abandoned weekly run cannot finish against the next ordinary match |
| `f5d2d530a` | fix(astro-league): ending a slow-mo no longer un-pauses the match |
| `8937989ed` | fix(scarab-scramble): clients see the cell-overload toast |
| `ea324b808` | fix(weekly-leaderboard): the leaderboard refetches every time it opens |
| `6829a90b2` | fix(weekly-leaderboard): a reused row no longer shows the previous player's avatar |

---

## BH-2.3 — invite-clear could skip the lobby mutex and race an invite send

- **Date:** fixed 2026-10-05; merged 2026-10-05 at Yash's call with the retest deferred to the handoff revisit list. Repro skipped.
- **Symptom (risk):** an invite that never arrives, or one that fires twice, when a clear and a
  send overlap.
- **Root cause:** `HandleInviteClearedAsync` decided whether to take `_lobbyMutex` from a shared
  `_insideRefreshCycle` flag. That flag only meant "some refresh or reconcile is running", not
  "this caller holds the lock". So user-cancel, the party-leave callback and the fire-and-forget
  clears started inside a refresh (which keep running after the refresh releases the mutex) wrote the
  invite property without the lock whenever a refresh was in flight.
- **Fix:** `_insideRefreshCycle` is removed. `ClearOutgoingInviteIfPresentAsync` and
  `HandleInviteClearedAsync` take `callerHoldsLobbyMutex` (default false). Only the awaited call
  inside `RefreshPartyMembersAsync`, which always runs under the mutex, passes true. All other
  callers (Update expiry, user cancel, presence-leave, presence-join, party-leave) wait for the
  mutex. The fire-and-forget ones are not awaited by the refresh, so they cannot deadlock it.
- **Audit:** the six callers were checked; the invite send path (`SendInviteAsync`) writes under
  its own mutex hold and does not call the clear.
- **Verification:** all gate scripts pass; not run in Unity (two players needed). Retest is on
  the handoff playtest list.
- **PR/commit:** pending.

---

## BH-2.2 — presence lobby was never rejoined after a failed reconnect

- **Date:** fixed 2026-10-05; merged 2026-10-05 at Yash's call with the retest deferred to the handoff revisit list. Repro skipped.
- **Symptom:** after a network blip the online list stays empty and invites stop arriving until
  the app is restarted.
- **Root cause:** after three consecutive refresh errors `RefreshAsync` calls `ForceReset()` and
  runs `JoinOrCreateAsync` once. If that attempt fails (`CreateAsync` swallows its own errors and
  leaves the lobby null), `Update` is gated on `IsInPresenceLobby` (lobby not null), so nothing ever
  ran the join again. A throw from `JoinOrCreateAsync` there was also unobserved.
- **Fix:** `HostConnectionService` sets `_presenceRejoinPending` when the rejoin leaves no lobby
  (or throws). `Update` then calls `TryPresenceRejoin`, which retries `JoinOrCreateAsync` with
  exponential backoff (3s doubling to 60s). It stops when the lobby is back, the service is
  disconnected, the session is offline, or the normal `EnsureInitializedAsync` path takes over.
  `Docs/PresenceSystem/ARCHITECTURE.md` documents it under ForceReset.
- **Verification:** all gate scripts pass; not run in Unity (needs two players and a network cut).
  Retest is on the handoff playtest list. The identity republish after rejoin still comes from
  `LivePropertySource` as before.
- **PR/commit:** pending.

---

## BH-2.1 — Cloud Save could not tell "load failed" from "no data yet"

- **Date:** fixed 2026-10-05; merged 2026-10-05 at Yash's call with the retest deferred to the handoff revisit list. Repro skipped.
- **Symptom (risk):** on a flaky connection a player's progression, unlocks or profile could be
  replaced by defaults.
- **Root cause:** `UGSCloudSaveProvider.LoadAsync` returned `null` both for a missing key and
  for any error (offline, auth, network, unreadable value). `CloudDataRepository` treated both as
  "new player", kept its fresh default object and uploaded it on the next write, over the
  real record.
- **Fix:**
  - `ICloudSaveProvider.TryLoadAsync` returns `CloudLoadResult<T>` with a `CloudLoadStatus` of
    `Loaded`, `Missing` or `Failed`. `LoadAsync` stays as a wrapper. A stored value that cannot be
    read is `Failed`, not `Missing`.
  - `CloudDataRepository.LoadAsync` records a failed load. It still falls back to the local
    snapshot so the player can play. `SaveAsync` still writes the local snapshot, but while the
    load is marked failed it does not upload: it retries the load first. `Missing` clears the flag
    and allows the upload, `Loaded` adopts the cloud record (cloud wins, as on a normal load; the
    pending local edits are dropped and a warning is logged), and `Failed` leaves the data dirty to
    retry later. `ResetAsync` clears the flag because a deliberate wipe is meant to overwrite.
- **Trade-off:** edits made while the load was failing are dropped if the real record then loads.
  That matches what already happened on the next launch, and is safer than overwriting the record.
- **Verification:** all gate scripts pass; not run in Unity. No test double implements
  `ICloudSaveProvider`, so no tests needed updating. Retest is on the handoff playtest list.
- **PR/commit:** pending.

---

## BH-1.12 — a departed player's vessel handed to the AI was not marked AI

- **Date:** fixed 2026-10-05; merged 2026-10-05 at Yash's call with the retest deferred to the handoff revisit list. Repro skipped.
- **Symptom:** after a client leaves mid-match the ship flies on under the AI, but the rest of the
  game still treats that Player as a human (ready gates, round reset, HUD checks).
- **Root cause:** `ServerPlayerVesselInitializer.ConvertPlayerToAI` flips the networked
  `NetIsAI` only. `Player.IsInitializedAsAI`, the flag everything reads, is a local copy that was
  refreshed only when the pair was first initialised, so it stayed `false` on the server and on
  every client. A spawned backfill bot has it set at spawn, which is why bots were fine.
- **Fix:** `Player` subscribes to `NetIsAI.OnValueChanged` (subscribed in `OnNetworkSpawn`,
  unsubscribed in `OnNetworkDespawn`) and updates `IsInitializedAsAI` and the object name.
  The player is already in `_processedPlayers` from when it was a human, so no change was needed
  there.
- **Verification:** all gate scripts pass; not run in Unity (needs two devices). Retest is on the
  handoff playtest list.
- **PR/commit:** pending.

---

## BH-1.11 — non-ASCII characters in UI strings rendered as empty boxes

- **Date:** fixed 2026-10-05; merged 2026-10-05 at Yash's call with the retest deferred to the handoff revisit list. Repro skipped.
- **Symptom:** arrows, a cross, a middle dot, a times sign and shape bullets in UI text show as
  empty boxes. Nothing in the Console.
- **Root cause:** the only UI font (`ALDRICH-REGULAR SDF`) carries 97 glyphs (ASCII, nbsp and an
  ellipsis) with no fallback table. See `Docs/claude/ANTI_PATTERNS.md`.
- **Fix:** ASCII replacements, all in strings that reach a `TMP_Text`:
  - `SpectatorOverlay`: `<` and `>` buttons, `X  LEAVE`, and the hint line `< > / Q E ...`.
  - `ToyConfigureModal`: `<  Back`. `ToyVariantCard`: branch marker `>`.
  - `DogFightScoringRuleSO`: `N pts - B rounds, M rockets` (was `N pts · B×● M×◆`, the shapes
    had no meaning without a legend).
  - `BroadsideScoringRuleSO` and `UndertowScoringRuleSO`: the `·` separator became `, `.
- **Verification:** all gate scripts pass; no test asserted the old strings; not run in Unity.
  Retest is on the handoff playtest list. Alternative if the symbols are wanted back: add the
  glyphs to the font asset instead.
- **PR/commit:** pending.

---

## BH-1.9 — culture-dependent timestamps in analytics and file names

- **Date:** fixed 2026-10-05; merged 2026-10-05 at Yash's call with the retest deferred to the handoff revisit list. Repro skipped.
- **Symptom:** on a device whose culture uses a non-Gregorian calendar or non-Latin digits (ar-SA,
  th-TH, fa-IR) the PostHog event timestamp and generated file names carried the wrong year or
  digits. Nothing in the Console.
- **Root cause:** `DateTime.ToString("yyyy-MM-dd...")` and `$"{dt:format}"` with no culture use the
  current culture.
- **Fix:** `CultureInfo.InvariantCulture` on all four sites the handoff listed:
  `PostHogAnalyticsSink` (event timestamp), `AnalyticsServiceFacade` (`timestamp_utc_iso`),
  `ScreenshotDirectorConfigSO.BuildFileName` and `DesktopPlatformServices.TimestampedName`.
- **Not changed (dev tools only):** a similar `DateTime...ToString` stamp exists in
  `PrismExplosionBenchmark`, `LoadInsightReport`, `DiagnosticsHUD`, `ProfilerCsvLogger` and
  `LogControlWindow` (benchmark/diagnostic file names and display text). They are outside the
  handoff list; sweep them if a diagnostic ever needs to be machine-parsed.
- **Verification:** all gate scripts pass; not run in Unity. Retest is on the handoff playtest list.
- **PR/commit:** pending.

---

## BH-1.7 — combat-hit latch pruned every entry by one window

- **Date:** fixed 2026-10-05; merged 2026-10-05 at Yash's call with the retest and Broadside balance re-check deferred to the handoff revisit list. Repro skipped.
- **Symptom:** a hit with a long per-weapon window (Rhino sword 1.4 s) could pay twice, because its
  latch entry was dropped before its own window ran out.
- **Root cause:** windows are authored per weapon asset, but `VesselCombatHitLatch.Prune` judged
  every entry against the cooldown of whichever call happened to trigger the periodic sweep (every
  128 admissions). A short-window call (Urchin spike 0.12 s) therefore pruned long-window entries
  early. The handoff also said entries could be KEPT past their window, but admission compares
  against the calling asset's own cooldown, so a stale entry only costs memory; only the early-drop
  half was a real gameplay bug.
- **Fix:** `Assets/_Scripts/Controller/ImpactEffects/EffectsSO/Helpers/VesselCombatHitLatch.cs`
  stores the admitting window on each `Entry` and `Prune(now)` drops an entry only when
  `now - entry.Time >= entry.Window`. `TryAdmit` behaviour is unchanged.
- **Verification:** all gate scripts pass; not run in Unity. Retest and the Broadside balance
  re-check are on the handoff playtest list.
- **PR/commit:** pending.

---

## BH-1.6 — online duel rematch started with the last game's round/turn counters

- **Date:** fixed 2026-10-05; merged 2026-10-05 at Yash's call with the two-peer retest deferred to the handoff revisit list. Repro skipped.
- **Symptom:** Cellular Duel, finish a game, Play Again. The rematch ends early and/or swaps the
  vessels on its very first round.
- **Root cause:** `MultiplayerMiniGameControllerBase.ResetForReplay_ClientRpc` (the in-place replay;
  Cellular Duel is the one mode that does not reload the scene) reset scores and players but never
  `GameDataSO.RoundsPlayed` / `TurnsTakenThisRound`. The server's `SetupNewRound` zeroes the turn
  counter, but `RoundsPlayed` stayed at the old game's value on every peer, so
  `RoundsPlayed >= numberOfRounds` held almost at once and
  `OnlineDuelForTheCellController.SetupNewRound` (`allowSwap = RoundsPlayed > 0`) swapped on round one.
- **Fix:** `Assets/_Scripts/Controller/Arcade/MultiplayerMiniGameControllerBase.cs` zeroes both
  counters in `ResetForReplay_ClientRpc`, so it runs on every peer. Deliberately NOT
  `GameDataSO.ResetRuntimeDataForReplay`, which also clears `GameConfigSynced` and the spawn poses
  that a live session must keep.
- **Verification:** all gate scripts pass; not run in Unity. Retest steps (two peers) are on the
  handoff playtest list.
- **PR/commit:** pending.

---

## BH-1.5 — friends init latched `_initialized` even when the service failed to start

- **Date:** fixed 2026-10-02; awaiting Yash's retest on `Bug_Hunt`. Repro skipped.
- **Symptom:** if Friends initialization fails once (UGS slow or unreachable at sign-in), friends and
  presence stay dead for the rest of the session. Only a warning from the facade is logged.
- **Root cause:** `FriendsServiceFacade.InitializeAsync` catches its own exceptions and returns, so
  `FriendsInitializer.InitializeFriendsAsync` could not tell failure from success and set
  `_initialized = true`. The guard in that method and in `HandleSignedInEvent` then refused every
  retry.
- **Fix:** `Assets/_Scripts/Controller/Party/FriendsInitializer.cs` now sets
  `_initialized = friendsService.IsInitialized` and returns early (no presence write) when the
  service is not up, so the next sign-in event can retry. The facade already resets its own
  in-progress flag on failure, so a retry is a real second attempt.
- **Verification:** all gate scripts pass; not run in Unity. Retest steps are on the handoff
  playtest list. Same lesson as BH-1.4 (PLAYBOOK §7): a call that swallows failures is not proof of
  success; check the state after it.
- **PR/commit:** pending.

---

## CAM-1 — Sparrow freestyle: camera stops following after the turret stance (partial)

- **Date:** 2026-10-02. **Status: not reproduced; root cause of the stance trigger NOT found.**
- **Symptom (reported):** Menu_Main freestyle, Sparrow, gamepad. Pressing Turret Stance (A) makes
  the camera stop following the ship; exiting to the menu and re-entering freestyle fixes it.
- **What was established:** the Sparrow's camera is hard-attached (`SparrowCameraSettingsSO` has
  no `mode` key, so `FixedCamera`), so a frozen view needs the player rig to have NO follow
  target, be inactive, or have its framed point overridden. Two full read-only traces (forward
  from the stance, backward from the camera) found no path from the stance to any of those.
  The menu round-trip heals it because freestyle entry re-runs
  `CameraManager.SetupGamePlayCameras`, which re-points the rig.
- **Fixed in this neighborhood:**
  - `CameraManager.EndWindowedPlayerCamera` restored `_windowedPreviousTarget` even when no loan
    was running (null), so an unmatched or repeated End (the mode preview calls it from four
    teardown paths) handed the gameplay camera a NULL follow target: exactly this symptom. The
    loan is now balanced. And when the preview swaps the hull while it holds the loan (its tap-in
    and tap-out both do), `SetupGamePlayCameras` now records the NEW hull as the target to give
    back - `End` used to restore the hull captured at `Begin`, which the swap had just destroyed.
  - `CustomCameraController` now reports, once, when the on-screen player rig loses its follow
    target, naming the call stack that cleared it (or that the target was destroyed), and
    re-latches onto `CameraManager.PlayerFollowTarget`. **This is the diagnostic for the next
    repro:** if the stance still breaks the camera, the Console names the culprit.
  - `ScreenSwitcher`'s freestyle input gate now re-applies if anything re-opens
    `sendNavigationEvents` mid-flight (both preview hosts restore it), instead of trusting its
    own "applied" flag.
  - `MainMenuController.HandleMenuReady` is guarded against freestyle, like its camera twin: a
    re-raised `OnClientReady` used to put the hull on autopilot and pause input mid-flight.
  - `ToggleTranslationModeActionExecutor.End` cleared the stance locally but not the replicated
    `n_IsTranslationRestricted`; it now goes through `VesselController.SetTranslationRestricted`.
  - Both `ToggleStationaryModeAction` assets serialized a dead `mode` key, so both ran the
    default (`Serpent`). They now author `stationaryMode` with the value each hull already ran
    (no behaviour change; the Sparrow has no seed assembler, so its branch is identical).
- **Verification:** out-of-editor gates pass (conditional compilation, using directives,
  self-referential locals, duplicate attributes, console logging, abstract members, enum refs,
  switch collisions). **Not compiled or run in the Unity editor.**
- **Retest:** freestyle → vessel changer → Sparrow → gamepad A (stance) on and off, fly. If the
  camera still freezes, copy the `[CustomCameraController] The player camera lost its follow
  target` warning (with its stack) into this entry.
- **Follow-ups seen, not fixed (rows, unmeasured in play):**
  - After a mode-preview tap-out the hull swap runs `SetupGamePlayCameras`, which makes the player
    rig the ACTIVE controller; `EndWindowedPlayerCamera` then skips its `Deactivate` because
    `_activeController == _playerCamera`. So the player rig may stay enabled behind the menu
    camera after a preview. Measure: after tapping out of a card preview, read
    `CameraManager.GetActiveController()` and whether the player rig GameObject is active.
  - `SingleStickVesselTransformer.Initialize` creates a new `CourseObject` GameObject on every
    call and never destroys it (only used until the first `RotateShip`). Measure: count
    `CourseObject` roots in the hierarchy after several vessel swaps.
  - `ControllerButtonPress` gates on an `EventSystem` cached via `FindAnyObjectByType` while
    `ScreenSwitcher` gates `EventSystem.current`. Each of Bootstrap / Authentication / Menu_Main
    authors one root EventSystem (none DDOL in the scene files), so this is only a defect if two
    are ever alive at once. Measure: `FindObjectsByType<EventSystem>` count in Menu_Main at runtime.

---

## BH-1.3 / BH-1.4 — auth scene: timeout off the main thread, and silent sign-in failure

- **Date:** fixed 2026-10-02; retest deferred by Yash on 2026-10-02 and parked on the handoff's revisit/playtest list. Repro skipped at Yash's call.
- **Symptom (1.3):** with a slow or unreachable UGS at boot, the cached-auth timeout expires and the
  auth scene can throw `EnsureRunningOnMainThread` or freeze on the auth screen.
- **Symptom (1.4):** a failed guest or auto sign-in navigates on as if signed in, then waits out the
  whole profile timeout for a profile that never loads.
- **Root cause (1.3):** the timeout is raised by `CancelAfter`'s timer thread. In
  `TrySignInCachedWithTimeoutAsync`, `.AttachExternalCancellation` sat outside `.AsMainThread()` (the
  call used `.AsUniTask()`), so the `catch (OperationCanceledException)` resumed on the timer
  thread and its caller then touched Unity/UI state. `HostConnectionService.WaitForProfileInitAsync`
  has the identical shape.
- **Root cause (1.4):** `AuthenticationServiceFacade` reports a failed sign-in through its
  `OnSignInFailed` event and never throws, so `OnGuestLoginAsync` / `AttemptAutoSignInAsync` carried
  on to `HandlePostAuthFlowAsync` after a failure.
- **Fix:**
  - `AuthenticationSceneController.TrySignInCachedWithTimeoutAsync`: `.AsMainThread()` on the
    success path, and `await MainThreadDispatcher.SwitchToMainThreadAsync()` as the first statement
    of both catch blocks.
  - `HostConnectionService.WaitForProfileInitAsync`: the same switch at the top of its catch.
  - `OnGuestLoginAsync`: after the await, `if (!_facade.IsSignedIn)` shows the sign-in error and
    re-enables the button (the existing `finally`), via a shared `ShowGuestSignInFailed`.
  - `AttemptAutoSignInAsync`: after the await, `if (!_facade.IsSignedIn)` logs and goes to the main
    menu, matching its existing failure behaviour, instead of waiting out the profile timeout.
- **Verification:** all gate scripts pass; not run in Unity. Retest steps are in the handoff
  playtest list and PLAYBOOK §7.
- **PR/commit:** pending.

---

## BH-1.2 — gamepad triggers stayed held across a strategy switch or pause

- **Date:** fixed 2026-10-02; Yash retested on `Bug_Hunt` and it works. Skipped the repro on
  `bleeding-edge` at Yash's call; the cause is clear from the code.
- **Symptom:** hold a gamepad trigger, then touch the keyboard or mouse (the input controller
  hands over to another strategy) or pause. The vessel keeps the trigger's ability held (drift,
  charge, and so on) until the trigger is pressed and released again on the pad. Nothing in the
  Console.
- **Root cause:** `KeyboardInputStrategy` releases held triggers and speed gestures in
  `OnStrategyDeactivated` and `OnPaused`; `GamepadInputStrategy` had neither override. Once it
  stops being the live strategy `ProcessInput` no longer runs, so the release edge
  (`leftJustReleased` and friends) is never raised, and its remembered `prevLeftTriggerActive` /
  `prevRightTriggerActive` stay stale.
- **Repro:** not run on `bleeding-edge`. With a pad connected, hold a trigger, then move the mouse
  or press a key, and check whether the ability stays on.
- **Fix:** `Assets/_Scripts/Controller/IO/GamepadInputStrategy.cs`.
  - Added `OnStrategyDeactivated` (release triggers and speed effects, `ResetInput`, reset state)
    and `OnPaused` (release triggers, zero sticks and analog triggers), mirroring the keyboard.
  - The trigger edge logic moved unchanged into `DispatchTriggers(left, right)`, so a release is
    the same code path as a real let-go (`ReleaseHeldTriggers` calls it with 0, 0).
- **Verification:** all gate scripts pass; Yash retested with a pad and it works. Re-verify steps are
  kept in PLAYBOOK §6 and the handoff playtest list in case it recurs.
- **PR/commit:** pending.

---

## CI-1 — raw `Debug.Log` in `TrainingSessionRunner.LeaveSlot` failed the console-logging check

- **Date:** fixed 2026-10-02.
- **Symptom:** the `conditional-compilation` CI job failed on PR #936 (and would fail on any PR)
  at its "Check console logging" step: `TrainingSessionRunner.cs:710: raw Debug.Log - route through
  CSDebug`. The failure was in code already on `bleeding-edge`, not in the PR's own change.
- **Root cause:** the AI training commits of 2026-09-29 added a raw `Debug.Log` in `LeaveSlot`.
  The project's rule is that all logging goes through `CSDebug`.
- **Fix:** `LeaveSlot` calls the file's own `Trace` helper (`CSDebug.LogVerbose` on the
  `AITraining` channel), the same as every other training log in that file. Same message text.
- **Verification:** `python3 Tools/Build/check_console_logging.py` reports no problems (it
  reported 1 before).
- **PR/commit:** pending.

---

## BH-1.1 — AI held drift on stop (already fixed; closed with no code change)

- **Date:** closed 2026-10-02. The fix itself is `4c866f880` of 2026-09-26, which predates the
  handoff doc's review of this item.
- **Symptom (from the handoff):** a vessel stays in the AI's commit drift (course locked, nose
  free) after autopilot is switched off, until the human taps drift.
- **Cause:** `StopAIPilot` stopped the brain but never sent the matching stop for the commit drift.
- **What already fixes it:** `AIPilot.StopAIPilot` releases the commit drift (`_commitDriftHeld`),
  stops every cycled ability that had started and clears the aim telegraph. `PilotSwap` stops the
  AI and calls `ReleaseHeldInputs` while the server still owns the hull, so the release replicates.
- **Left alone on purpose:** `AIPilot.OnDisable` does not release a drift. It only runs on
  teardown, where the vessel is going away, and the one other disabler (the AI training pilot)
  calls `StopAIPilot` first. Sending input from a teardown path risks null references.
- **Verification:** Yash tested the Menu_Main freestyle takeover on `Bug_Hunt` and the ship flew
  normally, with no stuck drift.
- **PR/commit:** docs only.

---

## BH-1.13 — `Fauna` never left its cell's spawned-object list

- **Date:** fixed 2026-10-02; Yash retested on `Bug_Hunt` and it works. Skipped the repro on
  `bleeding-edge` at Yash's call, because the cause is clear from the code.
- **Symptom:** none visible in normal play and nothing in the Console. `Cell.spawnedLifeForms`
  kept an entry for every creature that died or was torn down, so the cell's `LifeFormsInCell`
  stat only ever went up. That stat is read by `AllLifeFormsDestroyedTurnMonitor` (the
  Wildlife Blitz co-op scene, `MinigameWildlifeBlitzMultuplayerCoOp`) and the single-player
  Wildlife Blitz turn monitor, so "clear every creature" could never reach 0.
- **Root cause:** `Flora` leaves the list in `LifeForm.Die`, but `Fauna` derives from
  `MonoBehaviour`, not `LifeForm`. No fauna death path (starvation, predation, joust, scene
  teardown, cell swap or split) called `Cell.UnregisterSpawnedObject`, and `Fauna.OnDestroy`
  only left the live-fauna registry (`UnregisterLiveFauna`), which is a different collection.
- **Repro:** not run on `bleeding-edge`. To see it, log `spawnedLifeForms.Count` in
  `Cell.UpdateCellStats`: it only rises as creatures die.
- **Fix:** `Fauna.OnDestroy` calls `hostCell.UnregisterSpawnedObject(gameObject)`. The call is a
  no-op when the object isn't in the list, and skipped if the cell is gone, so it is safe on
  every destroy route. The handoff doc's §1.13 moved to §0.
- **Verification:** all four gate scripts pass. Needs Yash's retest on `Bug_Hunt`: play the
  Wildlife Blitz co-op scene (or any mode with fauna), let creatures die, and check that nothing
  throws and the creatures count goes down as they die.
- **PR/commit:** pending.

---

## BH-1.10 — crystal colour fade leaks a Material per colour change

- **Date:** fixed 2026-10-02; Yash retested on `Bug_Hunt` and it works.
- **Symptom:** nothing in the Console. The Profiler's Materials count creeps up over a long
  session in one scene (crystal colour changes: a heart becoming a pickup, theft and decay back
  to blue), and the extra materials are named `Crystal... (Instance)`. Unity frees them on a full
  scene load, so the growth only shows within one scene.
- **Root cause:** `Crystal.LerpCrystalMaterialCoroutine` ran `new Material(renderer.material)`.
  The `.material` getter clones the renderer's current material onto the renderer, and the
  following `renderer.material = tempMaterial` replaced that clone without destroying it, so
  one Material leaked per colour change. (The handoff said two; the fade copy is destroyed at
  the end, so it is one.) The fade copy also leaked if the crystal was destroyed mid-fade,
  because the `Destroy(tempMaterial)` at the end of the coroutine never ran.
- **Repro (unfixed `bleeding-edge`):** Profiler > Memory > Take Sample Editor, then play one
  round in a mode where crystals change colour (Skim Race, hearts dropping), take a second
  sample in the same round, and compare the Material count and `Crystal (Instance)` entries.
- **Fix:** `Assets/_Scripts/Controller/Environment/FlowField/Crystal.cs`.
  - The fade copy is `new Material(renderer.sharedMaterial)`, and the renderer is assigned
    through `sharedMaterial`, so no hidden clone is made.
  - Each fade copy is added to `_lerpTempMaterials` and removed when the coroutine destroys it;
    `OnDestroy` destroys any still listed.
  - The handoff doc's §1.10 moved to §0.
- **Verification:** all four gate scripts pass. Needs Yash's retest on `Bug_Hunt`: the same
  play path should leave the Material count flat and show no `Crystal (Instance)` entries.
- **PR/commit:** pending.

---

## BH-1.8 — `Cell.countGrids` never disposed on destroy

- **Date:** fixed 2026-09-26 (commit), merged 2026-09-29 04:46.
- **Symptom:** after a domain reload or editor quit (not during play):
  `Leak Detected : Persistent allocates N individual allocations`. The Console was empty after
  an editor restart; the report was in `Editor-prev.log`.
- **Root cause:** `Cell.SetupDensityGrids` disposes the old density grids when it rebuilds them,
  but `Cell.OnDestroy` never disposed the last set. A cell owns 4 `BlockCountDensityGrid`s (Jade,
  Ruby, Gold, plus the Blue all-domain grid), and each allocates 6 `Allocator.Persistent`
  `NativeArray`s in `BlockDensityGrid.Init`. That's 24 leaked allocations per destroyed cell.
- **Repro (unfixed `bleeding-edge`):** set Preferences > Jobs > Leak Detection Level to
  *Enabled With Stack Trace*. Play a few arcade games, exit play mode, restart the editor, then
  read `Editor-prev.log`. Yash got `Leak Detected : Persistent allocates 48 individual
  allocations` and `... 168 ...`, with stacks at `BlockDensityGrid.Init` (BlockDensityGrid.cs:316-321)
  ← `BlockCountDensityGrid..ctor` (:468). Both counts are multiples of 24. Earlier logs without
  stack traces had the same shape: `264` (0510 log line 112170, also in the 0447 log) and `24`
  (0510 log line 159993), each right after a script recompile.
- **Fix:** `Assets/_Scripts/Controller/Environment/Cell.cs`.
  - `OnDestroy` disposes every grid (null-safe) and clears `countGrids`.
    `BlockDensityGrid.Dispose` is idempotent (guarded by `jobSystemInitialized` plus `IsCreated`),
    so the rebuild path can't double-free.
  - `AddBlock`/`RemoveBlock` read the Jade/Ruby/Gold grids with `TryGetValue`, as the Blue grid
    already was, so a prism removed after its cell during teardown can't throw
    `KeyNotFoundException` on the emptied map.
  - The handoff doc's §1.8 moved to §0.
- **Verification:** Yash retested on `Bug_Hunt` (314ad51) with the same play path and forced a
  recompile (saved a `.cs`). There were no leak reports and no `KeyNotFoundException`. All four
  gate scripts passed.
- **PR/commit:** `2195795` · PR #926 · merge `546bda3`.

---

## CC-4 — `[PrismRenderVisibilityFlush]` re-created on play-mode exit

- **Date:** 2026-09-26 (part of PR #905).
- **Symptom:** on stopping play mode: `Some objects were not cleaned up when closing the scene.
  (Did you spawn new GameObjects from OnDestroy?)` listing `[PrismRenderVisibilityFlush]`. The
  Unity stack was `ValidateNoSceneObjectsAreLoaded ← EditorSceneManager::RestoreSceneBackups ←
  PlayerLoopController::ExitPlayMode`.
- **Root cause:** `PrismRenderService.QueueVisible` lazily creates a DontDestroyOnLoad host
  (`EnsureFlushHost`). `Prism.OnDisable` queues a hide for its render entity on every disable,
  including the mass disable at play-mode exit. After the teardown destroyed the host, the next
  `Prism.OnDisable` created a new one, which leaked.
- **Repro:** enter play mode, play, then stop. In the 0510 log, the first session running the
  CC-3 fix (after the recompile and asset reimport at ~line 159984–160204) ended at line 163544
  with only `[PrismRenderVisibilityFlush]` listed. Nothing logs when the host is created, so the
  call path comes from searching the code: `Prism.OnDisable` is the only teardown caller of
  `QueueVisible`.
- **Fix:**
  - `PrismRenderService`: `IsQuitting` flag set from `Application.quitting` (which also fires on
    editor play-mode exit) and reset on `SubsystemRegistration`. While it is set, `QueueVisible`
    returns early, and `EnsureFlushHost` never creates the host while quitting or when
    `!Application.isPlaying`.
  - The same guard went into `PrismShieldShatter.TryRequest`/`EnsureHost` and
    `PrismDebris.EnsureHost`.
- **Verification:** Yash tested PR #905 and confirmed the cleanup error was gone.
- **PR/commit:** `f02cef8` · PR #905 · merge `44a9d5f`.

## CC-3 — `[PrismTimerManager]` / `[PrismDebris]` re-created during scene teardown

- **Date:** 2026-09-26 (part of PR #905).
- **Symptom:** `Some objects were not cleaned up when closing the scene` listing
  `[PrismTimerManager]`, on scene switches (`UnloadGameScene`) and on play-mode exit. On exit it
  sometimes also listed `[PrismDebris]`. The 0447 log has 9 occurrences.
- **Root cause:** `Spindle.OnDisable` has a "scene unloading, don't run the death cascade"
  guard, but `parentSpindle.RemoveSpindle` and `LifeForm.RemoveSpindle` call
  `CheckForLife`/`CheckIfDead` themselves, so the cascade ran anyway during teardown.
  - The parent spindle evaporated, and `PrismTimerManager.EnsureInstance()` found the scene's
    manager already destroyed, so it auto-created a new one.
  - The LifeForm died, its structure exploded, and `PrismDebris` re-created its host.
- **Repro/evidence:** each leak is preceded by `[PrismTimerManager] No instance found in scene -
  auto-created.` with the managed stack `EnsureInstance ← Spindle.StampDeathFade ←
  StampEvaporate ← EvaporateSpindle ← CheckForLife ← … ← LifeForm.RemoveSpindle ←
  PhyllotacticFlora.RemoveSpindle ← Spindle.OnDisable`. The native frames below it are
  `UnloadGameScene` or `RestoreSceneBackups`/`ExitPlayMode`. See the 0447 log lines 7749→7828,
  10526→10605 and 14684→14738.
- **Fix:**
  - `Spindle.OnDisable`: on unload it drops the parent's reference directly and skips
    `LifeForm.RemoveSpindle`.
  - `PrismTimerManager.EnsureInstance()` returns null while quitting (`Application.quitting`,
    `ApplicationLifecycleManager.IsQuitting`) or while its own scene is unloading (flag set in
    `OnDestroy`, cleared on `sceneUnloaded`). Every caller now uses `?.`.
  - `PrismDebris.TryRequestExplosion/Implosion` return false after `Application.quitting`.
- **Verification:** in the 0510 log's post-fix session, `[PrismTimerManager]` was only
  auto-created during normal scene loads (Authentication, Menu_Main). There was no cleanup error
  on those scene switches, and the error on exit listed only `[PrismRenderVisibilityFlush]`
  (CC-4).
- **PR/commit:** `cfcd671` · PR #905 · merge `44a9d5f`.

## CC-2 — Ability map assets fail to parse (Squirrel, Dolphin, Rhino, Sparrow)

- **Date:** 2026-09-26 (part of PR #905).
- **Symptom:**
  - `Unable to parse file Assets/Resources/ElementalAbilityMaps/Squirrel.asset: [Parser Failure at line 52: Expect ':' between key and value within mapping]`
  - the same for `Dolphin.asset` (line 76)

  The whole asset fails to load.
- **Root cause:** `AbilityDescription`/`UpgradeDescription` were multi-line **plain (unquoted)**
  YAML scalars containing `: `, with continuation lines ending in a colon
  (`ILifeFormEntity.Nourish:`, `boost speed:`, `SPEED:`). Unity only reported the files that
  have a line ending in a colon. Rhino and Sparrow have only mid-line `: `, which Unity
  tolerated, but they fail a strict YAML parse.
- **Fix:** each offending field became a single-quoted scalar (`''` escapes an apostrophe), the
  way Unity writes long strings itself. Line breaks were kept, so the loaded text is identical.
  No generator writes these four maps.
- **Verification:** after the reimport in the 0510 log (~160189–160204), the post-fix session
  has no parse errors. A strict YAML parse passes for all 8 ability maps.
- **PR/commit:** `d7e3617` (Squirrel), `8df5d33` (Dolphin, Rhino, Sparrow) · PR #905 · merge `44a9d5f`.

## CC-1 — Rampage Cell Configs fail to parse; generator emitted invalid YAML

- **Date:** 2026-09-26 (part of PR #905).
- **Symptom:** `Unable to parse file Assets/_SO_Assets/Cell Configs/Rampage Cell/Rampage Cell
  Config 1.asset: [Parser Failure at line 28: Expect ':' between key and value within mapping]`.
- **Root cause:** `Tools/Build/rampage_intensity.py` (`_wrap_yaml_scalar`) wrote the long
  `Description` as a wrapped plain scalar containing `: `, and in Config 1 one wrapped line ends
  in `hit:`. Configs 2–4 had the same invalid YAML (mid-line colons only).
- **Fix:** the generator now emits a single-quoted scalar, and all four configs were regenerated
  from it. Only the quoting changed. `rampage_intensity.py --check` passes.
- **Verification:** no parse error after the reimport (0510 log, post-fix session). A strict
  YAML parse passes for all four configs.
- **PR/commit:** `a227e1e` · PR #905 · merge `44a9d5f`.

---

## Known open console issues

Checked against `Bug_Hunt` @ `546bda3` on 2026-09-29.

- **17 assets fail a strict YAML parse.** Unity has not reported these (it tolerates a mid-line
  `: `), but they are invalid YAML, and the next edit that wraps a line ending in `:` will break
  them. Fix the generator where there is one (PLAYBOOK §3).
  - `Boneyard Cell Config 1-4`: `author_dogfight_assets.py`
  - `Regatta Cell Config 1-4`: `author_regatta_assets.py`
  - `Tollway Cell Config 1-4`: `author_tollway_assets.py`
  - `ArcadeGameBroadside`: `author_broadside_assets.py`
  - `ArcadeGameWaystation`: `author_waystation_assets.py` (**new since PR #905**, added by
    `de3a4c8`)
  - `SO_Captain_Dolphin_Space`: `Flavor: “…Death: The…”` is unquoted
  - `SO_Captain_Sparrow_Charge`: tab-indented `Space:`/`Time:` lines
  - `SO_Captain_Sparrow_Space`: `IconActive:` is on the same line as `HeadshotImage: {fileID: 0}`,
    so the value is probably lost
- **`NullReferenceException` in `Crystal.ActivateCrystal`** (Crystal.cs:699,
  `transform.parent = cellData.Cell.transform;`), called from `Fauna.ReleaseHeart ←
  LightFauna.WitherCoroutine`. It appears 3 times in the 0447 log, e.g. line 44274, about 100
  lines before a scene-cleanup error. It is likely a teardown-order problem: the cell is gone
  before the wither coroutine reaches the heart (PLAYBOOK §4). Not fixed.
