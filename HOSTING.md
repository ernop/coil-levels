# Public board explorer

Live target: <https://ernop.github.io/coil-levels/>.
The root opens `web/gallery.html` and preserves query parameters and anchors.

The repository's GitHub Pages source is **GitHub Actions**. Pushing `master`
runs `.github/workflows/pages.yml`: check the viewer, stage the website, and
deploy it to the `github-pages` environment. The same workflow can be run
manually from the repository's Actions tab. No separate hosting service,
credentials in the repository, or application server is required.

The viewer contains all 4,864 catalog entries. Under **Notes & files**, **Board**
downloads the selected level; **Legal solution** is available for the 732 boards
with saved solutions. Files ending in `.gz` need decompression before use in a
tool that expects plain `.board` or `.solution` text. The repository also contains
the historical `.coil` files under `levels/`.

The full gallery exceeds GitHub Pages' 1 GB site limit. `scripts/build_pages.py`
stages tracked website and gallery files, omitting the unused `catalog.js`
duplicate and the full-resolution map PNGs. It changes map download links in the
staged data to GitHub's raw files at the exact deployed commit. Every board,
thumbnail, exact cell record, board download, saved solution, and measurement
remains available. The source gallery is unchanged and still opens offline.
The build verifies all indexed assets and rejects a bundle of 1 GB or larger.

To inspect a local bundle, first stage new source files with Git, then run:

```sh
python3 scripts/build_pages.py /tmp/coil-pages-preview
python3 -m http.server 8764 --bind 127.0.0.1 --directory /tmp/coil-pages-preview
```

The output directory must be new. Map download links resolve once that source
commit has been pushed. Check the workflow's deployment result and the live
viewer after publishing.
