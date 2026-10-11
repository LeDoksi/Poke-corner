# Remaining contextual furniture

Six missing furniture states: T3,T4,A3,A4,C2,C4. Native room480x193; display3=1440x579. Built-in image_gen only for art. Python only crops, masks, nearest sampling and composition; implementation untouched.

`manifest.json` contains complete produce/direct/semantic handoff, exact source-prompt paths, provenance, logical dimensions, zoneRect and anchors. `proof.json` records pixel bounds, table protection, trophy fitting and original-pet checks. `QA.md` records direct visual review. `previews/` contains native and display3 scenes, six-state contact sheet, original-pet previews and cabinet trophies.

Table zoneRect124,107,184,168: T3 topPlantAnchor151,120 and floorAnchor151,150; T4 topPlantAnchor151,116 and floorAnchor151,155. Tops are empty. Chair zoneRect0,72,225,193, shared floorAnchor85,178; actual furniture contact approximately85,163. Both chair masks hard-exclude original table/plant rect128,92,187,168.

Cabinet zoneRect195,65,345,142, both models exact cabinetBounds198,72,325,136; shelf anchors226,106 /262,106 /300,106. `cabinet-c2-frontlip.png` and `cabinet-c4-frontlip.png` share their cabinet zone origin; intrinsic opaque lip is207,106,317,108. Render cabinet, three independent trophies, frontlip, independent top plant, unchanged original BW pet. TopPlantAnchor307,73; maximum20x18 logical pixels.

Sources and technical masks live in_sources; final consumable files only in assets. Reproduce source normalization with prepare.py and patches/proofs with assemble.py. No new studio atlas or independent furniture illustration was used.
