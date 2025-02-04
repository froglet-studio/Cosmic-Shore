using CosmicShore.Core;
using CosmicShore.Game;
using CosmicShore.Utility.ClassExtensions;
using System.Collections;
using UnityEngine;

namespace CosmicShore
{
    public class NudgeShard : MonoBehaviour
    {
        float Displacement = 10f;
        float Duration = 2f;
        [SerializeField] int boostResourceIndex = 0;
        [SerializeField] float MaxBoostMultiplier = 2;
        [SerializeField] float BoostDischargeRate = .25f;


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
                var ship = other.GetComponent<ShipGeometry>()?.Ship;
                if (ship != null)
                {
                    if (ship.ShipType == ShipTypes.Squirrel)
                    {

                        //var sign = Mathf.Sign(Vector3.Dot(transform.parent.forward, ship.Transform.forward));
                        //ship.ShipTransformer.ModifyVelocity(sign * transform.parent.forward * Displacement, Duration);
                        StartCoroutine(DischargeBoostCoroutine(ship, Displacement));
                    }
                    else
                    {
                        ship.ShipTransformer.ModifyVelocity(transform.parent.forward * Displacement, Duration);
                    }
                }
            }
        }

        IEnumerator DischargeBoostCoroutine(IShip ship, float ChargedBoostIncrease)
        {
            // TODO: figure out how to get ship data component here so that it is not null
            ship.ResourceSystem.ChangeResourceAmount(boostResourceIndex, ChargedBoostIncrease);
            ship.ShipStatus.ChargedBoostCharge += ChargedBoostIncrease;
            ship.ShipStatus.ChargedBoostDischarging = true;
            while (ship.ShipStatus.ChargedBoostCharge > 1)
            {
                ship.ResourceSystem.ChangeResourceAmount(boostResourceIndex, -BoostDischargeRate);
                ship.ShipStatus.ChargedBoostCharge = 1 + (MaxBoostMultiplier * ship.ResourceSystem.Resources[boostResourceIndex].CurrentAmount);
                yield return new WaitForSeconds(.1f);
            }
            ship.ShipStatus.ChargedBoostCharge = 1;
            ship.ShipStatus.ChargedBoostDischarging = false;

            ship.ResourceSystem.ChangeResourceAmount(boostResourceIndex, -ship.ResourceSystem.Resources[boostResourceIndex].CurrentAmount);
        }

    }
}
