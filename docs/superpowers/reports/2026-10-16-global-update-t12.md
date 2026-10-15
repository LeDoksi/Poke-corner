# Приёмка T12 / POKE-37 — 2026-10-16

Независимое spec/quality review принято для code `29dc4c9`. Ниже исходный отчёт реализации и доработок; старые промежуточные counts сохранены как история.

# T12 — экран питомника и вылупления

Статус: **DONE_WITH_CONCERNS**. Ветка `codex/global-update`, исходный HEAD `423a353`, commit `e659dd4` (`feat: add durable nursery screen`). `git diff --cached --check` завершился с ExitCode 0 перед commit; в коммит вошли только восемь собственных source/test файлов. Экран собран на существующих `NurseryCatalog`, `NurseryService`, `App.PreviewNursery` и подтверждённых `StartNurseryAsync`/`HatchNurseryAsync`/`RefreshNurseryReadyAsync`. Он не создаёт второго расчёта исхода или сохранения.

## Поставка

- `NurseryView` выбирает два разных GUID принадлежащих игроку экземпляров. Второй список ограничен опубликованными совместимыми партнёрами; занятые походом/гнездом экземпляры недоступны. Активный спутник при выборе и запуске не меняется.
- Preview показывает корневые исходы (в том числе baby и одиночные семьи), неизвестные виды силуэтом, вес каждого исхода, его shiny-вероятность, доступные особенности с равными вероятностями, ягоды и время. Выбор вида доступен только при нескольких исходах; талисман и 4 дополнительные ягоды учитываются сервисом.
- Нехватка ягод или талисмана открывает магазин с заполненным количеством. Экран ничего не покупает сам.
- Running/ready читаются из сохранённого `Nest`, показывают родителей, зафиксированные цену и дедлайн. Случайный фактический `Nest.Child` до подтверждённого Hatch не читается для отображения. Результат раскрывается после сохранённого hatch; действия открывают карточку конкретного малыша в Покедексе либо отдельным подтверждённым commit делают его активным спутником.
- Start и Hatch удерживают GUID операции при ошибке/повторе; пока команда выполняется, выбор заблокирован. После `await` UI сверяет поколение выбора и опубликованный nest/archive/active GUID до сообщения об успехе. App-команды питомника используют существующий `CommitStateAsync(..., closeMenu: false)`; прочие вызывающие стороны сохраняют прежнее поведение. Нет кнопки отмены или выдуманного возврата.

## Проверки и TDD

- RED до production: `task-12-red.log`, `rtk proxy dotnet test tests/PokeCorner.App.Tests/PokeCorner.App.Tests.csproj --no-restore --filter FullyQualifiedName~NurseryUiTests --verbosity minimal`, ExitCode **1**, `CS0246` для отсутствующего `NurseryView`.
- GREEN: `task-12-focused-green.log`, тот же узкий фильтр, **7/7**, ExitCode **0**. Синтетические сценарии проверяют компактную ширину 600, стандартные кнопки с tab stop, совместимые GUID/отказ занятому, canonical preview, нехватку без покупки, выбранный исход и талисман, отказ записи и стабильный повтор Start/Hatch, скрытый исход и явную смену спутника.
- Core: `task-12-core-green.log`, `rtk proxy dotnet test tests/PokeCorner.Core.Tests/PokeCorner.Core.Tests.csproj --no-restore --verbosity minimal`, **256/256**, ExitCode **0**. После этого Core source не изменялся.
- Финальный App после последнего production/test изменения: `task-12-app-green.log`, `rtk proxy dotnet test tests/PokeCorner.App.Tests/PokeCorner.App.Tests.csproj --no-restore --filter FullyQualifiedName!~FinishReviewEvidenceRendersPopulatedPokedexAndResetArea --verbosity minimal`, **466/466**, ExitCode **0**. Исключён только известный тест захвата экрана, требующий `POKE_SHELL_CAPTURE_DIR`.
- Более ранний App gate в той же работе имел **464/465**, ExitCode **1**: `CollectionView_HeroSprite_MultiFrameSpecies_ActuallyCyclesFrames` увидел один кадр за 755 мс. Вывод сохранён в `task-12-app-intermittent-fail.log`. Узкий повтор 1/1 и последующие полные App gates прошли. Причина первого сбоя **не установлена**; assertion и фильтр не ослаблялись. Timer `NurseryView` стартует только на `Loaded` и останавливается на `Unloaded`; синтетические `WpfTestHost.Layout` не вызывают `Loaded`, поэтому связь с этим тестом не доказана.

## Самопроверка и границы

