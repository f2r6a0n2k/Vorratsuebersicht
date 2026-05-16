using Vorratsuebersicht.Client.Models;
using Vorratsuebersicht.Client.Services;

namespace Vorratsuebersicht.Client.Pages;

public partial class ArticlesPage : ContentPage
{
    private readonly LocalDatabase _db;
    private List<Article> _allArticles = new();

    public ArticlesPage(LocalDatabase db)
    {
        InitializeComponent();
        _db = db;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await LoadArticlesAsync();
    }

    private async Task LoadArticlesAsync()
    {
        try
        {
            await _db.InitAsync();
            _allArticles = await _db.GetArticlesAsync();
            ArticleList.ItemsSource = _allArticles;
        }
        catch { }
    }

    private void OnSearchTextChanged(object sender, TextChangedEventArgs e)
    {
        var search = e.NewTextValue?.ToLower() ?? "";
        if (string.IsNullOrEmpty(search))
        {
            ArticleList.ItemsSource = _allArticles;
        }
        else
        {
            ArticleList.ItemsSource = _allArticles
                .Where(a => (a.Name?.ToLower().Contains(search) ?? false) ||
                            (a.Category?.ToLower().Contains(search) ?? false) ||
                            (a.Manufacturer?.ToLower().Contains(search) ?? false))
                .ToList();
        }
    }
}
