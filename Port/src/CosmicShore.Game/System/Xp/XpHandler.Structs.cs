// Extracted verbatim from Assets/_Scripts/System/Xp/XpHandler.cs (the XpData struct
// only). The XpHandler class itself ports in the services phase (PlayFab-coupled).
namespace CosmicShore.Core
{
    /// <summary>
    /// Captain Xp Data
    /// Contains Captain class elements - Space, Time, Charge, Mass
    /// </summary>
    [System.Serializable]
    public struct XpData
    {
        public int Space;
        public int Time;
        public int Charge;
        public int Mass;

        public XpData(int space, int time, int mass, int charge)
        {
            Space = space;
            Time = time;
            Mass = mass;
            Charge = charge;
        }
    }
}
