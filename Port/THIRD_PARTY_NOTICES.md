# Froglet Engine — third-party notices

Everything under `Port/` is first-party Froglet Inc. code except the components below, which
the engine, the player and the launcher use as libraries. None of them is Unity software, and
no Unity binary is used at build time or at run time.

| Component | Used by | Licence | Ships in a build? |
|---|---|---|---|
| Silk.NET 2.22 (OpenGL, Windowing, Input, Maths, SDL, OpenAL bindings) | engine, player, launcher | MIT | yes |
| GLFW (native, via Silk.NET) | desktop window | zlib | yes |
| SDL2 (native, via Silk.NET) | phone builds | zlib | yes (mobile) |
| OpenAL Soft (native, `Silk.NET.OpenAL.Soft.Native`) | legacy SkimRace/Client builds | **LGPL-2.0** — ship as a separate replaceable DLL with this notice and a link to its source (https://github.com/kcat/openal-soft) | older zips only |
| Dear ImGui (cimgui native) + ImGui.NET + `Silk.NET.OpenGL.Extensions.ImGui` | launcher UI | MIT | launcher only |
| StbImageSharp | texture loading | public domain / MIT | yes |
| Newtonsoft.Json | `CosmicShore.Live` (the game's own scripts use it) | MIT | yes |
| FMOD Studio (Firelight Technologies Pty Ltd.) | audio | **Proprietary — FMOD EULA**; needs an FMOD licence for the shipped tier and the in-game credit "FMOD Studio by Firelight Technologies Pty Ltd." | yes |
| Chakra Petch, Aldrich | launcher UI fonts | SIL Open Font License 1.1 | launcher only |
| Roboto Mono | launcher console font | Apache 2.0 | launcher only |
| xunit, NUnit, Microsoft.NET.Test.Sdk | tests | Apache 2.0 / MIT | never |

## APIs we re-implement (no vendor source used)

The engine re-creates, from public documentation, the *API surface* the game's scripts call, so
those scripts compile unchanged against our code: UnityEngine (as `CosmicShore.Engine`),
TextMesh Pro, Netcode for GameObjects, Unity Gaming Services, Unity.Mathematics/Entities/Jobs/
Burst/Collections, Cinemachine, Animation Rigging, Timeline, VFX Graph, UniTask, DOTween,
Reflex and Obvious SOAP. Matching names and signatures is what makes the game run; the code
behind them is ours. Two places were found to follow published Unity source too closely and are
tracked in `docs/LEGAL_REVIEW.md`.

## The game's content

The engine reads the Unity project's `Assets/` folder (scenes, prefabs, models, textures,
audio) as data. Content bought from the Unity Asset Store is licensed under the Asset Store
EULA; see `Docs/THIRD_PARTY_REGISTER.md` for every item and its status.
