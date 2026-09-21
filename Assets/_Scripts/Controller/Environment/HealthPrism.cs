using System;
using CosmicShore.Core;
using CosmicShore.Gameplay;
using CosmicShore.Utility;
using UnityEngine;
using CosmicShore.Data;
using CosmicShore.ScriptableObjects;
namespace CosmicShore.Gameplay
{
    public class HealthPrism : Prism
    {
        public LifeForm LifeForm;

        /// <summary>
        /// The fauna whose body this prism is — stamped by Fauna.CacheBodyPrisms
        /// (and lazily by ResolveOwnerFauna). Null for flora health prisms and free
        /// prisms. Runtime-only: an auto-property is never serialized, so prefab
        /// instances always start unstamped.
        /// </summary>
        public Fauna OwnerFauna { get; set; }

        /// <summary>
        /// The owning fauna: the stamp when present, else one upward
        /// GetComponentInParent walk whose result is backfilled — so fauna senses
        /// pay a field read per neighbor instead of a hierarchy walk per neighbor
        /// per behavior tick. Unity-null aware: a destroyed owner reads as null,
        /// matching what the walk would return after the owner died.
        /// </summary>
        public Fauna ResolveOwnerFauna()
        {
            var owner = OwnerFauna;
            if (owner == null)
            {
                owner = GetComponentInParent<Fauna>();
                if (owner != null)
                    OwnerFauna = owner;
            }
            return owner;
        }

        [Header("Optional Components")]
        [SerializeField] Spindle spindle;

        // Flora health prisms get a dedicated destruction sound; fauna (and any other
        // lifeform) health prisms keep the generic BlockDestroy one-shot.
        protected override GameplaySFXCategory DestructionSFX =>
            LifeForm is Flora ? GameplaySFXCategory.FloraCollision : base.DestructionSFX;

        /// <summary>
        /// The limb this prism hangs off, resolved lazily and NEVER through a bare
        /// <c>transform.parent</c>.
        ///
        /// <para>A health prism is not parented to a spindle for the whole of its life. The
        /// SKELETON a dead lifeform leaves behind (Docs/ECOSYSTEM.md §26) is re-parented onto
        /// the host cell by <see cref="LeaveAsSkeleton"/> — and onto the SCENE ROOT when that
        /// lifeform had no cell, which is a real state, not a defensive hypothetical. The
        /// skeleton is then ordinary grazeable mass, so the food web routes it straight back
        /// through <see cref="Implode"/> and <see cref="Explode"/>, the two methods that opened
        /// with that deref.</para>
        ///
        /// <para>The consequence was worse than one exception, because both of those methods
        /// resolved the spindle BEFORE calling base: the throw aborted the destruction, so the
        /// prism was never marked destroyed and never left the spatial index — the same grazer
        /// picked the same prism again on the next tick and threw again, forever, once per
        /// frame per skeleton prism, with the mass it was trying to remove still standing. A
        /// self-perpetuating console storm that reads as the game degrading rather than as one
        /// bug.</para>
        /// </summary>
        Spindle ResolveSpindle()
        {
            if (spindle) return spindle;
            var parent = transform.parent;
            if (parent) spindle = parent.GetComponent<Spindle>();
            return spindle;
        }

        public override void Initialize(string playerName = DEFAULT_PLAYER_NAME)
        {
            base.Initialize(playerName);
            if (LifeForm)
                LifeForm.AddHealthBlock(this);

            // Spindle logic disabled for now
            ResolveSpindle();
            if (spindle) spindle.AddHealthBlock(this);
        }

        /// <summary>
        /// A living health prism rides its limb's sway (Docs/ECOSYSTEM.md §47). This is the
        /// creation stamp and it has to be HERE rather than in <see cref="Initialize"/>:
        /// Initialize runs before the companion entity exists and, on the assembled-flora
        /// path, before the prism has been re-parented onto its spindle at local identity —
        /// baking a basis off a transform that is about to change is how a prism ends up
        /// swaying in the wrong direction with nothing to explain it.
        /// </summary>
        protected override void OnCreationComplete()
        {
            base.OnCreationComplete();
            if (spindle) PrismSway.TryStamp(this, spindle);
        }

