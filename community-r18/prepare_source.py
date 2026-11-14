from pathlib import Path
import subprocess,sys,zipfile
source=Path(sys.argv[1]).resolve();root=Path(__file__).resolve().parent
subprocess.run(['git','-C',str(source),'apply','--check',str(root/'community-r18.patch')],check=True)
subprocess.run(['git','-C',str(source),'apply',str(root/'community-r18.patch')],check=True)
subprocess.run(['git','-C',str(source),'diff','--check'],check=True)
subprocess.run([sys.executable,str(root/'verify_export.py'),str(source/'mods/src')],check=True)
# Snapshot before building, excluding checkout metadata and generated compiler output.
archive=Path('Community-R18-Source.zip')
with zipfile.ZipFile(archive,'w',zipfile.ZIP_DEFLATED,compresslevel=7) as z:
 for p in source.rglob('*'):
  if p.is_file() and '.git' not in p.relative_to(source).parts:z.write(p,Path('STFC-Community-R18')/p.relative_to(source))
 for p in root.rglob('*'):
  if p.is_file():z.write(p,Path('export-tools')/p.relative_to(root))
print('Prepared exact clean source snapshot')
