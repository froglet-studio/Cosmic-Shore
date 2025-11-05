using CosmicShore.Game;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

namespace CosmicShore
{
    public class Silhouette : MonoBehaviour
    {
        [Header("Legacy Mapping")]
        [SerializeField] private float worldToUIScale = 2f;
        [SerializeField] private float imageScale     = 0.02f;

        [SerializeField] private List<GameObject> silhouetteParts = new();

        #region Sources / Options
        [FormerlySerializedAs("prismSpawner")]
        [FormerlySerializedAs("trailSpawner")]
        [SerializeField] private VesselPrismController vesselPrismController;

        [SerializeField] private DriftTrailAction driftTrailAction;

        [SerializeField] private Vector3 sihouetteScale = Vector3.one;

        [SerializeField] private GameObject topJaw;
        [SerializeField] private GameObject bottomJaw;
        [SerializeField] private int JawResourceIndex = -1;

        [SerializeField] private bool swingBlocks = false;
        #endregion

        #region Orientation / Tuning / Smoothing
        private enum FlowDirection { HorizontalRTL, VerticalTopDown }
        [Header("Flow & Tuning")]
        [Tooltip("Horizontal RTL = old style; VerticalTopDown = spawn from top to bottom (Y).")]
        [SerializeField] private FlowDirection flow = FlowDirection.VerticalTopDown;

        [Tooltip("Extra rotation applied to each column container (deg). Add 90 if your dash sprite is horizontal.")]
        [SerializeField] private float columnRotationOffsetDeg = 0f;

        [Tooltip("Multiply computed block scale per-axis (X,Y,Z). X=thickness, Y=height.")]
        [SerializeField] private Vector3 perBlockScaleMul = new Vector3(10f, 10f, 1f);

        [Tooltip("Additive local offset applied to each block (x,y,z) after we position it.")]
        [SerializeField] private Vector3 perBlockLocalOffset = Vector3.zero;

        [Header("Smoothing")]
        [SerializeField] private bool  smooth = true;
        [SerializeField] private float smoothingSeconds = 0.08f;
        
        [Header("Gap Between Top/Bottom Blocks")]
        [Tooltip("Override the per-column top/bottom separation (pixels). Set < 0 to use VesselPrismController.Gap.")]
        [SerializeField] private float rowGapPxOverride = -1f;

        [Tooltip("Multiply the resolved row gap for quick tuning.")]
        [SerializeField] private float gapMultiplier = 1f;

        [Tooltip("If true, base gap is added during motion too (legacy used gap only at seed).")]
        [SerializeField] private bool addBaseGapDuringMotion = false;

        #endregion

        public GameObject BlockPrefab { get; private set; }
        private GameObject[,] blockPool; // [index, row(0/1)]
        private int poolSize;

        private IVessel vessel;
        private Transform _silhouetteContainer;
        private Transform _trailDisplayContainer;

        private float dotProduct = .9999f;

        private float Alpha => smooth ? (1f - Mathf.Exp(-Time.unscaledDeltaTime / Mathf.Max(0.0001f, smoothingSeconds))) : 1f;
        private float RowGap
        {
            get
            {
                float baseGap = (vesselPrismController ? vesselPrismController.Gap : 10f);
                if (rowGapPxOverride >= 0f) baseGap = rowGapPxOverride;
                return Mathf.Max(0f, baseGap * Mathf.Max(0f, gapMultiplier));
            }
        }

        private void OnDisable()
        {
            if (driftTrailAction) driftTrailAction.OnChangeDriftAltitude -= calculateDriftAngle;
            if (topJaw && vessel?.VesselStatus?.ResourceSystem?.Resources != null &&
                JawResourceIndex >= 0 && JawResourceIndex < vessel.VesselStatus.ResourceSystem.Resources.Count)
            {
                vessel.VesselStatus.ResourceSystem.Resources[JawResourceIndex].OnResourceChange -= calculateBlastAngle;
            }
            if (vesselPrismController) vesselPrismController.OnBlockCreated -= HandleBlockCreation;
        }