        public void Reparent(Transform newParent)
        {
            ResolveSpindle();
            if (spindle) spindle.RemoveHealthBlock(this);

            transform.parent = newParent;

            if (LifeForm)
                LifeForm.RemoveHealthBlock(this);

            if (spindle) spindle.CheckForLife();
        }

        /// <summary>
        /// Leaves this prism in the world as part of a dead lifeform's SKELETON: it stops
        /// being body tissue and stays exactly where the creature died, as ordinary cell
        /// mass (Docs/ECOSYSTEM.md §26). This is mass conservation taken at its word - the
        /// body used to be destroyed along with the husk, so a creature's whole frame left
        /// the world on death and only the heart survived it; now the frame stays and the
        /// food web is what eventually removes it, exactly like any other prism.
        ///
        /// The body-part links are dropped FIRST because they are what classifies the prism:
        /// <c>PrismSpatialIndex.ComputeEnvironmentMass</c> reads <see cref="OwnerFauna"/> to
        /// keep a live swarm out of the targeting grids, so the re-file at the end is what
        /// promotes the skeleton from volume-only body mass to grazeable, steerable, counted
        /// environment mass.
        /// </summary>
        /// <param name="newParent">Where the freed prism is re-homed - the host cell. Null
        /// detaches it to the scene root, which is still better than dying with the husk.</param>
        public void LeaveAsSkeleton(Transform newParent)
        {
            if (destroyed) return;

            ResolveSpindle();
            if (spindle)
            {
                spindle.RemoveHealthBlock(this);
                spindle = null;
            }

            // Clear the back-reference before telling the LifeForm, so a re-entrant
            // CheckIfDead can never walk back into a prism that has already left.
            var owningLifeForm = LifeForm;
            LifeForm = null;
            if (owningLifeForm) owningLifeForm.RemoveHealthBlock(this);

            OwnerFauna = null;

            transform.SetParent(newParent, true);

            // The lifeform is gone; what is left is ordinary cell mass. Stopping the sway
            // is the visible half of that — a skeleton that went on swaying would read as
            // a creature nobody could kill (Docs/ECOSYSTEM.md §47).
            PrismSway.Clear(this);

            NotifyPositionChanged();
            if (SpatialIndexId >= 0)
                PrismSpatialIndex.Instance?.NotifyOwnershipChanged(SpatialIndexId);
        }

        protected override void Explode(Vector3 impactVector, Domains domain, string playerName, bool devastate = false,
                                        float debrisSpeedLimit = 0f)
        {
            ResolveSpindle();
            if (spindle) spindle.RemoveHealthBlock(this);

            base.Explode(impactVector, domain, playerName, devastate, debrisSpeedLimit);

            if (LifeForm)
                LifeForm.RemoveHealthBlock(this, playerName);

            // Fauna-body notification: creatures whose bodies are these prisms learn
            // they were shot (the LifeForm path above is flora-only — fauna body
            // prisms deliberately author LifeForm null so they never register as
            // consumable cell mass). Resolved via the stamped owner; a walk-and-
            // backfill only runs for unstamped prisms, once, on this one-shot path.
            var ownerFauna = ResolveOwnerFauna();
            if (ownerFauna) ownerFauna.OnBodyPrismExploded(this, playerName);

            if (spindle) spindle.CheckForLife();
        }
        
        protected override void Implode(Transform targetTransform, Domains domain, string playerName, bool devastate = false)
        {
            ResolveSpindle();
            if (spindle) spindle.RemoveHealthBlock(this);

            base.Implode(targetTransform, domain, playerName, devastate);

            if (LifeForm)
                LifeForm.RemoveHealthBlock(this);

            if (spindle) spindle.CheckForLife();
        }
    }
}