using StarWriter.Core;
using UnityEngine;

public abstract class LevelAwareShipActionAbstractBase : ShipAction
{
    public abstract void SetLevelParameter(Element element, float amount);
}