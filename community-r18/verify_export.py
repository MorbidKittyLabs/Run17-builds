"""Fail closed if a private controller/attack-loop component enters an export."""
from pathlib import Path
import sys
FORBIDDEN=(b'DSE.Background',b'DSE-STFC',b'MorbidKitty.DSE',b'background_control::',
 b'controller_session',b'lease_until_tick_ms',b'PressDue(',b'owner_settings',b'minimized_f6_probe',b'CreateNamedPipeW')
root=Path(sys.argv[1]);binary='--binary' in sys.argv
paths=list(root.rglob('*'))
checked=0
for p in paths:
 if not p.is_file() or '.git' in p.parts:continue
 if binary and p.suffix.lower() not in ('.dll','.exe'):continue
 if not binary and (p.suffix not in ('.cc','.h','.inc','.toml') or 'third_party' in p.parts):continue
 data=p.read_bytes()
 for word in FORBIDDEN:
  if word in data or word.decode().encode('utf-16-le') in data:raise SystemExit(f'Excluded component marker in {p}: {word.decode()}')
 checked+=1
if not checked:raise SystemExit('Nothing audited')
print(f'PASS: {checked} {"binaries" if binary else "source/config files"} contain no excluded controller/loop markers')
