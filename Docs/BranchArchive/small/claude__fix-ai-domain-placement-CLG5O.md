# Branch archive: `claude/fix-ai-domain-placement-CLG5O`

_Snapshot 2026-10-08. Index: [README](../README.md)_

- **Last commit:** 2026-05-12 by Claude
- **Unmerged commits:** 1
- **Forked from:** `a6c25b9f5` (2026-05-11, rhino sword)
- **Tip:** `d1d0af89f`
- **Files touched (3):**
  - `Assets/_Scripts/Controller/Multiplayer/ServerPlayerVesselInitializerWithAI.cs`
  - `Assets/_Scripts/Controller/Player/Player.cs`
  - `CLAUDE.md`

### `d1d0af89f` — fix(domains): balanced AI tie-break + Random click defers to scene-spawn

_Claude, 2026-05-12 10:26:54 +0000_

```text
Two regressions from the profile-icon-sync arc.

1. ServerPlayerVesselInitializerWithAI.GetBalancedDomain — tie-break

   0790358 (Phase 4) flipped this from random to deterministic ActiveDomains
   enum order with the rationale "match user spec walkthroughs". That misread
   the spec: with all domains tied at the minimum, Jade always won, so the
   first AI in a fresh game was always Jade, then Ruby, then Gold, every time.

   Now: pick uniformly at random among all active domains tied at the minimum
   count. When a single domain has the lowest count it's still returned
   outright. Server-only, so all clients see the same outcome via NetDomain
   replication — no shared seed needed.

   Allocates a tiny static List<Domains>(3) scratch buffer once for the AI
   spawn loop instead of allocating per AI.

2. Player.RequestSetDomain_ServerRpc(Blue) — Random click

   2c9a670 changed Random click from "write Blue (sentinel)" to "roll a
   uniform-random active domain and commit it at click time". That ignored
   existing population entirely — Random against Jade=2, Ruby=1, Gold=0 still
   gave Jade a 1-in-3 chance.

   Reverted to writing Blue. NormalizeUnassignedHumans at scene-spawn folds
   every Random-picker through the same balanced algorithm as AI fill, so all
   Blue-state humans resolve against the same human+AI snapshot instead of
   committing mid-picker against a stale count.

   Avatar chips stay on the Random tile through the picker phase — players
   see who's still undecided. They migrate to the assigned team tile only at
   scene-load, by which point the modal is closed and no UI flicker is visible.

Updated CLAUDE.md to describe the new tie-break and Random handling.
```

```text
 .../Controller/Multiplayer/ServerPlayerVesselInitializerWithAI.cs     | 33 ++++++++++++++++++++++-----------
 Assets/_Scripts/Controller/Player/Player.cs                           | 27 +++++++++++++++------------
 CLAUDE.md                                                             |  2 +-
 3 files changed, 38 insertions(+), 24 deletions(-)
```

<details><summary>Patch (code/doc/text files)</summary>

