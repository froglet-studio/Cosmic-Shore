using CosmicShore.Game;
using CosmicShore.Utilities;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

namespace CosmicShore
{
    public class Silhouette : MonoBehaviour
    {
        float worldToUIScale = 2f;
        float imageScale = .02f;

        [SerializeField] List<GameObject> silhouetteParts = new();

        #region Optional configuration 
        [FormerlySerializedAs("prismSpawner")]
        [FormerlySerializedAs("trailSpawner")]
        [SerializeField] VesselPrismController vesselPrismController;

        [SerializeField] DriftTrailAction driftTrailAction;

        [SerializeField] Vector3 sihouetteScale = Vector3.one;

        // jaws //
        [SerializeField] GameObject topJaw;
        [SerializeField] GameObject bottomJaw;
        [SerializeField] int JawResourceIndex;

        [SerializeField] bool swingBlocks;
        #endregion

        public GameObject BlockPrefab { get; private set; }
        private GameObject[,] blockPool;
        private int poolSize;

        IVessel vessel;

        Transform _silhouetteContainer;
        Transform _trailDisplayContainer;

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

        // === Legacy init kept for backwards-compat (doesn't wire HUD) ===
        public void Initialize(IVessel vessel)
        {
            this.vessel = vessel;

            if (topJaw && vessel?.VesselStatus?.ResourceSystem?.Resources != null &&
                JawResourceIndex >= 0 && JawResourceIndex < vessel.VesselStatus.ResourceSystem.Resources.Count)
                vessel.VesselStatus.ResourceSystem.Resources[JawResourceIndex].OnResourceChange += calculateBlastAngle;

            if (driftTrailAction)      driftTrailAction.OnChangeDriftAltitude += calculateDriftAngle;
            if (vesselPrismController) vesselPrismController.OnBlockCreated   += HandleBlockCreation;
        }

        // === New: same logic, but HUD containers come from VesselHUDView ===
        public void Initialize(CosmicShore.Game.IVesselStatus status, CosmicShore.Game.VesselHUDView hudView)
        {
            if (status == null)
                throw new System.ArgumentNullException(nameof(status));
            // keep legacy subscriptions and behavior
            Initialize(status.Vessel);
            // bind HUD containers/jaws from the view (same logic you already had)
            if (hudView)
            {
                _silhouetteContainer   = hudView.SilhouetteContainer;
                _trailDisplayContainer = hudView.TrailDisplayContainer;

                if (!topJaw && hudView.TopJaw)       topJaw = hudView.TopJaw.gameObject;
                if (!bottomJaw && hudView.BottomJaw) bottomJaw = hudView.BottomJaw.gameObject;
                if (JawResourceIndex < 0)            JawResourceIndex = hudView.JawResourceIndex;
            }
        }
        
        public void SetHudReferences(Transform silhouetteContainer, Transform trailDisplayContainer)
        {
            SetSilhouetteReference(silhouetteContainer, trailDisplayContainer);
        }

        public void SetSilhouetteReference(Transform silhouetteContainer, Transform trailDisplayContainer)
        {
            _silhouetteContainer = silhouetteContainer;
            _trailDisplayContainer = trailDisplayContainer;
        }

        public void SetBlockPrefab(GameObject block)
        {
            BlockPrefab = block;
        }

        float dotProduct = .9999f;
        private void calculateDriftAngle(float dotProduct)
        {
            if (vessel?.VesselStatus == null) return;

            foreach (var part in silhouetteParts)
                if (part) part.SetActive(!vessel.VesselStatus.AutoPilotEnabled && vessel.VesselStatus.Player.IsActive);

            if (_silhouetteContainer != null)
                _silhouetteContainer.transform.localRotation =
                    Quaternion.Euler(0, 0, Mathf.Asin(dotProduct - .0001f) * Mathf.Rad2Deg);

            this.dotProduct = dotProduct; // Acos hates 1
        }

