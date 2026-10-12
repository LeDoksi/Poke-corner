# T02 / POKE-52 — v2 schema, isolated migration and publication barrier

Status: DONE_WITH_CONCERNS (implementation ready for independent review; global-update release/MSI acceptance is still gated).
Branch: codex/global-update.
Base: a94cc783883b60b8c1e68d3977352c796a4d5cc2.
Tested implementation SHA: 0219b22158b1699de71f145916940af6273a509b.
A documentation-only follow-up commit records this report and raw logs.

## Scope and caller reach

Owned production files: SaveData, JsonSaveStore, SaveMigration (new), BackgroundSaveWriter, AppState, AppSettings, PokemonIndividual, AppPaths and App.xaml.cs. Tests are in tests/PokeCorner.App.Tests/GlobalUpdate/migrationTests.cs.

Two controller-approved additions were necessary:
- CatalogPackage exposes read-only DataRoot and CatalogRevision from the already validated package, so Migrate's exact signature consumes the shipped tables without hard-coded installation paths or catalog DTO mutation.
- SpeciesEvolutionChainExtensions.ResolveDisplaySpeciesId and CollectionViewModel.BuildEvolutionPreview honor persisted actual/reached v2 species. Otherwise a migrated custom Eevee→Jolteon branch could be displayed or offered as the bundled Eevee→Vaporeon branch. All resolver callers were searched: App overlay/cry paths, collection ordering/entries and existing Core display tests. V1 behavior remains unchanged when explicit v2 fields are absent.

No new database, dependency, parallel store or generic operation framework. Existing JSON decoding and BackgroundSaveWriter own disk writes. No full gameplay services from T03–T19 were implemented.

## Exact operations

- AppPaths.GetV2SaveFilePath(overrideRoot?) returns state-v2.json alongside the existing state.json.
- LegacyLookup.Load(dataRoot, existingCachePath?, preservedLookupPath?) first accepts an explicitly preserved lookup, otherwise a usable current v1 cache, otherwise the SHA/size-verified immutable bundled fallback. Reserved LegacyOfflineChains are appended for resolution.
- LegacyLookup.PreservedLookupFileName = legacy-v1-lookup.json. SHA256 covers exact OriginalBytes; the exposed byte array is a copy.
- SaveMigration.Migrate(v1, legacyLookup, catalog) validates and deep-copies v1; resolves actual old stages before topology changes; records original lineage/SHA; initializes one-time traits, proof-based awards and a compatible gift reservation. Source objects are not mutated.
- SaveMigration.LoadAsync(v1Path, v2Path, lookup, catalog) loads existing v2 only and never reimports v1 after a v2 error. With no v2, validated v1 migrates into a separate file; with neither file it returns null for new-game selection.
- JsonSaveStore.Load(out originalBytes) provides the exact bytes decoded, preventing a second read from backing up different input. Load() remains compatible with existing callers.
- SaveMigration.RestoreBackup(backupPath, targetPath, expectedSchemaVersion) validates the selected backup/version before writing; retains rejected original bytes as target.rejected-SHA; restores through temp+atomic replacement.
- AppState.CommitAsync(writer, update, pendingCatchCount, afterPublish?) snapshots current state, runs update on that deep clone, validates it, awaits durable commit, hydrates the committed state, then invokes afterPublish before replaying queued activity/mutations.
- App.CommitStateAsync(update) is the App-level boundary: disables existing menu interaction while awaiting commit, updates the App-owned pending encounter count during publication, and refreshes the view after success.
- BackgroundSaveWriter.CommitAndPublishAsync(snapshot, publish) suppresses Post and Flush throughout disk commit and UI publication. Raw CommitAsync retains its existing documented ordering behavior for legitimate later writes.
- AppState.RunWhenReady(Action) defers mutations, ApplyActivityUnits buffers input while a commit awaits, and FlushAsync waits for all pending publication barriers before capturing exit state. Consecutive commits re-snapshot published state.
- Graceful WPF exit stops activity sources, drains queued input, awaits the final writer task without blocking dispatcher continuation, and cancels exit visibly on I/O failure. Exit/crash/autosave are guarded until successful hydration; while publication awaits they never flush old live state.
- Explicit reset retains existing UI confirmations, deletes both active v1/v2 files (so v1 cannot resurrect), and retains immutable migration backups.

## V2 persisted contract

