using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;

namespace CosmicShore.Engine.Networking
{
    /// <summary>
    /// Finding an <see cref="OnlineServer"/> on the local network: a UDP probe to every broadcast
    /// address (and to this machine, for a server running here), answered by each server with its
    /// <see cref="ServerAnnouncement"/>. The answers come back best first - the server every game on
    /// the network converges on (<see cref="ServerAnnouncement.Better"/>).
    /// </summary>
    internal static class OnlineDiscovery
    {
        internal sealed class Found
        {
            public ServerAnnouncement Server;
            /// <summary>The address the answer came from: where this machine reaches the server.</summary>
            public string Address;
        }

        /// <summary>Where a probe or an announcement goes: the limited broadcast, each interface's directed broadcast, and this machine.</summary>
        public static List<IPAddress> BroadcastTargets()
        {
            var list = new List<IPAddress> { IPAddress.Broadcast, IPAddress.Loopback };
            try
            {
                foreach (var ni in NetworkInterface.GetAllNetworkInterfaces())
                {
                    if (ni.OperationalStatus != OperationalStatus.Up || ni.NetworkInterfaceType == NetworkInterfaceType.Loopback) continue;
                    foreach (var ua in ni.GetIPProperties().UnicastAddresses)
                    {
                        if (ua.Address.AddressFamily != AddressFamily.InterNetwork || ua.IPv4Mask == null) continue;
                        var ip = ua.Address.GetAddressBytes();
                        var mask = ua.IPv4Mask.GetAddressBytes();
                        if (mask.All(b => b == 0)) continue;
                        var bc = new byte[4];
                        for (int i = 0; i < 4; i++) bc[i] = (byte)(ip[i] | ~mask[i]);
                        var addr = new IPAddress(bc);
                        if (!list.Contains(addr)) list.Add(addr);
                    }
                }
            }
            catch (Exception) { }
            return list;
        }

        /// <summary>Probes for <paramref name="timeoutMs"/> and returns every server that answered, best first.</summary>
        public static List<Found> Probe(int timeoutMs, IEnumerable<IPAddress> extraTargets = null)
        {
            var found = new Dictionary<string, Found>();
            UdpClient udp;
            try
            {
                udp = new UdpClient(new IPEndPoint(IPAddress.Any, 0)) { EnableBroadcast = true };
                OnlineWire.IgnoreConnectionResets(udp);
            }
            catch (SocketException) { return new List<Found>(); }
            using (udp)
            {
                var probe = Encoding.UTF8.GetBytes(OnlineWire.ProbePrefix + OnlineWire.Version);
                var targets = BroadcastTargets();
                if (extraTargets != null) targets.AddRange(extraTargets);
                void SendProbes()
                {
                    foreach (var t in targets)
                        try { udp.Send(probe, probe.Length, new IPEndPoint(t, OnlineWire.DiscoveryPort)); } catch (Exception) { }
                }
                SendProbes();
                var sw = Stopwatch.StartNew();
                bool resent = false;
                udp.Client.ReceiveTimeout = 50;
                while (sw.ElapsedMilliseconds < timeoutMs)
                {
                    // Datagrams get lost: probe a second time halfway through.
                    if (!resent && sw.ElapsedMilliseconds >= timeoutMs / 2) { SendProbes(); resent = true; }
                    IPEndPoint from = new(IPAddress.Any, 0);
                    byte[] data;
                    try { data = udp.Receive(ref from); }
                    catch (SocketException) { continue; }
                    catch (ObjectDisposedException) { break; }
                    var a = OnlineWire.DecodeAnnouncement(SafeUtf8(data));
                    if (a == null || a.V != OnlineWire.Version || string.IsNullOrEmpty(a.Id) || a.Port <= 0) continue;
                    var ip = from.Address.IsIPv4MappedToIPv6 ? from.Address.MapToIPv4() : from.Address;
                    // A server on this machine answers on every path; loopback is the one to use.
                    if (!found.TryGetValue(a.Id, out var have) || (IPAddress.IsLoopback(ip) && have.Address != "127.0.0.1"))
                        found[a.Id] = new Found { Server = a, Address = IPAddress.IsLoopback(ip) ? "127.0.0.1" : ip.ToString() };
                }
            }
            var list = found.Values.ToList();
            list.Sort((x, y) => ServerAnnouncement.Better(x.Server, y.Server) ? -1 : ServerAnnouncement.Better(y.Server, x.Server) ? 1 : 0);
            return list;
        }

        static string SafeUtf8(byte[] data)
        {
            try { return Encoding.UTF8.GetString(data); }
            catch (Exception) { return null; }
        }
    }
}
