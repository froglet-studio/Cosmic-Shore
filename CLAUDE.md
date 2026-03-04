# CLAUDE.md – Cosmic Shore AI Assistant Guide

This file provides context, conventions, and workflows for AI assistants (Claude and others) working in this repository.

---

## Project Overview

**Cosmic Shore** is a live-service mobile/PC game built on **Unity 6000.0.62f1**. It is a multiplayer arcade game with ships, elemental mechanics, minigames, and a social meta-layer (squads, quests, leaderboards).

- **Primary targets**: PC (editor/dev), iOS, Android
- **Secondary targets**: WebGL
- **Engine**: Unity 6000.0.62f1
- **Language**: C# (1,003+ scripts)
- **License**: MIT
- **Organization**: Froglet Games / froglet-studio

---

## Repository Layout

```
/
├── Assets/                     # All game content and code
│   ├── Scripts/
│   │   ├── App/                # App-layer: UI screens, systems, services
│   │   │   ├── Systems/        # Feature systems (Ads, Audio, Quests, Squads, XP, Loadout…)
│   │   │   └── UI/             # Menu screens, modals, UI elements
│   │   ├── Core/               # Core engine managers (GameManager, CameraManager, etc.)
│   │   ├── Game/               # Gameplay logic
│   │   │   ├── Arcade/         # Minigame modes
│   │   │   ├── AI/             # Opponent AI
│   │   │   ├── Animation/      # Animation controllers
│   │   │   ├── Camera/         # Cinemachine-based camera system
│   │   │   ├── FX/             # Visual and impact effects
│   │   │   ├── Managers/       # Game-state managers (Arcade, Hangar…)
│   │   │   ├── Multiplayer/    # Netcode game logic
│   │   │   ├── Ship/           # Vessel mechanics and properties
│   │   │   └── UI/             # In-game HUD controllers
│   │   ├── Models/             # Data definitions (Enums, Structs, ScriptableObjects)
│   │   ├── Utilities/          # Helpers (Pools, Network, Reporting, Effects)
│   │   ├── Integrations/       # Third-party (Firebase, PlayFab, Analytics)
│   │   ├── DialogueSystem/     # Dialogue management
│   │   ├── Services/           # Auth and other service abstractions
│   │   └── Soap/               # SOAP event-system utilities
│   ├── Scenes/                 # Unity scene files
│   ├── Prefabs/                # Reusable game object prefabs
│   ├── ScriptableObjects/      # Data assets (ship configs, captain data, events…)
│   ├── Shaders/                # HLSL / URP shader files
│   └── Plugins/                # Third-party plugins (SOAP, etc.)
├── ProjectSettings/            # Unity project configuration
├── Packages/
│   └── manifest.json           # Package Manager dependencies (76+ packages)
├── Docs/                       # Technical documentation
├── GIT_RULES.md                # Branching, commit, and PR standards
└── README.md                   # Project overview
```

---

## Technology Stack

| Category | Technology |
|---|---|
| Engine | Unity 6000.0.62f1 |
| Language | C# |
| Async | Cysharp UniTask (async/await) |
| DI | VContainer 1.6.3 |
| Networking | Unity Netcode for GameObjects 2.5.0 + Transport 2.6.0 |
| UI | Unity UGUI 2.0.0 + UIElements |
| Rendering | Universal Render Pipeline (URP) 17.0.4 |
| VFX | Unity Visual Effect Graph 17.0.4 |
| Animation | Cinemachine 3.1.2, Animation Rigging 1.3.0 |
| Events | SOAP (Scriptable Object As Property) |
| Analytics | Firebase, Unity Analytics, PlayFab |
| Audio | Wwise |
| Ads | Unity Ads |
| IAP | Unity In-App Purchasing 4.12.2 |
| Testing | Unity Test Framework 1.6.0 |

---

## Namespace Conventions

All code lives under the `CosmicShore.*` root namespace:

