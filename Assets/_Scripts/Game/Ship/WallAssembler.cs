using CosmicShore.Core;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace CosmicShore
{
    public class WallAssembler : MonoBehaviour
    {
        public bool isActive = false;
        public Vector3 BondSiteA;
        public Vector3 BondSiteB;
        public Vector3 BondSiteC;
        public bool SiteAIsEmpty = true;
        public bool SiteBIsEmpty = true;
        public bool SiteCIsEmpty = true;
        WallAssembler mateA;
        WallAssembler mateB;
        WallAssembler mateC;

        

        // Start is called before the first frame update
        void Start()
        {
            var scale = GetComponent<TrailBlock>().TargetScale;
            BondSiteA = scale.y * transform.up / 2f;
            BondSiteB = (-scale.y * transform.up / 3f) + (scale.x * transform.right / 2f);
            BondSiteC = (scale.y * transform.up / 3f) - (scale.x * transform.right / 2f);
        }

        // Update is called once per frame
        void FixedUpdate()
        {
            float minLengthA = float.MaxValue;
            Vector3 minDistanceA = Vector3.zero;
            Vector3 axisA = Vector3.one;

            float minLengthB = float.MaxValue;
            Vector3 minDistanceB = Vector3.zero;
            Vector3 axisB = Vector3.zero;

            float minLengthC = float.MaxValue;
            Vector3 minDistanceC = Vector3.zero;
            Vector3 axisC = Vector3.zero;

            WallAssembler wallAssembler;

            var colliders = Physics.OverlapSphere(transform.position, 20);

            foreach (var collider in colliders)
            {
                collider.TryGetComponent(out wallAssembler);
                if (wallAssembler != null && wallAssembler != this && (wallAssembler.SiteBIsEmpty || wallAssembler == mateA))
                {
                    var distance = wallAssembler.BondSiteB + wallAssembler.transform.position - (BondSiteA + transform.position);
                    var length = distance.magnitude;
                    if (length < minLengthA)
                    {
                        minLengthA = length;
                        minDistanceA = distance;
                        if (mateA && mateA != wallAssembler) mateA.SiteBIsEmpty = true;
                        mateA = wallAssembler;
                    }
                }
            }
            if (mateA)
            {
                mateA.SiteBIsEmpty = false;
                axisA = Vector3.Cross(BondSiteA, minDistanceA); 
            }

            foreach (var collider in colliders)
            {
                collider.TryGetComponent(out wallAssembler);
                if (wallAssembler != null && wallAssembler != this && (wallAssembler.SiteAIsEmpty || wallAssembler == mateB))
                {
                    var distance = wallAssembler.BondSiteA + wallAssembler.transform.position - BondSiteB - transform.position;
                    var length = distance.magnitude;
                    if (length < minLengthB)
                    {
                        minLengthB = length;
                        minDistanceB = distance;
                        if (mateB && mateB != wallAssembler) mateB.SiteBIsEmpty = true;
                        mateB = wallAssembler;
                    }
                }
            }
            if (mateB)
            {
                mateB.SiteAIsEmpty = false;
                axisB = Vector3.Cross(BondSiteB, minDistanceB);
            }

            foreach (var collider in colliders)
            {
                collider.TryGetComponent(out wallAssembler);
                if (wallAssembler != null && wallAssembler != this && (wallAssembler.SiteCIsEmpty || wallAssembler == mateC))
                {
                    var distance = wallAssembler.BondSiteC + wallAssembler.transform.position - BondSiteC - transform.position;
                    var length = distance.magnitude;
                    if (length < minLengthC)
                    {
                        minLengthC = length;
                        minDistanceC = distance;
                        if (mateC && mateC != wallAssembler) mateC.SiteBIsEmpty = true;
                        mateC = wallAssembler;
                    }
                }
            }
            if (mateC)
            {
                mateC.SiteCIsEmpty = false;
                axisC = Vector3.Cross(BondSiteC, minDistanceC);
            }

            if (minLengthA > 3)
            {
                transform.position += minDistanceA * .05f;
                transform.Rotate(axisA.normalized, 1);
            }
            else if (minLengthA < 2.8)
            {
                transform.position -= minDistanceA * .05f;
                transform.Rotate(axisA.normalized, -1);
            }
            if (minLengthB > 3)
            {
                transform.position += minDistanceB * .05f;
                transform.Rotate(axisB.normalized, 1);
            }
            else if (minLengthB < 2.8)
            {
                transform.position -= minDistanceB * .05f;
                transform.Rotate(axisB.normalized, -1);
            }
            if (minLengthC > 3)
            {
                transform.position += minDistanceC * .05f;
                transform.Rotate(axisC.normalized, 1);
            }
            else if (minLengthC < 2.8)
            {
                transform.position -= minDistanceC * .05f;
                transform.Rotate(axisC.normalized, -1);
            }

        }
    }
}