Прочитан собственный diff. Изменены только `NurseryView.xaml/.cs`, прямые `App`/menu/Shop/trait-label адаптеры и `nursery_uiTests.cs`. Экран не обращается к реальному профилю, MSI, ОС или сети. Полная визуальная/type/color/layout/DPI матрица и физический ввод принадлежат общему T20. Функционально проверена доступность WPF кнопок для Tab, но реальный клавиатурный ввод в окне не воспроизводился. Отдельная необязательная кличка на результатном экране не добавлена; существующая карточка Покедекса позволяет её задать. Риск первого несистематического сбоя hero-frame остаётся без установленной причины.

## Handoff

Доступные T12 вызовы: `App.PreviewNursery(parentIds, selectedSpeciesId)`, `StartNurseryAsync(NurseryRequest)`, `HatchNurseryAsync(nestId, operationId)`, `RefreshNurseryReadyAsync(now)`. Экран следует saved `Nest.Status` и `HatchedNurseryResults`; T20 может валидировать визуал без изменения доменного расчёта.

## Review fix 1 — 2026-10-16

После `task-12-review.md` исправлены четыре важных замечания и защитный случай отсутствующего архива. Commit `c13f801` (`fix: preserve nursery preview and hatch context`), пять собственных source/test файлов; staged `git diff --cached --check` — ExitCode 0. До production-правок добавлены синтетические WpfTestHost сценарии и выполнен неизменённый тестовый набор: `rtk proxy dotnet test tests/PokeCorner.App.Tests/PokeCorner.App.Tests.csproj --no-restore --filter FullyQualifiedName~NurseryUiTests --verbosity minimal`, **7/13 passed, 6 failed, ExitCode 1**. Полный raw output сохранён в `task-12-fix1-red.log` до изменения source. Падали видимый отказ одинаковому GUID, скрытие имени неизвестного выбранного вида, три случая результата с эффектом/удачей и показ frozen вариантов random-гнезда. Отдельный кейс shiny 0% на неподдерживаемом исходе уже проходил на RED и оставлен как явное доказательство.

В `NurseryService.Start` на том же candidate commit сохраняются только публичные `PossibleOutcomeSpeciesIds` из canonical `preview.Outcomes`; private `Nest.Child` и `Nest.Outcomes` по-прежнему содержат фактический случайный исход и не используются для раннего UI. `Hatch` сохраняет `LuckBeforeHatch` перед общим `ShinyRules.NextNurseryLuck`. Оба поля опциональны для старых v2 сохранений: при отсутствии списка UI честно пишет, что прежние варианты не сохранены, при отсутствии прошлой удачи не придумывает её изменение. `JsonSaveStore` проверяет новые поля, когда они есть. RNG, eligibility, родители, архив и выдача не менялись.

В running/ready экран показывает frozen номера всех возможных видов в случайном режиме и discovery-aware имя в выбранном. Реально выбранный random child до Hatch не читается для показа. Один и тот же GUID виден в списке второго родителя как недоступный с причиной. Подтверждённый результат использует только `HatchedNurseryResults`, показывает эффект первой особенности и сохранённое изменение/сброс/отсутствие изменения удачи; при отсутствующем архиве результат отмечен недоступным, переход и активация отключены.

После правки: `task-12-fix1-focused.log`, **14/14**, ExitCode **0**; финальный Core `task-12-fix1-core.log`, **256/256**, ExitCode **0**; финальный App `task-12-fix1-app.log`, **473/473**, ExitCode **0** с одним прежним capture-environment исключением. Последнее изменение теста лишь согласовало синтетический shiny draw с синтетическим ребёнком; после него App gate повторён и прошёл. Первоначальный одиночный hero-frame сбой из T12 остаётся с неустановленной причиной; ни assertion, ни фильтр не ослаблены. Реальные сохранения, DPI-матрица и физическая клавиатура не проверялись.
# T12 fix1 gate execution metadata

The following commands and ExitCode values are transcribed from the retained `exec_command` call and return records. The linked `.log` files contain their redirected raw output.

## Core

Command: `rtk proxy dotnet test tests/PokeCorner.Core.Tests/PokeCorner.Core.Tests.csproj --no-restore --verbosity minimal *> .superpowers/sdd/2026-10-11-global-update-transition/task-12-fix1-core.log`

`exec_command` returned `exit_code: 0`. Raw output: `task-12-fix1-core.log`.

## App

Command: `rtk proxy dotnet test tests/PokeCorner.App.Tests/PokeCorner.App.Tests.csproj --no-restore --filter FullyQualifiedName!~FinishReviewEvidenceRendersPopulatedPokedexAndResetArea --verbosity minimal *> .superpowers/sdd/2026-10-11-global-update-transition/task-12-fix1-app.log`

`exec_command` returned `exit_code: 0`. Raw output: `task-12-fix1-app.log`.

## Review fix 2 — 2026-10-16

