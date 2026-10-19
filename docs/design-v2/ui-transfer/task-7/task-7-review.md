### Spec Compliance

- ✅ Spec compliant for Task7. Reviewed the supplied package for f86dde2f55edc38aeb015a12793526ab7bc14637..24f342a26bab65dcc83a8adb5abe58b6ef00f0e5, including fe82d6f, a0572f9 and 24f342a. Read the complete changed production C#, project/resource mapping, Python helper and test diff. Generated fact/provenance/batch content was inspected in bounded semantic samples rather than claimed as an exhaustive independent translation review.
- ✅ Complete production coverage is supported by the default release coverage assertion and exact ID/source checks at docs/design-v2/catalog-package/prepare-russian-facts.py:78-117, the COMPLETE artifact at docs/design-v2/catalog-package/russian-facts-checks.json:2-16, and passing runtime coverage/package tests at tests/PokeCorner.App.Tests/GlobalUpdate/russianFactsTests.cs:17-46. The final focused raw log records 66/66 at .superpowers/sdd/2026-10-17-approved-ui-transfer/task-7-final-focused.log:85-88.
- ✅ Genuine source provenance and editorial Russian are separate: source CSV/approved bytes are pinned, English language9 tuples are compared to the supplied CSV, and UTF-8 raw-text SHA is checked at docs/design-v2/catalog-package/prepare-russian-facts.py:37-41 and :79-99. Runtime metadata explicitly labels Russian as editorial rather than official; normalized sourceText and exact sourceRawText occupy separate artifacts.
- ✅ The approved11 objects are copied without editorial replacement, protected by the approved artifact SHA and object equality at docs/design-v2/catalog-package/prepare-russian-facts.py:21 and :92-116. Read all11 original objects in src/PokeCorner.App/Assets/UiSystem/curated-russian-facts.json and compared their displayed values with the generated samples. For43/133/152, inspected raw CR/LF/form-feed/U+00AD and raw SHA in the provenance copy; normalization is separate and no false raw equality is asserted.
- ✅ Both fact and raw-provenance files ship through production RuntimePackage.props at src/PokeCorner.App/RuntimePackage.props:2751-2752 and the two manifest entries at docs/design-v2/final/runtime-package.json:16485-16506. The final proof artifact records equal source/App-bin/test-bin bytes and1025rows: .superpowers/sdd/2026-10-17-approved-ui-transfer/task-7-final-package-proof.json:1. These are existing retained proof results, not freshly recomputed checks.
- ✅ Offline loading is one Lazy file read with shared species-ID lookup, not per-card network: src/PokeCorner.App/Popup/CollectionPresentation.cs:18-20. Missing, unreadable, malformed, wrong-type, out-of-range, duplicate or non-Cyrillic row data returns empty facts without a catalog-summary fallback at :22-49.
- ✅ Discovered species entries use the prepared fact and mask names/facts when closed: src/PokeCorner.App/Popup/CollectionViewModel.cs:147-160. Focused unchanged-code risk check: searched only CollectionViewModel.cs and CollectionView.xaml.cs for fact callers/search/detail binding to verify that replacing the shared fact table cannot bypass selected-form masking or introduce a summary fallback. CollectionView.xaml.cs:377-378 gates name and fact on IsDiscovered; :871-872 hides missing individual facts; CollectionViewModel.cs:164-171 searches only already-masked names/route/number; :232 uses the same species fact for owned individuals.
- ✅ Tests exercise the actual WPF species and individual detail surfaces, selected ordinary/shiny forms and equal owned-form facts at tests/PokeCorner.App.Tests/GlobalUpdate/russianFactsTests.cs:49-80. The binding RED log demonstrated the old service-summary value before the one-line correction, rather than a mock-only failure: .superpowers/sdd/2026-10-17-approved-ui-transfer/task-7-binding-red.log:18-45 and :97-100.
- ✅ No save, acquisition, discovery metadata, gameplay service, Core implementation, screen redesign, network API or new dependency change appears in this task diff. The only old assertion changed is the obsolete missing1025 fact expectation; invalid0/1026 lookups remain asserted at tests/PokeCorner.App.Tests/GlobalUpdate/russianFactsTests.cs:24-25. Sampled lore numbers describe Pokémon observations, not UI economy/probability/evolution formulas.

### Strengths

- The offline preparation helper keeps selection, manual Russian input, exact provenance, approved-subset protection and package checks in one bounded tool. Its default path rejects incomplete coverage; --allow-partial is explicit. Duplicate IDs/text and stale manifest totals are checked rather than silently accepted: docs/design-v2/catalog-package/prepare-russian-facts.py:101-117 and :130-150.
- Runtime failure handling is small and atomic: one invalid row discards the table rather than exposing partially parsed malformed content; no generated fallback or save access exists in the loader: src/PokeCorner.App/Popup/CollectionPresentation.cs:22-49.
- Genuine behavioral RED evidence exists: initial coverage found1014missing and absent package entries, then the shared VM binding failed on the actual summary text. Final focused tests cover the reader, package, real WPF binding/masking and all nine malformed/missing inputs: .superpowers/sdd/2026-10-17-approved-ui-transfer/task-7-red.log:20-43 and :47-51; task-7-final-focused.log:17-88; tests/PokeCorner.App.Tests/GlobalUpdate/russianFactsTests.cs:81-103.
- Source overrides are reproducible, not undocumented edits to generated output: docs/design-v2/catalog-package/prepare-russian-facts.py:22 and :43-66. The bounded samples include known baseline/regional traps, Hoopa/Primal/sky-form risks, conditional transformations and uncertain magazine lore. Sampled Russian retains qualifications such as likely, according to legend, reported and possible.

