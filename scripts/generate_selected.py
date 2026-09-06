"""Scale measured representatives; at most two <=5000 jobs, one 10000 job."""
import argparse
from concurrent.futures import ThreadPoolExecutor
import json
from pathlib import Path
import subprocess
ROOT=Path(__file__).resolve().parents[1]
def planned_jobs():
    selection=json.loads((ROOT/'gallery/selection.json').read_text())
    jobs=[]
    for side in (500,1000,2000,5000,10000):
        key='selected' if side==500 else ('scaleSelected' if side<=2000 else ('largestSelected' if side==5000 else 'tenThousandSelected'))
        selected=selection[key]
        if side==5000:selected=selected+selection['additionalFiveThousand']
        jobs.extend((side,g['config'],seed) for g in selected for seed in (g['seeds'] if side<=2000 or side==10000 else g['seeds'][:1]))
    return [j for j in jobs if name(j) not in selection["excludedAtScale"]]

def name(job):
    side,c,seed=job
    return f'{side}-{c["id"]}-s{seed}'

def run(job):
    side,c,seed=job
    dest=ROOT/'gallery/boards'/name(job)
    if (dest/'stats.json').exists():
        print(f'already complete {dest.name}',flush=True);return
    command=['dotnet',str(ROOT/'bin/Release/net10.0/coil-levels-csharp.dll'),'specimen',str(dest),str(side),str(seed)]
    for k,v in c.items():
        if k not in ('id','family'):
            command.append('--'+k)
            if v is not True:command.append(v)
    print(f'starting {dest.name}',flush=True)
    subprocess.run(command,cwd=ROOT,check=True)

def main():
    parser=argparse.ArgumentParser()
    parser.add_argument('--sizes',nargs='+',type=int,choices=[500,1000,2000,5000,10000],default=[500,1000,2000,5000,10000])
    parser.add_argument('--exclude-active',nargs='*',default=[],help='Explicit IDs already running in a separately supervised queue; never counts them as complete')
    args=parser.parse_args();all_jobs=planned_jobs()
    if set(args.exclude_active)-{name(j) for j in all_jobs}:raise ValueError('Unknown excluded specimen ID')
    for side in args.sizes:
        jobs=[j for j in all_jobs if j[0]==side and name(j) not in args.exclude_active]
        print(f'Size {side}: {len(jobs)} jobs in this dispatch',flush=True)
        with ThreadPoolExecutor(max_workers=1 if side==10000 else 2) as pool:list(pool.map(run,jobs))
    print('Dispatched jobs complete. Excluded jobs require separate completion verification.',flush=True)
if __name__=='__main__':main()
