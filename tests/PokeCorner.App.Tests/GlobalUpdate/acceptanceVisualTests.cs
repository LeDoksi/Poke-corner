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
    public Task CaptureAssembledApplicationMatrix() => CaptureApplication(false);

    [Fact]
    public Task CaptureTargetedCompositionContinuation() => CaptureApplication(true);

    [Fact]
    public Task CapturePinnedEvolutionFooterContinuation() => CaptureApplication(true, true);

    [Fact]
    public Task CaptureHierarchyContinuation() => CaptureApplication(true, hierarchy: true);

    [Fact]
    public Task CaptureDailyUseFinish() => CaptureApplication(true, dailyUse: true);

    [Fact]
    public Task CaptureDailyUseFinalDetails() => CaptureApplication(true, dailyUse: true, dailyDetailsOnly: true);

    private async Task CaptureApplication(bool targeted, bool evolutionOnly = false, bool hierarchy = false, bool dailyUse = false, bool dailyDetailsOnly = false)
    {
        var cancellations = new List<Task<bool>>();
        WpfTestHost.Run(() =>
    {
        var output = Environment.GetEnvironmentVariable("POKE_SHELL_CAPTURE_DIR");
        Assert.False(string.IsNullOrWhiteSpace(output));
        output = Path.Combine(output!, dailyUse ? "daily-use" : hierarchy ? "hierarchy" : evolutionOnly ? "evolution-footer" : targeted ? "targeted" : "assembled");
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
        data.Individuals = [Pet(133), Pet(recipe.FirstParentId), Pet(recipe.SecondParentId), Pet(25, true), Pet(689), Pet(1)];
        data.Individuals[4].Nickname = "Очень длинная кириллическая кличка Каменистого Берега";
        for (var i = 0; i < 794; i++) { var pet = Pet(1); pet.Nickname = "Длинная кириллическая кличка " + i; data.Individuals.Add(pet); }
        data.ActiveIndividualId = data.Individuals[0].Id;
        data.DiscoveredSpeciesIds.UnionWith(data.Individuals.Select(p => p.CurrentSpeciesId));
        data.NormalSpeciesIds.UnionWith(data.Individuals.Where(p => !p.IsShiny).Select(p => p.CurrentSpeciesId));
        data.ShinySpeciesIds.Add(25);
        data.Individuals[0].AccumulatedCarePoints = 40000;
        data.CatchAccumulatorUnits = PokeCorner.Core.Activity.ActivityAccumulator.CatchCheckIntervalUnits / 2;
        data.FeedingRemainderUnits = FeedingService.ActivityUnitsPerTreatEarned / 2;
        data.ShopCoins = 12500;
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
        var presentation = new CollectionPresentation(catalog);
        var model = new CollectionViewModel(state.Collection, state.CareStateByIndividualId, new CareModeService(),
            lookup.Chains, state.PendingEggs, () => ExpeditionService.BusyIndividualIds(data),
            catalog.Species, () => data.DiscoveredSpeciesIds, catalog.GetRouteHints(),
            p => levels.LevelForXp(p.TotalXp), () => state.NormalSpeciesIds, () => state.ShinySpeciesIds, presentation);
        var cacheDirectory = Path.Combine(Path.GetTempPath(), "PokeCorner-T20-visual-" + Guid.NewGuid().ToString("N"));
        var goods = new ShopService(catalog.DataRoot);
        var collection = new CollectionView(model, state, new SpriteCache(cacheDirectory), () => { }, () => { }, () => { },
            () => Frames(133), () => false, () => false,
            getAnimatedFrames: (id, shiny) => Task.FromResult(Frames(id, shiny)),
            getSpeciesAssetPath: (id, shiny) => catalog.GetAssetPath(id, shiny),
            previewEvolution: id => new EvolutionService(catalog, data).Preview(id),
            evolveIndividual: (id, branch, op) => Task.FromResult(new EvolutionService(catalog, data).Evolve(id, branch, op)),
            previewRehome: id => new RehomingService(catalog, data).Quote(id),
            rehomeIndividual: (id, op) => Task.FromResult(new RehomingService(catalog, data).Transfer(id, op)),
            previewRetraining: (id, slot) => new RetrainingService(catalog, data).Preview(id, slot),
            retrainIndividual: (id, slot, op) => Task.FromResult(new RetrainingService(catalog, data).Retrain(id, slot, op)),
            previewItem: (id, item) => new IndividualItemPreview(item, goods.Goods.Single(g => g.Id == item).Name,
                data.Items.GetValueOrDefault(item), item == "care-booster" ? "Добавляет уход выбранному питомцу." : "Добавляет опыт выбранному питомцу.", null),
            useItem: (id, item, op) => Task.FromResult(item == "care-booster" ? goods.TryUseCareBooster(data, id, op)
                : goods.TryUseXpPotion(data, id, op, levels, catalog)),
            xpProgress: id => {
                var pet = data.Individuals.Single(p => p.Id == id); var level = levels.LevelForXp(pet.TotalXp);
                var start = levels.XpForLevel(level); var end = level >= 40 ? start : levels.XpForLevel(level + 1);
                return level >= 40 ? ("Максимальный уровень", 1d) : ($"До следующего уровня — {end - pet.TotalXp} опыта",
                    (pet.TotalXp-start)/(double)(end-start));
            },
            whereFind: (id, shiny) => presentation.WhereFind(id, shiny, shiny ? data.ShinySpeciesIds : data.NormalSpeciesIds),
            speciesPaths: (id, shiny) => presentation.Paths(id, shiny, shiny ? data.ShinySpeciesIds : data.NormalSpeciesIds),
            openRoute: _ => { }, openPair: (_, _) => null);
        var settings = new SettingsView(new SettingsViewModel(state.Settings), () => { }, () => { }, () => { },
            getAutostart: () => false, setAutostart: _ => { });
        var shop = new ShopView(state, new ShopService(catalog.DataRoot), _ => Task.FromException(new IOException("Тест: запись недоступна. Повторите действие.")), () => { }, catalog);
        var motion = true;
        var revealDelays = new Queue<(TimeSpan Duration, TaskCompletionSource Completion)>();
        Task RevealDelay(TimeSpan duration, CancellationToken token)
        {
            var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            token.Register(() => completion.TrySetCanceled(token));
            revealDelays.Enqueue((duration, completion));
            return completion.Task;
        }
        var catchView = new CatchView(state, () => null, id => Task.FromResult(Frames(id)?.FirstOrDefault()),
            () => 1, () => { }, id => catalog.Species.Single(s => s.Id == id).DisplayName,
            animationEnabled: () => motion, delayAsync: RevealDelay,
            fetchVariantSprite: (id, shiny) => Task.FromResult(Frames(id, shiny)?.FirstOrDefault()),
            fetchScene: _ => File.ReadAllBytes(Path.Combine(catalog.DataRoot, "expeditions", "grove.png")),
            luckyCharmArtPath: Path.Combine(catalog.DataRoot, "final", "assets", "shop", "lucky-charm.png"));
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
        var room = new RoomView(art, () => data, () => Frames(133), () => { },
            id => catalog.Species.Single(s => s.Id == id).DisplayName, catalog, () => { }, () => { },
            id => collection.OpenIndividual(id), () => { });
        var album = new AchievementsView(art, () => data, _ => Task.CompletedTask, () => { });
        var menu = new AppMenuWindow(collection, settings, shop, catchView, expedition, nursery, room, album, () => data);
        menu.SetCatchReady(CatchBallQueue.GiftReadyCount(state.Encounters));
        var root = (FrameworkElement)menu.Content;
        var records = new List<object>();
        var keys = new HashSet<string>();
        void Click(FrameworkElement view, string name) => ((Button)view.FindName(name)).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        void Invoke(object target, string name, params object[] args) => target.GetType().GetMethod(name,
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.Invoke(target, args);
        void Capture(string key, MenuSection section, string? variant = null, FrameworkElement? action = null, bool navigate = true)
        {
            if (navigate) menu.Navigate(section);
            keys.Add(key);
            // POKE_ND_SIZES=1: the Night Diary review sizes (D-10 minimum and maximized FullHD) instead of the v2 pair.
            var sizes = Environment.GetEnvironmentVariable("POKE_ND_SIZES") == "1"
                ? new[] { (Width: 1200, Height: 660, Scale: 1d, Suffix: "min"), (Width: 1920, Height: 1040, Scale: 1d, Suffix: "fhd") }
                : new[] { (Width: 1000, Height: 760, Scale: 1.5, Suffix: "base-150"), (Width: 840, Height: 650, Scale: 2d, Suffix: "compact-200") };
            foreach (var size in sizes)
            {
                WpfTestHost.Layout(root, size.Width, size.Height);
                foreach (var scroll in Descendants(root).OfType<ScrollViewer>().Where(s => s.ActualHeight > 0)) scroll.ScrollToTop();
                root.UpdateLayout();
                var file = key + (variant is null ? "" : "-" + variant) + "-" + size.Suffix + ".png";
                Render(root, size.Width, size.Height, size.Scale, Path.Combine(output, file));
                string? actionFile = null;
                Rect? actionBounds = null;
                if (targeted && key == "collection")
                {
                    var card = Descendants(root).OfType<Border>().First(b => b.DataContext is PokedexCard);
                    var cardBounds = card.TransformToAncestor(root).TransformBounds(new Rect(card.RenderSize));
                    Assert.True(cardBounds.Bottom <= size.Height, $"First collection card clipped: {cardBounds}");
                }
                if (targeted && key == "detail" && variant == "supported-eevee" && size.Width == 1000)
                {
                    foreach (var name in new[] { "DetailDescriptionText", "DetailRehomeButton", "DetailSelectButton" })
                    {
                        var target = (FrameworkElement)collection.FindName(name);
                        var rect = target.TransformToAncestor(root).TransformBounds(new Rect(target.RenderSize));
                        Assert.True(rect.Bottom <= size.Height && rect.Top >= 0, $"{name} outside base viewport: {rect}");
                    }
                }
                if ((hierarchy || dailyUse) && (key == "expedition-selected" || key == "nursery" && variant == "selected-pinned-footer"))
                {
                    var view = key == "nursery" ? (FrameworkElement)nursery : expedition;
                    var names = key == "nursery"
                        ? new[] { "StartButton", "FirstSlot", "SecondSlot", "ChooseFirstButton", "ChooseSecondButton", "ResourcePricesPanel", "CostText", "ShortageText" }
                        : new[] { "StartButton", "TeamSlots", "ChooseTeamButton", "PreviewText", "PreviewRewardCards" };
                    foreach (var name in names)
                    {
                        var target = (FrameworkElement)view.FindName(name);
                        var bounds = target.TransformToAncestor(root).TransformBounds(new Rect(target.RenderSize));
                        Assert.True(bounds.Top >= 0 && bounds.Left >= 0 && bounds.Bottom <= size.Height && bounds.Right <= size.Width,
                            $"{key}/{name} outside initial viewport: {bounds}");
                    }
                }
                if (targeted && key == "evolution")
                {
                    foreach (var name in new[] { "EvolutionLaterButton", "DetailEvolveButton", "DetailEvolutionShopButton", "DetailEvolutionRouteButton" })
                    {
                        var button = (Button)collection.FindName(name);
                        var rect = button.TransformToAncestor(root).TransformBounds(new Rect(button.RenderSize));
                        Assert.True(rect.Bottom <= size.Height && rect.Top >= 0 && rect.Left >= 0 && rect.Right <= size.Width,
                            $"{name} outside initial viewport: {rect}");
                    }
                }
                if (action is not null)
                {
                    Assert.Equal(Visibility.Visible, action.Visibility);
                    action.BringIntoView(); root.UpdateLayout();
                    var bounds = action.TransformToAncestor(root).TransformBounds(new Rect(action.RenderSize));
                    Assert.True(bounds.Top >= 0 && bounds.Left >= 0 && bounds.Bottom <= size.Height && bounds.Right <= size.Width,
                        $"{key}/{action.Name}: {bounds} outside {size.Width}x{size.Height}");
                    actionBounds = bounds;
                    actionFile = Path.GetFileNameWithoutExtension(file) + "-action.png";
                    Render(root, size.Width, size.Height, size.Scale, Path.Combine(output, actionFile));
                }
                records.Add(new { reference = key, variant, file, actionFile, actionBounds, action = action?.Name, size.Width, size.Height, size.Scale,
                    individuals = data.Individuals.Count, syntheticData = true, nativeWpfRender = true,
                    imagesWithSource = Descendants(root).OfType<Image>().Count(i => i.ActualHeight > 0 && i.Source is not null) });
            }
        }
        // Destructive overlays are captured the way their real callers raise them (Оставить + danger button, D-14).
        void Overlay(string key, string title, string body, string caption, bool destructive = false)
        {
            var task = destructive ? menu.ConfirmDestructiveAsync(title, body, caption) : menu.ConfirmAsync(title, body, caption);
            var overlay = (FrameworkElement)menu.FindName("ConfirmationHost");
            Capture(key, MenuSection.Settings, action: (FrameworkElement)overlay.FindName("CancelButton"), navigate: false);
            Click(overlay, "CancelButton");
            Assert.True(task.IsCompletedSuccessfully); cancellations.Add(task);
        }
        void SaveProof()
        {
                File.WriteAllText(Path.Combine(output, "matrix.json"), JsonSerializer.Serialize(records, new JsonSerializerOptions { WriteIndented = true }));
                File.WriteAllText(Path.Combine(output, "capture-proof.json"), JsonSerializer.Serialize(Directory.GetFiles(output, "*.png").Order().Select(path => new {
                    file = Path.GetFileName(path), bytes = new FileInfo(path).Length,
                    sha256 = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(path)))
                }), new JsonSerializerOptions { WriteIndented = true }));
        }
        try
        {
            if (dailyUse)
            {
                if (dailyDetailsOnly)
                {
                    data.Individuals[0].AccumulatedCarePoints = data.Individuals[5].AccumulatedCarePoints = 200000;
                    menu.Navigate(MenuSection.Pokedex); collection.OpenIndividual(data.Individuals[0].Id);
                    Click(collection, "DetailNameButton"); ((TextBox)collection.FindName("DetailNicknameBox")).Text = "Длинная кличка друга";
                    Assert.Equal("Eevee", ((TextBlock)collection.FindName("DetailSpeciesSubtitleText")).Text);
                    Capture("detail", MenuSection.Pokedex, "name-editor"); Click(collection, "DetailNicknameCancelButton");
                    data.Individuals[0].Nickname = "Очень длинная кличка"; collection.OpenIndividual(data.Individuals[0].Id);
                    Capture("detail", MenuSection.Pokedex, "long-nickname");
                    Click(collection, "DetailRetrainingToggle");
                    var cost = ((TextBlock)collection.FindName("DetailRetrainingCostText")).Text;
                    Assert.Contains("в запасе", cost); Assert.DoesNotContain("aspear", cost);
                    Capture("detail", MenuSection.Pokedex, "retraining", (FrameworkElement)collection.FindName("DetailRetrainButton"));
                    Invoke(collection, "HideDetail"); collection.OpenIndividual(data.Individuals[5].Id); Click(collection, "DetailConditionsButton");
                    Assert.Equal(Visibility.Collapsed, ((ComboBox)collection.FindName("DetailEvolutionBranchPicker")).Visibility);
                    Capture("evolution", MenuSection.Pokedex, "linear", (FrameworkElement)collection.FindName("DetailEvolveButton"));
                    SaveProof(); return;
                }
                void ChooseParents()
                {
                    Click(nursery, "ChooseFirstButton");
                    foreach (var pet in data.Individuals.Skip(1).Take(2))
                    {
                        ((TextBox)nursery.FindName("ParentSearchBox")).Text = pet.CurrentSpeciesId.ToString();
                        var candidates = (WrapPanel)nursery.FindName("PickerIndividualsPanel");
                        while (!candidates.Children.OfType<Button>().Any(b => Equals(b.Tag, pet.Id)))
                        {
                            Assert.True(((Button)nursery.FindName("PickerNextButton")).IsEnabled); Click(nursery, "PickerNextButton");
                        }
                        candidates.Children.OfType<Button>().Single(b => Equals(b.Tag, pet.Id)).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                    }
                    Click(nursery, "PickerSaveButton");
                }
                Capture("collection", MenuSection.Pokedex);
                Click(collection, "SpeciesModeButton"); Capture("species", MenuSection.Pokedex);
                var unknown = ((ListBox)collection.FindName("SpeciesGrid")).Items.Cast<object>().First(item => {
                    var entry = (PokedexSpeciesEntry)item.GetType().GetProperty("Entry")!.GetValue(item)!;
                    return !entry.IsDiscovered;
                });
                var unknownEntry = (PokedexSpeciesEntry)unknown.GetType().GetProperty("Entry")!.GetValue(unknown)!;
                Invoke(collection, "SpeciesWhere_Click", new Button { Tag = unknownEntry.SpeciesId }, new RoutedEventArgs());
                Capture("species", MenuSection.Pokedex, "where-find"); Invoke(collection, "CloseSpeciesDialog");
                Click(collection, "IndividualsModeButton");
                data.Individuals[0].AccumulatedCarePoints = data.Individuals[5].AccumulatedCarePoints = 200000;
                collection.OpenIndividual(data.Individuals[0].Id);
                Capture("detail", MenuSection.Pokedex, "supported-eevee");
                Click(collection, "DetailNameButton"); ((TextBox)collection.FindName("DetailNicknameBox")).Text = "Длинная кличка друга";
                Capture("detail", MenuSection.Pokedex, "name-editor"); Click(collection, "DetailNicknameCancelButton");
                Click(collection, "DetailRetrainingToggle");
                Capture("detail", MenuSection.Pokedex, "retraining", (FrameworkElement)collection.FindName("DetailRetrainButton"));
                Invoke(collection, "HideDetail"); collection.OpenIndividual(data.Individuals[5].Id); Click(collection, "DetailConditionsButton");
                Capture("evolution", MenuSection.Pokedex, "linear", (FrameworkElement)collection.FindName("DetailEvolveButton"));
                Invoke(collection, "HideDetail");
                Capture("nursery", MenuSection.Nursery, "unselected"); ChooseParents();
                Capture("nursery", MenuSection.Nursery, "selected-pinned-footer");
                ((StackPanel)nursery.FindName("OutcomePanel")).Children.OfType<Button>().Last().RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                Assert.Contains("Талисман потомства ×1", ((TextBlock)nursery.FindName("CostText")).Text);
                Capture("nursery", MenuSection.Nursery, "selected-charm");
                Capture("expedition", MenuSection.Expeditions, "unselected"); Click(expedition, "ChooseTeamButton");
                Capture("expedition-team", MenuSection.Expeditions, "duplicates");
                foreach (var pet in data.Individuals.Skip(1).Take(3))
                {
                    ((TextBox)expedition.FindName("PickerSearchBox")).Text = pet.CurrentSpeciesId.ToString();
                    ((WrapPanel)expedition.FindName("PickerMembers")).Children.OfType<Button>().First(b => Equals(b.Tag, pet.Id)).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                }
                Click(expedition, "PickerDoneButton"); Capture("expedition-selected", MenuSection.Expeditions, "pinned-footer");
                Capture("shop", MenuSection.Shop, "stepper");
                menu.Navigate(MenuSection.Pokedex); collection.OpenIndividual(data.Individuals[5].Id);
                Click(collection, "DetailRehomeButton");
                var rehomeOverlay = (FrameworkElement)menu.FindName("ConfirmationHost");
                Assert.Same(menu.FindResource("UiDangerButtonStyle"), ((Button)rehomeOverlay.FindName("ConfirmButton")).Style);
                Capture("confirm-rehome", MenuSection.Pokedex, "actual-caller", navigate: false);
                Click(rehomeOverlay, "CancelButton");
                Invoke(collection, "HideDetail");
                var knownEncounter = new EncounterState { Id = Guid.NewGuid(), Source = "expedition", Phase = "resolved", KnownSpeciesId = 133, Individual = Pet(133), Rarity = RarityTier.Common };
                state.Encounters.Add(knownEncounter); catchView.SelectEncounter(knownEncounter.Id);
                Capture("encounter", MenuSection.Catch, "source-explanation");
                SaveProof();
                return;
            }
            if (hierarchy)
            {
                data.Individuals[0].AccumulatedCarePoints = 200000;
                menu.Navigate(MenuSection.Pokedex); collection.OpenIndividual(data.Individuals[0].Id);
                Capture("detail", MenuSection.Pokedex, "supported-eevee", (FrameworkElement)collection.FindName("DetailRehomeButton"));
                Assert.NotNull(((Image)collection.FindName("DetailCareBoosterImage")).Source);
                Assert.NotNull(((Image)collection.FindName("DetailXpPotionImage")).Source);
                Assert.StartsWith("Характер:", ((TextBlock)collection.FindName("DetailNatureText")).Text);
                Invoke(collection, "HideDetail");
                menu.Navigate(MenuSection.Expeditions); Click(expedition, "ChooseTeamButton");
                Capture("expedition-team", MenuSection.Expeditions, "search-role", (FrameworkElement)expedition.FindName("PickerDoneButton"));
                foreach (var pet in data.Individuals.Skip(1).Take(3))
                {
                    ((TextBox)expedition.FindName("PickerSearchBox")).Text = pet.CurrentSpeciesId.ToString();
                    ((WrapPanel)expedition.FindName("PickerMembers")).Children.OfType<Button>().First(b => Equals(b.Tag, pet.Id))
                        .RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                }
                Click(expedition, "PickerDoneButton");
                Capture("expedition-selected", MenuSection.Expeditions, "pinned-footer", (FrameworkElement)expedition.FindName("StartButton"));
                menu.Navigate(MenuSection.Nursery); Click(nursery, "ChooseFirstButton");
                Capture("nursery-select", MenuSection.Nursery, "search-role", (FrameworkElement)nursery.FindName("PickerSaveButton"));
                foreach (var pet in data.Individuals.Skip(1).Take(2))
                {
                    ((TextBox)nursery.FindName("ParentSearchBox")).Text = pet.CurrentSpeciesId.ToString();
                    var candidates = (WrapPanel)nursery.FindName("PickerIndividualsPanel");
                    while (!candidates.Children.OfType<Button>().Any(b => Equals(b.Tag, pet.Id)))
                    {
                        Assert.True(((Button)nursery.FindName("PickerNextButton")).IsEnabled);
                        Click(nursery, "PickerNextButton");
                    }
                    candidates.Children.OfType<Button>().Single(b => Equals(b.Tag, pet.Id)).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                }
                Click(nursery, "PickerSaveButton");
                Capture("nursery", MenuSection.Nursery, "selected-pinned-footer", (FrameworkElement)nursery.FindName("StartButton"));
                var choices = ((StackPanel)nursery.FindName("OutcomePanel")).Children.OfType<Button>().ToArray();
                Assert.Equal(1 + new NurseryService(catalog, data).Preview(data.Individuals.Skip(1).Take(2).Select(p => p.Id).ToArray()).Outcomes.Count, choices.Length);
                var nurseryPreview = new NurseryService(catalog, data).Preview(data.Individuals.Skip(1).Take(2).Select(p => p.Id).ToArray());
                Assert.Equal(nurseryPreview.Outcomes.Count(o => !data.NormalSpeciesIds.Contains(o.SpeciesId) && catalog.GetAssetPath(o.SpeciesId, false) is not null),
                    Descendants((DependencyObject)nursery.FindName("OutcomePanel")).OfType<Border>().Count(b => b.OpacityMask is ImageBrush));
                Capture("shop", MenuSection.Shop, "native-quantity");
                Capture("settings", MenuSection.Settings, "visible-back");
                SaveProof();
                return;
            }
            if (targeted)
            {
                data.Individuals[0].AccumulatedCarePoints = 200000;
                menu.Navigate(MenuSection.Pokedex);
                collection.OpenIndividual(data.Individuals[0].Id);
                if (!evolutionOnly)
                {
                    Invoke(collection, "HideDetail");
                    Capture("collection", MenuSection.Pokedex);
                    collection.OpenIndividual(data.Individuals[0].Id);
                Assert.NotNull(((Image)collection.FindName("DetailSpriteImage")).Source);
                Capture("detail", MenuSection.Pokedex, "supported-eevee", (FrameworkElement)collection.FindName("DetailDescriptionText"));
                Capture("detail", MenuSection.Pokedex, "eevee-rehome", (FrameworkElement)collection.FindName("DetailRehomeButton"));
                data.Individuals[0].Nickname = "Очень длинная кличка";
                Invoke(collection, "HideDetail"); collection.OpenIndividual(data.Individuals[0].Id);
                Capture("detail", MenuSection.Pokedex, "eevee-long-nickname", (FrameworkElement)collection.FindName("DetailCareBoosterButton"));
                }
                Click(collection, "DetailConditionsButton");
                Capture("evolution", MenuSection.Pokedex, "without-stone", (FrameworkElement)collection.FindName("DetailEvolveButton"));
                ((CheckBox)collection.FindName("DetailEvolutionStoneToggle")).IsChecked = true;
                Capture("evolution", MenuSection.Pokedex, "with-stone", (FrameworkElement)collection.FindName("DetailEvolveButton"));
                Assert.NotNull(((Image)collection.FindName("EvolutionBerryImage")).Source);
                Assert.NotNull(((Image)collection.FindName("DetailStoneImage")).Source);
                if (!evolutionOnly)
                {
                Invoke(collection, "HideDetail"); collection.OpenIndividual(data.Individuals[4].Id);
                Assert.True(((TextBlock)collection.FindName("DetailDescriptionText")).Text.Length >= 157);
                Assert.Null(((Image)collection.FindName("DetailSpriteImage")).Source);
                Capture("detail", MenuSection.Pokedex, "longest-fact-689-visible", (FrameworkElement)collection.FindName("DetailDescriptionText"));
                var family = (ItemsControl)collection.FindName("DetailEvolutionChainStrip");
                var familyStages = Descendants(family).OfType<Border>().Where(b => b.ReadLocalValue(FrameworkElement.DataContextProperty) is EvolutionStageCard)
                    .Select(b => (EvolutionStageCard)b.DataContext).ToArray();
                Assert.Equal(2, familyStages.Length);
                Assert.Equal("#0688", familyStages[0].NameText); Assert.False(familyStages[0].IsRevealed);
                Assert.NotNull(familyStages[0].SilhouetteImage);
                Assert.True(familyStages[1].IsCurrent); Assert.Contains("Barbaracle", familyStages[1].NameText);
                var tour = new OnboardingWindow(5, dataRoot: catalog.DataRoot);
                var content = tour.TakeHostedContent(); menu.ShowHostedAsync(content);
                Capture("tour-5", MenuSection.Corner, "silhouettes", (FrameworkElement)tour.FindName("NextButton"), navigate: false);
                Assert.Equal(2, Descendants(content).OfType<Border>().Count(b => b.OpacityMask is ImageBrush));
                menu.CompleteHostedContent(content); tour.Close();
                }
                if (evolutionOnly)
                {
                    menu.Show();
                    var later = (Button)collection.FindName("EvolutionLaterButton");
                    var evolve = (Button)collection.FindName("DetailEvolveButton");
                    Assert.True(later.Focus());
                    Assert.Same(later, System.Windows.Input.Keyboard.FocusedElement);
                    Assert.True(later.MoveFocus(new System.Windows.Input.TraversalRequest(System.Windows.Input.FocusNavigationDirection.Next)));
                    Assert.Same(evolve, System.Windows.Input.Keyboard.FocusedElement);
                    void InvokeButton(Button button)
                    {
                        var peer = new System.Windows.Automation.Peers.ButtonAutomationPeer(button);
                        ((System.Windows.Automation.Provider.IInvokeProvider)peer.GetPattern(System.Windows.Automation.Peers.PatternInterface.Invoke)).Invoke();
                        System.Windows.Threading.Dispatcher.CurrentDispatcher.Invoke(() => { },
                            System.Windows.Threading.DispatcherPriority.ContextIdle);
                    }
                    InvokeButton(later);
                    Assert.Equal(Visibility.Collapsed, ((FrameworkElement)collection.FindName("EvolutionFooter")).Visibility);
                    Assert.Equal(Visibility.Visible, ((FrameworkElement)collection.FindName("DetailPersonalGrid")).Visibility);
                    Click(collection, "DetailConditionsButton");
                    Assert.True(evolve.Focus());
                    InvokeButton(evolve);
                    var confirmation = (FrameworkElement)menu.FindName("ConfirmationHost");
                    Assert.Equal(Visibility.Visible, confirmation.Visibility);
                    Click(confirmation, "CancelButton");
                    System.Windows.Threading.Dispatcher.CurrentDispatcher.Invoke(() => { },
                        System.Windows.Threading.DispatcherPriority.ContextIdle);
                    Assert.Equal(133, data.Individuals[0].CurrentSpeciesId);
                    Assert.Equal(100, data.Berries["oran"]); Assert.Equal(3, data.Items["evolution-stone"]);
                }
                SaveProof();
                return;
            }
            foreach (var award in art.Cards.Take(3)) data.AwardLedger[award] = new();
            data.TrophyShelfIds.AddRange(art.Cards.Take(3)); room.Refresh(); album.Refresh();
            Capture("corner", MenuSection.Corner);
            Capture("achievements", MenuSection.Achievements);
            menu.Navigate(MenuSection.Pokedex);
            ((TabControl)((Grid)collection.Content).Children[0]).SelectedIndex = 1;
            Capture("collection", MenuSection.Pokedex);
            Click(collection, "SpeciesModeButton"); Capture("species", MenuSection.Pokedex);
            ((TextBox)collection.FindName("SpeciesSearchBox")).Text = "#0004";
            ((ListBox)collection.FindName("SpeciesGrid")).SelectedIndex = 0;
            Capture("species", MenuSection.Pokedex, "unknown-acquisition");
            Invoke(collection, "SpeciesBack_Click", collection, new RoutedEventArgs());
            ((TextBox)collection.FindName("SpeciesSearchBox")).Text = "#0133";
            ((ListBox)collection.FindName("SpeciesGrid")).SelectedIndex = 0;
            Capture("species", MenuSection.Pokedex, "known-paths");
            Invoke(collection, "SpeciesBack_Click", collection, new RoutedEventArgs());
            ((TextBox)collection.FindName("SpeciesSearchBox")).Text = "";
            Click(collection, "IndividualsModeButton");
            Assert.True(collection.OpenIndividual(data.Individuals[4].Id));
            Capture("detail", MenuSection.Pokedex, "longest-fact-689", (FrameworkElement)collection.FindName("DetailXpPotionButton"));
            Assert.True(((TextBlock)collection.FindName("DetailDescriptionText")).Text.Length >= 157);
            Invoke(collection, "HideDetail"); collection.OpenIndividual(data.Individuals[0].Id);
            Capture("detail", MenuSection.Pokedex, "supported-eevee", (FrameworkElement)collection.FindName("DetailCareBoosterButton"));
            Click(collection, "DetailConditionsButton");
            Capture("evolution", MenuSection.Pokedex, action: (FrameworkElement)collection.FindName("DetailEvolveButton"));
            ((CheckBox)collection.FindName("DetailEvolutionStoneToggle")).IsChecked = true;
            Capture("evolution-branch", MenuSection.Pokedex, action: (FrameworkElement)collection.FindName("DetailEvolutionBranchPicker"));
            Invoke(collection, "HideDetail"); collection.OpenIndividual(data.Individuals[3].Id);
            Capture("detail", MenuSection.Pokedex, "shiny"); Invoke(collection, "HideDetail");
            Capture("shop", MenuSection.Shop);
            Capture("expedition", MenuSection.Expeditions, action: (FrameworkElement)expedition.FindName("ChooseTeamButton"));
            Click(expedition, "ChooseTeamButton");
            Capture("expedition-team", MenuSection.Expeditions, "800", (FrameworkElement)expedition.FindName("PickerDoneButton"));
            foreach (var pet in data.Individuals.Skip(1).Take(3))
            {
                ((TextBox)expedition.FindName("PickerSearchBox")).Text = pet.CurrentSpeciesId.ToString();
                ((WrapPanel)expedition.FindName("PickerMembers")).Children.OfType<Button>().First(b => Equals(b.Tag, pet.Id))
                    .RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            }
            Click(expedition, "PickerDoneButton");
            Capture("expedition-selected", MenuSection.Expeditions, action: (FrameworkElement)expedition.FindName("StartButton"));
            Click(expedition, "StartButton"); Assert.Equal("running", data.Expedition!.Status);
            Capture("expedition-running", MenuSection.Expeditions, action: (FrameworkElement)expedition.FindName("CancelButton"));
            data.Expedition.Status = "ready"; expedition.Refresh();
            Capture("expedition-ready", MenuSection.Expeditions, action: (FrameworkElement)expedition.FindName("ClaimButton"));
            Click(expedition, "ClaimButton"); Capture("expedition-result", MenuSection.Expeditions);
            Capture("nursery", MenuSection.Nursery, action: (FrameworkElement)nursery.FindName("ChooseFirstButton"));
            Click(nursery, "ChooseFirstButton");
            Capture("nursery-select", MenuSection.Nursery, "800-groups", (FrameworkElement)nursery.FindName("PickerSaveButton"));
            foreach (var pet in data.Individuals.Skip(1).Take(2))
            {
                ((TextBox)nursery.FindName("ParentSearchBox")).Text = pet.CurrentSpeciesId.ToString();
                var candidates = (WrapPanel)nursery.FindName("PickerIndividualsPanel");
                while (!candidates.Children.OfType<Button>().Any(b => Equals(b.Tag, pet.Id)))
                {
                    Assert.True(((Button)nursery.FindName("PickerNextButton")).IsEnabled);
                    Click(nursery, "PickerNextButton");
                }
                if (pet.Id == data.Individuals[1].Id)
                    Capture("nursery-select", MenuSection.Nursery, "800-individuals", (FrameworkElement)nursery.FindName("PickerSaveButton"));
                // Capturing resizes the window, and the page holds three rows of its width, so look again.
                ((TextBox)nursery.FindName("ParentSearchBox")).Text = ""; ((TextBox)nursery.FindName("ParentSearchBox")).Text = pet.CurrentSpeciesId.ToString();
                while (!candidates.Children.OfType<Button>().Any(b => Equals(b.Tag, pet.Id)))
                {
                    Assert.True(((Button)nursery.FindName("PickerNextButton")).IsEnabled);
                    Click(nursery, "PickerNextButton");
                }
                candidates.Children.OfType<Button>().Single(b => Equals(b.Tag, pet.Id)).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            }
            Click(nursery, "PickerSaveButton");
            Capture("nursery", MenuSection.Nursery, "selected", (FrameworkElement)nursery.FindName("StartButton"));
            Click(nursery, "StartButton"); Assert.Equal("running", data.Nest!.Status);
            Capture("nursery-running", MenuSection.Nursery);
            data.Nest.Status = "ready"; nursery.Refresh();
            Capture("nursery-ready", MenuSection.Nursery, action: (FrameworkElement)nursery.FindName("HatchButton"));
            Click(nursery, "HatchButton");
            Capture("nursery-hatching", MenuSection.Nursery, action: (FrameworkElement)nursery.FindName("OpenPokedexButton"));
            Capture("nursery-result", MenuSection.Nursery, action: (FrameworkElement)nursery.FindName("OpenPokedexButton"));
            Capture("catch", MenuSection.Catch, action: (FrameworkElement)catchView.FindName("CatchButton"));
            var known = new EncounterState { Id = Guid.NewGuid(), Source = "expedition", Phase = "resolved",
                KnownSpeciesId = 1, Individual = Pet(1) };
            state.Encounters.Insert(0, known); Assert.True(catchView.SelectEncounter(known.Id));
            Assert.NotNull(((Image)catchView.FindName("SceneImage")).Source);
            Capture("encounter", MenuSection.Catch);
            var reveal = new CatchReveal(new CatchResult(1, 1, Nature.Bold, false, false, 3), RarityTier.Common);
            void FinishPhase()
            {
                var phase = revealDelays.Dequeue();
                phase.Completion.TrySetResult();
                System.Windows.Threading.Dispatcher.CurrentDispatcher.Invoke(() => { },
                    System.Windows.Threading.DispatcherPriority.ContextIdle);
            }
            void AnimatedReveal(string variant)
            {
                var animation = catchView.PlayReveal(reveal);
                var durations = new[] { 1100, 650, 800, 1000, 1000, 1000, 700 };
                for (var phase = 0; phase < durations.Length; phase++)
                {
                    Assert.Equal(durations[phase], revealDelays.Peek().Duration.TotalMilliseconds);
                    Capture("catch-animation", MenuSection.Catch, variant + "-phase-" + phase,
                        (FrameworkElement)catchView.FindName("SkipButton"));
                    if (variant == "unknown")
                    {
                        Assert.Equal(Visibility.Collapsed, ((TextBlock)catchView.FindName("RevealNameText")).Visibility);
                        Assert.Null(((Image)catchView.FindName("RevealSpriteImage")).Source);
                    }
                    FinishPhase();
                }
                Assert.True(animation.IsCompletedSuccessfully);
            }
            AnimatedReveal("known");
            Capture("catch-result", MenuSection.Catch, action: (FrameworkElement)catchView.FindName("CatchButton"));
            state.Encounters.Remove(known);
            Click(catchView, "CatchButton");
            Capture("catch", MenuSection.Catch, "unknown");
            AnimatedReveal("unknown");
            Click(catchView, "CatchButton");
            motion = false;
            var reduced = catchView.PlayReveal(reveal);
            Assert.Equal(250, revealDelays.Peek().Duration.TotalMilliseconds);
            Capture("catch-reduced", MenuSection.Catch, "reduced-motion", (FrameworkElement)catchView.FindName("SkipButton"));
            FinishPhase(); Assert.True(reduced.IsCompletedSuccessfully);
            Capture("catch-result", MenuSection.Catch, "reduced-settled", (FrameworkElement)catchView.FindName("CatchButton"));
            Capture("settings", MenuSection.Settings, action: (FrameworkElement)settings.FindName("ResetDataButton"));
            var history = new SaveHistoryWindow(Enumerable.Range(0, 30).Select(i => new SaveHistoryEntry("synthetic-" + i,
                DateTimeOffset.Parse("2026-10-20T10:00:00Z").AddDays(-i), 2, 800-i, 12500-i)).ToArray(), 2, menu.ConfirmAsync);
            var hosted = history.TakeHostedContent(); var historyTask = menu.ShowHostedAsync(hosted);
            ((ListBox)history.FindName("HistoryList")).SelectedIndex = 0;
            Capture("save-history", MenuSection.Settings, action: (FrameworkElement)history.FindName("RestoreButton"), navigate: false);
            Overlay("confirm-restore", "Вернуться к этой копии?", "800 питомцев · 12500 монет. Данные останутся в истории.", "Продолжить");
            menu.CompleteHostedContent(hosted); Assert.True(historyTask.IsCompleted); history.Close();
            Overlay("confirm-cancel", "Прервать экспедицию?", "Команда вернётся без добычи и опыта.", "Прервать", destructive: true);
            Overlay("confirm-rehome", "Найти новый дом?", "Этот питомец покинет коллекцию. Действие нельзя отменить.", "Передать", destructive: true);
            Capture("components", MenuSection.Settings, "audio-zero-and-controls", (FrameworkElement)settings.FindName("CrySoundVolumeSlider"));
            foreach (var step in Enumerable.Range(1, 6))
            {
                var tour = new OnboardingWindow(step, dataRoot: catalog.DataRoot, giftQueueCount: () => 2);
                var content = tour.TakeHostedContent(); var pending = menu.ShowHostedAsync(content);
                Capture("tour-" + step, MenuSection.Corner, action: (FrameworkElement)tour.FindName("NextButton"), navigate: false);
                menu.CompleteHostedContent(content); Assert.True(pending.IsCompleted); tour.Close();
            }
            var openedTour = new OnboardingWindow(6, dataRoot: catalog.DataRoot, giftQueueCount: () => 0);
            var openedContent = openedTour.TakeHostedContent(); menu.ShowHostedAsync(openedContent);
            Capture("tour-6", MenuSection.Corner, "all-opened", (FrameworkElement)openedTour.FindName("NextButton"), navigate: false);
            menu.CompleteHostedContent(openedContent); openedTour.Close();
            var expected = new[] { "corner", "collection", "species", "detail", "evolution", "expedition", "expedition-running",
                "expedition-ready", "expedition-result", "nursery", "nursery-running", "nursery-ready", "nursery-result",
                "shop", "achievements", "catch", "catch-result", "settings", "save-history", "confirm-cancel", "confirm-restore",
                "confirm-rehome", "tour-1", "tour-2", "tour-3", "tour-4", "tour-5", "tour-6", "components", "evolution-branch",
                "catch-animation", "encounter", "nursery-hatching", "nursery-select", "catch-reduced", "expedition-selected", "expedition-team" };
            Assert.Equal(expected.Order(), keys.Order());
            File.WriteAllText(Path.Combine(output, "matrix.json"), JsonSerializer.Serialize(records, new JsonSerializerOptions { WriteIndented = true }));
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
        Assert.All(await Task.WhenAll(cancellations), Assert.False);
    }

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

