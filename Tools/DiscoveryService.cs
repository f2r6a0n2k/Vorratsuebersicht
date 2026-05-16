using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using Android.App;
using Android.Content;

namespace VorratsUebersicht
{
    public class DiscoveryService : IDisposable
    {
        private UdpClient _udp;
        private bool _running;
        private int _servicePort; // Port des HTTP-Servers

        public event Action<string> OnLog;

        public void Start(int httpPort)
        {
            if (_running) return;
            _servicePort = httpPort;

            try
            {
                _udp = new UdpClient();
                _udp.ExclusiveAddressUse = false;
                _udp.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
                _udp.Client.Bind(new IPEndPoint(IPAddress.Any, 5190));
                _running = true;
                Task.Run(() => ListenLoop());
                OnLog?.Invoke("Discovery-Service gestartet (Port 5190 UDP)");
            }
            catch (Exception ex)
            {
                OnLog?.Invoke($"Discovery-Fehler: {ex.Message}");
            }
        }

        public void Stop()
        {
            _running = false;
            try { _udp?.Close(); } catch { }
            _udp = null;
        }

        private async Task ListenLoop()
        {
            while (_running && _udp != null)
            {
                try
                {
                    var result = await _udp.ReceiveAsync();
                    var msg = Encoding.UTF8.GetString(result.Buffer).Trim();
                    OnLog?.Invoke($"Discovery-Anfrage von {result.RemoteEndPoint}: {msg}");

                    if (msg == "VORRAT_DISCOVERY")
                    {
                        var hostName = Java.Net.InetAddress.GetLocalHost()?.HostName ?? "android";
                        var db = Android_Database.Instance.GetConnection();
                        var dbId = db.ExecuteScalar<string>("SELECT Value FROM Settings WHERE Key = 'SYNC_DATABASE_ID'") ?? "unknown";

                        var response = $"VORRAT_MASTER|{hostName}|{_servicePort}|{dbId}";
                        var respBytes = Encoding.UTF8.GetBytes(response);
                        await _udp.SendAsync(respBytes, respBytes.Length, result.RemoteEndPoint);
                        OnLog?.Invoke($"Antwort gesendet an {result.RemoteEndPoint}");
                    }
                }
                catch (ObjectDisposedException) { break; }
                catch (Exception ex)
                {
                    if (_running)
                        OnLog?.Invoke($"Discovery-Fehler: {ex.Message}");
                }
            }
        }

        public void Dispose()
        {
            Stop();
        }

        /// <summary>
        /// Sucht im lokalen Netz nach einem Master (Client-seitig).
        /// </summary>
        public static async Task<string[]> DiscoverMasterAsync(int timeoutMs = 3000)
        {
            try
            {
                using (var client = new UdpClient())
                {
                    client.Client.ReceiveTimeout = timeoutMs;
                    client.EnableBroadcast = true;
                    client.Client.Bind(new IPEndPoint(IPAddress.Any, 0));

                    var broadcast = new IPEndPoint(IPAddress.Broadcast, 5190);
                    var request = Encoding.UTF8.GetBytes("VORRAT_DISCOVERY");
                    await client.SendAsync(request, request.Length, broadcast);

                    var tasks = new System.Collections.Generic.List<Task<UdpReceiveResult>>();
                    var results = new System.Collections.Generic.List<string>();

                    while (true)
                    {
                        try
                        {
                            var result = await client.ReceiveAsync();
                            var msg = Encoding.UTF8.GetString(result.Buffer).Trim();
                            if (msg.StartsWith("VORRAT_MASTER|"))
                            {
                                results.Add(msg);
                            }
                        }
                        catch (SocketException)
                        {
                            break;
                        }
                    }

                    return results.ToArray();
                }
            }
            catch
            {
                return new string[0];
            }
        }
    }
}