The unsupported-shiny WpfTestHost assertion now matches the complete displayed zero percentage (`$"shiny {0:P2} ·"`) using the current culture, so a nonzero value beginning with `0` cannot pass. This is a one-line test-only correction; production code is unchanged.

Command: `rtk proxy dotnet test tests/PokeCorner.App.Tests/PokeCorner.App.Tests.csproj --no-restore --filter FullyQualifiedName~UnsupportedShinyOutcomeRendersZeroPercent --verbosity minimal *> .superpowers/sdd/2026-10-11-global-update-transition/task-12-fix2-focused.log; exit $LASTEXITCODE`

Observed `exec_command` exit_code: **0**; raw `task-12-fix2-focused.log`: **1/1 passed, 0 failed**. The earlier Core 256/256 and App 473/473 gates remain the validation for unchanged production code. No full suite was repeated for this assertion-only change.

Staged only `tests/PokeCorner.App.Tests/GlobalUpdate/nursery_uiTests.cs`; `git diff --cached --check` ExitCode **0**. Commit `29dc4c9` (`test: assert exact zero nursery shiny chance`), one test file, one insertion and one deletion.



## Independent review evidence: task-12-review.md

﻿### Spec Compliance

- Issues found: the running nest omits possible outcomes for random mode (NurseryView.xaml.cs:272-274; NURSERY.md:47), reveals the name of a selected undiscovered species (NurseryView.xaml.cs:273 versus OutcomeName at :244), and the hatch result lacks the trait effect and an explicit luck change/unchanged statement (NurseryView.xaml.cs:135-138; NURSERY.md:49).
- Cannot verify from this task's evidence: full visual quality, animation, compact-window and 150/200% DPI matrix, and physical keyboard behavior. T20 owns those checks. The optional nickname is reachable through the existing Pokedex card after OpenIndividual (App.xaml.cs:857; CollectionView.xaml.cs:361-367), but there is no direct result-screen entry.

### Strengths

- App.xaml.cs:849-867 wires the real App preview and commands. CommitStateAsync at :916-926 awaits the save/publish barrier; nursery commands alone pass closeMenu:false at :948 and :956-960, preserving other callers.
- NurseryView.xaml.cs:123-127 and :151-168 uses owned individual GUIDs, excludes the same GUID from partner choices, disables busy individuals, and gets the canonical preview. Lines :184-211 display outcome, trait and shiny probabilities; an unsupported shiny gets the preview's zero chance.
- NurseryView.xaml.cs:298-339 retains request/operation IDs on failure, blocks edits while busy, and checks published nest/archive state before reporting success. Lines :60-62 start and stop the timer with Loaded/Unloaded. Lines :348-365 open the actual child card and require a separate activation action.
- nursery_uiTests.cs:125-224 covers failed Start/Hatch retries, no early child disclosure, an unconfirmed command reply, and explicit activation with synthetic state.

### Issues

#### Important (Should Fix)

- NurseryView.xaml.cs:273 reveals SpeciesName(selected) in NestTerms even when the selected species has not been discovered. The preview uses OutcomeName at :244 to hide that name. Render the selected entry with the same discovery-aware label, retaining the allowed selected species number.
- NurseryView.xaml.cs:272-274 replaces a random nest's possible outcomes with a generic hidden-result label. NURSERY.md:47 asks for the variants while the actual draw stays hidden. Show the frozen recipe's possible outcomes without reading Nest.Child or Nest.Outcomes; keep the selected mode distinct.
- NurseryView.xaml.cs:135-138 shows only the trait label and current luck value. NURSERY.md:49 requires the trait's effect and whether luck increased, reset, or stayed unchanged after hatch. Derive the display from the saved child and saved shiny eligibility/bonus rules; do not expose these before confirmed hatch.
- nursery_uiTests.cs:61-78, :81-109 never assert the required unsupported-shiny 0% rendering or a visible same-GUID refusal as separate synthetic cases, despite task-12-brief.md step 2. Add focused WpfTestHost assertions for those cases. Existing tests validate service-mutated in-memory state; App.xaml.cs:916-926 was inspected to check the actual commit barrier.

#### Minor (Nice to Have)

- NurseryView.xaml.cs:133 falls back to Nest.Child if the hatch archive entry is absent, while the commands require HatchedNurseryResults. If the saved record is inconsistent, the screen can present a result whose card cannot be opened; prefer an explicit unavailable state. This is defensive against corrupt data, not a normal-path failure.

### Assessment

**Task quality:** Needs fixes

**Reasoning:** Durable command wiring and retry behavior are sound on the inspected path. The three result/visibility omissions conflict with the canonical Nursery screen contract, and two explicitly requested UI assertions are missing.

