using Vorratsuebersicht.Client.Services;

namespace Vorratsuebersicht.Client.Pages;

public partial class SyncPage : ContentPage
{
    private readonly SyncService _sync;
    private readonly LocalDatabase _db;

    public SyncPage(SyncService sync, LocalDatabase db)
    {
        InitializeComponent();
        _sync = sync;
        _db = db;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        var savedKey = await SecureStorage.GetAsync("sync_access_key");
        if (!string.IsNullOrEmpty(savedKey))
            AccessKeyEntry.Text = savedKey;
        await UpdateStatusAsync();
    }

    private async Task UpdateStatusAsync()
    {
        if (_sync.IsConnected && !string.IsNullOrEmpty(_sync.MasterUrl))
        {
            ConnectionStatus.Text = $"Verbunden mit {_sync.MasterUrl}";
            ConnectionStatus.TextColor = Color.FromArgb("#27ae60");
            MasterUrlEntry.Text = _sync.MasterUrl;
            FullSyncButton.IsEnabled = true;
            IncrementalSyncButton.IsEnabled = true;

            var lastSync = await _db.GetLastSyncAsync();
            LastSyncLabel.Text = lastSync > DateTime.MinValue
                ? $"Letzter Sync: {lastSync:g}"
                : "Noch nie synchronisiert";
        }
    }

        private async void OnConnectClicked(object sender, EventArgs e)
        {
            var url = MasterUrlEntry.Text?.Trim();
            if (string.IsNullOrEmpty(url))
            {
                await DisplayAlert("Fehler", "Bitte geben Sie die Master-URL ein", "OK");
                return;
            }

            var accessKey = AccessKeyEntry.Text?.Trim();
            _sync.Configure(url, accessKey);
            var (ok, error) = await _sync.PingWithErrorAsync();
            if (ok)
            {
                await SecureStorage.SetAsync("sync_access_key", accessKey ?? "");
                var info = await _sync.GetDiscoveryInfoAsync();
                ConnectionStatus.Text = $"Verbunden mit {url}";
                ConnectionStatus.TextColor = Color.FromArgb("#27ae60");
                FullSyncButton.IsEnabled = true;
                IncrementalSyncButton.IsEnabled = true;

                var dbId = info?.ContainsKey("databaseId") == true ? info["databaseId"] : "?";
                AddLog($"Verbunden. Datenbank-ID: {dbId}");

                await DisplayAlert("Erfolg", $"Mit Master verbunden!\nDatenbank-ID: {dbId}", "OK");
            }
            else
            {
                ConnectionStatus.Text = $"Fehler: {error}";
                ConnectionStatus.TextColor = Color.FromArgb("#e74c3c");
                AddLog($"Fehler: {url} - {error}");
                await DisplayAlert("Fehler", $"Master unter {url}\n{error}", "OK");
            }
        }

    private async void OnDiscoverClicked(object sender, EventArgs e)
    {
        AddLog("Suche nach Master im Netzwerk...");
        SyncProgress.Progress = 0;

        try
        {
            var masters = await DiscoveryService.DiscoverMastersAsync(3000);

            if (masters.Length == 0)
            {
                AddLog("Keine Master gefunden");
                await DisplayAlert("Info", "Keine Master im Netzwerk gefunden.\n\n" +
                    "Tipps:\n" +
                    "- Starten Sie den Master-Modus in der Vorrats\xfcbersicht-App\n" +
                    "- Beide Ger\xe4te m\xfcssen im selben Netzwerk sein\n" +
                    "- Geben Sie die Adresse manuell ein", "OK");
                return;
            }

            AddLog($"{masters.Length} Master gefunden:");
            foreach (var m in masters)
            {
                var parts = m.Split('|');
                if (parts.Length >= 3)
                {
                    var host = parts[1];
                    var port = parts[2];
                    var dbId = parts.Length > 3 ? parts[3] : "?";
                    var url = $"http://{host}:{port}/";
                    AddLog($"  - {host}:{port} (DB: {dbId})");
                }
            }

            // Ersten Master automatisch verbinden
            var firstMaster = masters[0].Split('|');
            var firstUrl = $"http://{firstMaster[1]}:{firstMaster[2]}/";
            MasterUrlEntry.Text = firstUrl;
            var accessKey = AccessKeyEntry.Text?.Trim();
            _sync.Configure(firstUrl, accessKey);

            var (ok, errMsg) = await _sync.PingWithErrorAsync();
            if (ok)
            {
                await SecureStorage.SetAsync("sync_access_key", accessKey ?? "");
                ConnectionStatus.Text = $"Verbunden mit {firstUrl}";
                ConnectionStatus.TextColor = Color.FromArgb("#27ae60");
                FullSyncButton.IsEnabled = true;
                IncrementalSyncButton.IsEnabled = true;
                AddLog("Automatisch verbunden");
            }
            else
            {
                AddLog($"Fehler: {errMsg}");
            }
        }
        catch (Exception ex)
        {
            AddLog($"Fehler: {ex.Message}");
        }
    }

