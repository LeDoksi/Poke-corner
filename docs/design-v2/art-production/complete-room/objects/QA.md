# Object normalization QA

95 native-grid PNG assets: 20 plant variants,12 paintings,63 trophy appearances. No artwork generation or RGB repainting. Source hashes, original generation prompts and derivation metadata are embedded in every asset and recorded in manifest.json.

Visual comparison completed against accepted pilot master, next-zones/state-6.png and original plant/painting/trophy contacts. Reviewed all normalized contacts, every plant in its eligible slots, all paintings in four room examples, and all63 trophies in21 actual shelf compositions for each C1/C2/C3/C4 cabinet. The resulting84 full-room previews use original BW Eevee frame0 without changing opaque pet pixels.

One visual correction: removed confirmed tabletop residue from contextual terracotta succulent by changing only bounded alpha pixels. Contextual ceramic pilea was not assigned to woven-cream inventory variant because its pot differs; original woven source retained. Floor widths respect asymmetric source foot anchors and stay on-canvas near [23,172], to the left of the chair.

Automated checks verify95unique inventory IDs/files, decoded pixel identity, transparent nonempty alpha, embedded original prompts and SHA provenance, source-selected visible RGB/alpha, all63 trophy RGBA source samples, and756 trophy placements (63×3slots×4cabinet geometries). Plants never use floor slots when small or furniture slots when floor-family. Paintings preserve source aspect inside a common40×34 canvas.

Native-grid limits: trophy sizes are14–22pixels wide. Object families, colors and main silhouettes remain visible; tiny book/map markings and crowded individual character faces are not readable. The12paintings retain their original scenes; map detail is decorative at40pixels wide. Keep full original inventory names in semantic selection controls. These limits are recorded rather than claiming illustration-level detail at room scale.

Window-sill plants belong behind the chair. Previews apply a bounded native chair silhouette occlusion; the combined renderer must own correct foreground masking/layer order. Pot/plant source PNGs themselves are uncut. Table anchor here is T2[151,116]; select per-table topPlantAnchor when composing T1/T3/T4.

No WPF/app integration or original67-frame pet animation test in this pass. Original source images, other agents' assets and application code remain unchanged.

Reproduce: `rtk proxy C:/Users/Georgy/.cache/codex-runtimes/codex-primary-runtime/dependencies/python/python.exe -Xutf8 docs/design-v2/art-production/complete-room/objects/normalize.py`

The script expects contextual C2/C4 cabinet zone/frontlip assets from complete-room/left-furniture and accepted C1/C3 from next-zones/cabinet.
