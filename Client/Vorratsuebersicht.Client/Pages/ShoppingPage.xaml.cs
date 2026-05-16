using Vorratsuebersicht.Client.Services;

namespace Vorratsuebersicht.Client.Pages;

public partial class ShoppingPage : ContentPage
{
    private readonly LocalDatabase _db;

    public ShoppingPage(LocalDatabase db)
    {
        InitializeComponent();
        _db = db;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        try
        {
            await _db.InitAsync();
            ShoppingList.ItemsSource = await _db.GetShoppingItemsAsync();
        }
        catch { }
    }
}
