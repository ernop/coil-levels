"""Verify archive completeness and fidelity against the original source files."""
import gzip
import hashlib
import json
from pathlib import Path
import subprocess
from urllib.parse import parse_qs
import zipfile
import sys

ROOT = Path(__file__).resolve().parents[1]
report = json.loads((ROOT / 'gallery/imported-boards.json').read_text())
archive_path = Path(sys.argv[1])
assert hashlib.sha256(archive_path.read_bytes()).hexdigest() == report['archiveSha256']
occurrences = {}
with zipfile.ZipFile(archive_path) as archive:
    for entry in report['boards']:
        directory = ROOT / entry['path']
        meta = json.loads((directory / 'stats.json').read_text())
        text = gzip.decompress((directory / 'level.board.gz').read_bytes()).decode()
        assert hashlib.sha256(text.encode()).hexdigest() == meta['boardSha256']
        if meta['collection'] == 'original-game':
            assert text == archive.read(meta['archiveMember']).decode().strip(), entry['id']
        elif meta['collection'] == 'legacy-generator':
            cells = parse_qs(text)['board'][0]
            for record in meta['sourceRecords']:
                occurrences.setdefault(record['file'], []).append((record, meta['width'], meta['height'], cells))
        else:
            assert text == (ROOT / meta['sourceBoard']).read_text().strip()
            assert gzip.decompress((directory / 'level.solution.gz').read_bytes()).decode() == (ROOT / meta['sourceSolution']).read_text().strip()
tracked = set(filter(None, subprocess.check_output(['git', 'ls-files', '-z', 'levels/*.coil'], cwd=ROOT).decode().split('\0')))
assert set(occurrences) == tracked
for filename, records in occurrences.items():
    lines = (ROOT / filename).read_text().splitlines()
    covered = set()
    for record, width, height, cells in records:
        start = record['line'] - 1
        assert lines[start] == record['heading']
        assert ''.join(lines[start+1:start+height+1]) == cells, (filename, start)
        covered.update(range(start, start+height+1))
    assert all(i in covered or not line.strip() for i, line in enumerate(lines)), filename
assert sum(map(len, occurrences.values())) == report['legacyOccurrences']
print(f'Verified all {report["originalGameBoards"]} original game levels byte-for-byte and every one of {report["legacyOccurrences"]} historical board occurrences across {len(tracked)} files; no omitted boards or altered cells.')
