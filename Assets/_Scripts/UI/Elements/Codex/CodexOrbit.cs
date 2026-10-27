using System.Collections.Generic;
using UnityEngine;

namespace CosmicShore.UI
{
    /// <summary>
    /// The Codex as an orbit (Docs/MENU_REDESIGN_B.md section 91; the approved Rev 30 board C, Shombith 2026-10-27: "Go
    /// with C, implement it"): a chapter's entries ride tilted rings around the core, the picked entry's picture.
    ///
    /// <para>The rings fill from the inside out (<see cref="ringCapacity"/>), each entry spaced evenly round its ring. A
    /// ring is a flat ellipse seen from above, so an entry's depth is how far round to the front it is: the front draws
    /// larger, brighter and on top, the back smaller and dimmer. Picking an entry turns its ring so the entry swings round
    /// to the front - the selection's motion - and the ring settles there. Nothing moves while nothing is turning.</para>
    ///
    /// <para>It only places what it is handed (<see cref="Arrange"/>, <see cref="Focus"/>): <see cref="CodexScreen"/>
    /// owns the entries and the pick.</para>
    /// </summary>
    [AddComponentMenu("Cosmic Shore/UI/Codex Orbit")]
    public class CodexOrbit : MonoBehaviour
    {
        [Tooltip("The orbit's centre: the rings and the entries are placed around it (the core's picture sits here).")]
        [SerializeField] private RectTransform center;
        [Tooltip("The core's picture: a sibling of the entries, drawn between the back of the rings and the front, so an entry " +
                 "passing in front of it covers it and one passing behind is covered. Optional.")]
        [SerializeField] private RectTransform core;
        [Tooltip("The drawn rings, inside first: each is a thin circle stretched to its ring's ellipse, hidden while empty.")]
        [SerializeField] private RectTransform[] rings = { };
        [Tooltip("How many entries each ring holds, inside first. Entries past the last ring share the last ring.")]
        [SerializeField] private int[] ringCapacity = { 4, 8, 12 };
        [Tooltip("Each ring's half-width, inside first, in canvas units.")]
        [SerializeField] private float[] ringRadius = { 250f, 400f, 540f };
        [Tooltip("How flat the rings lie: the ellipse's height over its width.")]
        [SerializeField, Range(0.2f, 1f)] private float tilt = 0.5f;

        [Header("Depth")]
        [Tooltip("An entry's scale at the back of its ring and at the front.")]
        [SerializeField] private Vector2 scaleBackFront = new(0.6f, 1.05f);
        [Tooltip("An entry's alpha at the back of its ring and at the front.")]
        [SerializeField] private Vector2 alphaBackFront = new(0.5f, 1f);

        [Header("Motion")]
        [Tooltip("Seconds a ring takes to turn a picked entry round to the front.")]
        [SerializeField, Min(0.05f)] private float turnSeconds = 0.6f;

        // Per entry: its ring and its angle on the ring (radians, before the ring's turn). The front is +90 degrees:
        // canvas y runs up, so the bottom of the ellipse - nearest the player - is -90 in canvas terms.
        readonly List<RectTransform> _nodes = new();
        readonly List<CanvasGroup> _groups = new();
        readonly List<int> _ring = new();
        readonly List<float> _angle = new();
        float[] _turn = new float[0], _from = new float[0], _to = new float[0];
        float _clock = -1f;
        int _count;

        const float Front = -Mathf.PI / 2f;

