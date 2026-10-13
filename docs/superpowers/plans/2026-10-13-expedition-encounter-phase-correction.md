# T08 cross-stage correction: expedition reward claim versus catch

Worktree: `C:\Users\Georgy\.codex\worktrees\global-update\PokeTokenBar-Windows`.
Read the existing task-8 brief/report, `docs/design-v2/EXPEDITIONS.md:121`, `docs/design-v2/final/SCREENS.md`, and `docs/design-v2/SHINY.md` before editing.

The approved expedition contract says claim adds a new encounter to the encounter list; it must not automatically catch it or present it as an already caught pet. Claim determines and commits its actual species/shiny and any eligible charm consumption. The user opens the encounter later to receive the individual.

Confirmed gap in accepted code: `ExpeditionService.Claim` invokes `EncounterService.Open`; Open adds the individual and discovery/shiny journals, sets phase opened. Thus rewards currently grant the pet immediately instead of retaining a closed, resolved encounter.

One meaningful correction batch, before the new Pokedex/metrics/screens depend on this behavior:

- Separate resolution from the final grant in the existing EncounterService. At claim, persist stable encounter ID/source/species/variant/nature/traits and correct charm consumption, while keeping it closed and out of the collection. Reuse existing deterministic draw and shared shiny rules; do not create another store or duplicate roll engine.
- Opening that saved encounter later grants exactly once, uses its frozen result without another draw or charm spend, then updates collection/discovery/shiny journals. Activity guarantee and nursery luck remain unchanged by this source. Preserve current activity/gift semantics.
- Adapt the existing expedition action/replay result and validation to distinguish a saved closed encounter from a caught individual. Expose only the permitted encounter metadata to future T09/T18 callers. No cosmetic UI or full screen implementation.
- Preserve all already granted/opened encounters and existing individual GUID/shiny/progression. Do not retract historical grants or refund/duplicate their charm consumption.
- Real behavioral RED before production edits: claim with a guaranteed encounter leaves collection count unchanged and retains a closed result; restart/failed write/repeated claim keeps the same frozen result; later open adds exactly one individual; a newly activated charm after claim stays active when opening the already resolved encounter; unsupported shiny preserves the claim-time charm. Existing opened legacy records still replay safely.
- Run focused covering tests, then the appropriate shared Core/App gate. Only the known capture-env test may be excluded. Logs preserve actual COMMAND and ExitCode, no late rollback of production save guards for RED.

Own only necessary ExpeditionService/EncounterService/EncounterState, save validation and regression tests plus required App result adaptation. Preserve the completed T11 trait/busy fixes and registered queued-commit shutdown barrier. No subagents or real saves/MSI/OS changes. Every command `rtk proxy` and worktree writes/builds need per-command `require_escalated`; Git metadata are valid.

Append correction report/API handoff to task-8-report.md, commit explicit source/tests only, run diff check. Scratch stays ignored. Controller records/reviews this as a reopened T08 contract correction, not a repeat of the whole accepted implementation.
