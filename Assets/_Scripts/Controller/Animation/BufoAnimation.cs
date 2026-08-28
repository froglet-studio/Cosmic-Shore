using CosmicShore.Gameplay;
using System.Collections.Generic;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    class BufoAnimation : VesselAnimation
    {
        [SerializeField] Transform Fusilage;
        [SerializeField] Transform ThrusterTopRight;
        [SerializeField] Transform TopWing;
        [SerializeField] Transform ThrusterBottomRight;
        [SerializeField] Transform ThrusterBottomLeft;
        [SerializeField] Transform BottomWing;
        [SerializeField] Transform ThrusterTopLeft;

        // The BODY lean, in degrees at full stick. The fleet authors this at 25 for a
        // fuselage (Dolphin/Rhino/Riptide/Urchin 25, Manta 30); the 80-ish scalers
        // elsewhere are yaw scalers that swing WINGS and ENGINES, not the hull. This
        // was a hard-coded 82 - over 3x the fleet value for a body - which read as the
        // ship visually over-rotating relative to where it was actually pointed.
        // Serialized rather than const so it is tunable in the Inspector like every
        // other vessel, instead of needing a recompile.
        [SerializeField, Tooltip("Body lean in degrees at full stick. Fleet standard for a fuselage is 25.")]
        float animationScalar = 25f;

        /// <summary>Thrusters lean slightly harder than the hull, preserving the original 1.05 ratio.</summary>
        float exaggeratedAnimationScalar => 1.05f * animationScalar;

        protected override void AssignTransforms()
        {
            Transforms.Add(Fusilage);
            //Transforms.Add(Turret);
            Transforms.Add(ThrusterTopRight);
            Transforms.Add(TopWing);
            Transforms.Add(ThrusterBottomRight);
            Transforms.Add(ThrusterBottomLeft);
            Transforms.Add(BottomWing);
            Transforms.Add(ThrusterTopLeft);
        }

        protected override void PerformShipPuppetry(float pitch, float yaw, float roll, float throttle)
        {
            var pitchScalar = pitch * exaggeratedAnimationScalar;
            var yawScalar = yaw * exaggeratedAnimationScalar;
            var rollScalar = roll * exaggeratedAnimationScalar;

            RotatePart(Fusilage, pitch * animationScalar, yaw * animationScalar, 0);
            //AnimatePart(Turret, pitchScalar * .7f, yawScalar, rollScalar);

            foreach (var part in new List<Transform>() { ThrusterTopRight, TopWing, ThrusterBottomRight, ThrusterBottomLeft, BottomWing, ThrusterTopLeft })
                RotatePart(part, pitchScalar, yawScalar, -yawScalar);
        }

        protected override void RotatePart(Transform part, float pitch, float yaw, float roll)
        {
            Quaternion rotation = VesselStatus.IsPortrait ? Quaternion.Euler(yaw, -pitch, -roll) : Quaternion.Euler(pitch, yaw, roll);

            part.localRotation = Quaternion.Lerp(
                                    part.localRotation,
                                    rotation,
                                    lerpAmount * Time.deltaTime);
        }
    }
}