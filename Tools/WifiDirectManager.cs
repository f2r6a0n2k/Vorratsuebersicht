using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using Android.App;
using Android.Content;
using Android.Net.Wifi.P2p;
using Android.Net.Wifi;
using Android.OS;

namespace VorratsUebersicht
{
    /// <summary>
    /// WiFi-Direct (P2P) Manager f�r die direkte Ger�te-zu-Ger�te-Verbindung
    /// ohne WLAN-Router. Erm�glicht Off-Grid-Sync.
    /// </summary>
    public class WifiDirectManager : Java.Lang.Object, WifiP2pManager.IChannelListener
    {
        private WifiP2pManager _manager;
        private WifiP2pManager.Channel _channel;
        private bool _isConnected;
        private string _groupOwnerIp;

        public bool IsConnected => _isConnected;
        public string GroupOwnerIp => _groupOwnerIp;

        public event Action<string> OnLog;
        public event Action<string> OnPeerFound;     // argument: device name + address
        public event Action<string> OnConnected;     // argument: group owner IP
        public event Action<string> OnDisconnected;
        public event Action<string> OnError;

        public WifiDirectManager()
        {
            var ctx = Application.Context;
            _manager = (WifiP2pManager)ctx.GetSystemService(Context.WifiP2pService);
            _channel = _manager.Initialize(ctx, Looper.MainLooper, this);
        }

        public void OnChannelDisconnected()
        {
            _isConnected = false;
            OnDisconnected?.Invoke("WiFi-Direct Kanal getrennt");
        }

        /// <summary>
        /// Startet die Suche nach WiFi-Direct Ger�ten in der N�he.
        /// </summary>
        public void DiscoverPeers()
        {
            _manager.DiscoverPeers(_channel, new WifiP2pManager.ActionListener
            {
                OnSuccess = () => OnLog?.Invoke("WiFi-Direct: Suche gestartet"),
                OnFailure = (reason) => OnError?.Invoke($"WiFi-Direct: Suche fehlgeschlagen (Code {reason})")
            });
        }

        /// <summary>
        /// Verbindet zu einem gefundenen Ger�t.
        /// </summary>
        public void Connect(string deviceAddress)
        {
            var config = new WifiP2pConfig
            {
                DeviceAddress = deviceAddress,
                GroupOwnerIntent = 15 // will this device be the group owner (0-15, 15 = highest)
            };

            _manager.Connect(_channel, config, new WifiP2pManager.ActionListener
            {
                OnSuccess = () => OnLog?.Invoke($"WiFi-Direct: Verbindung zu {deviceAddress} erfolgreich"),
                OnFailure = (reason) => OnError?.Invoke($"WiFi-Direct: Verbindung fehlgeschlagen (Code {reason})")
            });
        }

        /// <summary>
        /// Entfernt die aktuelle WiFi-Direct Verbindung.
        /// </summary>
        public void Disconnect()
        {
            if (_isConnected)
            {
                _manager.RemoveGroup(_channel, new WifiP2pManager.ActionListener
                {
                    OnSuccess = () =>
                    {
                        _isConnected = false;
                        OnDisconnected?.Invoke("WiFi-Direct: Gruppe verlassen");
                    },
                    OnFailure = (reason) => OnError?.Invoke($"WiFi-Direct: Trennen fehlgeschlagen (Code {reason})")
                });
            }
        }

        /// <summary>
        /// Wird aufgerufen, wenn sich der WiFi-Direct Verbindungsstatus �ndert.
        /// Extrahiert die IP-Adresse des Gruppen-Besitzers.
        /// </summary>
        public void OnConnectionInfoAvailable(WifiP2pInfo info)
        {
            _isConnected = info.GroupFormed;
            if (_isConnected && info.IsGroupOwner)
            {
                // Dieses Ger�t ist der Gruppen-Besitzer (Host)
                _groupOwnerIp = "192.168.49.1"; // Standard WiFi-Direct Gateway
                OnConnected?.Invoke(_groupOwnerIp);
                OnLog?.Invoke($"WiFi-Direct: Dieses Ger�t ist Gruppen-Besitzer ({_groupOwnerIp})");
            }
            else if (_isConnected && info.GroupOwnerAddress != null)
            {
                // Dieses Ger�t ist ein Client
                _groupOwnerIp = info.GroupOwnerAddress.HostAddress;
                OnConnected?.Invoke(_groupOwnerIp);
                OnLog?.Invoke($"WiFi-Direct: Verbunden mit Gruppen-Besitzer ({_groupOwnerIp})");
            }
        }

        /// <summary>
        /// Pr�ft, ob WiFi-Direct auf dem Ger�t verf�gbar ist.
        /// </summary>
        public static bool IsSupported()
        {
            var ctx = Application.Context;
            return ctx.PackageManager.HasSystemFeature(Android.Content.PM.PackageFeatures.WifiDirect);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                try { _manager?.RemoveGroup(_channel, null); } catch { }
                try { _channel?.Dispose(); } catch { }
            }
            base.Dispose(disposing);
        }
    }

    /// <summary>
    /// Broadcast-Receiver f�r WiFi-Direct Ereignisse.
    /// </summary>
    [BroadcastReceiver(Enabled = true, Exported = false)]
    public class WifiDirectBroadcastReceiver : BroadcastReceiver
    {
        private readonly WifiP2pManager _manager;
        private readonly WifiP2pManager.Channel _channel;
        private readonly WifiDirectManager _wifiDirect;

        public WifiDirectBroadcastReceiver(WifiP2pManager manager, WifiP2pManager.Channel channel, WifiDirectManager wifiDirect)
        {
            _manager = manager;
            _channel = channel;
            _wifiDirect = wifiDirect;
        }

        public override void OnReceive(Context context, Intent intent)
        {
            var action = intent.Action;

            switch (action)
            {
                case WifiP2pManager.WifiP2pStateChangedAction:
                    var state = intent.GetIntExtra(WifiP2pManager.ExtraWifiState, -1);
                    if (state == (int)WifiP2pState.Enabled)
                        _wifiDirect.OnLog?.Invoke("WiFi-Direct ist bereit");
                    break;

                case WifiP2pManager.WifiP2pPeersChangedAction:
                    // Peers verf�gbar - Liste abrufen
                    _manager.RequestPeers(_channel, peerList =>
                    {
                        var peers = peerList?.DeviceList;
                        if (peers != null && peers.Count > 0)
                        {
                            foreach (var peer in peers)
                            {
                                _wifiDirect.OnPeerFound?.Invoke($"{peer.DeviceName} ({peer.DeviceAddress})");
                            }
                            _wifiDirect.OnLog?.Invoke($"WiFi-Direct: {peers.Count} Ger�te gefunden");
                        }
                    });
                    break;

                case WifiP2pManager.WifiP2pConnectionChangedAction:
                    var connectionInfo = (WifiP2pInfo)intent.GetParcelableExtra(WifiP2pManager.ExtraWifiP2pInfo);
                    if (connectionInfo != null)
                        _wifiDirect.OnConnectionInfoAvailable(connectionInfo);
                    break;

                case WifiP2pManager.WifiP2pThisDeviceChangedAction:
                    var device = (WifiP2pDevice)intent.GetParcelableExtra(WifiP2pManager.ExtraWifiP2pDevice);
                    _wifiDirect.OnLog?.Invoke($"WiFi-Direct: Dieses Ger�t = {device?.DeviceName}");
                    break;
            }
        }
    }
}
