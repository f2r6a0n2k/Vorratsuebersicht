using Vorratsuebersicht.Client.Models;
using Vorratsuebersicht.Client.Services;

namespace Vorratsuebersicht.Client.Pages;

public partial class ShoppingPage : ContentPage
{
    private readonly LocalDatabase _db;
    private readonly SyncService _sync;
    private bool _isToggling;

    public ShoppingPage(LocalDatabase db, SyncService sync)
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
        ShoppingList.ItemsSource = null;
        ShoppingList.ItemsSource = await _db.GetShoppingItemsAsync();
    }

    private async void OnBoughtToggled(object sender, ToggledEventArgs e)
    {
        if (_isToggling) return;
        _isToggling = true;
        try
        {
            var sw = (Switch)sender;
            var item = (ShoppingItem)sw.BindingContext;
            item.Bought = e.Value;

            await _db.SaveShoppingItemAsync(item);

            if (_sync.IsConnected)
            {
                var data = new Dictionary<string, object>
                {
                    ["isChecked"] = e.Value
                };
                await _sync.PushChangeAsync("ShoppingItem", "update", item.ShoppingListId, data);
            }

            await RefreshListAsync();
        }
        catch (Exception ex)
        {
            await DisplayAlert("Fehler", $"Konnte Status nicht speichern: {ex.Message}", "OK");
            await RefreshListAsync();
        }
        finally
        {
            _isToggling = false;
        }
    }

    private async void OnItemTapped(object sender, TappedEventArgs e)
    {
        var frame = (Frame)sender;
        var item = (ShoppingItem)frame.BindingContext;

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
            await _db.SaveShoppingItemAsync(item);

            if (_sync.IsConnected)
            {
                var data = new Dictionary<string, object> { ["quantity"] = newQuantity };
                await _sync.PushChangeAsync("ShoppingItem", "update", item.ShoppingListId, data);
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
