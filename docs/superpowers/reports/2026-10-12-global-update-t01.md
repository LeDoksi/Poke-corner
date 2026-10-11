# T01 / POKE-53 — канонический офлайн-каталог

Принято 12 октября 2026, ветка `codex/global-update`, implementation commit `0d7f0f3`, base `6441a874`. Независимое task review: spec compliant, quality approved. Это первая поставка перехода; миграция, новый gameplay/UI и выпуск MSI ещё не готовы.

## Реализовано

`CatalogPackage.Load(DataRoot)` читает `Data/final/runtime-catalog.json` и `runtime-package.json`, проверяет версии/ревизии, allowlist, относительные пути, размеры и SHA каждого файла, GIF/WAV-заголовки. `CanObtain(speciesId, isShiny)` проверяет конкретный вариант. `GetAssetPath` / `GetVoicePath` возвращают локальные пути. Пакет содержит 1025 информационных видов, 868 normal GIF, 859 пригодных shiny, 868 WAV + crunch; у девяти blocked shiny нет operational path.

`RuntimePackage.props` генерируется из 2739 source→Data/target записей манифеста; тест сравнивает все mappings. App startup загружает пакет и не запускает сетевой recrawl; анимации/голоса используют его пути. Оригинальный v1 lookup и явные старые PNG/GIF/WAV сохранены до безопасного перехода T02. Это временно увеличивает payload; новые выдачи не должны обходить canonical eligibility.

Четыре точечных правила `.gitattributes` сохраняют LF в berry-profiles.json, location-draft.json, nursery-recipes-draft.json и rarity-draft-v2.json: Windows CRLF checkout нарушал SHA/size. JSON blobs не изменены. Полный generated diff сохранён в служебном workspace; review проверил компактный diff, XML skeleton и exact mapping test.

## Проверки

Baseline: Core 223/223 + App 262/262, exit 0. RED нового API: узкий прогон завершился exit 1 / CS0103 (`CatalogPackage` отсутствовал). GREEN: focused 11/11; полный `rtk proxy dotnet test PokeCorner.sln --no-restore --verbosity minimal` — Core 223/223 + App 273/273 = 496/496, 0 failed / 0 skipped, exit 0. Проверены весь output allowlist, неподдержанный shiny, legacy lookup и отказ неправильного пути/хеша/формата.

Воспроизводимая генерация без новых зависимостей:

```powershell
rtk proxy python src/PokeCorner.App/generate-runtime-props.py
rtk proxy python src/PokeCorner.App/generate-runtime-props.py --check
```

`--check`: 2739 entries, exit 0. Raw logs и implementer report: `.superpowers/sdd/2026-10-11-global-update-transition/task-1-{focused,full}-tests.log`, `task-1-report.md`. Служебный журнал сохраняется до завершения всего плана.

## Следующие границы

T02 разрешает фактически достигнутую старую форму прежним lookup до смены топологии, пишет отдельный state-v2.json и включает commit/publish барьер. V1, backup, GUID, shiny, уход, даты, клички, избранное, валюта и настройки не должны теряться. Ошибка загрузки не даёт новый starter/autosave; повреждённый существующий v2 не импортирует v1 повторно.

Текущие App.GetAnimatedFramesAsync / CollectionView callbacks принимают только speciesId. T14/T18 должны передавать shiny variant при отображении экземпляра. Существующий shiny flag не изменяется; T01 не объявляет новые выдачи или variant rendering завершёнными.

Личные saves/cache, установка MSI и manual compact/150–200% DPI inspection не выполнялись. POKE-34 является обязательной приёмкой перед распространением установщика девушке и другу: old→new MSI сохраняет прогресс, UpgradeCode ABEC4F80-F721-4D76-85F5-BCFF1CCBD9B4 неизменен. До неё установщик не считается готовым к выпуску.
