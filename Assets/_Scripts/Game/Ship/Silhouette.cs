using CosmicShore.Game;
using CosmicShore.Utilities;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore
{
    public class Silhouette : MonoBehaviour
    {
        float worldToUIScale = 2;
        float imageScale = .02f;

        [SerializeField] List<GameObject> silhouetteParts = new();

        #region Optional configuration 
        // trails //
        [SerializeField] TrailSpawner trailSpawner;

        // drifting //
        [SerializeField] DriftTrailAction driftTrailAction;

        [SerializeField] Vector3 sihouetteScale = Vector3.one;

        // [SerializeField] SilhouetteEventChannelSO OnSilhouetteInitialized;
        // [SerializeField] ScriptableEventSilhouetteData OnSilhouetteInitialized;

        // jaws //
        [SerializeField] GameObject topJaw;
        [SerializeField] GameObject bottomJaw;
        [SerializeField] int JawResourceIndex;

        [SerializeField] bool swingBlocks;
        #endregion

        public GameObject BlockPrefab { get; private set; }
        private GameObject[,] blockPool;
        private int poolSize;

        IShip _ship;

        Transform _silhouetteContainer;
        Transform _trailDisplayContainer;


        private void OnDisable()
        {
            if (driftTrailAction) driftTrailAction.OnChangeDriftAltitude -= calculateDriftAngle;
            if (topJaw) _ship.ShipStatus.ResourceSystem.Resources[JawResourceIndex].OnResourceChange -= calculateBlastAngle;
            if (trailSpawner) trailSpawner.OnBlockCreated -= HandleBlockCreation;
        }

        public void Initialize(IShip ship)
        {
            _ship = ship;

            // TODO - Remove GameCanvas dependency
            /*if (!_ship.ShipStatus.AIPilot.AutoPilotEnabled && _ship.ShipStatus.Player.GameCanvas != null)
            {
                hud = _ship.ShipStatus.Player.GameCanvas.MiniGameHUD;
                silhouetteContainer = hud.SetSilhouetteActive(!ship.ShipStatus.AIPilot.AutoPilotEnabled && ship.ShipStatus.Player.IsActive);
                trailDisplayContainer = hud.SetTrailDisplayActive(!ship.ShipStatus.AIPilot.AutoPilotEnabled).transform;
                foreach (var part in silhouetteParts)
                {
                    part.transform.SetParent(silhouetteContainer.transform, false);
                    part.SetActive(true);
                }
            }*/

            // if (!_ship.ShipStatus.AutoPilotEnabled)
            // {
            //     OnSilhouetteInitialized.Raise(new SilhouetteData()
            //     {
            //         Sender = this,
            //         IsSilhouetteActive = !ship.ShipStatus.AutoPilotEnabled && ship.ShipStatus.Player.IsActive,
            //         IsTrailDisplayActive = !ship.ShipStatus.AutoPilotEnabled,
            //         Silhouettes = silhouetteParts
            //     });
            // }

            if (topJaw) _ship.ShipStatus.ResourceSystem.Resources[JawResourceIndex].OnResourceChange += calculateBlastAngle;
            if (driftTrailAction) driftTrailAction.OnChangeDriftAltitude += calculateDriftAngle;
            if (trailSpawner) trailSpawner.OnBlockCreated += HandleBlockCreation;
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
            foreach (var part in silhouetteParts) { part.gameObject.SetActive(!_ship.ShipStatus.AutoPilotEnabled && _ship.ShipStatus.Player.IsActive); } // TODO: why?

            // TODO - SilhouetteContainer should be set by the HUD that is listening to our initialize event.
            if (_silhouetteContainer != null)
                _silhouetteContainer.transform.localRotation = Quaternion.Euler(0, 0, Mathf.Asin(dotProduct-.0001f) * Mathf.Rad2Deg);
            
            this.dotProduct = dotProduct;// Acos hates 1
        }

        private void calculateBlastAngle(float currentAmmo)
        {
            foreach (var part in silhouetteParts) { part.gameObject.SetActive(true);}
            topJaw.transform.localRotation = Quaternion.Euler(0, 0, 21 * currentAmmo);
            topJaw.GetComponent<Image>().color = currentAmmo > .98 ? Color.green : Color.white;

            
            bottomJaw.transform.localRotation = Quaternion.Euler(0, 0, -21 * currentAmmo);
            bottomJaw.GetComponent<Image>().color = currentAmmo > .98 ? Color.green : Color.white ;

        }

        private void HandleBlockCreation(float xShift, float wavelength, float scaleX, float scaleY, float scaleZ)
        {
            if (!_ship.ShipStatus.AutoPilotEnabled)
            {
                if (poolSize < 1)
                {
                    if (swingBlocks && _trailDisplayContainer != null) 
                        poolSize = Mathf.CeilToInt(((RectTransform)_trailDisplayContainer).rect.width / (trailSpawner.MinWaveLength * worldToUIScale));

                    else if (_trailDisplayContainer != null) 
                        poolSize = Mathf.CeilToInt(((RectTransform)_trailDisplayContainer).rect.width / (trailSpawner.MinWaveLength * worldToUIScale * scaleY));
                    
                    InitializeBlockPool();
                }
                if (swingBlocks) UpdateBlockPool(xShift * (scaleY / 2) * worldToUIScale, wavelength * worldToUIScale, scaleX * scaleY * imageScale, scaleZ * imageScale);
                else UpdateBlockPool(xShift * worldToUIScale * scaleY, wavelength * worldToUIScale * scaleY, scaleX * scaleY * imageScale, scaleZ * scaleY * imageScale); // VPS per unit speed is proportional to display area because gap doesn't matter and (x*y) * (z*y) / (wavelength*y) is proportional to volume/wavelength.
            }
        }

        private void InitializeBlockPool()
        {
            if (_trailDisplayContainer == null)
                return;

            blockPool = new GameObject[poolSize, 2]; // Two blocks per column
            for (int i = 0; i < poolSize; i++)
            {
                GameObject tempContainer = new GameObject();
                tempContainer.AddComponent<RectTransform>();
                tempContainer.transform.SetParent(_trailDisplayContainer, false);
                
                
                for (int j = 0; j < 2; j++)
                {
                    GameObject newBlock = Instantiate(BlockPrefab, _trailDisplayContainer.transform);
                    newBlock.transform.SetParent(tempContainer.transform, false);
                    newBlock.transform.parent.localPosition = new Vector3(-i * trailSpawner.MinWaveLength * worldToUIScale +
                        (((RectTransform)_trailDisplayContainer).rect.width/2), 0, 0);
                    newBlock.transform.localPosition = new Vector3(0, j * 2 * trailSpawner.Gap - trailSpawner.Gap, 0);
                    newBlock.transform.localScale = Vector3.zero;
                    newBlock.SetActive(true);
                    blockPool[i, j] = newBlock;
                }
            }
        }



        private void UpdateBlockPool(float xShift, float wavelength, float scaleX, float scaleZ)
        {
            if (_trailDisplayContainer == null)
                return;

            if (!_ship.ShipStatus.AutoPilotEnabled)
            {
                for (int j = 0; j < 2; j++)
                {
                    blockPool[0, j].transform.localScale = new Vector3(scaleZ, j * 2 * scaleX - scaleX, 1);
                    blockPool[0, j].transform.parent.localPosition = new Vector3(((RectTransform)_trailDisplayContainer).rect.width / 2, 0, 0);
                    blockPool[0, j].transform.localPosition = new Vector3(0, j * 2 * xShift - xShift, 0);
                }
                if (driftTrailAction)
                {
                    blockPool[0, 0].transform.parent.localRotation = Quaternion.Euler(0, 0, -Mathf.Acos(dotProduct - .0001f) * Mathf.Rad2Deg);
                }
                for (int i = poolSize - 1; i > 0; i--)
                {
                    for (int j = 0; j < 2; j++)
                    {
                        blockPool[i, j].transform.localScale = blockPool[i - 1, j].transform.localScale;
                        blockPool[i, j].transform.parent.localPosition = new Vector3(-i * wavelength +
                            (((RectTransform)_trailDisplayContainer).rect.width / 2), 0, 0);
                        blockPool[i, j].transform.localPosition = blockPool[i - 1, j].transform.localPosition;
                    }

                    bool underCurrentPoolSize = i < Mathf.CeilToInt(((RectTransform)_trailDisplayContainer).rect.width / wavelength);
                    blockPool[i, 1].transform.parent.gameObject.SetActive(underCurrentPoolSize);

                    if (driftTrailAction)
                    {
                        if (underCurrentPoolSize) blockPool[i, 0].transform.parent.localRotation = blockPool[i - 1, 0].transform.parent.localRotation;
                    }
                }
            }
        }

        public void Clear()
        {
            if (_trailDisplayContainer != null)
                foreach (Transform t in _trailDisplayContainer.transform) { t.gameObject.SetActive(false); }

            if (_silhouetteContainer != null)
                foreach (Transform t in _silhouetteContainer.transform) { t.gameObject.SetActive(false); }
        }
    }
}
