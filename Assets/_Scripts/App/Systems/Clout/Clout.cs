namespace CosmicShore.App.Systems.Clout
{
    /// <summary>
    /// Clout is a measurement of you succuss using a ship/element combo in a specific game
    /// TODO: Probably goes to /~chopping block
    /// </summary>
    public class Clout
    {
        ShipTypes shipType; //Ship Class

        int value;

        public Clout(ShipTypes shipType, int value)
        {
            this.shipType = shipType;
            this.value = value;
        }

        public int GetValue() {  return value; }
    }
}