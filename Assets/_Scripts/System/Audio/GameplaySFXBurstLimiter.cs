using System.Collections.Generic;
using CosmicShore.ScriptableObjects;
using UnityEngine;

namespace CosmicShore.Core
{
    /// <summary>
    /// The burst gate every gameplay SFX one-shot passes through. Pure C# — no MonoBehaviour, no
    /// FMOD, no <see cref="Time"/> (the caller supplies "now") — so the whole decision surface is
    /// covered by edit-mode tests.
    ///
    /// <para><b>The problem it solves.</b> Gameplay SFX are fired per-EVENT, and several event
    /// classes are inherently bursty — a vessel sweeping a crystal shower, an AOE blast destroying
    /// a field of crystals, a fauna die-off dropping every heart at once, a trail collapsing.
    /// Each one-shot is its own FMOD instance, so N events in a frame is N voices; and because
    /// they are the same asset started within the same frame they sum COHERENTLY (+20·log10(N) dB
    /// — ten copies is +20 dB) while their sub-millisecond offsets comb-filter into harsh metallic
    /// phasing. Turning the volume down does not help: the phasing is a function of simultaneity,
    /// not level.</para>
    ///
    /// <para><b>What it does.</b> Per category, per
    /// <see cref="GameplaySFXCategoryPolicy"/>:</para>
    /// <list type="number">
    ///   <item>An event arriving at least <c>minRetriggerSeconds</c> after the category's last
    ///   voice, with window budget left, plays immediately at
    ///   <c>volumeScale · burstVolumeFalloff^(voices already in window)</c>. The leading edge is
    ///   never delayed, so the sound stays responsive.</item>
    ///   <item>An event that would violate the spacing or the budget does NOT simply vanish: it is
    ///   folded into a pending aggregate (running position centroid + a count of what it stands
    ///   for), up to <c>maxPendingVoices</c> aggregates. Arrivals past that merge into the last
    ///   aggregate, so nothing is silently lost and the pending list cannot grow.</item>
    ///   <item><see cref="Drain"/> — pumped once a frame — emits at most one pending aggregate per
    ///   call, and only once the spacing and budget allow it. A 30-crystal burst therefore becomes
    ///   a short spaced rattle of a few distinct, progressively quieter hits at the centroid of
    ///   where the crystals actually were, instead of one phased wall.</item>
    /// </list>
    ///
    /// <para><b>Positions.</b> An aggregate is spatial if any event folded into it was spatial, and
    /// its position is the mean of the spatial ones — the acoustic centre of the burst.
    /// Non-spatial (2D) events contribute only to the count.</para>
    /// </summary>
    public sealed class GameplaySFXBurstLimiter
    {
        /// <summary>One voice the limiter has decided should now be played.</summary>
        public readonly struct Emission
        {
            /// <summary>Category whose FMOD event should be started.</summary>
            public readonly GameplaySFXCategory Category;
            /// <summary>True when <see cref="Position"/> is meaningful and the voice is 3D.</summary>
            public readonly bool Spatial;
            /// <summary>Centroid of the spatial events this voice stands for.</summary>
            public readonly Vector3 Position;
            /// <summary>Final volume multiplier (already includes the category's volumeScale).</summary>
            public readonly float VolumeMultiplier;
            /// <summary>How many suppressed events this one voice represents (≥ 1).</summary>
            public readonly int Represents;

            public Emission(
                GameplaySFXCategory category, bool spatial, Vector3 position,
                float volumeMultiplier, int represents)
            {
                Category = category;
                Spatial = spatial;
                Position = position;
                VolumeMultiplier = volumeMultiplier;
                Represents = represents;
            }
        }

        sealed class PendingAggregate
        {
            public int Count;
            public int SpatialCount;
            public Vector3 SpatialPositionSum;

            public void Add(bool spatial, Vector3 position)
            {
                Count++;
                if (!spatial) return;
                SpatialCount++;
                SpatialPositionSum += position;
            }

            public bool Spatial => SpatialCount > 0;
            public Vector3 Centroid => SpatialCount > 0 ? SpatialPositionSum / SpatialCount : Vector3.zero;
        }

        sealed class CategoryState
        {
            public float WindowStart;
            public int WindowCount;
            public float LastVoiceTime;
            public bool HasPlayed;
            public readonly List<PendingAggregate> Pending = new();
        }

        readonly Dictionary<GameplaySFXCategory, CategoryState> _states = new();

