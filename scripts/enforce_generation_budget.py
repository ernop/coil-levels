"""One deadline for one explicitly identified local generation process, with PID-reuse protection."""
import argparse
import json
import os
from pathlib import Path
import signal
import time
ROOT=Path(__file__).resolve().parents[1]
def main():
    p=argparse.ArgumentParser();p.add_argument('pid',type=int);p.add_argument('specimen');p.add_argument('seconds',type=int);a=p.parse_args()
    directory=(ROOT/'gallery/boards'/a.specimen).resolve()
    if a.pid<=1 or a.seconds<1 or directory.parent!=(ROOT/'gallery/boards').resolve():raise ValueError('Expected a positive budget and one specimen directory name')
    proc=Path(f'/proc/{a.pid}')
    def start():return int((proc/'stat').read_text().rsplit(')',1)[1].split()[19])
    original=start()
    command=(proc/'cmdline').read_bytes().split(b'\0')
    if str(directory).encode() not in command or b'specimen' not in command:raise RuntimeError('PID does not match the requested specimen')
    elapsed=time.clock_gettime(time.CLOCK_BOOTTIME)-original/os.sysconf('SC_CLK_TCK')
    time.sleep(max(0,a.seconds-elapsed))
    complete=(directory/'stats.json').exists()
    if not complete:
        if not proc.exists():raise RuntimeError('Generator exited without a completion marker; inspect its log')
        if start()!=original:raise RuntimeError('PID changed; refusing to signal')
        command=(proc/'cmdline').read_bytes().split(b'\0')
        if str(directory).encode() not in command:raise RuntimeError('Process command changed; refusing to signal')
        os.kill(a.pid,signal.SIGTERM)
    result=dict(id=a.specimen,status='complete' if complete else 'generation_budget_exceeded',budgetSeconds=a.seconds,
                note='Budget covers the entire specimen command, including generation, validation, and export. Completed specimens are replay-validated. A budget-exceeded attempt has no claimed board or geometry.')
    out=ROOT/'gallery/attempts';out.mkdir(exist_ok=True)
    (out/(a.specimen+'.json')).write_text(json.dumps(result,indent=2)+'\n')
    print(json.dumps(result),flush=True)
if __name__=='__main__':main()
