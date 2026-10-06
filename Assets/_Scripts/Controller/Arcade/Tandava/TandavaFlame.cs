using CosmicShore.Data;
using CosmicShore.ScriptableObjects;
using CosmicShore.Utility;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// One flame of Tandava's RING OF FIRE (Assets/_Scripts/Controller/Arcade/TANDAVA.md §3.6): a SWITCH ring the pilots put
    /// out by threading it (CLAUDE.md, "Switch"), drawn in the prism shader at the radius its own crossing test uses and
    /// painted <see cref="ToySwitchSignal.Flame"/> - the danger red, "this is a fire: thread it to put it out".
    ///
    /// <para><b>Built on every peer, judged on the server.</b> TandavaController places the ring from the replicated
    /// dance centre and body axes, so every machine draws the same twelve flames; whether a flame is GUARDED (an attendant
    /// pack is over its guard post) and whether it is OUT are the server's, replicated as two masks this class only
    /// displays. A peer tests only the pilots it simulates and reports a threaded flame; the server re-checks before it
    /// counts it.</para>
    ///
    /// <para><b>A guarded flame gutters, it does not recolour.</b> While an attendant pack guards it the crossing test is
    /// off, so the drawn ring shrinks to <see cref="Gutter"/> of its mouth - a ring never advertises a mouth that is not
    /// open (the switch law in its strict direction: drawn smaller than its trigger is legal, drawn larger is the lie).
    /// The pack sitting over it is the rest of the message.</para>
    ///
    /// <para><b>A marker, not mass.</b> One renderer, no collider, nothing eaten; it comes down by withering, never by
    /// popping (the continuity law applies to a marker as it does to a prism).</para>
    /// </summary>
    public class TandavaFlame : MonoBehaviour
    {
        /// <summary>The flame's place in the ring (its bit in the replicated masks).</summary>
        public int Index { get; private set; }

        /// <summary>Unit normal of the flame's plane: the ring's TANGENT, so a pilot circling the dancer threads flame
        /// after flame - against the attendants patrolling the same circle, two packs each way.</summary>
        public Vector3 Axis { get; private set; } = Vector3.forward;

        /// <summary>Mouth radius. The drawn ring (lit) and the crossing test share it, by construction.</summary>
        public float Radius { get; private set; } = 1f;

        public bool IsOut { get; private set; }
        public bool IsGuarded { get; private set; }

        /// <summary>The share of its mouth a guarded flame is drawn at.</summary>
        public float Gutter { get; private set; } = 0.35f;

        Transform _gutter;
        float _drawn = 1f;

        /// <summary>Light the flame. Call immediately after AddComponent.</summary>
        public void Build(int index, Vector3 position, Vector3 axis, Vector3 upHint, float radius, float gutter,
                          ThemeManagerDataContainerSO theme, float bloomSeconds)
        {
            Index = index;
            Axis = axis.sqrMagnitude > 1e-6f ? axis.normalized : Vector3.forward;
            Radius = Mathf.Max(1f, radius);
            Gutter = Mathf.Clamp(gutter, 0.05f, 1f);

            transform.position = position;
            transform.localScale = Vector3.one;
            Vector3 up = Vector3.ProjectOnPlane(upHint, Axis);
            if (up.sqrMagnitude < 1e-4f) up = Vector3.ProjectOnPlane(Vector3.right, Axis);
            if (SafeLookRotation.TryGet(Axis, up.normalized, out var rot, this)) transform.rotation = rot;

            // two holders: the bloom scales the outer one, the guard gutters the inner one, so neither fights the other
            var bloom = new GameObject("Visual").transform;
            bloom.SetParent(transform, false);
            _gutter = new GameObject("Gutter").transform;
            _gutter.SetParent(bloom, false);
            ToyFactory.AddSwitchRing(_gutter, Radius, theme, ToySwitchSignal.Flame, Domains.Blue);
            if (bloomSeconds > 0f) ToyFactory.ScaleInFromZero(bloom, bloomSeconds).Forget();
        }

        /// <summary>The server's guard state for this flame (every peer, from the replicated mask). Idempotent.</summary>
        public void SetGuarded(bool guarded) => IsGuarded = guarded;

        void Update()
        {
            if (IsOut || !_gutter) return;
            float target = IsGuarded ? Gutter : 1f;
            if (Mathf.Approximately(_drawn, target)) return;
            _drawn = Mathf.MoveTowards(_drawn, target, Time.deltaTime * 3f);
            _gutter.localScale = Vector3.one * _drawn;
        }

        /// <summary>
        /// Did the segment <paramref name="prev"/> to <paramref name="cur"/> cross this flame's plane INSIDE the mouth?
        /// Direction-agnostic, and false while it is out or guarded. The same plane-crossing math as
        /// <see cref="RaceGateRing.CrossedMouth"/>: a fast hull covers more than a mouth per physics tick, so a trigger
        /// volume can be flown through between samples while a swept segment cannot be missed.
        /// </summary>
        public bool CrossedMouth(Vector3 prev, Vector3 cur)
        {
            if (IsOut || IsGuarded) return false;
            Vector3 c = transform.position;
            float dPrev = Vector3.Dot(prev - c, Axis);
            float dCur = Vector3.Dot(cur - c, Axis);
            if (dPrev * dCur > 0f) return false;
            if (Mathf.Approximately(dPrev, dCur)) return false;
            float t = Mathf.Clamp01(dPrev / (dPrev - dCur));
            Vector3 rel = Vector3.Lerp(prev, cur, t) - c;
            Vector3 lateral = rel - Vector3.Dot(rel, Axis) * Axis;
            return lateral.sqrMagnitude <= Radius * Radius;
        }

        /// <summary>Put the flame out (or strike the ring): it stops answering crossings at once and withers away.</summary>
        public void PutOut(float seconds)
        {
            if (IsOut) return;
            IsOut = true;
            ToyFactory.ScaleOutAndDestroy(gameObject, Mathf.Max(0.01f, seconds)).Forget();
        }
    }
}
