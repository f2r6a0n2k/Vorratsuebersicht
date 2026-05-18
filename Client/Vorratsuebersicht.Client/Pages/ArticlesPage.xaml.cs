using Vorratsuebersicht.Client.Models;
using Vorratsuebersicht.Client.Services;

namespace Vorratsuebersicht.Client.Pages;

public partial class ArticlesPage : ContentPage
{
    private readonly LocalDatabase _db;
    private readonly SyncService _sync;
    private List<Article> _allArticles = new();

    public ArticlesPage(LocalDatabase db, SyncService sync)
    {
        InitializeComponent();
        _db = db;
        _sync = sync;
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

    private async void OnArticleTapped(object sender, TappedEventArgs e)
    {
        var frame = (Frame)sender;
        var article = (Article)frame.BindingContext;

        var action = await DisplayActionSheet(
            article.Name,
            "Abbrechen",
            null,
            "Details anzeigen",
            "Ins Lager aufnehmen",
            "Auf Einkaufsliste setzen");

        switch (action)
        {
            case "Details anzeigen":
                await ShowDetailsAsync(article);
                break;
            case "Ins Lager aufnehmen":
                await AddToStorageAsync(article);
                break;
            case "Auf Einkaufsliste setzen":
                await AddToShoppingListAsync(article);
                break;
        }
    }

    private async Task ShowDetailsAsync(Article article)
    {
        var details = new List<string>();
        if (!string.IsNullOrEmpty(article.Manufacturer)) details.Add($"Hersteller: {article.Manufacturer}");
        if (!string.IsNullOrEmpty(article.Category)) details.Add($"Kategorie: {article.Category}");
        if (!string.IsNullOrEmpty(article.SubCategory)) details.Add($"Unterkategorie: {article.SubCategory}");
        if (!string.IsNullOrEmpty(article.EANCode)) details.Add($"EAN: {article.EANCode}");
        if (article.Size.HasValue) details.Add($"Größe: {article.Size} {article.Unit ?? ""}");
        if (article.Price.HasValue) details.Add($"Preis: {article.Price:F2} €");
        if (!string.IsNullOrEmpty(article.StorageName)) details.Add($"Lagerort: {article.StorageName}");
        if (article.MinQuantity.HasValue) details.Add($"Mindestmenge: {article.MinQuantity}");
        if (article.PrefQuantity.HasValue) details.Add($"Vorzugsmenge: {article.PrefQuantity}");
        if (!string.IsNullOrEmpty(article.Supermarket)) details.Add($"Supermarkt: {article.Supermarket}");
        if (!string.IsNullOrEmpty(article.Notes)) details.Add($"Notizen: {article.Notes}");
        if (article.DurableInfinity) details.Add("Unbegrenzt haltbar");
        if (article.WarnInDays.HasValue) details.Add($"Warnung in {article.WarnInDays} Tagen");
        if (article.Calorie.HasValue) details.Add($"Kalorien: {article.Calorie}");

        var text = details.Count > 0 ? string.Join("\n", details) : "Keine weiteren Details";
        await DisplayAlert(article.Name, text, "OK");
    }

    private async Task AddToStorageAsync(Article article)
    {
        var quantityStr = await DisplayPromptAsync(
            "Ins Lager aufnehmen",
            $"Menge für {article.Name}:",
            initialValue: "1",
            keyboard: Keyboard.Numeric);
        if (quantityStr == null) return;
        if (!int.TryParse(quantityStr, out var quantity) || quantity < 1)
        {
            await DisplayAlert("Fehler", "Bitte eine gültige Zahl eingeben", "OK");
            return;
        }

        var bestBefore = await DisplayPromptAsync(
            "Mindesthaltbarkeitsdatum",
            "MHD (TT.MM.JJJJ) – leer lassen, falls unbekannt:",
            placeholder: "z.B. 31.12.2026");
        if (bestBefore == null) return;

        try
        {
            var data = new Dictionary<string, object>
            {
                ["articleId"] = article.ArticleId,
                ["quantity"] = quantity
            };
            if (!string.IsNullOrWhiteSpace(bestBefore))
            {
                if (DateTime.TryParse(bestBefore, out var dt))
                    data["bestBeforeDate"] = dt.ToString("yyyy-MM-dd");
                else
                {
                    await DisplayAlert("Fehler", "Ungültiges Datum. Bitte TT.MM.JJJJ eingeben.", "OK");
                    return;
                }
            }

            var (ok, err) = await _sync.PushChangeAsync("StorageItem", "create", null, data);
            if (!ok)
            {
                await DisplayAlert("Fehler", $"Konnte nicht zum Master senden:\n{err}", "OK");
                return;
            }

            var refreshed = await _sync.RefreshStorageItemsAsync();
            var msg = refreshed
                ? $"{article.Name} ({quantity}x) wurde ins Lager aufgenommen"
                : $"{article.Name} ({quantity}x) wurde an Master gesendet, aber lokale Daten konnten nicht aktualisiert werden";
            await DisplayAlert("Erfolg", msg, "OK");
        }
        catch (Exception ex)
        {
            await DisplayAlert("Fehler", ex.Message, "OK");
        }
    }

    private async Task AddToShoppingListAsync(Article article)
    {
        var result = await DisplayPromptAsync(
            "Auf Einkaufsliste setzen",
            $"Menge für {article.Name}:",
            initialValue: "1",
            keyboard: Keyboard.Numeric);
        if (result == null) return;
        if (!int.TryParse(result, out var quantity) || quantity < 1)
        {
            await DisplayAlert("Fehler", "Bitte eine gültige Zahl eingeben", "OK");
            return;
        }

        try
        {
            var data = new Dictionary<string, object>
            {
                ["articleId"] = article.ArticleId,
                ["quantity"] = quantity
            };
            var (ok, err) = await _sync.PushChangeAsync("ShoppingItem", "create", null, data);
            if (!ok)
            {
                await DisplayAlert("Fehler", $"Konnte nicht zum Master senden:\n{err}", "OK");
                return;
            }

            var refreshed = await _sync.RefreshShoppingItemsAsync();
            var msg = refreshed
                ? $"{article.Name} ({quantity}x) wurde auf die Einkaufsliste gesetzt"
                : $"{article.Name} ({quantity}x) wurde an Master gesendet, aber lokale Daten konnten nicht aktualisiert werden";
            await DisplayAlert("Erfolg", msg, "OK");
        }
        catch (Exception ex)
        {
            await DisplayAlert("Fehler", ex.Message, "OK");
        }
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
