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
    names = subprocess.check_output(['dotnet', DLL, 'pickers'], text=True).splitlines()[0].split(':', 1)[1].split()
    configs = []
    for name in names:
        configs.append(dict(id=f'tweak-{name}', family='tweak survey', picker=name, segpicker='Weighted4', lim='20'))
    for name in ('rnd99', 'last', 'first', '2lim10', 'equal23short', 'sz2-3'):
        for seg in ('First', 'Last', 'Longest', 'Weighted', 'Weighted2', 'Weighted3'):
            configs.append(dict(id=f'seg-{name}-{seg}', family='segment interaction', picker=name, segpicker=seg, lim='20'))
    for name in ('rnd99', 'last', '2lim10'):
        for variant, options in [
            ('unlimited', {'lim':'none'}), ('lim5', {'lim':'5'}),
            ('onepass', {'loops':'1'}), ('keepends', {'keep-deadends':True}),
            ('wander5', {'wander-max':'5'}), ('wanderfull', {'wander-full':True}),
            ('wander8steps', {'wander-steps':'8'})]:
            configs.append(dict(id=f'control-{name}-{variant}', family='generation control', picker=name, segpicker='Weighted4', lim='20') | options)
    return configs

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
