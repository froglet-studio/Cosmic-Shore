using System.Collections;
using System.Collections.Generic;
using CosmicShore.Core;
using Obvious.Soap;
using UnityEngine;
using CosmicShore.Game.IO;

namespace CosmicShore.Game
{
    public class Skimmer : ElementalShipComponent
    {
        [SerializeField] private ScriptableEventString onSkimmerShipImpact;

        [Header("Skim / Crystal")]
        [SerializeField] private float vaccumAmount = 80f;
        [SerializeField] private bool vacuumCrystal = true;
        [SerializeField] private bool affectSelf = true;

        [Header("FX / Viz")]
        [SerializeField] private float particleDurationAtSpeedOne = 300f;
        [SerializeField] private bool visible;
        [SerializeField] private ElementalFloat Scale = new ElementalFloat(1);
        [SerializeField] private PoolManager markerContainer;
        [SerializeField, Min(1)] private int markerDistance = 70;

        [Header("Optional")]
        [SerializeField] private GameObject AOEPrefab;
        [SerializeField] private float AOEPeriod;
        [SerializeField] private Material lineMaterial;

        [SerializeField, Min(0)] private int resourceIndex = 0;

        public IPlayer Player => ShipStatus.Player;
        public IShipStatus ShipStatus { get; private set; }
        public bool AffectSelf => affectSelf;

        private CameraManager cameraManager;
        private float _appliedScale;
        private float _sweetSpot;
        private float _sqrRadius;
        private float _initialGap;
        private float _boosterTimer;

        private void Start()
        {
            cameraManager = CameraManager.Instance;

            // These depend on current transform scale; keep them cheap and deterministic
            var s = transform.localScale.x;
            _sweetSpot = s * 0.25f;
            _sqrRadius = (s * s) * 0.25f;

            ApplyScaleIfChanged();

            // Don’t assume ShipStatus exists at Start; Initialize() sets it later.
            if (markerContainer && ShipStatus?.Player?.Transform != null)
                markerContainer.transform.parent = ShipStatus.Player.Transform;
        }

        private void Update() => ApplyScaleIfChanged();

        private void ApplyScaleIfChanged()
        {
            if (Mathf.Approximately(_appliedScale, Scale.Value)) return;
            _appliedScale = Scale.Value;
            transform.localScale = Vector3.one * _appliedScale;
        }

        public void Initialize(IShipStatus shipStatus)
        {
            ShipStatus = shipStatus;
            BindElementalFloats(ShipStatus.Ship);

            if (visible)
            {
                // NOTE: This instantiates a unique material. Keep if you truly need a unique instance.
                var mr = GetComponent<MeshRenderer>();
                if (mr && ShipStatus.SkimmerMaterial) mr.material = new Material(ShipStatus.SkimmerMaterial);
            }

            _initialGap = ShipStatus.TrailSpawner.Gap;

            if (markerContainer && ShipStatus.Player?.Transform != null)
                markerContainer.transform.parent = ShipStatus.Player.Transform;
        }

        // ---------------- Secondary helpers the Impactor can call ----------------

        public void ExecuteImpactOnShip(IShip ship)
        {
            if (ShipStatus != null) onSkimmerShipImpact.Raise(ShipStatus.PlayerName);
        }

        public void ExecuteImpactOnPrism(TrailBlock trailBlock)
        {
            if (ShipStatus is null || (!affectSelf && trailBlock.Team == ShipStatus.Team)) return;
            MakeBoosters(trailBlock);
        }

        public void TryVacuumCrystal(Crystal crystal)
        {
            if (!vacuumCrystal || crystal == null) return;

            var t = crystal.transform;
            var target = transform.position;
            var step = vaccumAmount * Time.deltaTime / Mathf.Max(1e-6f, crystal.transform.lossyScale.x);
            t.position = Vector3.MoveTowards(t.position, target, step);
        }