        // Categories with at least one pending aggregate. Keeps the per-frame Drain a walk over
        // "what is actually bursting right now" rather than over every category that has ever
        // played — Drain is called every frame and is almost always a no-op.
        readonly List<GameplaySFXCategory> _pendingCategories = new();

        GameplaySFXPolicySO _policy;

        /// <summary>Total events suppressed (folded or dropped) since the last <see cref="Reset"/>.</summary>
        public int SuppressedCount { get; private set; }

        /// <summary>Total events dropped outright because the pending aggregates were full.</summary>
        public int DroppedCount { get; private set; }

        public GameplaySFXBurstLimiter(GameplaySFXPolicySO policy = null)
        {
            _policy = policy;
        }

        /// <summary>Swaps the governing policy asset. Clears all in-flight burst state.</summary>
        public void SetPolicy(GameplaySFXPolicySO policy)
        {
            _policy = policy;
            Reset();
        }

        /// <summary>Clears all window / spacing / pending state and the suppression counters.</summary>
        public void Reset()
        {
            _states.Clear();
            _pendingCategories.Clear();
            SuppressedCount = 0;
            DroppedCount = 0;
        }

        /// <summary>
        /// Decides whether an event that just occurred may start a voice immediately.
        ///
        /// Returns true with the final <paramref name="volumeMultiplier"/> when it may. Returns
        /// false when it may not — in which case the event has either been folded into a pending
        /// aggregate for <see cref="Drain"/> to replay, or (if the aggregates are full) dropped.
        /// </summary>
        /// <param name="category">Category of the event.</param>
        /// <param name="now">Monotonic seconds (callers pass <see cref="Time.unscaledTime"/>).</param>
        /// <param name="spatial">True when the caller supplied a world position.</param>
        /// <param name="position">World position; ignored when <paramref name="spatial"/> is false.</param>
        /// <param name="volumeMultiplier">Final volume multiplier when the return value is true.</param>
        public bool TryAdmit(
            GameplaySFXCategory category, float now, bool spatial, Vector3 position,
            out float volumeMultiplier)
        {
            var policy = PolicyFor(category);
            var state = StateFor(category);

            RollWindow(state, now, policy);

            // A pending backlog OUTRANKS a fresh arrival. This ordering is load-bearing: a
            // sustained burst (an AOE blast destroying 48 prisms every frame for many frames)
            // delivers new events before every Drain, so letting the newest one take the slot
            // starves the backlog for as long as the burst lasts — every voice then speaks for
            // exactly one prism, gets no magnitude bonus, and the aggregates just grow until the
            // blast ends and release as a late thump. Deferring to the queue instead means the
            // voices that do play are the ones representing the crowd.
            //
            // An isolated event still plays instantly, because nothing is pending in that case.
            if (state.Pending.Count == 0 && Admits(state, now, policy))
            {
                // An immediate voice speaks for exactly one event, so it gets no magnitude bonus.
                volumeMultiplier = VolumeFor(state, policy, represents: 1);
                Commit(state, now);
                return true;
            }

            volumeMultiplier = 0f;
            SuppressedCount++;
            Fold(category, state, policy, spatial, position);
            return false;
        }

        /// <summary>
        /// Pump once per frame. Emits at most one pending aggregate per bursting category — the
        /// spacing between successive voices is exactly what stops them summing coherently, so
        /// they are deliberately released one at a time rather than flushed together.
        /// </summary>
        /// <param name="now">Monotonic seconds (callers pass <see cref="Time.unscaledTime"/>).</param>
        /// <param name="into">Emissions are appended here. Not cleared by this method.</param>
        public void Drain(float now, List<Emission> into)
        {
            if (_pendingCategories.Count == 0 || into == null) return;

            for (int i = _pendingCategories.Count - 1; i >= 0; i--)
            {
                var category = _pendingCategories[i];
                if (!_states.TryGetValue(category, out var state) || state.Pending.Count == 0)
                {
                    _pendingCategories.RemoveAt(i);
                    continue;
                }

                var policy = PolicyFor(category);
                RollWindow(state, now, policy);
                if (!Admits(state, now, policy)) continue;

                var aggregate = state.Pending[0];
                state.Pending.RemoveAt(0);

                int represents = Mathf.Max(1, aggregate.Count);
                into.Add(new Emission(
                    category,
                    aggregate.Spatial,
                    aggregate.Centroid,
                    VolumeFor(state, policy, represents),
                    represents));

                Commit(state, now);

                if (state.Pending.Count == 0) _pendingCategories.RemoveAt(i);
            }
        }

        // ---------- internals ----------

