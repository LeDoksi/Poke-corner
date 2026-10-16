using System.Diagnostics;
using System.Windows.Threading;
using PokeCorner.App.Tests.Theme;

namespace PokeCorner.App.Tests.GlobalUpdate;

public class acceptanceTests
{
    private static string RepositoryRoot => Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));

    [Fact]
    public async Task OfflineStarterUsesCanonicalFramesBeforeAnyLegacyPngFetch()
    {
        var catalog = PokeCorner.Core.PokeApi.CatalogPackage.Load(Path.Combine(AppContext.BaseDirectory, "Data"));
        var chains = PokeCorner.Core.Persistence.LegacyLookup.Load(catalog.DataRoot).Chains
            .Where(c => catalog.CanIssueDirectly(c.StarterSpeciesId, false)).Take(3).ToArray();
        var frames = PokeCorner.Core.PokeApi.GifFrameDecoder.DecodeFramesAsPng(File.ReadAllBytes(catalog.GetAssetPath(chains[0].StarterSpeciesId, false)!));
        var networkCalls = 0;
        PokeCorner.App.Onboarding.StarterSelectionWindow? window = null;
        Task? loading = null;
        try
        {
            WpfTestHost.Run(() =>
            {
                window = new PokeCorner.App.Onboarding.StarterSelectionWindow(chains,
                    _ => { networkCalls++; return Task.FromException<byte[]?>(new InvalidOperationException("Legacy PNG/network fetch should not run.")); },
                    _ => Task.FromResult<IReadOnlyList<byte[]>?>(frames));
                loading = (Task)typeof(PokeCorner.App.Onboarding.StarterSelectionWindow).GetMethod("LoadSpritesAsync",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.Invoke(window, [])!;
            });
            await loading!;
            WpfTestHost.Run(() =>
            {
                foreach (var name in new[] { "Candidate1Image", "Candidate2Image", "Candidate3Image" })
                    Assert.NotNull(((System.Windows.Controls.Image)window!.FindName(name)).Source);
            });
            Assert.Equal(0, networkCalls);
        }
        finally { WpfTestHost.Run(() => window?.Close()); }
    }
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void CanonicalCardThumbnailReadsPackagedGifWithoutLegacyCacheOrNetwork(bool shiny) => WpfTestHost.Run(() =>
    {
        var catalog = PokeCorner.Core.PokeApi.CatalogPackage.Load(Path.Combine(AppContext.BaseDirectory, "Data"));
        var person = new PokeCorner.Core.Domain.PokemonIndividual
        {
            SpeciesId = 1, CurrentSpeciesId = 1, FamilyRootId = 1, EvolutionChainId = 1, ChainLength = 3,
            Nature = PokeCorner.Core.Domain.Nature.Hardy, IsShiny = shiny, CaughtAtUtc = DateTimeOffset.UtcNow
        };
        var state = new PokeCorner.Core.AppState();
        state.Collection.Add(person);
        var vm = new PokeCorner.App.Popup.CollectionViewModel(state.Collection, state.CareStateByIndividualId,
            new PokeCorner.Core.Domain.CareModeService(), [], state.PendingEggs, species: catalog.Species);
        var cache = Path.Combine(Path.GetTempPath(), "T20-offline-card-" + Guid.NewGuid().ToString("N"));
        var networkCalls = 0;
        try
        {
            var view = new PokeCorner.App.Popup.CollectionView(vm, state, new PokeCorner.Core.PokeApi.SpriteCache(cache),
                () => { }, () => { }, () => { }, () => null, () => false, () => false,
                fetchSprite: _ => { networkCalls++; return Task.FromResult<byte[]?>(null); },
                getSpeciesAssetPath: (id, variant) => catalog.GetAssetPath(id, variant));
            Assert.True(view.OpenIndividual(person.Id));
            var image = (System.Windows.Controls.Image)view.FindName("DetailSpriteImage");
            var actual = Assert.IsAssignableFrom<System.Windows.Media.Imaging.BitmapSource>(image.Source);
            var expected = (System.Windows.Media.Imaging.BitmapSource)PokeCorner.App.Imaging.SpriteImages.Decode(File.ReadAllBytes(catalog.GetAssetPath(1, shiny)!));
            Assert.Equal(expected.PixelWidth, actual.PixelWidth);
            Assert.Equal(expected.PixelHeight, actual.PixelHeight);
            var stride = (actual.PixelWidth * actual.Format.BitsPerPixel + 7) / 8;
            var actualPixels = new byte[stride * actual.PixelHeight];
            var expectedPixels = new byte[actualPixels.Length];
            actual.CopyPixels(actualPixels, stride, 0); expected.CopyPixels(expectedPixels, stride, 0);
            Assert.Equal(expectedPixels, actualPixels);
            Assert.Equal(0, networkCalls);
            view.RaiseEvent(new System.Windows.RoutedEventArgs(System.Windows.FrameworkElement.UnloadedEvent));
        }
        finally { Directory.Delete(cache, true); }
    });
    [Fact]
    public void ReleaseInstallerPreservesPreReplacementLookupAndUpgradeIdentity()
    {
        var document = System.Xml.Linq.XDocument.Load(Path.Combine(RepositoryRoot, "installer/Product.wxs"));
        var ns = document.Root!.Name.Namespace;
        var package = document.Root.Element(ns + "Package")!;
        Assert.Equal("ABEC4F80-F721-4D76-85F5-BCFF1CCBD9B4", (string?)package.Attribute("UpgradeCode"));
        Assert.True(Version.Parse((string)package.Attribute("Version")!) > new Version(1, 0, 18, 0));
        Assert.Equal("afterInstallExecute", (string?)package.Element(ns + "MajorUpgrade")!.Attribute("Schedule"));
        var copy = package.Descendants(ns + "CopyFile").Single(e => (string?)e.Attribute("Id") == "PreserveLegacyLookup");
        Assert.Equal("LEGACY_CATALOG_PATH", (string?)copy.Attribute("SourceProperty"));
        Assert.Null(copy.Attribute("SourceName"));
        Assert.Equal("legacy-v1-lookup.json", (string?)copy.Attribute("DestinationName"));
        Assert.Equal("no", (string?)copy.Attribute("Delete"));
        Assert.Contains("NOT PRESERVED_LEGACY_LOOKUP", (string?)copy.Parent!.Attribute("Condition"));
    }

    [Fact]
    public void ReleaseDataContentUsesApprovedInventoryAndFrozenLookupOnly()
    {
        var project = System.Xml.Linq.XDocument.Load(Path.Combine(RepositoryRoot, "src/PokeCorner.App/PokeCorner.App.csproj"));
        Assert.DoesNotContain(project.Descendants("Content"), item => ((string?)item.Attribute("Include"))?.StartsWith("Data\\", StringComparison.Ordinal) == true);
        Assert.Contains(project.Descendants("Content"), item => (string?)item.Attribute("Link") == "Data\\catalog.json");
    }
    [Fact]
    public async Task ExistingHeroFrameAssertionSurvivesACompetingSlowUiFixture()
    {
        using var requested = new ManualResetEventSlim();
        Task? contender = null;
        try
        {
            WpfTestHost.Run(() =>
            {
                contender = Task.Run(() =>
                {
                    requested.Set();
                    WpfTestHost.Run(() => Thread.Sleep(850));
                });
                Assert.True(requested.Wait(TimeSpan.FromSeconds(2)));
                new ThemeRenderingTests().CollectionView_HeroSprite_MultiFrameSpecies_ActuallyCyclesFrames();
            });
        }
        finally { if (contender is not null) await contender; }
    }
    [Fact]
    public async Task SharedWpfDispatcherDoesNotReenterAnotherFixtureWhilePumpingFrames()
    {
        using var requested = new ManualResetEventSlim();
        Task? contender = null;
        var firstActive = false;
        var secondEntered = false;
        var overlapped = false;
        WpfTestHost.Run(() =>
        {
            firstActive = true;
            contender = Task.Run(() =>
            {
                requested.Set();
                WpfTestHost.Run(() => { overlapped = firstActive; secondEntered = true; });
            });
            Assert.True(requested.Wait(TimeSpan.FromSeconds(2)));
            var clock = Stopwatch.StartNew();
            while (!secondEntered && clock.ElapsedMilliseconds < 150)
            {
                Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.Background);
                Thread.Sleep(5);
            }
            firstActive = false;
        });
        await contender!;
        Assert.True(secondEntered);
        Assert.False(overlapped, "A competing fixture ran inside the frame sampler's nested dispatcher pump.");
    }
}
