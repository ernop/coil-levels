"""Run the declared multi-start comparison; keep every planned result."""
import argparse
import hashlib
import json
from pathlib import Path
import subprocess

ROOT = Path(__file__).resolve().parents[1]


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("destination", type=Path)
    parser.add_argument("--plan", type=Path, default=ROOT / "gallery/deep-v1/plan.json")
    args = parser.parse_args()
    plan = json.loads(args.plan.read_text())
    directory = args.destination.resolve()
    directory.mkdir(parents=True, exist_ok=False)
    (directory / "plan.json").write_text(json.dumps(plan, indent=2) + "\n")
    for kernel in plan["kernels"]:
        for init in plan["initializations"]:
            for index, seed in enumerate(plan["seeds"], 1):
                if plan["seedDerivation"] == "sha512-base-and-initialization-v1":
                    seed = hashlib.sha512(bytes.fromhex(seed) + init.encode("ascii")).hexdigest()
                elif plan["seedDerivation"] != "paired-seeds-v1":
                    raise ValueError("Unknown seedDerivation")
                name = f"{plan['side']}-{kernel}-{init}-s{index}"
                subprocess.run(["dotnet", str(ROOT / "bin/Release/net10.0/coil-levels-csharp.dll"),
                                "deep-specimen", str(directory / name), str(plan["side"]),
                                "--steps", str(plan["steps"]), "--activity", str(plan["activity"]),
                                "--observations", str(plan["observations"]), "--init", init,
                                "--kernel", kernel, "--seed-hex", seed], check=True)


if __name__ == "__main__":
    main()
