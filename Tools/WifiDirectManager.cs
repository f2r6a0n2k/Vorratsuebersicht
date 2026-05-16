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
    public class WifiDirectManager : Java.Lang.Object, WifiP2pManager.IChannelListener
    {
        private WifiP2pManager _manager;
        private WifiP2pManager.Channel _channel;
        private bool _isConnected;
        private string _groupOwnerIp;

        public bool IsConnected => _isConnected;
        public string GroupOwnerIp => _groupOwnerIp;

        public Action<string> OnLog;
        public Action<string> OnPeerFound;
        public Action<string> OnConnected;
        public Action<string> OnDisconnected;
        public Action<string> OnError;

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

        public void DiscoverPeers()
        {
            _manager.DiscoverPeers(_channel, new WifiActionListener(
                onSuccess: () => OnLog?.Invoke("WiFi-Direct: Suche gestartet"),
                onFailure: (reason) => OnError?.Invoke($"WiFi-Direct: Suche fehlgeschlagen (Code {reason})")
            ));
        }

        public void Connect(string deviceAddress)
        {
            var config = new WifiP2pConfig
            {
                DeviceAddress = deviceAddress,
                GroupOwnerIntent = 15
            };

            _manager.Connect(_channel, config, new WifiActionListener(
                onSuccess: () => OnLog?.Invoke($"WiFi-Direct: Verbindung zu {deviceAddress} erfolgreich"),
                onFailure: (reason) => OnError?.Invoke($"WiFi-Direct: Verbindung fehlgeschlagen (Code {reason})")
            ));
        }

        public void Disconnect()
        {
            if (_isConnected)
            {
                _manager.RemoveGroup(_channel, new WifiActionListener(
                    onSuccess: () =>
                    {
                        _isConnected = false;
                        OnDisconnected?.Invoke("WiFi-Direct: Gruppe verlassen");
                    },
                    onFailure: (reason) => OnError?.Invoke($"WiFi-Direct: Trennen fehlgeschlagen (Code {reason})")
                ));
            }
        }

        public void OnConnectionInfoAvailable(WifiP2pInfo info)
        {
            _isConnected = info.GroupFormed;
            if (_isConnected && info.IsGroupOwner)
            {
                _groupOwnerIp = "192.168.49.1";
                OnConnected?.Invoke(_groupOwnerIp);
                OnLog?.Invoke($"WiFi-Direct: Dieses Gerät ist Gruppen-Besitzer ({_groupOwnerIp})");
            }
            else if (_isConnected && info.GroupOwnerAddress != null)
            {
                _groupOwnerIp = info.GroupOwnerAddress.HostAddress;
                OnConnected?.Invoke(_groupOwnerIp);
                OnLog?.Invoke($"WiFi-Direct: Verbunden mit Gruppen-Besitzer ({_groupOwnerIp})");
            }
        }

        public static bool IsSupported()
        {
            var ctx = Application.Context;
            return ctx.PackageManager.HasSystemFeature("android.hardware.wifi.direct");
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

    internal class WifiActionListener : Java.Lang.Object, WifiP2pManager.IActionListener
    {
        private readonly Action _onSuccess;
        private readonly Action<WifiP2pFailureReason> _onFailure;

        public WifiActionListener(Action onSuccess, Action<WifiP2pFailureReason> onFailure)
        {
            _onSuccess = onSuccess;
            _onFailure = onFailure;
        }

        public void OnSuccess() => _onSuccess?.Invoke();
        public void OnFailure(WifiP2pFailureReason reason) => _onFailure?.Invoke(reason);
    }

    internal class WifiPeerListListener : Java.Lang.Object, WifiP2pManager.IPeerListListener
    {
        private readonly Action<WifiP2pDeviceList> _onPeersAvailable;

        public WifiPeerListListener(Action<WifiP2pDeviceList> onPeersAvailable)
        {
            _onPeersAvailable = onPeersAvailable;
        }

        public void OnPeersAvailable(WifiP2pDeviceList peers) => _onPeersAvailable?.Invoke(peers);
    }

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
                    _manager.RequestPeers(_channel, new WifiPeerListListener(peerList =>
                    {
                        var peers = peerList?.DeviceList;
                        if (peers != null && peers.Count > 0)
                        {
                            foreach (var peer in peers)
                            {
                                _wifiDirect.OnPeerFound?.Invoke($"{peer.DeviceName} ({peer.DeviceAddress})");
                            }
                            _wifiDirect.OnLog?.Invoke($"WiFi-Direct: {peers.Count} Geräte gefunden");
                        }
                    }));
                    break;

                case WifiP2pManager.WifiP2pConnectionChangedAction:
                    var connectionInfo = (WifiP2pInfo)intent.GetParcelableExtra(WifiP2pManager.ExtraWifiP2pInfo);
                    if (connectionInfo != null)
                        _wifiDirect.OnConnectionInfoAvailable(connectionInfo);
                    break;

                case WifiP2pManager.WifiP2pThisDeviceChangedAction:
                    var device = (WifiP2pDevice)intent.GetParcelableExtra(WifiP2pManager.ExtraWifiP2pDevice);
                    _wifiDirect.OnLog?.Invoke($"WiFi-Direct: Dieses Gerät = {device?.DeviceName}");
                    break;
            }
        }
    }
}
