using System.Collections;
using CosmicShore.Data;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// One member of a swarm (Docs/SWARM_FAUNA.md) - a heart, a spindle and one body prism. It is a
    /// genuine fauna and a genuine lifeform: it carries its OWN elemental heart and dies through the
    /// sealed <see cref="Fauna.Die"/>, so every kill drops a collectable crystal and nothing pops.
    /// It has NO behaviour of its own and no Update: the swarm's simulation core decides where every
    /// member is, and <see cref="SwarmFauna"/> writes the pose. This is the worm colony's ruling
    /// (Docs/ECOSYSTEM.md §23.3) one step further - a swarm is a POPULATION, its members are the
    /// lifeforms, and the population's anchor is heartless.
    ///
    /// Death paths, all through the sealed chokepoint:
    ///  • its body prism destroyed by any active force (ram, gun, missile, blast) - the platform's
    ///    <see cref="Fauna.OnBodyPrismExploded"/>;
    ///  • its heart jousted, or a predator eating it (<see cref="Fauna.Predated"/>);
    ///  • STARVATION - the swarm sheds a member when it has gone too long without a meal
    ///    (<see cref="Starve"/>). It withers and leaves its body prism standing as a skeleton.
    /// </summary>
    public class SwarmTadpoleFauna : Fauna
    {
        [Header("Swarm Member")]
        [Tooltip("Optional explicit body prism; resolved from children when empty.")]
        [SerializeField] HealthPrism bodyPrism;

        /// <summary>The swarm this tadpole belongs to and its index in the swarm's sim arrays.</summary>
        public SwarmFauna Swarm { get; private set; }
        public int Index { get; private set; } = -1;

        HealthPrism _body;
        Element _heartElement = Element.None;
        bool _dead;
        bool _danger;
        bool _shield;
        Vector3 _shape;

        /// <summary>True once any death path has run.</summary>
        public bool IsDead => _dead;
        public Element HeartElement => _heartElement;
        public HealthPrism Body => _body;

        /// <summary>The member rides its swarm - jousting its heart means outrunning the body.</summary>
        public override float CurrentSpeed => Swarm ? Swarm.MemberSpeed(Index) : 0f;

        /// <summary>No goal coroutine: the swarm steers every member (the base Start would tick a
        /// Goal nothing reads, once per tadpole).</summary>
        protected override void Start() { }

        /// <summary>
        /// Bind to the swarm and bring the body up: recolour the body prism to the swarm's ONE
        /// domain, start its grow-in, and provision the heart of <paramref name="element"/> at its
        /// species size. Called once, right after Instantiate, by <see cref="SwarmFauna"/>.
        /// </summary>
        public void Bind(Cell cell, SwarmFauna swarm, int index, Element element, float heartWorldScale,
                         Vector3 prismLocalScale, float prismLocalZ)
        {
            Swarm = swarm;
            Index = index;
            domain = swarm.domain;
            Initialize(cell);

            var prisms = CacheBodyPrisms();
            _body = bodyPrism ? bodyPrism : (prisms.Length > 0 ? prisms[0] : null);
            for (int i = 0; i < prisms.Length; i++)
            {
                var hp = prisms[i];
                if (!hp) continue;
                hp.ChangeTeam(domain);
                hp.Initialize("swarm tadpole");
            }
            SetShape(prismLocalScale, prismLocalZ);

            // Every member carries its own heart (Docs/ECOSYSTEM.md §23.3): joustable while it lives,
            // dropped by the sealed Die on death.
            ProvisionMemberHeart(element, heartWorldScale);
        }

        void ProvisionMemberHeart(Element element, float heartWorldScale)
        {
            ApplyHeartSize(heartWorldScale);
            crystal = LifeFormCrystal.EnsureElementalCrystal(this, element);
            if (crystal)
            {
                crystal.transform.localPosition = Vector3.zero;   // the heart LEADS; the body trails along -z
                crystal.SetEmbeddedIn(this);
            }
            ApplyHeartSize(heartWorldScale);
            _heartElement = element;
        }

        /// <summary>
        /// Re-forms the heart as another element - the second half of a MOLT, after the swarm has
        /// shrunk the old heart away (Docs/SWARM_FAUNA.md "Molting"). Never called while dying.
        /// </summary>
        public void ReformHeart(Element element, float heartWorldScale)
        {
            if (_dead || element == _heartElement) return;
            ProvisionMemberHeart(element, heartWorldScale);
        }

        /// <summary>
        /// Draws the heart at <paramref name="factor"/> of its species size - the molt's shrink and
        /// re-form (factor 1 = the true size). The member's root is authored at scale 1, so the heart's
        /// local scale IS its world scale at rest; during a newborn's bloom the heart scales WITH the
        /// root, which is the bloom.
        /// </summary>
        public void SetHeartDisplay(float factor)
        {
            if (_dead || !crystal || !crystal.IsEmbedded) return;
            crystal.transform.localScale = Vector3.one * Mathf.Max(0.001f, HeartWorldScale * Mathf.Clamp01(factor));
        }

        /// <summary>
        /// A heart caught mid-molt is drawn shrunk, and a crystal's world scale IS its collect reward
        /// (Docs/ECOSYSTEM.md §40.2) - so every death path restores the true size BEFORE the sealed Die
        /// releases it. A molt can never make a kill pay less.
        /// </summary>
        void RestoreHeartSize()
        {
            if (crystal && crystal.IsEmbedded) ApplyHeartSize(HeartWorldScale);
        }

        public override bool Predated(string predatorName, Transform devourTarget)
        {
            if (!_dead) RestoreHeartSize();
            return base.Predated(predatorName, devourTarget);
        }

        /// <summary>The heart's transform (null once released).</summary>
        public Transform HeartTransform => crystal && crystal.IsEmbedded ? crystal.transform : null;

        /// <summary>
        /// Sets the body prism's shape (local scale, long axis +z) and seats it behind the heart. A
        /// change after birth GROWS to the new size on the prism clock - the clock-material law: one
        /// stamp, the GPU runs the course - so a molt reads as the body re-forming, not snapping.
        /// </summary>
        public void SetShape(Vector3 localScale, float localZ)
        {
            if (!_body || _body.destroyed) return;
            if ((localScale - _shape).sqrMagnitude < 1e-4f) return;
            _shape = localScale;
            _body.transform.localPosition = new Vector3(0f, 0f, localZ);
            _body.AdmitTargetScale(localScale);
            _body.TargetScale = localScale;
            if (_body.IsCreationComplete) _body.ChangeSize();
        }

        /// <summary>
        /// The body prism's TIER, driven by the swarm: a Charge member startled past the threshold
        /// spikes to DANGER (the pufferfish's spines - they hurt a ship that rams them), and a Charge
        /// member on a shield slot wears the octahedral shield (two hits to kill). Danger and shield
        /// are mutually exclusive (locked design); danger wins.
        /// </summary>
        public void SetTier(bool danger, bool shield)
        {
            if (_dead || !_body || _body.destroyed || !_body.IsCreationComplete) return;
            if (danger) shield = false;
            if (danger == _danger && shield == _shield) return;

            if (danger)
            {
                _body.MakeDangerous();
            }
            else if (shield)
            {
                if (_danger) _body.prismProperties.IsDangerous = false;
                _body.ActivateShield();
            }
            else
            {
                // There is no "make safe" API: MakeDangerous only ever flips one way. Clearing the
                // flag and returning the prism to its normal state through DeactivateShields is the
                // same pair of writes MakeDangerous makes, reversed (PrismStateManager.ApplyNormalState
                // swaps the material back and drops both shield tiers).
                _body.prismProperties.IsDangerous = false;
                _body.DeactivateShields();
            }
            _danger = danger;
            _shield = shield;
        }

        /// <summary>Keep the spatial index honest after the swarm moved this member (movers contract).</summary>
        public void SyncBodyToIndex() => NotifyBodyPrismsMoved();

        /// <summary>Starvation shed: withers to its crystal, leaving its body prism as a skeleton.</summary>
        public void Starve()
        {
            if (_dead) return;
            RestoreHeartSize();
            Die(StarvationKiller);
        }

        public override void OnBodyPrismExploded(HealthPrism prism, string killerName)
        {
            if (_dead) return;
            RestoreHeartSize();
            base.OnBodyPrismExploded(prism, killerName);
        }

        protected override void OnDeath(string killerName = "")
        {
            if (_dead) return;
            _dead = true;

            // The population re-solves its body around the hole first, while this husk still exists.
            if (Swarm) Swarm.HandleMemberDeath(this);

            if (!isActiveAndEnabled || !gameObject.activeInHierarchy)
            {
                Destroy(gameObject);
                return;
            }

            if (DevourTarget)
            {
                // Predation: the body transfers to the eater - suction into the mouth, no skeleton.
                var prisms = BodyPrisms;
                if (prisms != null)
                    for (int i = 0; i < prisms.Length; i++)
                        if (prisms[i] && !prisms[i].destroyed)
                            prisms[i].Consume(DevourTarget, domain, killerName, true, true);
            }
            else
            {
                // Starvation, a joust, or a shot that left the frame standing: the body prism stays
                // as this creature's SKELETON - ordinary cell mass the food web grazes
                // (Docs/ECOSYSTEM.md §26). Detached BEFORE the husk fades or the fade would take it.
                LeaveSkeleton();
            }
            StartCoroutine(FadeOutAndRemove());
        }

        /// <summary>The husk (spindle) shrinks away - continuity of existence; the heart and the
        /// skeleton are already gone from under it.</summary>
        IEnumerator FadeOutAndRemove()
        {
            Vector3 from = transform.localScale;
            float t = 0f;
            const float dur = 0.45f;
            while (t < dur)
            {
                t += Time.deltaTime;
                transform.localScale = Vector3.Lerp(from, Vector3.zero, t / dur);
                yield return null;
            }
            Destroy(gameObject);
        }
    }
}
