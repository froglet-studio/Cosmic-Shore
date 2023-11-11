namespace CosmicShore.App.Systems.Clout
{
    /// <summary>
    /// Clout is a measurement of you succuss using a ship/element combo in a specific game 
    /// </summary>
    public class Clout
    {
        ShipTypes shipType; //Ship Class
        Element element; //Element
        CloutType cloutType; //Mission or Sport

        int value;

        public Clout(ShipTypes shipType, Element element, CloutType cloutType, int value)
        {
            this.shipType = shipType;
            this.element = element;
            this.cloutType = cloutType;
            this.value = value;
        }

        public int GetValue() {  return value; }
    }
}