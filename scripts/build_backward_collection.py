"""Generate an unselected backward-growth survey, with reproducible seeds and gallery assets."""

import argparse
import csv
import json
from pathlib import Path
import secrets
import subprocess

from PIL import Image, ImageDraw, ImageFont

ROOT = Path(__file__).resolve().parents[1]
DLL = ROOT / "bin/Release/net10.0/coil-levels-csharp.dll"


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("destination", type=Path)
    parser.add_argument("--plan", type=Path, help="Reuse the sizes and full seed strings in a previous plan.json")
    args = parser.parse_args()
    destination = args.destination.resolve()
    plan = json.loads(args.plan.read_text()) if args.plan else {
        "generator": "backward-growth-v1",
        "sampler": "uniform-legal-cell-v1",
        "sizes": [2, 4, 8, 16, 32, 64, 128, 256, 512, 1000],
        "seeds": [secrets.token_hex(64) for _ in range(3)],
        "selection": "All runs retained. The same three 512-bit seeds are reused at each size.",
    }
    destination.mkdir(parents=True, exist_ok=False)
    (destination / "plan.json").write_text(json.dumps(plan, indent=2) + "\n")
    records = []
    for side in plan["sizes"]:
        for index, seed in enumerate(plan["seeds"], 1):
            name = f"{side}-backward-v1-s{index}"
            directory = destination / name
            subprocess.run(["dotnet", str(DLL), "backward-specimen", str(directory), str(side), "--seed-hex", seed], check=True)
            data = json.loads((directory / "stats.json").read_text())
            stats = data["stats"]
            bounds = data["occupiedBounds"]
            records.append({
                "id": name, "generator": data["generator"], "sampler": data["sampler"],
                "side": side, "seedIndex": index, "seedHex": seed,
                "openCells": stats["openCells"], "openFraction": stats["openFraction"],
                "targetOpenCells": data["targetOpenCells"], "stopReason": data["stopReason"],
                "occupiedWidth": bounds["width"], "occupiedHeight": bounds["height"],
                "solutionSlides": data["solutionSlides"], "legalExtensionCount": len(data["legalExtensions"]),
                "boardSha256": data["boardSha256"], "stats": f"{name}/stats.json",
                "map": f"{name}/map.png", "preview": f"{name}/preview.png",
                "detail": f"{name}/detail.png", "solutionDetail": f"{name}/solution-detail.png",
                "solutionMap": f"{name}/solution-map.png",
                "recipe": f"{name}/recipe.json", "board": f"{name}/level.board.gz",
                "constructionCode": f"{name}/construction.code.json", "constructionFormat": "coil-construction-v1",
                "solution": f"{name}/level.solution.gz",
            })
    (destination / "manifest.json").write_text(json.dumps(records, indent=2) + "\n")
    with (destination / "summary.csv").open("w", newline="") as output:
        writer = csv.DictWriter(output, fieldnames=list(records[0]), lineterminator='\n')
        writer.writeheader()
        writer.writerows(records)
    contact_sheet(destination, records, len(plan["seeds"]))
    contact_sheet(destination, [row for row in records if row["side"] == max(plan["sizes"])],
                  len(plan["seeds"]), "largest-comparison.png")
    rows = ["| Side | Sample | Open cells | Board coverage | Occupied bounds | Slides | Stop |",
            "|---:|---:|---:|---:|---|---:|---|"]
    for row in records:
        rows.append(f"| {row['side']} | [{row['seedIndex']}]({row['detail']}) | {row['openCells']} | "
                    f"{100 * row['openFraction']:.4f}% | {row['occupiedWidth']} x {row['occupiedHeight']} | "
                    f"{row['solutionSlides']} | {row['stopReason']} |")
    trapped = sum(row["stopReason"] == "trapped" for row in records)
    (destination / "README.md").write_text(
        "# Backward-growth collection v1\n\n"
        f"{len(records)} unselected runs: {len(plan['seeds'])} saved seeds at {len(plan['sizes'])} square sizes, "
        f"{min(plan['sizes'])} through {max(plan['sizes'])}. "
        "Generator: `backward-growth-v1`; sampler: `uniform-legal-cell-v1`. "
        "Every board and recipe is validated against Mortal Coil physics.\n\n"
        f"**{trapped} of {len(records)} runs trapped before their selected target size.** "
        "Completeness guarantees that every valid construction is possible; it does not make "
        "large, dense boards likely. The table distinguishes board dimensions from occupied extent. "
        "Large black previews are not missing images: walls are black and open cells are white.\n\n"
        "![Full board and occupied-region crop pairs](contact-sheet.png)\n\n"
        + "\n".join(rows) + "\n\n"
        "## Assets for gallery integration\n\n"
        "`manifest.json` has one entry per board with relative asset links and generator labels. "
        "`summary.csv` contains the same flat records. Per-board `stats.json` embeds the existing "
        "`BoardCharacterization` geometry schema, plus target size, stopping reason, occupied bounds, "
        "solution slide count, endpoint coordinates, seed, random-stream version, and validation.\n\n"
        "`map.png` is the full board, exactly one pixel per cell. `preview.png` is a 480-square "
        "whole-board overview. `detail.png` is a visibly labeled crop around the occupied region; "
        "do not display it as a full board. `solution-map.png` and `solution-detail.png` color forward "
        "visitation order from blue start to orange finish. `recipe.json` records every prepend. "
        "The gzip board and solution use the existing interchange format.\n\n"
        "Do not silently mix this unfiltered generator with the earlier wander/tweak generator "
        "or apply density/quality filtering to this completeness survey. No runs were discarded or retried.\n\n"
        "## Reproduce and verify\n\n"
        "Build Release, then run `python3 scripts/build_backward_collection.py NEW-DIRECTORY "
        "--plan gallery/backward-v1/plan.json`. The script requires Pillow. "
        "Run `dotnet run -c Release -- verify-collection gallery/backward-v1` to check saved maps, "
        "hashes, geometry, and physics. Replay any recipe with `gen-backward --recipe FILE --out NEW-STEM`. "
        "Generation times may differ; board hashes and recipes are reproducible.\n"
    )


