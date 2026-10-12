# Приёмка T04 / POKE-51 — 2026-10-12

Независимое spec/quality review принято для code `ebd4a48`. Ниже исходный отчёт реализации и доработок; старые промежуточные counts сохранены как история.

# T04 native WPF shell report — 2026-10-12

Base for implementation: `b4767f692c4c1fb9053e7c94fdf7421561362fda` (controller's accepted POKE-55 documentation; source baseline from `6d392c3`). Code/test head: `f87aa76` (`feat: build five-section native shell`). No persistence or gameplay source was changed.

## Shipped surface and handoff

- `AppMenuWindow.Navigate(MenuSection)` is the stable entry point. `MenuSection` offers `Corner`, `Pokedex`, `Shop`, `Expeditions`, `Nursery`, `Settings`, and `Catch`; Catch routes to the nested Поимка tab in Уголок. `SelectTab(int)` remains for existing four-tab callers/tests and ignores invalid indices. App tray, pet click, single-instance, shop, settings, catch-ready and reveal routes now use named sections.
- Five main tabs are Уголок, Покедекс, Магазин, Экспедиции, Питомник. Настройки has its own header action and returns to the previous section on Esc. The room, expeditions and nursery show neutral text until their owners provide real mechanics; no task IDs, fabricated rewards or mutating controls appear there. Existing CollectionView, ShopView, CatchView, SettingsView and confirmation dialogs remain connected.
- The shared WPF palette is cream paper with forest actions and selected tabs. The old dark hero background was corrected for existing collection, shop and catch content; each injected view gets explicit ink so selected-tab white header text does not leak into content. `BundledFont.Family` loads shipped `Data/fonts/nunito.ttf` from the executable directory; the focused test resolves its actual glyph URI rather than accepting silent fallback.
- Settings adds independent `EffectsSoundVolume` 0–100 (default 30) without playback on opening. Existing cry volume, fullscreen hiding, progress panel, pet size, autostart, onboarding, save history/restore and double-confirmed reset remain in their existing paths. Shell catch badge animation follows live `SystemParameters.ClientAreaAnimation`; no new motion toggle. Core audio effects will consume the existing setting when their later owner implements them.

## Verification

- Focused `rtk proxy dotnet test tests/PokeCorner.App.Tests/PokeCorner.App.Tests.csproj --no-restore --filter FullyQualifiedName~ShellTests --verbosity minimal`: 3/3 passed. Tests cover five sections, catch badge/navigation, Settings return via a routed Esc key, AutomationName, independent 0% effects volume, actual bundled Nunito typeface, and real WPF bitmap rendering.
- Full `rtk proxy dotnet test PokeCorner.sln --no-restore --verbosity minimal --filter "FullyQualifiedName!~StartStop_WithNoControllerConnected_RecordsNothingAndDisposesCleanly"`: Core 243/243 and App 358/358 passed, exit 0. Output: `t04-full-test.log`. The excluded test asserts no physical gamepad is connected; a first run without the exact exclusion failed that hardware assumption, and one frame-timing animation assertion failed under that run but passed standalone and in the final full run.
- `rtk git diff --cached --check`: exit 0 before commit. No user save, registry setting, installation or desktop pet was modified by verification.
- Native `RenderTargetBitmap` captures use actual WPF controls and theme, regular 820×660 at 150% render DPI and compact 640×480 at 200% render DPI. Captures were opened and visually reviewed. The first render exposed a dark hero/background and inherited white card text; both were corrected and affected screens were rendered again.

## Captures

All under `.superpowers/sdd/2026-10-11-global-update-transition/t04-captures/`:

- `regular-150.png`, `compact-200.png` — Уголок.
- `settings-compact-200.png` — Настройки with scrolling.
- `pokedex-regular-150.png`, `shop-regular-150.png`, `catch-regular-150.png` — inherited views after contrast correction.
- `expeditions-compact-200.png`, `nursery-compact-200.png` — neutral future sections.

These are synthetic render-DPI captures, not physical monitor or native input acceptance. The hidden HwndSource in the Esc test is solely a WPF key-event source. Real multi-monitor, window resizing, fullscreen interactions, persisted save recovery and device-specific input remain for T20. Existing shop egg inventory still reflects the legacy implementation and must be replaced by its gameplay owner; T04 did not alter purchases or persistence. HTML/CSS design detector was inapplicable to WPF and not rerun. No new assets or dependencies were added.

## Finish-review correction — 2026-10-12

The initial implementation head above was `f87aa76`; the four material findings in `task-4-review.md` were fixed in `ebd4a48` (`fix: address native shell review findings`). This is an addendum to the original verification, not a new implementation phase.

- Settings now records the actual selected main and nested tabs before showing its page. A direct `TabControl` selection returns to that exact section through Esc or the back action, including the nested Catch tab.
- Rarity and all type label foregrounds were retuned against the resolved paper surface to meet at least 4.5:1 text contrast while retaining distinct categories. The focused WPF test evaluates the applied brushes and templates rather than matching palette strings.
- Reset Data now uses a distinct danger ink instead of the forest primary-action color. Its existing double confirmation remains intact.
- The dark card and hero-card drop shadows were removed; paper borders provide the card separation.

Review-fix RED command: `rtk proxy dotnet test tests/PokeCorner.App.Tests/PokeCorner.App.Tests.csproj --no-restore --filter "FullyQualifiedName~DirectTabSelectionReturnsFromSettingsToTheExactView|FullyQualifiedName~RarityAndTypeLabelsMeetTextContrastOnTheirResolvedPaperSurface|FullyQualifiedName~ResetActionUsesDistinctDangerInk|FullyQualifiedName~PaperCardsUseTheirBorderWithoutDarkHalos" --verbosity minimal` — 4 failed as intended; output `t04-fix-red.log`.

Review-fix GREEN command: `rtk proxy dotnet test tests/PokeCorner.App.Tests/PokeCorner.App.Tests.csproj --no-restore --filter FullyQualifiedName~ShellTests --verbosity minimal` — 7/7 passed; output `t04-fix-green.log`.

Shared UI/theme full gate: `rtk proxy dotnet test PokeCorner.sln --no-restore --verbosity minimal --filter "FullyQualifiedName!~StartStop_WithNoControllerConnected_RecordsNothingAndDisposesCleanly"` — Core 243/243 and App 362/362, 605 passed; output `t04-fix-full.log`. The excluded test requires no physical gamepad and is the same hardware boundary described above.

The same eight PNG paths listed above were recaptured after the review fix (last modified 2026-10-12 20:41:44–20:41:45 local) and opened individually for visual inspection. They cover the same regular 820×660 at 150% and compact 640×480 at 200% synthetic render-DPI matrix; physical-monitor acceptance remains T20.

## Verdict 1 visual-evidence supplement — 2026-10-12

`task-4-verdict1.md` confirmed navigation and paper-card elevation, but requested two additional actual WPF views for the rarity/type labels and Reset Data danger color. No production UI, gameplay, persistence, or OS setting source was changed after `ebd4a48`. The added `FinishReviewEvidenceRendersPopulatedPokedexAndResetArea` test uses only in-memory `AppState`: four synthetic owned species with capture rates 200/100/45/3 (Common/Uncommon/Rare/Legendary) and normal/fire/electric/psychic types. It selects the actual inner `CollectionView` Pokédex tab, scrolls its content, and captures the rendered card templates. It then navigates to the actual `SettingsView`, scrolls to its bottom, and captures recovery history and the danger-colored "Очистить сохранённые данные" button without clicking it.

Focused command: `rtk proxy powershell -NoProfile -Command '$env:POKE_SHELL_CAPTURE_DIR = (Resolve-Path ".superpowers/sdd/2026-10-11-global-update-transition/t04-captures").Path; & rtk proxy dotnet test tests/PokeCorner.App.Tests/PokeCorner.App.Tests.csproj --no-restore --filter FullyQualifiedName~FinishReviewEvidenceRendersPopulatedPokedexAndResetArea --verbosity minimal'` — 1 passed, 0 failed, 0 skipped. Four preliminary fixture attempts failed on shell variable expansion, uninitialized visual tree, looking for the unhosted tab's visual child, and `IsVisible` on a non-windowed render root; the final fixture corrected these harness issues. The 605-test full gate from the previous section was not rerun because this batch adds only an evidence fixture.

New captures, each opened at original resolution for visual inspection: `t04-captures/pokedex-populated-150.png` (820×660 at 150%; all four tier labels and four type badges visible), and `t04-captures/settings-reset-compact-200.png` (640×480 at 200%; history action and red Reset Data action visible). The original eight captures remain untouched. The populated capture also shows species names with light text against the card paper; this separate visual issue was not changed in this bounded evidence-only batch and should be triaged independently. These captures remain synthetic render-DPI evidence, not physical display acceptance.


## Уточнённая область приёмки

Технический каркас T04 принят. Новые вкладки, настройки, общий шрифт, навигация и безопасность сохранены. Финальная визуальная приёмка всех новых экранов — после сборки в T20 по прямому решению пользователя; legacy-белые имена видов переданы T14, временные старые тела не полируются. Дизайн-система будет записана по окончательному интерфейсу.


## task-4-review.md

disposition: fix

## persistence

Pass: `PRODUCT.md` exists; the approved world is recorded in `docs/design-v2/final/DESIGN.md` and `index.html` (seed `pokecorner-fixed-room`). T04 is a code-led WPF shell, so a comp-round approval and hero reproduction checkpoint do not apply. All eight named RenderTargetBitmap captures are present, correctly labeled and visually valid. The native XAML build had no HTML/CSS detector. SDD spec compliance: issues found; task quality: **Needs fixes**. Critical: none. Reported final tests: Core 243/243 and App 358/358 passed with the physical-gamepad assumption excluded; real monitor and input acceptance remains T20.

## fidelity

| Element | Verdict | Evidence |
| --- | --- | --- |
| Five sections, separate Settings, nested Catch | match | `AppMenuWindow.xaml:62-100`; all eight captures show the intended route and scale. |
| Honest future section states and unbuilt room | acceptable adaptation | T04 contract assigns room to T17 and mechanics to later owners; `regular-150.png`, `expeditions-compact-200.png`, `nursery-compact-200.png` show no invented result. |
| TYPE | match | Bundled Nunito resolves to its shipped glyph file in `shellTests.cs:69-75`; lettering is consistent across captures. |
| MATERIAL | contradicted | `PokedexTheme.xaml:193-212` retains 1px borders plus 40–50% black shadows; Settings, Pokédex and Shop captures show dark halos around cream cards. |
| GROUND | match | `PokedexTheme.xaml:7-10` uses approved `#F8F4E9` paper surface and `#E9E2D3` ground; screenshots show those values as the net field. |
| Control meaning and readability | contradicted | The reset control resolves to green (`SettingsView.xaml:102`, `PokedexTheme.xaml:14`); unchanged rarity and type text colors sit on the new light cards (`PokedexTheme.xaml:28-31,249-257`). |
| Settings return to the current section | contradicted | `_lastSection` changes only in `Navigate`, while `MenuTabControl` and `CornerTabs` allow direct user tab selection (`AppMenuWindow.xaml.cs:77-109`, `AppMenuWindow.xaml:62,72`). |

The approved THESIS/STORY/FIRST VIEWPORT room promise is staged for T17; T04 keeps its shell topology. OWN-WORLD and native Operate FORM are visible, subject to the material and control defects above. The prior approved seed is corroborated by `docs/design-v2/final/index.html`.

## ceiling

Not reached: the forest selection, window frame, keyboard focus, Esc routing and Windows `ClientAreaAnimation` are present, but the inherited dark card elevation and light-ground accent contrast undercut the paper world's finish. The compact captures show the five-section rail without clipping; physical display and DPI behavior remains unverified by this synthetic evidence.

## material_fixes

1. **Important — navigation/contract:** Save the actual selected main and nested tab when entering Settings, or update the return target on selection changes, so a direct click from Pokédex, Shop or Catch returns to that exact view on Esc or “К разделам”; add a test that clicks the tab controls before opening Settings (`AppMenuWindow.xaml.cs:77-109`, `shellTests.cs:45-67`).
2. **Important — floor/contrast:** Retune rarity and type badge foregrounds for `#F8F4E9`, preserving category distinctions: current common/uncommon/rare/legendary colors measure about 2.20/1.68/2.48/1.66:1 on paper, below the 4.5:1 text floor (`PokedexTheme.xaml:28-31,249-257`; `CollectionView.xaml:707-711`).
3. **Important — control truth:** Give “Очистить сохранённые данные” a distinct danger color instead of the remapped `AccentRedBrush` forest green; its own code comment promises a red warning (`SettingsView.xaml:96-102`, `PokedexTheme.xaml:14`).
4. **Minor — MATERIAL/floor:** Reduce or remove the inherited black card shadows and choose border or shadow elevation for paper cards; the current 40–50% black effects read as dark halos in the Settings, Pokédex and Shop captures (`PokedexTheme.xaml:193-212`).

## keep

Keep the five-section/native Settings hierarchy, real bundled Nunito, honest placeholders for later owners, and preserved collection/shop/catch routes while fixing the four issues.


## task-4-verdict1.md

## verdict

1. **Resolved — Settings return.** `AppMenuWindow.xaml.cs:79,113-121` records the selected main and nested tabs before showing Settings; `shellTests.cs:18-38` exercises direct tab clicks and the back action. The existing Esc route uses the same saved section.
2. **Partial — rarity and type labels.** `PokedexTheme.xaml:29-32` and `TypeDisplay.cs:26-48` use darker foregrounds; the new WPF test checks the resolved brushes against the card at ≥4.5:1. The recaptured Pokédex has no populated rarity or type labels, so their actual on-screen legibility is not visible.
3. **Unresolved — reset danger color.** `SettingsView.xaml:102` binds a distinct `DangerTextBrush` and its test passes, but `settings-compact-200.png` ends above the reset action. The required visual confirmation of that control is absent from the recaptures.
4. **Resolved — card elevation.** `PokedexTheme.xaml:193-207` removes the black effects from both card styles; the Settings, Pokédex, Shop and Catch recaptures visibly show clean bordered paper cards without the former halos.

No regression from this fix batch is visible in the eight recaptures. SDD task quality remains **Needs fixes** for the two unconfirmed visual findings; no Critical code regression found. Reported 605-test gate passed; no suite was rerun.

## remaining

Provide a populated Pokédex capture with rarity and type text, and a scrolled Settings capture showing “Очистить сохранённые данные”; then score findings 2 and 3 from those actual WPF views. Synthetic render DPI does not establish physical display or input acceptance, which remains T20.

disposition: fix


## task-4-verdict2.md

## verdict
1. Resolved: Settings returns to the exact directly selected tab (prior scored finding; production unchanged).
2. Resolved: pokedex-populated-150.png visibly shows four dark readable rarity labels and Normal, Electric, Fire, Psychic type labels on paper; focused WPF test verifies >=4.5:1 for all.
3. Resolved: settings-reset-compact-200.png visibly shows the reset-data action in distinct red beside history/recovery.
4. Resolved: cards remain clean bordered paper without black halos (prior scored finding; production unchanged).
No fix-batch regressions. SDD spec compliance: pass; task quality: Approved.

## remaining
Clear for the four scored T04 fixes. Legacy species-name styling is T14; physical display, input, installer and save recovery acceptance are T20. Ship covers scored fixes only.

disposition: ship

Текстовые логи сохранены рядом в `2026-10-12-global-update-t04/` (UTF-16 stdout преобразован в UTF-8); десять native-captures — в `captures/`. Физические мониторы и MSI остаются T20.
