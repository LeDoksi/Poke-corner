# Shop, Visual Overhaul & Sprites — дизайн-документ

**Дата:** 2026-08-20
**Статус:** утверждено, готово к написанию implementation-плана
**Контекст:** после того как [Collection & Progress](2026-08-20-collection-and-progress-design.md) закрыла главные механические дыры (Pet/Feed, живые бары, вкладки коллекции), пользователь явно попросил закрыть весь оставшийся "красивости"-долг одним заходом: магазин (был осознанно отложен в Collection & Progress), полный визуальный редизайн (Настройки/Коллекция/Магазин/трей), и подключение полного каталога + анимированных спрайтов (существующий, но невыполненный план `2026-08-19-pokecorner-full-experience-and-installer.md`, Tasks 1-2).

Это отдельная, самостоятельная фича поверх Collection & Progress — использует её типы (`FeedingInventory`, `AppState.RecomputeStage`, `CollectionEntry`/`RarityTier`-подобную логику), но не переписывает её содержимое, кроме одного явного пункта переиспользования (см. §4).

## 0. Пересчёт активности под реальных геймеров

Существующая калибровка (`CatchCheckIntervalUnits = 3000` с комментарием "~2-3 hours of medium-high activity") молчаливо предполагала ~1200 единиц активности/час. Реальные данные по Dota 2 (Dotabuff/сообщество): казуальные игроки ~90 APM, средний скилл (3500-4000 MMR) ~200 APM, профи 200-280+. Взяв **150 APM** как целевую базовую линию (сессия вовлечённого игрока, не только про) даёт **9000 единиц/час** — в **7.5 раза** больше старого предположения.

Чтобы реальное время до улова/эволюции/покупки осталось тем же (не ускорилось только потому, что геймеры чаще кликают), **все константы, выраженные в единицах активности или Care Points, масштабируются ×7.5** от их исходных значений — независимо от того, реализованы они уже в коде или только в дизайне (Collection & Progress ещё не выполнена, поэтому её числа меняются здесь до реализации, без конфликта). Ценники в монетах пересчёта не требуют — курс заработка монет масштабируется вместе с активностью, так что цена-в-часах не меняется.

| Константа | Где живёт | Старое | Новое |
|---|---|---|---|
| `ActivityAccumulator.CatchCheckIntervalUnits` | уже в коде | 3,000 | **22,500** |
| `FeedingService.ActivityUnitsPerTreatEarned` | уже в коде | 500 | **3,750** |
| `EvolutionThresholds.Stage2Threshold` | уже в коде | 5,000 | **37,500** |
| `EvolutionThresholds.Stage3Threshold` | уже в коде | 20,000 | **150,000** |
| `FeedingService.PetCarePointsBonus` | Collection & Progress, не реализовано | 15 | **110** |
| `FeedingService.FeedCarePointsBonus` | Collection & Progress, не реализовано | 300 | **2,250** |
| `ShopService.ActivityUnitsPerCoinEarned` | этот документ | — | **900** (см. §1.3) |
| `ShopService.CarePointsBoosterAmount` | этот документ | — | **7,500** (см. §1.3) |

Правка четырёх уже реализованных констант — обычная задача в implementation-плане (поменять значение + числа в существующих тестах, которые их проверяют, например `FeedingServiceTests`/`AppStateTests`/тесты `ActivityAccumulator`, если такие проверяют точное пороговое число).

## 1. Магазин

### 1.1 Валюта

Новая валюта **монеты** (`Coins`), отдельная от лакомств — копится тем же пулом активности (клики/клавиши, опционально токены — тем же `_inputCounter`/`_tokenUsageProvider`, что и Care Points/лакомства), но по собственному курсу.

```csharp
// Core/Domain/ShopInventory.cs
namespace PokeCorner.Core.Domain;

public sealed class ShopInventory
{
    public int Coins { get; set; }
    public long RemainderUnits { get; set; }
    public bool CatchLuckyCharmActive { get; set; }
}
```

### 1.2 Редкость — переиспользуемый классификатор

Формализует правило редкости, уже описанное (но inline) в Collection & Progress §3, в отдельный класс — им пользуются и Pokédex-вкладка (Collection & Progress), и магазин (яйца):

```csharp
// Core/Domain/RarityClassifier.cs
namespace PokeCorner.Core.Domain;

public enum RarityTier { Common, Uncommon, Rare, Legendary }

public static class RarityClassifier
{
    public static RarityTier Classify(int captureRate) => captureRate switch
    {
        >= 150 => RarityTier.Common,
        >= 70 => RarityTier.Uncommon,
        >= 30 => RarityTier.Rare,
        _ => RarityTier.Legendary
    };
}
```

