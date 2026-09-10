# Original and historical boards

The [Board explorer](../web/gallery.html) distinguishes two sources:

- **Original game**: all 1,208 numbered layouts (1–1208) from the Mortal Coil author's [published coilbench data archive](https://github.com/adum/coilbench/releases/tag/data-v1.0). The [game](https://www.hacker.org/coil/) links to this benchmark. These files supply layouts, not generator settings or solution certificates.
- **coil-levels generator**: this project's study collections, all distinct layouts in the tracked historical `.coil` files, and the twelve saved solver-selected boards.

The 3,306 tracked `.coil` files contain 5,292 board occurrences and 2,924 distinct layouts. Identical historical layouts have one gallery card. Every occurrence remains recorded with its source filename, line, heading, and recorded seed. This avoids filling the browser with copies while retaining every original layout and its provenance. Experimental draws remain separate because repetition is part of the sampling record.

Imports retain rectangular dimensions exactly. No board is regenerated, padded into a square, trimmed, or altered. Geometry is computed from the complete original layout. All supplied solutions are independently replayed. Historical layouts without certificates remain viewable and say **No saved solution**; their solvability has not been independently verified by this import. The original game's unpublished creation settings are not inferred from its appearance.

`imported-boards.json` records archive provenance and coverage. Each imported board has `import.json`, exact compressed board text, map and preview images, and full geometry in `stats.json`. Supplied solutions are retained where available. The archive's URL, SHA-256, and member name are included in every original-game record. The original `.coil` files remain unchanged.

To rebuild:

```sh
curl -fL https://github.com/adum/coilbench/releases/download/data-v1.0/boards.zip -o /tmp/coil-original-boards.zip
python3 scripts/import_original_boards.py /tmp/coil-original-boards.zip
dotnet build -c Release
dotnet run -c Release --no-build -- prepare-archive-gallery
python3 scripts/build_catalog.py --require-complete
python3 scripts/build_stats_lab.py
python3 scripts/check_archives.py /tmp/coil-original-boards.zip
node scripts/check_gallery.js
node scripts/check_stats_lab.js
```