def contact_sheet(destination: Path, records: list[dict], columns: int, filename: str = "contact-sheet.png") -> None:
    card_width, card_height = 440, 244
    sheet = Image.new("RGB", (columns * card_width, ((len(records) + columns - 1) // columns) * card_height), "#10231d")
    draw = ImageDraw.Draw(sheet)
    font = ImageFont.truetype("DejaVuSans.ttf", 15)
    small = ImageFont.truetype("DejaVuSans.ttf", 12)
    for index, row in enumerate(records):
        x, y = index % columns * card_width, index // columns * card_height
        draw.text((x + 10, y + 7), f"{row['side']} x {row['side']} / seed {row['seedIndex']} / {row['openCells']} open", font=font, fill="white")
        with Image.open(destination / row["map"]) as raw:
            full = raw.convert("RGB").resize((180, 180), Image.Resampling.NEAREST if row["side"] <= 180 else Image.Resampling.BOX)
            sheet.paste(full, (x + 10, y + 32))
            metadata = json.loads((destination / row["stats"]).read_text())
            bounds = metadata["occupiedBounds"]
            crop = raw.crop((bounds["x"], bounds["y"], bounds["x"] + bounds["width"], bounds["y"] + bounds["height"])).convert("RGB")
            crop.thumbnail((180, 180), Image.Resampling.NEAREST)
            scale = min(180 / crop.width, 180 / crop.height)
            crop = crop.resize((max(1, int(crop.width * scale)), max(1, int(crop.height * scale))), Image.Resampling.NEAREST)
            sheet.paste(crop, (x + 235 + (180 - crop.width) // 2, y + 32 + (180 - crop.height) // 2))
        draw.text((x + 10, y + 220), f"FULL / {100 * row['openFraction']:.4f}%", font=small, fill="#bce4d1")
        draw.text((x + 235, y + 220), f"CROP / {row['occupiedWidth']} x {row['occupiedHeight']}", font=small, fill="#bce4d1")
    sheet.save(destination / filename)


if __name__ == "__main__":
    main()
