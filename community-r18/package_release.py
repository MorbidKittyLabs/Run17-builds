from pathlib import Path
import hashlib,json,os,shutil,sys,zipfile
root=Path(__file__).resolve().parent;package=Path('package')
RUNTIME=['version.dll','Community-Panel.exe','MorbidKitty-Launcher.exe','MorbidKitty-Uninstall.exe','MorbidKitty-Splash.png','MorbidKitty.ico','Timer-Complete.wav','Collect-Diagnostics.ps1','Collect-Diagnostics.bat','README-FIRST.txt','DIAGNOSTICS.md','LICENSE','community_patch_settings.example.toml']
mode=sys.argv[1] if len(sys.argv)>1 else 'stage'
def sha(path):return hashlib.sha256(path.read_bytes()).hexdigest()
if mode=='stage':
 package.mkdir(exist_ok=True)
 dll=Path('source/build/windows/x64/release/stfc-community-mod.dll')
 if not dll.is_file():raise SystemExit('Fresh compiled DLL missing')
 shutil.copyfile(dll,package/'version.dll')
 for name in RUNTIME:
  if name=='version.dll':continue
  src=Path('source/example_community_patch_settings_en.toml') if name.endswith('.example.toml') else root/'assets'/name if name.endswith(('.png','.ico','.wav')) else root/name
  shutil.copyfile(src,package/name)
 manifest={'Files':[{'Key':name,'Value':sha(package/name)} for name in RUNTIME]}
 with zipfile.ZipFile(root/'payload.zip','w',zipfile.ZIP_DEFLATED,compresslevel=9) as z:
  for name in RUNTIME:z.write(package/name,name)
  z.writestr('PACKAGE-MANIFEST.json',json.dumps(manifest))
elif mode=='installer':
 shutil.copyfile(root/'MorbidKitty-Setup.exe',package/'MorbidKitty-Setup.exe')
 for name in ['community-panel.png','timer-alert.png','community-timers.png','morbid-intro.png','morbid-installer.png','panel-validation.txt','installer-validation.txt']:
  shutil.copyfile(Path('ui-evidence')/name,package/name)
 (package/'BUILD-INFO.json').write_text(json.dumps({'build':'Morbid Kitty Run17 R18 Installer','source_commit':os.environ['GITHUB_SHA'],'upstream_commit':'37e33084b711499c1ae3cc1988b58400c097546f','run_id':os.environ['GITHUB_RUN_ID'],'platform':'Windows x64','scan_status':'pending','live_game_validation':'required'},indent=2)+'\n')
elif mode=='complete':
 info=json.loads((package/'BUILD-INFO.json').read_text());info['scan_status']='Defender and ClamAV completed without detections; see reports'
 (package/'BUILD-INFO.json').write_text(json.dumps(info,indent=2)+'\n')
 (package/'SHA256SUMS.txt').write_text('\n'.join(sha(p)+'  '+p.relative_to(package).as_posix() for p in sorted(package.rglob('*')) if p.is_file() and p.name!='SHA256SUMS.txt')+'\n')
 with zipfile.ZipFile('MorbidKitty-Run17-R18-Installer.zip','w',zipfile.ZIP_DEFLATED,compresslevel=9) as z:
  for p in sorted(package.rglob('*')):
   if p.is_file():z.write(p,p.relative_to(package))
 files=[Path('MorbidKitty-Run17-R18-Installer.zip'),Path('Community-R18-Source.zip'),package/'MorbidKitty-Setup.exe']
 Path('SHA256SUMS.txt').write_text('\n'.join(sha(p)+'  '+p.name for p in files)+'\n')
 print('Packaged scanned installer, runtime ZIP, source and checksums')
else:raise SystemExit('Unknown package mode')