```diff
diff --git a/Assets/_Scripts/Controller/Multiplayer/ServerPlayerVesselInitializerWithAI.cs b/Assets/_Scripts/Controller/Multiplayer/ServerPlayerVesselInitializerWithAI.cs
index 14d3f167c..197820e4f 100644
--- a/Assets/_Scripts/Controller/Multiplayer/ServerPlayerVesselInitializerWithAI.cs
+++ b/Assets/_Scripts/Controller/Multiplayer/ServerPlayerVesselInitializerWithAI.cs
@@ -190,10 +190,11 @@ namespace CosmicShore.Gameplay
         }
 
         /// <summary>
-        /// Returns the active domain with the fewest players. Ties are broken
-        /// deterministically by <see cref="GameDataSO.ActiveDomains"/> enum order
-        /// (Jade → Ruby → Gold), so identical inputs produce identical AI
-        /// distributions across machines without needing a shared RNG seed.
+        /// Returns one of the active domains tied for the fewest players. When a single
+        /// domain has the lowest count it's returned outright; when multiple domains tie
+        /// at the minimum, one is chosen uniformly at random.
+        /// Server-only — runs on the host, so all clients see the same outcome via
+        /// NetDomain replication.
         /// </summary>
         static Domains GetBalancedDomain(Dictionary<Domains, int> counts)
         {
@@ -201,18 +202,28 @@ namespace CosmicShore.Gameplay
             foreach (var v in counts.Values)
                 if (v < min) min = v;
 
-            // Iterate ActiveDomains in order so the first match (== smallest team
-            // with the lowest enum index) wins ties deterministically.
+            // Collect every active domain tied at the minimum count. Iterate
+            // ActiveDomains (not counts.Keys) so the candidate list has a stable
+            // order regardless of dictionary hashing; the random pick below is
+            // the only source of nondeterminism.
+            _tiedBuf.Clear();
             foreach (var d in GameDataSO.ActiveDomains)
                 if (counts.TryGetValue(d, out var c) && c == min)
-                    return d;
+                    _tiedBuf.Add(d);
 
-            // Should never happen if counts is initialized from BuildInitialCounts,
-            // but degrade gracefully rather than throw.
-            CSDebug.LogError("[ServerPlayerVesselInitializerWithAI] GetBalancedDomain: counts dict is empty");
-            return GameDataSO.ActiveDomains[0];
+            if (_tiedBuf.Count == 0)
+            {
+                CSDebug.LogError("[ServerPlayerVesselInitializerWithAI] GetBalancedDomain: no active domains in counts");
+                return GameDataSO.ActiveDomains[0];
+            }
+
+            return _tiedBuf[UnityEngine.Random.Range(0, _tiedBuf.Count)];
         }
 
+        // Reused per call to keep AI spawning allocation-free. Server-only access on
+        // the main thread — no concurrency concerns.
+        static readonly List<Domains> _tiedBuf = new(GameDataSO.ActiveDomains.Length);
+
         /// <summary>
         /// Gathers human Player objects from NetworkManager.ConnectedClients.
         /// gameData.Players is empty at this point (cleared by ResetRuntimeData
diff --git a/Assets/_Scripts/Controller/Player/Player.cs b/Assets/_Scripts/Controller/Player/Player.cs
index e376714b8..975d28561 100644
--- a/Assets/_Scripts/Controller/Player/Player.cs
+++ b/Assets/_Scripts/Controller/Player/Player.cs
@@ -68,24 +68,27 @@ namespace CosmicShore.Gameplay
         /// <summary>
         /// Owner-initiated request to change this player's domain.
         /// NetDomain is server-write, so clients route their selections through this RPC.
-        /// Special case: <see cref="Domains.Blue"/> means "Random" — the server rolls
-        /// a real domain from <see cref="GameDataSO.ActiveDomains"/> instead of writing
-        /// the sentinel. All other inputs must match an active domain or are rejected.
+        ///
+        /// <see cref="Domains.Blue"/> is the "Random / no pick yet" sentinel. The server
+        /// keeps it Blue here — clicking Random does NOT commit a domain at picker time.
+        /// All Blue-state humans are resolved together at scene-spawn by
+        /// <c>ServerPlayerVesselInitializerWithAI.NormalizeUnassignedHumans</c>, which
+        /// uses the same balanced algorithm as AI fill (least-populated domain first,
+        /// uniform random among ties). Resolving once at spawn-time means every Random
+        /// picker fills against the same snapshot of humans + AI, not a stale mid-picker
+        /// count.
+        ///
+        /// Any other value must be in <see cref="GameDataSO.ActiveDomains"/> or it's rejected.
         /// </summary>
         [ServerRpc] // RequireOwnership = true is the default — only the player's owner may request
         public void RequestSetDomain_ServerRpc(Domains domain)
         {
-            // "Random" — pick a real active domain server-side. Blue is the sentinel
-            // for "no pick yet"; clicking the Random tile means "commit me to something".
+            // Random pick: keep Blue. The scene-spawn balance pass handles all
+            // Blue-state humans together with full info instead of committing
+            // each one mid-picker against a stale count.
             if (domain == Domains.Blue)
             {
-                var actives = GameDataSO.ActiveDomains;
-                if (actives == null || actives.Length == 0)
-                {
-                    Debug.LogWarning("[Player] Random pick requested but ActiveDomains is empty.");
-                    return;
-                }
-                NetDomain.Value = actives[UnityEngine.Random.Range(0, actives.Length)];
+                NetDomain.Value = Domains.Blue;
                 return;
             }
 
diff --git a/CLAUDE.md b/CLAUDE.md
index 79b8e781d..d541fe507 100644
--- a/CLAUDE.md
+++ b/CLAUDE.md
@@ -42,7 +42,7 @@ Cross-client domain sync is driven entirely by `Player.NetDomain` (server-write
 
 Do not snapshot domain at component-creation time. Either subscribe to `Player.NetDomain.OnValueChanged` directly or read the live `Player.Domain` mirror each time you need it. `RoundStats.Domain` is also live (after Phase 5) so end-game UIs can keep using it.
 
-`ServerPlayerVesselInitializerWithAI.GetBalancedDomain` ties break by `ActiveDomains` enum order (Jade → Ruby → Gold), not RNG, so identical inputs produce identical AI distributions across machines without needing a shared seed.
+`ServerPlayerVesselInitializerWithAI.GetBalancedDomain` always picks the domain with the fewest players; when multiple domains tie at the minimum, one is chosen uniformly at random. Runs server-only — all clients see the same outcome via `NetDomain` replication. Humans who clicked the Random tile keep `NetDomain = Blue` through the picker and are folded into the same balanced pass by `NormalizeUnassignedHumans` at scene-spawn time (so all Random pickers fill against the same human+AI snapshot, not stale mid-picker counts).
 
 ### Tech Stack
 
```

</details>
