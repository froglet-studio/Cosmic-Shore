using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using CosmicShore.Gameplay;
using System.Linq;
namespace CosmicShore.UI
{
    public class SerpentVesselHUDView : VesselHUDView
    {
        [Header("SEED WALL")]
        [SerializeField] private Sprite[] shieldIconsByCount;
        [SerializeField] private Image   shieldIcon;

        [Header("BOOST Pips")]
        [SerializeField] private Image boostPip1;
        [SerializeField] private Image boostPip2;
        [SerializeField] private Image boostPip3;
        [SerializeField] private Image boostPip4;

        [Header("Pip Colors")]
        [SerializeField] private Color pipFullColor      = new Color(1f, 1f, 1f, 1f);
        [SerializeField] private Color pipConsumingColor = new Color(0.3f, 1f, 0.3f, 1f);
        [SerializeField] private Color pipEmptyColor     = new Color(1f, 1f, 1f, 0.25f);

        Image[]    _boostPips;
        Coroutine[] _pipAnim;

        public override void Initialize()
        {
            BuildBoostPipCache();
            InitializeShieldHUD();
            InitializeBoostPips();
        }

        void BuildBoostPipCache()
        {
            if (_boostPips != null) return;

            _boostPips = new[] { boostPip1, boostPip2, boostPip3, boostPip4 };
            _pipAnim   = _boostPips != null ? new Coroutine[_boostPips.Length] : null;
        }

        // ---------- Public API for controller ----------

        public void InitializeHUD()
        {
            InitializeShieldHUD();
            InitializeBoostPips();
        }

        void InitializeShieldHUD()
        {
            if (!shieldIcon) return;

            shieldIcon.enabled = true;
            if (!shieldIcon.gameObject.activeSelf)
                shieldIcon.gameObject.SetActive(true);
        }

        public void SetShieldCount(int shields)
        {
            if (!shieldIcon || shieldIconsByCount == null || shieldIconsByCount.Length < 5)
                return;

            shields = Mathf.Clamp(shields, 0, 4);
            var sprite = shieldIconsByCount[shields];
            if (!sprite) return;

            if (!shieldIcon.gameObject.activeSelf)
                shieldIcon.gameObject.SetActive(true);

            shieldIcon.enabled = true;
            shieldIcon.sprite  = sprite;
        }

        void InitializeBoostPips()
        {
            BuildBoostPipCache();
            if (_boostPips == null) return;

            if (_pipAnim != null)
            {
                for (var i = 0; i < _pipAnim.Length; i++)
                    _pipAnim[i] = null;
            }

            foreach (var pip in _boostPips)
            {
                if (!pip) continue;

                pip.gameObject.SetActive(true);
                pip.enabled    = true;
                pip.type       = Image.Type.Filled;
                pip.fillAmount = 1f;
                pip.color      = pipFullColor;
            }
        }

        /// <summary>
        /// Snap all pips to match available/max charges.
        /// </summary>
        public void ApplyBoostSnapshot(int available, int max)
        {
            BuildBoostPipCache();
            if (_boostPips == null) return;

            // All full case
            if (available >= max && _boostPips.Length > 0)
            {
                if (_pipAnim != null)
                {
                    for (int i = 0; i < _pipAnim.Length; i++)
                    {
                        if (_pipAnim[i] != null)
                            StopCoroutine(_pipAnim[i]);
                        _pipAnim[i] = null;
                    }
                }

                foreach (var pip in _boostPips)
                {
                    if (!pip) continue;

                    pip.gameObject.SetActive(true);
                    pip.enabled    = true;
                    pip.type       = Image.Type.Filled;
                    pip.fillAmount = 1f;
                    pip.color      = pipFullColor;
                }

                return;
            }

            // Partial / empty
            for (var i = 0; i < _boostPips.Length; i++)
            {
                var pip = _boostPips[i];
                if (!pip) continue;

                pip.gameObject.SetActive(true);
                pip.enabled = true;

                // if already animating, let that finish
                if (_pipAnim?[i] != null)
                    continue;

                var full = i < available;

                pip.type       = Image.Type.Filled;
                pip.fillAmount = full ? 1f : 0f;
                pip.color      = full ? pipFullColor : pipEmptyColor;
            }
        }

        /// <summary>
        /// Animate a single pip being consumed (to empty).
        /// </summary>
        public void AnimateBoostChargeConsumed(int pipIndex, float duration)
        {
            BuildBoostPipCache();
            if (_boostPips == null) return;
            if (pipIndex < 0 || pipIndex >= _boostPips.Length) return;

            var pip = _boostPips[pipIndex];
            if (!pip) return;

            var from = pip.fillAmount > 0f ? pip.fillAmount : 1f;
            StartPipAnim(pipIndex, from, 0f, Mathf.Max(0.05f, duration));
        }