### Issues

#### Critical (Must Fix)

None found in Task7.

#### Important (Should Fix)

None found in Task7. The full application gate remains red and is an integration/Task8 acceptance concern below; it is not represented as a successful whole-app gate.

#### Minor (Nice to Have)

1. Existing test-build warnings remain in the final focused validation output: .superpowers/sdd/2026-10-17-approved-ui-transfer/task-7-final-focused.log:5-6 reports CS8602 at tests/PokeCorner.App.Tests/GlobalUpdate/approvedCollectionTests.cs:283 and xUnit2013 at :255. They also appear in the initial RED log, and the Task7 diff does not modify those statements, so this is inherited noise rather than a new Task7 defect. Clean up the nullable dereference/assertion in the owning test-maintenance work so future validation output is pristine.

### Cannot verify / acceptance boundaries

- Independent editorial review here sampled141unique source/Russian pairs, across all21input batches, including all11approved facts and targeted baseline/regional/conditional-form risks. Exact sampled ID groups:
  - 1,4,7,16,25,26,27,28,37,38,43,52,58,59,77,78,83,110,124,128,133,152,155,156,157,194,211,215,222,263,264,383,439,460,491,492,503,532,549,554,555,562,568,570,571,618,628,678,689,705.
  - 706,713,718,720,724,741,745,747,748,774,795,797,798,800,801,808,818,849,875,888,889,892,901,902,905,916,920,931,953,964,981,990,991,992,993,994,995,999,1005,1006,1017,1018,1022,1024,1025.
  - 50,80,100,114,131,150,165,187,200,225,250,275,289,300,325,350,372,375,384,400,413,421,425,450,475,500,525,550,575,600,625,647,650,675,700,725,750,775,825,850,885,900,925,950,975,1000.
- All141pairs were read from generated data copies inside task-7-review-package.md; all21batch selfReview notes were also read. No material source/Russian mismatch was identified in that sample. This is not exhaustive semantic certification of1025translations, Russian catalog-name spellings or every baseline-form claim. Full editorial self-review remains the implementer evidence recorded in batch notes and bounded review logs. Structural/source tuple assertions cannot certify translation meaning.
- The .NET test named DataAndExactSourceProvenanceShipThroughManifest checks actual packaged bytes/hash, IDs and English language; it does not parse the source CSV or independently verify raw-text SHA/version identity: tests/PokeCorner.App.Tests/GlobalUpdate/russianFactsTests.cs:29-46. Exact CSV identity is instead supplied by the preparation assertions and retained final proof. No broader guarantee is attributed to that .NET test.
- Full App gate is688/690, two failures,40.5185s: .superpowers/sdd/2026-10-17-approved-ui-transfer/task-7-final-app-gate.log:759-763. The raw stacks identify AcceptanceVisualTests.CaptureAssembledApplicationMatrix, NullReferenceException at acceptanceVisualTests.cs:131, and ShellTests.FinishReviewEvidenceRendersPopulatedPokedexAndResetArea, Grid-to-ScrollViewer InvalidCastException at shellTests.cs:260; see raw log:253-282 and :461-489. Those fixture files are unchanged in Task7. The report attributes them to existing Task8 capture work; this review did not rederive their historical origin or claim the entire UI is accepted.
- Final source/App-bin/test-bin equality and all1025raw tuple results were read in the named final proof artifact and backed by reviewed preparation assertions; hashes/suites/builds were deliberately not rerun. The proof JSON records outcomes, not a new independent execution by this reviewer.
- No physical UI comparison was performed. Longest fact689 and other typography/layout acceptance remain in the existing Task8 visual gate, as recorded by task-7-final-package-proof.json. No real profile, app, installer, OS state, git state or production file was modified.
- Scope outside the diff was limited to the explicitly named masking/fallback caller check and original approved11 artifact check. No repository crawl, network lookup, child reviewer or broad test run was performed.

### Assessment

**Task quality: Approved**

**Issue counts:** Critical0; Important0; Minor1(inherited test warnings).

**Reasoning:** The scoped implementation supplies a complete offline species-keyed Russian dataset, separately preserves exact source provenance and approved examples, connects the existing package/UI path with fail-safe behavior, and has meaningful reader/binding/package tests. Approval is for Task7; complete application acceptance still needs the two retained capture failures and existing Task8 visual comparison resolved.
