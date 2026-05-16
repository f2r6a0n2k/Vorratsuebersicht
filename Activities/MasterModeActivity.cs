using Android.App;
using Android.Content;
using Android.OS;
using Android.Views;
using Android.Widget;
using Android.Graphics;
using System;
using System.Net;
using System.Text;

namespace VorratsUebersicht
{
    [Activity(Label = "Master-Modus")]
    public class MasterModeActivity : Activity
    {
        private SyncServerListener _server;
        private DiscoveryService _discovery;
        private TextView _statusText, _ipText, _clientCount, _accessKeyLabel;
        private Button _startStopButton, _toggleAuthButton;
        private ImageView _qrImage;
        private LinearLayout _logLayout;
        private bool _isRunning;
        private bool _authEnabled;

        protected override void OnCreate(Bundle savedInstanceState)
        {
            base.OnCreate(savedInstanceState);
            SetContentView(Resource.Layout.MasterMode);

            _statusText = FindViewById<TextView>(Resource.Id.masterStatus);
            _ipText = FindViewById<TextView>(Resource.Id.masterIp);
            _clientCount = FindViewById<TextView>(Resource.Id.masterClientCount);
            _startStopButton = FindViewById<Button>(Resource.Id.masterStartStop);
            _qrImage = FindViewById<ImageView>(Resource.Id.masterQrCode);
            _logLayout = FindViewById<LinearLayout>(Resource.Id.masterLog);
            _accessKeyLabel = FindViewById<TextView>(Resource.Id.masterAccessKey);
            _toggleAuthButton = FindViewById<Button>(Resource.Id.masterToggleAuth);

            _server = new SyncServerListener();
            _server.OnClientConnected += OnClientConnected;
            _server.OnError += OnError;

            _discovery = new DiscoveryService();
            _discovery.OnLog += msg => LogMessage(msg);

            _startStopButton.Click += OnStartStopClick;
            _toggleAuthButton.Click += OnToggleAuthClick;

            // Gespeicherten Access-Key anzeigen
            var existingKey = SyncServerListener.GetOrCreateAccessKey();
            _authEnabled = !string.IsNullOrEmpty(existingKey);

            UpdateUI();
            ShowLocalIPs();
        }

        private void ShowLocalIPs()
        {
            var ips = new StringBuilder();
            try
            {
                var addresses = Java.Net.NetworkInterface.NetworkInterfaces;
                if (addresses != null)
                {
                    while (addresses.HasMoreElements)
                    {
                        var iface = addresses.NextElement() as Java.Net.NetworkInterface;
                        if (iface == null || !iface.IsUp) continue;
                        var inets = iface.InetAddresses;
                        if (inets != null)
                        {
                            while (inets.HasMoreElements)
                            {
                                var addr = inets.NextElement() as Java.Net.InetAddress;
                                if (addr == null || addr.IsLoopbackAddress) continue;
                                var host = addr.HostAddress;
                                if (!string.IsNullOrEmpty(host) && host.Contains('.'))
                                {
                                    if (ips.Length > 0) ips.Append("\n");
                                    ips.Append($"http://{host}:5191/");
                                }
                            }
                        }
                    }
                }
            }
            catch { }

            if (ips.Length == 0)
                ips.Append("Keine Netzwerkverbindung\n\nTipp: WiFi-Direct oder Hotspot nutzen");

            _ipText.Text = ips.ToString();
        }

        private void OnStartStopClick(object sender, EventArgs e)
        {
            if (_isRunning)
            {
                _server.Stop();
                _discovery.Stop();
                _isRunning = false;
                LogMessage("Server gestoppt");
            }
            else
            {
                try
                {
                    _server.RequireAccessKey = _authEnabled;
                    _server.Start(5191);
                    _discovery.Start(5191);
                    _isRunning = true;
                    LogMessage($"Server gestartet (Port 5191)");
                    LogMessage($"Zugangsschl\u00fcssel: {( _authEnabled ? _accessKeyLabel.Text : "AUS")}");
                    LogMessage("Auto-Discovery aktiv (UDP Port 5190)");
                }
                catch (Exception ex)
                {
                    Toast.MakeText(this, $"Fehler: {ex.Message}", ToastLength.Long).Show();
                }
            }
            UpdateUI();
        }

