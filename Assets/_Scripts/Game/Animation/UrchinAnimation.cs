using UnityEngine;
using CosmicShore.Core;
using System.Collections.Generic;

namespace CosmicShore.Game.Animation
{
    class UrchinAnimation : ShipAnimation
    {
        [SerializeField] Transform Body;
        [SerializeField] Transform JetBottomLeft;
        [SerializeField] Transform JetBottomRight;
        [SerializeField] Transform ShroudBottomLeft;
        [SerializeField] Transform ShroudBottomRight;
        [SerializeField] Transform ShroudLeft;
        [SerializeField] Transform ShroudRight;
        [SerializeField] Transform LeftGun;
        [SerializeField] Transform RightGun;
        [SerializeField] Transform JetTopLeft;
        [SerializeField] Transform JetTopRight;
        [SerializeField] Transform ShroudTopLeft;
        [SerializeField] Transform ShroudTopRight;

        [SerializeField] float animationScaler = 25f;

        protected List<Transform> scaledParts = new();

        protected override void Start()
        {
            base.Start();
            scaledParts.Add(JetBottomLeft);
            scaledParts.Add(JetBottomRight);
            scaledParts.Add(JetTopLeft);
            scaledParts.Add(JetTopRight);
        }

        protected override void Update()
        {
            base.Update();

            if (GetComponent<Ship>().ShipStatus.Attached)
            {
                ResetParts(scaledParts, 6);
            }
            else
            {
                ScaleParts(scaledParts, 1.75f, 6);
            }
        }

        void ScaleParts(List<Transform> transforms, float scale, float speed)
        {
            foreach (Transform transform in transforms)
            {
                transform.localScale = Vector3.Lerp(transform.localScale, new Vector3(scale, scale, scale), speed * Time.deltaTime);
            }
        }

        void ResetParts(List<Transform> transforms, float speed)
        {
            foreach (Transform transform in transforms)
            {
                transform.localScale = Vector3.Lerp(transform.localScale, Vector3.one, speed * Time.deltaTime);
            }
        }

        protected override void PerformShipPuppetry(float pitch, float yaw, float roll, float throttle)
        {

            if (GetComponent<Ship>().ShipStatus.Attached)
            {
                RotatePart(Body,
                   Time.deltaTime * 100f,
                    0,
                    0);
            }
            else
            {
                RotatePart(Body,
                    pitch * animationScaler,
                    yaw * animationScaler,
                    roll * animationScaler);
            }

            RotatePart(LeftGun,
                        pitch * animationScaler,
                        yaw * animationScaler,
                        roll * animationScaler);

            RotatePart(RightGun,
                        pitch * animationScaler,
                        yaw * animationScaler,
                        roll * animationScaler);

            RotatePart(JetBottomLeft,
                    pitch * animationScaler,
                    yaw * animationScaler,
                    roll * animationScaler);

            RotatePart(JetBottomRight,
                    pitch * animationScaler,
                    yaw * animationScaler,
                    roll * animationScaler);

            RotatePart(JetTopLeft,
                    pitch * animationScaler,
                    yaw * animationScaler,
                    roll * animationScaler);

            RotatePart(JetTopRight,
                    pitch * animationScaler,
                    yaw * animationScaler,
                    roll * animationScaler);
        }

        protected override void AssignTransforms()
        {
            //Transforms.Add(Fusilage);
            //Transforms.Add(LeftWing);
            //Transforms.Add(RightWing);
        }
    }
}