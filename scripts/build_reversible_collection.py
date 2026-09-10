"""Reproduce every planned reversible specimen without selection or retries."""
import argparse
import json
from pathlib import Path
import subprocess

ROOT = Path(__file__).resolve().parents[1]


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("destination", type=Path)
    parser.add_argument("--plan", type=Path, default=ROOT / "gallery/reversible-v1/plan.json")
    args = parser.parse_args()
    plan = json.loads(args.plan.read_text())
    output = args.destination.resolve()
    output.mkdir(parents=True, exist_ok=False)
    (output / "plan.json").write_text(json.dumps(plan, indent=2) + "\n")
    for side in plan["sizes"]:
        for activity in plan["activities"]:
            for index, seed in enumerate(plan["seeds"], 1):
                directory = output / f"{side}-reversible-a{activity}-s{index}"
                subprocess.run(["dotnet", str(ROOT / "bin/Release/net10.0/coil-levels-csharp.dll"),
                                "reversible-specimen", str(directory), str(side), "--seed-hex", seed,
                                "--steps", str(plan["stepsPerCell"] * side * side), "--activity", str(activity),
                                "--init", plan["initialization"], "--spacing", str(plan["spacing"])], check=True)
    for init in ["singleton", "stripes"]:
        subprocess.run(["dotnet", str(ROOT / "bin/Release/net10.0/coil-levels-csharp.dll"),
                        "reversible-specimen", str(output / "controls" / f"64-{init}-a2-s1"), "64",
                        "--seed-hex", plan["seeds"][0], "--steps", "5000000", "--activity", "2", "--init", init], check=True)


if __name__ == "__main__":
    main()
