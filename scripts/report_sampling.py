"""Build gallery manifests and figures from saved sampling experiments (Pillow only)."""
import csv
import json
import math
from pathlib import Path
from collections import Counter
import sys

from PIL import Image, ImageDraw, ImageFont

ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(ROOT))
from analysis.check_backward_generation import DIRECTIONS

STUDY = ROOT / "gallery/sampling-v1"
COLLECTION = ROOT / "gallery/reversible-v1"
FONT = ImageFont.truetype("DejaVuSans.ttf", 16)
SMALL = ImageFont.truetype("DejaVuSans.ttf", 12)
TITLE = ImageFont.truetype("DejaVuSans.ttf", 22)
COLORS = ["#1670ad", "#e56b29", "#3e8b48", "#a03b85", "#7563aa", "#8b6b26"]


def line_chart(path: Path, title: str, series: list[tuple], xlabel: str, ylabel: str,
               logarithmic_y: bool = False, logarithmic_x: bool = False) -> None:
    image = Image.new("RGB", (920, 600), "white")
    draw = ImageDraw.Draw(image)
    draw.text((30, 18), title, font=TITLE, fill="#172b35")
    left, top, right, bottom = 85, 70, 885, 450
    tx = (lambda x: math.log2(max(1, x))) if logarithmic_x else (lambda x: x)
    ty = (lambda y: math.log10(max(1e-12, y))) if logarithmic_y else (lambda y: y)
    xmax = max(tx(x) for _, points in series for x, _ in points)
    ymin = -12 if logarithmic_y else 0
    ymax = 0 if logarithmic_y else max(y for _, points in series for _, y in points) * 1.08
    def point(x: float, y: float) -> tuple[float, float]:
        return left + tx(x) / max(1, xmax) * (right - left), bottom - (ty(y) - ymin) / (ymax - ymin) * (bottom - top)
    for i in range(5):
        y = ymin + i * (ymax - ymin) / 4
        screen = bottom - i * (bottom - top) / 4
        draw.line((left, screen, right, screen), fill="#dce2e5")
        label = f"1e{y:.0f}" if logarithmic_y else f"{y:.2f}"
        draw.text((8, screen - 7), label, font=SMALL, fill="#243b48")
    for i in range(5):
        x = xmax * i / 4
        screen = left + i * (right - left) / 4
        draw.text((screen - 14, bottom + 8), f"{2**x:.0f}" if logarithmic_x else f"{x:.0f}", font=SMALL, fill="#243b48")
    draw.text((left, bottom + 32), xlabel, font=FONT, fill="#243b48")
    draw.text((left, top - 22), ylabel, font=SMALL, fill="#243b48")
    for index, (label, points) in enumerate(series):
        color = COLORS[index % len(COLORS)]
        xy = [point(x, y) for x, y in points]
        if len(xy) > 1: draw.line(xy, fill=color, width=3)
        for x, y in xy: draw.ellipse((x - 2, y - 2, x + 2, y + 2), fill=color)
        column, row = index % 2, index // 2
        draw.text((35 + column * 440, 515 + row * 23), label, font=SMALL, fill=color)
    image.save(path)


def baseline() -> dict:
    width = height = 4
    area = 16
    histogram = Counter()
    def visit(path: tuple, opened: set, mass: float) -> None:
        sx, sy = path[0]
        children = []
        for dx, dy in DIRECTIONS:
            v, ahead = (sx - dx, sy - dy), (sx + dx, sy + dy)
            if not (0 <= v[0] < width and 0 <= v[1] < height) or v in opened: continue
            if len(path) > 1 and path[1] != ahead and ahead in opened: continue
            children.append(v)
        count = len(path)
        histogram[count] += mass * (1 if children else area - count + 1) / area
        for v in children: visit((v,) + path, opened | {v}, mass / len(children))
    for y in range(height):
        for x in range(width): visit(((x, y),), {(x, y)}, 1 / area)
    assert abs(sum(histogram.values()) - 1) < 1e-12
    return {"side": 4, "sampler": "uniform-legal-cell-v1", "occupancyProbabilities": dict(sorted(histogram.items())),
            "meanOpenCells": sum(k * p for k, p in histogram.items()),
            "probabilityAtLeastHalfOpen": sum(p for k, p in histogram.items() if k >= 8),
            "method": "Enumerated construction probabilities including target-size choice and early trapping."}


