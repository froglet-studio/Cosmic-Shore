using CosmicShore.Core;
using CosmicShore.Game;
using CosmicShore.Utility.ClassExtensions;
using System.Collections;
using UnityEngine;

namespace CosmicShore
{
    public class NudgeShard : MonoBehaviour
    {
        float Displacement = 75f;
        float Duration = .75f;
        [SerializeField] int boostResourceIndex = 0;
        [SerializeField] float MaxBoostMultiplier = 2;
        [SerializeField] float BoostDischargeRate = .25f;

        public TrailBlock Prism;

        private void Start()
        {
            var scale = transform.parent.localScale;
            Displacement = Displacement * scale.x * scale.y;
            Duration = Duration * scale.z;
        }

        void OnTriggerEnter(Collider other)
        {
            if (other.gameObject.IsLayer("Ships"))
            {
                if (!other.TryGetComponent(out ShipGeometry shipGeometry))
                {
                    return;
                }
                
                //if (shipStatus.ShipType == ShipTypes.Squirrel)
                //{
                //    //var sign = Mathf.Sign(Vector3.Dot(transform.parent.forward, ship.Transform.forward));
                //    //ship.ShipTransformer.ModifyVelocity(sign * transform.parent.forward * Displacement, Duration);
                //    StartCoroutine(DischargeBoostCoroutine(shipStatus, Displacement));
                //    Prism?.Steal(shipStatus.Player);
                //}
                else
                {
                    var shipStatus = shipGeometry.Ship.ShipStatus;
                    shipStatus.ShipTransformer.ModifyVelocity(transform.parent.forward * Displacement, Duration);
                }
            }
        }

        //IEnumerator DischargeBoostCoroutine(IShipStatus shipStatus, float ChargedBoostIncrease)
        //{
        //    // TODO: figure out how to get ship data component here so that it is not null
        //    shipStatus.ResourceSystem.ChangeResourceAmount(boostResourceIndex, ChargedBoostIncrease);
        //    shipStatus.ChargedBoostCharge += ChargedBoostIncrease;
        //    shipStatus.ChargedBoostDischarging = true;
        //    while (shipStatus.ChargedBoostCharge > 1)
        //    {
        //        shipStatus.ResourceSystem.ChangeResourceAmount(boostResourceIndex, -BoostDischargeRate/Duration);
        //        shipStatus.ChargedBoostCharge = 1 + (MaxBoostMultiplier * shipStatus.ResourceSystem.Resources[boostResourceIndex].CurrentAmount);
        //        yield return new WaitForSeconds(.1f);
        //    }
        //    shipStatus.ChargedBoostCharge = 1;
        //    shipStatus.ChargedBoostDischarging = false;

        //    shipStatus.ResourceSystem.ChangeResourceAmount(boostResourceIndex, -shipStatus.ResourceSystem.Resources[boostResourceIndex].CurrentAmount);
        //}

    }
}
