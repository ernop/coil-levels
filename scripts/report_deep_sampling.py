"""Summarize the saved deeper-sampling experiments without selecting reruns."""
import gzip
import json
from pathlib import Path
from statistics import mean
import sys

from PIL import Image, ImageDraw, ImageFont

ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(ROOT))
from analysis.chain_diagnostics import diagnose
from scripts.report_sampling import line_chart

COLLECTION = ROOT / "gallery/deep-v1"
FONT = ImageFont.truetype("DejaVuSans.ttf", 15)
SMALL = ImageFont.truetype("DejaVuSans.ttf", 12)
TITLE = ImageFont.truetype("DejaVuSans.ttf", 22)
COLORS = {"singleton": "#188070", "backward": "#8053a3", "horizontal": "#c55225", "vertical": "#266bbb"}
METRICS = ["openFraction", "adjacencyAnisotropy", "absoluteAnisotropy", "pathAnisotropy", "solutionSlides"]


def load_runs(directory: str, kernel: str) -> list[dict]:
    result = []
    for path in sorted((COLLECTION / directory).glob(f"32-{kernel}-*/stats.json")):
        data = json.loads(path.read_text())
        data["directory"] = path.parent.relative_to(COLLECTION).as_posix()
        result.append(data)
    return result


def trace_figure(path: Path, groups: list[tuple[str, list]], title: str) -> None:
    canvas = Image.new("RGB", (1200, 100 + 320 * len(groups)), "white")
    draw = ImageDraw.Draw(canvas)
    draw.text((22, 12), title, font=TITLE, fill="#172b35")
    for i, (name, color) in enumerate(COLORS.items()):
        draw.text((24 + 280*i, 48), name, font=FONT, fill=color)
    for row, (label, records) in enumerate(groups):
        for column, (metric, lo, hi, metric_label) in enumerate([
            ("openFraction", 0, 1, "Open fraction"), ("pathAnisotropy", -1, 1, "Path direction: horizontal +1 / vertical -1")]):
            left, top = 70 + 600*column, 112 + row*320
            width, height = 500, 220
            draw.text((left, top-24), f"{label} / {metric_label}", font=FONT, fill="#172b35")
            maximum = max(d["dynamics"]["steps"] for d in records)
            for i in range(5):
                value = lo + (hi-lo)*i/4; y = top + height - height*i/4
                draw.line((left, y, left+width, y), fill="#dce2e5")
                draw.text((left-42, y-6), f"{value:.2f}", font=SMALL, fill="#334d59")
                x = left+width*i/4
                draw.text((x-20, top+height+8), f"{maximum*i/4/1e6:.1f}M", font=SMALL, fill="#334d59")
            for record in records:
                trace = record["dynamics"]["trace"]
                points = [(left+t["step"]/maximum*width, top+height-(t[metric]-lo)/(hi-lo)*height) for t in trace]
                draw.line(points, fill=COLORS[record["dynamics"]["initialization"]], width=2)
    draw.text((24, canvas.height-25), "All chains shown; same colors share initialization. Steps include stays. Similar occupancy does not establish mixing.", font=SMALL, fill="#334d59")
    canvas.save(path)


