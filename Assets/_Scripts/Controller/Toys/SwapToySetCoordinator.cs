using System.Collections.Generic;
using CosmicShore.ScriptableObjects;
using TMPro;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Manages a <b>set</b> of toys that together represent "the options you are not currently on".
    /// Each toy targets one option (a domain, a vessel) and is visually that option; flying through
    /// it applies the change. The set always shows <c>universe \ {current}</c>, and - the key
    /// behaviour the prompter asked for - <b>the toy you use flips to the option you just left</b>,
    /// so the set continuously mirrors "everything except where you are now".
    ///
    /// Used by <see cref="DomainChangerToySet"/> (Jade/Ruby/Gold), whose universe is small enough
    /// that showing it laid out around you beats unfolding it. Toys with a bigger universe (the
    /// vessel changer, the painting gallery, the cell selector) are <see cref="MatrixToy"/>s
    /// instead: one station that opens into its options. Current state is polled each frame, so
    /// external changes (a panel, a menu reset) reconcile the same way a toy activation does.
    /// </summary>
    /// <typeparam name="T">The option type - <c>Domains</c>.</typeparam>
    public abstract class SwapToySetCoordinator<T> : MonoBehaviour
    {
        protected ToyContext Context { get; private set; }
        protected ToyDefinitionSO Definition { get; private set; }
        protected float BodyRadius { get; private set; }
        protected float TriggerRadius { get; private set; }

        [SerializeField, Tooltip("Angular gap (degrees) between adjacent toys in the set, around the membrane.")]
        float anglePerToyDeg = 14f;

        /// <summary>
        /// Whether this set's toys wear the shared SWITCH ring. True for a set of anonymous
        /// stations; false for one whose bodies already carry the "fly through me" read on their
        /// own - the domain changer's cones, whose apex points the way you go through, and which
        /// are rebuilt on every flip.
        /// </summary>
        protected virtual bool SlotsWearSwitchRing => true;

        static readonly EqualityComparer<T> Eq = EqualityComparer<T>.Default;

        readonly List<Slot> _slots = new();
        readonly List<T> _universe = new();
        Vector3 _center;
        float _radius;
        float _baseAngleRad;
        bool _hasCurrent;
        T _current;
        bool _initialized;

        protected sealed class Slot
        {
            public SwapToy Toy;
            public Transform BodyHolder;
            public TMP_Text Label;
            public T Option;
            internal bool Keep;
        }

        public void Initialize(ToyDefinitionSO definition, ToyContext context, ToyPlacement placement,
            float bodyRadius, float triggerRadius)
        {
            Definition = definition;
            Context = context;
            BodyRadius = bodyRadius;
            TriggerRadius = triggerRadius;

            _center = placement.LookTarget;
            Vector3 toCenter = placement.Position - _center;
            _radius = new Vector2(toCenter.x, toCenter.z).magnitude;
            if (_radius < 1f) _radius = Mathf.Max(1f, toCenter.magnitude);
            _baseAngleRad = Mathf.Atan2(toCenter.x, toCenter.z);

            _universe.Clear();
            foreach (var o in InitialUniverse())
                if (IsValid(o) && !_universe.Contains(o)) _universe.Add(o);

            _initialized = true;
        }

        void Update()
        {
            if (!_initialized) return;

            OnTick();

            if (!TryGetCurrent(out var cur) || !IsValid(cur)) return;

            if (!_hasCurrent)
            {
                _hasCurrent = true;
                _current = cur;
                EnsureInUniverse(cur);
                Reconcile(cur, cur);
                return;
            }

            if (!Eq.Equals(cur, _current))
            {
                var prev = _current;
                _current = cur;
                EnsureInUniverse(prev);
                EnsureInUniverse(cur);
                Reconcile(prev, cur);
            }
        }

        void EnsureInUniverse(T option)
        {
            if (IsValid(option) && !_universe.Contains(option)) _universe.Add(option);
        }

        void Reconcile(T prev, T cur)
        {
            // Desired = every known option except the current one, in universe order.
            var desired = new List<T>();
            foreach (var o in _universe)
                if (!Eq.Equals(o, cur)) desired.Add(o);

            // Pass 1: keep slots already showing a desired option (dedup).
            var covered = new HashSet<T>(Eq);
            foreach (var s in _slots)
            {
                s.Keep = !Eq.Equals(s.Option, cur) && desired.Contains(s.Option) && !covered.Contains(s.Option);
                if (s.Keep) covered.Add(s.Option);
            }

            // Options still needing a slot - offer `prev` first so the used toy flips to it.
            var remaining = new List<T>();
            if (desired.Contains(prev) && !covered.Contains(prev)) remaining.Add(prev);
            foreach (var d in desired)
                if (!covered.Contains(d) && !Eq.Equals(d, prev)) remaining.Add(d);

            // Reassign not-kept slots, then create/destroy to match the desired count.
            int ri = 0;
            for (int i = _slots.Count - 1; i >= 0; i--)
            {
                var s = _slots[i];
                if (s.Keep) continue;
                if (ri < remaining.Count)
                {
                    s.Option = remaining[ri++];
                    ConfigureVisual(s);
                    s.Toy.Rebloom();
                }
                else
                {
                    if (s.Toy) Destroy(s.Toy.gameObject);
                    _slots.RemoveAt(i);
                }
            }
            while (ri < remaining.Count)
                _slots.Add(CreateSlot(remaining[ri++]));

            Layout();
        }

        Slot CreateSlot(T option)
        {
            var root = ToyFactory.CreateBareRoot($"{Definition.Id}_{option}", transform, transform.position, _center, TriggerRadius);

            var bodyHolder = new GameObject("Body").transform;
            bodyHolder.SetParent(root.transform, false);

            var label = ToyFactory.AddLabel(root.transform, LabelFor(option), Color.white, BodyRadius * 1.9f);

            var toy = root.AddComponent<SwapToy>();
            if (!SlotsWearSwitchRing) toy.ConfigureSwitchRing(0f);
            var slot = new Slot { Toy = toy, BodyHolder = bodyHolder, Label = label, Option = option };

            ConfigureVisual(slot);
            toy.Activated += OnToyActivated;
            toy.Initialize(Definition, Context, default);
            return slot;
        }

        void OnToyActivated(SwapToy toy)
        {
            var slot = _slots.Find(s => s.Toy == toy);
            if (slot == null) return;

            // Disarm EVERY toy in the set the instant one fires. After a vessel swap the new
            // vessel re-spawns right where you flew through (on top of these toys), and the domain
            // change keeps you inside the cluster - so without this the neighbouring toys would
            // chain-trigger and you could never escape. Each toy re-arms only once the vessel has
            // flown clear of it (Toy.Update exit gate).
            foreach (var s in _slots)
                if (s.Toy) s.Toy.Disarm();

            Apply(slot.Option);
            // The domain/vessel change replicates; Update() detects the new current and flips this slot.
        }

        void Layout()
        {
            int n = _slots.Count;
            float step = anglePerToyDeg * Mathf.Deg2Rad;
            for (int i = 0; i < n; i++)
            {
                float a = _baseAngleRad + step * (i - (n - 1) * 0.5f);
                var dir = new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a));
                var pos = _center + dir * _radius;
                var t = _slots[i].Toy.transform;
                t.position = pos;
                Vector3 toC = _center - pos;
                if (toC.sqrMagnitude > 1e-4f) t.rotation = Quaternion.LookRotation(toC.normalized, Vector3.up);
            }
        }

        protected static void ClearChildren(Transform t)
        {
            for (int i = t.childCount - 1; i >= 0; i--)
                Destroy(t.GetChild(i).gameObject);
        }

        /// <summary>
        /// Per-frame hook for reacting to external state that isn't the tracked "current option" -
        /// e.g. the vessel changer recolouring all its mini ships when the player's domain changes,
        /// not just the one slot that flips on a ship swap. Runs before the current-option reconcile.
        /// </summary>
        protected virtual void OnTick() { }

        /// <summary>Invoke an action for every live slot (e.g. to recolour them all in place).</summary>
        protected void ForEachSlot(System.Action<Slot> action)
        {
            foreach (var s in _slots) action(s);
        }

        // ── Abstract per-kind behaviour ─────────────────────────────────────
        protected abstract IReadOnlyList<T> InitialUniverse();
        protected abstract bool TryGetCurrent(out T current);
        protected abstract bool IsValid(T option);
        protected abstract void Apply(T target);
        protected abstract void ConfigureVisual(Slot slot);
        protected virtual string LabelFor(T option) => option.ToString();
    }
}
