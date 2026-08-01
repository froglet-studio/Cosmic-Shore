// NetworkJoustCollisionTurnMonitor.cs
using CosmicShore.Data;
using CosmicShore.ScriptableObjects;
using Unity.Netcode;
using System.Linq;
using CosmicShore.Utility;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Joust objective monitor. Resolves the joust target at <see cref="StartMonitor"/>
    /// from <see cref="EndConditionOverridesSO"/> (Tools &gt; Cosmic Shore &gt; End Game
    /// Conditions; 0 = default 3 - never a per-scene field) and publishes it to
    /// <see cref="GameDataSO.JoustTargetCount"/> on EVERY peer (a scene constant, so no
    /// NetworkVariable leg is needed - deliberate, R10). Owns the collision sync RPC pair
    /// so client-observed collisions reach the server authoritatively. End condition,
    /// remaining display, and the B15 subscription lifecycle come from
    /// <see cref="ObjectiveTurnMonitor"/>.
    /// </summary>
    public class NetworkJoustCollisionTurnMonitor : ObjectiveTurnMonitor
    {
        // RESOLVED joust target - set in StartMonitor. Intentionally NOT a
        // [SerializeField]: end-game counts are authored only via the tool, never
        // per-scene. Do not re-add [SerializeField] here (see /EndGameConditions skill).
        int collisionsNeeded;
        public int CollisionsNeeded => collisionsNeeded;

        public override void StartMonitor()
        {
            // End-game count from the tool: 0 there = default (3). Published per-peer -
            // every machine resolves the same committed asset value.
            var overrides = EndConditionOverridesSO.Instance;
            collisionsNeeded = overrides != null ? overrides.GetJoustCount() : EndConditionOverridesSO.DefaultJoustCount;
            gameData.JoustTargetCount = collisionsNeeded;

            // ALL machines subscribe - clients report their own observed collisions up to
            // the server, and the "jousts remaining" readout reflects the local player's
            // DOMAIN aggregate, which changes whenever ANY teammate jousts. B15
            // bookkeeping is the base's.
            SubscribeRoster();

            CSDebug.Log($"[NetworkJoustMonitor] StartMonitor - IsServer={IsServer}, " +
                $"CollisionsNeeded={collisionsNeeded}, " +
                $"Players={gameData.RoundStatsList.Count}, " +
                $"Names=[{string.Join(", ", gameData.RoundStatsList.Select(s => s.Name))}]");

            RaiseRemainingUI();
            base.StartMonitor();
        }

        protected override void AttachStatsHandlers(IRoundStats stats)
        {
            stats.OnJoustCollisionChanged += OnCollisionChanged;
            stats.OnJoustCollisionChanged += OnAnyJoustChangedUI;
        }

        protected override void DetachStatsHandlers(IRoundStats stats)
        {
            stats.OnJoustCollisionChanged -= OnCollisionChanged;
            stats.OnJoustCollisionChanged -= OnAnyJoustChangedUI;
        }

        void OnAnyJoustChangedUI(IRoundStats _) => RaiseRemainingUI();

        void OnCollisionChanged(IRoundStats stats)
        {
            if (IsServer)
            {
                // Server already has the correct local value from the setter -
                // just broadcast to clients. Do NOT re-assign JoustCollisions here
                // or it will re-trigger this handler and cause infinite recursion.
                SyncCollision_ClientRpc(stats.Name, stats.JoustCollisions);
            }
            else
            {
                // Client detected a collision the server missed (high-speed physics)
                // - report it up so the server can authoritatively sync everyone
                ReportCollision_ServerRpc(stats.Name, stats.JoustCollisions);
            }
        }

        [ServerRpc(RequireOwnership = false)]
        void ReportCollision_ServerRpc(string playerName, int collisionCount)
        {
            var stats = gameData.RoundStatsList.FirstOrDefault(s => s.Name == playerName);
            if (stats == null)
            {
                CSDebug.LogError($"[NetworkJoustMonitor] ServerRpc: no stats for '{playerName}'");
                return;
            }

            // Only accept if the client reports a higher count (prevent stale/duplicate reports)
            if (collisionCount <= stats.JoustCollisions) return;

            stats.JoustCollisions = collisionCount;
            SyncCollision_ClientRpc(playerName, collisionCount);
        }

        [ClientRpc]
        void SyncCollision_ClientRpc(string playerName, int collisionCount)
        {
            // Server already has the correct value - only clients need the update.
            if (IsServer) return;
            if (!gameData.TryGetRoundStats(playerName, out IRoundStats stats)) return;
            stats.JoustCollisions = collisionCount;
        }
    }
}
