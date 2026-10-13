# Приёмка T06 / POKE-49 — 2026-10-13

Независимое spec/quality review принято для code `8adf284118b26b8bf101533b8a81db3fd0014a0b`. Ниже исходный отчёт реализации и доработок; старые промежуточные counts сохранены как история.

# T06 / POKE-49 — очередь встреч и однократная выдача

Base: `510fc4778d29b8398468869bcb255e5d1154dafd`. Implementation head: `cbd7557ab4509bf3422804bab70c7340e65978f5`.

## Поставка

- `EncounterService.Reserve(SaveData, source, operationId, knownSpeciesId?)` и перегрузка для live-очереди создают стабильный ID закрытой встречи. `ReserveGift` привязывает уже зарезервированный миграцией экземпляр, не создавая новые свойства. `Open(SaveData, encounterId)` изменяет только кандидат снимка: сохраняет фазу/result, добавляет экземпляр один раз, обновляет открытые виды и расходует старый активный shiny-талисман только при технически доступном варианте. Повторный ID возвращает тот же экземпляр. Экспедиция передаёт вид, определённый своим источником; без него открытие запрещено.
- Активность выдаёт покебол каждые 22 500 действий и сохраняет очередь без старого лимита в три. Выбор при открытии: новый/повтор 80/20, после дубля гарант нового при доступном неизвестном корне, затем веса редкости 60/30/9/1 по непустым категориям. Уже открытые виды остаются известными после передачи экземпляра. Подарок и экспедиция обновляют неизвестный пул, но не флаг гаранта активности.
- Пул строится из всего подписанного `runtime-catalog.json` через `CanIssueDirectly`, включая baby и одиночные корни. Пустое место старого lookup (#490 в тестовых данных) больше не исключает допустимый корень. Новые экземпляры получают v2 family/root/reached-species/trait/XP поля. Существующие развитые и shiny экземпляры не переписываются.
- UI открывает первый заработанный закрытый покебол через `AppState.CommitAsync`; успех, счётчик и экземпляр публикуются после подтверждённой записи. Двойное нажатие удерживается, отказ отображается без выдачи, после анимации действие — «Продолжить». Содержимое и свойства закрытой активности не показываются. Старый `PendingCatchCount` материализуется в очередь при загрузке приложения, сохраняя семантику старого сохранения и чистый `AppState` round-trip.
- Результат определяется детерминированно по ID встречи до commit; при отказе записи новая попытка, включая новый экземпляр сервиса после перезапуска, получает тот же вид, характер, особенность и ID. После успешного commit результат и фаза записаны в save. `JsonSaveStore` проверяет фазы, уникальные ID, связь открытых экземпляров и подарков; `SaveMigration.ValidateCatalog` проверяет сохранённые виды.

## Проверки

- RED до реализации: `rtk proxy dotnet test tests/PokeCorner.App.Tests/PokeCorner.App.Tests.csproj --no-restore --filter FullyQualifiedName~EncountersTests --verbosity minimal` → exit 1, компиляция: отсутствуют `EncounterService`, `EncounterOpenResult`, `AppState.Encounters`, `SaveData.ActivityNewGuaranteed`.
- Первый полный gate выявил изменение round-trip при старом `PendingCatchCount`: Core 227/228, App 378/379, exit 1. Перенёс материализацию старого счётчика из `AppState.Hydrate` в startup App; прежние снимки снова проходят дословный round-trip.
- Дополнительный RED каталога: тест полного набора корней выявил отсутствующий в legacy lookup корень #490. После перехода на canonical catalog проверка проходит.
- `rtk proxy dotnet test tests/PokeCorner.App.Tests/PokeCorner.App.Tests.csproj --no-restore --filter FullyQualifiedName~EncountersTests --verbosity minimal` → 10/10, exit 0. Проверены commit/restart/repeat, отказ записи, подарок, новизна/гарант, закрытый UI, недоступный shiny, полный корневой пул, выдача корня без legacy lookup, отсутствие старого лимита очереди.
- `rtk proxy dotnet test PokeCorner.sln --no-restore --verbosity minimal --filter "FullyQualifiedName!~FinishReviewEvidenceRendersPopulatedPokedexAndResetArea&FullyQualifiedName!~StartStop_WithNoControllerConnected_RecordsNothingAndDisposesCleanly"` → Core 228/228, App 382/382, exit 0. Исключены только тест захвата экрана с обязательным `POKE_SHELL_CAPTURE_DIR` и тест, предполагающий отсутствие ввода с физического геймпада; остальные ShellTests выполнялись.
- `rtk git diff --check` → exit 0. Настоящие сохранения, настройки Windows и MSI не трогались. WPF-функциональный путь проверен тестом закрытого состояния и общей App-сборкой; вся визуальная матрица размеров/DPI — T20.

## Границы и самостоятельное ревью

- T07 должен вынести shiny/charm-процедуру в общий сервис, убрать старый скрытый множитель для дублей из оставшихся путей и подключить `Items["lucky-charm"]`. Сейчас T06 использует базу 1/512, множитель активированного старого талисмана ×4 и guard неподдержанного shiny; новый купленный предмет остаётся в инвентаре до T07.
- T11 может зарезервировать видимую экспедиционную встречу через `Reserve(..., "expedition", operationId, knownSpeciesId)` в том же снимке, что claim. T19 передаёт сохранённый `ReservedGift.Individual.Id` в `ReserveGift`, затем вызывает `Open` внутри одного commit; сам закрытый покебол до открытия не читает `Gift.Gifts`.
- Открытые результаты сохраняются в журнале встреч для повторного ID. Он растёт вместе с количеством поимок; при практическом росте файла понадобится компактный журнал без потери idempotence. Экземпляр получает `CaughtAtUtc` времени резервирования покебола, чтобы результат остался тем же и после отказа записи/перезапуска.
- Повторная проверка сохранения, отказа и снимка сделана на синтетических данных. Аппаратную DPI/анимационную приёмку и полный визуальный finish review сейчас не проводили согласно `GLOBAL-WORKFLOW.md`.

## Fix round 1 — 2026-10-13

- `ReserveGift` checks an existing operation's source and gift ID before validating a new reservation. The same operation replays an opened gift after save/restart and `Open` returns its recorded individual; mismatched gift/source refuses without adding an encounter. New operations still require an eligible `reserved` gift.
- Earned activity events now hold stable operation IDs in `AppState` until a candidate containing the reserved encounters and count reaches `CommitAsync`. `App` starts that commit after activity, treat and coin credit finishes. Tray badge, menu count and first-ball notification update only after confirmed publication. Extra activity arriving during an in-flight commit is buffered by the existing `AppState` barrier and then processed with its own ID. On failure, the previous encounter/count remains visible, a save error is shown, and the earned ID remains queued. A later retry can reuse it; the failed-writer recovery/history snapshot includes the queued ID. Normal exit awaits the commit task before flushing. `BackgroundSaveWriter` latches a disk failure, so that writer cannot retry until recovery creates a usable writer.
- Regression tests cover save/restart/repeat for the gift, a delayed reservation write, buffered second threshold, failed write, recovery snapshot validity, and retry without duplicate IDs. Initial RED compilation recorded the missing activity reservation API. The first gift test run failed because its synthetic fixture had one gift while v2 validation requires zero or two; it was corrected before the passing test. A behavioral RED for gift replay against the original implementation was not captured, and no later rollback was performed merely to produce it.
- Raw stdout: `task-6-fix1-focused.log` (EncountersTests 12/12, exit 0) and `task-6-fix1-full.log` (Core 228/228, App 390/390, exit 0). Exit values and the two environment-dependent full-gate exclusions are also in `task-6-fix1-exits.log`. These tests use synthetic profiles; no real save, MSI or OS state was changed. Full visual/DPI acceptance remains T20.
- Handoff correction: the expedition caller is T08; T19 supplies the migrated gift caller. The earlier T11 reference above was a task-number typo.


## Independent scoped re-review

### Spec Compliance

- ✅ ADDRESSED — opened gift replay and matching identity: `ReserveGift` checks an existing encounter's source and gift ID before requiring a newly reserved gift (`src/PokeCorner.Core/Domain/EncounterService.cs:72-87`). The new save/restart test repeats reservation and opening, verifies the original individual and single grant, and rejects a mismatched gift ID (`tests/PokeCorner.App.Tests/GlobalUpdate/encountersTests.cs:134-169`).
- ✅ ADDRESSED — confirmed commit before activity publication: `OnCatchRollDue` only queues a stable ID (`src/PokeCorner.App/App.xaml.cs:584-588`); `CommitActivityReservationsAsync` commits that ID/count before updating tray/menu state or notifying the player (`src/PokeCorner.App/App.xaml.cs:597-617`). `AppState.CommitAsync` builds a candidate and publishes it only after `CommitAndPublishAsync` succeeds (`src/PokeCorner.Core/AppState.cs:56-69`).
- ⚠️ CannotVerify here: the synthetic tests establish delayed write, failure, recovery snapshot and retry behavior, while real save failure/exit and physical UI timing remain outside this scoped diff (`tests/PokeCorner.App.Tests/GlobalUpdate/encountersTests.cs:171-209`). The updated report states focused 12/12 and full Core 228/App 390 passed with raw logs; these suites were not rerun.

### Strengths

- Captured IDs are removed from the pending list only after a successful commit (`src/PokeCorner.Core/AppState.cs:33-43`). A failed write leaves the ID queued, and `ToSaveDataIncludingPendingActivity` reserves it in the recovery candidate (`src/PokeCorner.Core/AppState.cs:45-50`).
- Activity credited during an in-flight commit is deferred by `RunWhenReady`/`CommitAsync` (`src/PokeCorner.Core/AppState.cs:52-77`), then the reservation loop sees and commits the additional ID (`src/PokeCorner.App/App.xaml.cs:597-617`). The new test covers a delayed first write, buffered second threshold, failed second write and retry without duplicate IDs (`tests/PokeCorner.App.Tests/GlobalUpdate/encountersTests.cs:171-209`).
- Normal exit awaits the activity task and captures a recovery snapshot containing pending IDs on a latched writer failure (`src/PokeCorner.App/App.xaml.cs:875-885`); recovery and failure handlers use the same snapshot method (`src/PokeCorner.App/App.xaml.cs:931,1011`).

### Issues

#### Critical (Must Fix)

- None introduced by this fix.

#### Important (Should Fix)

- None introduced by this fix.

#### Minor (Nice to Have)

- None identified in this scoped review. The report explicitly notes that a behavioral RED for gift replay was not captured before the fix (`.superpowers/sdd/2026-10-11-global-update-transition/task-6-report.md`, Fix round 1).

### Assessment

**Task quality:** Approved for the scoped round-1 fix.

**Reasoning:** Both previously blocking behaviors are corrected. The queued-ID path retains a failed reservation and commits activity arriving during the first write; no new Critical or Important issue was found in this fix.

**Focused shared-caller check:** Inspected `AppState.CommitAsync` and its deferred mutation drain at `src/PokeCorner.Core/AppState.cs:56-77` to assess whether a second threshold can be lost while the first ID is being written. No suites were rerun.