### 1.3 `ShopService`

Отдельные именованные методы на каждое действие (не диспетчеризация по enum) — тот же принцип, что развёл Pet/Feed в Collection & Progress: явное действие с понятным названием вместо одной универсальной кнопки/метода.

```csharp
// Core/Domain/ShopService.cs
namespace PokeCorner.Core.Domain;

public sealed class ShopService
{
    // Baseline recalibration: real Dota 2 APM data (casual ~90, mid-MMR ~200, pro 200-280+;
    // see design doc discussion) puts an engaged gamer's active click/key rate far above the
    // ~1200 units/hour this app's existing tuning silently assumed. 150 APM (9000 units/hour)
    // is the new reference rate — every activity/Care-Point-denominated constant in this app
    // (existing and new) is scaled by the resulting 7.5x factor so real-world pacing (hours to
    // a catch, an evolution, a purchase) stays what it was designed to feel like, just correctly
    // calibrated for this app's actual gamer audience instead of a generic guess.
    public const long ActivityUnitsPerCoinEarned = 900; // ~10 coins/hour of active play

    public const int TreatPackPrice = 20;
    public const int TreatPackAmount = 5;
    public const int CarePointsBoosterPrice = 20;
    public const int CarePointsBoosterAmount = 7500; // scaled 1000 -> 7500 (7.5x), keeps ~20% of Stage2Threshold
    public const int CatchLuckyCharmPrice = 30;
    public const double CatchLuckyCharmShinyMultiplier = 4.0;

    public const int EggPriceCommon = 30;     // ~3h of play
    public const int EggPriceUncommon = 80;   // ~8h
    public const int EggPriceRare = 150;      // ~15h
    public const int EggPriceLegendary = 300; // ~28-30h — deliberately hard to afford

    public void EarnCoinsFromActivity(ShopInventory shop, long activityUnits)
    {
        var totalUnits = shop.RemainderUnits + activityUnits;
        shop.Coins += (int)(totalUnits / ActivityUnitsPerCoinEarned);
        shop.RemainderUnits = totalUnits % ActivityUnitsPerCoinEarned;
    }

    public bool BuyTreatPack(ShopInventory shop, FeedingInventory feeding)
    {
        if (shop.Coins < TreatPackPrice) return false;
        shop.Coins -= TreatPackPrice;
        feeding.Treats += TreatPackAmount;
        return true;
    }

    // Caller (App layer) must call AppState.RecomputeStage(activeIndividual) after a
    // successful purchase — same pattern FeedingService.Feed/Pet already established in
    // Collection & Progress: this method only mutates AccumulatedCarePoints, never stage.
    public bool BuyCarePointsBooster(ShopInventory shop, PokemonIndividual activeIndividual)
    {
        if (shop.Coins < CarePointsBoosterPrice) return false;
        shop.Coins -= CarePointsBoosterPrice;
        activeIndividual.AccumulatedCarePoints += CarePointsBoosterAmount;
        return true;
    }

    public bool BuyCatchLuckyCharm(ShopInventory shop)
    {
        if (shop.Coins < CatchLuckyCharmPrice) return false;
        shop.Coins -= CatchLuckyCharmPrice;
        shop.CatchLuckyCharmActive = true;
        return true;
    }

    public static int EggPriceForTier(RarityTier tier) => tier switch
    {
        RarityTier.Common => EggPriceCommon,
        RarityTier.Uncommon => EggPriceUncommon,
        RarityTier.Rare => EggPriceRare,
        RarityTier.Legendary => EggPriceLegendary,
        _ => throw new ArgumentOutOfRangeException(nameof(tier))
    };

    // Only spends the currency — the App layer performs the actual catch roll via
    // CatchRoller.RollCatchFromTier and adds the result to the collection, mirroring how
    // OnCatchRequested already works for activity-triggered catches. Kept separate so
    // ShopService (like FeedingService) never needs to know about PokemonCollection.
    public bool TrySpendForEgg(ShopInventory shop, RarityTier tier)
    {
        var price = EggPriceForTier(tier);
        if (shop.Coins < price) return false;
        shop.Coins -= price;
        return true;
    }
}
```

### 1.4 Яйцо: гарантированная поимка по редкости

`CatchRoller` получает новый метод, переиспользующий существующую логику взвешивания/шайни/натуры — только сначала фильтрует пул по редкости:

