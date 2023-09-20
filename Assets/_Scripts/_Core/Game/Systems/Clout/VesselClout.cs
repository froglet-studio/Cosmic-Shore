namespace StarWriter.Core.CloutSystem
{
    public class VesselClout
    {
        ShipTypes shipType; //Ship Class
        Element element; //Element
        CloutType cloutType; //Mission or Sport

        int value;

        public VesselClout(ShipTypes shipType, Element element, CloutType cloutType, int value)
        {
            this.shipType = shipType;
            this.element = element;
            this.cloutType = cloutType;
            this.value = value;
        }

        public int GetValue() {  return value; }
    }
}
