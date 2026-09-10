"""Import exact historical layouts; never regenerate them or invent missing solutions."""
import argparse
import gzip
import hashlib
import json
from pathlib import Path, PurePosixPath
import re
import subprocess
from urllib.parse import parse_qs
import zipfile

ROOT = Path(__file__).resolve().parents[1]
ARCHIVE_URL = 'https://github.com/adum/coilbench/releases/download/data-v1.0/boards.zip'
SOURCES = {
    'original-game': {'id': 'original-game', 'label': 'Original game', 'description': 'Mortal Coil · hacker.org', 'url': 'https://www.hacker.org/coil/'},
    'coil-levels-generator': {'id': 'coil-levels-generator', 'label': 'coil-levels generator', 'description': 'Boards created by this project', 'url': 'https://github.com/ernop/coil-levels'},
}


def save_board(identity: str, text: str, metadata: dict, solution: str | None = None) -> dict:
    fields = parse_qs(text)
    width, height = int(fields['x'][0]), int(fields['y'][0])
    cells = fields['board'][0]
    if len(cells) != width * height or set(cells) - set('.X') or '.' not in cells:
        raise ValueError(f'Invalid archived board: {identity}')
    directory = ROOT / 'gallery' / identity
    directory.mkdir(parents=True, exist_ok=True)
    packed = directory / 'level.board.gz'
    if packed.exists():
        if gzip.decompress(packed.read_bytes()).decode() != text:
            raise ValueError(f'Existing import differs: {identity}')
    else:
        packed.write_bytes(gzip.compress(text.encode(), compresslevel=1, mtime=0))
    if solution is not None:
        (directory / 'level.solution.gz').write_bytes(gzip.compress(solution.encode(), compresslevel=1, mtime=0))
    metadata.update(width=width, height=height, boardSha256=hashlib.sha256(text.encode()).hexdigest(),
                    solutionAvailable=solution is not None)
    (directory / 'import.json').write_text(json.dumps(metadata, indent=2) + '\n')
    return {'id': identity, 'path': directory.relative_to(ROOT).as_posix()}


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument('archive', type=Path)
    args = parser.parse_args()
    records = []
    archive_sha = hashlib.sha256(args.archive.read_bytes()).hexdigest()
    with zipfile.ZipFile(args.archive) as archive:
        names = sorted((n for n in archive.namelist() if PurePosixPath(n).name.isdigit()), key=lambda n: int(PurePosixPath(n).name))
        numbers = [int(PurePosixPath(n).name) for n in names]
        if numbers != list(range(1, 1209)):
            raise ValueError('Expected all 1208 numbered boards in the published data-v1.0 archive')
        for name, number in zip(names, numbers):
            text = archive.read(name).decode().strip()
            records.append(save_board(f'original-game/level-{number:04d}', text, {
                'schemaVersion': 1, 'title': f'Level {number}', 'generator': 'original-game',
                'source': SOURCES['original-game'], 'collection': 'original-game', 'levelNumber': number,
                'archiveUrl': ARCHIVE_URL, 'archiveSha256': archive_sha, 'archiveMember': name,
                'settingsStatus': 'The archive supplies the layout, not the original generation settings or a solution.',
            }))
    unique = {}
    names = subprocess.check_output(['git', 'ls-files', '-z', 'levels/*.coil'], cwd=ROOT).decode().split('\0')
    block_count = 0
    for name in filter(None, names):
        lines = (ROOT / name).read_text().splitlines()
        index = 0
        while index < len(lines):
            if not lines[index].strip():
                index += 1
                continue
            match = re.match(r'^(\d+)x(\d+)(?:\s|$)', lines[index])
            if not match:
                raise ValueError(f'{name}:{index + 1}: invalid board heading')
            width, height = map(int, match.groups())
            rows = lines[index + 1:index + height + 1]
            if len(rows) != height or any(len(row) != width or set(row) - set('.X') for row in rows):
                raise ValueError(f'{name}:{index + 1}: invalid board cells')
            text = f'x={width}&y={height}&board=' + ''.join(rows)
            digest = hashlib.sha256(text.encode()).hexdigest()
            seed = re.search(r'seed=(-?\d+)', name)
            occurrence = {'file': name, 'line': index + 1, 'heading': lines[index], 'seed': int(seed[1]) if seed else None}
            record = unique.setdefault(digest, {'text': text, 'occurrences': []})
            record['occurrences'].append(occurrence)
            block_count += 1
            index += height + 1
    for digest, record in unique.items():
        first = record['occurrences'][0]
        settings = sorted(set(re.sub(r'^\d+x\d+\s*-?\s*', '', o['heading']) for o in record['occurrences']))
        records.append(save_board('legacy-generator/' + digest[:20], record['text'], {
            'schemaVersion': 1, 'title': Path(first['file']).stem, 'generator': 'archived-tweak-generator',
            'source': SOURCES['coil-levels-generator'], 'collection': 'legacy-generator',
            'seed': first['seed'], 'archiveSettings': settings, 'sourceRecords': record['occurrences'],
            'settingsStatus': 'Settings are preserved exactly as written in the archived headings and filenames. Other settings and the solution were not saved.',
        }))
    for path in sorted((ROOT / 'levels/hard').glob('*.board')):
        records.append(save_board('saved-hard/' + path.stem, path.read_text().strip(), {
            'schemaVersion': 1, 'title': path.stem, 'generator': 'hardness-selected-generator',
            'source': SOURCES['coil-levels-generator'], 'collection': 'saved-hard',
            'sourceBoard': path.relative_to(ROOT).as_posix(), 'sourceSolution': path.with_suffix('.solution').relative_to(ROOT).as_posix(),
            'settingsStatus': 'An earlier generated board selected by solver effort; the saved board and solution are retained.',
        }, path.with_suffix('.solution').read_text().strip()))
    report = {'archiveUrl': ARCHIVE_URL, 'archiveSha256': archive_sha, 'originalGameBoards': len(numbers),
              'legacyFiles': len(list(filter(None, names))), 'legacyOccurrences': block_count,
              'legacyDistinctBoards': len(unique), 'savedHardBoards': 12, 'boards': records}
    (ROOT / 'gallery/imported-boards.json').write_text(json.dumps(report, indent=2) + '\n')
    print(f'Imported {len(numbers)} original game boards, {len(unique)} distinct legacy layouts from {block_count} occurrences, and 12 saved hard boards.')


if __name__ == '__main__':
    main()
