# Corrected trophy source pack — 2026-10-11

User-approved follow-up to `final/TROPHY-SEMANTICS.md`:20new drawings replace four stages each of species/evolution/team/nursery and four standalone goals (type-team, evolved-expedition, all-berries, berry-evolution). The4population stages are reused from the original63-image bank; they are not new drawings here. All active IDs/conditions/thresholds remain unchanged.

`sources/` contains five builtin imagegen atlases with actual transparency. `jobs.json` records the exact accepted prompts, four canonical IDs per atlas and reviewed gutter cuts; the initial unsuccessful requests produced no accepted art. `prepare.py` only crops, uniformly fits, adds metadata and composes a review contact sheet. It does not repaint or create artistic strokes. Each output fits132×94; the physical foot anchor is measured from alpha bounds. Embedded metadata preserves prompts and source/crop provenance. The source bank remains untouched.

`review.png` shows20new normalized images. Evolution uses distinct abstract growth-stage medallions and arrows; nursery uses eggs, hatching/paw emblem and an open cradle rather than tiny babies or greenhouses. These keep the approved meaning readable at the small shelf scale. Team rank ornaments are decorative stages, not an additional numeric game parameter.

Run `prepare.py`, then sibling `final-assets/export.py`, then `docs/design-v2/final/build.py` and `last-attempt/assemble.py`. The canonical export includes20revised plus36original award sources,56exact-alpha white masks and168placement checks. No WPF or user-save data is changed.
