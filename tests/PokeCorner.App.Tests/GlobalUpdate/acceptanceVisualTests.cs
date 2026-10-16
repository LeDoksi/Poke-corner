using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using PokeCorner.App.Onboarding;
using PokeCorner.App.Popup;
using PokeCorner.App.Tests.Theme;
using PokeCorner.Core;
using PokeCorner.Core.Domain;
using PokeCorner.Core.Persistence;
using PokeCorner.Core.PokeApi;

namespace PokeCorner.App.Tests.GlobalUpdate;

public class AcceptanceVisualTests
{
    [Fact]
    public void CaptureAssembledApplicationMatrix() => WpfTestHost.Run(() =>
    {
        var output = Environment.GetEnvironmentVariable("POKE_SHELL_CAPTURE_DIR");
        Assert.False(string.IsNullOrWhiteSpace(output));
        output = Path.Combine(output!, "assembled");
        Directory.CreateDirectory(output);
        var catalog = CatalogPackage.Load(Path.Combine(AppContext.BaseDirectory, "Data"));
        var lookup = LegacyLookup.Load(catalog.DataRoot);
        var levels = new LevelProgression(catalog.DataRoot);
        var data = SaveMigration.Migrate(new SaveData { Individuals = [], Settings = new() }, lookup, catalog);
        var recipe = new NurseryCatalog(catalog).MixedRecipes.First(r => r.IsAvailable && r.SelectionUseful);
        PokemonIndividual Pet(int id, bool shiny = false)
        {
            var s = catalog.Species.Single(s => s.Id == id);
            return new PokemonIndividual { SpeciesId = id, CurrentSpeciesId = id, FamilyRootId = s.FamilyRootId,
                EvolutionChainId = s.EvolutionChainId, ChainLength = 3, Nature = Nature.Bold, IsShiny = shiny,
                CaughtAtUtc = DateTimeOffset.Parse("2026-10-01T10:00:00Z"), ReachedSpeciesIds = [id],
                FirstTraitId = "gatherer", TotalXp = levels.XpForLevel(15) };
        }
        data.Individuals = [Pet(133), Pet(recipe.FirstParentId), Pet(recipe.SecondParentId), Pet(25, true), Pet(3)];
        data.ActiveIndividualId = data.Individuals[0].Id;
        data.DiscoveredSpeciesIds.UnionWith(data.Individuals.Select(p => p.CurrentSpeciesId));
        data.ShopCoins = 250;
        data.FeedingTreats = 12;
        foreach (var key in data.Berries.Keys.ToArray()) data.Berries[key] = 100;
        foreach (var key in data.Items.Keys.ToArray()) data.Items[key] = 3;
        data.Settings.CrySoundVolume = data.Settings.EffectsSoundVolume = 0;
        FirstRunFlow.RestoreCompanionAndGift(data, catalog);
        var state = new AppState(); state.Hydrate(data);
        var art = RoomArt.Load(catalog.DataRoot);
        var frames = new Dictionary<(int, bool), IReadOnlyList<byte[]>>();
        IReadOnlyList<byte[]>? Frames(int id, bool shiny = false)
        {
            if (frames.TryGetValue((id, shiny), out var cached)) return cached;
            var path = catalog.GetAssetPath(id, shiny);
            return path is null ? null : frames[(id, shiny)] = GifFrameDecoder.DecodeFramesAsPng(File.ReadAllBytes(path));
        }
        var model = new CollectionViewModel(state.Collection, state.CareStateByIndividualId, new CareModeService(),
            lookup.Chains, state.PendingEggs, () => ExpeditionService.BusyIndividualIds(data),
            catalog.Species, () => data.DiscoveredSpeciesIds, catalog.GetRouteHints(),
            p => levels.LevelForXp(p.TotalXp));
        var cacheDirectory = Path.Combine(Path.GetTempPath(), "PokeCorner-T20-visual-" + Guid.NewGuid().ToString("N"));
        var collection = new CollectionView(model, state, new SpriteCache(cacheDirectory), () => { }, () => { }, () => { },
            () => Frames(133), () => false, () => false,
            getAnimatedFrames: (id, shiny) => Task.FromResult(Frames(id, shiny)),
            getSpeciesAssetPath: (id, shiny) => catalog.GetAssetPath(id, shiny),
            previewEvolution: id => new EvolutionService(catalog, data).Preview(id),
            evolveIndividual: (id, branch, op) => Task.FromResult(new EvolutionService(catalog, data).Evolve(id, branch, op)),
            previewRehome: id => new RehomingService(catalog, data).Quote(id),
            rehomeIndividual: (id, op) => Task.FromResult(new RehomingService(catalog, data).Transfer(id, op)),
            previewRetraining: (id, slot) => new RetrainingService(catalog, data).Preview(id, slot),
            retrainIndividual: (id, slot, op) => Task.FromResult(new RetrainingService(catalog, data).Retrain(id, slot, op)));
        var settings = new SettingsView(new SettingsViewModel(state.Settings), () => { });
        var shop = new ShopView(state, new ShopService(catalog.DataRoot), _ => Task.FromException(new IOException("Тест: запись недоступна. Повторите действие.")), () => { }, catalog);
        var catchView = new CatchView(state, () => null, id => Task.FromResult(Frames(id)?.FirstOrDefault()),
            () => 1, () => { }, id => catalog.Species.Single(s => s.Id == id).DisplayName);
        var expedition = new ExpeditionView(() => data, r => new ExpeditionService(catalog, data).Preview(r),
            r => Task.FromResult(new ExpeditionService(catalog, data).Start(r)),
            (id, now) => Task.FromResult(new ExpeditionService(catalog, data).Cancel(id, now)),
            (id, op) => Task.FromResult(new ExpeditionService(catalog, data).Claim(id, op)),
            id => Task.FromResult(new EncounterService(catalog).Open(data, id)), catalog, () => true);
        var nursery = new NurseryView(() => data, (ids, selected) => new NurseryService(catalog, data).Preview(ids, selected),
            r => Task.FromResult(new NurseryService(catalog, data).Start(r)),
            (id, op) => Task.FromResult(new NurseryService(catalog, data).Hatch(id, op)),
            now => Task.FromResult(new NurseryService(catalog, data).RefreshReady(now)), catalog, (_, _) => { }, _ => { },
            _ => Task.CompletedTask);
        var room = new RoomView(art, () => data, () => Frames(133), () => { }, id => catalog.Species.Single(s => s.Id == id).DisplayName);
        var album = new AchievementsView(art, () => data, _ => Task.CompletedTask, () => { });
        var menu = new AppMenuWindow(collection, settings, shop, catchView, expedition, nursery, room, album);
        var root = (FrameworkElement)menu.Content;
        var records = new List<object>();
        void Capture(string name, MenuSection section, bool bottom = false)
        {
            menu.Navigate(section);
            foreach (var size in new[] { (Width: 1000, Height: 900, Scale: 1.5, Suffix: "150"), (Width: 640, Height: 480, Scale: 2d, Suffix: "compact-200") })
            {
                WpfTestHost.Layout(root, size.Width, size.Height);
                foreach (var scroll in Descendants(root).OfType<ScrollViewer>().Where(s => s.IsVisible || s.ActualHeight > 0))
                { if (bottom) scroll.ScrollToEnd(); else scroll.ScrollToTop(); }
                root.UpdateLayout();
                var file = name + "-" + size.Suffix + ".png";
                Render(root, size.Width, size.Height, size.Scale, Path.Combine(output, file));
                records.Add(new { file, size.Width, size.Height, size.Scale, syntheticData = true, nativeWpfRender = true });
            }
        }
        try
        {
            Capture("corner-empty-shelf", MenuSection.Corner);
            foreach (var award in art.Cards.Take(3)) data.AwardLedger[award] = new();
            data.TrophyShelfIds.AddRange(art.Cards.Take(3)); room.Refresh(); album.Refresh();
            Capture("corner-full-shelf", MenuSection.Corner);
            Capture("album-locked-earned", MenuSection.Achievements);
            Capture("album-goals", MenuSection.Achievements, true);
            Capture("companion-card", MenuSection.Pokedex);
            ((TabControl)((Grid)collection.Content).Children[0]).SelectedIndex = 1;
            Capture("pokedex-owned", MenuSection.Pokedex);
            ((Button)collection.FindName("SpeciesModeButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Capture("pokedex-species", MenuSection.Pokedex);
            ((Button)collection.FindName("IndividualsModeButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            collection.OpenIndividual(data.Individuals[0].Id);
            Capture("personal-card-evolution", MenuSection.Pokedex);
            Capture("personal-card-actions", MenuSection.Pokedex, true);
            typeof(CollectionView).GetMethod("HideDetail", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.Invoke(collection, []);
            collection.OpenIndividual(data.Individuals[3].Id);
            Capture("personal-card-shiny", MenuSection.Pokedex);
            typeof(CollectionView).GetMethod("HideDetail", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.Invoke(collection, []);
            Capture("shop-items", MenuSection.Shop);
            Capture("shop-berries", MenuSection.Shop, true);
            typeof(ShopView).GetMethod("ActivateLuckyCharm", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.Invoke(shop, []);
            Assert.Contains("запись недоступна", ((TextBlock)shop.FindName("MessageText")).Text);
            Assert.False(state.Shop.CatchLuckyCharmActive);
            Capture("shop-write-failure", MenuSection.Shop);
            foreach (Button member in ((WrapPanel)expedition.FindName("TeamPanel")).Children.OfType<Button>().Take(3).ToArray())
                member.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            foreach (Button route in expedition.RouteButtons.Children.OfType<Button>())
            {
                route.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                Assert.NotNull(((Image)expedition.FindName("LandscapeImage")).Source);
                Capture("expedition-preparing-" + route.Tag, MenuSection.Expeditions);
            }
            Capture("expedition-preparing-controls", MenuSection.Expeditions, true);
            var tripId = Guid.NewGuid();
            Assert.True(new ExpeditionService(catalog, data).Start(new ExpeditionRequest("grove", "easy", 30,
                data.Individuals.Take(3).Select(p => p.Id).ToArray(), tripId, DateTimeOffset.UtcNow)).Success);
            expedition.Refresh(); Capture("expedition-running", MenuSection.Expeditions);
            data.Expedition!.Status = "ready"; expedition.Refresh(); Capture("expedition-ready", MenuSection.Expeditions);
            new ExpeditionService(catalog, data).Claim(tripId, Guid.NewGuid());
            expedition.Refresh(); Capture("expedition-claimed", MenuSection.Expeditions, true);
            nursery.Refresh();
            ((WrapPanel)nursery.FindName("FirstParentsPanel")).Children.OfType<Button>().First(b => Equals(b.Tag, data.Individuals[1].Id)).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            ((WrapPanel)nursery.FindName("SecondParentsPanel")).Children.OfType<Button>().First(b => Equals(b.Tag, data.Individuals[2].Id)).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Capture("nursery-parents-preview", MenuSection.Nursery);
            var nestId = Guid.NewGuid();
            Assert.True(new NurseryService(catalog, data).Start(new NurseryRequest([data.Individuals[1].Id, data.Individuals[2].Id], null, nestId, DateTimeOffset.UtcNow)).Success);
            nursery.Refresh(); Capture("nursery-running", MenuSection.Nursery);
            data.Nest!.Status = "ready"; nursery.Refresh(); Capture("nursery-ready", MenuSection.Nursery);
            new NurseryService(catalog, data).Hatch(nestId, Guid.NewGuid());
            nursery.Refresh(); Capture("nursery-hatched", MenuSection.Nursery, true);
            Capture("catch-two-sealed-gifts", MenuSection.Catch);
            catchView.RestoreReveal(new CatchReveal(new CatchResult(1, 1, Nature.Bold, false, false, 3), RarityTier.Common));
            Capture("catch-confirmed-result", MenuSection.Catch);
            Capture("settings-zero-audio", MenuSection.Settings);
            Capture("settings-reset-area", MenuSection.Settings, true);
            foreach (var step in new[] { 1, 4, 6 })
            {
                var tour = new OnboardingWindow(step);
                var content = (FrameworkElement)tour.Content;
                WpfTestHost.Layout(content, 640, 480);
                Render(content, 640, 480, 2, Path.Combine(output, $"onboarding-step-{step}-200.png"));
                tour.Close();
            }
            var confirm = new ConfirmDialog("Очистить сохранённые данные?", "Коллекция и ресурсы останутся в истории восстановления. Новая игра начнётся только после второго подтверждения.", "Продолжить");
            WpfTestHost.Layout((FrameworkElement)confirm.Content, 560, 300);
            Render((FrameworkElement)confirm.Content, 560, 300, 2, Path.Combine(output, "reset-confirmation-200.png"));
            confirm.Close();
            File.WriteAllText(Path.Combine(output, "matrix.json"), JsonSerializer.Serialize(records, new JsonSerializerOptions { WriteIndented = true }));
            Assert.True(records.Count >= 50);
        }
        finally
        {
            menu.Content = null;
            menu.Close();
            foreach (var view in new FrameworkElement[] { collection, shop, catchView, expedition, nursery, room })
                view.RaiseEvent(new RoutedEventArgs(FrameworkElement.UnloadedEvent));
            Directory.Delete(cacheDirectory, true);
        }
    });

    private static void Render(FrameworkElement element, int width, int height, double scale, string path)
    {
        var bitmap = new RenderTargetBitmap((int)(width * scale), (int)(height * scale), 96 * scale, 96 * scale, PixelFormats.Pbgra32);
        bitmap.Render(element);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var stream = File.Create(path);
        encoder.Save(stream);
        Assert.True(stream.Length > 1000, path);
    }

    private static IEnumerable<DependencyObject> Descendants(DependencyObject root)
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            yield return child;
            foreach (var nested in Descendants(child)) yield return nested;
        }
    }
}

