from pathlib import Path
import subprocess,sys,zipfile,tempfile
source=Path(sys.argv[1]).resolve();root=Path(__file__).resolve().parent
patch=(root/'community-r18.patch').read_bytes().replace(b'\r\n',b'\n')
# Git for Windows checks out text as CRLF. Normalize only existing patch inputs.
for line in patch.splitlines():
 if line.startswith(b'--- a/'):
  p=source/line[len(b'--- a/'):].decode('utf-8')
  data=p.read_bytes();p.write_bytes(data.replace(b'\r\n',b'\n'))
with tempfile.TemporaryDirectory() as temp:
 normalized=Path(temp)/'community.patch';normalized.write_bytes(patch)
 subprocess.run(['git','-C',str(source),'apply','--check',str(normalized)],check=True)
 subprocess.run(['git','-C',str(source),'apply',str(normalized)],check=True)
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
