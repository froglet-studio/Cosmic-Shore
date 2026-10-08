# Branch archive: `claude/claude-md-mmconidf84v88i8m-BpUSq`

_Snapshot 2026-10-08. Index: [README](../README.md)_

- **Last commit:** 2026-03-04 by Claude
- **Unmerged commits:** 1
- **Forked from:** `0d48ab5b5` (2026-02-25, Merge pull request #78 from froglet-studio/claude/add-missing-sounds-1ZgoJ)
- **Tip:** `6b05b5dfd`
- **Files touched (1):**
  - `CLAUDE.md`

### `6b05b5dfd` — docs: add CLAUDE.md with codebase guide for AI assistants

_Claude, 2026-03-04 23:51:23 +0000_

```text
Covers project structure, tech stack, namespace conventions, naming
rules, architecture patterns, git workflow, testing, and common gotchas.
```

```text
 CLAUDE.md | 274 ++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++
 1 file changed, 274 insertions(+)
```

<details><summary>Patch (code/doc/text files, first 150 of 280 lines)</summary>

```diff
diff --git a/CLAUDE.md b/CLAUDE.md
new file mode 100644
index 000000000..bdc5b2ef1
--- /dev/null
+++ b/CLAUDE.md
@@ -0,0 +1,274 @@
+# CLAUDE.md – Cosmic Shore AI Assistant Guide
+
+This file provides context, conventions, and workflows for AI assistants (Claude and others) working in this repository.
+
+---
+
+## Project Overview
+
+**Cosmic Shore** is a live-service mobile/PC game built on **Unity 6000.0.62f1**. It is a multiplayer arcade game with ships, elemental mechanics, minigames, and a social meta-layer (squads, quests, leaderboards).
+
+- **Primary targets**: PC (editor/dev), iOS, Android
+- **Secondary targets**: WebGL
+- **Engine**: Unity 6000.0.62f1
+- **Language**: C# (1,003+ scripts)
+- **License**: MIT
+- **Organization**: Froglet Games / froglet-studio
+
+---
+
+## Repository Layout
+
+```
+/
+├── Assets/                     # All game content and code
+│   ├── Scripts/
+│   │   ├── App/                # App-layer: UI screens, systems, services
+│   │   │   ├── Systems/        # Feature systems (Ads, Audio, Quests, Squads, XP, Loadout…)
+│   │   │   └── UI/             # Menu screens, modals, UI elements
+│   │   ├── Core/               # Core engine managers (GameManager, CameraManager, etc.)
+│   │   ├── Game/               # Gameplay logic
+│   │   │   ├── Arcade/         # Minigame modes
+│   │   │   ├── AI/             # Opponent AI
+│   │   │   ├── Animation/      # Animation controllers
+│   │   │   ├── Camera/         # Cinemachine-based camera system
+│   │   │   ├── FX/             # Visual and impact effects
+│   │   │   ├── Managers/       # Game-state managers (Arcade, Hangar…)
+│   │   │   ├── Multiplayer/    # Netcode game logic
+│   │   │   ├── Ship/           # Vessel mechanics and properties
+│   │   │   └── UI/             # In-game HUD controllers
+│   │   ├── Models/             # Data definitions (Enums, Structs, ScriptableObjects)
+│   │   ├── Utilities/          # Helpers (Pools, Network, Reporting, Effects)
+│   │   ├── Integrations/       # Third-party (Firebase, PlayFab, Analytics)
+│   │   ├── DialogueSystem/     # Dialogue management
+│   │   ├── Services/           # Auth and other service abstractions
+│   │   └── Soap/               # SOAP event-system utilities
+│   ├── Scenes/                 # Unity scene files
+│   ├── Prefabs/                # Reusable game object prefabs
+│   ├── ScriptableObjects/      # Data assets (ship configs, captain data, events…)
+│   ├── Shaders/                # HLSL / URP shader files
+│   └── Plugins/                # Third-party plugins (SOAP, etc.)
+├── ProjectSettings/            # Unity project configuration
+├── Packages/
+│   └── manifest.json           # Package Manager dependencies (76+ packages)
+├── Docs/                       # Technical documentation
+├── GIT_RULES.md                # Branching, commit, and PR standards
+└── README.md                   # Project overview
+```
+
+---
+
+## Technology Stack
+
+| Category | Technology |
+|---|---|
+| Engine | Unity 6000.0.62f1 |
+| Language | C# |
+| Async | Cysharp UniTask (async/await) |
+| DI | VContainer 1.6.3 |
+| Networking | Unity Netcode for GameObjects 2.5.0 + Transport 2.6.0 |
+| UI | Unity UGUI 2.0.0 + UIElements |
+| Rendering | Universal Render Pipeline (URP) 17.0.4 |
+| VFX | Unity Visual Effect Graph 17.0.4 |
+| Animation | Cinemachine 3.1.2, Animation Rigging 1.3.0 |
+| Events | SOAP (Scriptable Object As Property) |
+| Analytics | Firebase, Unity Analytics, PlayFab |
+| Audio | Wwise |
+| Ads | Unity Ads |
+| IAP | Unity In-App Purchasing 4.12.2 |
+| Testing | Unity Test Framework 1.6.0 |
+
+---
+
+## Namespace Conventions
+
+All code lives under the `CosmicShore.*` root namespace:
+
+```
+CosmicShore.App.*              App layer (UI, Systems)
+CosmicShore.Game.*             Core gameplay systems
+CosmicShore.Core.*             Core managers (GameManager, CameraManager)
+CosmicShore.Models.*           Data models (Enums, Structs, ScriptableObjects)
+CosmicShore.Utilities.*        Helper functions and utilities
+CosmicShore.Integrations.*     Third-party integrations (Firebase, PlayFab)
+CosmicShore.DialogueSystem.*   Dialogue management
+CosmicShore.Services.*         Service layer (Auth)
+CosmicShore.Soap.*             SOAP event-system utilities
+```
+
+---
+
+## Naming Conventions
+
+| Category | Convention | Example |
+|---|---|---|
+| Classes | PascalCase | `GameManager`, `DuelGameController` |
+| Methods | PascalCase | `RestartGame()`, `LaunchGameScene()` |
+| Private fields | camelCase with `_` or `m_` prefix | `_sceneNames`, `m_Profile` |
+| Public properties | PascalCase with accessors | `Profile { get; set; }` |
+| Constants | ALL_CAPS | `WAIT_FOR_SECONDS_BEFORE_SCENELOAD` |
+| Enums (type) | PascalCase | `enum Element` |
+| Enum values | PascalCase | `Charge`, `Mass`, `Space`, `Time`, `Omni` |
+| ScriptableObjects | `SO_` or `Scriptable` prefix | `SO_Captain`, `ScriptableEventBool` |
+| SerializeField | `[SerializeField] private Type _name` | `[SerializeField] SceneNameListSO _sceneNames;` |
+
+---
+
+## Architecture Patterns
+
+### 1. Manager Pattern (Singleton-like)
+Core systems use manager classes that act as singletons:
+- `GameManager` – game flow and scene loading
+- `CameraManager` – camera control
+- `StatsManager` – game statistics
+- `ThemeManager` – visual theming
+
+### 2. SOAP Event System
+ScriptableObject-based events for decoupled communication. Events are defined as assets:
+```csharp
+// Raise an event
+_onSceneTransition.Raise(true);
+
+// Subscribe in inspector or via code
+[SerializeField] ScriptableEventBool _onSceneTransition;
+```
+Event types: `ScriptableEventBool`, `ScriptableEventShipClassType`, etc.
+Base class: `ScriptableEvent<T>`
+
+### 3. EventBus Architecture
+Centralized event buses for cross-system communication (e.g., `LoginEventBus`).
+
+### 4. MVC/MVVM for UI
+UI components follow a Controller-View-Model split:
+- **Controllers**: input handling, logic (`DialogueUIController`, `VesselHUDController`)
+- **Views**: rendering (`MinigameHUDView`)
```

</details>
