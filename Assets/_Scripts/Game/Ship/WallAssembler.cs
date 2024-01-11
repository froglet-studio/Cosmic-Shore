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


        // Start is called before the first frame update
        void Start()
        {
            BondSiteA = transform.localScale.y * transform.up / 2;
            BondSiteB = (-transform.localScale.y * transform.up / 3) + (transform.localScale.x * transform.right / 2);
            BondSiteC = (transform.localScale.y * transform.up / 3) - (transform.localScale.x * transform.right / 2);
        }

        // Update is called once per frame
        void Update()
        {
            float minLength = float.MaxValue;
            Vector3 minDistance = Vector3.zero;

            var colliders = Physics.OverlapSphere(transform.position, 20);
            foreach (var collider in colliders)
            {
                collider.TryGetComponent(out WallAssembler wallAssembler);
                if (wallAssembler != null && wallAssembler != this)
                {
                    var distance = wallAssembler.BondSiteA + wallAssembler.transform.position - BondSiteB - transform.position;
                    var length = distance.magnitude;
                    if (length < minLength)
                    {
                        minLength = length;
                        minDistance = distance;
                    }
                }
            }
            if (minLength > 1) transform.position += minDistance * .05f;
        }
    }
}