```csharp
// Добавляется в существующий CatchRoller (src/PokeCorner.Core/Domain/CatchRoller.cs)
public CatchResult RollCatchFromTier(
    IReadOnlyList<SpeciesEvolutionChain> allChains,
    RarityTier tier,
    IReadOnlySet<int> ownedChainIds,
    IReadOnlySet<Nature> ownedNaturesForDuplicateChain,
    double shinyChanceMultiplier = 1.0)
{
    var tierChains = allChains.Where(c => RarityClassifier.Classify(c.CaptureRate) == tier).ToList();
    if (tierChains.Count == 0)
    {
        throw new InvalidOperationException($"No species available in rarity tier {tier} — caller must check availability before offering this purchase.");
    }
    return RollCatch(tierChains, ownedChainIds, ownedNaturesForDuplicateChain, shinyChanceMultiplier);
}
```

`RollCatch`'s existing signature gains one optional trailing parameter (backward compatible, existing tests untouched):

```csharp
public CatchResult RollCatch(
    IReadOnlyList<SpeciesEvolutionChain> allChains,
    IReadOnlySet<int> ownedChainIds,
    IReadOnlySet<Nature> ownedNaturesForDuplicateChain,
    double shinyChanceMultiplier = 1.0) // NEW — multiplies both the base and duplicate shiny chance
{
    // ... existing body, only the isShiny line changes:
    // var isShiny = _random.NextDouble() < (isDuplicate ? BaseShinyChance * DuplicateShinyChanceMultiplier : BaseShinyChance) * shinyChanceMultiplier;
}
```

**Талисман удачи** — App layer passes `4.0` into `RollCatch`/`RollCatchFromTier` when `_appState.Shop.CatchLuckyCharmActive` is true, then resets the flag to `false` immediately after the roll (consumed regardless of outcome — matches "одноразовый").

**App-layer wiring** (`App.xaml.cs`): the existing catch-adding logic inside `OnCatchRequested` (species pick → `new PokemonIndividual { ... }` → `_appState.Collection.Add(...)` → refresh/notify) is extracted into a private helper `AddCatchResultToCollection(CatchResult result)`, reused by both `OnCatchRequested` (activity-triggered) and the new `BuyEgg(RarityTier tier)` method (shop-triggered, guaranteed). `BuyEgg` disables itself in the UI (not even attempted) when `RarityClassifier.Classify` finds zero chains of that tier in the currently loaded catalog — this is expected on the 7-chain offline fallback until §4's `SpeciesCatalog` syncs.

### 1.5 Персистентность

`AppState` gets `public ShopInventory Shop { get; } = new();`, wired into `SaveData`/`JsonSaveStore`/`Hydrate` exactly like `Feeding`:

```csharp
// SaveData.cs additions
public int ShopCoins { get; init; }
public long ShopRemainderUnits { get; init; }
public bool ShopCatchLuckyCharmActive { get; init; }
```

```csharp
// AppState.Hydrate additions
Shop.Coins = data.ShopCoins;
Shop.RemainderUnits = data.ShopRemainderUnits;
Shop.CatchLuckyCharmActive = data.ShopCatchLuckyCharmActive;
```

```csharp
// App.xaml.cs PersistState additions
ShopCoins = _appState.Shop.Coins,
ShopRemainderUnits = _appState.Shop.RemainderUnits,
ShopCatchLuckyCharmActive = _appState.Shop.CatchLuckyCharmActive,
```

`OnGrowthTick` gains one line alongside the existing `_feedingService.EarnTreatsFromActivity(...)` call: `_shopService.EarnCoinsFromActivity(_appState.Shop, units);`.

### 1.6 Магазин — App-слой (`ShopView`)

New popup window, same composition pattern as `CollectionView`/`SettingsView` (`Window { Content = new ShopView(...) }`, opened from a new tray menu item "Магазин"). Layout: coin balance header, then a list of buyable items (треды/бустер CP/талисман — always available, price-gated) followed by a 4-item egg row (Common/Uncommon/Rare/Legendary), each showing price and disabled with a tooltip ("Пока недоступно — нужен более широкий каталог видов") when its tier has zero chains in the current catalog. Every purchase button re-checks affordability and refreshes the coin balance display immediately (same `Refresh()` pattern as `CollectionView`).

Косметика/аксессуары для питомца — осознанно не в этом заходе, отложены на будущее обновление.

## 2. Реакции питомца

