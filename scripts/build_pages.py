"""Stage the static explorer, keeping full-resolution PNG downloads on GitHub."""

import argparse
import json
from pathlib import Path
import re
import shutil
import subprocess

ROOT = Path(__file__).resolve().parents[1]
REPOSITORY = "ernop/coil-levels"
PAGES_LIMIT = 1_000_000_000


def build(destination: Path) -> None:
    destination = destination.resolve()
    # Requiring a new directory prevents stale assets from inflating the site.
    destination.mkdir(parents=True, exist_ok=False)
    revision = subprocess.check_output(
        ["git", "rev-parse", "HEAD"], cwd=ROOT, text=True
    ).strip()
    raw_base = f"https://raw.githubusercontent.com/{REPOSITORY}/{revision}/"
    catalog = json.loads((ROOT / "gallery/catalog.json").read_text())
    map_urls = {board["assets"]["map"] for board in catalog["boards"]}
    maps = {url.removeprefix("../") for url in map_urls}
    tracked = subprocess.check_output(
        ["git", "ls-files", "-z"], cwd=ROOT, text=True
    ).split("\0")
    copied = set()
    total = 0

    def map_link(match: re.Match) -> str:
        url = match[2]
        if url not in map_urls:
            raise ValueError(f"Uncatalogued full-resolution map: {url}")
        return match[1] + raw_base + url.removeprefix("../") + match[3]

    for name in tracked:
        path = Path(name)
        if not (name.startswith(("web/", "gallery/")) or
                name == "index.html" or
                (len(path.parts) == 1 and path.suffix == ".md") or
                name == "meta_solver/README.md"):
            continue
        # The explorer draws from exact cells. Full map PNGs are download-only;
        # catalog.js is an unused duplicate of the downloadable catalog.json.
        if name in maps or name == "gallery/catalog.js":
            continue
        source = ROOT / name
        if source.is_symlink():
            raise ValueError(f"Pages cannot package symbolic links: {name}")
        target = destination / name
        target.parent.mkdir(parents=True, exist_ok=True)
        if name.startswith("gallery/") and path.suffix in (".js", ".json"):
            content = re.sub(r'("map"\s*:\s*")(\.\./gallery/[^"]+)(")',
                             map_link, source.read_text())
            target.write_text(content)
        else:
            shutil.copyfile(source, target)
        total += target.stat().st_size
        copied.add(name)

    index_source = (destination / "gallery/stats-lab-data.js").read_text()
    index = json.loads(index_source.partition("=")[2].strip().removesuffix(";"))
    for board in index["boards"]:
        assert board["recordScript"].removeprefix("../") in copied, board["id"]
        for key, url in board["assets"].items():
            if key == "map":
                assert url.startswith(raw_base), url
                assert (ROOT / url.removeprefix(raw_base)).is_file(), url
            else:
                assert url.removeprefix("../") in copied, url
    assert {"index.html", "web/home-redirect.js", "web/gallery.html"} <= copied
    if total >= PAGES_LIMIT:
        raise ValueError(f"Pages bundle is {total:,} bytes; limit is {PAGES_LIMIT:,}")
    (destination / ".nojekyll").touch()
    print(f"Staged {len(index['boards']):,} boards in {len(copied):,} files "
          f"({total / 1_000_000:.1f} MB); all viewer assets verified.")


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("destination", type=Path, help="New output directory")
    build(parser.parse_args().destination)
