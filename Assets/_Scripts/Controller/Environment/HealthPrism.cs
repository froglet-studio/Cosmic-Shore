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

        public override void Initialize(string playerName = DEFAULT_PLAYER_NAME)
        {
            base.Initialize(playerName);
            if (LifeForm)
                LifeForm.AddHealthBlock(this);

            // Spindle logic disabled for now
            spindle ??= transform.parent.GetComponent<Spindle>(); // Every healthPrism requires a spindle parent
            if (spindle) spindle.AddHealthBlock(this);
        }

        public void Reparent(Transform newParent)
        {
            spindle ??= transform.parent.GetComponent<Spindle>();
            if (spindle) spindle.RemoveHealthBlock(this);

            transform.parent = newParent;

            if (LifeForm)
                LifeForm.RemoveHealthBlock(this);

            if (spindle) spindle.CheckForLife();
        }

        protected override void Explode(Vector3 impactVector, Domains domain, string playerName, bool devastate = false)
        {
            spindle ??= transform.parent.GetComponent<Spindle>();
            if (spindle) spindle.RemoveHealthBlock(this);

            base.Explode(impactVector, domain, playerName, devastate);

            if (LifeForm)
                LifeForm.RemoveHealthBlock(this, playerName);

            if (spindle) spindle.CheckForLife();
        }
        
        protected override void Implode(Transform targetTransform, Domains domain, string playerName, bool devastate = false)
        {
            spindle ??= transform.parent.GetComponent<Spindle>();
            if (spindle) spindle.RemoveHealthBlock(this);

            base.Implode(targetTransform, domain, playerName, devastate);

            if (LifeForm)
                LifeForm.RemoveHealthBlock(this);

            if (spindle) spindle.CheckForLife();
        }
    }
}