**Checks:** Read review-423a353..e659dd4.diff once; reread NurseryView.xaml.cs only because the diff output was truncated mid-hunk. Focused unchanged-code checks: NURSERY.md:35-49 for the disclosure/result contract; NurseryService.cs:45-79 and :82-167 for canonical preview/frozen hatch; AppState.cs:66-82 and :190-215 for save publication and CollectionViewModel.cs:112-113/CollectionView.xaml.cs:361-367 for the actual child card. No suite rerun. Reported evidence: RED CS0246 before production; focused 7/7, Core 256/256, App 466/466 after excluding only the known capture test. An earlier App run failed 1/465 because the hero-frame test saw one distinct frame over 755 ms; isolated 1/1 and later broad runs passed, but the cause remains unproven. No assertion/filter weakening is claimed.



## Independent review evidence: task-12-rereview1.md

﻿### Finding Verdicts

- Selected undiscovered name leak: ADDRESSED. NurseryView.xaml.cs:309 now uses OutcomeName(data, selected), which returns a number and "unknown species" until discovery (NurseryView.xaml.cs:261-262).
- Random running nest omits possible variants: ADDRESSED. NurseryService.cs:136 freezes preview.Outcomes species IDs in PossibleOutcomeSpeciesIds at Start; NurseryView.xaml.cs:305-309 lists those IDs for running/ready nests without reading Nest.Child or the private Outcomes payload. Legacy nests without the new list get an explicit unavailable-history message.
- Confirmed result lacks trait effect and luck change: ADDRESSED. NurseryService.cs:163 stores LuckBeforeHatch before NextNurseryLuck; NurseryView.xaml.cs:139-146 displays the saved archive child, mapped trait effect, and increased/reset/unchanged luck. Legacy missing before-value is stated rather than inferred.
- Separate 0% shiny and same-GUID UI cases absent: NOT ADDRESSED in full. The same-GUID case is now explicit and checks the disabled visible button (nursery_uiTests.cs:69-72; NurseryView.xaml.cs:165-173). UnsupportedShinyOutcomeRendersZeroPercent exists (nursery_uiTests.cs:261-272), but Assert.Contains("shiny 0", text.Text) also passes for a nonzero 0.01% or 0.99%. Assert the full culture-formatted zero percentage (for example String.Format("{0:P2}", 0d)) so the test actually guards the specified 0% behavior. Production UI still uses canonical Preview.ShinyChances and P2 at NurseryView.xaml.cs:226.
- Missing archive fallback could show an unconfirmed result: ADDRESSED. NurseryView.xaml.cs:134-146 now requires HatchedNurseryResults or shows an unavailable state; :125-126 and :384-407 disable/guard card and activation actions.

### New Breakage in the Fix Diff

- Important, nursery_uiTests.cs:272: the newly added unsupported-shiny assertion is too broad to distinguish zero from a small nonzero chance. This is the remaining piece of the previous test finding.
- No other new Critical/Important breakage found. JsonSaveStore.cs:416-424 accepts old missing metadata through the NurseryState.cs:13-14 defaults and validates populated IDs and pre-hatch luck; the new fields contain only canonical public alternatives and a pre-hatch bonus.

### Out-of-Scope Observations

- None. Full visual/DPI and physical-keyboard acceptance remains assigned to T20. The earlier hero-frame 1/465 failure remains unexplained; later reported runs pass without weakening the assertion or capture-test exclusion.

### Verdict

**Fix round:** Findings remain open: make the unsupported-shiny test assert an exact formatted zero percentage.

**Checks:** Read review-e659dd4..c13f801.diff once, the appended task-12-report.md fix section, and raw task-12-fix1-red.log. Reported RED 7/13, ExitCode 1; final focused 14/14, Core 256/256, App 473/473, each ExitCode 0. No suite rerun. Focused unchanged checks were limited to TraitRules.cs:12-52 and ShinyRules.cs:36-40 to evaluate the new trait/luck display and validator bounds.



## Independent review evidence: task-12-rereview2.md

﻿### Finding Verdicts

- Unsupported-shiny zero-percent UI assertion: ADDRESSED. nursery_uiTests.cs:272 now requires the entire culture-formatted zero percentage followed by the field delimiter, so a nonzero value beginning with 0 cannot pass. The previously addressed same-GUID assertion is unchanged.

### New Breakage in the Fix Diff

- None. The fix changes one test assertion and no production code (review-c13f801..29dc4c9.diff).

### Out-of-Scope Observations

- None. T20 still owns visual/DPI and physical-keyboard acceptance; the earlier hero-frame failure remains unexplained.

### Verdict

**Fix round:** All findings addressed, no new Critical/Important breakage.

**Checks:** Read the one-line fix package and appended task-12-report.md round-2 section. The retained focused log reports UnsupportedShinyOutcomeRendersZeroPercent 1/1, and the report records exec_command exit_code 0. No tests rerun. Prior Core 256/256 and App 473/473 gates cover unchanged production code.
