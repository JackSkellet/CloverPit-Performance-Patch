#!/usr/bin/env python3
"""Package an already-published self-contained installer, not game files."""
from pathlib import Path
import argparse,hashlib,zipfile
p=argparse.ArgumentParser();p.add_argument('rid',choices=['linux-x64','win-x64']);p.add_argument('publish_dir',type=Path);p.add_argument('output_dir',type=Path);a=p.parse_args()
root=Path(__file__).resolve().parents[1];exe=a.publish_dir/('CloverPitPatch.exe' if a.rid=='win-x64' else 'CloverPitPatch');assert exe.is_file();a.output_dir.mkdir(parents=True,exist_ok=True)
archive=a.output_dir/f'CloverPit-Performance-Patch-v2.0.0-{a.rid}.zip'
with zipfile.ZipFile(archive,'w',zipfile.ZIP_DEFLATED) as z:
    z.write(exe,exe.name)
    for name in ['README.md','VALIDATION.md','LICENSE','THIRD-PARTY-NOTICES.md']:z.write(root/name,name)
    for f in (root/'third-party').iterdir():z.write(f,'third-party/'+f.name)
archive.with_suffix('.zip.sha256').write_text(hashlib.sha256(archive.read_bytes()).hexdigest()+'  '+archive.name+'\n')
print(archive)
