# Small cabinet module batch

Two contextual cabinet states for the accepted shared pilot, logical scene 480×193, integer display scale 3. No application implementation changed.

- C1: honey-oak, one empty open display niche, two lower doors.
- C3: warm walnut, one empty open display niche, lower books between two doors.

Each model ships as `assets/c1-zone.png` / `assets/c3-zone.png`, local 150×77 alpha patches placed at scene (195,65). These are bounded contextual replacement patches containing intrinsic surrounding light/shadow, not isolated movable cabinet cutouts. Always compose with the SAME `../../pilot-zones/master.png`. Each separate `*-frontlip.png` sits at the same origin, above trophy layers.

`profile.json` defines exact three shelf anchors, source-derived shelf lip, top plant anchor and reserved painting region. `manifest.json` groups produced, reused and composition responsibilities. The three representative trophies are existing artwork resized technically to native logical dimensions: population-1, expeditions-1, care-1. Their source art was not regenerated or repainted.

`previews/*-empty-display3.png` shows each empty shelf; `previews/*-full-display3.png` shows three independent trophy assets and one unchanged real BW Eevee frame. Corresponding `*-logical.png` previews omit the pet for common parent composition. Empty cabinet patches do not contain trophies, plants, paintings or pets.

Rebuild and verify with:

```powershell
rtk C:/Users/Georgy/.cache/codex-runtimes/codex-primary-runtime/dependencies/python/python.exe -X utf8 docs/design-v2/art-production/next-zones/cabinet/assemble.py
```

`proof.json` records 378 actual opaque pixel-bound checks (63 trophies × 3 slots × 2 cabinet states), preservation outside the bounded zone, and preservation of the opaque BW pet pixels. These checks do not approve all 63 trophy compositions visually. All showcase and catalog art remains subject to parent common-scene review.

Exact prompts are in `_sources/*-prompt.txt`, embedded into PNG metadata together with origins. `source-metadata.json` retains the built-in generated paths and native dimensions. One C1 corrective generation removed an accidental second shelf; C3 needed no corrective generation. Python performed crop/mask/nearest resize/composition only. No new room was generated independently.
