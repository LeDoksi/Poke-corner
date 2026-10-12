# Приёмка T03 / POKE-50 — 2026-10-12

Независимое spec/quality review принято для code `7855b69`. Ниже исходный отчёт реализации и доработок; старые промежуточные counts сохранены как история.

# T03 — levels, traits, exact shared residues

## Scope and APIs

- `LevelProgression(string dataRoot)` reads canonical `final/balance.json`. `XpForLevel`, `LevelForXp`, `TraitTierForXp`, `MaxXp`, and `AddXp(PokemonIndividual, int)` derive level and tier from persisted `TotalXp`. Addition caps at 6400. Crossing level 10 selects and persists a distinct second trait once. Care remains separate.
- `TraitRules(string dataRoot)` reads canonical trait values and evolution edges. `Allowed(speciesId)` excludes new fast-grower assignments for terminal species. `Value(traitId,tier)` and `Stack(effects)` expose exact decimal tier values and strongest 100/50/25 contributions. `SaveMigration.ChooseTrait` now calls the shared `TraitRules.Allowed` eligibility logic; previously owned fast-grower traits are untouched.
- Existing `Persistence.RationalRemainder` DTO moved unchanged in namespace into `Domain/RationalRemainder.cs` and gained `Settle(long numerator,long denominator)`. It uses `BigInteger` intermediates, reduces exact fractions, rejects invalid operands/state, checks storage overflow, and mutates only after a successful settlement.
- `SaveData.CoinRemainder`, existing `SaveData.BerryRemainders`, and `PokemonIndividual.XpRemainder` share that DTO. `JsonSaveStore.ValidateData` checks all three residue locations. Older v2 JSON omitting the new fields deserializes to zero residues through property initializers; v1 migration initializes zero residues explicitly. `JsonSaveStore.Clone` carries the fields.
- No expedition/shop/nursery gameplay or UI integration is in T03. Future claim handlers must call the domain operations inside the T02 `AppState.CommitAsync` snapshot boundary and publish only after commit.

## RED / GREEN

- RED: `rtk proxy dotnet test tests/PokeCorner.Core.Tests --no-restore --filter FullyQualifiedName~ProgressionTests --verbosity minimal` exited 1 before production edits: missing `LevelProgression`, `TraitRules`, and `RationalRemainder.Settle` compiler errors.
- Intermediate GREEN run found two erroneous test expectations: strongest stacking expected 0.1375 instead of 0.1875, and a residue denominator divisible by 7 reduced before overflow. Corrected the tests to the arithmetic and a coprime denominator.
- Focused GREEN: same command exited 0, 7/7 passed, 0 failed/skipped.
- Final full: `rtk proxy dotnet test PokeCorner.sln --no-restore --verbosity minimal` exited 0, Core 234/234 + App 337/337 = 571/571 passed, 0 failed/skipped. `rtk git diff --check` exited 0.
- Cases cover all 40 thresholds against the canonical JSON, boundary off-by-one, anchors 1200/2800/4800/6400, one-time second trait with JSON restart and cap, terminal fast-grower exclusion and previously owned trait, tier values and repeat ordering, 20 × 1.05 = 21, split versus long starting 2/7 with roundtrip, invalid and overflow residue behavior, and save schema roundtrip/invalid rejection.

## Limits

This task does not connect any gameplay reward action or WPF display to these APIs. No real saves, caches, MSI, or Windows settings were touched. No manual compact-window or physical-DPI acceptance was performed.

## T03 fix round 1 — accepted v2 XP with missing second trait

An accepted v2 save can have TotalXp >= 1200 with SecondTraitId null. Previous AddXp only chose while crossing level 10 and returned early at MaxXp. AddXp now fills a missing second trait whenever resulting XP is at least 1200, including at 6400. It preserves an existing choice and previous traits.

RED: `rtk proxy dotnet test tests/PokeCorner.Core.Tests --no-restore --filter FullyQualifiedName~LoadedV2MissingSecondTraitIsRepairedOnce --verbosity minimal` exited 1: 2/2 failed (null second trait at 1200 and 6400).
GREEN: `rtk proxy dotnet test tests/PokeCorner.Core.Tests --no-restore --filter FullyQualifiedName~ProgressionTests --verbosity minimal` exited 0: 9/9 passed. Fixture validates accepted v2 state, clones through T02 SaveData snapshot serialization, applies XP, clones/validates committed state, and confirms the chosen trait stays stable. `rtk git diff --check` exited 0. Full suite not repeated for this two-line domain correction; previous full evidence was 571/571 at a04d90e.

Fix commit: 7855b69.
