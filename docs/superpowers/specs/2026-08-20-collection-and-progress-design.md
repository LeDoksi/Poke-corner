# Collection & Progress Bars — дизайн-документ

**Дата:** 2026-08-20
**Статус:** утверждено, готово к написанию implementation-плана
**Контекст:** написано по итогам живого тестирования собранного [PokéCorner](2026-08-19-desktop-pokemon-pet-design.md) — часть механик оказались не такими, как задумывалось при первом дизайне, и часть UI оказалась слишком непонятной/статичной. Этот документ фиксирует исправления и расширения поверх уже реализованных Core Domain и Windows Shell планов.

## Причина

Три конкретные проблемы, найденные при реальном использовании:

1. Кнопка "Покормить/погладить" была одним действием под общей меткой — непонятно, что именно происходит по клику.
2. Прогресс (Care Points, лакомства, до следующей поимки) не виден в реальном времени — окно коллекции считает данные один раз при открытии и больше не обновляется, пока его не переоткрыть.
3. Коллекция — голая таблица с сырыми ID и `True/False` без подписей; по сравнению с референсом ([PokeTokenBar](https://github.com/chattymin/PokeTokenBar)) это ощущается сыро и непонятно.

## 1. Разделение "погладить" и "покормить"

Сейчас `FeedingService.TryFeed` — одно действие, режим (Simple/Economy) переключается в настройках. Это заменяется на два разных действия с фиксированными правилами — настройка Feeding mode **убирается полностью**, как и `AppSettings.FeedingMode`/`FeedingMode` enum (не оставляем как deprecated — ничего не ссылается на старую механику, чистое удаление).

- **Погладить (`FeedingService.Pet`)** — всегда доступно, бесплатно. Даёт **+15 Care Points** активному питомцу и сбрасывает таймер заброшенности (`CareModeService.RegisterInteraction`) — то есть снимает Hardcore-"обиду" точно так же, как раньше комбинированное действие. Триггерится **левым кликом по спрайту на рабочем столе** (уже существующий `OverlayWindow.PetClicked`) — по клику на пета теперь ВСЕГДА гладим, независимо ни от чего.
- **Покормить (`FeedingService.Feed`)** — доступно только через попап коллекции, отдельной кнопкой, только когда `FeedingInventory.Treats >= 1`. Списывает 1 лакомство, даёт **+300 Care Points**, тоже сбрасывает таймер заброшенности. Кнопка визуально задизейблена и показывает "Нужно лакомство" (или похожее), когда лакомств нет.

Сигнатуры (заменяют `TryFeed`):

```csharp
// FeedingService
public const int PetCarePointsBonus = 15;
public const int FeedCarePointsBonus = 300;

public void Pet(PokemonIndividual individual, CareState careState, DateTimeOffset nowUtc)
{
    individual.AccumulatedCarePoints += PetCarePointsBonus;
    careState.LastInteractionUtc = nowUtc;
    careState.RanAwayUntilUtc = null;
}

public bool Feed(PokemonIndividual individual, FeedingInventory inventory, CareState careState, DateTimeOffset nowUtc)
{
    if (inventory.Treats < 1) return false;
    inventory.Treats -= 1;
    individual.AccumulatedCarePoints += FeedCarePointsBonus;
    careState.LastInteractionUtc = nowUtc;
    careState.RanAwayUntilUtc = null;
    return true;
}
```

Оба метода мутируют `AccumulatedCarePoints` напрямую (раньше эту логику целиком вела `AppState.ApplyActivityUnits`) — после `Pet`/`Feed` вызывающий код (App-слой) обязан пересчитать `CurrentStage`. Вынести это в новый метод `AppState.RecomputeStage(PokemonIndividual individual)`, вызывающий `EvolutionCalculator.StageForPoints(individual.AccumulatedCarePoints, individual.ChainLength)` и присваивающий результат `individual.CurrentStage` — `ApplyActivityUnits` тоже переключается на вызов этого метода вместо дублирования той же строчки инлайн.

`EarnTreatsFromActivity` (уже существует) остаётся единственным источником лакомств до появления магазина — она никак не меняется.

## 2. Живые прогресс-бары

Три бара, все на основе уже существующих данных плюс один новый паблик-геттер в Core:

- **До поимки** — `ActivityAccumulator` уже считает `_unitsSinceLastCatchCheck` внутри себя, но не отдаёт наружу. Добавить `public long UnitsSinceLastCatchCheck => _unitsSinceLastCatchCheck;`. Бар = `UnitsSinceLastCatchCheck / CatchCheckIntervalUnits`.
- **До эволюции** — по `AccumulatedCarePoints` активного питомца относительно порога текущей и следующей стадии (`EvolutionThresholds.Stage2Threshold`/`Stage3Threshold`). На последней стадии бар полный, без дальнейшего прогресса.
- **До лакомства** — уже есть все данные: `FeedingInventory.RemainderUnits / FeedingService.ActivityUnitsPerTreatEarned`.

**Живое обновление:** окно коллекции сейчас пересчитывает данные только при открытии/действии — добавить `DispatcherTimer` с интервалом ~1 секунда, работающий, пока окно открыто (останавливается на `Closed`), который просто вызывает существующий `Refresh()`.

**Опциональная плавающая панель** — новая настройка (чекбокс) "Показывать прогресс рядом с питомцем". Если включена — рядом с `OverlayWindow` (например, прямо под ним) показывается маленькое всегда-видимое окно с теми же тремя барами без подписей-простыней (компактный вид), обновляется на той же частоте, что и рост (существующий 5-секундный `_growthTickTimer`, через `ReconcileSettingsState`-подобный паттерн: если настройка включена и панели нет — создать и показать; если выключена и панель есть — скрыть/убрать).

## 3. Редизайн коллекции: вкладки Pokédex / Catch log

Два таба вместо одного плоского списка.

**Catch log** (по сути — сегодняшний список, доработанный): каждая карточка — особь (`PokemonIndividual`), показывает спрайт текущей стадии (уже есть `ResolveDisplaySpeciesId`-логика в App-слое), имя вида (`SpeciesNames`), натуру, шайни-метку, тег редкости (см. ниже), и прогресс-бар до следующей эволюции. Кнопки "Погладить"/"Покормить"/"Выбрать"/"Отпустить" — как сейчас, но "Покормить и погладить" разделяется на две отдельные кнопки согласно разделу 1.

**Pokédex**: сетка **только когда-либо пойманных** видов (дедуп по `SpeciesId`, включая все стадии, до которых доходил хоть один питомец) — **без заглушек для непойманных**. Коллекция стартует пустой (кроме стартового вида после хатча) и растёт органически по мере игры — никаких "тёмных силуэтов" или счётчика "сколько всего существует", это осознанно проще референса. Каждая ячейка — спрайт, номер вида (dex-style, просто `SpeciesId`), имя, тег редкости.

**Тег редкости** — вычисляется из `CaptureRate` (уже есть в `SpeciesEvolutionChain`), простое разбиение на 4 корзины:
```
CaptureRate >= 150        → "Common"
70 <= CaptureRate < 150    → "Uncommon"
30 <= CaptureRate < 70     → "Rare"
CaptureRate < 30           → "Legendary"
```
На нынешнем 7-линейном каталоге это даст в основном "Uncommon"/"Common" — ожидаемо и нормально, разбиение рассчитано на будущий полный каталог (план Full Experience), тюнить пороги не нужно уже сейчас.

## Границы

- Магазин — не в этом дизайне, отдельным заходом позже.
- Полный каталог (Gen 1-5) и анимированные спрайты — по-прежнему в `2026-08-19-pokecorner-full-experience-and-installer.md`, не трогаем здесь; редизайн коллекции строится так, чтобы принять более широкий каталог без переделки.
- Иконка трея, ассеты — вне скоупа, как и раньше.
