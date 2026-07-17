// FrictionHunterTag.cs
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Empty marker component placed on Friction's hunter vessel prefab so impact
    /// effects (e.g. VesselLifeLossByHunterSkimmerEffectSO) can identify "this is a
    /// hunter" without coupling to Domain or VesselClassType, which can both change
    /// independently of hunter status.
    /// </summary>
    public class FrictionHunterTag : MonoBehaviour
    {
    }
}
