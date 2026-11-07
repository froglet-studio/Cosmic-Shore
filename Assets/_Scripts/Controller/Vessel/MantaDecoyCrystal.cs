using System.Collections.Generic;
using CosmicShore.Data;
using CosmicShore.ScriptableObjects;
using CosmicShore.Utility;
using Reflex.Core;
using UnityEngine;
using Object = UnityEngine.Object;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The Manta's MASS ability (MANTA_STING_KABLOOM.md §4a): a crystal the Manta collects leaves a DECOY where it was -
    /// a copy of the real crystal with ONE of its elemental effects altered, the tell a sharp pilot can read. A rival who
    /// takes it gets no crystal: the decoy morphs into a cluster of danger prisms laid across their course.
    ///
    /// <para><b>Which effect is altered.</b> An elemental decoy alters its own element's effect; an omni decoy (Mass 5)
    /// alters one of its four, picked by a stream seeded from where the crystal was collected, so every peer picks the
    /// same one. "Altered" is FROZEN: the effect's driver never starts, so the Charge arcs never strike (the Charge
    /// crystal falls back to its old edge crawl), the Mass prisms stand sealed and solid, the Space squares and the Time
    /// rhombi hold still while the rest of the crystal moves.</para>
    ///
    /// <para><b>Built from the real prefab, stripped.</b> The copy is made under an INACTIVE holder so nothing on it wakes,
    /// painted once with the colours the real crystal wears (<see cref="Crystal.PaintNow"/>), and then every gameplay
    /// part is removed - the <see cref="Crystal"/>, its impactors, colliders, rigidbodies and network components - so a
    /// decoy is never collectable, never in the cell's lists or <see cref="Crystal.Active"/>, and never targeted by
    /// an AI's crystal search. Only its looks are left, and the one trigger this component adds.</para>
    ///
    /// Spawned on every peer (the crystal effect that leaves it is already broadcast), and sprung by each peer's own
    /// physics.
    /// </summary>
    public sealed class MantaDecoyCrystal : MonoBehaviour
    {
        static readonly Element[] Elementals = { Element.Charge, Element.Mass, Element.Space, Element.Time };
        const float FadeSeconds = 0.35f;

        static GameObject s_stash;
        static ElementalCrystalSetSO s_elementalSet;
        static readonly List<Component> s_strip = new();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            s_stash = null;
            s_elementalSet = null;
            s_strip.Clear();
        }

        MantaStingActionExecutor _owner;
        MantaStingConfigSO _config;
        Domains _domain;
        IVessel _vessel;
        Container _container;
        float _expiresAt;
        float _fadeFrom = -1f;
        Vector3 _scale;
        bool _done;

        /// <summary>Which element's effect this decoy wears altered - the tell.</summary>
        public Element AlteredElement { get; private set; }

        /// <summary>
        /// Builds a decoy of the crystal <paramref name="data"/> describes, where it was collected. Null when there is no
        /// prefab to build it from (the omni prefab unwired, the elemental set missing).
        /// </summary>
        public static MantaDecoyCrystal Spawn(MantaStingActionExecutor owner, MantaStingConfigSO config,
                                              in CrystalImpactData data, float lifetimeSeconds)
        {
            if (!owner || !config) return null;
            bool omni = data.Element == Element.Omni;
            var prefab = omni ? config.OmniCrystalPrefab : ElementalPrefab(data.Element);
            if (!prefab)
            {
                CSDebug.LogWarning($"[MantaDecoy] No crystal prefab to build a {data.Element} decoy from - " +
                                   (omni ? "wire omniCrystalPrefab on MantaStingConfig.asset." : "Resources/ElementalCrystalSet is missing it."));
                return null;
            }

            // Inactive in hierarchy from the first instant: no Awake, OnEnable or Start runs on anything until the
            // gameplay parts are gone.
            var go = Instantiate(prefab, Stash().transform, false);
            go.name = prefab.name + " (Manta decoy)";

            float triggerRadius = 1f;
            if (go.TryGetComponent<SphereCollider>(out var sphere)) triggerRadius = sphere.radius;

            // Wear the real crystal's colours before its Crystal goes.
            if (go.TryGetComponent<Crystal>(out var crystal)) crystal.PaintNow();
            foreach (var accent in go.GetComponentsInChildren<CrystalAccentTint>(true)) accent.SyncNow();

            StripGameplay(go);
            // A decoy is simply still THERE where the real one was taken - a crystal blooming in on the spot would be a
            // second tell nobody designed.
            foreach (var fade in go.GetComponentsInChildren<FadeIn>(true)) fade.enabled = false;

            var rng = new System.Random(Seed(data));
            var altered = omni ? Elementals[rng.Next(Elementals.Length)] : data.Element;
            Alter(go, altered);

            var decoy = go.AddComponent<MantaDecoyCrystal>();
            decoy._owner = owner;
            decoy._config = config;
            var status = owner.Status;
            decoy._domain = status != null ? status.Domain : Domains.Blue;
            decoy._vessel = status?.Vessel;
            decoy._container = owner.DiContainer;
            decoy._expiresAt = Time.time + Mathf.Max(1f, lifetimeSeconds);
            decoy.AlteredElement = altered;

            var trigger = go.AddComponent<SphereCollider>();
            trigger.isTrigger = true;
            trigger.radius = Mathf.Max(0.01f, triggerRadius * config.DecoyTriggerRadiusScale);

            // Out of the stash and into the world at the collect pose: this is the moment it wakes.
            var t = go.transform;
            t.SetParent(null, false);
            t.SetPositionAndRotation(data.Origin.Position, data.Origin.Rotation);
            if (data.Origin.Scale != Vector3.zero) t.localScale = data.Origin.Scale;
            decoy._scale = t.localScale;
            return decoy;
        }

        static GameObject ElementalPrefab(Element element)
        {
            if (!s_elementalSet) s_elementalSet = Resources.Load<ElementalCrystalSetSO>(ElementalCrystalSetSO.ResourcePath);
            var crystal = s_elementalSet ? s_elementalSet.GetPrefab(element) : null;
            return crystal ? crystal.gameObject : null;
        }

        static GameObject Stash()
        {
            if (s_stash) return s_stash;
            s_stash = new GameObject("[MantaDecoyStash]") { hideFlags = HideFlags.HideAndDontSave };
            s_stash.SetActive(false);
            DontDestroyOnLoad(s_stash);
            return s_stash;
        }

        /// <summary>The same seed on every peer: the collect pose and crystal id ride the broadcast payload.</summary>
        static int Seed(in CrystalImpactData data)
        {
            var p = data.Origin.Position;
            unchecked
            {
                int h = data.Origin.CrystalId;
                h = h * 397 ^ Mathf.RoundToInt(p.x * 10f);
                h = h * 397 ^ Mathf.RoundToInt(p.y * 10f);
                h = h * 397 ^ Mathf.RoundToInt(p.z * 10f);
                return h;
            }
        }

        /// <summary>
        /// Removes every gameplay part from the copy, dependents first. Only looks are left: renderers, mesh filters,
        /// animators and the crystal's own effect drivers.
        /// </summary>
        static void StripGameplay(GameObject go)
        {
            s_strip.Clear();
            foreach (var c in go.GetComponentsInChildren<Component>(true))
                if (IsGameplay(c)) s_strip.Add(c);

            // Behaviours before the colliders and rigidbodies they may require, network behaviours before the
            // NetworkObject they hang off.
            s_strip.Sort((a, b) => Rank(a).CompareTo(Rank(b)));
            for (int i = 0; i < s_strip.Count; i++)
                if (s_strip[i]) DestroyImmediate(s_strip[i]);
            s_strip.Clear();
        }

        static bool IsGameplay(Component c) =>
            c is CellItem or IImpactor or ImpactCollider or Collider or Rigidbody
                or Unity.Netcode.NetworkBehaviour or Unity.Netcode.NetworkObject;

        static int Rank(Component c) => c switch
        {
            Unity.Netcode.NetworkObject => 3,
            Collider or Rigidbody => 2,
            CellItem => 1,
            _ => 0,
        };

        /// <summary>Freezes <paramref name="element"/>'s effect driver on the copy - the tell.</summary>
        static void Alter(GameObject go, Element element)
        {
            bool any = false;
            switch (element)
            {
                case Element.Charge:
                    foreach (var c in go.GetComponentsInChildren<CrystalChargeArcs>(true)) { c.enabled = false; any = true; }
                    break;
                case Element.Mass:
                    foreach (var c in go.GetComponentsInChildren<CrystalDepthPrisms>(true)) { c.enabled = false; any = true; }
                    break;
                case Element.Space:
                    foreach (var c in go.GetComponentsInChildren<CrystalSpaceSquares>(true)) { c.enabled = false; any = true; }
                    foreach (var c in go.GetComponentsInChildren<SpaceCrystalAnimator>(true)) { c.enabled = false; any = true; }
                    break;
                case Element.Time:
                    foreach (var c in go.GetComponentsInChildren<CrystalFlipWave>(true)) { c.enabled = false; any = true; }
                    break;
            }
            if (!any)
                CSDebug.LogWarning($"[MantaDecoy] '{go.name}' has no {element} effect driver to alter - the decoy carries no tell.");
        }

        void Update()
        {
            if (_done) return;
            if (_fadeFrom < 0f && Time.time >= _expiresAt) Pop();
            if (_fadeFrom < 0f) return;

            float k = 1f - Mathf.Clamp01((Time.time - _fadeFrom) / FadeSeconds);
            transform.localScale = _scale * k;
            if (k <= 0f) Finish();
        }

        void OnTriggerEnter(Collider other)
        {
            if (_done || _fadeFrom >= 0f) return;
            if (!other.TryGetComponent<ImpactCollider>(out var ic)) return;

            IVesselStatus victim = ic.Impactor switch
            {
                VesselImpactor vi => vi.Vessel?.VesselStatus,
                SkimmerImpactor si => si.Skimmer ? si.Skimmer.VesselStatus : null,
                _ => null,
            };
            if (victim == null || victim.Domain == _domain) return;   // the Manta's own side knows a decoy when it sees one
            Spring(victim);
        }

        /// <summary>A rival took the bait: the decoy becomes danger prisms laid across their course.</summary>
        void Spring(IVesselStatus victim)
        {
            if (_config && _config.DecoyMorphPrefabs is { Length: > 0 })
            {
                var heading = victim.ShipTransform ? victim.ShipTransform.forward : transform.forward;
                var init = new AOEExplosion.InitializeStruct
                {
                    OwnDomain = _domain,
                    Vessel = _vessel,
                    MaxScale = _config.DecoyMorphScale,
                    AnnonymousExplosion = false,
                    SpawnPosition = transform.position,
                    SpawnRotation = Quaternion.LookRotation(heading.sqrMagnitude > 1e-6f ? heading : Vector3.forward),
                    AffectSelfOverride = false,
                };
                ExplosionHelper.CreateExplosion(_config.DecoyMorphPrefabs, init, _container);
            }

            if (_owner) _owner.NotifyDecoySprung(this);
            _owner = null;
            Finish();
        }

        /// <summary>Leaves without springing: its lifetime ran out, or a newer decoy of its kind replaced it.</summary>
        public void Pop()
        {
            if (_done || _fadeFrom >= 0f) return;
            _fadeFrom = Time.time;
            if (TryGetComponent<SphereCollider>(out var trigger)) trigger.enabled = false;
            if (_owner) _owner.NotifyDecoyGone(this);
            _owner = null;
        }

        void Finish()
        {
            if (_done) return;
            _done = true;
            Destroy(gameObject);
        }

        void OnDestroy()
        {
            if (_owner) _owner.NotifyDecoyGone(this);
            _owner = null;
        }
    }
}
