using CosmicShore.Game;
using UnityEngine;

public abstract class ShipActionExecutorBase : MonoBehaviour
{
    public virtual void Initialize(IShipStatus shipStatus) { }
}