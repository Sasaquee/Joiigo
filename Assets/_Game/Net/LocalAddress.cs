using System.Collections.Generic;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace Game.Net
{
    /// <summary>IPs IPv4 desta máquina na LAN, para o host passar aos amigos (D-012).</summary>
    public static class LocalAddress
    {
        public static List<string> LanIPv4()
        {
            var result = new List<string>();
            foreach (var nic in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (nic.OperationalStatus != OperationalStatus.Up ||
                    nic.NetworkInterfaceType == NetworkInterfaceType.Loopback ||
                    nic.NetworkInterfaceType == NetworkInterfaceType.Tunnel)
                    continue;

                foreach (var addr in nic.GetIPProperties().UnicastAddresses)
                {
                    if (addr.Address.AddressFamily == AddressFamily.InterNetwork)
                        result.Add(addr.Address.ToString());
                }
            }

            // Endereços de LAN doméstica primeiro.
            result.Sort((a, b) => Rank(a).CompareTo(Rank(b)));
            return result;
        }

        private static int Rank(string ip) =>
            ip.StartsWith("192.168.") ? 0 : ip.StartsWith("10.") ? 1 : ip.StartsWith("172.") ? 2 : 3;
    }
}
