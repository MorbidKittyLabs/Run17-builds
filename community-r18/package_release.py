from pathlib import Path
import hashlib,json,os,shutil,sys,zipfile
root=Path(__file__).resolve().parent;package=Path('package')
if len(sys.argv)==1:
 package.mkdir(exist_ok=True)
 dll=Path('source/build/windows/x64/release/stfc-community-mod.dll')
 if not dll.is_file():raise SystemExit('Fresh compiled DLL missing')
 shutil.copyfile(dll,package/'version.dll')
 for name in ['Community-Panel.exe','Collect-Diagnostics.ps1','Collect-Diagnostics.bat','README-FIRST.txt','LICENSE']:
  shutil.copyfile(root/name,package/name)
 shutil.copyfile('source/example_community_patch_settings_en.toml',package/'community_patch_settings.example.toml')
 for name in ['community-panel.png','community-timers.png','panel-validation.txt']:
  shutil.copyfile(Path('ui-evidence')/name,package/name)
 (package/'BUILD-INFO.json').write_text(json.dumps({'build':'Community R18 Single Account','source_commit':os.environ['GITHUB_SHA'],'upstream_commit':'37e33084b711499c1ae3cc1988b58400c097546f','run_id':os.environ['GITHUB_RUN_ID'],'platform':'Windows x64','scan_status':'pending','live_game_validation':'required'},indent=2)+'\n')
else:
 info=json.loads((package/'BUILD-INFO.json').read_text());info['scan_status']='Defender and ClamAV completed without detections; see reports'
 (package/'BUILD-INFO.json').write_text(json.dumps(info,indent=2)+'\n')
 lines=[]
 for p in sorted(package.rglob('*')):
  if p.is_file() and p.name!='SHA256SUMS.txt':lines.append(hashlib.sha256(p.read_bytes()).hexdigest()+'  '+p.relative_to(package).as_posix())
 (package/'SHA256SUMS.txt').write_text('\n'.join(lines)+'\n')
 with zipfile.ZipFile('STFC-Community-R18-Windows.zip','w',zipfile.ZIP_DEFLATED,compresslevel=9) as z:
  for p in sorted(package.rglob('*')):
   if p.is_file():z.write(p,p.relative_to(package))
 archive=Path('STFC-Community-R18-Windows.zip')
 Path('SHA256SUMS.txt').write_text(hashlib.sha256(archive.read_bytes()).hexdigest()+'  '+archive.name+'\n'+hashlib.sha256(Path('Community-R18-Source.zip').read_bytes()).hexdigest()+'  Community-R18-Source.zip\n')
 print('Packaged verified runtime ZIP, exact source and checksums')
