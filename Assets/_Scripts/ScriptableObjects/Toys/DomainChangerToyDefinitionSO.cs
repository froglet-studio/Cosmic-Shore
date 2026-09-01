using CosmicShore.Data;
using CosmicShore.Gameplay;
using UnityEngine;

namespace CosmicShore.ScriptableObjects
{
    /// <summary>
    /// Toy that cycles the local player's domain (team colour) on each pass - fly through it
    /// to rotate Jade → Ruby → Gold. Routes through the server-authoritative
    /// <c>Player.RequestSetDomain_ServerRpc</c> (never a client-local write), so the recolour
    /// replicates to every peer via <c>Player.NetDomain</c>.
    /// </summary>
    [CreateAssetMenu(fileName = "Toy_DomainChanger", menuName = "ScriptableObjects/Toys/Domain Changer Toy")]
    public class DomainChangerToyDefinitionSO : ToyDefinitionSO
    {
        /// <summary>The colours you wear. It changes DOMAIN and nothing else - and because domain is what
        /// mass belongs to, the trail you lay after it is a different team's.</summary>
        public override ToyCategory Category => ToyCategory.Pilot;

        public override void Spawn(Transform parent, ToyPlacement placement, ToyContext context)
        {
            var host = new GameObject($"ToySet_{Id}");
            host.transform.SetParent(parent, false);
            var set = host.AddComponent<DomainChangerToySet>();
            set.Initialize(this, context, placement, placement.BodyRadius, placement.TriggerRadius);
        }
    }
}