```
CosmicShore.App.*              App layer (UI, Systems)
CosmicShore.Game.*             Core gameplay systems
CosmicShore.Core.*             Core managers (GameManager, CameraManager)
CosmicShore.Models.*           Data models (Enums, Structs, ScriptableObjects)
CosmicShore.Utilities.*        Helper functions and utilities
CosmicShore.Integrations.*     Third-party integrations (Firebase, PlayFab)
CosmicShore.DialogueSystem.*   Dialogue management
CosmicShore.Services.*         Service layer (Auth)
CosmicShore.Soap.*             SOAP event-system utilities
```

---

## Naming Conventions

| Category | Convention | Example |
|---|---|---|
| Classes | PascalCase | `GameManager`, `DuelGameController` |
| Methods | PascalCase | `RestartGame()`, `LaunchGameScene()` |
| Private fields | camelCase with `_` or `m_` prefix | `_sceneNames`, `m_Profile` |
| Public properties | PascalCase with accessors | `Profile { get; set; }` |
| Constants | ALL_CAPS | `WAIT_FOR_SECONDS_BEFORE_SCENELOAD` |
| Enums (type) | PascalCase | `enum Element` |
| Enum values | PascalCase | `Charge`, `Mass`, `Space`, `Time`, `Omni` |
| ScriptableObjects | `SO_` or `Scriptable` prefix | `SO_Captain`, `ScriptableEventBool` |
| SerializeField | `[SerializeField] private Type _name` | `[SerializeField] SceneNameListSO _sceneNames;` |

---

## Architecture Patterns

### 1. Manager Pattern (Singleton-like)
Core systems use manager classes that act as singletons:
- `GameManager` – game flow and scene loading
- `CameraManager` – camera control
- `StatsManager` – game statistics
- `ThemeManager` – visual theming

### 2. SOAP Event System
ScriptableObject-based events for decoupled communication. Events are defined as assets:
```csharp
// Raise an event
_onSceneTransition.Raise(true);

// Subscribe in inspector or via code
[SerializeField] ScriptableEventBool _onSceneTransition;
```
Event types: `ScriptableEventBool`, `ScriptableEventShipClassType`, etc.
Base class: `ScriptableEvent<T>`

### 3. EventBus Architecture
Centralized event buses for cross-system communication (e.g., `LoginEventBus`).

### 4. MVC/MVVM for UI
UI components follow a Controller-View-Model split:
- **Controllers**: input handling, logic (`DialogueUIController`, `VesselHUDController`)
- **Views**: rendering (`MinigameHUDView`)
- **Models**: state data

### 5. ScriptableObjects for Configuration
Game data (ships, abilities, captains) is defined as SO assets, not hardcoded. This enables designer iteration without code changes.

### 6. Dependency Injection
VContainer is used for DI in complex systems. Interfaces abstract implementations (`ICameraController`, `ICameraConfigurator`).

### 7. UniTask for Async
Prefer `UniTask` over `Coroutine` or `Task` for all async operations.

---

## Key Files to Know

| File | Purpose |
|---|---|
| `GIT_RULES.md` | **Read this before committing.** Full branching, commit, and PR standards. |
| `Packages/manifest.json` | All Unity Package Manager dependencies |
| `ProjectSettings/ProjectSettings.asset` | Unity project settings (company, version, etc.) |
| `Assets/Scripts/Core/GameManager.cs` | Top-level game flow controller |
| `Assets/Scripts/Core/CameraManager.cs` | Camera system entry point |

---

## Development Workflow

### Git Branching
Follow the conventions in `GIT_RULES.md`:
- `feature/<area>-<description>` – new features
- `bugfix/<area>-<description>` – bug fixes
- `hotfix/<area>-<description>` – urgent fixes to production
- `chore/<area>-<description>` – maintenance, configs

`main` is always stable. **Never push directly to `main`.**

### Commit Message Format
```
<type>(<optional-scope>): <summary in imperative mood>
```

**Types**: `feat`, `fix`, `refactor`, `chore`, `docs`, `test`, `perf`

