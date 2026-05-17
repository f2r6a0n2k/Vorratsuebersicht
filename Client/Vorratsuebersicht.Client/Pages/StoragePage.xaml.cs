using Vorratsuebersicht.Client.Models;
using Vorratsuebersicht.Client.Services;

namespace Vorratsuebersicht.Client.Pages;

public partial class StoragePage : ContentPage
{
    private readonly LocalDatabase _db;
    private readonly SyncService _sync;

    public StoragePage(LocalDatabase db, SyncService sync)
    {
        InitializeComponent();
        _db = db;
        _sync = sync;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        try
        {
            await _db.InitAsync();
            await RefreshListAsync();
        }
        catch { }
    }

    private async Task RefreshListAsync()
    {
        StorageList.ItemsSource = null;
        StorageList.ItemsSource = await _db.GetStorageItemsAsync();
    }

    private async void OnItemTapped(object sender, TappedEventArgs e)
    {
        var frame = (Frame)sender;
        var item = (StorageItem)frame.BindingContext;

        var result = await DisplayPromptAsync(
            "Menge ändern",
            $"Neue Menge für {item.ArticleName}:",
            initialValue: item.Quantity.ToString(),
            keyboard: Keyboard.Numeric);

        if (result == null) return;
        if (!int.TryParse(result, out var newQuantity) || newQuantity < 0)
        {
            await DisplayAlert("Fehler", "Bitte eine gültige Zahl eingeben", "OK");
            return;
        }

        try
        {
            item.Quantity = newQuantity;
            await _db.SaveStorageItemAsync(item);

            if (_sync.IsConnected)
            {
                var data = new Dictionary<string, object> { ["quantity"] = newQuantity };
                await _sync.PushChangeAsync("StorageItem", "update", item.StorageItemId, data);
            }

            await RefreshListAsync();
        }
        catch (Exception ex)
        {
            await DisplayAlert("Fehler", $"Konnte Menge nicht speichern: {ex.Message}", "OK");
            await RefreshListAsync();
        }
    }
}
