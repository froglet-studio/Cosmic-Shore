# Ownership and Unity dependency — review (Oct 2026)

*An engineering review, not legal advice. Have counsel confirm before a commercial release.*

## 1. Does anything need Unity?

**At run time and build time, no.** No `UnityEngine.dll`, no Unity install path, no Unity package
binary is referenced anywhere under `Port/`. The engine, the player, the launcher and the phone
builds compile with the .NET SDK alone.

**Unity is the editor.** Designers keep authoring scenes, prefabs and assets in Unity; the
engine reads those files from `Assets/` as data and compiles `Assets/_Scripts` against our own
engine. That is the same relationship any tool has with a file it reads.

## 2. How much is ours

| Part | Lines of C# | Whose |
|---|---|---|
| Engine, renderer, content pipeline, player, launcher, tools (`Port/src` minus Game) | ~84,000 | Froglet — written for the port |
| `CosmicShore.Game` (ported game scripts) | ~82,000 | Froglet — the game's own code |
| Tests | ~43,000 | Froglet |
| Third-party libraries | — | Silk.NET, GLFW, SDL, Dear ImGui, StbImageSharp, Newtonsoft.Json (MIT/zlib/PD), FMOD (proprietary) |

The renderer's shaders (~1,800 lines of GLSL) are ours; the skybox is a translation of our own
HyperSea shader.

## 3. The legal shape

- **Re-implementing an API is the safe pattern we use throughout.** The engine provides types
  named like Unity's (`GameObject`, `Transform`, `MonoBehaviour`…) so the game's scripts compile
  unchanged, but the code behind those names is written from public documentation. Matching an
  interface is what *Google v. Oracle* (US Supreme Court, 2021) held to be fair use; it is also
  how Wine, Mono and Godot's C# layer relate to the APIs they mirror.
- **Reading Unity's file formats** (YAML scenes, `.meta`, prefabs) is interoperability with our
  own data.
- **Unity's terms**: the Unity EULA restricts using, decompiling or copying *Unity software*. We
  do neither — no Unity binary is shipped, decompiled or linked. Unity's *reference source*
  (UnityCsReference) is licensed for reading only, so code must not be copied from it.

## 4. Findings and their status

| Item | Finding | Status |
|---|---|---|
| `Mathf.SmoothDamp` | Followed Unity's published reference source line for line | **Fixed** — rewritten from the exact solution of a critically damped spring (different maths: exact exponential, not the reference's polynomial approximation) |
| TextMesh Pro SDF text shader (`Engine/UI/Text/TmpSdfShader.cs`) | Colour/weight/underlay terms follow `TMP_SDF.shader` / `TMPro.cginc` closely (Unity Companion License) | **Open** — rewrite from the SDF technique (Green, SIGGRAPH 2007) and verify the look against screenshots |
| Voronoi hash in `Render/SceneRenderer.cs` | Uses the constants from Unity's Shader Graph documentation snippet | **Open, low risk** — swap for our own hash; the pattern changes, the look does not |
| `Mathf.Approximately`, `Quaternion.kEpsilon` | One-line numeric facts | No action (not protectable expression) |
| Third-party notices | Port had none | **Fixed** — `Port/THIRD_PARTY_NOTICES.md` |

## 5. The real licence questions are vendors, not Unity

- **FMOD** is proprietary: ship it only under a valid FMOD licence for our revenue tier and keep
  the required credit. The libraries we load are FMOD's, taken from the FMOD-for-Unity package.
- **Obvious SOAP** and **DOTween**: we re-implement their APIs and never use their source, so
  shipping the engine needs no licence from them; the Unity build that uses the real packages
  still needs its Asset Store/seat licences (`Docs/THIRD_PARTY_REGISTER.md`).
- **OpenAL Soft** (only in the old SkimRace/Client zips) is LGPL: ship it as a replaceable DLL
  with its notice, or drop it.
- **Asset Store content** in `Assets/` (models, textures, sounds) is used under the Asset Store
  EULA; check each item in the register before shipping it outside Unity.
