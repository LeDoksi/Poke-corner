# Fixed-room implementation pack

Consume assets/room-master.png, assets/shelf-frontlip.png,56canonical trophy PNGs and56white opacity masks. manifest.json records canonical id,chain/special category,tier,sourceId,source SHA and originalRGBAhash,logicalSize,footAnchor and destinations.56active appearances =10chains×4tiers+16specials; album contains26cards (10first-tier chains+16specials).

Population1–4keeps its canonical IDs and uses the original growing Eevee companion group again; no decor-house mapping.20other miniatures have user-approved revised sources in ../trophy-revision/. The original63-source bank stays unchanged. Decor1–4 and special-all-plants/three-pictures/mixed-furniture are absent from the active export; original files are preserved.

Room coordinates1448×582. Place each award at shelf anchors[664,280],[810,280],[956,280] by normalized footAnchor, then draw intrinsic frontlip at[590,281] from crop[590,281,1031,288]. Root owns pet and UI. Full decor customization is retired; this is one coherent fixed room plus award swaps.

Masks contain white RGB255and exact source alpha. Use them as opacity brushes for UI-owned dark locked silhouettes. Locked chain cards show only first-tier silhouette; future versions are not revealed. previews/album-locked.png and album-open.png contain26cards; late-tiers-review-only.png is a review artifact and must not feed locked UI.

PNG re-encoding adds readable embedded original prompts and canonical/source provenance, with exact decodedRGBA identity checks.56×3slots=168bounds checked, plus exact room/lip pixels,56mask alpha identities, all metadata reads and absence of7retired IDs. proof.json records evidence. Preview examples include empty,one-companion-group,first-tier andlast-tier rooms; no pet added.

The20new source drawings were generated with builtin imagegen; cropping/fitting was technical only. No application changes or WPF integration. Dense trophy groups may still crowd tiny faces; export checks do not substitute for final user visual acceptance.

Visual inspection completed on room-first-tiers.png,album-open.png and album-locked.png. Population card shows the growing companion group; companions/mountain/heart are visible on the physical shelf. Locked first-tier/special silhouettes use the same exact-alpha masks with dark fill. No future chain-tier artwork appears in the locked album.

Reproduce: `rtk proxy C:/Users/Georgy/.cache/codex-runtimes/codex-primary-runtime/dependencies/python/python.exe -Xutf8 docs/design-v2/art-production/last-attempt/final-assets/export.py`
