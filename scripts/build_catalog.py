"""Package measured data for an offline HTML explorer; do not synthesize metrics."""
import argparse
import json
from generate_selected import planned_jobs, name as job_name
from build_collection import jobs as preliminary_jobs
from pathlib import Path
ROOT=Path(__file__).resolve().parents[1]
def main():
    parser=argparse.ArgumentParser();parser.add_argument("--require-complete",action="store_true");args=parser.parse_args()
    configs=json.loads((ROOT/'gallery/survey-configs.json').read_text())
    keys={c['id']:c for c in configs}
    boards=[]
    for p in sorted(p for phase in ('boards', 'survey') for p in (ROOT/'gallery'/phase).glob('*/stats.json')):
        d=json.loads(p.read_text());name=p.parent.name
        cid=name[len(str(d['side']))+1:].rsplit('-s',1)[0]
        recipe=keys.get(cid)
        if recipe is None:
            recipe={'id':'initial-late-pivot' if cid=='short-tweaks' else cid,'family':'initial sample'} | d['options']
        s=d['stats']
        boards.append(dict(id=name,path='../'+p.parent.relative_to(ROOT).as_posix(),recipe=recipe,phase=p.parent.parent.name,**d))
    survey=json.loads((ROOT/'gallery/survey-results.json').read_text())
    selection=json.loads((ROOT/'gallery/selection.json').read_text())
    expected={f'300-{c["id"]}-s{s}' for c in configs for s in (101,202,303)}
    expected.update(job_name(j) for j in planned_jobs())
    expected.update(f'{side}-{c["id"]}-s{seed}' for side,seed,c in preliminary_jobs())
    missing=sorted(expected-{b['id'] for b in boards})
    if args.require_complete and missing:raise RuntimeError('Missing planned specimens: '+', '.join(missing))
    result={'complete':not missing,'expectedCount':len(expected),'missing':missing,'boards':boards,'configs':configs,'survey':survey,'selection':selection}
    (ROOT/'gallery/catalog.json').write_text(json.dumps(result,separators=(',',':'))+'\n')
    (ROOT/'gallery/catalog.js').write_text('window.COIL_CATALOG = '+json.dumps(result,separators=(',',':'))+';\n')
    print(f'{len(boards)} specimens packaged')
if __name__=='__main__':main()