SaveData keeps old progress fields and adds:
- SchemaVersion=2; CatalogRevision from CatalogPackage; BalanceRevision=lowercase SHA256 of exact shipped balance.json bytes; MigrationId=global-update-v1.
- DiscoveredSpeciesIds, ShinySpeciesIds and EvolutionTransitions ("from->to"), accumulated only from proven actual paths.
- Metrics with nullable long values: unknown legacy activity histories are null, never invented zeros. Proven care readiness is retained separately without minting an unproved first-feeding award.
- AwardLedger: AwardId → AwardedAtUtc, ClaimStatus, CoinsGranted, CatalogRevision, AvailableSpeciesIds. Proven retro awards include previous quantitative thresholds; final-category awards snapshot availability/revision. Coin overflow rejects migration without clamping or writing.
- Berries: oran, cheri, pecha, rawst, aspear, chesto, initially zero.
- Items: treats, care-booster, lucky-charm, evolution-stone, offspring-charm, xp-potion. Five new-item counts start zero.
- FeedingTreats / live Feeding.Treats is the ONE authoritative treat balance. Items["treats"] is a serialized compatibility alias: derived during snapshots/commits and equality-validated on v2 decode. T05 must spend/add the authoritative value, never treat the alias as an additional spendable inventory.
- BerryRemainders use integer Numerator/Denominator, initially 0/1; denominator>0 and 0<=numerator<denominator.
- Tutorial: TutorialRevision=global-update-v1, Status not-started/in-progress/completed/skipped, CurrentStep 1..6, SeenHintKeys.
- Gift: GrantId=global-update-compatible-pair-v1, RecipeId, ExcludedSpeciesId, two separate reserved/opened entries containing full persisted individual properties/IDs.
- Expedition/Nest are neutral null timed-activity slots. Their DTOs hold Id, Status, ParticipantIds, StartedAtUtc, ReadyAtUtc, DurationMinutes, ResourcePrices, Conditions and Outcomes. Validation rejects duplicate busy participation. Dependent services own launch/claim/hatch mechanics.
- CompletedOperations stores operation-key → result GUID references for consumers; no operation executor framework.

PokemonIndividual keeps all old identity/progress/cosmetic fields and adds:
- CurrentSpeciesId (actual reached form), FamilyRootId, LegacyOrigin(original SpeciesId/ChainId/ChainLength/CurrentStage/LookupSha256).
- TotalXp=0 at migration; no care→XP conversion or stored derived level/tier.
- FirstTraitId chosen once from the shipped traits table; fast-grower excluded when there is no outgoing evolution; SecondTraitId initially null.
- ReachedSpeciesIds contains only the exact proven old path; cosmetic overrides select these IDs and cannot change actual CurrentSpeciesId or reveal another branch.
- AcquisitionSource=unknown and empty ParentIds for unproven v1 history.
- Reserved gifts carry source update-gift, fixed GUID/nature/trait and one saved eligibility-aware base shiny decision. They are not added to Individuals/discoveries before opening.

AppSettings retains all 11 v1 fields and adds independent EffectsSoundVolume=30 (0–100), with 0 preserved as silence. Care timestamps are copied, including a missing checkpoint; existing DiscountIdleGap preserves the ordinary one-hour grace on the first poll.

## Migration choices and neutral states

Actual form resolution matches original starter/chain/length against the exact legacy lookup and rejects absent/ambiguous paths. DisplayStageOverride never acts as evolution rollback. Unsupported existing shiny remains shiny even when CanObtain(...,true) is false. PendingEggs is the only deliberately removed old progress.

Compatible gifts are selected from active catalog-rules giftRecipeCandidates/nursery recipes, preferring undiscovered members, then shortest recipe. Both different Common/Uncommon members must have normal resources and exclude the known companion's actual species. Empty existing saves and nonempty saves without a known selected/last-active companion defer the pair to T19. A known active ID remains known even when the pet is temporarily away; return times are retained.

Existing empty saves keep their money/treats/settings. Only absence of both files triggers the new installation starter resource grant (20 coins/5 treats). The new-install decision is local to startup, not a reusable persisted grant flag. Null active with a nonempty collection does not mint a replacement starter.

## Backup and installer contract

Normal migration never overwrites v1, a previous backup, or original lookup bytes. It retains:
- sibling legacy-v1/<SHA256-of-exact-v1-bytes>/state.json;
- same directory legacy-v1-lookup.json containing exact original lookup bytes;
- manifest.json with SchemaVersion=1, StateSha256, LookupSha256 and MigrationId.

An existing backup must match byte-for-byte; it cannot silently be replaced. Existing v2 startup does not read the original user cache or the machine-wide preserved migration lookup. It reads v2 only, plus the immutable bundled lookup required by interim legacy compatibility code.

T20 must preserve a usable original installation lookup BEFORE MSI removes/replaces Data/catalog.json. App is prepared to read an explicit preserved file at CommonApplicationData/PokeCorner/legacy-v1-lookup.json. T02 writes no CommonAppData/system settings and changes no WiX/installer authoring. Startup alone cannot recover a custom old lookup already destroyed by an earlier installer; this remains a release blocker owned by T20.

## RED / GREEN evidence

