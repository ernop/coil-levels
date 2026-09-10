"""Package measured data for an offline HTML explorer; do not synthesize metrics."""
import argparse
import json
from generate_selected import planned_jobs, name as job_name
from build_collection import jobs as preliminary_jobs
from pathlib import Path
from gallery_sampling import COLLECTIONS, sampling_boards, assets
from import_original_boards import SOURCES
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
    for b in boards:
        b['collection'] = b['phase']
        directory = ROOT / b['path'][3:]
        b['assets'] = assets(directory, directory / 'level.board.gz', directory / 'level.solution.gz')
        b['detailLabel'] = 'Central 128×128 crop · coordinates printed on image'
    original_count = len(boards)
    additions = sampling_boards()
    boards.extend(additions)
    for board in boards:
        board.update(width=board['stats']['width'], height=board['stats']['height'], source=SOURCES['coil-levels-generator'], solutionAvailable=True)
    imported = json.loads((ROOT/'gallery/imported-boards.json').read_text())
    for entry in imported['boards']:
        directory = ROOT / entry['path']
        saved = json.loads((directory/'stats.json').read_text())
        links = {key: '../' + (directory / name).relative_to(ROOT).as_posix() for key, name in [('map','map.png'),('preview','preview.png'),('board','level.board.gz'),('stats','stats.json')]}
        if saved['solutionAvailable']: links['solution'] = '../' + (directory/'level.solution.gz').relative_to(ROOT).as_posix()
        boards.append(dict(saved, id=entry['id'], path='../'+entry['path'], phase='archive', side=max(saved['width'],saved['height']), seed=saved.get('seed'),
                           recipe={'id': saved['title'], 'family': saved['collection']}, assets=links))
    if len({b['id'] for b in boards}) != len(boards):
        raise RuntimeError('Duplicate gallery IDs')
    survey=json.loads((ROOT/'gallery/survey-results.json').read_text())
    selection=json.loads((ROOT/'gallery/selection.json').read_text())
    expected={f'300-{c["id"]}-s{s}' for c in configs for s in (101,202,303)}
    expected.update(job_name(j) for j in planned_jobs())
    expected.update(f'{side}-{c["id"]}-s{seed}' for side,seed,c in preliminary_jobs())
    missing=sorted(expected-{b['id'] for b in boards})
    if args.require_complete and missing:raise RuntimeError('Missing planned specimens: '+', '.join(missing))
    result={'complete':not missing,'expectedCount':len(expected)+len(additions)+len(imported['boards']),'collections':COLLECTIONS,'archiveCount':len(imported['boards']),'originalCount':original_count,'samplingCount':len(additions),'missing':missing,'boards':boards,'configs':configs,'survey':survey,'selection':selection}
    (ROOT/'gallery/catalog.json').write_text(json.dumps(result,separators=(',',':'))+'\n')
    (ROOT/'gallery/catalog.js').write_text('window.COIL_CATALOG = '+json.dumps(result,separators=(',',':'))+';\n')
    print(f'{len(boards)} specimens packaged')
if __name__=='__main__':main()