    private async void OnScanQrClicked(object sender, EventArgs e)
    {
        // QR-Code-Scanning - auf mobilen Ger\xe4ten verf\xfcgbar
        await DisplayAlert("Info", "QR-Code-Scanning ist auf dem Mobilger\xe4t in der Master-App verf\xfcgbar.\n\n" +
            "Scannen Sie den QR-Code in der Master-App, um die URL zu erhalten.", "OK");
    }

    private async void OnFullSyncClicked(object sender, EventArgs e)
    {
        FullSyncButton.IsEnabled = false;
        IncrementalSyncButton.IsEnabled = false;
        SyncProgress.Progress = 0;
        AddLog("Starte vollst\xe4ndige Synchronisation...");

        var progress = new Progress<string>(msg =>
        {
            AddLog(msg);
            SyncProgress.Progress = Math.Min(SyncProgress.Progress + 0.25, 1.0);
        });

        var success = await _sync.FullSyncAsync(progress);
        SyncProgress.Progress = 1;

        if (success)
        {
            AddLog("Vollst\xe4ndige Synchronisation erfolgreich!");
            await DisplayAlert("Erfolg", "Daten wurden erfolgreich synchronisiert", "OK");
        }
        else
        {
            await DisplayAlert("Fehler", "Synchronisation fehlgeschlagen", "OK");
        }

        FullSyncButton.IsEnabled = true;
        IncrementalSyncButton.IsEnabled = true;
        await UpdateStatusAsync();
    }

    private async void OnIncrementalSyncClicked(object sender, EventArgs e)
    {
        FullSyncButton.IsEnabled = false;
        IncrementalSyncButton.IsEnabled = false;
        SyncProgress.Progress = 0;
        AddLog("Starte inkrementelle Synchronisation...");

        var progress = new Progress<string>(msg =>
        {
            AddLog(msg);
        });

        var success = await _sync.IncrementalSyncAsync(progress);
        SyncProgress.Progress = 1;

        if (success)
        {
            AddLog("Inkrementelle Synchronisation erfolgreich!");
        }
        else
        {
            await DisplayAlert("Fehler", "Synchronisation fehlgeschlagen", "OK");
        }

        FullSyncButton.IsEnabled = true;
        IncrementalSyncButton.IsEnabled = true;
        await UpdateStatusAsync();
    }

    private async void OnClearClicked(object sender, EventArgs e)
    {
        var confirm = await DisplayAlert("L\xf6schen?",
            "Alle lokalen Daten werden gel\xf6scht. M\xf6chten Sie fortfahren?", "Ja", "Abbrechen");
        if (!confirm) return;

        await _db.ClearAllAsync();
        AddLog("Lokale Daten gel\xf6scht");
        await UpdateStatusAsync();
    }

    private void AddLog(string msg)
    {
        var current = LogLabel.Text;
        var timestamp = DateTime.Now.ToString("HH:mm:ss");
        if (current == "Keine Eintr\xe4ge")
            current = "";
        else
            current += "\n";
        LogLabel.Text = current + $"[{timestamp}] {msg}";
    }
}
