using UnityEngine;

namespace CosmicShore.Data
{
    public struct ShipVelocityModifier
    {
        public Vector3 initialValue;
        public float duration;
        public float elapsedTime;

        /// <summary>
        /// When true this displacement still moves the vessel while
        /// <c>IVesselStatus.IsTranslationRestricted</c> is set — the deliberate, narrow
        /// exception to "restricted means no translation". Only the Sparrow's strafing roll
        /// opts in today: the roll is a dodge, and a dodge you cannot perform in the stance
        /// that pins you in place is not a dodge. Everything else (knockback, nudges,
        /// ability displacement) leaves this false and is held at zero while restricted.
        /// </summary>
        public bool ignoresTranslationRestriction;

        /// <summary>
        /// The velocity ceiling this displacement is allowed to reach, u/s. 0 (every shove but
        /// one) leaves the vessel's own ceiling (<c>VesselTransformer.velocityModifierMax</c>,
        /// 100) in charge. A positive value RAISES the shared ceiling to itself for as long as
        /// this modifier lives - never lowers it - so a launch that was designed to throw harder
        /// than any knock-back can, without lifting the cap on every other shove the vessel
        /// takes. Only the Grizzly's trigger-bomb self-launch sets it today
        /// (<c>GrizzlyTriggerBombConfigSO.selfLaunchCeiling</c>).
        /// </summary>
        public float ceiling;

        public ShipVelocityModifier(Vector3 initialValue, float duration, float elapsedTime)
            : this(initialValue, duration, elapsedTime, false) { }

        public ShipVelocityModifier(Vector3 initialValue, float duration, float elapsedTime,
                                    bool ignoresTranslationRestriction, float ceiling = 0f)
        {
            this.initialValue = initialValue;
            this.duration = duration;
            this.elapsedTime = elapsedTime;
            this.ignoresTranslationRestriction = ignoresTranslationRestriction;
            this.ceiling = ceiling;
        }
    }
}
