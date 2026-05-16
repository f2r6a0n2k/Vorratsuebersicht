using Vorratsuebersicht.Client.Services;

namespace Vorratsuebersicht.Client.Pages;

public partial class StoragePage : ContentPage
{
    private readonly LocalDatabase _db;

    public StoragePage(LocalDatabase db)
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
            StorageList.ItemsSource = await _db.GetStorageItemsAsync();
        }
        catch { }
    }
}