        // Legacy init (kept)
        public void Initialize(IVessel vessel)
        {
            this.vessel = vessel;

            if (topJaw && vessel?.VesselStatus?.ResourceSystem?.Resources != null &&
                JawResourceIndex >= 0 && JawResourceIndex < vessel.VesselStatus.ResourceSystem.Resources.Count)
                vessel.VesselStatus.ResourceSystem.Resources[JawResourceIndex].OnResourceChange += calculateBlastAngle;

            if (driftTrailAction)      driftTrailAction.OnChangeDriftAltitude += calculateDriftAngle;
            if (vesselPrismController) vesselPrismController.OnBlockCreated   += HandleBlockCreation;
        }

        // Status + HUD view overload
        public void Initialize(IVesselStatus status, VesselHUDView hudView)
        {
            Initialize(status?.Vessel);
            if (hudView)
            {
                _silhouetteContainer   = hudView.SilhouetteContainer;
                _trailDisplayContainer = hudView.TrailDisplayContainer;
                if (!topJaw && hudView.TopJaw)       topJaw = hudView.TopJaw.gameObject;
                if (!bottomJaw && hudView.BottomJaw) bottomJaw = hudView.BottomJaw.gameObject;
                if (JawResourceIndex < 0)            JawResourceIndex = hudView.JawResourceIndex;
            }
        }

        // For older controllers
        public void SetHudReferences(Transform silhouetteContainer, Transform trailDisplayContainer)
        {
            _silhouetteContainer = silhouetteContainer;
            _trailDisplayContainer = trailDisplayContainer;
        }

        public void SetBlockPrefab(GameObject blockPrefab)
        {
            BlockPrefab = blockPrefab;
            if (blockPool != null) { DestroyPoolImmediate(); poolSize = 0; }
        }

        [ContextMenu("Reset Trail UI")]
        private void ResetTrailUI()
        {
            DestroyPoolImmediate();
            poolSize = 0;
            if (_trailDisplayContainer)
                foreach (Transform t in _trailDisplayContainer)
                    t.localRotation = Quaternion.Euler(0, 0, columnRotationOffsetDeg);
            Debug.Log("[Silhouette] Trail UI reset; will rebuild on next OnBlockCreated.");
        }

        private void calculateDriftAngle(float dot)
        {
            if (vessel?.VesselStatus == null) return;

            foreach (var part in silhouetteParts)
                if (part) part.SetActive(!vessel.VesselStatus.AutoPilotEnabled && vessel.VesselStatus.Player.IsActive);

            if (_silhouetteContainer != null)
                _silhouetteContainer.transform.localRotation =
                    Quaternion.Euler(0, 0, Mathf.Asin(dot - .0001f) * Mathf.Rad2Deg);

            dotProduct = dot;
        }

        private void calculateBlastAngle(float currentAmmo)
        {
            foreach (var part in silhouetteParts) { if (part) part.SetActive(true); }

            if (topJaw)
            {
                topJaw.transform.localRotation = Quaternion.Euler(0, 0, 21f * currentAmmo);
                var img = topJaw.GetComponent<Image>();
                if (img) img.color = currentAmmo > .98f ? Color.green : Color.white;
            }

            if (bottomJaw)
            {
                bottomJaw.transform.localRotation = Quaternion.Euler(0, 0, -21f * currentAmmo);
                var img = bottomJaw.GetComponent<Image>();
                if (img) img.color = currentAmmo > .98f ? Color.green : Color.white;
            }
        }

