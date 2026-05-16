using Vorratsuebersicht.Client.Services;

namespace Vorratsuebersicht.Client.Pages;

public partial class MainPage : ContentPage
{
    private readonly LocalDatabase _db;
    private readonly SyncService _sync;

    public MainPage(LocalDatabase db, SyncService sync)
    {
        InitializeComponent();
        _db = db;
        _sync = sync;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await UpdateStatsAsync();
    }

    public async Task UpdateStatsAsync()
    {
        try
        {
            await _db.InitAsync();

            var articles = await _db.GetArticlesAsync();
            var storage = await _db.GetStorageItemsAsync();
            var shopping = await _db.GetShoppingItemsAsync();

            ArticleCountLabel.Text = articles.Count.ToString();
            StorageCountLabel.Text = storage.Count.ToString();
            ShoppingCountLabel.Text = shopping.Count.ToString();

            if (_sync.IsConnected)
            {
                StatusLabel.Text = "Verbunden";
                StatusLabel.TextColor = Color.FromArgb("#27ae60");
                DetailLabel.Text = $"Master: {_sync.MasterUrl}";
            }
            else
            {
                StatusLabel.Text = "Nicht verbunden";
                StatusLabel.TextColor = Color.FromArgb("#e74c3c");
                DetailLabel.Text = "Tippen Sie auf Sync, um einen Master zu suchen";
            }
        }
        catch { }
    }
}