        GameplaySFXCategoryPolicy PolicyFor(GameplaySFXCategory category)
        {
            _policy ??= GameplaySFXPolicySO.LoadDefault();
            return _policy != null ? _policy.For(category) : GameplaySFXCategoryPolicy.Unlimited();
        }

        CategoryState StateFor(GameplaySFXCategory category)
        {
            if (_states.TryGetValue(category, out var state)) return state;
            state = new CategoryState();
            _states[category] = state;
            return state;
        }

        /// <summary>Starts a fresh counting window once the current one has elapsed.</summary>
        static void RollWindow(CategoryState state, float now, GameplaySFXCategoryPolicy policy)
        {
            if (!state.HasPlayed)
            {
                state.WindowStart = now;
                return;
            }

            if (now - state.WindowStart > Mathf.Max(0.0001f, policy.windowSeconds))
            {
                state.WindowStart = now;
                state.WindowCount = 0;
            }
        }

        /// <summary>True when the category may start a voice right now: spacing met and budget left.</summary>
        static bool Admits(CategoryState state, float now, GameplaySFXCategoryPolicy policy)
        {
            if (state.HasPlayed && now - state.LastVoiceTime < policy.minRetriggerSeconds) return false;
            return state.WindowCount < Mathf.Max(1, policy.maxVoicesPerWindow);
        }

        /// <summary>
        /// Volume for the next voice: the category baseline, decayed once per voice already played
        /// in this window (floored so a long burst never fades to inaudible), then scaled up by how
        /// many events this one voice stands for.
        ///
        /// <para>That last term is what carries MAGNITUDE. Before the limiter, a big burst was loud
        /// because its voices stacked — which is exactly the defect, and why categories like
        /// BlockDestroy were attenuated to compensate. With stacking gone, that attenuation would
        /// simply make a 300-prism blast quiet. So loudness is restored deliberately and
        /// logarithmically (per doubling, clamped) on the voice that represents the crowd, instead
        /// of accidentally and coherently across N voices.</para>
        /// </summary>
        static float VolumeFor(CategoryState state, GameplaySFXCategoryPolicy policy, int represents)
        {
            float falloff = Mathf.Pow(Mathf.Clamp(policy.burstVolumeFalloff, 0.05f, 1f), state.WindowCount);
            float volume = policy.volumeScale * Mathf.Max(Mathf.Clamp01(policy.minBurstVolume), falloff);

            if (policy.burstMagnitudeGain > 0f && represents > 1)
            {
                float magnitude = 1f + policy.burstMagnitudeGain * Mathf.Log(represents, 2f);
                volume *= Mathf.Min(magnitude, Mathf.Max(1f, policy.maxBurstMagnitude));
            }

            return volume;
        }

        static void Commit(CategoryState state, float now)
        {
            state.LastVoiceTime = now;
            state.HasPlayed = true;
            state.WindowCount++;
        }

        /// <summary>
        /// Folds a suppressed event into the pending aggregates so the burst keeps its magnitude.
        /// With <c>maxPendingVoices == 0</c> it is dropped outright — the older pure-throttle
        /// behaviour.
        ///
        /// <para>Once the aggregates are full, the event merges into the one holding the FEWEST
        /// events so far, which keeps them balanced. That is load-bearing, not tidiness:
        /// <see cref="Drain"/> pops FIFO, so appending overflow to the LAST aggregate (the obvious
        /// implementation, and the one this replaced) put the whole crowd behind a single-event
        /// aggregate. The first replay then spoke for 1 event and got no magnitude bonus, and on a
        /// sustained burst — a 2000-prism blast draining at 48/frame — the loud voice was
        /// perpetually the one still queued. Balancing makes every replay carry a fair share of
        /// the burst, so the bonus actually lands.</para>
        /// </summary>
        void Fold(
            GameplaySFXCategory category, CategoryState state, GameplaySFXCategoryPolicy policy,
            bool spatial, Vector3 position)
        {
            int cap = Mathf.Max(0, policy.maxPendingVoices);
            if (cap == 0)
            {
                DroppedCount++;
                return;
            }

            if (state.Pending.Count < cap)
            {
                var aggregate = new PendingAggregate();
                aggregate.Add(spatial, position);
                state.Pending.Add(aggregate);
                if (!_pendingCategories.Contains(category)) _pendingCategories.Add(category);
                return;
            }

            var lightest = state.Pending[0];
            for (int i = 1; i < state.Pending.Count; i++)
                if (state.Pending[i].Count < lightest.Count) lightest = state.Pending[i];

            lightest.Add(spatial, position);
        }
    }
}
