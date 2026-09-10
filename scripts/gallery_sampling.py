"""Adapt saved experiments to the shared gallery without resampling or dropping repeats."""
import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
COLLECTIONS = [
    dict(id='original-game', label='Original game', description='All 1,208 numbered layouts in the game author’s published coilbench archive.', target='Original game levels; generation settings and solutions are not supplied in this archive.', report='../gallery/ARCHIVES.md'),
    dict(id='legacy-generator', label='Earlier generated boards', description='Every distinct layout in the 3,306 tracked .coil files. Identical archived occurrences share one card; their filenames and headings are preserved.', target='Historical generator outputs, imported unchanged. No saved solution certificate.', report='../gallery/ARCHIVES.md'),
    dict(id='saved-hard', label='Earlier solver-selected boards', description='Twelve earlier generator outputs selected by solver effort, with their saved legal solutions.', target='Selected examples, not a random sample.', report='../HARDNESS.md'),
    dict(id='deep-v1', label='Deeper solution edits', description='Exact small-region route replacements mixed with endpoint and detour edits. Includes pilot, independent controls, longer runs, and the 1000-square run.', target='At stationarity: solutionCount(board) × activity^openCells. Finite runs remain dependent on initialization; these are not uniform board samples.', report='../DEEP-SAMPLING.md'),
    dict(id='reversible-v1', label='Earlier solution edits', description='Undoable endpoint growth, deletion, and rectangular detours. “Reversible” refers to edits, not playing the solution backward.', target='At stationarity: solutionCount(board) × activity^openCells. Finite runs have unresolved mixing.', report='../SAMPLING.md'),
    dict(id='backward-v1', label='Backward growth', description='Grow a legal solution backward from its finish. Stop at any length, including boards that could still grow.', target='Complete construction language; this particular random growth policy often gets trapped at low occupancy. It does not sample boards uniformly.', report='../UNIVERSAL-GENERATOR.md'),
    dict(id='uniform-4x4', label='Uniform boards · 4×4', description='48 independent draws from all 3,503 solvable 4×4 boards; repeated outcomes retained.', target='Uniform boards under ideal random choices. Exhaustive catalog, no solution-count weighting.', report='../SAMPLING.md'),
    dict(id='chain-4x4', label='Board chain · 4×4', description='48 saved states of a board-flip Markov chain over the exact solvable catalog.', target='Uniform boards at stationarity; finite chain states can be correlated and need not be equilibrated.', report='../SAMPLING.md'),
    dict(id='uniform-5x5', label='Uniform boards · 5×5', description='48 of 48 requested draws. Fair-bit boards accepted only after exact solving.', target='Independent uniform boards under ideal fair bits; repeated outcomes retained.', report='../DEEP-SAMPLING.md'),
    dict(id='uniform-6x6', label='Uniform boards · 6×6', description='48 of 48 requested draws. Fair-bit boards accepted only after exact solving.', target='Independent uniform boards under ideal fair bits; repeated outcomes retained.', report='../DEEP-SAMPLING.md'),
    dict(id='uniform-7x7', label='Uniform boards · 7×7 (partial)', description='6 of 12 requested draws. Stopped at the 10,000,000-attempt limit; the batch is incomplete.', target='Accepted draws use uniform-board rejection sampling under ideal fair bits. Incomplete solver decisions abort instead of being counted as unsolvable.', report='../DEEP-SAMPLING.md'),
    dict(id='boards', label='Scale study', description='Selected tweak-generator recipes and initial samples at several sizes.', target='Selected examples, not a probability sample. The tweak quality policy is separate from Mortal Coil legality.', report='../gallery/README.md'),
    dict(id='survey', label='Controlled survey', description='110 tweak-generator configurations, three shared seeds each, at 300×300.', target='Controlled recipe comparisons, not a uniform sample of boards.', report='../gallery/README.md'),
]
BY_COLLECTION = {c['id']: c for c in COLLECTIONS}


def assets(directory: Path, board: Path, solution: Path, detail: str = 'detail.png') -> dict:
    paths = {key: directory / filename for key, filename in [('map', 'map.png'), ('preview', 'preview.png'),
             ('detail', detail), ('stats', 'stats.json')]}
    paths.update(board=board, solution=solution)
    for key, filename in [('constructionCode', 'construction.code.json'), ('recipe', 'recipe.json'), ('solutionMap', 'solution-map.png')]:
        if (directory / filename).exists():
            paths[key] = directory / filename
    for path in paths.values():
        if not path.is_file():
            raise FileNotFoundError(path)
    return {key: '../' + path.relative_to(ROOT).as_posix() for key, path in paths.items()}


def sampling_boards() -> list[dict]:
    boards = []
    for collection in ('backward-v1', 'reversible-v1', 'deep-v1'):
        root = ROOT / 'gallery' / collection
        for entry in json.loads((root / 'manifest.json').read_text()):
            directory = (root / entry['stats']).parent
            saved = json.loads((directory / 'stats.json').read_text())
            identity = collection + '/' + entry['id']
            dynamics = saved.get('dynamics') or {}
            seed = entry.get('seedIndex', entry['id'].rsplit('-s', 1)[-1])
            label = entry['id'].split('/')[-1]
            boards.append(dict(saved, id=identity, collection=collection, phase='sampling',
                path='../' + directory.relative_to(ROOT).as_posix(), seed=seed,
                recipe=dict(id=label, family=BY_COLLECTION[collection]['label']), options=dynamics,
                detailLabel='Occupied-region crop · coordinates printed on image' if collection == 'backward-v1' else 'Full small board or central 128×128 crop · labeled',
                assets=assets(directory, root / entry['board'], root / entry['solution'])))
    draws = []
    for source_group in ('sampling-v1/uniform-4x4', 'sampling-v1/chain-4x4', 'deep-v1/uniform-5x5', 'deep-v1/uniform-6x6', 'deep-v1/uniform-7x7'):
        batch = json.loads((ROOT / 'gallery' / source_group / 'sampling.json').read_text())
        for sample in batch['samples']:
            directory = ROOT / 'gallery/sampling-viewer' / Path(source_group).name / f"sample-{sample['index']:04d}"
            saved = json.loads((directory / 'stats.json').read_text())
            if saved['samplingBatch'] != {k: v for k, v in batch.items() if k != 'samples'} or saved['sampleRecord'] != sample:
                raise ValueError(f'Stale imported sample metadata: {directory}; run prepare-sampling-gallery')
            draws.append((directory, saved))
    for directory, saved in draws:
        collection = directory.parent.name
        source = ROOT / saved['sourceBoard']
        links = assets(directory, source, ROOT / saved['sourceSolution'])
        for key, suffix in [('constructionCode', '.code.json'), ('recipe', '.recipe.json')]:
            if source.with_suffix(suffix).exists():
                links[key] = '../' + source.with_suffix(suffix).relative_to(ROOT).as_posix()
        links['samplingBatch'] = '../' + source.with_name('sampling.json').relative_to(ROOT).as_posix()
        boards.append(dict(saved, id=collection + '/' + directory.name, collection=collection, phase='sampling',
            path='../' + directory.relative_to(ROOT).as_posix(), seed=saved['sampleRecord']['index'],
            recipe=dict(id=BY_COLLECTION[collection]['label'], family='Board sampling'), options={},
            detailLabel='Full board enlarged · coordinates printed on image', assets=links))
    return boards