        private void HandleBlockCreation(float xShift, float wavelength, float scaleX, float scaleY, float scaleZ)
        {
            if (vessel?.VesselStatus == null || vessel.VesselStatus.AutoPilotEnabled) return;

            if (poolSize < 1 && _trailDisplayContainer != null)
            {
                var rect = (RectTransform)_trailDisplayContainer;
                float minWL = Mathf.Max(0.0001f, vesselPrismController ? vesselPrismController.MinWaveLength : wavelength);

                if (flow == FlowDirection.HorizontalRTL)
                {
                    poolSize = Mathf.CeilToInt(rect.rect.width / (minWL * worldToUIScale * (swingBlocks ? 1f : Mathf.Max(0.0001f, scaleY))));
                }
                else // VerticalTopDown
                {
                    poolSize = Mathf.CeilToInt(rect.rect.height / (minWL * worldToUIScale * (swingBlocks ? 1f : Mathf.Max(0.0001f, scaleY))));
                }
                InitializeBlockPool();
            }

            if (swingBlocks)
            {
                ApplyUpdate(
                    xShift     * (scaleY * 0.5f) * worldToUIScale,
                    wavelength * worldToUIScale,
                    scaleX     * scaleY * imageScale,
                    scaleZ     * imageScale
                );
            }
            else
            {
                ApplyUpdate(
                    xShift     * worldToUIScale * scaleY,
                    wavelength * worldToUIScale * scaleY,
                    scaleX     * scaleY * imageScale,
                    scaleZ     * scaleY * imageScale
                );
            }
        }

        private void InitializeBlockPool()
        {
            if (_trailDisplayContainer == null || BlockPrefab == null) return;

            blockPool = new GameObject[poolSize, 2];

            var rect = (RectTransform)_trailDisplayContainer;
            float width  = rect.rect.width;
            float height = rect.rect.height;

            float minWL = Mathf.Max(0.0001f, vesselPrismController ? vesselPrismController.MinWaveLength : 1f);
            float gap = RowGap;

            for (int i = 0; i < poolSize; i++)
            {
                var col = new GameObject($"TrailSeg_{i}", typeof(RectTransform));
                var segRT = (RectTransform)col.transform;
                segRT.SetParent(_trailDisplayContainer, false);
                segRT.anchorMin = segRT.anchorMax = new Vector2(0.5f, 0.5f);
                segRT.localRotation = Quaternion.Euler(0, 0, columnRotationOffsetDeg);

                if (flow == FlowDirection.HorizontalRTL)
                    segRT.localPosition = new Vector3(-i * minWL * worldToUIScale + (width * 0.5f), 0f, 0f);
                else
                    segRT.localPosition = new Vector3(0f, (height * 0.5f) - i * minWL * worldToUIScale, 0f);

                for (int j = 0; j < 2; j++)
                {
                    var block = Instantiate(BlockPrefab, segRT, false);

                    var img = block.GetComponent<Image>();
                    if (img)
                    {
                        img.type = Image.Type.Simple;
                        img.preserveAspect = false;
                        img.raycastTarget = false;
                    }

                    var brt = (RectTransform)block.transform;
                    brt.localScale = Vector3.zero;

                    if (flow == FlowDirection.HorizontalRTL)
                        brt.localPosition = new Vector3(0f, j * 2f * gap - gap, 0f);
                    else
                        brt.localPosition = new Vector3(j * 2f * gap - gap, 0f, 0f);

                    block.SetActive(true);
                    blockPool[i, j] = block;
                }
            }
        }