        /// <summary>Places the first <paramref name="count"/> nodes on the rings and turns the picked one to the front at once.</summary>
        public void Arrange(IReadOnlyList<RectTransform> nodes, int count, int picked)
        {
            _nodes.Clear();
            _groups.Clear();
            _ring.Clear();
            _angle.Clear();
            _count = Mathf.Min(count, nodes.Count);
            int ringCount = Mathf.Max(1, Mathf.Min(rings.Length > 0 ? rings.Length : ringRadius.Length, ringRadius.Length));
            var perRing = Distribute(_count, ringCount);
            int index = 0;
            for (int r = 0; r < ringCount; r++)
                for (int k = 0; k < perRing[r]; k++, index++)
                {
                    _nodes.Add(nodes[index]);
                    _groups.Add(nodes[index] ? nodes[index].GetComponent<CanvasGroup>() : null);
                    _ring.Add(r);
                    // Evenly round the ring; the first entry starts at the front.
                    _angle.Add(Front + 2f * Mathf.PI * k / perRing[r]);
                }
            for (int r = 0; r < rings.Length; r++)
            {
                if (!rings[r]) continue;
                bool used = r < perRing.Length && perRing[r] > 0;
                rings[r].gameObject.SetActive(used);
                float rx = Radius(r);
                rings[r].sizeDelta = new Vector2(2f * rx, 2f * rx * tilt);
                if (center) rings[r].anchoredPosition = center.anchoredPosition;
            }
            _turn = new float[ringCount];
            _from = new float[ringCount];
            _to = new float[ringCount];
            if (picked >= 0 && picked < _count) _turn[_ring[picked]] = _to[_ring[picked]] = Front - _angle[picked];
            _clock = -1f;
            Place();
        }

        /// <summary>Turns the picked entry's ring so the entry swings round to the front.</summary>
        public void Focus(int picked)
        {
            if (picked < 0 || picked >= _count) return;
            int r = _ring[picked];
            for (int i = 0; i < _turn.Length; i++) _from[i] = _to[i] = _turn[i];
            // The shortest way round.
            float want = Front - _angle[picked];
            float delta = Mathf.DeltaAngle(_turn[r] * Mathf.Rad2Deg, want * Mathf.Rad2Deg) * Mathf.Deg2Rad;
            _to[r] = _turn[r] + delta;
            _clock = 0f;
        }

        void Update()
        {
            if (_clock < 0f) return;
            _clock += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(_clock / turnSeconds);
            float e = 1f - (1f - k) * (1f - k) * (1f - k);    // OutCubic
            for (int i = 0; i < _turn.Length; i++) _turn[i] = Mathf.LerpUnclamped(_from[i], _to[i], e);
            Place();
            if (k >= 1f) _clock = -1f;
        }

        void Place()
        {
            Vector2 c = center ? center.anchoredPosition : Vector2.zero;
            for (int i = 0; i < _nodes.Count; i++)
            {
                var node = _nodes[i];
                if (!node) continue;
                int r = _ring[i];
                float a = _angle[i] + _turn[r], rx = Radius(r);
                node.anchoredPosition = c + new Vector2(Mathf.Cos(a) * rx, Mathf.Sin(a) * rx * tilt);
                // Depth: 1 at the front (the bottom of the ellipse), 0 at the back.
                float depth = 0.5f - 0.5f * Mathf.Sin(a);
                node.localScale = Vector3.one * Mathf.Lerp(scaleBackFront.x, scaleBackFront.y, depth);
                if (_groups[i]) _groups[i].alpha = Mathf.Lerp(alphaBackFront.x, alphaBackFront.y, depth);
            }
            // Front over back: draw order follows depth, the core at mid depth (a height of 0).
            _order.Clear();
            for (int i = 0; i < _nodes.Count; i++) _order.Add(i);
            if (core) _order.Add(-1);
            _order.Sort(_byHeight ??= (x, y) => Height(y).CompareTo(Height(x)));
            for (int i = 0; i < _order.Count; i++)
            {
                var t = _order[i] < 0 ? core : _nodes[_order[i]];
                if (t) t.SetSiblingIndex(i);
            }
        }

        readonly List<int> _order = new();
        System.Comparison<int> _byHeight;

        // How far up the ellipse an entry sits: 1 at the back, -1 at the front, 0 for the core.
        float Height(int i) => i < 0 ? 0f : Mathf.Sin(_angle[i] + _turn[_ring[i]]);

        float Radius(int ring) => ring < ringRadius.Length ? ringRadius[ring] : ringRadius[ringRadius.Length - 1];

        /// <summary>How many entries each ring takes: inside first, each up to its capacity; the last ring takes the rest.</summary>
        public int[] Distribute(int count, int ringCount)
        {
            var per = new int[ringCount];
            int left = count;
            for (int r = 0; r < ringCount && left > 0; r++)
            {
                int cap = r < ringCapacity.Length ? ringCapacity[r] : int.MaxValue;
                if (r == ringCount - 1) cap = int.MaxValue;
                per[r] = Mathf.Min(cap, left);
                left -= per[r];
            }
            return per;
        }
    }
}
