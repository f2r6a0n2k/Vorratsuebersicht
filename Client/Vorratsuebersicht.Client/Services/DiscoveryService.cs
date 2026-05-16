using System.Net;
using System.Net.Sockets;
using System.Text;

namespace Vorratsuebersicht.Client.Services
{
    /// <summary>Findet Vorrats�bersicht-Master im lokalen Netzwerk via UDP-Broadcast.</summary>
    public static class DiscoveryService
    {
        public static async Task<string[]> DiscoverMastersAsync(int timeoutMs = 3000)
        {
            var results = new List<string>();

            try
            {
                using var client = new UdpClient();
                client.Client.ReceiveTimeout = timeoutMs;
                client.EnableBroadcast = true;
                client.Client.Bind(new IPEndPoint(IPAddress.Any, 0));

                var broadcast = new IPEndPoint(IPAddress.Broadcast, 5190);
                var request = Encoding.UTF8.GetBytes("VORRAT_DISCOVERY");
                await client.SendAsync(request, request.Length, broadcast);

                while (true)
                {
                    try
                    {
                        var result = await client.ReceiveAsync();
                        var msg = Encoding.UTF8.GetString(result.Buffer).Trim();
                        if (msg.StartsWith("VORRAT_MASTER|"))
                            results.Add(msg);
                    }
                    catch (SocketException) { break; }
                }
            }
            catch { }

            return results.ToArray();
        }
    }
}