**Examples**:
```
feat(arcade): add swap-tray powerup
fix(scoring): compute round2 score using volume delta
refactor(camera): extract orbit logic into separate controller
chore(packages): update URP to 17.0.4
```

**Rules**:
- Imperative mood: "add", not "added"
- Max ~72 characters on first line
- No trailing period
- No emojis

### Pull Requests
- Squash-and-merge into `main`
- Use the PR template in `GIT_RULES.md` (summary, motivation, changes, testing, risks)
- Include screenshots/video for any UI or gameplay changes
- Rebase on `main` before opening PR; resolve all conflicts

---

## Testing

- The project primarily relies on **manual playtesting** (automated unit tests are minimal).
- Unity Test Framework is available but sparsely used; tests live in `Assets/Plugins/Obvious/Soap/Core/Editor/Tests/`.
- When writing new logic, prefer verifiable, isolated methods that can be tested.
- PR checklist (from `GIT_RULES.md`):
  - [ ] Unity project opens without compilation errors
  - [ ] Manual playtest: main menu → Arcade mode, Duel mode
  - [ ] Platform builds tested (Android / iOS / WebGL as applicable)

---

## What to Commit and What to Ignore

**Always commit:**
- `Assets/` (code, scenes, prefabs, ScriptableObjects, shaders)
- `ProjectSettings/`
- `Packages/manifest.json`
- All `*.meta` files (required by Unity)

**Never commit:**
- `Library/`, `Temp/`, `Logs/`, `Obj/`, `Build/`
- `.vs/`, `*.user`, `*.csproj` personal edits
- API keys, secrets, passwords
- Leftover `Debug.Log("test")` statements
- Commented-out blocks of old code

---

## Unity-Specific Conventions

- **Save scenes and prefabs** intentionally before committing — avoid noise in unrelated assets.
- **Avoid heavy logic in `Update()`** — use events, coroutines (or UniTask), or state machines.
- Prefer **ScriptableObject-based data** over hardcoded values.
- Use `[SerializeField] private` for inspector-visible fields, not `public`.
- Null-check references that may not be assigned in the inspector.
- Use `UniTask` for all async operations, not raw `Task` or `IEnumerator` coroutines.

---

## AI Assistant Guidelines

When making changes to this codebase:

1. **Read before editing.** Always read the target file before modifying it.
2. **Follow namespace conventions.** New scripts must be in the appropriate `CosmicShore.*` namespace.
3. **Match existing patterns.** Use SOAP events for cross-system communication, ScriptableObjects for data, and managers for core system state.
4. **Prefer UniTask over coroutines.** Any new async code should use `UniTask`.
5. **No direct `main` pushes.** All changes go through a feature/fix branch and PR.
6. **Commit atomically.** One logical change per commit with a proper `type(scope): summary` message.
7. **Do not commit Unity-generated files.** `Library/`, `Temp/`, `.vs/`, etc. are all gitignored.
8. **Always include `*.meta` files** when adding or moving Unity assets.
9. **Keep scope tight.** Do not refactor unrelated code, add extra logging, or introduce new dependencies unless asked.
10. **No `Debug.Log` in committed code** unless specifically for a logging/diagnostic feature.

---

## Common Gotchas

- **Meta files matter.** Missing or mismatched `.meta` files break asset references in Unity. Always commit the `.meta` alongside its asset.
- **Scene merge conflicts are painful.** Communicate with the team before editing shared scenes. Use prefabs to minimize scene-level changes.
- **SOAP events need a Listener.** Raising a `ScriptableEvent` with no subscribers does nothing — ensure listeners are wired up in the scene or prefab.
- **VContainer lifetimes.** Be aware of scoped vs. singleton lifetimes when registering services with VContainer.
- **UniTask cancellation.** Use `CancellationTokenSource` and propagate tokens properly; fire-and-forget UniTask calls can cause issues on scene unload.
- **Platform preprocessor directives.** Code that calls iOS/Android APIs must be wrapped in `#if UNITY_IOS` / `#if UNITY_ANDROID` guards.
