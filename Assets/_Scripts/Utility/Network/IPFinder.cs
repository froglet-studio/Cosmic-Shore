using System.Linq;
using System.Net;
using System.Net.Sockets;


namespace CosmicShore.Utilities.Network
{
    public static class IPFinder
    {
        public static string FindIP()
        {
            return Dns.GetHostEntry(Dns.GetHostName()).AddressList.LastOrDefault(ip => ip.AddressFamily == AddressFamily.InterNetwork && ip.ToString().StartsWith("192.168")).ToString();
        }
    }
}