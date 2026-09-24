using CosmicShore.ScriptableObjects;
using CosmicShore.Utility;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The per-object half of the WAKE — a warhead's SHOCKWAVE FRONT (Docs/PRISM_ANIMATION.md
    /// §4.7.3): while this object is carrying a live warhead it reports its position, its blast
    /// reach and where the current pulse's shell has got to, every frame, and the mass inside that
    /// radius ripples on the GPU as the front sweeps out through it.
    ///
    /// <para><b>It belongs to ONE thing today, deliberately.</b> This shipped on every vessel for
    /// exactly one playtest and was pulled: a ripple that follows every hull in the match is
    /// wallpaper, and wallpaper is the one thing a 96-prism high-poly budget cannot afford to be
    /// spent on. It then spent a branch on two carriers — a missile and a ball — and the ball was
    /// pulled for the same reason one step down: a ball is in play for a whole match, so its front
    /// was continuous too. What is left is the ONE object whose front carries information nobody can
    /// get any other way: the Sparrow's HEAVY skyburst, whose shell is the radius its warhead is
    /// about to go off in. A shockwave is an event, not a texture. Adding a second carrier is a
    /// design call, not a wiring one.</para>
    ///
    /// <para><b>It asks a CAPABILITY for its reach.</b> Whatever component on this GameObject answers
    /// <see cref="IPrismWakeCarrier"/> supplies the blast radius; with nothing answering, this
    /// component does nothing at all. There is deliberately no transform-measurement fallback — the
    /// cylinder wake had one, and for a front the fallback would have to invent the one number that
    /// must not be invented: a radius the blast does not actually have.</para>
    ///
    /// <para><b>The PULSE is integrated here.</b> A front is born at the shell's own half-thickness
    /// and dies at the full reach, and the next is already on its way; the source advances one
    /// normalised pulse clock at the config's rate and derives both the front's radius and its own
    /// strength envelope from it (<c>PrismWakeConfigSO.FrontRadiusAt</c> /
    /// <c>FrontEnvelope</c>). Integrating rather than evaluating a function of the round's position
    /// is what keeps the front travelling smoothly while the round's reach GROWS under it as MASS
    /// swells it — the shell keeps going outward instead of jumping when the reach changes.</para>
    ///
    /// <para><b>There is no speed window.</b> See <see cref="IPrismWakeCarrier"/>: the first cut's
    /// engage speed sat above the top speed of the hull that flew it, so the effect never appeared,
    /// which reads on screen exactly like one that is too weak.</para>
    /// </summary>
    [DisallowMultipleComponent]
    public class PrismWakeSource : MonoBehaviour
    {
        IPrismWakeCarrier _carrier;
        bool _carrierResolved;

        float _ease;        // 0..1 — is this round carrying a shockwave at all
        float _pulse;       // 0..1 — how far through the current front's life
        float _reach;

        /// <summary>The blast reach the front is drawing this frame; 0 when nothing is live.</summary>
        public float Reach => _reach;

        /// <summary>The eased live-or-not weight, 0..1. Not the published strength — see Update.</summary>
        public float Ease => _ease;

        /// <summary>How far through its life the current front is, 0..1.</summary>
        public float Pulse => _pulse;

        void Awake() => ResolveCarrier();

        void OnEnable()
        {
            // A pooled round comes back out of the pool somewhere else entirely, so everything that
            // describes the LAST flight has to go — otherwise the first frame publishes a front at
            // the previous detonation's radius, mid-sweep, around this launch bay.
            _ease = 0f;
            _pulse = 0f;
            _reach = 0f;
        }

        void Update()
        {
            var config = PrismWake.Config;

            // The local is declared and seeded BEFORE the guard chain, because `&&` short-circuits:
            // with the config off or insane, TryReadReach is never called and an inline `out` would
            // be unassigned (CS0165 here, and silently a default read the day the chain gains a
            // branch that makes it well-defined).
            float reach = 0f;
            bool live = config.Enabled && config.IsSane && TryReadReach(out reach);
            if (live) _reach = reach;

            // Ease toward live/not-live rather than snapping. A round leaving the bay should not
            // arrive with a front already at full depth, and a detonating one should not blink its
            // front off (continuity of existence).
            float seconds = live ? config.EngageSeconds : config.ReleaseSeconds;
            _ease = seconds > 0f
                ? Mathf.MoveTowards(_ease, live ? 1f : 0f, Time.deltaTime / seconds)
                : (live ? 1f : 0f);

            if (_ease <= 0.001f || _reach <= 0f)
            {
                _pulse = 0f;
                PrismWake.Clear(GetInstanceID());
                return;
            }

            // The pulse clock keeps running while the front fades out, so a detonating round's last
            // shell finishes its sweep rather than freezing mid-flight and dimming.
            _pulse += PrismWake.PulseRate(config) * Time.deltaTime;
            if (_pulse >= 1f) _pulse -= Mathf.Floor(_pulse);

            // The strength the shader reads is the live weight TIMES the front's own envelope, which
            // is zero at birth and at the reach — so nothing ever appears or disappears, including at
            // the seam where one front hands over to the next.
            float strength = _ease * PrismWakeConfigSO.FrontEnvelope(_pulse);

            PrismWake.Publish(GetInstanceID(), transform, _reach,
                config.FrontRadiusAt(_pulse, _reach), strength);
        }

        void OnDisable()
        {
            _ease = 0f;
            PrismWake.Clear(GetInstanceID());
        }

        void ResolveCarrier()
        {
            if (_carrierResolved) return;
            _carrierResolved = true;
            TryGetComponent(out _carrier);
        }

        /// <summary>
        /// This frame's blast reach, from the carrier. Asked EVERY frame because a skyburst's warhead
        /// radius grows with the round in flight; there is no cache and no fallback, because a front
        /// with an invented radius would be drawing a blast that does not exist.
        /// </summary>
        bool TryReadReach(out float reach)
        {
            ResolveCarrier();
            reach = 0f;
            return _carrier != null && _carrier.TryGetShockwaveReach(out reach) && reach > 0f;
        }
    }
}
