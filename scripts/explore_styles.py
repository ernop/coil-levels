"""Controlled 300-square style survey: all tweak names, segment interactions, seed variation."""
from concurrent.futures import ThreadPoolExecutor, as_completed
import json
from pathlib import Path
import subprocess
import time

ROOT = Path(__file__).resolve().parents[1]
DLL = str(ROOT / 'bin/Release/net10.0/coil-levels-csharp.dll')
SEEDS = (101, 202, 303)

def configurations():
    # Reproduction uses the recorded study, even when new picker names are added later.
    return json.loads((ROOT/'gallery/survey-configs.json').read_text())

def run(config, seed):
    name = f'300-{config["id"]}-s{seed}'
    dest = ROOT / 'gallery/survey' / name
    if (dest/'stats.json').exists():
        return dict(id=name, status='complete', config=config, seed=seed)
    command = ['dotnet', DLL, 'specimen', str(dest), '300', str(seed)]
    for key, value in config.items():
        if key not in ('id','family'):
            command += ['--'+key]
            if value is not True: command.append(value)
    start=time.monotonic()
    try:
        result = subprocess.run(command, text=True, capture_output=True, timeout=120)
        return dict(id=name, status='complete' if result.returncode==0 else 'failed', config=config, seed=seed,
                    seconds=time.monotonic()-start, output=(result.stdout+result.stderr)[-3000:])
    except subprocess.TimeoutExpired:
        return dict(id=name, status='timeout', config=config, seed=seed, seconds=time.monotonic()-start,
                    output='Stopped at explicit 120-second generation/export budget. No valid specimen claimed.')

def main():
    configs=configurations()
    (ROOT/'gallery/survey-configs.json').write_text(json.dumps(configs,indent=2)+'\n')
    results=[]
    with ThreadPoolExecutor(max_workers=3) as pool:
        futures=[pool.submit(run,c,s) for c in configs for s in SEEDS]
        for f in as_completed(futures):
            r=f.result();results.append(r)
            print(f'{len(results)}/{len(futures)} {r["status"]} {r["id"]}',flush=True)
            (ROOT/'gallery/survey-results.json').write_text(json.dumps(sorted(results,key=lambda r:r['id']),indent=2)+'\n')
if __name__=='__main__':main()
