using CosmicShore.Data;
using CosmicShore.ScriptableObjects;
using CosmicShore.Utility;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// One ring of the Lord of the Dance's HALO (Assets/_Scripts/Controller/Arcade/TANDAVA.md §3.6): a SWITCH ring the
    /// pilots break by threading it (CLAUDE.md, "Switch"), drawn in the prism shader at the radius its own crossing test
    /// uses and painted <see cref="ToySwitchSignal.Halo"/> - the pearl, "this is a halo ring: thread it to break it".
    ///
    /// <para><b>Built on every peer, judged on the server.</b> TandavaController places the halo from the replicated
    /// dance centre and body axes, so every machine draws the same twelve rings; whether a ring is GUARDED (an attendant
    /// pack is over its post) and whether it is BROKEN are the server's, replicated as two masks this class only displays.
    /// A peer tests only the pilots it simulates and reports a threaded ring; the server re-checks before it counts.</para>
    ///
    /// <para><b>A guarded ring dims, it does not recolour.</b> While an attendant pack guards it the crossing test is off,
    /// so the drawn ring shrinks to <see cref="Guard"/> of its mouth - a ring never advertises a mouth that is not open
    /// (the switch law in its strict direction: drawn smaller than its trigger is legal, drawn larger is the lie). The pack
    /// sitting over it is the rest of the message.</para>
    ///
    /// <para><b>A marker, not mass.</b> One renderer, no collider, nothing eaten; it comes down by withering, never by
    /// popping (the continuity law applies to a marker as it does to a prism). Its BREAK is the gold burst the controller
    /// throws where it stood.</para>
    /// </summary>
    public class TandavaHaloRing : MonoBehaviour
    {
        /// <summary>The ring's place in the halo (its bit in the replicated masks).</summary>
        public int Index { get; private set; }

        /// <summary>Unit normal of the ring's plane: the halo's TANGENT, so a pilot circling the dancer threads ring after
        /// ring - against the attendants patrolling the same circle, two packs each way.</summary>
        public Vector3 Axis { get; private set; } = Vector3.forward;

        /// <summary>Mouth radius. The drawn ring (open) and the crossing test share it, by construction.</summary>
        public float Radius { get; private set; } = 1f;

        public bool IsBroken { get; private set; }
        public bool IsGuarded { get; private set; }

        /// <summary>The share of its mouth a guarded ring is drawn at.</summary>
        public float Guard { get; private set; } = 0.35f;

        Transform _inner;
        float _drawn = 1f;

        /// <summary>Raise the ring. Call immediately after AddComponent.</summary>
        public void Build(int index, Vector3 position, Vector3 axis, Vector3 upHint, float radius, float guard,
                          ThemeManagerDataContainerSO theme, float bloomSeconds)
        {
            Index = index;
            Axis = axis.sqrMagnitude > 1e-6f ? axis.normalized : Vector3.forward;
            Radius = Mathf.Max(1f, radius);
            Guard = Mathf.Clamp(guard, 0.05f, 1f);

            transform.position = position;
            transform.localScale = Vector3.one;
            Vector3 up = Vector3.ProjectOnPlane(upHint, Axis);
            if (up.sqrMagnitude < 1e-4f) up = Vector3.ProjectOnPlane(Vector3.right, Axis);
            if (SafeLookRotation.TryGet(Axis, up.normalized, out var rot, this)) transform.rotation = rot;

            // two holders: the bloom scales the outer one, the guard dims the inner one, so neither fights the other
            var bloom = new GameObject("Visual").transform;
            bloom.SetParent(transform, false);
            _inner = new GameObject("Guard").transform;
            _inner.SetParent(bloom, false);
            ToyFactory.AddSwitchRing(_inner, Radius, theme, ToySwitchSignal.Halo, Domains.Blue);
            if (bloomSeconds > 0f) ToyFactory.ScaleInFromZero(bloom, bloomSeconds).Forget();
        }

        /// <summary>The server's guard state for this ring (every peer, from the replicated mask). Idempotent.</summary>
        public void SetGuarded(bool guarded) => IsGuarded = guarded;

        void Update()
        {
            if (IsBroken || !_inner) return;
            float target = IsGuarded ? Guard : 1f;
            if (Mathf.Approximately(_drawn, target)) return;
            _drawn = Mathf.MoveTowards(_drawn, target, Time.deltaTime * 3f);
            _inner.localScale = Vector3.one * _drawn;
        }

        /// <summary>
        /// Did the segment <paramref name="prev"/> to <paramref name="cur"/> cross this ring's plane INSIDE the mouth?
        /// Direction-agnostic, and false while it is broken or guarded. The same plane-crossing math as
        /// <see cref="RaceGateRing.CrossedMouth"/>: a fast hull covers more than a mouth per physics tick, so a trigger
        /// volume can be flown through between samples while a swept segment cannot be missed.
        /// </summary>
        public bool CrossedMouth(Vector3 prev, Vector3 cur)
        {
            if (IsBroken || IsGuarded) return false;
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

        /// <summary>Break the ring (or strike the halo): it stops answering crossings at once and withers away.</summary>
        public void Break(float seconds)
        {
            if (IsBroken) return;
            IsBroken = true;
            ToyFactory.ScaleOutAndDestroy(gameObject, Mathf.Max(0.01f, seconds)).Forget();
        }
    }
}
