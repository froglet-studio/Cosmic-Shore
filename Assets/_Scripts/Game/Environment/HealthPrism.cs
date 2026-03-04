using System;
using CosmicShore.Core;
using CosmicShore.Game;
using CosmicShore.Utilities;
using UnityEngine;

namespace CosmicShore
{
    public class HealthPrism : Prism
    {
        public LifeForm LifeForm;

        [Header("Optional Components")]
        [SerializeField] Spindle spindle;

        public override void Initialize(string playerName = DEFAULT_PLAYER_NAME)
        {
            // Clear stale reference from previous pool use
            spindle = null;

            base.Initialize(playerName);
            if (LifeForm)
                LifeForm.AddHealthBlock(this);

            spindle ??= transform.parent.GetComponent<Spindle>();
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

            // Clear references and return to pool (no-op if not pooled)
            LifeForm = null;
            spindle = null;
            ReturnToPool();
        }

        protected override void Implode(Transform targetTransform, Domains domain, string playerName, bool devastate = false)
        {
            spindle ??= transform.parent.GetComponent<Spindle>();
            if (spindle) spindle.RemoveHealthBlock(this);

            base.Implode(targetTransform, domain, playerName, devastate);

            if (LifeForm)
                LifeForm.RemoveHealthBlock(this);

            if (spindle) spindle.CheckForLife();

            // Clear references and return to pool (no-op if not pooled)
            LifeForm = null;
            spindle = null;
            ReturnToPool();
        }
    }
}