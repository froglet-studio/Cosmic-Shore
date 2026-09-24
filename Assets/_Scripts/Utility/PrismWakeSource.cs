using CosmicShore.ScriptableObjects;
using CosmicShore.Utility;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The per-object half of the SHOCKWAVE FRONT (Docs/PRISM_ANIMATION.md §4.7.3): while this
    /// object is carrying a live blast it reports its position, that blast's reach and where its
    /// wavefront has got to, every frame, and the mass inside that radius ripples on the GPU as the
    /// front sweeps out through it.
    ///
    /// <para><b>It belongs to ONE thing today, deliberately.</b> This shipped on every vessel for
    /// exactly one playtest and was pulled: a ripple that follows every hull in the match is
    /// wallpaper, and wallpaper is the one thing a bounded high-poly budget cannot afford to be
    /// spent on. It then spent a branch on a missile and a ball, and the ball was pulled for the
    /// same reason one step down — a ball is in play for a whole match, so its front was continuous
    /// too. The missile IN FLIGHT went the same way for the same reason, one step further down
    /// again: a front trailing a travelling round is a wake, which is a texture, and the round is in
    /// the air for seconds. What is left is the ONE thing that is unambiguously an EVENT: the
    /// Sparrow's heavy skyburst WARHEAD, the 10-point shockwave blast, which exists for 0.15 s,
    /// touches no prism mass at all, and until now had no expression in the arena beyond a
    /// translucent sphere flashing once. A shockwave is an event, not a texture. Adding a second
    /// carrier is a design call, not a wiring one.</para>
    ///
    /// <para><b>It asks a CAPABILITY for everything.</b> Whatever component on this GameObject
    /// answers <see cref="IPrismWakeCarrier"/> supplies both the blast radius and how far through
    /// its sweep the front has got; with nothing answering, this component does nothing at all.
    /// There is deliberately no transform-measurement fallback and no clock of its own — the
    /// cylinder wake had a velocity fallback, and for a front the fallback would have to invent the
    /// one number that must not be invented: a radius the blast does not actually have.</para>
    ///
    /// <para><b>The PULSE CLOCK is gone with the travelling carrier.</b> A round in flight had no
    /// front of its own, so the source integrated one at an authored rate; a blast HAS a front — its
    /// trigger volume expands from nothing to its full radius on its own authored duration, and that
    /// radius is the number its damage pass already uses. So the position is read rather than
    /// integrated, the two cannot drift, and there is no pulses-per-second to author wrong. What the
    /// source still owns is the mapping into the shell's legal travel band
    /// (<c>PrismWakeConfigSO.FrontRadiusAt</c>, which floors the front at the shell's own
    /// half-thickness so it can never straddle the centre — the second half of the no-fold proof)
    /// and the front's strength envelope (<c>FrontEnvelope</c>).</para>
    ///
    /// <para><b>The ENGAGE ease is authored 0 for this carrier, on purpose.</b> The envelope is
    /// already exactly zero — value AND slope — at birth, which is the whole of what an engage ease
    /// was buying on a round leaving its bay. On a 0.15 s event a second one is pure attenuation:
    /// at the old 0.15 s engage the front never reached half depth. The RELEASE ease still earns its
    /// keep, because a blast can be cancelled mid-sweep (a turn end freezes it) with the envelope at
    /// full value, and that must fade rather than blink.</para>
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

        float _ease;        // 0..1 — is this object carrying a shockwave at all
        float _progress;    // 0..1 — how far through its sweep the carrier says its front is
        float _reach;

        /// <summary>The blast reach the front is drawing this frame; 0 when nothing is live.</summary>
        public float Reach => _reach;

        /// <summary>The eased live-or-not weight, 0..1. Not the published strength — see Update.</summary>
        public float Ease => _ease;

        /// <summary>How far through its sweep the front is, 0..1, as last reported by the carrier.</summary>
        public float Progress => _progress;

        void Awake() => ResolveCarrier();

        void OnEnable()
        {
            // Nothing that describes a previous life may survive — a pooled carrier comes back out
            // somewhere else entirely, and the first frame would otherwise publish a front at the
            // last blast's radius, mid-sweep, around wherever this one now is.
            _ease = 0f;
            _progress = 0f;
            _reach = 0f;
        }

        void Update()
        {
            var config = PrismWake.Config;

            // The locals are declared and seeded BEFORE the guard chain, because `&&`
            // short-circuits: with the config off or insane, TryReadShockwave is never called and an
            // inline `out` would be unassigned (CS0165 here, and silently a default read the day the
            // chain gains a branch that makes it well-defined).
            float reach = 0f;
            float progress = 0f;
            bool live = config.Enabled && config.IsSane && TryReadShockwave(out reach, out progress);
            if (live)
            {
                _reach = reach;
                // Held rather than reset when the carrier goes quiet, so a cancelled blast's front
                // fades from where it actually was instead of snapping back to the shell's birth
                // radius while it does it.
                _progress = progress;
            }

            // Ease toward live/not-live rather than snapping. A blast that is cancelled mid-sweep
            // must not blink its front off (continuity of existence).
            float seconds = live ? config.EngageSeconds : config.ReleaseSeconds;
            _ease = seconds > 0f
                ? Mathf.MoveTowards(_ease, live ? 1f : 0f, Time.deltaTime / seconds)
                : (live ? 1f : 0f);

            if (_ease <= 0.001f || _reach <= 0f)
            {
                _progress = 0f;
                PrismWake.Clear(GetInstanceID());
                return;
            }

            // The strength the shader reads is the live weight TIMES the front's own envelope, which
            // is zero — value and slope — at birth and at the reach. The zero at BIRTH is what makes
            // the residency swap invisible: the carrier answers true at progress 0 for a frame
            // before its sweep begins (see IPrismWakeCarrier), so the frame PrismWake.Flush hands
            // those prisms the high-poly mesh is a frame on which the map moves nothing at all.
            float strength = _ease * PrismWakeConfigSO.FrontEnvelope(_progress);

            PrismWake.Publish(GetInstanceID(), transform, _reach,
                config.FrontRadiusAt(_progress, _reach), strength);
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
        /// This frame's blast reach and wavefront progress, from the carrier. Asked EVERY frame:
        /// the progress obviously moves, and the reach is read live because a carrier is entitled to
        /// resize. There is no cache and no fallback, because a front with an invented radius would
        /// be drawing a blast that does not exist.
        /// </summary>
        bool TryReadShockwave(out float reach, out float progress01)
        {
            ResolveCarrier();
            reach = 0f;
            progress01 = 0f;
            return _carrier != null
                && _carrier.TryGetShockwave(out reach, out progress01)
                && reach > 0f;
        }
    }
}
