# Task2B fix1 addendum — root resolution of trait semantics

Initial independent review records I1–I4, unchanged. Root resolved its cross-feature trait-effect verification boundary with focused source checks, finding I5 before fixround1 completed.

## I5 — Important: fast-grower effect label has no canonical effect

CollectionView.xaml.cs TraitEffect says fast-grower speeds evolution. EvolutionService.cs:53 uses EvolutionCalculator.RequiredCareForNext(stage) without the trait. Core fast-grower references were only eligibility/assignment (TraitRules, SaveMigration); LevelProgression only XP and tier. Existing code has no reduction applied in canonical Preview. CARE-EVOLUTION.md:29/37, TRAITS.md and final/balance.json explicitly require care-requirement reduction, unchanged berry price/cumulative stored care. This is an inherited gameplay omission exposed by the new exact-card promise, not a new design.

Ruling: fix minimal accepted fast-grower application inside the canonical requirement/Preview and consume it in exact-card and Corner care progress during2Bfix1. The global gameplay spec is binding over the task boundary forbidding economic rewrites; this narrowly restores agreed behavior without changing balance or adding an engine. Wrong ruling costs bounded local logic rework; leaving it would knowingly ship a false trait effect and inconsistent evolution boundary.

Ownership extended only to required shared care helper/Preview, related tests and RoomView.xaml.cs care lines190–195 plus approvedRoomAlbumTests. Room composition/art/sprites/album untouched. Use shared accepted tier/value and unique-slot/stack policy, no copied magic coefficients or catalog reload per1s refresh. Preserve cumulative stored care, berry cost, all busy/asset/branch/commit safeguards, standalone/final behavior, and care-specific words. No gameplay percentages displayed.

Meaningful RED→GREEN: no trait vs first/second-slot fast-grower at accepted tiers, exact threshold boundary, unchanged berries and cumulative care, terminal/unsupported safeguards and matching exact-card/Corner progress. Existing accepted coefficients multiply base thresholds to whole integers; no rounding ambiguity identified by implementer. One final Core gate justified by canonical gameplay source change, covering App/migration/refusal/lifecycle tests only unless a concrete new concern warrants more. No broad other-trait audit.
