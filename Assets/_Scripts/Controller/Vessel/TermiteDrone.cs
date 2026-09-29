using System;
using System.Collections.Generic;
using CosmicShore.Data;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>Tuning shared by every drone a queen releases. Lives on
    /// <see cref="TermiteDeckExecutor"/> and is handed to each drone at birth.</summary>
    [Serializable]
    public class TermiteDroneTuning
    {
        [Tooltip("Cruise speed, world u/s. Under the queen's commanded 70 so her escort trails her.")]
        [Min(1f)] public float speed = 55f;
        [Tooltip("How quickly a drone turns onto its heading (per second, exponential).")]
        [Min(0.1f)] public float turnSharpness = 6f;
        [Tooltip("How far from its HOME (the queen, or its mound) a drone looks for mass, world units.")]
        [Min(1f)] public float forageRadius = 70f;
        [Tooltip("Radius of the idle orbit around home while there is nothing to do.")]
        [Min(1f)] public float escortRadius = 18f;
        [Tooltip("How close a drone must get to act on a prism, beyond the prism's own size.")]
        [Min(0.1f)] public float reach = 3.5f;
        [Tooltip("Seconds between a drone's searches for a new target (staggered per drone).")]
        [Min(0.05f)] public float retargetInterval = 0.5f;
        [Tooltip("Prisms a WORKER carries before it flies home to build.")]
        [Min(1)] public int carryCapacity = 3;
        [Tooltip("World size of a drone's body.")]
        [Min(0.1f)] public float bodyScale = 2.4f;
        [Tooltip("Seconds a drone takes to bloom in, and to wither out.")]
        [Min(0.05f)] public float bloomSeconds = 0.45f;
    }

    /// <summary>
    /// One of the Termite queen's drones — a small vessel-owned AGENT, not a lifeform. Design
    /// record: <c>R_VesselActions/TERMITE.md</c> §5.3.
    ///
    /// <para><b>Deliberately NOT a <see cref="Fauna"/>.</b> Every lifeform drops exactly one
    /// elemental crystal and lifeform spawning is the elemental economy's only SOURCE, so a card
    /// that spawned lifeforms would mint petals on demand. A drone is the queen's hand reaching
    /// out — the same class of thing as a Sparrow's round or an Urchin's spike: it acts on mass
    /// with the queen's name and domain and is credited to her exactly as her own abilities are.
    /// It carries no heart, no body prisms and <b>no collider</b>, so it costs nothing in the
    /// collider budget and cannot be grazed or shot; its limit is the executor's live cap.</para>
    ///
    /// <list type="bullet">
    /// <item><b>Soldier</b> (Queen Drones): escorts the queen and DESTROYS opposing mass within
    /// reach of her — <see cref="Prism.Damage"/>, so a shield is shed first and a super-shield
    /// deflects, exactly as it would under any other active force.</item>
    /// <item><b>Worker</b> (Mound Drones): lives at a mound, GRAZES opposing mass
    /// (<see cref="Prism.Consume"/> — the suction death, toward the worker's mouth) and carries it
    /// home, where the mound lays it back down as the queen's own mass. Consumption and deposit
    /// are one prism for one prism: the worker MOVES mass between domains, it never mints it. A
    /// worker whose mound is full idles rather than eat mass it cannot store.</item>
    /// </list>
    ///
    /// <para><b>Simulated on every peer</b>, like the client-local fauna: the press that
    /// released it is replayed everywhere and each machine flies its own copy. Two machines'
    /// drones can therefore pick different targets — the same divergence the food web already
    /// accepts (TERMITE.md §8).</para>
    ///
    /// <para>Continuity of existence: a drone blooms in from zero and withers out to zero; it is
    /// never simply shown or destroyed.</para>
    /// </summary>
    public sealed class TermiteDrone : MonoBehaviour
    {
        public enum Role { Soldier = 1, Worker = 2 }

        TermiteDeckExecutor _deck;
        TermiteDroneTuning _tuning;
        Role _role;
        Domains _domain;
        string _playerName;
        Transform _queen;
        TermiteMound _mound;

        Prism _target;
        int _carried;
        bool _homeward;
        float _nextSearch;
        float _orbitPhase;
        Vector3 _heading = Vector3.forward;

        float _life;          // 0 -> 1 bloom
        bool _retiring;
        float _retireT = 1f;

        static readonly List<Prism> s_scratch = new();

        public Role DroneRole => _role;
        public TermiteMound Mound => _mound;
        public bool IsRetiring => _retiring;
        public int Carried => _carried;

        /// <summary>Called once by the executor that made this drone.</summary>
        public void Initialize(TermiteDeckExecutor deck, TermiteDroneTuning tuning, Role role,
                               Domains domain, string playerName, Transform queen, TermiteMound mound,
                               float phase)
        {
            _deck = deck;
            _tuning = tuning;
            _role = role;
            _domain = domain;
            _playerName = playerName;
            _queen = queen;
            _mound = mound;
            _orbitPhase = phase;
            _nextSearch = Time.time + phase % Mathf.Max(0.05f, tuning.retargetInterval);
            _heading = queen ? queen.forward : Vector3.forward;
            transform.localScale = Vector3.zero;
        }

        /// <summary>A worker whose mound fell moves to another, or becomes an escort.</summary>
        public void Rehome(TermiteMound mound)
        {
            _mound = mound;
            if (!mound) _role = Role.Soldier;
            _target = null;
        }

        /// <summary>Wither out and go away. Idempotent.</summary>
        public void Retire()
        {
            if (_retiring) return;
            _retiring = true;
            _retireT = Mathf.Clamp01(_life);
            _target = null;
        }

        Vector3 Home
        {
            get
            {
                if (_role == Role.Worker && _mound) return _mound.Entrance;
                return _queen ? _queen.position : transform.position;
            }
        }

        void Update()
        {
            if (_tuning == null) return;
            float dt = Time.deltaTime;
            float bloom = Mathf.Max(0.05f, _tuning.bloomSeconds);

            if (_retiring)
            {
                _retireT -= dt / bloom;
                ApplyScale(Mathf.Max(0f, _retireT));
                if (_retireT <= 0f)
                {
                    _deck?.NotifyDroneGone(this);
                    Destroy(gameObject);
                }
                return;
            }

            if (_life < 1f)
            {
                _life = Mathf.Min(1f, _life + dt / bloom);
                ApplyScale(_life);
            }

            if (!_queen && !_mound) { Retire(); return; }

            Think();
            Fly(dt);
        }

        void ApplyScale(float t)
        {
            // Smoothstep so the bloom starts and lands softly.
            float s = t * t * (3f - 2f * t);
            transform.localScale = Vector3.one * (_tuning.bodyScale * s);
        }

        // ------------------------------------------------------------------ behaviour

        void Think()
        {
            if (_target && (_target.destroyed || !IsPrey(_target))) { _deck?.Release(_target, this); _target = null; }

            if (_role == Role.Worker)
            {
                // A worker goes home when it is full, when there is nothing left to graze, or when
                // its mound cannot take another prism — and it never eats what it cannot store.
                bool mustStore = _carried >= _tuning.carryCapacity
                                 || (_carried > 0 && _mound && !_mound.HasRoomFor(_carried + 1));
                if (!mustStore && _carried > 0 && !_target && Time.time >= _nextSearch && !FindTarget())
                    mustStore = true;

                if (mustStore)
                {
                    if (_target) { _deck?.Release(_target, this); _target = null; }
                    _homeward = true;
                    if (_mound && (transform.position - _mound.Entrance).sqrMagnitude <= Reach2(_mound.EntranceRadius))
                    {
                        _mound.Deposit(_carried);
                        _carried = 0;
                        _homeward = false;
                    }
                    return;
                }
                _homeward = false;
                if (_mound && !_mound.HasRoomFor(1)) return;   // full mound: idle at home
            }

            if (!_target && Time.time >= _nextSearch) FindTarget();

            if (_target)
            {
                float reach = _tuning.reach + PrismRadius(_target);
                if ((_target.transform.position - transform.position).sqrMagnitude <= reach * reach)
                    Act(_target);
            }
        }

        bool FindTarget()
        {
            _nextSearch = Time.time + _tuning.retargetInterval;
            var index = PrismSpatialIndex.Instance;
            if (!index) return false;

            Vector3 home = Home;
            index.QuerySphere(home, _tuning.forageRadius, s_scratch);

            Prism best = null;
            float bestD = float.PositiveInfinity;
            Vector3 me = transform.position;
            for (int i = 0; i < s_scratch.Count; i++)
            {
                var p = s_scratch[i];
                if (!p || p.destroyed || !IsPrey(p)) continue;
                if (_deck && _deck.IsClaimedByAnother(p, this)) continue;
                float d = (p.transform.position - me).sqrMagnitude;
                if (d < bestD) { bestD = d; best = p; }
            }
            s_scratch.Clear();

            _target = best;
            if (best && _deck) _deck.Claim(best, this);
            return best;
        }

        /// <summary>
        /// Opposing mass, never shielded mass: <see cref="Prism.Consume"/> only sheds a shield and
        /// is a no-op on a super-shield, so a drone led to armour would hold forever — the fauna's
        /// rule ("never led to mass it cannot eat") applied to the queen's hands. Blue (neutral
        /// environment) counts as opposing, as it does for every weapon.
        /// </summary>
        bool IsPrey(Prism p)
        {
            if (p.Domain == _domain) return false;
            var props = p.prismProperties;
            if (props != null && (props.IsShielded || props.IsSuperShielded)) return false;
            return true;
        }

        void Act(Prism p)
        {
            _deck?.Release(p, this);
            _target = null;
            if (_role == Role.Soldier)
            {
                Vector3 impact = (p.transform.position - transform.position).normalized * _tuning.speed;
                p.Damage(impact, _domain, _playerName);
            }
            else
            {
                p.Consume(transform, _domain, _playerName);
                _carried++;
            }
        }

        void Fly(float dt)
        {
            Vector3 goal;
            if (_target) goal = _target.transform.position;
            else if (_homeward && _mound) goal = _mound.Entrance;
            else
            {
                // Idle: a slow orbit around home, each drone on its own phase so an escort reads
                // as a swarm rather than a single file.
                _orbitPhase += dt * 0.9f;
                Vector3 axis = _queen ? _queen.up : Vector3.up;
                Vector3 radial = Vector3.Cross(axis, Quaternion.AngleAxis(_orbitPhase * Mathf.Rad2Deg, axis) * Vector3.forward);
                if (radial.sqrMagnitude < 1e-4f) radial = Vector3.right;
                goal = Home + radial.normalized * _tuning.escortRadius
                            + axis * (Mathf.Sin(_orbitPhase * 1.7f) * _tuning.escortRadius * 0.3f);
            }

            Vector3 to = goal - transform.position;
            float dist = to.magnitude;
            if (dist > 1e-3f)
            {
                Vector3 want = to / dist;
                float t = 1f - Mathf.Exp(-_tuning.turnSharpness * dt);
                _heading = Vector3.Slerp(_heading, want, t);
                if (_heading.sqrMagnitude < 1e-6f) _heading = want;
                _heading.Normalize();
            }

            // Ease into an idle point rather than overshooting and circling it.
            float speed = _target ? _tuning.speed : Mathf.Min(_tuning.speed, dist * 2.5f);
            transform.position += _heading * (speed * dt);
            if (_heading.sqrMagnitude > 1e-6f) transform.rotation = Quaternion.LookRotation(_heading, Vector3.up);
        }

        float Reach2(float extra)
        {
            float r = _tuning.reach + extra;
            return r * r;
        }

        static float PrismRadius(Prism p)
        {
            Vector3 s = p.transform.lossyScale;
            return 0.5f * Mathf.Max(s.x, Mathf.Max(s.y, s.z));
        }

        void OnDestroy() => _deck?.NotifyDroneGone(this);

        // ------------------------------------------------------------------ the body

        static Mesh s_mesh;

        /// <summary>
        /// One shared low-poly worker body (head, thorax, abdomen along +z), built once. Shared so
        /// every drone of every queen batches as one mesh.
        /// </summary>
        public static Mesh SharedMesh()
        {
            if (s_mesh) return s_mesh;
            var verts = new List<Vector3>();
            var normals = new List<Vector3>();
            var tris = new List<int>();
            AddEllipsoid(verts, normals, tris, new Vector3(0f, 0f, 0.42f), new Vector3(0.20f, 0.18f, 0.20f));  // head
            AddEllipsoid(verts, normals, tris, new Vector3(0f, 0f, 0.12f), new Vector3(0.16f, 0.15f, 0.18f));  // thorax
            AddEllipsoid(verts, normals, tris, new Vector3(0f, 0f, -0.30f), new Vector3(0.24f, 0.22f, 0.34f)); // abdomen
            s_mesh = new Mesh { name = "TermiteDrone" };
            s_mesh.SetVertices(verts);
            s_mesh.SetNormals(normals);
            s_mesh.SetTriangles(tris, 0);
            s_mesh.RecalculateBounds();
            return s_mesh;
        }

        static void AddEllipsoid(List<Vector3> v, List<Vector3> n, List<int> t, Vector3 c, Vector3 r)
        {
            const int rings = 5, sides = 8;
            int start = v.Count;
            for (int i = 0; i <= rings; i++)
            {
                float phi = Mathf.PI * i / rings;
                for (int j = 0; j <= sides; j++)
                {
                    float th = 2f * Mathf.PI * j / sides;
                    var unit = new Vector3(Mathf.Sin(phi) * Mathf.Cos(th), Mathf.Sin(phi) * Mathf.Sin(th), Mathf.Cos(phi));
                    v.Add(c + Vector3.Scale(unit, r));
                    n.Add(new Vector3(unit.x / r.x, unit.y / r.y, unit.z / r.z).normalized);
                }
            }
            for (int i = 0; i < rings; i++)
                for (int j = 0; j < sides; j++)
                {
                    int a = start + i * (sides + 1) + j, b = a + sides + 1;
                    // Unity's front face is the one whose Cross(v1 - v0, v2 - v0) faces the viewer;
                    // with phi running from the +z pole this order points every face OUT.
                    t.Add(a); t.Add(b); t.Add(a + 1);
                    t.Add(a + 1); t.Add(b); t.Add(b + 1);
                }
        }
    }
}