All shell entries used rtk; builds/tests/git ran in the authorized native worktree with require_escalated. Native Git Bash was C:/Program Files/Git/bin/bash.exe, not WSL.

RED before barrier implementation:
- task-2-red-tests.log: actual old writer regression 1 failed / 0 passed; expected coins 12, actual 7. The initial shell wrapper incorrectly reported exit 0 because PowerShell expanded $?; raw dotnet output proves the test failure. Subsequent commands use correctly quoted Bash scripts and preserve their real exit code.
- task-2-contract-red-tests.log: exact new API fixture compilation failed CS0246 (LegacyLookup absent), exit 1, before migration/barrier implementation.
- task-2-display-red-tests.log: actual divergent migrated branch failed, expected 135 / actual 134, 1 failed, exit 1.
- task-2-selector-red-tests.log: actual VM selector failed, expected reached [133,135] / actual installed [133,134], 1 failed, exit 1, before the caller fix.

GREEN:
- rtk proxy dotnet test tests/PokeCorner.App.Tests --no-restore --filter FullyQualifiedName~MigrationTests --verbosity minimal
- task-2-focused-tests.log: 54 passed / 0 failed / 0 skipped; exit 0.
- rtk proxy dotnet test PokeCorner.sln --no-restore --verbosity minimal
- task-2-full-tests.log: Core 223/223 + App 327/327 = 550/550; 0 failed / 0 skipped; exit 0. Baseline was 496 total; 54 migration fixtures added.
- Full suite was rerun only after a final self-review correction isolating existing-v2 startup from original cache reads; final log covers the committed C#.
- task-2-self-review.log: git diff --check exit 0; every one of the five frozen legacy-v1 manifest files retains exact byte count and SHA.
- A controlled-runner test originally held its final exit write indefinitely; it was corrected to release subsequent writes. Its orphaned task-specific testhost PID 13488 was stopped after verifying the worktree command line. This did not involve the real application/save.

## Fixture coverage

Covered by the 54 synthetic cases plus existing full suite:
- Old empty and 100-individual collection; every old GUID remains; proof-based retro thresholds/coin delta; unknown catch histories do not become awards.
- Immutable chosen Eevee path versus different installed branch; duplicate/ambiguous lookup; missing ChainId; actual achieved stage with cosmetic rollback; source input byte/object equality.
- Unsupported existing shiny through the actual Migrate path (meaningful replacement for T01's deferred invariant); no flag downgrade.
- Invalid/corrupt JSON; future schema; null Individuals/Settings/CareStates/PendingEggs and list/care null elements; missing/empty/duplicate GUID; missing reserved gift GUID.
- Negative/overflow counters and residues; invalid stage/override; invalid v2 trait/species/family/XP and missing journals; invalid rational residues.
- All settings preserved, default/zero effects and zero cry volume; known and missing neglect checkpoint with old interaction/spam timestamps; absent monitor/offscreen position through an actual WPF OverlayWindow placement call.
- Nonempty no-active; all temporarily unavailable; active charm retained without extra item; old purchased eggs removed and no hatchling removed; pending earned encounters/residues retained.
- Pre-atomic write refusal leaves original v1 and immutable backup intact; retry succeeds; existing committed v2/relaunch retains same gift properties/award ledger and does not mint them again; corrupt existing v2 with valid v1 never imports.
- Valid backup selection preserves rejected file and rejects wrong schema; journal recovery never resets missing values silently.
- Stale Post/Flush while commit awaits, queued activity and mutation replay, failed commit retaining live/disk values, serial commits and exit ordering.
- Actual WpfTestHost STA dispatcher remains available during commit/exit await; no blocking UI continuation.
- Sole authoritative treats balance and validated alias; actual VM does not reveal/offer a different or unreached branch.

## Remaining boundaries / limitations

- Real gift-open and achievement-claim gameplay services belong to T19/T16. Restart fixtures prove persisted reservation/status/result IDs and retro award journals survive and are not regenerated; they do not claim completion of future UI/service operations.
- T05–T19 must use App.CommitStateAsync/AppState.CommitAsync for their resource operations and snapshots. Existing synchronous gameplay remains an interim compatibility path; this commit does not make the whole global-update gameplay release-ready.
- Full canonical collection names/types/rarity/layout belong to T14/T18. Missing old lookup names safely fall back to Pokemon #ID; an unreachable different branch is never substituted.
- Existing mathematical 150%/200% positioning tests passed in the full suite, and a synthetic WPF missing-monitor placement check passed. Manual compact-window/physical 150–200% DPI acceptance, recovery-dialog interaction and MSI old→new upgrade were not performed or claimed.
- No real user save/cache was opened, copied, altered or deleted by this task; no MSI installed; no Windows settings changed. All persistence fixtures use unique temporary directories.
- Controller owns board/ledger/public design docs; these were not modified. Independent review remains required before the controller marks T02 done.
