# Приёмка T10 / POKE-46 — 2026-10-12

Независимое spec/quality review принято для code `5e594e0`. Ниже исходный отчёт реализации и доработок; старые промежуточные counts сохранены как история.

# T10 / POKE-46 report

## Contract delivered

- `new NurseryCatalog(CatalogPackage)` reads `final/catalog-rules.json` for the package-relative recipe source, rejects paths outside `DataRoot`, checks recipe revision against `CatalogRevision`, and reads packaged `balance.json`. Source currently resolves to `nursery-recipes-draft.json`; despite that filename it is the accepted packaged canonical table.
- `Find(int, int)` returns a cached lookup object for a defined pair, including a disabled recipe when its parents/outcomes are unavailable. It is order independent. `Partners(int)` lists only partners of enabled recipes, including self where the same-species rule is enabled.
- `Find(PokemonIndividual, PokemonIndividual)` is T11's instance-level guard: it rejects equal GUIDs and looks up `CurrentSpeciesId` of each parent. Cosmetic override and original `SpeciesId` are not used. T11 still owns occupancy, persistence, resource commit, and result selection.
- `MixedRecipes` has 1046 definitions; `SameSpeciesRecipes` has 1025. `Outcomes` retains source roots and `NeedsThemeReview`; `AvailableOutcomes` filters by `CanObtain(normal)`. Unavailable parents disable the recipe. `Hours` is nullable and uses the highest rarity of available roots from balance. `Weight` is the balance weight scaled by two to preserve the Legendary 0.5 ratio as integer 1. `ShinyAvailable` is independently read from `CanObtain(shiny)`. Base berries remain from the canonical table; surcharge is empty when choice is unavailable. Same-species berries and roots are the table values (Raichu/Pichu, Vaporeon/Eevee).
- Core test fixture copies the canonical rules, balance, recipe, and runtime species tables into a synthetic SHA-verified `CatalogPackage` with one tiny controlled GIF. It has no dependency on App build outputs or real saves. Core test csproj needed no edit.

## Verification (raw command output)

RED, exit 1:

```text
rtk proxy dotnet test tests/PokeCorner.Core.Tests/PokeCorner.Core.Tests.csproj --no-restore --filter FullyQualifiedName~NurseryCatalogTests --verbosity minimal
  PokeCorner.Core -> C:\Users\Georgy\.codex\worktrees\global-update\PokeTokenBar-Windows\src\PokeCorner.Core\bin\Debug\net8.0\PokeCorner.Core.dll
tests\PokeCorner.Core.Tests\GlobalUpdate\nursery_catalogTests.cs(14,13): error CS0246: Не удалось найти тип или имя пространства имен "NurseryCatalog" (возможно, отсутствует директива using или ссылка на сборку).
```

Focused GREEN after implementation and expanded edge assertions, exit 0:

```text
rtk proxy dotnet test tests/PokeCorner.Core.Tests/PokeCorner.Core.Tests.csproj --no-restore --filter FullyQualifiedName~NurseryCatalogTests --verbosity minimal
  PokeCorner.Core -> C:\Users\Georgy\.codex\worktrees\global-update\PokeTokenBar-Windows\src\PokeCorner.Core\bin\Debug\net8.0\PokeCorner.Core.dll
  PokeCorner.Core.Tests -> C:\Users\Georgy\.codex\worktrees\global-update\PokeTokenBar-Windows\tests\PokeCorner.Core.Tests\bin\Debug\net8.0\PokeCorner.Core.Tests.dll
Тестовый запуск для C:\Users\Georgy\.codex\worktrees\global-update\PokeTokenBar-Windows\tests\PokeCorner.Core.Tests\bin\Debug\net8.0\PokeCorner.Core.Tests.dll (.NETCoreApp,Version=v8.0)
Версия VSTest 17.11.1 (x64)

Запуск выполнения тестов; подождите...
Общее количество тестовых файлов (1), соответствующих указанному шаблону.
Пройден!   : не пройдено     0, пройдено     3, пропущено     0, всего     3, длительность 369 ms. - PokeCorner.Core.Tests.dll (net8.0)
```

Final full solution, exit 0:

```text
rtk proxy dotnet test PokeCorner.sln --no-restore --verbosity minimal
  PokeCorner.Core -> C:\Users\Georgy\.codex\worktrees\global-update\PokeTokenBar-Windows\src\PokeCorner.Core\bin\Debug\net8.0\PokeCorner.Core.dll
  PokeCorner.Core.Tests -> C:\Users\Georgy\.codex\worktrees\global-update\PokeTokenBar-Windows\tests\PokeCorner.Core.Tests\bin\Debug\net8.0\PokeCorner.Core.Tests.dll
Тестовый запуск для C:\Users\Georgy\.codex\worktrees\global-update\PokeTokenBar-Windows\tests\PokeCorner.Core.Tests\bin\Debug\net8.0\PokeCorner.Core.Tests.dll (.NETCoreApp,Version=v8.0)
Версия VSTest 17.11.1 (x64)

Запуск выполнения тестов; подождите...
Общее количество тестовых файлов (1), соответствующих указанному шаблону.
Пройден!   : не пройдено     0, пройдено   226, пропущено     0, всего   226, длительность 770 ms. - PokeCorner.Core.Tests.dll (net8.0)
  PokeCorner.App -> C:\Users\Georgy\.codex\worktrees\global-update\PokeTokenBar-Windows\src\PokeCorner.App\bin\Debug\net8.0-windows\PokeCorner.dll
  PokeCorner.App.Tests -> C:\Users\Georgy\.codex\worktrees\global-update\PokeTokenBar-Windows\tests\PokeCorner.App.Tests\bin\Debug\net8.0-windows\PokeCorner.App.Tests.dll
Тестовый запуск для C:\Users\Georgy\.codex\worktrees\global-update\PokeTokenBar-Windows\tests\PokeCorner.App.Tests\bin\Debug\net8.0-windows\PokeCorner.App.Tests.dll (.NETCoreApp,Version=v8.0)
Версия VSTest 17.11.1 (x64)

Запуск выполнения тестов; подождите...
Общее количество тестовых файлов (1), соответствующих указанному шаблону.

Пройден!   : не пройдено     0, пройдено   327, пропущено     0, всего   327, длительность 3 s. - PokeCorner.App.Tests.dll (net8.0)
```