        private void calculateBlastAngle(float currentAmmo)
        {
            foreach (var part in silhouetteParts) { if (part) part.SetActive(true); }

            if (topJaw)
            {
                topJaw.transform.localRotation = Quaternion.Euler(0, 0, 21 * currentAmmo);
                var img = topJaw.GetComponent<Image>();
                if (img) img.color = currentAmmo > .98f ? Color.green : Color.white;
            }

            if (bottomJaw)
            {
                bottomJaw.transform.localRotation = Quaternion.Euler(0, 0, -21 * currentAmmo);
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
                if (swingBlocks)
                    poolSize = Mathf.CeilToInt(rect.rect.width / (vesselPrismController.MinWaveLength * worldToUIScale));
                else
                    poolSize = Mathf.CeilToInt(rect.rect.width / (vesselPrismController.MinWaveLength * worldToUIScale * scaleY));
                InitializeBlockPool();
            }

            if (swingBlocks)
                UpdateBlockPool(
                    xShift * (scaleY / 2f) * worldToUIScale,
                    wavelength * worldToUIScale,
                    scaleX * scaleY * imageScale,
                    scaleZ * imageScale
                );
            else
                UpdateBlockPool(
                    xShift * worldToUIScale * scaleY,
                    wavelength * worldToUIScale * scaleY,
                    scaleX * scaleY * imageScale,
                    scaleZ * scaleY * imageScale
                ); // VPS per unit speed proportional to display area
        }

        private void InitializeBlockPool()
        {
            if (_trailDisplayContainer == null || BlockPrefab == null) return;

            blockPool = new GameObject[poolSize, 2]; // Two blocks per column
            for (int i = 0; i < poolSize; i++)
            {
                var tempContainer = new GameObject($"TrailCol_{i}");
                var colRT = tempContainer.AddComponent<RectTransform>();
                colRT.SetParent(_trailDisplayContainer, false);

                for (int j = 0; j < 2; j++)
                {
                    var newBlock = Instantiate(BlockPrefab, _trailDisplayContainer.transform);
                    newBlock.transform.SetParent(tempContainer.transform, false);

                    var rect = (RectTransform)_trailDisplayContainer;
                    newBlock.transform.parent.localPosition = new Vector3(
                        -i * vesselPrismController.MinWaveLength * worldToUIScale + (rect.rect.width / 2f),
                        0, 0);

                    newBlock.transform.localPosition = new Vector3(
                        0, j * 2 * vesselPrismController.Gap - vesselPrismController.Gap, 0);

                    newBlock.transform.localScale = Vector3.zero;
                    newBlock.SetActive(true);
                    blockPool[i, j] = newBlock;
                }
            }
        }

        private void UpdateBlockPool(float xShift, float wavelength, float scaleX, float scaleZ)
        {
            if (_trailDisplayContainer == null || vessel?.VesselStatus == null) return;
            if (vessel.VesselStatus.AutoPilotEnabled) return;

            var rect = (RectTransform)_trailDisplayContainer;

            // head (column 0)
            for (int j = 0; j < 2; j++)
            {
                blockPool[0, j].transform.localScale = new Vector3(scaleZ, j * 2 * scaleX - scaleX, 1);
                blockPool[0, j].transform.parent.localPosition = new Vector3(rect.rect.width / 2f, 0, 0);
                blockPool[0, j].transform.localPosition = new Vector3(0, j * 2 * xShift - xShift, 0);
            }

            if (driftTrailAction)
            {
                blockPool[0, 0].transform.parent.localRotation =
                    Quaternion.Euler(0, 0, -Mathf.Acos(dotProduct - .0001f) * Mathf.Rad2Deg);
            }

            // conveyor
            for (int i = poolSize - 1; i > 0; i--)
            {
                for (int j = 0; j < 2; j++)
                {
                    blockPool[i, j].transform.localScale = blockPool[i - 1, j].transform.localScale;
                    blockPool[i, j].transform.parent.localPosition = new Vector3(
                        -i * wavelength + (rect.rect.width / 2f), 0, 0);
                    blockPool[i, j].transform.localPosition = blockPool[i - 1, j].transform.localPosition;
                }

                bool underCurrentPoolSize = i < Mathf.CeilToInt(rect.rect.width / wavelength);
                blockPool[i, 1].transform.parent.gameObject.SetActive(underCurrentPoolSize);

                if (driftTrailAction && underCurrentPoolSize)
                {
                    blockPool[i, 0].transform.parent.localRotation =
                        blockPool[i - 1, 0].transform.parent.localRotation;
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
    }
}
