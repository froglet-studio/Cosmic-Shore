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
        [SerializeField] ScriptableEventString onSkimmerShipImpact;

        [Header("Skim / Crystal")]
        [SerializeField] float vaccumAmount = 80f;
        [SerializeField] bool vacuumCrystal = true;
        [SerializeField] bool affectSelf = true;

        [Header("FX / Viz")]
        [SerializeField] float particleDurationAtSpeedOne = 300f;
        [SerializeField] bool visible;
        [SerializeField] ElementalFloat Scale = new ElementalFloat(1);
        [SerializeField] NudgeShardPoolManager nudgeShardPoolManager;
        [SerializeField] int markerDistance = 70;

        [Header("Optional")]
        [SerializeField] GameObject AOEPrefab;
        [SerializeField] float AOEPeriod;
        [SerializeField] private Material lineMaterial;

        [SerializeField] int resourceIndex = 0;
        
        public IVesselStatus VesselStatus { get; private set; }
        public bool AffectSelf => affectSelf;

        CameraManager cameraManager;
        float _appliedScale;
        float _sweetSpot;
        float _sqrRadius;
        float _initialGap;
        float _boosterTimer;
        
        public bool IsInitialized { get; private set; }

        void Update()
        {
            if (!IsInitialized) return;
            ApplyScaleIfChanged();   
        }

        public void Initialize(IVesselStatus vesselStatus)
        {
            IsInitialized = true;
            VesselStatus = vesselStatus;
            
            cameraManager = CameraManager.Instance;

            _sweetSpot = transform.localScale.x / 4f;
            _sqrRadius = transform.localScale.x * transform.localScale.x / 4f;

            ApplyScaleIfChanged();
            BindElementalFloats(VesselStatus.Vessel);

            // if (visible)
            //     GetComponent<MeshRenderer>().material = new Material(VesselStatus.SkimmerMaterial);

            _initialGap = VesselStatus.PrismSpawner.Gap;
            if (nudgeShardPoolManager) nudgeShardPoolManager.transform.parent = VesselStatus?.Player?.Transform;
        }

        // ---------------- Secondary helpers the Impactor can call ----------------

        public void ExecuteImpactOnShip(IVessel vessel)
        {
            onSkimmerShipImpact.Raise(VesselStatus.PlayerName);
        }

        public void ExecuteImpactOnPrism(Prism prism)
        {
            if (VesselStatus is null || (!affectSelf && prism.Domain == VesselStatus.Domain)) return;
            MakeBoosters(prism);
        }

        public void TryVacuumCrystal(Crystal crystal)
        {
            if (!vacuumCrystal || crystal == null) return;

            crystal.transform.position = Vector3.MoveTowards(
                crystal.transform.position,
                transform.position,
                vaccumAmount * Time.deltaTime / crystal.transform.lossyScale.x);
        }
        
        void ApplyScaleIfChanged()
        {
            if (_appliedScale == Scale.Value) return;
            _appliedScale = Scale.Value;
            transform.localScale = Vector3.one * _appliedScale;
        }

        void MakeBoosters(Prism prism)
        {
            const int markerCount = 5;
            const float cooldown = 4f;

            if (Time.time - _boosterTimer < cooldown) return;
            _boosterTimer = Time.time;

            var nextBlocks = FindNextBlocks(prism, markerCount * markerDistance);
            if (nextBlocks.Count == 0) return;

            // last element
            VisualizeTubeAroundBlock(nextBlocks[^1]);

            if (markerCount == 1) return;

            float stepSize = (float)(nextBlocks.Count - 1) / (markerCount - 1);
            for (int i = 1; i < markerCount - 1; i++)
            {
                int index = nextBlocks.Count - 1 - (int)Mathf.Round(i * stepSize);
                if (index >= 0 && index < nextBlocks.Count)
                    VisualizeTubeAroundBlock(nextBlocks[index]);
            }
        }

        List<Prism> FindNextBlocks(Prism block, float distance = 100f)
        {
            if (block == null || block.Trail == null)
                return new List<Prism> { block };

            int idx = block.prismProperties.Index;
            var forward = TrailFollowerDirection.Forward;
            var backward = TrailFollowerDirection.Backward;

            // simple: prefer forward unless at start and direction negative; adjust if you track direction elsewhere
            if (idx > 0)
                return block.Trail.LookAhead(idx, 0, forward, distance);

            return block.Trail.LookAhead(idx, 0, forward, distance);
        }

        public static float ComputeGaussian(float x, float b, float c)
            => Mathf.Exp(-Mathf.Pow(x - b, 2) / (2 * c * c));

        void VisualizeTubeAroundBlock(Prism prism)
        {
            if (prism)
                StartCoroutine(DrawCircle(prism.transform, _sweetSpot));
        }

        readonly HashSet<Vector3> shardPositions = new();

        IEnumerator DrawCircle(Transform blockTransform, float radius)
        {
            int segments = Mathf.Min((int)(Mathf.PI * 2f * radius / blockTransform.localScale.x), 360);
            float anglePerSegment = blockTransform.localScale.x / radius;

            var markers = new List<NudgeShard>();

            for (int i = -segments / 2; i < segments / 2; i++)
            {
                float angle = i * anglePerSegment;
                Vector3 localPos = (Mathf.Cos(angle + (Mathf.PI / 2)) * blockTransform.right
                                   + Mathf.Sin(angle + (Mathf.PI / 2)) * blockTransform.up) * radius;

                Vector3 worldPos = blockTransform.position + localPos;

                var marker = nudgeShardPoolManager.Get(
                    worldPos,
                    Quaternion.LookRotation(blockTransform.forward, localPos), nudgeShardPoolManager.transform);

                if (!shardPositions.Add(marker.transform.position))
                {
                    nudgeShardPoolManager.Release(marker);
                    continue;
                }

                marker.transform.localScale = blockTransform.localScale / 2f;
                marker.Prisms =
                    FindNextBlocks(blockTransform.GetComponent<Prism>(), markerDistance * VesselStatus.ResourceSystem.Resources[0].CurrentAmount);

                markers.Add(marker);
            }

            yield return new WaitForSeconds(8f);

            foreach (var m in markers)
            {
                if (!m) continue;
                shardPositions.Remove(m.transform.position);
                nudgeShardPoolManager.Release(m);
            }
        }
    }
}