def main() -> None:
    summaries = []
    groups = [("independent-controls", "legacy"), ("independent-controls", "deep"), ("effort-controls", "legacy"), ("long-controls", "deep")]
    for directory, kernel in groups:
        records = load_runs(directory, kernel)
        expected = 4 if directory == "long-controls" else 8
        if len(records) != expected:
            raise ValueError(f"Expected {expected} completed runs in {directory}/{kernel}, found {len(records)}")
        diagnostics = {}
        for metric in METRICS:
            traces = [[p[metric] for p in r["dynamics"]["trace"][len(r["dynamics"]["trace"])//2+1:]] for r in records]
            diagnostics[metric] = diagnose(traces)
        summaries.append({"collection": directory, "kernel": kernel, "chains": len(records),
                          "stepsPerChain": records[0]["dynamics"]["steps"],
                          "meanSeconds": mean(r["generationSeconds"] for r in records),
                          "finalOpenRange": [min(r["stats"]["openCells"] for r in records), max(r["stats"]["openCells"] for r in records)],
                          "discardedTraceFraction": 0.5, "diagnostics": diagnostics,
                          "streams": "SHA-512(base seed bytes || ASCII initialization), distinct within each diagnostic group"})
    (COLLECTION / "diagnostics.json").write_text(json.dumps(summaries, indent=2, allow_nan=False)+"\n")
    old, new = load_runs("independent-controls", "legacy"), load_runs("independent-controls", "deep")
    trace_figure(COLLECTION / "traces-32.png", [("Earlier edits / 2M", old), ("Block mixture / 2M", new)], "32 x 32: occupancy improves faster than path direction")
    trace_figure(COLLECTION / "traces-long.png", [("Block mixture / 20M", load_runs("long-controls", "deep"))], "Longer independent chains still retain directional differences")

    panel = Image.new("RGB", (1040, 655), "#14291f"); draw = ImageDraw.Draw(panel)
    draw.text((24, 15), "32 x 32 full boards / seed 1 / 2 million steps per chain", font=TITLE, fill="white")
    for row, records in enumerate([old, new]):
        for column, init in enumerate(COLORS):
            record = next(r for r in records if r["dynamics"]["initialization"] == init and r["directory"].endswith("s1"))
            x, y = 24 + column*258, 66 + row*290
            label = "Earlier edits" if row == 0 else "Block mixture"
            draw.text((x, y), f"{label} / {init}", font=FONT, fill="white")
            board = Image.open(COLLECTION / record["directory"] / "map.png")
            panel.paste(board.resize((224, 224), Image.Resampling.NEAREST), (x, y+28))
            draw.text((x, y+260), f"{record['stats']['openCells']} open / {record['solutionSlides']} slides", font=SMALL, fill="#bdd6cc")
    panel.save(COLLECTION / "boards-32.png")

    exact_old = json.loads((ROOT / "gallery/sampling-v1/stationary-distributions.paths.json").read_text())
    exact_new = json.loads((COLLECTION / "exact-3x3.json").read_text())
    series = []
    for label, data in [("Earlier", exact_old), ("Blocks", exact_new)]:
        activity = next(a for a in data['activities'] if a['activity'] == 2)
        for history in activity['histories']:
            series.append((f"{label} / {history['initialOpenCells']}-cell start", [(p['step'], p['totalVariation']) for p in history['trace']]))
    line_chart(COLLECTION / "exact-mixing.png", "Exact 3 x 3 probabilities / activity 2 / same step count", series,
               "Iterations, including stays (log2)", "Distance to weighted solution target (log10)", True, True)

    large = COLLECTION / "large/1000-deep-horizontal-s1"
    previous = ROOT / "gallery/reversible-v1/1000-reversible-a2-s1"
    image = Image.new("RGB", (1040, 1140), "#14291f"); draw = ImageDraw.Draw(image)
    draw.text((24, 14), "1000 x 1000 / same seed, activity 2, 100 million steps", font=TITLE, fill="white")
    for column, (label, folder) in enumerate([("Earlier edits", previous), ("Block mixture", large)]):
        x = 24 + 512*column; data = json.loads((folder / "stats.json").read_text()); board = Image.open(folder / "map.png")
        draw.text((x, 54), f"{label}: {data['stats']['openCells']:,} open", font=FONT, fill="white")
        draw.text((x, 78), f"{data['solutionSlides']:,} slides / {data['generationSeconds']:.1f}s recorded", font=FONT, fill="#bdd6cc")
        image.paste(board.resize((480,480), Image.Resampling.BOX), (x,108))
        draw.text((x, 600), "FULL BOARD", font=FONT, fill="white")
        image.paste(board.crop((436,436,564,564)).resize((440,440),Image.Resampling.NEAREST),(x,630))
        draw.text((x, 1080), "CROP: x=436..563, y=436..563 (128 x 128)", font=SMALL, fill="white")
    draw.text((24,1115), "Finite samples of weighted solutions; both retain initialization bias. Timings are not isolated microbenchmarks.", font=SMALL, fill="#bdd6cc")
    image.save(COLLECTION / "large-comparison.png")

    uniform = []
    for side in [5,6,7]:
        folder = COLLECTION / f"uniform-{side}x{side}"; data = json.loads((folder / "sampling.json").read_text())
        canvas = Image.new("RGB", (1020, 95+145*((data['Accepted']+7)//8)), "#14291f"); draw = ImageDraw.Draw(canvas)
        draw.text((20,15), f"{side} x {side}: {data['Accepted']} independent uniform boards / {data['Attempts']:,} proposals", font=TITLE, fill="white")
        draw.text((20,48), "All accepted draws shown; repeats retained. " + ("Requested count completed." if data['completed'] else "Attempt limit reached; partial collection."), font=FONT, fill="#bdd6cc")
        for i, sample in enumerate(data['samples']):
            stem = f"sample-{i+1:04d}"
            board_string = (folder / (stem+'.board')).read_text(); cells = board_string.split('board=')[1].strip()
            board = Image.frombytes('L',(side,side),bytes(255 if c=='.' else 0 for c in cells))
            board.save(folder / (stem+'.map.png'))
            x,y = 20+(i%8)*125, 90+(i//8)*145
            canvas.paste(board.resize((100,100),Image.Resampling.NEAREST),(x,y))
            draw.text((x,y+108),f"#{i+1} / {sample['openCells']} open",font=SMALL,fill="white")
        canvas.save(folder / 'boards.png')
        uniform.append({"side":side,"accepted":data['Accepted'],"requested":data['requested'],"attempts":data['Attempts'],
                        "seconds":data['seconds'],"completed":data['completed'],"meanOpenCells":mean(s['openCells'] for s in data['samples']),
                        "image":f"uniform-{side}x{side}/boards.png","metadata":f"uniform-{side}x{side}/sampling.json"})
    (COLLECTION / 'uniform-summary.json').write_text(json.dumps(uniform,indent=2)+'\n')
    records = []
    for file in sorted(COLLECTION.rglob('stats.json')):
        data = json.loads(file.read_text()); directory = file.parent.relative_to(COLLECTION).as_posix()
        record = {"id":directory,"generator":data['generator'],"sampler":data['sampler'],"side":data['side'],
                  "openCells":data['stats']['openCells'],"solutionSlides":data['solutionSlides'],"seedHex":data['seedHex'],
                  "initialization":data['dynamics']['initialization'],"steps":data['dynamics']['steps']}
        for key,name in [('map','map.png'),('preview','preview.png'),('detail','detail.png'),('solutionMap','solution-map.png'),
                         ('stats','stats.json'),('board','level.board.gz'),('solution','level.solution.gz'),('constructionCode','construction.code.json')]:
            record[key] = f"{directory}/{name}"
            if not (COLLECTION / record[key]).is_file(): raise FileNotFoundError(record[key])
        records.append(record)
    (COLLECTION / 'manifest.json').write_text(json.dumps(records,indent=2)+'\n')
    rows = ['| Kernel | Steps per chain | Chains | Final open cells | Occupancy R-hat | Path-direction R-hat |',
            '|---|---:|---:|---:|---:|---:|']
    for s in summaries:
        rows.append(f"| {s['kernel']} | {s['stepsPerChain']:,} | {s['chains']} | {s['finalOpenRange'][0]}–{s['finalOpenRange'][1]} | {s['diagnostics']['openFraction']['rHat']:.3f} | {s['diagnostics']['pathAnisotropy']['rHat']:.3f} |")
    (COLLECTION / 'README.md').write_text('# Deeper sampling experiments v1\n\n'
        '[Research, method, guarantees, and interpretation](../../DEEP-SAMPLING.md). '
        f'This collection retains all {len(records)} path specimens, plus 48 uniform 5x5 boards, 48 uniform 6x6 boards, '
        'and six uniform 7x7 boards from a run that reached its attempt limit. All saved path specimens have images, geometry, solutions, and construction codes.\n\n'
        '![Independent-start board comparison](boards-32.png)\n\n'+'\n'.join(rows)+'\n\n'
        'All these R-hat values flag remaining disagreement. They use the last half of each trace, then rank normalization, splitting, and folding. '
        'A value near 1 alone would not prove mixing. [Diagnostics and bulk ESS](diagnostics.json). '
        'The 4M legacy run is an approximate runtime comparison; recorded mean runtimes are in diagnostics.json.\n\n'
        '![Trace comparison](traces-32.png)\n\n![Longer runs](traces-long.png)\n\n'
        '![Exact small-state comparison](exact-mixing.png)\n\n![Million-cell comparison](large-comparison.png)\n\n'
        '[Uniform 5x5 boards](uniform-5x5/boards.png), [uniform 6x6 boards](uniform-6x6/boards.png), '
        '[partial uniform 7x7 collection](uniform-7x7/boards.png). [Uniform run statistics](uniform-summary.json).\n\n'
        '[Path specimen manifest](manifest.json). The initial `controls/` pilot reused the same two base streams across starts and is retained separately; '
        'the reported diagnostics use `independent-controls/`, `effort-controls/`, and `long-controls/`, with distinct streams per start. '
        'Nothing was selected or rerun to replace a less favorable result. Reproduction plans record every parameter and stream derivation.\n\n'
        '`python3 scripts/build_deep_collection.py NEW-DIRECTORY --plan gallery/deep-v1/plan.json` reproduces the main comparison. '
        '`long-plan.json` and `effort-plan.json` select the other experiments. `python3 scripts/report_deep_sampling.py` rebuilds this report.\n')
    print(f'Reported {len(records)} path specimens; independent diagnostics and all uniform draws retained.')


if __name__ == '__main__':
    main()
