# Branch archive: `claude/add-coding-standards-JX6VT`

_Snapshot 2026-10-08. Index: [README](../README.md)_

- **Last commit:** 2026-02-25 by Claude
- **Unmerged commits:** 1
- **Forked from:** `d5e2a48dc` (2026-02-25, Merge pull request #113 from froglet-studio/claude/connect-menu-main-scene-0TM)
- **Tip:** `abb31bfbc`
- **Files touched (1):**
  - `CLAUDE.md`

### `abb31bfbc` — Add SOLID principles and clean code standards to CLAUDE.md

_Claude, 2026-02-25 09:01:05 +0000_

```text
Adds a comprehensive coding standards section covering all five SOLID
principles and AAA-industry clean code practices, tailored to the
project's Unity/C# architecture and existing conventions.
```

```text
 CLAUDE.md | 23 +++++++++++++++++++++++
 1 file changed, 23 insertions(+)
```

<details><summary>Patch (code/doc/text files)</summary>

```diff
diff --git a/CLAUDE.md b/CLAUDE.md
index 18ed57502..b7ea6d358 100644
--- a/CLAUDE.md
+++ b/CLAUDE.md
@@ -83,6 +83,29 @@ All game code lives under `CosmicShore.*`:
 - Prefer expression-bodied members for simple accessors: `public Transform Transform => transform;`
 - Anti-spam / cooldown patterns belong in the SO config, not hardcoded
 
+## Coding Standards — SOLID Principles & Clean Code (AAA Industry Standards)
+
+All code must adhere to SOLID principles and clean code practices consistent with AAA game studio standards.
+
+### SOLID Principles
+
+- **Single Responsibility (SRP)**: Every class and method does exactly one thing. A MonoBehaviour that handles input should not also manage UI. A ScriptableObject that defines config should not execute runtime logic. If a class has more than one reason to change, split it.
+- **Open/Closed (OCP)**: Systems are open for extension, closed for modification. Use abstract base classes (`ImpactorBase`), interfaces (`IImpactor`, `IImpactCollider`), and ScriptableObject-driven config to add new behaviors without editing existing, stable code.
+- **Liskov Substitution (LSP)**: Derived classes must be fully substitutable for their base types. If `VesselBase` defines a contract, every vessel subclass must honor it without special-case checks or broken preconditions.
+- **Interface Segregation (ISP)**: Prefer small, focused interfaces over large ones. A class should never be forced to implement methods it doesn't use. Split broad interfaces into role-specific ones (e.g., `IDamageable`, `IPoolable`, `IConfigurable` rather than a single `IEntity`).
+- **Dependency Inversion (DIP)**: High-level gameplay systems depend on abstractions, not concrete implementations. Inject dependencies through serialized fields, ScriptableObject references, or constructor/init methods — not by reaching into singletons or using `FindObjectOfType`.
+
+### Clean Code Practices
+
+- **Meaningful naming**: Names must reveal intent. `remainingBoostDuration` over `t`. `ApplyDamageToShield()` over `Process()`. No abbreviations except universally understood ones (`SO`, `UI`, `FX`).
+- **Small, focused methods**: Methods do one thing and do it well. If a method exceeds ~20 lines, it likely needs extraction. Each method operates at a single level of abstraction.
+- **No magic numbers**: All numeric constants are named and placed in SO configs or `const`/`static readonly` fields. `private const float DashCooldownSeconds = 0.5f;` — never a raw `0.5f` inline.
+- **Fail fast and explicitly**: Validate at boundaries, assert preconditions with clear messages. Use guard clauses to exit early rather than deep nesting.
+- **DRY without premature abstraction**: Eliminate genuine duplication, but don't abstract until a pattern repeats at least three times with the same shape. Three similar lines are better than one fragile helper used once.
+- **Minimal public surface**: Expose only what other systems need. Default to `private`. Use `[SerializeField]` for Inspector access, `internal` for assembly-scoped access, `public` only for true API contracts.
+- **Consistent formatting**: Follow the project's existing conventions for bracing, spacing, and file organization. New code should be indistinguishable in style from surrounding code.
+- **No dead code**: Remove unused variables, methods, commented-out blocks, and unreachable branches. Version control is the archive — the codebase is not.
+
 ## Debugging Methodology
 
 When investigating issues, follow this systematic approach:
```

</details>
