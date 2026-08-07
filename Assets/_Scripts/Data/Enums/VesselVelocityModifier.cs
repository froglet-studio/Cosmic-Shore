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

        public ShipVelocityModifier(Vector3 initialValue, float duration, float elapsedTime)
            : this(initialValue, duration, elapsedTime, false) { }

        public ShipVelocityModifier(Vector3 initialValue, float duration, float elapsedTime,
                                    bool ignoresTranslationRestriction)
        {
            this.initialValue = initialValue;
            this.duration = duration;
            this.elapsedTime = elapsedTime;
            this.ignoresTranslationRestriction = ignoresTranslationRestriction;
        }
    }
}
