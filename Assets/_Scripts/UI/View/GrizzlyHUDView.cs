using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.UI
{
    /// <summary>
    /// Grizzly HUD: energy/charge meter (single pool), rush pips, active-weapon
    /// label, dug-in indicator, and the sniper's picture-in-picture scope panel.
    /// The scope RenderTexture is created at runtime (no RT asset to manage) and
    /// bound to both the scope camera and the RawImage.
    /// </summary>
    public class GrizzlyHUDView : VesselHUDView
    {
        [Header("Energy / Charge")]
        [SerializeField] Image energyFill;
        [SerializeField] Image chargeRing;

        [Header("Rush pips")]
        [SerializeField] List<Image> rushPips = new();
        [SerializeField] Color pipReady = Color.white;
        [SerializeField] Color pipSpent = new Color(1f, 1f, 1f, 0.25f);

        [Header("Weapon mode")]
        [SerializeField] TMP_Text weaponModeLabel;

        [Header("Dig In")]
        [SerializeField] GameObject dugInIndicator;

        [Header("Sniper scope (PiP)")]
        [SerializeField] GameObject scopePanel;
        [SerializeField] RawImage scopeImage;
        [SerializeField] Camera scopeCamera;
        [SerializeField] int scopeTextureSize = 512;

        [Header("Scope reticle (scales with aim distance)")]
        [SerializeField] Image reticle;
        [SerializeField, Tooltip("Reticle size when aiming at something point-blank.")]
        float reticleMinSize = 40f;
        [SerializeField, Tooltip("Reticle size at Reticle Max Distance or aiming into empty space.")]
        float reticleMaxSize = 220f;
        [SerializeField, Tooltip("Aim distance that maps to the max reticle size.")]
        float reticleMaxDistance = 600f;
        [SerializeField, Tooltip("Seconds to smooth reticle size changes (0 = instant).")]
        float reticleSmoothTime = 0.08f;

        RenderTexture _scopeRT;
        bool _scoped;
        float _reticleSizeVelocity;

        public override void Initialize()
        {
            SetEnergy(0f);
            SetCharge(0f);
            SetDugIn(false);
            SetWeaponMode("EXPLOSIVES");
            SetScope(false);
        }

        public void SetEnergy(float energy01)
        {
            if (energyFill) energyFill.fillAmount = Mathf.Clamp01(energy01);
        }

        public void SetCharge(float charge01)
        {
            if (chargeRing) chargeRing.fillAmount = Mathf.Clamp01(charge01);
        }

        public void SetRushCharges(int current, int max)
        {
            for (int i = 0; i < rushPips.Count; i++)
            {
                if (!rushPips[i]) continue;
                rushPips[i].gameObject.SetActive(i < max);
                rushPips[i].color = i < current ? pipReady : pipSpent;
            }
        }

        public void SetWeaponMode(string label)
        {
            if (weaponModeLabel) weaponModeLabel.text = label;
        }

        public void SetDugIn(bool dugIn)
        {
            if (dugInIndicator) dugInIndicator.SetActive(dugIn);
        }

        public void SetScope(bool scoped)
        {
            _scoped = scoped;
            if (scopePanel) scopePanel.SetActive(scoped);

            if (scopeCamera)
            {
                if (scoped && _scopeRT == null)
                {
                    _scopeRT = new RenderTexture(scopeTextureSize, scopeTextureSize, 16)
                    {
                        name = "GrizzlyScopeRT"
                    };
                    scopeCamera.targetTexture = _scopeRT;
                    if (scopeImage) scopeImage.texture = _scopeRT;
                }
                scopeCamera.enabled = scoped;
            }

            // Seed the reticle at max (empty-space) size so opening the scope
            // doesn't play a grow animation from whatever the last hold left.
            if (scoped && reticle)
            {
                reticle.rectTransform.sizeDelta = Vector2.one * reticleMaxSize;
                _reticleSizeVelocity = 0f;
            }
        }

        void Update()
        {
            if (!_scoped || !reticle || !scopeCamera) return;

            // The reticle reads as the shot's footprint at the aimed surface:
            // small on a close hit, wide at range or into empty space.
            float distance = reticleMaxDistance;
            var scopeTf = scopeCamera.transform;
            if (Physics.Raycast(scopeTf.position, scopeTf.forward, out var hit, reticleMaxDistance))
                distance = hit.distance;

            float target = Mathf.Lerp(reticleMinSize, reticleMaxSize,
                Mathf.Clamp01(distance / reticleMaxDistance));

            var rect = reticle.rectTransform;
            float size = reticleSmoothTime > 0f
                ? Mathf.SmoothDamp(rect.sizeDelta.x, target, ref _reticleSizeVelocity, reticleSmoothTime)
                : target;
            rect.sizeDelta = Vector2.one * size;
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
            if (_scopeRT != null)
            {
                if (scopeCamera) scopeCamera.targetTexture = null;
                _scopeRT.Release();
                Destroy(_scopeRT);
                _scopeRT = null;
            }
        }
    }
}