        private void OnToggleAuthClick(object sender, EventArgs e)
        {
            _authEnabled = !_authEnabled;
            UpdateUI();
            if (_isRunning)
            {
                _server.RequireAccessKey = _authEnabled;
                LogMessage($"Zugangsschl\u00fcssel {( _authEnabled ? "AKTIVIERT" : "DEAKTIVIERT")}");
            }
        }

        private void UpdateUI()
        {
            var accessKey = SyncServerListener.GetOrCreateAccessKey();

            if (_isRunning)
            {
                _startStopButton.Text = "Server stoppen";
                _startStopButton.SetBackgroundColor(Color.ParseColor("#e74c3c"));
                _statusText.Text = "Server l\u00e4uft \u2713";
                _statusText.SetTextColor(Color.ParseColor("#27ae60"));
                _clientCount.Text = "0 verbundene Clients";

                var ip = _ipText.Text?.Split('\n')[0]?.Trim();
                if (!string.IsNullOrEmpty(ip))
                    GenerateQrCode(ip);
            }
            else
            {
                _startStopButton.Text = "Server starten";
                _startStopButton.SetBackgroundColor(Color.ParseColor("#3498db"));
                _statusText.Text = "Server gestoppt";
                _statusText.SetTextColor(Color.ParseColor("#95a5a6"));
                _qrImage.SetImageBitmap(null);
            }

            // Access-Key Anzeige
            if (!string.IsNullOrEmpty(accessKey))
            {
                _accessKeyLabel.Text = accessKey;
                _accessKeyLabel.Visibility = ViewStates.Visible;
            }
            else
            {
                _accessKeyLabel.Text = "Kein Schl\u00fcssel";
            }

            _toggleAuthButton.Text = _authEnabled ? "Schutz AUS" : "Schutz EIN";
            _toggleAuthButton.SetBackgroundColor(Color.ParseColor(_authEnabled ? "#27ae60" : "#95a5a6"));
        }

        private void GenerateQrCode(string text)
        {
            try
            {
                var writer = new ZXing.QrCode.QRCodeWriter();
                var hints = new System.Collections.Generic.Dictionary<ZXing.EncodeHintType, object>
                {
                    { ZXing.EncodeHintType.MARGIN, 1 }
                };
                var bitMatrix = writer.encode(text, ZXing.BarcodeFormat.QR_CODE, 400, 400, hints);
                int w = bitMatrix.Width, h = bitMatrix.Height;
                var pixels = new int[w * h];
                for (int y = 0; y < h; y++)
                    for (int x = 0; x < w; x++)
                        pixels[y * w + x] = bitMatrix[x, y] ? Color.Black : Color.White;

                var bmp = Bitmap.CreateBitmap(w, h, Bitmap.Config.Rgb565);
                bmp.SetPixels(pixels, 0, w, 0, 0, w, h);
                _qrImage.SetImageBitmap(bmp);
            }
            catch (Exception ex)
            {
                Toast.MakeText(this, $"QR-Fehler: {ex.Message}", ToastLength.Short).Show();
            }
        }

        private void OnClientConnected(string info)
        {
            RunOnUiThread(() => LogMessage(info));
        }

        private void OnError(string msg)
        {
            RunOnUiThread(() =>
            {
                var tv = new TextView(this) { Text = $"\u26a0 {msg}", TextSize = 11 };
                tv.SetTextColor(Color.ParseColor("#e74c3c"));
                _logLayout.AddView(tv, 0);

                if (_logLayout.ChildCount > 50)
                    _logLayout.RemoveViewAt(_logLayout.ChildCount - 1);
            });
        }

        private void LogMessage(string msg)
        {
            RunOnUiThread(() =>
            {
                var tv = new TextView(this) { Text = msg, TextSize = 11 };
                tv.SetTextColor(Color.ParseColor("#666666"));
                _logLayout.AddView(tv, 0);

                var count = _logLayout.ChildCount;
                _clientCount.Text = $"Letzte: {count} Zugriffe";

                if (_logLayout.ChildCount > 50)
                    _logLayout.RemoveViewAt(_logLayout.ChildCount - 1);
            });
        }

        protected override void OnDestroy()
        {
            _server.Stop();
            _discovery.Stop();
            base.OnDestroy();
        }
    }
}
