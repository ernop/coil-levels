"""Replace degenerate full-walk specimens using the bounded initial-walk policy; preserve audit metadata."""
from concurrent.futures import ThreadPoolExecutor
import json
from pathlib import Path
import shutil
import subprocess
import time
ROOT=Path(__file__).resolve().parents[1]
DLL=ROOT/'bin/Release/net10.0/coil-levels-csharp.dll'
ARCHIVE=Path('/tmp/coil-open-region-repair-originals')

def run(path):
    old=json.loads(path.read_text());directory=path.parent;backup=ARCHIVE/directory.parent.name/directory.name
    backup.parent.mkdir(parents=True,exist_ok=True)
    if backup.exists():raise RuntimeError(f'Backup already exists; inspect before retrying {backup}')
    shutil.move(str(directory),str(backup))
    opts=old['options'] | {'wander-steps':'8'}
    command=['dotnet',str(DLL),'specimen',str(directory),str(old['side']),str(old['seed'])]
    for k,v in opts.items():command+=['--'+k,str(v)]
    start=time.monotonic()
    try:
        subprocess.run(command,check=True,timeout=1800)
        subprocess.run(['dotnet',str(DLL),'verify-collection',str(directory)],check=True,timeout=180)
    except BaseException:
        # Original artifacts remain available in the archive for an explicit recovery.
        raise
    new=json.loads(path.read_text())
    record={'id':directory.name,'side':old['side'],'beforeSha256':old['boardSha256'],'afterSha256':new['boardSha256'],
            'beforeOpenFraction':old['stats']['openFraction'],'afterOpenFraction':new['stats']['openFraction'],
            'beforeOpenSquare':old['stats']['largestOpenSquare'][2],'afterOpenSquare':new['stats']['largestOpenSquare'][2],
            'seconds':round(time.monotonic()-start,2),'validation':'Persisted pair replayed; geometry, hashes, and map cells verified',
            'options':opts}
    record_dir=ROOT/'gallery/repairs';record_dir.mkdir(exist_ok=True)
    (record_dir/(directory.name+'.json')).write_text(json.dumps(record,indent=2)+'\n')
    print('REPAIRED '+directory.name,flush=True)

def main():
    targets=[]
    for p in sorted(p for phase in ('boards', 'survey') for p in (ROOT/'gallery'/phase).glob('*/stats.json')):
        d=json.loads(p.read_text())
        if d['stats']['largestOpenSquare'][2]>max(8,d['side']//5):targets.append(p)
    print(f'{len(targets)} specimens require repair',flush=True)
    small=[p for p in targets if json.loads(p.read_text())['side']<10000]
    large=[p for p in targets if p not in small]
    with ThreadPoolExecutor(max_workers=2) as pool:list(pool.map(run,small))
    for p in large:run(p)
if __name__=='__main__':main()
