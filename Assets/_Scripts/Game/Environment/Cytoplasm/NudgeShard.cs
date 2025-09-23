using CosmicShore.Core;
using CosmicShore.Game;
using CosmicShore.Utility.ClassExtensions;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace CosmicShore
{
    public class NudgeShard : MonoBehaviour
    {
        float Displacement = 40f;
        float Duration = .3f;
        [SerializeField] int energyResourceIndex = 0;
        [SerializeField] float energyAmount = 0.05f;

        public List<Prism> Prisms;

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
                if (!other.TryGetComponent(out IVesselStatus shipStatus))
                    return;

                shipStatus.VesselTransformer.ModifyVelocity(transform.parent.forward * Displacement, Duration);

                if (shipStatus.VesselType == VesselClassType.Squirrel)
                {
                    shipStatus.ResourceSystem.ChangeResourceAmount(energyResourceIndex, energyAmount);
                    foreach (var prism in Prisms)
                    {
                        prism.Steal(shipStatus.Player.Name, shipStatus.Player.Domain);
                    }
                }
            }
        }
    }
}