PokeAPI не предоставляет по-вида анимации под конкретные действия (кормление/грусть/etc.) — только статичный спрайт и один "idle"-GIF (см. §4). Ручная отрисовка реакций на весь каталог видов нереалистична для этого проекта. Вместо нового арта — **процедурная анимация поверх уже загруженного спрайта**, через WPF `Storyboard`/`RenderTransform` на существующем `Image`-контроле `OverlayWindow`.

`OverlayWindow` gets new public methods, called from `App.xaml.cs` at the existing action call sites:

```csharp
// OverlayWindow.xaml.cs additions
public void PlayPetReaction();      // scale bounce 1.0 -> 1.15 -> 1.0 (~300ms) + fading heart glyph
public void PlayFeedReaction();     // bigger double-bounce + fading "+2250" text (uses FeedCarePointsBonus)
public void PlayEvolutionFlash();   // brief scale-pulse + opacity flash
public void PlayRanAwayReaction();  // slide/fade out immediately before Hide()
public void SetMoodTint(double mood); // 0.0-1.0 — continuously drives a slight downward Y-offset + desaturation as mood decreases
```

Wiring in `App.xaml.cs`:
- `PetActiveIndividual()`/`FeedActiveIndividual()` (Collection & Progress §1) call `_overlayWindow.PlayPetReaction()`/`PlayFeedReaction()` right after the domain call succeeds.
- `AddCatchResultToCollection` / any `AppState.RecomputeStage` call site that observes `individual.CurrentStage` change before vs. after calls `_overlayWindow.PlayEvolutionFlash()`.
- `CheckHardcoreNeglect`'s existing 5-second poll also calls `_overlayWindow.SetMoodTint(_careModeService.ComputeMood(careState, now))` every tick (this activates a `ComputeMood` call that exists in Core today but is never consumed anywhere — first real use).
- `CheckHardcoreNeglect`'s run-away branch calls `_overlayWindow.PlayRanAwayReaction()` before `RefreshOverlayForActiveIndividual()` hides it.

All animations reuse whatever `SpriteImage` currently shows (static or animated frame) — they transform the existing visual, they don't replace it.

## 3. Визуальная тема — "Pokédex dark"

Единый `ResourceDictionary`, подключённый через `App.xaml`'s `Application.Resources` (`MergedDictionaries`), применяется одинаково к `SettingsView`, `CollectionView` (Collection & Progress's new tabs), and the new `ShopView`.

```xaml
<!-- src/PokeCorner.App/Theme/PokedexTheme.xaml -->
<ResourceDictionary xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
                     xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
    <SolidColorBrush x:Key="WindowBackgroundBrush" Color="#1A1F3C" />
    <SolidColorBrush x:Key="CardBackgroundBrush" Color="#252B4A" />
    <SolidColorBrush x:Key="AccentRedBrush" Color="#E63946" />
    <SolidColorBrush x:Key="AccentYellowBrush" Color="#FFD166" />
    <SolidColorBrush x:Key="PrimaryTextBrush" Color="#F1F1F1" />
    <SolidColorBrush x:Key="SecondaryTextBrush" Color="#9AA0C3" />

    <SolidColorBrush x:Key="RarityCommonBrush" Color="#9AA0C3" />
    <SolidColorBrush x:Key="RarityUncommonBrush" Color="#52C97A" />
    <SolidColorBrush x:Key="RarityRareBrush" Color="#4C8DF6" />
    <SolidColorBrush x:Key="RarityLegendaryBrush" Color="#FFB020" />

    <Style TargetType="Window">
        <Setter Property="Background" Value="{StaticResource WindowBackgroundBrush}" />
        <Setter Property="Foreground" Value="{StaticResource PrimaryTextBrush}" />
    </Style>
    <Style TargetType="Button">
        <Setter Property="Background" Value="{StaticResource AccentRedBrush}" />
        <Setter Property="Foreground" Value="{StaticResource PrimaryTextBrush}" />
        <Setter Property="Padding" Value="10,5" />
        <Setter Property="BorderThickness" Value="0" />
        <Setter Property="Template">
            <Setter.Value>
                <ControlTemplate TargetType="Button">
                    <Border Background="{TemplateBinding Background}" CornerRadius="4" Padding="{TemplateBinding Padding}">
                        <ContentPresenter HorizontalAlignment="Center" VerticalAlignment="Center" />
                    </Border>
                </ControlTemplate>
            </Setter.Value>
        </Setter>
        <Style.Triggers>
            <Trigger Property="IsMouseOver" Value="True">
                <Setter Property="Opacity" Value="0.85" />
            </Trigger>
            <Trigger Property="IsEnabled" Value="False">
                <Setter Property="Opacity" Value="0.4" />
            </Trigger>
        </Style.Triggers>
    </Style>
    <Style TargetType="ProgressBar">
        <Setter Property="Background" Value="{StaticResource CardBackgroundBrush}" />
        <Setter Property="Foreground" Value="{StaticResource AccentYellowBrush}" />
        <Setter Property="BorderThickness" Value="0" />
        <Setter Property="Height" Value="8" />
    </Style>
    <Style TargetType="TabItem">
        <Setter Property="Background" Value="{StaticResource CardBackgroundBrush}" />
        <Setter Property="Foreground" Value="{StaticResource SecondaryTextBrush}" />
        <Style.Triggers>
            <Trigger Property="IsSelected" Value="True">
                <Setter Property="Background" Value="{StaticResource AccentRedBrush}" />
                <Setter Property="Foreground" Value="{StaticResource PrimaryTextBrush}" />
            </Trigger>
        </Style.Triggers>
    </Style>
</ResourceDictionary>
```