        public void ResetBoostPips()
        {
            BuildBoostPipCache();
            if (_boostPips == null) return;

            if (_pipAnim != null)
            {
                for (int i = 0; i < _pipAnim.Length; i++)
                {
                    if (_pipAnim[i] != null)
                        StopCoroutine(_pipAnim[i]);
                    _pipAnim[i] = null;
                }
            }

            foreach (var pip in _boostPips)
            {
                if (!pip) continue;

                pip.gameObject.SetActive(true);
                pip.enabled    = true;
                pip.type       = Image.Type.Filled;
                pip.fillAmount = 0f;
                pip.color      = pipEmptyColor;
            }
        }

        // ---------- Internal animation helpers ----------

        /// <summary>
        /// Run one pip's fill animation — or, on a HUD nobody is looking at, SETTLE it.
        ///
        /// <para><b>A vessel HUD is INACTIVE on every vessel but the local pilot's</b>, and
        /// <c>MonoBehaviour.StartCoroutine</c> on an inactive GameObject does not silently
        /// no-op: it logs an error, once per call, with a full native stack. An AI Serpent
        /// spends boost charges like any other pilot, so this method is reached on every AI
        /// hull in the match and the console fills with
        /// <i>"Coroutine couldn't be started because the the game object 'SerpentHUDVariant'
        /// is inactive!"</i> — an error about a display that is correctly switched off.</para>
        ///
        /// <para>The answer is the prism clock's own rule: <b>the STATE goes final and the
        /// TRANSITION is skipped.</b> A settled pip is exactly what the animation would have
        /// left behind, so a HUD re-activated later (a vessel swap handing this hull to the
        /// local pilot) shows the right value rather than a stale one — where simply
        /// returning would leave the pip reading full after the charge was spent.</para>
        ///
        /// <para>It is a guard on the VIEW rather than only on the controller because the
        /// controller's own subscribe-time gate cannot be sufficient: on the host an AI
        /// Player carries the HOST's <c>OwnerClientId</c>, so <c>IsLocalUser</c> is true for
        /// it, and <c>IsInitializedAsAI</c> — the only thing that separates the two — is
        /// written LATER in the spawn chain than the HUD is built. The controller re-checks
        /// at the point of use for the same reason; this settles whatever still gets through.</para>
        /// </summary>
        void StartPipAnim(int index, float from, float to, float seconds)
        {
            if (_boostPips == null) return;

            var pip = _boostPips[index];
            if (!pip) return;

            if (_pipAnim != null && _pipAnim[index] != null)
                StopCoroutine(_pipAnim[index]);

            if (!isActiveAndEnabled)
            {
                SettlePip(index, pip, to);
                return;
            }

            if (_pipAnim != null)
                _pipAnim[index] = StartCoroutine(CoAnimatePip(index, pip, from, to, seconds));
            else
                StartCoroutine(CoAnimatePip(index, pip, from, to, seconds));
        }

        /// <summary>
        /// The end state <see cref="CoAnimatePip"/> would have arrived at, applied at once.
        /// Kept beside the coroutine so the two cannot drift: if one gains a channel the
        /// other has to gain it too.
        /// </summary>
        void SettlePip(int index, Image pip, float to)
        {
            var end = Mathf.Clamp01(to);

            pip.enabled = true;
            pip.gameObject.SetActive(true);
            pip.type = Image.Type.Filled;
            pip.fillAmount = end;
            pip.color = (end >= 1f) ? pipFullColor : pipEmptyColor;

            if (_pipAnim != null) _pipAnim[index] = null;
        }

        IEnumerator CoAnimatePip(int index, Image pip, float from, float to, float seconds)
        {
            pip.enabled = true;
            pip.gameObject.SetActive(true);
            pip.type = Image.Type.Filled;

            var start = Mathf.Clamp01(from);
            var end   = Mathf.Clamp01(to);

            pip.color      = pipConsumingColor;
            pip.fillAmount = start;

            var t = 0f;
            while (t < seconds)
            {
                t += Time.deltaTime;
                var k = Mathf.Clamp01(t / seconds);
                pip.fillAmount = Mathf.Lerp(start, end, k);
                yield return null;
            }

            pip.fillAmount = end;
            pip.color      = (end >= 1f) ? pipFullColor : pipEmptyColor;

            if (_pipAnim != null)
                _pipAnim[index] = null;
        }
    }
}