def main() -> None:
    exact = json.loads((STUDY / "stationary-distributions.json").read_text())
    path_study = json.loads((STUDY / "stationary-distributions.paths.json").read_text())
    old = baseline()
    (STUDY / "backward-baseline.json").write_text(json.dumps(old, indent=2) + "\n")
    uniform4 = exact[-1]
    line_chart(STUDY / "occupancy-comparison.png", "4 x 4 occupancy: one-pass growth vs uniform boards", [
        ("Uniform solvable boards", [(i, n / uniform4["solvableBoards"]) for i, n in enumerate(uniform4["occupancyCounts"]) if i]),
        ("Original backward growth", list(old["occupancyProbabilities"].items()))], "Open cells", "Probability")
    line_chart(STUDY / "board-mixing.png", "4 x 4 board sampler: distance to uniform", [
        ("Singleton start" if i == 0 else "Full-board start", [(p["step"], p["totalVariation"]) for p in study["points"]])
        for i, study in enumerate(uniform4["studies"])], "Iterations (including stays; log2 scale)",
        "Total variation distance; numerical floor 1e-12", True, True)
    line_chart(STUDY / "path-mixing.png", "3 x 3 solution edits: density weighting slows mixing", [
        (f"activity {a['activity']} / {h['initialOpenCells']}-cell start", [(p["step"], p["totalVariation"]) for p in h["trace"]])
        for a in path_study["activities"] for h in a["histories"]], "Iterations (including stays; log2 scale)",
        "Distance to the stated path distribution; numerical floor 1e-12", True, True)
    records = []
    for file in sorted(COLLECTION.rglob("stats.json")):
        meta = json.loads(file.read_text()); stats = meta["stats"]; dynamics = meta["dynamics"]
        directory = file.parent.relative_to(COLLECTION).as_posix()
        records.append({"id": directory, "generator": meta["generator"], "sampler": meta["sampler"],
                        "side": meta["side"], "activity": dynamics["activity"], "initialization": dynamics["initialization"],
                        "steps": dynamics["steps"], "seedHex": meta["seedHex"], "openCells": stats["openCells"],
                        "openFraction": stats["openFraction"], "solutionSlides": meta["solutionSlides"],
                        "generationSeconds": meta["generationSeconds"], "boardSha256": meta["boardSha256"],
                        "stats": f"{directory}/stats.json", "map": f"{directory}/map.png", "preview": f"{directory}/preview.png",
                        "detail": f"{directory}/center-detail.png" if meta["side"] > 128 else f"{directory}/detail.png",
                        "solutionDetail": f"{directory}/center-solution-detail.png" if meta["side"] > 128 else f"{directory}/solution-detail.png",
                        "solutionMap": f"{directory}/solution-map.png", "recipe": f"{directory}/recipe.json",
                        "constructionCode": f"{directory}/construction.code.json", "constructionFormat": "coil-construction-v1",
                        "board": f"{directory}/level.board.gz", "solution": f"{directory}/level.solution.gz",
                        "control": directory.startswith("controls/"), "distribution": dynamics["stationaryTarget"],
                        "mixing": dynamics["mixing"]})
    records.sort(key=lambda r: (r["control"], r["side"], r["activity"], r["id"]))
    for record in records:
        for key in ["stats", "map", "preview", "detail", "solutionDetail", "solutionMap", "recipe", "constructionCode", "board", "solution"]:
            if not (COLLECTION / record[key]).is_file(): raise FileNotFoundError(record[key])
    (COLLECTION / "manifest.json").write_text(json.dumps(records, indent=2) + "\n")
    with (COLLECTION / "summary.csv").open("w", newline="") as output:
        writer = csv.DictWriter(output, fieldnames=list(records[0])); writer.writeheader(); writer.writerows(records)
    largest = [r for r in records if r["side"] == 1000]
    panel = Image.new("RGB", (1360, 880), "#10231d"); draw = ImageDraw.Draw(panel)
    for i, r in enumerate(largest):
        x, y = i % 2 * 680, i // 2 * 440
        draw.text((x + 18, y + 10), f"1000 x 1000 / activity {r['activity']} / {r['openCells']:,} open", font=FONT, fill="white")
        with Image.open(COLLECTION / r["map"]) as image:
            panel.paste(image.convert("RGB").resize((310, 310), Image.Resampling.BOX), (x + 18, y + 50))
            panel.paste(image.crop((436, 436, 564, 564)).convert("RGB").resize((310, 310), Image.Resampling.NEAREST), (x + 352, y + 50))
        draw.text((x + 18, y + 375), f"FULL BOARD / {100*r['openFraction']:.2f}% OPEN", font=SMALL, fill="#bce4d1")
        draw.text((x + 352, y + 375), "DETAIL: 128 x 128 / x=436, y=436", font=SMALL, fill="#bce4d1")
        draw.text((x + 18, y + 403), "Stripe initialization; finite run; uniformity not claimed", font=SMALL, fill="#bce4d1")
    panel.save(COLLECTION / "largest-comparison.png")
    controls = [r for r in records if r["control"]]
    line_chart(STUDY / "initialization-comparison.png", "64 x 64: same seed and budget, different starting boards", [
        (r["initialization"], [(p["step"] / 4096, p["openCells"] / 4096)
                               for p in json.loads((COLLECTION / r["stats"]).read_text())["dynamics"]["trace"]])
        for r in controls], "Iterations per board cell", "Open fraction")
    samples = json.loads((STUDY / "uniform-4x4/sampling.json").read_text())["samples"]
    tiles = Image.new("RGB", (960, 6 * 150), "#10231d"); draw = ImageDraw.Draw(tiles)
    for i, sample in enumerate(samples):
        x, y = i % 8 * 120, i // 8 * 150
        board = Image.new("RGB", (4, 4), "black")
        for cell in range(16):
            if sample["mask"] & (1 << cell): board.putpixel((cell % 4, cell // 4), (255, 255, 255))
        tiles.paste(board.resize((96, 96), Image.Resampling.NEAREST), (x + 12, y + 10))
        draw.text((x + 10, y + 115), f"#{i+1}: {sample['openCells']} open", font=SMALL, fill="white")
    tiles.save(STUDY / "uniform-4x4.png")
    rows = ["| Board | Activity | Open cells | Coverage | Slides |", "|---|---:|---:|---:|---:|"]
    for r in records:
        rows.append(f"| [{r['id']}]({r['detail']}) | {r['activity']} | {r['openCells']:,} | {100*r['openFraction']:.2f}% | {r['solutionSlides']:,} |")
    (COLLECTION / "README.md").write_text(
        "# Solution-edit collection v1\n\n"
        "The saved identifier `reversible-path-v1` means undoable generator edits. "
        "Solutions need not be playable backward. Each specimen now includes a validated "
        "`construction.code.json` usable with `gen-any --code-file FILE --out NEW-STEM`; "
        "the manifest links these codes. [Coverage proof and terminology](../../UNIVERSAL-GENERATOR.md).\n\n"
        "20 specimens: 16, 64, 256, 512, and 1000 square; two saved 512-bit seeds; activities 1 and 2. "
        "Every main run starts from a validated serpentine corridor board and performs 100 iterations per board cell. "
        "Two additional 64-square controls use the same seed and five million steps from different starts. "
        "All planned runs are retained. All 22 specimens were fully regenerated from their seeds; boards, "
        "solutions, recipes, geometry, acceptance counts, and occupancy histories matched exactly "
        "(see `seed-verification.json`).\n\n"
        "**These are finite runs targeting weighted ordered solutions, not uniform board samples.** "
        "Activity 1 gives equal stationary weight to solutions; activity 2 gives each solution weight "
        "2^openCells. A board's weight also includes its number of solutions. Initialization dependence "
        "and remaining stripes are visible; equilibrium on large boards is not established.\n\n"
        "![1000-square full boards and labeled details](largest-comparison.png)\n\n"
        + "\n".join(rows) + "\n\n"
        "[Sampling study and measured bias](../sampling-v1/README.md). "
        "[Machine-readable manifest](manifest.json) and [CSV](summary.csv). "
        "Each folder includes validated gzip board/solution, final construction recipe, full map, "
        "solution colors, and exact geometry. Larger specimens also include visibly labeled "
        "128-square center details. `stats.json` records proposals, accepted operations, occupancy "
        "history, initialization, activity, seed, and the stationary target.\n\n"
        "Reproduce: build Release and run `python3 scripts/build_reversible_collection.py NEW-DIRECTORY "
        "--plan gallery/reversible-v1/plan.json`. Verify with `verify-collection gallery/reversible-v1`. "
        "Final recipes replay with `gen-backward --recipe FILE --out NEW-STEM`. "
        "Gallery integration should preserve the generator, target distribution, and initialization labels.\n"
    )
    lines = ["# Sampling study v1\n", "The exact sampler, the board-state Markov chain, and the scalable path sampler are implemented. "
             "Their output distributions are different and are labeled separately.\n",
             "## Exact uniform board sampling\n", "`sample-uniform 4 NEW-DIRECTORY --count 48` enumerates all 3,503 solvable boards, "
             "assigns one index per board, and draws indices uniformly. Solution multiplicity does not affect selection. "
             "The saved 48 draws include repeats when drawn; there is no uniqueness filter. Every witness also passes "
             "backward structural validation and independent Mortal Coil replay.\n",
             "![48 uniform draws](uniform-4x4.png)\n",
             f"Uniform 4x4 mean occupancy: **{uniform4['meanOpenCells']:.5f}**. Original backward sampler: "
             f"**{old['meanOpenCells']:.5f}**. Occupancy is calculated from complete distributions, not these 48 draws.\n",
             "![Occupancy bias](occupancy-comparison.png)\n",
             "## Uniform board-state chain\n", "The lazy random-cell-flip chain is implemented with exact catalogue membership. "
             "Every accepted transition and its reverse have equal probability; rejected and lazy steps remain in the clock. "
             "Transition probabilities were propagated from singleton and full-board starts on 2x2, 3x3, and 4x4. "
             "On 4x4, both approach uniformity to floating-point precision by 4,096 steps. "
             "This is a small-board result, not a bound for 1000-square boards. "
             "`sample-uniform 4 NEW-DIRECTORY --method chain --burn-in 4096 --stride 128` also exports actual "
             "chain draws; 48 are saved in `chain-4x4/` with their seed and labels. They remain correlated samples.\n",
             "![Board-chain convergence](board-mixing.png)\n",
             "## Undoable solution-edit chain\n", "All 477 ordered 3x3 solutions and all production proposal descriptors were enumerated. "
             "Detailed-balance residual was zero at double precision for activities 1, 2, and 4. "
             "The target is activity^openCells per solution, or solutionCount * activity^openCells per board.\n",
             "![Path-chain convergence](path-mixing.png)\n",
             "| Activity | Stationary mean open cells (3x3) | TV distance after 16,384 steps: singleton / full start |",
             "|---:|---:|---:|"]
    for a in path_study["activities"]:
        tv = [h["trace"][-1]["totalVariation"] for h in a["histories"]]
        lines.append(f"| {a['activity']} | {a['meanOpenCells']:.5f} | {tv[0]:.6f} / {tv[1]:.6f} |")
    lines += ["\nStronger occupancy weighting slows exploration. Matching an occupancy average does not establish "
              "that the whole distribution has converged.\n", "## Large boards and initialization\n",
              "[22 saved solution-edit specimens through 1000 square](../reversible-v1/README.md). "
              "The four 1000-square runs contain " + ", ".join(f"{r['openCells']:,}" for r in largest) + " open cells. "
              "These large occupied puzzles start from large valid corridor boards; they were not grown from singleton seeds "
              "to those sizes during this run.\n", "![Initialization comparison](initialization-comparison.png)\n",
              "At 64x64, the controlled five-million-step runs end at " + ", ".join(f"{r['openCells']:,} cells from {r['initialization']}" for r in controls) +
              ". This demonstrates remaining initialization bias. The large samples must not be advertised as uniform or equilibrated.\n",
              "## Reproduce\n", "Build Release. `sampling-study NEW.json` writes board-distribution and `.paths.json` "
              "studies. `sample-uniform 4 NEW-DIRECTORY --count 48 --seed-hex HEX` replays exact draws using the seed in "
              "`uniform-4x4/sampling.json`. `python3 scripts/report_sampling.py` rebuilds the manifests and PNG figures "
              "from saved JSON and maps using Pillow. Floating-point propagation has rounding error but no Monte Carlo error.\n"]
    (STUDY / "README.md").write_text("\n".join(lines) + "\n")


if __name__ == "__main__":
    main()
