using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The celestial bee's vanes beat (the CPU-driver motion tier, the /fauna skill §3): each vane is a braid spindle
    /// carrying a plate, a <see cref="HealthPrism"/> no vertex shader can move, so this writes only the vane spindle's
    /// <c>localRotation</c> (never a position): the vane opens and closes about its root, away from and back toward the
    /// spine, and its plate rides it with zero collider or index work of its own (the body's movers contract carries it).
    ///
    /// <para>The body is three-fold round its travel axis with no up (the /fauna-creation skill, law L6), so the beat is
    /// too: every vane strokes about its own root's tangent, the n vanes a whole beat apart by 360/n degrees of phase, a
    /// wave that runs round the spine. The vanes are found by name (<see cref="vanes"/>, the spec's names,
    /// Tools/Build/fauna_kit/specs/bee.py), and a per-bee phase keeps a swarm from beating in lockstep.</para>
    /// </summary>
    [DefaultExecutionOrder(-1)]
    public class BeeVaneDriver : MonoBehaviour
    {
        [Tooltip("Names of the vane spindles (children of the bee, at any depth), in order round the spine.")]
        [SerializeField] string[] vanes = { "Vane0", "Vane1", "Vane2" };
        [Tooltip("Beats per second: the read of a blur, not a real rate.")]
        [SerializeField, Min(0f)] float beatsPerSecond = 9f;
        [Tooltip("Half the stroke, in degrees, about each vane's root (away from and back toward the spine).")]
        [SerializeField, Range(0f, 60f)] float strokeDegrees = 24f;

        Transform[] _vanes;
        Quaternion[] _rest;
        float _phase;

        void Awake()
        {
            int n = vanes?.Length ?? 0;
            _vanes = new Transform[n];
            _rest = new Quaternion[n];
            for (int i = 0; i < n; i++)
            {
                _vanes[i] = Find(transform, vanes[i]);
                if (_vanes[i]) _rest[i] = _vanes[i].localRotation;
            }
            _phase = (GetInstanceID() & 1023) * 0.0061f;
        }

        void Update()
        {
            int n = _vanes.Length;
            if (n == 0) return;
            float t = (Time.time * beatsPerSecond + _phase) * 6.2831853f;
            for (int i = 0; i < n; i++)
            {
                if (!_vanes[i]) continue;
                // about the vane's own local x: its frame's up is radial (fauna_kit oriented), so x is the root's tangent
                float a = strokeDegrees * Mathf.Sin(t + 6.2831853f * i / n);
                _vanes[i].localRotation = _rest[i] * Quaternion.Euler(a, 0f, 0f);
            }
        }

        static Transform Find(Transform root, string name)
        {
            if (string.IsNullOrEmpty(name)) return null;
            for (int i = 0; i < root.childCount; i++)
            {
                var c = root.GetChild(i);
                if (c.name == name) return c;
                var deeper = Find(c, name);
                if (deeper) return deeper;
            }
            return null;
        }
    }
}
