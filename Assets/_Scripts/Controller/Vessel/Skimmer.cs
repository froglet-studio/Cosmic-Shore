using System.Collections;
using System.Collections.Generic;
using CosmicShore.Gameplay;
using Obvious.Soap;
using UnityEngine;
using CosmicShore.Utility;
using Cysharp.Threading.Tasks;
using CosmicShore.Data;
using System.Linq;
namespace CosmicShore.Gameplay
{
    public class Skimmer : ElementalShipComponent
    {
        [SerializeField] ScriptableEventString onSkimmerShipImpact;

        [Header("Skim / Crystal")]
        [SerializeField] float vaccumAmount = 80f;
        public float VaccumAmount =>  vaccumAmount;
        [SerializeField] bool vacuumCrystal = true;
        public bool AllowVaccumCrystal => vacuumCrystal;
        [SerializeField] bool affectSelf = true;

        [Header("FX / Viz")]
        [SerializeField] bool visible;
        [SerializeField] ElementalFloat Scale = new(1);
        [Tooltip("Sword-style capsule skimmers (Rhino): preserve the authored local X/Z silhouette and let Scale drive ONLY the local Y length. Leave off for spherical skimmers (uniform XYZ).")]
        [SerializeField] bool elongateYOnly;
        [SerializeField] NudgeShardPoolManager nudgeShardPoolManager;
        [SerializeField] int markerDistance = 70;

        [Header("Optional")]
        [SerializeField] GameObject AOEPrefab;
        [SerializeField] float AOEPeriod;
        [SerializeField] private Material lineMaterial;
        
        public IVesselStatus VesselStatus { get; private set; }
        public Domains Domain => VesselStatus.Domain;
        public bool AffectSelf => affectSelf;
        public bool IsInitialized => VesselStatus is { Player: { IsActive: true } };

        float _appliedScale;
        float _sweetSpot;
        float _boosterTimer;
        bool isResizingScale;
        Vector3 _authoredShape;

        /// <summary>Local-space dimensions authored on the prefab, captured before any runtime scale writer runs.</summary>
        public Vector3 AuthoredShape => _authoredShape;

        /// <summary>True when scale changes elongate only local Y, preserving the authored X/Z (sword capsules).</summary>
        public bool ElongateYOnly => elongateYOnly;

        /// <summary>Live elemental scale in world units — the resting size an external scale driver should grow from.</summary>
        public float LiveElementalScale => Scale.EvaluateLive(VesselStatus);

        /// <summary>Set by an external per-frame scale driver (ShieldSkimmerScaleDriver) while it owns this transform's scale.</summary>
        public bool HasExternalScaleDriver { get; set; }

        /// <summary>
        /// Rhino "energy sword" per-vessel state, registered by <see cref="ShieldSkimmerScaleDriver"/>.
        /// Null on every other vessel's skimmer. Shared impact-effect SOs read it via
        /// <c>impactor.Skimmer.SwordState</c> to bank energy, pulse the blade's impact flash, and
        /// trigger the crystal burst — none of which they could hold themselves (effect SOs are
        /// singletons). See <c>RHINO_ENERGY_SWORD.md</c>.
        /// </summary>
        public IRhinoSwordState SwordState { get; set; }

        SkimmerSwingKinematics _swingKinematics;
        bool _swingLookedUp;

        /// <summary>
        /// The swing velocity model for skimmers that move relative to their vessel (the
        /// Rhino's sword), or null for a skimmer rigidly mounted to the hull. Impact effects
        /// read it to feed a prism the velocity of the PART of the skimmer that touched it
        /// rather than the vessel's. Looked up once - impacts are a dense-trail hot path.
        /// </summary>
        public SkimmerSwingKinematics SwingKinematics
        {
            get
            {
                if (!_swingLookedUp)
                {
                    _swingLookedUp = true;
                    TryGetComponent(out _swingKinematics);
                }
                return _swingKinematics;
            }
        }

        void Awake()
        {
            _authoredShape = transform.localScale;
        }

        void Update()
        {
            if (!IsInitialized) return;
            ApplyScaleIfChanged();   
        }
        
        private void OnDestroy()
        {
            // Ensures any scaling tasks for this specific transform are cancelled
            transform.CancelResize();
        }

        public void Initialize(IVesselStatus vesselStatus)
        {
            VesselStatus = vesselStatus;
            
            _sweetSpot = transform.localScale.x / 4f;

            ApplyScaleIfChanged();
            BindElementalFloats(VesselStatus.Vessel);

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
            if (!nudgeShardPoolManager)
                return;
            MakeBoosters(prism);
        }

        public async UniTaskVoid ResizeForSeconds(float multiplier, float durationSeconds)
        {
            if (isResizingScale) return;

            isResizingScale = true;
            await transform.ResizeForSeconds(multiplier, durationSeconds);
            isResizingScale = false;
        }
        
        // SPACE -> skimmer reach: Scale is authored as an ElementalFloat on the vessel prefab
        // (the Squirrel maps it to Space, 15 -> 30). EvaluateLive is the unified read path for
        // per-vessel component floats - same math as the bound event path, but it also works
        // when the reflection binding was never wired (see ElementalFloat.EvaluateLive).
        // Sword capsules (elongateYOnly, Rhino) keep the authored X/Z and grow only along Y;
        // when a ShieldSkimmerScaleDriver owns this transform it reads LiveElementalScale
        // instead and this method stands down (single writer).
        void ApplyScaleIfChanged()
        {
            if (HasExternalScaleDriver) return;

            float liveScale = Scale.EvaluateLive(VesselStatus);
            if (_appliedScale == liveScale) return;
            _appliedScale = liveScale;
            transform.localScale = elongateYOnly
                ? new Vector3(_authoredShape.x, _appliedScale, _authoredShape.z)
                : Vector3.one * _appliedScale;
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

            return block.Trail.LookAhead(idx, 0, backward, distance);
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
 
                
                if (!SafeLookRotation.TryGet(blockTransform.forward, localPos, out var rotation, blockTransform))
                    continue;

                var marker = nudgeShardPoolManager?.Get(worldPos, rotation, nudgeShardPoolManager.transform);

                if (!marker) break;
                
                if (!shardPositions.Add(marker.transform.position))
                {
                    nudgeShardPoolManager?.Release(marker);
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
                nudgeShardPoolManager?.Release(m);
            }
        }
    }
}
