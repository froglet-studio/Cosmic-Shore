using StarWriter.Core;
using UnityEngine;

public abstract class LevelAwareShipActionAbstractBase : ShipActionAbstractBase
{
    public abstract void SetLevelParameter(Element element, float amount);
}