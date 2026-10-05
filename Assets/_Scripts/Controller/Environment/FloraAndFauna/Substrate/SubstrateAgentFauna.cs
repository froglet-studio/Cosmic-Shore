using System.Collections;
using CosmicShore.Data;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Round 11b (Docs/SUBSTRATE_FAUNA.md §5): the PROXY of one substrate agent - a heart and one body prism, the
    /// shape of the swarm's member proxy (<see cref="SwarmTadpoleFauna"/>, Docs/SWARM_FAUNA.md §14). An agent is data in
    /// the cell's <see cref="SubstrateCore"/> and is DRAWN by its population (<see cref="SubstrateFauna"/>); only an agent
    /// within reach of a vessel, one a weapon or predator has reached, or one starving to death is given this
    /// GameObject, so the platform's every weapon, ram, joust and skim - and its DANGER plate, which burns an opposing
    /// pilot's petals - acts on a real heart and a real body prism. While it lives it draws nothing (the population keeps
    /// drawing the agent); on death the proxy's real visuals take over: the released heart and the skeleton (or the
    /// suction into an eater). A proxy RETIRED because the vessel left is destroyed without dying: the agent lives on in
    /// the simulation.
    ///
    /// It is a genuine fauna and a genuine lifeform: its own elemental heart, and every death through the sealed
    /// <see cref="Fauna.Die"/>, so every kill drops one collectable crystal and nothing pops. Its body prism's volume is
    /// the agent's STOCK - the mass it has eaten - so the skeleton a death leaves is exactly the mass the agent held.
    /// No behaviour and no Update of its own: the core decides where the agent is and the population writes the pose.
    /// </summary>
    public class SubstrateAgentFauna : Fauna
    {
        [Header("Substrate Agent")]
        [Tooltip("Optional explicit body prism; resolved from children when empty.")]
        [SerializeField] HealthPrism bodyPrism;

        /// <summary>The population this proxy belongs to and the agent's slot in the cell's core.</summary>
        public SubstrateFauna Population { get; private set; }
        public int Index { get; private set; } = -1;

        HealthPrism _body;
        bool _dead, _danger, _ready, _hideLive = true;
        Vector3 _shape;

        public bool IsDead => _dead;
        public HealthPrism Body => _body;

        /// <summary>The agent's speed - what a jouster has to outrun.</summary>
        public override float CurrentSpeed => Population ? Population.AgentSpeed(Index) : 0f;

        /// <summary>No goal coroutine: the substrate steers every agent.</summary>
        protected override void Start() { }

        /// <summary>
        /// Bind to the population and bring the body up: the body prism wears the population's domain at the agent's
        /// TRUE body (volume = stock), and the heart of <paramref name="element"/> is provisioned. Called once, right
        /// after Instantiate, by <see cref="SubstrateFauna"/>.
        /// </summary>
        public void Bind(Cell cell, SubstrateFauna population, int index, Element element, float heartWorldScale,
                         Vector3 bodyLocalScale, float bodyLocalZ, Domains agentDomain, float ageSeconds, bool hideLive = true)
        {
            Population = population;
            Index = index;
            domain = agentDomain;
            Initialize(cell);
            BackdateSpawn(ageSeconds);

            var prisms = CacheBodyPrisms();
            _body = bodyPrism ? bodyPrism : (prisms.Length > 0 ? prisms[0] : null);
            for (int i = 0; i < prisms.Length; i++)
            {
                var hp = prisms[i];
                if (!hp) continue;
                hp.ChangeTeam(domain);
                hp.Initialize("substrate agent");
            }
            SetShape(bodyLocalScale, bodyLocalZ);

            ApplyHeartSize(heartWorldScale);
            crystal = LifeFormCrystal.EnsureElementalCrystal(this, element);
            if (crystal)
            {
                crystal.transform.localPosition = Vector3.zero;   // the heart leads; the body trails along -z
                crystal.SetEmbeddedIn(this);
            }
            ApplyHeartSize(heartWorldScale);

            _hideLive = hideLive;
            if (hideLive) HideLiveVisuals();
        }

        /// <summary>True once the body prism has finished its creation (collider live, spatial index registered) and its
        /// grow-in is complete: from here a death leaves a full-size skeleton where the agent was drawn.</summary>
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

        /// <summary>Make this proxy REAL this frame (a weapon, a predator or starvation has already reached the agent).</summary>
        public bool MaterialiseNow()
        {
            if (_dead || !_body || _body.destroyed) return false;
            _body.CompleteCreationImmediately();
            return Ready;
        }

        /// <summary>The living heart (null once released).</summary>
        public Crystal Heart => crystal && crystal.IsEmbedded ? crystal : null;

        /// <summary>A predator is chasing this agent: its proxy must not retire mid-hunt.</summary>
        public override void NotifyHunted()
        {
            if (!_dead && Population) Population.KeepProxy(Index);
        }

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

        /// <summary>The vessel left: give the agent back to the simulation. Not a death - nothing is released.</summary>
        public void Retire()
        {
            if (_dead) return;
            Population = null;   // a late death callback must not reach the population for a slot that moved on
            Destroy(gameObject);
        }

        /// <summary>
        /// The body prism's TRUE shape (local scale, long axis +z), seated behind the heart. Its volume is the agent's
        /// stock, so eating grows it and the skeleton is the mass held. A change after birth grows on the prism clock.
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
        /// DANGER while the agent strikes: the body prism is a hostile danger prism in its domain (the existing danger
        /// tier), so contact burns an opposing pilot's petals. Calm, resting or winded, it is a plain prism again.
        /// </summary>
        public void SetDanger(bool danger)
        {
            if (_dead || !_body || _body.destroyed || !_body.IsCreationComplete) return;
            if (danger == _danger) return;
            if (danger) _body.MakeDangerous();
            else
            {
                // MakeDangerous only flips one way; the reverse is the same two writes (SwarmTadpoleFauna.SetTier)
                _body.prismProperties.IsDangerous = false;
                _body.DeactivateShields();
            }
            _danger = danger;
        }

        /// <summary>Keep the spatial index honest after the population moved this proxy (movers contract).</summary>
        public void SyncBodyToIndex() => NotifyBodyPrismsMoved();

        /// <summary>Starvation (the stomach and reserve ran out - not a timer): withers to its crystal, leaving its
        /// body prism - its whole stock - as a skeleton.</summary>
        public void Starve()
        {
            if (_dead) return;
            ApplyHeartSize(HeartWorldScale);
            Die(StarvationKiller);
        }

        public override bool Predated(string predatorName, Transform devourTarget)
        {
            if (!_dead) ApplyHeartSize(HeartWorldScale);
            return base.Predated(predatorName, devourTarget);
        }

        public override void OnBodyPrismExploded(HealthPrism prism, string killerName)
        {
            if (_dead) return;
            ApplyHeartSize(HeartWorldScale);
            base.OnBodyPrismExploded(prism, killerName);
        }

        protected override void OnDeath(string killerName = "")
        {
            if (_dead) return;
            _dead = true;
            ShowDeathVisuals();
            if (Population) Population.HandleAgentDeath(this);

            if (!isActiveAndEnabled || !gameObject.activeInHierarchy)
            {
                Destroy(gameObject);
                return;
            }
            if (DevourTarget)
            {
                // Predation: the body transfers to the eater (a pack hunter's mouth, or any platform predator)
                var prisms = BodyPrisms;
                if (prisms != null)
                    for (int i = 0; i < prisms.Length; i++)
                        if (prisms[i] && !prisms[i].destroyed)
                            prisms[i].Consume(DevourTarget, domain, killerName, true, true);
            }
            else
            {
                // starvation, a joust or a shot that left the frame standing: the body stays as the SKELETON
                LeaveSkeleton();
            }
            StartCoroutine(FadeOutAndRemove());
        }

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
