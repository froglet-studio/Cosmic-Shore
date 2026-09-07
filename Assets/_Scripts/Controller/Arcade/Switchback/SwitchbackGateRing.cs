using CosmicShore.Data;
using CosmicShore.ScriptableObjects;
using Cysharp.Threading.Tasks;
using CosmicShore.Utility;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// One gate of a Switchback course: a SWITCH ring you thread (CLAUDE.md, "Switch";
    /// Docs/ToySystem/ARCHITECTURE.md § "The switch"), drawn in the prism shader at the radius
    /// its own crossing test uses.
    ///
    /// <para><b>Neutral, and deliberately so.</b> It is painted <see cref="Domains.Blue"/> via
    /// <see cref="ToySwitchSignal.Neutral"/> - threading it does not hand anyone a domain, and
    /// the domain colours stay reserved for the switches that do.</para>
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
    public class SwitchbackGateRing : MonoBehaviour
    {
        /// <summary>Position in the ordered course. Matches the index a pilot reports.</summary>
        public int Index { get; private set; }

        /// <summary>Unit normal of the ring's plane - the direction the course flows through it.</summary>
        public Vector3 Axis { get; private set; } = Vector3.forward;

        /// <summary>Mouth radius. The drawn ring and the crossing test share it, by construction.</summary>
        public float Radius { get; private set; } = 1f;

        Transform _visual;
        GameObject _ring;
        ThemeManagerDataContainerSO _theme;
        bool _isNext;
        bool _retired;

        /// <summary>
        /// Raise the gate. Call immediately after AddComponent.
        ///
        /// <para>The VISUAL blooms from zero on a child holder while the gate's own transform
        /// stays at scale 1 - detection is live at the full mouth from frame one and only the
        /// drawing grows into it (the split <c>ScarabScrambleHoop</c> makes for the same reason).
        /// A ring drawn SMALLER than its trigger is the legal direction: a crossing still always
        /// fires. Drawing one LARGER would be the lie the switch law forbids.</para>
        /// </summary>
        public void Build(int index, in SwitchbackGate gate, ThemeManagerDataContainerSO theme,
                          float bloomSeconds)
        {
            Index = index;
            Axis = gate.Axis.sqrMagnitude > 1e-6f ? gate.Axis.normalized : Vector3.forward;
            Radius = Mathf.Max(1f, gate.Radius);

            transform.position = gate.Position;
            transform.localScale = Vector3.one;

            // A randomly oriented gate WILL sometimes point at world up, where LookRotation's
            // default up-reference is degenerate. Project the hint onto the ring's plane and fall
            // back to +x, the guard ScarabSwitch.BuildBasis uses.
            Vector3 upHint = Vector3.ProjectOnPlane(Vector3.up, Axis);
            if (upHint.sqrMagnitude < 1e-4f) upHint = Vector3.ProjectOnPlane(Vector3.right, Axis);
            if (SafeLookRotation.TryGet(Axis, upHint.normalized, out var rot, this))
                transform.rotation = rot;

            var holder = new GameObject("Visual");
            holder.transform.SetParent(transform, false);
            _visual = holder.transform;

            // The one builder every switch ring in the game comes from. Neutral + Blue are the
            // defaults; they are passed explicitly so the reservation is visible at the call site.
            _theme = theme;
            _ring = ToyFactory.AddSwitchRing(_visual, Radius, theme, ToySwitchSignal.Neutral, Domains.Blue);

            if (bloomSeconds > 0f)
                ToyFactory.ScaleInFromZero(_visual, bloomSeconds).Forget();
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
            if (_retired || _isNext == isNext || _ring == null) return;
            _isNext = isNext;
            ToyFactory.RepaintSwitchRing(_ring, _theme,
                isNext ? ToySwitchSignal.Next : ToySwitchSignal.Neutral, Domains.Blue);
        }

        /// <summary>
        /// Did the segment <paramref name="prev"/> to <paramref name="cur"/> cross this ring's
        /// plane INSIDE the mouth? Direction-agnostic: a gate threaded backwards is still
        /// threaded, which is the honest reading for a race - you still had to fly there.
        /// </summary>
        public bool CrossedMouth(Vector3 prev, Vector3 cur)
        {
            if (_retired) return false;

            Vector3 c = transform.position;
            float dPrev = Vector3.Dot(prev - c, Axis);
            float dCur = Vector3.Dot(cur - c, Axis);
            if (dPrev * dCur > 0f) return false;              // same side of the plane
            if (Mathf.Approximately(dPrev, dCur)) return false;

            float t = Mathf.Clamp01(dPrev / (dPrev - dCur));
            Vector3 hit = Vector3.Lerp(prev, cur, t);
            Vector3 rel = hit - c;
            Vector3 lateral = rel - Vector3.Dot(rel, Axis) * Axis;
            return lateral.sqrMagnitude <= Radius * Radius;
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
}