`RarityXxxBrush` resources are referenced by `CollectionView`'s Pokédex grid cells (Collection & Progress §3) and `ShopView`'s egg rows via a value converter (`RarityTier` → brush key), added alongside `BoolToYesNoConverter` in `Popup/`.

This is pure WPF resources — no new image files, no restructuring of the already-designed Collection & Progress layout (Pet/Feed buttons, Pokédex/Catch log tabs), just visual restyling applied on top.

## 4. Каталог и анимированные спрайты

Полностью переиспользует the already-written, not-yet-executed `2026-08-19-pokecorner-full-experience-and-installer.md`:

- **Task 1 (`SpeciesCatalog`)** — network-backed full Gen 1-5 catalog with offline fallback to the existing 7-chain set. This is what actually populates the Rare/Legendary tiers for both the shop's eggs and the Pokédex tab's rarity tags — on the 7-chain fallback alone those tiers are near-empty, as already noted in the Collection & Progress spec.
- **Task 2 (`GifFrameDecoder`)** — animated Gen-V sprite instead of a static frame, feeding the same `SpriteAnimator`/`FrameSequencer` infrastructure already built to accept multiple frames.
- **Task 3 (MSI installer)** — explicitly stays out of scope here; it's the "hand it to someone else's PC" concern, unrelated to sprites/shop/visuals, executed separately later per the original agreement.

Both tasks' code is copied into the new combined implementation plan essentially unchanged (already reviewed, already TDD'd) — no redesign needed, just folded into the same execution pass as the shop/theme/reactions work above so "sprites and assets" ships together with everything else, per the request that started this design round.

## 5. Иконка трея

`TrayIconService` gets a new method, replacing the current `SystemIcons.Application` placeholder:

```csharp
public void SetSpriteIcon(byte[] pngBytes)
{
    using var stream = new MemoryStream(pngBytes);
    using var bitmap = new System.Drawing.Bitmap(stream);
    var hIcon = bitmap.GetHicon();
    _notifyIcon.Icon = System.Drawing.Icon.FromHandle(hIcon); // caller (App.xaml.cs) already has PNG bytes decoded for the overlay
}
```

Called from `App.xaml.cs`'s `RefreshOverlayForActiveIndividual`, right where sprite bytes are already fetched for the overlay — the tray icon always matches whoever is currently on the desktop, no separate fetch.

**Tracking-off indicator**: instead of a separate pre-made "inactive" icon file, `SetActivityTrackingIndicator(bool)` applies a `System.Drawing.Imaging.ColorMatrix` desaturation filter to the same current sprite bitmap when tracking is off, re-applying the plain sprite when it's back on. No new asset files either way.

**Cold-start fallback** (before the very first sprite has ever been fetched — a brief window before onboarding's starter hatch completes): a small `System.Drawing.Graphics`-drawn placeholder — red-topped, white-bottomed circle with a black center band and button, i.e. a generic Poké Ball, drawn entirely in code (no network, no file). Replaced by the real sprite icon the moment the starter hatches, which today already happens synchronously at first launch.

## Границы

- Косметика/аксессуары для питомца в магазине — отложены на будущее обновление (по явному согласию).
- MSI-инсталлятор — остаётся отдельным, невыполненным пока пунктом того же более раннего плана, не трогается здесь.
- Реакции — только процедурная анимация поверх текущего спрайта, не новый per-species арт (технически недоступно без стороннего источника ассетов).