## Scope and limits

Commit: `9c6b9b7` (`feat: add nursery compatibility catalog`). Only `NurseryCatalog.cs`, `NurseryRecipe.cs`, and `nursery_catalogTests.cs` changed for the commit. UI, WPF DPI, actual save/commit, RNG, time reduction, shiny chance, offspring inheritance, gender, and gameplay timers remain T11 or later work. Recipe thematic review remains pending where `NeedsThemeReview` is true.

## Review fix: published collections

Review identified that `IReadOnlyList`/`IReadOnlyDictionary` properties held mutable `List`, arrays, and dictionaries. A consumer could cast and corrupt an outcome, cost, or partner list used by later `Find` calls. `NurseryRecipe` now snapshots outcome arrays with `Array.AsReadOnly` and berry dictionaries with `ReadOnlyDictionary` over fresh copies; `NurseryCatalog` publishes read-only recipe lists and partner arrays. The regression test tries both direct casts to the old mutable concrete types and writes through collection interfaces, then checks a repeated reversed `Find` and public lists. No source table or lookup decision changed. Scope: the same three T10 files, no persistence/App edits. Commit: `5e594e0` (`fix: protect cached nursery recipe collections`). Full solution was not rerun because this fixes publication of existing cached values; the focused suite covers all affected surfaces. Latest full solution at the T02 integration checkpoint was 563 passing, per controller.

RED before fix, exit 1:

```text
rtk proxy dotnet test tests/PokeCorner.Core.Tests/PokeCorner.Core.Tests.csproj --no-restore --filter FullyQualifiedName~PublishedCollectionsCannotChangeCachedRecipes --verbosity minimal
  PokeCorner.Core -> C:\Users\Georgy\.codex\worktrees\global-update\PokeTokenBar-Windows\src\PokeCorner.Core\bin\Debug\net8.0\PokeCorner.Core.dll
  PokeCorner.Core.Tests -> C:\Users\Georgy\.codex\worktrees\global-update\PokeTokenBar-Windows\tests\PokeCorner.Core.Tests\bin\Debug\net8.0\PokeCorner.Core.Tests.dll
Тестовый запуск для C:\Users\Georgy\.codex\worktrees\global-update\PokeTokenBar-Windows\tests\PokeCorner.Core.Tests\bin\Debug\net8.0\PokeCorner.Core.Tests.dll (.NETCoreApp,Version=v8.0)
Версия VSTest 17.11.1 (x64)

Запуск выполнения тестов; подождите...
Общее количество тестовых файлов (1), соответствующих указанному шаблону.
[xUnit.net 00:00:00.36]     PokeCorner.Core.Tests.GlobalUpdate.NurseryCatalogTests.PublishedCollectionsCannotChangeCachedRecipes [FAIL]
  Не пройден PokeCorner.Core.Tests.GlobalUpdate.NurseryCatalogTests.PublishedCollectionsCannotChangeCachedRecipes [140 ms]
  Сообщение об ошибке:
   Assert.Equal() Failure: Values differ
Expected: 451
Actual:   999
  Трассировка стека:
     at PokeCorner.Core.Tests.GlobalUpdate.NurseryCatalogTests.PublishedCollectionsCannotChangeCachedRecipes() in C:\Users\Georgy\.codex\worktrees\global-update\PokeTokenBar-Windows\tests\PokeCorner.Core.Tests\GlobalUpdate\nursery_catalogTests.cs:line 135
   at System.RuntimeMethodHandle.InvokeMethod(Object target, Void** arguments, Signature sig, Boolean isConstructor)
   at System.Reflection.MethodBaseInvoker.InvokeWithNoArgs(Object obj, BindingFlags invokeAttr)

Не пройден!: не пройдено     1, пройдено     0, пропущено     0, всего     1, длительность < 1 ms. - PokeCorner.Core.Tests.dll (net8.0)
```

GREEN after fix, exit 0:

```text
rtk proxy dotnet test tests/PokeCorner.Core.Tests/PokeCorner.Core.Tests.csproj --no-restore --filter FullyQualifiedName~NurseryCatalogTests --verbosity minimal
  PokeCorner.Core -> C:\Users\Georgy\.codex\worktrees\global-update\PokeTokenBar-Windows\src\PokeCorner.Core\bin\Debug\net8.0\PokeCorner.Core.dll
  PokeCorner.Core.Tests -> C:\Users\Georgy\.codex\worktrees\global-update\PokeTokenBar-Windows\tests\PokeCorner.Core.Tests\bin\Debug\net8.0\PokeCorner.Core.Tests.dll
Тестовый запуск для C:\Users\Georgy\.codex\worktrees\global-update\PokeTokenBar-Windows\tests\PokeCorner.Core.Tests\bin\Debug\net8.0\PokeCorner.Core.Tests.dll (.NETCoreApp,Version=v8.0)
Версия VSTest 17.11.1 (x64)

Запуск выполнения тестов; подождите...
Общее количество тестовых файлов (1), соответствующих указанному шаблону.

Пройден!   : не пройдено     0, пройдено     4, пропущено     0, всего     4, длительность 647 ms. - PokeCorner.Core.Tests.dll (net8.0)
```
