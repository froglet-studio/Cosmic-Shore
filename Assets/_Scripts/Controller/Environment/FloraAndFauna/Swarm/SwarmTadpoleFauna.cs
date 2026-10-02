using System.Collections;
using CosmicShore.Data;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// ROUND 7 (Docs/SWARM_FAUNA.md §14): this is now a member's PROXY. A swarm member is data in the
    /// swarm's simulation and is DRAWN by the swarm from one GPU buffer; only a member within reach of a
    /// vessel (or one the swarm is about to shed) is given this GameObject, so the platform's every
    /// weapon, ram, joust, skim and predator finds a real heart and a real body prism to act on. While it
    /// lives a proxy draws NOTHING (its body prism is owner-hidden, its heart's and spindle's renderers are
    /// off) - the swarm keeps drawing it, so a member never changes look when it gains or loses its proxy.
    /// On death the proxy's real visuals take over: the released heart and the skeleton (or the suction
    /// into an eater) are the platform's own. A proxy RETIRED because the vessel left is destroyed without
    /// dying: no crystal, no skeleton - the member lives on in the simulation (Fauna.OnDestroy releases a
    /// heart only for a creature that died).
    ///
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
        bool _ready, _hideLive = true;
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
                         Vector3 prismLocalScale, float prismLocalZ, bool hideLive = true,
                         Domains memberDomain = Domains.Blue, float memberAgeSeconds = 0f)
        {
            Swarm = swarm;
            Index = index;
            // Round 8: a MULTI-DOMAIN swarm's members each wear their own domain (Docs/SWARM_FAUNA.md §16.4); a
            // one-colour swarm passes the anchor's, which is what it always was.
            domain = memberDomain != Domains.Blue || !swarm ? memberDomain : swarm.domain;
            Initialize(cell);
            // The GameObject is younger than the creature: grace is measured from the member's birth (§16.3).
            BackdateSpawn(memberAgeSeconds);

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

            // The swarm draws the living member; this proxy only collides and conserves (§14). Without GPU
            // drawing (the fallback) the proxy IS the member's picture and stays visible.
            _hideLive = hideLive;
            if (hideLive) HideLiveVisuals();
        }

        /// <summary>
        /// True once the body prism has finished its creation (collider live, spatial index registered)
        /// and its grow-in has been completed: from here a death leaves a full-size skeleton exactly where
        /// the swarm was drawing the body. Polled by the swarm, which defers a starvation shed until then.
        /// </summary>
        public bool Ready
        {
            get
            {
                if (_ready) return true;
                if (!_body || _body.destroyed || !_body.IsCreationComplete) return false;
                _body.CompleteGrowthImmediately();
                _ready = true;
                return true;
            }
        }

        /// <summary>
        /// Round 8 (Docs/SWARM_FAUNA.md §16.2): make this proxy REAL in the frame it was asked for - the body prism
        /// finishes its creation now (collider on, spatial index registered) and grows to full size - so a weapon
        /// that has already landed on the member can run its own code on this body and this heart. The caller owns
        /// the budget (<see cref="SwarmFauna.MaterialiseForHit"/>).
        /// </summary>
        public bool MaterialiseNow()
        {
            if (_dead || !_body || _body.destroyed) return false;
            _body.CompleteCreationImmediately();
            return Ready;
        }

        /// <summary>The living heart (null once released) - what a blast's lifeform-crystal effects act on.</summary>
        public Crystal Heart => crystal && crystal.IsEmbedded ? crystal : null;

        /// <summary>A predator is chasing this member: the swarm must not retire its proxy mid-hunt (§16.3).</summary>
        public override void NotifyHunted()
        {
            if (!_dead && Swarm) Swarm.KeepProxy(Index);
        }

        /// <summary>Every renderer this proxy owns, off, except what the platform draws for itself: the body
        /// prism is owner-hidden (its collider, index entry and companion entity stay live), and the heart's
        /// renderers are switched off. Undone by <see cref="ShowDeathVisuals"/>.</summary>
        void HideLiveVisuals()
        {
            if (_body) _body.SetOwnerHidden(true);
            var renderers = GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                var r = renderers[i];
                if (!r || r.GetComponentInParent<Prism>(true)) continue;   // the prism draws through its entity
                r.enabled = false;                                         // spindle + heart models
            }
        }

        /// <summary>The death's visuals are the platform's: un-hide the heart (released, collectable) and
        /// the body (skeleton or suction). Runs before anything the death leaves behind can be seen.</summary>
        void ShowDeathVisuals()
        {
            if (crystal)
            {
                var rs = crystal.GetComponentsInChildren<Renderer>(true);
                for (int i = 0; i < rs.Length; i++) if (rs[i]) rs[i].enabled = true;
            }
            var prisms = GetComponentsInChildren<HealthPrism>(true);
            for (int i = 0; i < prisms.Length; i++) if (prisms[i]) prisms[i].SetOwnerHidden(false);
        }

        /// <summary>
        /// The vessel left: give the member back to the swarm's simulation. Not a death - nothing is
        /// released, nothing is left behind, and nothing visible changes (the swarm was drawing it all
        /// along). Mass is conserved because the member still exists in the simulation.
        /// </summary>
        public void Retire()
        {
            if (_dead) return;
            Swarm = null;   // a late death callback must not reach the swarm for a slot that moved on
            Destroy(gameObject);
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
            if (crystal && !_dead && _hideLive)
            {
                // a proxy's heart is never drawn while it lives (the swarm draws it), so a re-formed one
                // is hidden from its first frame too
                var rs = crystal.GetComponentsInChildren<Renderer>(true);
                for (int i = 0; i < rs.Length; i++) if (rs[i]) rs[i].enabled = false;
            }
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
        /// A crystal's world scale IS its collect reward (Docs/ECOSYSTEM.md §40.2), so every death path
        /// asserts the true size BEFORE the sealed Die releases it. Since round 7 the proxy's heart is never
        /// drawn shrunk (the molt is drawn by the swarm's shader), so this is a guard, not a repair.
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
            ShowDeathVisuals();

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
