using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace CosmicShore.Game
{
    /// <summary>
    /// Multiplayer version:
    /// - Server owns authoritative positions (NetworkList<Vector3>)
    /// - index => crystalId = index + 1
    /// - OnTurnStarted (server): computes batch spawn positions, writes them to the list
    /// - Clients: OnPositionsChanged spawns/moves the corresponding crystal
    /// - RespawnCrystal (client): calls server rpc with crystalId; server calculates new pos and updates list
    /// </summary>
    public class NetworkCrystalManager : CrystalManager
    {
        private NetworkList<Vector3> n_Positions;

        protected override void Awake()
        {
            base.Awake();
            n_Positions = new NetworkList<Vector3>();
        }

        public override void OnNetworkSpawn()
        {
            gameData.OnMiniGameTurnStarted.OnRaised += OnTurnStarted;

            // IMPORTANT: Only the server should resize the list.
            if (IsServer)
                EnsureListSizedToSelectedPlayerCount();

            n_Positions.OnListChanged += OnPositionsChanged;
        }

        public override void OnNetworkDespawn()
        {
            gameData.OnMiniGameTurnStarted.OnRaised -= OnTurnStarted;

            if (n_Positions != null)
                n_Positions.OnListChanged -= OnPositionsChanged;
        }

        // ---------------- Server Turn Start ----------------

        private void EnsureListSizedToSelectedPlayerCount()
        {
            int count = Mathf.Max(1, gameData.SelectedPlayerCount.Value);

            // Grow
            while (n_Positions.Count < count)
                n_Positions.Add(Vector3.zero);

            // Shrink
            while (n_Positions.Count > count)
                n_Positions.RemoveAt(n_Positions.Count - 1);
        }

        private void OnTurnStarted()
        {
            if (!IsServer)
                return;

            EnsureListSizedToSelectedPlayerCount();

            // Spawn batch positions using base logic:
            // one anchor for the batch (SpawnBatchIfMissing does that),
            // but here we are not instantiating directly; we just write positions.
            //
            // Approach:
            // - Use the same anchor selection as batch spawning (one anchor per batch).
            // - Assign each index position around that anchor.
            //
            // NOTE: Since SpawnBatchIfMissing() instantiates, we DON'T call it here.
            // We replicate "one anchor per batch" behavior by using GetSpawnPointAroundAnchor(...) once per entry,
            // and then the base's batchAnchorIndex will progress when respawns happen too.
            //
            // The base class holds batchAnchorIndex internally; we can follow the same idea by doing:
            //  - pick anchor for current batchAnchorIndex via base logic by calling CalculateNewSpawnPos? No.
            //  - So: let base handle initial batch anchoring via SpawnBatchIfMissing on server? That would spawn server-only objects.
            //
            // Simpler: reuse the base spawn list by calling SpawnBatchIfMissing() ONLY on server scene where server objects exist.
            // But you asked to replicate via networklist only, so we just use local base helper logic:
            //
            // We'll use the same anchor for this batch by spawning around the CURRENT batch anchor.
            //
            // To keep it clean, we’ll just call this helper from base indirectly by computing respawn-like anchors isn’t desired.
            // So we emulate the batch behavior: one anchor per turn start. 
            //
            // Implementation: We'll ask the base for one anchor by temporarily "spawning logic" through protected helper:
            // There is no public "GetAnchorForBatchIndex". So if you want to access that, expose a protected method.
            //
            // For now, simplest: rely on CalculateNewSpawnPos per crystal? That moves next anchor per crystal, not desired.
            //
            // ✅ Best practice: expose a protected method in base:
            //    protected Vector3 GetBatchAnchor() and protected void AdvanceBatchAnchor()
            //
            // I'll implement the best practice below without changing your GameData contract.

            Vector3 batchAnchor = GetBatchAnchor_ForNetworkTurnStart();
            for (int i = 0; i < n_Positions.Count; i++)
                n_Positions[i] = GetSpawnPointAroundAnchor(batchAnchor);

            AdvanceBatchAnchor_ForNetworkTurnStart();
        }

        // ----------------------------------------------------------------
        // These two helpers are SMALL “bridge” helpers.
        // They simply call into the base protected behavior by reusing a per-turn anchor.
        // Because batchAnchorIndex is private in base, we keep a server-side copy here.
        // ----------------------------------------------------------------
        private int serverBatchAnchorIndex;

        private Vector3 GetBatchAnchor_ForNetworkTurnStart()
        {
            // Use the same intensity anchors, pick by serverBatchAnchorIndex
            // We can't access base's anchor list directly, so simplest is to use
            // CalculateNewSpawnPos-like internals. But we don't want "next anchor per crystal".
            //
            // Therefore, we reproduce a minimal anchor selection locally:
            // We'll call CalculateNewSpawnPos for a dummy id? No, that mutates dictionaries.
            //
            // Instead: we maintain our own serverBatchAnchorIndex and read anchor list by calling
            // a tiny protected method on base. Since base doesn't expose it, simplest solution:
            // Turn TryGetCrystalPositionListByIntensity into protected in base.
            //
            // If you don't want that change, then duplicate anchor list reading here:
            // But that means two places. Better: make it protected in base.

            // --- Minimal duplication (safe and practical) ---
            if (!TryGetAnchors(out var anchors) || anchors == null || anchors.Length == 0)
                return Vector3.forward * 30f;

            serverBatchAnchorIndex %= anchors.Length;
            return anchors[serverBatchAnchorIndex];
        }

        private void AdvanceBatchAnchor_ForNetworkTurnStart()
        {
            if (!TryGetAnchors(out var anchors) || anchors == null || anchors.Length == 0)
                return;

            serverBatchAnchorIndex = (serverBatchAnchorIndex + 1) % anchors.Length;
        }

        // Duplicated anchor getter for server-side only (kept private & small)
        private bool TryGetAnchors(out Vector3[] anchors)
        {
            // We can’t access base's private TryGetCrystalPositionListByIntensity.
            // If you make that protected in base, you can remove this duplication.
            //
            // For now, this function should be updated if base changes.
            anchors = null;

            // We can't read listOfCrystalPositions here because it is in base private fields.
            // So: easiest fix is to make TryGetCrystalPositionListByIntensity protected in base.
            //
            // ✅ DO THIS:
            // Change in CrystalManager:
            //     private bool TryGetCrystalPositionListByIntensity(...)  ->  protected bool TryGetCrystalPositionListByIntensity(...)
            //
            // Then replace this whole function with:
            //     return TryGetCrystalPositionListByIntensity(out anchors);

            return TryGetCrystalPositionListByIntensity(out anchors);
        }

        // ---------------- Replication ----------------

        private void OnPositionsChanged(NetworkListEvent<Vector3> e)
        {
            // Only handle add/insert/value changes.
            if (e.Type != NetworkListEvent<Vector3>.EventType.Add &&
                e.Type != NetworkListEvent<Vector3>.EventType.Insert &&
                e.Type != NetworkListEvent<Vector3>.EventType.Value)
                return;

            int idx = e.Index;
            if (idx < 0 || idx >= n_Positions.Count)
                return;

            int crystalId = idx + 1;

            // Use event value (more correct than reading list again).
            Vector3 pos = e.Value;

            // If the server still has placeholder zeros, ignore.
            if (pos == Vector3.zero)
                return;

            if (!cellData.TryGetCrystalById(crystalId, out _))
            {
                var crystal = Spawn(crystalId, pos);
                cellData.AddCrystalToList(crystal);
            }
            else
                UpdateCrystalPos(crystalId, pos);
        }

        // ---------------- Public API ----------------

        public override void RespawnCrystal(int crystalId)
        {
            // Clients request respawn for a specific crystal.
            RespawnCrystal_ServerRpc(crystalId);
        }

        [ServerRpc(RequireOwnership = false)]
        private void RespawnCrystal_ServerRpc(int crystalId)
        {
            int idx = crystalId - 1;
            if (idx < 0 || idx >= n_Positions.Count)
                return;

            // Server calculates a new position for THAT crystal id (per-crystal anchor progression).
            Vector3 newPos = CalculateNewSpawnPos(crystalId);

            // Writing to NetworkList replicates to all clients.
            n_Positions[idx] = newPos;
        }

        public override void ExplodeCrystal(int crystalId, Crystal.ExplodeParams explodeParams) =>
            ExplodeCrystal_ServerRpc(crystalId, NetworkExplodeParams.FromExplodeParams(explodeParams));

        [ServerRpc(RequireOwnership = false)]
        private void ExplodeCrystal_ServerRpc(int crystalId, NetworkExplodeParams explodeParams)
        {
            ExplodeCrystal_ClientRpc(crystalId, explodeParams);
        }

        [ClientRpc]
        private void ExplodeCrystal_ClientRpc(int crystalId, NetworkExplodeParams explodeParams)
        {
            if (cellData.TryGetCrystalById(crystalId, out var crystal))
                crystal.Explode(explodeParams.ToExplodeParams());
        }

        private void OnDrawGizmosSelected()
        {
            if (n_Positions == null)
                return;

            Gizmos.color = Color.yellow;
            foreach (var pos in n_Positions)
                Gizmos.DrawWireSphere(pos, 5f);
        }
    }

    public struct NetworkExplodeParams : INetworkSerializable
    {
        private Vector3 Course;
        private float Speed;
        private FixedString64Bytes PlayerName;

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref Course);
            serializer.SerializeValue(ref Speed);
            serializer.SerializeValue(ref PlayerName);
        }

        private NetworkExplodeParams(Vector3 course, float speed, FixedString64Bytes playerName)
        {
            Course = course;
            Speed = speed;
            PlayerName = playerName;
        }

        public static NetworkExplodeParams FromExplodeParams(Crystal.ExplodeParams e) =>
            new NetworkExplodeParams(e.Course, e.Speed, e.PlayerName);

        public Crystal.ExplodeParams ToExplodeParams() =>
            new Crystal.ExplodeParams { Course = Course, Speed = Speed, PlayerName = PlayerName };
    }
}
