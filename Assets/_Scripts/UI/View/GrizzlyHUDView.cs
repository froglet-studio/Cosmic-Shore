using System.Collections.Generic;
using CosmicShore.Data;
using CosmicShore.Gameplay;
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

        [Header("Scope reticle (shrinks with aim distance)")]
        [SerializeField] Image reticle;
        [SerializeField, Tooltip("SMALLEST size - reached at Reticle Max Distance or aiming into empty space.")]
        float reticleMinSize = 40f;
        [SerializeField, Tooltip("LARGEST size - reached when aiming at something point-blank.")]
        float reticleMaxSize = 220f;
        [SerializeField, Tooltip("Aim distance at which the reticle reaches its SMALLEST size.")]
        float reticleMaxDistance = 600f;
        [SerializeField, Tooltip("Seconds to smooth reticle size changes (0 = instant).")]
        float reticleSmoothTime = 0.08f;

        [SerializeField, Tooltip("Extra gap in front of the round's nose for the chase camera, world units.")]
        float roundCamNoseMargin = 3f;

        [Header("Scope reticle target colours")]
        [SerializeField, Tooltip("No target under the crosshair.")]
        Color reticleNoTargetColor = Color.white;
        [SerializeField, Tooltip("Own-domain vessels and structures.")]
        Color reticleAllyColor = new Color(0.25f, 1f, 0.35f, 1f);
        [SerializeField, Tooltip("Neutral wildlife - flora and fauna.")]
        Color reticleFaunaColor = new Color(0.3f, 0.6f, 1f, 1f);
        [SerializeField, Tooltip("Opposing vessels and structures.")]
        Color reticleEnemyColor = new Color(1f, 0.25f, 0.25f, 1f);
        [SerializeField, Tooltip("Seconds to blend between target colours (0 = instant).")]
        float reticleColorBlendTime = 0.06f;

        RenderTexture _scopeRT;
        bool _scoped;
        Transform _chasedRound;          // live sniper round the PiP is riding
        float _roundCamForwardOffset;    // measured clearance past the round's nose
        Transform _scopeRestParent;      // where the scope camera lives on the hull
        Vector3 _scopeRestLocalPos;
        Quaternion _scopeRestLocalRot;
        bool _scopeRestCaptured;
        float _reticleSizeVelocity;
        Domains _ownDomain = Domains.Blue;

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
            CaptureScopeRest();
            bool visible = scoped || _chasedRound != null;
            if (scopePanel) scopePanel.SetActive(visible);

            if (scopeCamera)
            {
                if (visible && _scopeRT == null)
                {
                    _scopeRT = new RenderTexture(scopeTextureSize, scopeTextureSize, 16)
                    {
                        name = "GrizzlyScopeRT"
                    };
                    scopeCamera.targetTexture = _scopeRT;
                    if (scopeImage) scopeImage.texture = _scopeRT;
                }
                scopeCamera.enabled = visible;
            }

            // Seed at the empty-space state - smallest size, no-target colour - so
            // opening the scope does not animate from whatever the last hold left.
            if (scoped && reticle)
            {
                reticle.rectTransform.sizeDelta = Vector2.one * reticleMinSize;
                reticle.color = reticleNoTargetColor;
                _reticleSizeVelocity = 0f;
            }
        }

        void Update()
        {
            if (_chasedRound != null) return;   // riding a round - not aiming
            if (!_scoped || !reticle || !scopeCamera) return;

            // The reticle reads as PERSPECTIVE on the aimed surface: it SHRINKS with
            // distance, so a far target sits under a tight crosshair and a point-blank
            // one under a wide bracket. Empty space reads as the far end.
            float distance = reticleMaxDistance;
            var scopeTf = scopeCamera.transform;
            bool hasHit = Physics.Raycast(scopeTf.position, scopeTf.forward, out var hit, reticleMaxDistance);
            if (hasHit)
                distance = hit.distance;

            float target = Mathf.Lerp(reticleMaxSize, reticleMinSize,
                Mathf.Clamp01(distance / reticleMaxDistance));

            var rect = reticle.rectTransform;
            float size = reticleSmoothTime > 0f
                ? Mathf.SmoothDamp(rect.sizeDelta.x, target, ref _reticleSizeVelocity, reticleSmoothTime)
                : target;
            rect.sizeDelta = Vector2.one * size;

            var targetColor = hasHit ? ClassifyTarget(hit.collider) : reticleNoTargetColor;
            reticle.color = reticleColorBlendTime > 0f
                ? Color.Lerp(reticle.color, targetColor,
                             1f - Mathf.Exp(-Time.deltaTime / Mathf.Max(1e-4f, reticleColorBlendTime)))
                : targetColor;
        }

        /// <summary>
        /// What is under the crosshair, as a colour: white nothing, green own domain,
        /// blue neutral wildlife, red anything belonging to another domain.
        ///
        /// Lifeforms are tested FIRST and deliberately: a creature body is built from
        /// <c>HealthPrism : Prism</c>, so a raycast onto fauna hits a Prism and would
        /// otherwise be classified by that prism domain rather than as wildlife.
        /// </summary>
        Color ClassifyTarget(Collider col)
        {
            if (!col) return reticleNoTargetColor;

            if (col.GetComponentInParent<ILifeFormEntity>() != null ||
                col.GetComponentInParent<LightFauna>() != null)
                return reticleFaunaColor;

            var vessel = col.GetComponentInParent<IVesselStatus>();
            if (vessel != null)
                return vessel.Domain == _ownDomain ? reticleAllyColor : reticleEnemyColor;

            var prism = col.GetComponentInParent<Prism>();
            if (prism != null)
            {
                if (prism.Domain == _ownDomain) return reticleAllyColor;
                return prism.Domain == Domains.Blue ? reticleFaunaColor : reticleEnemyColor;
            }

            return reticleNoTargetColor;
        }

        /// <summary>The pilot domain, so the reticle can tell friend from foe.</summary>
        public void SetOwnDomain(Domains domain) => _ownDomain = domain;

        /// <summary>
        /// Hand the PiP to a sniper round in flight. The scope closes on trigger RELEASE
        /// (that is what fires), so without this the panel would blink shut at the exact
        /// moment the shot became interesting. While a round is live the PiP rides it in
        /// first person and the reticle is hidden - you are no longer aiming, you are
        /// watching.
        /// </summary>
        public void FollowRound(Transform round)
        {
            if (round == null) return;
            CaptureScopeRest();
            _chasedRound = round;
            _roundCamForwardOffset = MeasureNoseClearance(round);

            if (scopePanel) scopePanel.SetActive(true);
            if (scopeCamera)
            {
                if (_scopeRT == null)
                {
                    _scopeRT = new RenderTexture(scopeTextureSize, scopeTextureSize, 16) { name = "GrizzlyScopeRT" };
                    scopeCamera.targetTexture = _scopeRT;
                    if (scopeImage) scopeImage.texture = _scopeRT;
                }
                // Unparent so the hull cannot drag the view around while the round flies.
                scopeCamera.transform.SetParent(null, true);
                scopeCamera.enabled = true;
            }
            if (reticle) reticle.enabled = false;
        }

        /// <summary>Round landed or expired - put the PiP back on the hull.</summary>
        public void ReleaseRound()
        {
            _chasedRound = null;
            RestoreScopeToHull();

            if (reticle) reticle.enabled = true;
            if (scopePanel) scopePanel.SetActive(_scoped);
            if (scopeCamera) scopeCamera.enabled = _scoped;
        }

        /// <summary>
        /// How far past the round's pivot its own geometry reaches along its forward axis,
        /// plus a margin. MEASURED from the live renderers rather than authored, so a round
        /// of any length or scale places the camera correctly with nothing to keep in sync -
        /// the same reason the occlusion corridor measures hulls instead of authoring radii.
        /// </summary>
        float MeasureNoseClearance(Transform round)
        {
            var renderers = round.GetComponentsInChildren<Renderer>(false);
            float reach = 0f;
            var origin = round.position;
            var fwd = round.forward;

            foreach (var r in renderers)
            {
                if (r == null) continue;
                var b = r.bounds;                     // world-space AABB
                var c = b.center;
                var e = b.extents;

                // Project all eight corners; the AABB is world-aligned so the round's
                // forward is not one of its axes and the max corner is the honest answer.
                for (int i = 0; i < 8; i++)
                {
                    var corner = c + new Vector3(
                        (i & 1) == 0 ? -e.x : e.x,
                        (i & 2) == 0 ? -e.y : e.y,
                        (i & 4) == 0 ? -e.z : e.z);
                    reach = Mathf.Max(reach, Vector3.Dot(corner - origin, fwd));
                }
            }

            return reach + Mathf.Max(0f, roundCamNoseMargin);
        }

        void CaptureScopeRest()
        {
            if (_scopeRestCaptured || scopeCamera == null) return;
            var t = scopeCamera.transform;
            _scopeRestParent = t.parent;
            _scopeRestLocalPos = t.localPosition;
            _scopeRestLocalRot = t.localRotation;
            _scopeRestCaptured = true;
        }

        void RestoreScopeToHull()
        {
            if (!_scopeRestCaptured || scopeCamera == null) return;
            var t = scopeCamera.transform;
            t.SetParent(_scopeRestParent, false);
            t.localPosition = _scopeRestLocalPos;
            t.localRotation = _scopeRestLocalRot;
        }

        /// <summary>
        /// Drive the PiP camera onto the live round. LateUpdate so the round has already
        /// moved this frame - in Update the view would trail it by a frame at 400 u/s,
        /// which reads as stutter. A pooled round can be recycled without notice, so the
        /// null check is a real recovery, not a formality.
        /// </summary>
        void LateUpdate()
        {
            if (_chasedRound == null || scopeCamera == null) return;

            if (!_chasedRound.gameObject.activeInHierarchy)
            {
                ReleaseRound();
                return;
            }

            // Sit just PAST the nose, not at the origin. The round's pivot is at its
            // centre and the shell is long (the sniper dart is 80 units at its shipped
            // scale), so a camera on the pivot renders the inside of its own mesh - which
            // read as a screen full of shell colour rather than a view of anything.
            scopeCamera.transform.SetPositionAndRotation(
                _chasedRound.position + _chasedRound.forward * _roundCamForwardOffset,
                _chasedRound.rotation);
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
            _chasedRound = null;
            RestoreScopeToHull();
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
