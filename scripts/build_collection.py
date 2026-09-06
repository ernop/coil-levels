"""Reproduce the 34 retained preliminary samples; the survey superseded the original broad plan."""
import argparse
import json
from pathlib import Path
import subprocess

ROOT = Path(__file__).resolve().parents[1]

def jobs():
    recipes = json.loads((ROOT / 'gallery/recipes.json').read_text())
    for side in (300, 500, 1000):
        selected = recipes if side != 1000 else recipes[:1]
        for recipe in selected:
            for seed in (101, 202):
                yield side, seed, recipe

def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--resume', action='store_true')
    args = parser.parse_args()
    for number, (side, seed, recipe) in enumerate(jobs(), 1):
        name = f'{side}-{recipe["id"]}-s{seed}'
        dest = ROOT / 'gallery/boards' / name
        if args.resume and (dest / 'stats.json').exists():
            print(f'{number}/34 already complete: {name}', flush=True)
            continue
        command = ['dotnet', str(ROOT / 'bin/Release/net10.0/coil-levels-csharp.dll'), 'specimen', str(dest), str(side), str(seed)]
        for key in ('picker', 'segpicker', 'lim', 'loops', 'keep-deadends'):
            if key in recipe:
                command.append('--' + key)
                if recipe[key] is not True:
                    command.append(recipe[key])
        print(f'{number}/34 starting {name}', flush=True)
        subprocess.run(command, cwd=ROOT, check=True)
    print('All 34 preliminary specimens generated.', flush=True)

if __name__ == '__main__':
    main()
