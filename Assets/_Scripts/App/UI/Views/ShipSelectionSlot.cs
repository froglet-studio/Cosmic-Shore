using System;
using CosmicShore.App.UI.Views;

[Serializable]
public struct ShipSelectionSlot
{
    public string vesselTypeNameOverride;

    public ShipSelectionItemView itemView;

    public bool TryGetVesselType(out VesselClassType type)
    {
        type = VesselClassType.Any;

        if (!string.IsNullOrWhiteSpace(vesselTypeNameOverride) &&
            Enum.TryParse(vesselTypeNameOverride, true, out type))
        {
            return true;
        }

        if (!itemView) return false;
        var sourceName = itemView.gameObject.name;
        return !string.IsNullOrWhiteSpace(sourceName) &&
               Enum.TryParse(sourceName, true, out type);
    }
}