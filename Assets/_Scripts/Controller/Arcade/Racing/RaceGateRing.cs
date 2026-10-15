using CosmicShore.Data;
using CosmicShore.ScriptableObjects;
using Cysharp.Threading.Tasks;
using CosmicShore.Utility;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// One gate of a race course: a SWITCH ring you thread (CLAUDE.md, "Switch";
    /// Docs/ToySystem/ARCHITECTURE.md § "The switch"), drawn in the prism shader at the radius
    /// its own crossing test uses.
    ///
    /// <para><b>Neutral, and deliberately so.</b> It is painted <see cref="Domains.Blue"/> via
    /// <see cref="ToySwitchSignal.Neutral"/> - threading it does not hand anyone a domain, and
    /// the domain colours stay reserved for the switches that do.</para>
    ///
    /// <para><b>Unless the ring BELONGS to a domain</b> (<see cref="Owner"/>). Skim Race's rings
    /// are its crystals redrawn (<see cref="CrystalRaceRing"/>): each one is a single pilot's, only
    /// that domain may thread it, and the colour is what says so - the same "belongs to" reading
    /// the Scarab's placed switch already gives <see cref="ToySwitchSignal.Domain"/> outside the
    /// toybox. Nothing in Skim Race changes your domain, so the two readings never share a
    /// screen. The gate races never pass an owner and keep the neutral ring.</para>
    ///
    /// <para><b>Except the one that is YOURS next, which goes LIME</b>
    /// (<see cref="ToySwitchSignal.Next"/>, the free-pickup CTA colour). Twenty identical rings
    /// scattered through a cell is a course you have to be told the order of; the objective arrow
    /// alone points a direction without saying WHICH ring, and at the far end of a leg several
    /// line up. This is set LOCALLY, on the local pilot's gate only - the course is built
    /// independently on every machine, so a gate object already belongs to one viewer and nothing
    /// is replicated. It makes no domain claim, so the reservation is untouched.</para>
    ///
    /// <para><b>It is a marker, not mass.</b> A mesh ring costs one renderer and no collider,
    /// against ~8 prisms and 8 colliders for a prism ring - which matters when a course is 20 of
    /// them. Nothing here is conserved mass, nothing is eaten, and nothing is removed by a clock:
    /// a gate stands for the whole match and comes down with the course.</para>
    ///
    /// <para><b>Detection lives on the CONTROLLER, not here.</b> The course is ORDERED, so each
    /// pilot only ever needs testing against one gate - their next - which is M tests a frame
    /// for M pilots rather than M x N against every ring. This class just answers "did this
    /// segment thread me", the same plane-crossing math <see cref="ScarabSwitch"/> and
    /// <see cref="AstroLeagueGoal"/> use, and for the same reason: a boosted Dolphin covers ~14
    /// units per physics tick, so a trigger volume can be flown through between two samples
    /// while a swept segment cannot be missed.</para>
    /// </summary>
    public class RaceGateRing : MonoBehaviour
    {
        /// <summary>Position in the ordered course. Matches the index a pilot reports.</summary>
        public int Index { get; private set; }

        /// <summary>Unit normal of the ring's plane - the direction the course flows through it.</summary>
        public Vector3 Axis { get; private set; } = Vector3.forward;

        /// <summary>Mouth radius. The drawn ring and the crossing test share it, by construction.</summary>
        public float Radius { get; private set; } = 1f;

        /// <summary>
        /// The domain this ring belongs to, or <see cref="Domains.Blue"/> for a neutral course gate.
        /// Only paints the ring - who may THREAD it is the caller's rule, not the ring's.
        /// </summary>
        public Domains Owner { get; private set; } = Domains.Blue;

        Transform _visual;
        GameObject _ring;
        ThemeManagerDataContainerSO _theme;
        bool _isNext;
        bool _retired;

        /// <summary>The signal the ring wears when it is not lit as the local pilot's next gate.</summary>
        ToySwitchSignal RestSignal => Owner is Domains.Jade or Domains.Ruby or Domains.Gold
            ? ToySwitchSignal.Domain
            : ToySwitchSignal.Neutral;

        /// <summary>
        /// The gate whose ring this one DRAWS THROUGH, when two gates occupy the same point.
        ///
        /// <para>An open chain can legitimately visit one place twice - Skein's course opens and
        /// closes on the same spine collar, so gate 0 and the finish gate are the same 150 u
        /// hoop. Built naively that is TWO coincident rings, and the highlight then cannot be
        /// seen: lighting the start lime leaves its neutral twin drawn in the same place, and
        /// whichever the renderer picks is a coin toss. It presented as "the start/finish ring
        /// never goes lime", which is a colour bug only in the sense that a duplicate object is
        /// a colour bug.</para>
        ///
        /// <para>One ring, two gates: the second gate draws nothing and forwards its highlight,
        /// while keeping its own crossing test and its own place in the order. The alternative -
        /// suppressing the duplicate's visual and leaving it unlit - would have made the FINISH
        /// unmarkable, which is the one gate a race most needs to point at.</para>
        /// </summary>
        RaceGateRing _visualOwner;

        /// <summary>
        /// Raise the gate. Call immediately after AddComponent.
        ///
        /// <para>The VISUAL blooms from zero on a child holder while the gate's own transform
        /// stays at scale 1 - detection is live at the full mouth from frame one and only the
        /// drawing grows into it (the split <c>ScarabScrambleHoop</c> makes for the same reason).
        /// A ring drawn SMALLER than its trigger is the legal direction: a crossing still always
        /// fires. Drawing one LARGER would be the lie the switch law forbids.</para>
        /// </summary>
        public void Build(int index, in RaceGate gate, ThemeManagerDataContainerSO theme,
                          float bloomSeconds, RaceGateRing visualOwner = null, Domains owner = Domains.Blue)
        {
            Index = index;
            Radius = Mathf.Max(1f, gate.Radius);
            Owner = owner;

            transform.position = gate.Position;
            transform.localScale = Vector3.one;
            SetAxis(gate.Axis);

            // Coincident with an earlier gate: keep the detection, skip the drawing. Nothing
            // below runs, so there is no second ring to z-fight with and no second bloom.
            _visualOwner = visualOwner;
            if (_visualOwner != null) return;

            var holder = new GameObject("Visual");
            holder.transform.SetParent(transform, false);
            _visual = holder.transform;

            // The one builder every switch ring in the game comes from. Neutral + Blue unless the
            // ring has an owner; passed explicitly so the reservation is visible at the call site.
            _theme = theme;
            _ring = ToyFactory.AddSwitchRing(_visual, Radius, theme, RestSignal, Owner);

            if (bloomSeconds > 0f)
                ToyFactory.ScaleInFromZero(_visual, bloomSeconds).Forget();
        }

        /// <summary>
        /// Turn the mouth to face <paramref name="axis"/> without rebuilding or re-blooming the
        /// ring. For a ring whose flow direction is learned AFTER it is raised - a Skim Race ring
        /// can stand before the track it sits on has been laid.
        /// </summary>
        public void SetAxis(Vector3 axis)
        {
            Axis = axis.sqrMagnitude > 1e-6f ? axis.normalized : Vector3.forward;

            // A randomly oriented gate WILL sometimes point at world up, where LookRotation's
            // default up-reference is degenerate. Project the hint onto the ring's plane and fall
            // back to +x, the guard ScarabSwitch.BuildBasis uses.
            Vector3 upHint = Vector3.ProjectOnPlane(Vector3.up, Axis);
            if (upHint.sqrMagnitude < 1e-4f) upHint = Vector3.ProjectOnPlane(Vector3.right, Axis);
            if (SafeLookRotation.TryGet(Axis, upHint.normalized, out var rot, this))
                transform.rotation = rot;
        }

        /// <summary>
        /// Repaint the ring for a new owner (<see cref="Domains.Blue"/> = neutral). Same material
        /// swap as the lime highlight, so it costs no renderer and mutates no shared material.
        /// </summary>
        public void SetOwner(Domains owner)
        {
            if (Owner == owner) return;
            Owner = owner;
            if (_visualOwner != null) { _visualOwner.SetOwner(owner); return; }
            if (_retired || _isNext || _ring == null) return;
            ToyFactory.RepaintSwitchRing(_ring, _theme, RestSignal, Owner);
        }

        /// <summary>
        /// Mark this gate as the LOCAL pilot's next one, or clear the mark.
        ///
        /// <para>A repaint, not a second renderer: <see cref="ToyFactory.RepaintSwitchRing"/>
        /// swaps the material REFERENCE (prism materials are shared theme assets and are never
        /// mutated), and the lime one is minted once and cached by colour, so a course's worth of
        /// gates costs one extra material however often the highlight moves.</para>
        ///
        /// <para>Idempotent, because the controller calls it every frame from the pilot's live
        /// progress rather than only on the frames it changes - a repaint per frame would swap a
        /// material reference 60 times a second for no reason.</para>
        /// </summary>
        public void SetIsNextForLocalPilot(bool isNext)
        {
            // Forwarded, never mirrored: the paint state belongs to whoever owns the ring, and
            // the controller always clears the old gate before setting the new one, so two gates
            // sharing one ring hand it back and forth cleanly.
            if (_visualOwner != null) { _visualOwner.SetIsNextForLocalPilot(isNext); return; }

            if (_retired || _isNext == isNext || _ring == null) return;
            _isNext = isNext;
            ToyFactory.RepaintSwitchRing(_ring, _theme,
                isNext ? ToySwitchSignal.Next : RestSignal, Owner);
        }

        /// <summary>
        /// Did the segment <paramref name="prev"/> to <paramref name="cur"/> cross this ring's
        /// plane INSIDE the mouth? Direction-agnostic: a gate threaded backwards is still
        /// threaded, which is the honest reading for a race - you still had to fly there.
        /// </summary>
        public bool CrossedMouth(Vector3 prev, Vector3 cur) =>
            !_retired && PlaneCrossing(prev, cur, transform.position, Axis, Radius) == GatePlaneCrossing.InsideMouth;

        /// <summary>
        /// Where the segment <paramref name="prev"/> to <paramref name="cur"/> crossed the plane of a
        /// ring at <paramref name="centre"/>: not at all, inside the mouth (threaded), or outside it
        /// (a miss). The one formula the ring counts threads with and the AI counts misses with
        /// (<see cref="GateRaceController"/>), so the two can never disagree about a crossing.
        /// </summary>
        public static GatePlaneCrossing PlaneCrossing(Vector3 prev, Vector3 cur, Vector3 centre, Vector3 axis, float radius)
        {
            float dPrev = Vector3.Dot(prev - centre, axis);
            float dCur = Vector3.Dot(cur - centre, axis);
            if (dPrev * dCur > 0f) return GatePlaneCrossing.None;              // same side of the plane
            if (Mathf.Approximately(dPrev, dCur)) return GatePlaneCrossing.None;

            float t = Mathf.Clamp01(dPrev / (dPrev - dCur));
            Vector3 hit = Vector3.Lerp(prev, cur, t);
            Vector3 rel = hit - centre;
            Vector3 lateral = rel - Vector3.Dot(rel, axis) * axis;
            return lateral.sqrMagnitude <= radius * radius ? GatePlaneCrossing.InsideMouth : GatePlaneCrossing.OutsideMouth;
        }

        /// <summary>
        /// Take the gate down without popping it out of existence (the continuity law applies to
        /// a marker exactly as it applies to a prism). The ring withers over
        /// <paramref name="seconds"/> and the gate goes with it.
        /// </summary>
        public void Retire(float seconds)
        {
            // Stop answering crossings the instant retirement begins - a course being struck
            // must not credit a gate somebody flies through on the way out. A flag rather than a
            // zeroed radius, so Radius stays an honest description of the mouth for anything
            // still reading it while the ring withers.
            _retired = true;
            ToyFactory.ScaleOutAndDestroy(gameObject, Mathf.Max(0.01f, seconds)).Forget();
        }
    }

    /// <summary>How a segment crossed a race ring's plane (<see cref="RaceGateRing.PlaneCrossing"/>).</summary>
    public enum GatePlaneCrossing
    {
        None = 0,
        InsideMouth = 1,
        OutsideMouth = 2,
    }
}