        private void ApplyUpdate(float xShift, float wavelength, float scaleX, float scaleZ)
        {
            if (_trailDisplayContainer == null || vessel?.VesselStatus == null) return;
            if (vessel.VesselStatus.AutoPilotEnabled) return;

            var rect = (RectTransform)_trailDisplayContainer;
            float width  = rect.rect.width;
            float height = rect.rect.height;

            // Head (index 0)
            for (int j = 0; j < 2; j++)
            {
                var head   = blockPool[0, j];
                var parent = (RectTransform)head.transform.parent;

                Vector3 parentTargetPos;
                if (flow == FlowDirection.HorizontalRTL)
                    parentTargetPos = new Vector3(width * 0.5f, 0f, 0f);
                else
                    parentTargetPos = new Vector3(0f, height * 0.5f, 0f);

                parent.localPosition = Vector3.Lerp(parent.localPosition, parentTargetPos, Alpha);

                float baseTilt = -Mathf.Acos(Mathf.Clamp(dotProduct - 0.0001f, -0.9999f, 0.9999f)) * Mathf.Rad2Deg;
                var targetRot = Quaternion.Euler(0, 0, baseTilt + columnRotationOffsetDeg);
                parent.localRotation = Quaternion.Slerp(parent.localRotation, targetRot, Alpha);

                var brt = (RectTransform)head.transform;

                float eff = addBaseGapDuringMotion ? (xShift + RowGap) : xShift;

                Vector3 blockTargetPos;
                blockTargetPos = flow == FlowDirection.HorizontalRTL ? new Vector3(0f, j * 2f * eff - eff, 0f) : new Vector3(j * 2f * eff - eff, 0f, 0f);


                blockTargetPos += perBlockLocalOffset;
                brt.localPosition = Vector3.Lerp(brt.localPosition, blockTargetPos, Alpha);

                // legacy thickness/height amplified, with per-axis multipliers
                float h = Mathf.Abs(j * 2f * scaleX - scaleX);
                float t = Mathf.Abs(scaleZ);

                Vector3 targetScale;
                // X = thickness, Y = height (legacy)
                targetScale = new Vector3(t * perBlockScaleMul.x, h * perBlockScaleMul.y, perBlockScaleMul.z);

                brt.localScale = Vector3.Lerp(brt.localScale, targetScale, Alpha);
            }

            // Conveyor
            for (int i = poolSize - 1; i > 0; i--)
            {
                bool under;
                Vector3 parentTarget;
                if (flow == FlowDirection.HorizontalRTL)
                {
                    float colX = -i * wavelength + (width * 0.5f);
                    under = i < Mathf.CeilToInt(width / Mathf.Max(1f, wavelength));
                    parentTarget = new Vector3(colX, 0f, 0f);
                }
                else
                {
                    float rowY = (height * 0.5f) - i * wavelength;
                    under = i < Mathf.CeilToInt(height / Mathf.Max(1f, wavelength));
                    parentTarget = new Vector3(0f, rowY, 0f);
                }

                for (int j = 0; j < 2; j++)
                {
                    var cur  = blockPool[i, j];
                    var prev = blockPool[i - 1, j];

                    var curParent = (RectTransform)cur.transform.parent;
                    curParent.localPosition = Vector3.Lerp(curParent.localPosition, parentTarget, Alpha);
                    curParent.gameObject.SetActive(under);

                    var crt = (RectTransform)cur.transform;
                    var prt = (RectTransform)prev.transform;

                    // smooth copy from prev (keeps the nice flowing feel)
                    crt.localScale    = Vector3.Lerp(crt.localScale,    prt.localScale,    Alpha);
                    crt.localPosition = Vector3.Lerp(crt.localPosition, prt.localPosition, Alpha);

                    // keep tilt continuity
                    curParent.localRotation = Quaternion.Slerp(curParent.localRotation, ((RectTransform)prev.transform.parent).localRotation, Alpha);
                }
            }
        }

        public void Clear()
        {
            if (_trailDisplayContainer != null)
                foreach (Transform t in _trailDisplayContainer) t.gameObject.SetActive(false);

            if (_silhouetteContainer != null)
                foreach (Transform t in _silhouetteContainer) t.gameObject.SetActive(false);
        }

        private void DestroyPoolImmediate()
        {
            if (blockPool == null) return;

            for (int i = 0; i < blockPool.GetLength(0); i++)
            for (int j = 0; j < 2; j++)
            {
                if (blockPool[i, j])
                {
                    var p = blockPool[i, j].transform.parent;
                    if (Application.isEditor)
                    {
                        Object.DestroyImmediate(blockPool[i, j]);
                        if (p) Object.DestroyImmediate(p.gameObject);
                    }
                    else
                    {
                        Object.Destroy(blockPool[i, j]);
                        if (p) Object.Destroy(p.gameObject);
                    }
                }
            }
            blockPool = null;
        }
    }
}