        private void MakeBoosters(TrailBlock trailBlock)
        {
            const int markerCount = 5;
            const float cooldown = 4f;

            if (Time.time - _boosterTimer < cooldown) return;
            _boosterTimer = Time.time;

            var nextBlocks = FindNextBlocks(trailBlock, markerCount * markerDistance);
            if (!markerContainer || nextBlocks.Count == 0) return;

            // last element
            VisualizeTubeAroundBlock(nextBlocks[^1]);

            if (markerCount == 1) return;

            float stepSize = (float)(nextBlocks.Count - 1) / (markerCount - 1);
            for (int i = 1; i < markerCount - 1; i++)
            {
                int index = nextBlocks.Count - 1 - Mathf.RoundToInt(i * stepSize);
                if ((uint)index < (uint)nextBlocks.Count)
                    VisualizeTubeAroundBlock(nextBlocks[index]);
            }
        }

        private List<TrailBlock> FindNextBlocks(TrailBlock block, float distance = 100f)
        {
            if (block == null || block.Trail == null)
                return new List<TrailBlock> { block };

            int idx = block.TrailBlockProperties.Index;
            // Prefer forward for now; adjust if you track direction elsewhere
            return block.Trail.LookAhead(idx, 0, TrailFollowerDirection.Forward, distance);
        }

        public static float ComputeGaussian(float x, float b, float c)
            => Mathf.Exp(-((x - b) * (x - b)) / (2f * c * c));

        private void VisualizeTubeAroundBlock(TrailBlock trailBlock)
        {
            if (trailBlock)
                StartCoroutine(DrawCircle(trailBlock.transform, _sweetSpot));
        }

        private readonly HashSet<Vector3> shardPositions = new();

        private IEnumerator DrawCircle(Transform blockTransform, float radius)
        {
            if (!markerContainer) yield break;

            float blockScale = Mathf.Max(1e-6f, blockTransform.localScale.x);
            int segments = Mathf.Min((int)(Mathf.PI * 2f * radius / blockScale), 360);
            if (segments <= 0) yield break;

            float anglePerSegment = blockScale / radius;
            var markers = new List<GameObject>(segments);

            // Cache once
            var block = blockTransform.GetComponent<TrailBlock>();

            for (int i = -segments / 2; i < segments / 2; i++)
            {
                float angle = i * anglePerSegment;
                Vector3 localPos =
                    (Mathf.Cos(angle + (Mathf.PI * 0.5f)) * blockTransform.right +
                     Mathf.Sin(angle + (Mathf.PI * 0.5f)) * blockTransform.up) * radius;

                Vector3 worldPos = blockTransform.position + localPos;

                var marker = markerContainer.SpawnFromPool(
                    "Shard",
                    worldPos,
                    Quaternion.LookRotation(blockTransform.forward, localPos));

                if (marker == null) continue; // pool may be missing/empty in edge cases

                // Dedup positions (note: exact Vector3 equality; consider approx if needed)
                if (shardPositions.Contains(marker.transform.position))
                {
                    markerContainer.ReturnToPool(marker);
                    continue;
                }

                shardPositions.Add(marker.transform.position);
                marker.transform.localScale = blockTransform.localScale * 0.5f;

                // Cache component; guard against missing
                var nudge = marker.GetComponentInChildren<NudgeShard>();
                if (nudge != null && ShipStatus?.ResourceSystem?.Resources != null)
                {
                    int idx = Mathf.Clamp(resourceIndex, 0, ShipStatus.ResourceSystem.Resources.Count - 1);
                    var amount = ShipStatus.ResourceSystem.Resources[idx].CurrentAmount;
                    nudge.Prisms = FindNextBlocks(block, markerDistance * amount);
                }

                markers.Add(marker);
            }

            yield return new WaitForSeconds(8f);

            foreach (var m in markers)
            {
                if (m == null) continue;
                shardPositions.Remove(m.transform.position);
                markerContainer.ReturnToPool(m); // NEW API: no tag argument
            }
        }
    }
}
