using System.Collections;
using CosmicShore.Data;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// One member of a worm colony (Docs/ECOSYSTEM.md §23) — the killable unit of the
    /// kaiju boss. A worm is a POPULATION and this is an individual in it: every segment
    /// is a genuine fauna carrying its OWN elemental heart, whatever its role. Three
    /// prefabs share this class, distinguished by <see cref="WormSegmentRole"/>: the HEAD
    /// (danger-prism fangs), a BODY segment (soft tissue — killing one SPLITS the
    /// population in two), and the TAIL (danger-prism stinger). The colony brain
    /// (<see cref="WormFauna"/>) owns movement, feeding, and topology; a segment owns
    /// only itself: prism lifecycle, death detection, wither, and its heart.
    ///
    /// A member's ROLE is fixed at birth. A colony that loses an end grows a real
    /// replacement on its next production cycle (<see cref="WormFauna"/>) instead of
    /// hardening a body segment into a stump — so a regrown head arrives with its whole
    /// armour cage and its fangs, which are also the jaws the colony feeds through.
    ///
    /// Death paths, all through the sealed <see cref="Fauna.Die"/> chokepoint:
    ///  • body stripped — every body prism destroyed by players/AOE
    ///    (<see cref="OnBodyPrismExploded"/>),
    ///  • heart jousted — a faster vessel jousts the embedded crystal
    ///    (<see cref="Fauna.Jousted"/> → Predated), and
    ///  • starvation shed — the colony digests its tail-most member
    ///    (<see cref="WormFauna"/>).
    /// EVERY death drops that individual's heart (mass conserved — the lifeform crystal
    /// invariant applies to each member, not to the colony as a whole); the husk withers —
    /// prisms suction inward and spindles evaporate — never pops out (continuity law).
    ///
    /// The colony is DELIBERATELY excluded from the platform's skeleton death
    /// (Docs/ECOSYSTEM.md §26), where an ordinary creature's wither leaves its body prisms
    /// standing as ordinary cell mass. Two reasons, both specific to a kaiju: a skeleton at
    /// this scale is a wall, and the capital segments carry DANGER prisms — leaving those
    /// standing would strew permanent hazards through the cell on every colony death. Keep
    /// the suction exit here unless that danger-prism question gets a decision.
    /// </summary>
    public class WormSegmentFauna : Fauna
    {
        [Header("Worm Segment")]
        [Tooltip("Which of the three colony fauna types this prefab is. Head and Tail " +
                 "author danger prisms; Body is the soft tissue a kill splits the " +
                 "population at. EVERY role carries its own elemental heart — a segment " +
                 "is an individual, not a body part (Docs/ECOSYSTEM.md §23.3). Fixed at " +
                 "birth: a missing end is GROWN, never hardened out of a body segment.")]
        [SerializeField] WormSegmentRole role = WormSegmentRole.Body;
        [Tooltip("Element of the heart THIS segment carries — the element-as-data path " +
                 "(LifeFormCrystal.EnsureElementalCrystal): the heart is provisioned from " +
                 "ElementalCrystalSet at Initialize, exactly as FaunaConfigurationSO.Element " +
                 "provisions other species. Authored, per the elemental contract — never " +
                 "rolled at random. Every role authors one; a per-element colony config " +
                 "overwrites all of them through Fauna.ProvisionHeart.")]
        [SerializeField] Element heartElement = Element.Mass;
        [Tooltip("Where the provisioned heart SITS in segment space (recovered from the " +
                 "2024 wormhead authoring: the heart nests INSIDE the head's armor cage " +
                 "at (0,0,-13.14); a body segment seats its heart at the FRONT face of " +
                 "its core prism, z = 3, so the crystal LEADS and the body trails — the " +
                 "tadpole arrangement). Zero = segment centre. This is a SEAT, not a size — " +
                 "a heart's scale is one curve keyed on LEVEL and is applied at " +
                 "Crystal.SetEmbeddedIn for every lifeform in the game " +
                 "(Docs/ECOSYSTEM.md §33); there is deliberately no per-prefab scale here.")]
        [SerializeField] Vector3 heartLocalPosition = Vector3.zero;
        [Tooltip("Engage the shield on this segment's non-danger body prisms at spawn — " +
                 "the head's authored armor plates (shield sheds on the first hit, then " +
                 "the plate is vulnerable: a two-stage kill). Danger prisms are skipped " +
                 "(danger and shield are mutually exclusive by locked design).")]
        [SerializeField] bool shieldArmor = false;

        /// <summary>The colony this segment belongs to. Set by <see cref="WormFauna"/> on adoption.</summary>
        public WormFauna Colony { get; set; }

        /// <summary>
        /// What this member is. FIXED at birth: a colony that loses an end GROWS a real
        /// replacement on its next production cycle (WormFauna.TickProduction) rather than
        /// hardening a body segment into a stump, so a regrown head arrives with its whole
        /// armour cage and fangs instead of one dangerous core prism.
        /// </summary>
        public WormSegmentRole Role => role;

        // Latched by the first death path to win; every other path no-ops after.
        bool _dead;

        /// <summary>True once any death path has claimed this segment (bloom/grow code bails).</summary>
        public bool IsDead => _dead;

        /// <summary>Segments ride the colony's motion — jousting a heart means outracing the kaiju.</summary>
        public override float CurrentSpeed => Colony ? Colony.CurrentSpeed : 0f;

        /// <summary>
        /// Colony-driven: no per-segment goal coroutine (the base Start would tick
        /// ResolveGoal per segment for a Goal nothing reads — the colony steers).
        /// The base Awake still stamps spawn time (predation-immunity window).
        /// </summary>
        protected override void Start() { }

        public override void Initialize(Cell cell)
        {
            base.Initialize(cell); // record the explicit host cell

            // Body prisms: recolor to the colony's domain and start their growth
            // stamp (they bloom from zero — continuity). LifeForm is deliberately
            // never assigned so these prisms don't register as consumable cell mass
            // (the LightFauna pattern); the cache also powers the per-frame
            // spatial-index sync the colony drives while the worm swims.
            var bodyPrisms = CacheBodyPrisms();
            for (int i = 0; i < bodyPrisms.Length; i++)
            {
                var hp = bodyPrisms[i];
                if (!hp) continue;
                hp.ChangeTeam(domain);
                hp.Initialize("WormColony");
                // Armor: the head's recovered plate cage spawns shielded (first hit
                // sheds the shield, second destroys the plate). Danger prisms skip —
                // danger and shield are mutually exclusive by locked design.
                if (shieldArmor && hp.prismProperties is { IsDangerous: false })
                    hp.ActivateShield();
            }

            // EVERY segment carries its own heart, provisioned to the AUTHORED element
            // (element-as-data — the same channel FaunaConfigurationSO.Element uses):
            // joustable while the segment lives, dropped by the sealed Die on death.
            // A worm is a POPULATION and a segment is an individual in it, so the
            // "every lifeform drops one elemental crystal" invariant lands on each
            // member — head, body and tail alike (Docs/ECOSYSTEM.md §23.3).
            crystal = LifeFormCrystal.EnsureElementalCrystal(this, heartElement);
            if (crystal) crystal.SetEmbeddedIn(this);
            PlaceHeart();
        }

        /// <summary>
        /// Seats the provisioned heart at the authored anchor (see heartLocalPosition) —
        /// the TADPOLE arrangement: the heart rides at the FRONT of the member's own body
        /// prisms with the body trailing behind it, never buried inside them. The body
        /// segment's core prism spans local z ∈ [-3, +3] (measured off the prefab), so its
        /// heart seats at z = 3; the head keeps its recovered 2024 seat INSIDE the armour
        /// cage, which is a fight mechanic rather than an oversight.
        ///
        /// Position ONLY: the heart's size is one curve keyed on LEVEL, applied for every
        /// lifeform at the single gate <see cref="Crystal.SetEmbeddedIn"/>
        /// (Docs/ECOSYSTEM.md §33) — a per-prefab scale here would be a per-prefab REWARD,
        /// because a crystal's world scale is read as gameplay by both the collect reward
        /// and the live domain fauna buff.
        /// </summary>
        void PlaceHeart()
        {
            if (!crystal) return;
            if (heartLocalPosition != Vector3.zero)
                crystal.transform.localPosition = heartLocalPosition;
        }

        /// <summary>Public bridge so the colony can keep the spatial index honest each frame.</summary>
        public void SyncBodyPrismsToIndex() => NotifyBodyPrismsMoved();

        /// <summary>
        /// The jaws: centroid of this segment's live DANGER prisms (the head's fangs /
        /// the tail's stinger) — the suction sink a devoured creature implodes toward,
        /// same construction as the shark's mouth. Falls back to just ahead of the
        /// segment centre when the danger prisms are shot off.
        /// </summary>
        public Vector3 MouthPoint
        {
            get
            {
                var prisms = BodyPrisms;
                Vector3 sum = Vector3.zero;
                int count = 0;
                if (prisms != null)
                {
                    for (int i = 0; i < prisms.Length; i++)
                    {
                        var hp = prisms[i];
                        if (!hp || hp.destroyed || hp.prismProperties is not { IsDangerous: true }) continue;
                        sum += hp.transform.position;
                        count++;
                    }
                }
                return count > 0 ? sum / count : transform.position + transform.forward * 2f;
            }
        }

        /// <summary>
        /// The player-facing kill path: when the last body prism is destroyed the segment
        /// dies. This is now the <see cref="Fauna"/> base behaviour (it was hoisted out of
        /// here so EVERY creature is shootable, not just the worm — see
        /// <see cref="Fauna.OnBodyPrismExploded"/>); the override survives only for the
        /// segment's own <c>_dead</c> guard, which covers the colony-initiated death paths
        /// (<see cref="WitherAway"/>, a split's shed) that the base's guard cannot see.
        /// </summary>
        public override void OnBodyPrismExploded(HealthPrism prism, string killerName)
        {
            if (_dead) return;
            base.OnBodyPrismExploded(prism, killerName);
        }

        /// <summary>
        /// Colony-initiated death (starvation shedding): routes through the sealed
        /// <see cref="Fauna.Die"/> like every other path — heart drops, husk withers.
        /// </summary>
        public void WitherAway(string reason) => Die(reason);

        /// <summary>
        /// Element-as-data forwarding from the colony (see WormFauna.ProvisionHeart):
        /// re-provisions this segment's heart to the picked element — every member of the
        /// population carries one, so the colony's pick reaches all of them.
        /// EnsureElementalCrystal keeps a matching authored crystal and replaces a
        /// mismatched one with the set's model for the requested element.
        /// </summary>
        public void ReprovisionHeart(Element element)
        {
            if (_dead || element == Element.None) return;
            crystal = LifeFormCrystal.EnsureElementalCrystal(this, element);
            if (crystal) crystal.SetEmbeddedIn(this);
            PlaceHeart();
        }

        protected override void OnDeath(string killerName = "")
        {
            if (_dead) return;
            _dead = true;

            // Topology first (split / wound bookkeeping) so the colony re-links while
            // this husk is still positioned in the chain.
            if (Colony) Colony.HandleSegmentDeath(this, killerName);

            if (isActiveAndEnabled && gameObject.activeInHierarchy)
                StartCoroutine(WitherHuskCoroutine(killerName));
            else
                Destroy(gameObject); // teardown path — can't animate while inactive
        }

        /// <summary>
        /// Continuity exit for whatever the killing blow left standing: surviving
        /// prisms suction into the segment centre (their removal cascades the spindle
        /// evaporation), then the husk waits for the spindles to finish fading before
        /// it is removed. Nothing pops out of existence.
        /// </summary>
        IEnumerator WitherHuskCoroutine(string killerName)
        {
            string witherName = string.IsNullOrEmpty(killerName) ? "WormColony" : killerName;
            var prisms = BodyPrisms;
            if (prisms != null)
            {
                for (int i = 0; i < prisms.Length; i++)
                {
                    var hp = prisms[i];
                    if (hp && !hp.destroyed)
                        hp.Consume(transform, domain, witherName, true, true);
                }
            }

            // Spindles destroy themselves when their evaporation completes; bounded
            // wait so a stalled fade can never leak an immortal husk.
            float deadline = Time.time + 5f;
            while (Time.time < deadline && GetComponentInChildren<Spindle>(true))
                yield return null;

            Destroy(gameObject);
        }
    }
}
