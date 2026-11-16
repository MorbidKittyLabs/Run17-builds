# Run17 R18 public input repair

Joseph requested repair of the single-account test build, not recovery or
changes to his separate four-account setup. Public release must exclude
gameplay automation, including the formerly hidden held-F6 repeat path.

Code commit: 55b95560f65a16334b629bdcd3b2d2ae2d4411a8
Successful Windows/policy/Defender/ClamAV run: 43839934987
Publish job: skipped. Original failed R18 remains blocked for public release.

Changes:
- Overlay GameButtons do not select/focus their parent form when clicked.
- Explicit non-activating mouse handling and process DPI awareness.
- Small borderless forms explicitly permit sizes below Windows default limits.
- Unified controller right-drag state/capture with a narrow gap-only hook.
- Timer dragging uses local mouse handlers. Scaled timer windows are positioned
  after layout and clamped to the game/monitor bounds so the X stays reachable.
- Controller uses a two-thirds size baseline, corrected red Timers hide X and
  purple Diagnostics game-close X, and cached placement/rounded scale updates.
- Full-screen game notification through ITaskbarList2; normal-window restore kept.
- Dock readers select a rendered fleet bar, recognize ShipBarItem controllers
  and lowercase fleet properties, and try a bounded dock ancestor for bounds.
- Dock badges move to the lower corner above health bars.
- F6 accepts one explicit press for one action; no held repeat implementation.

Verification:
- Original focus loss was reproduced with Windows SendInput, then the repaired
  buttons preserved game focus in the same fixture.
- Final physical input fixture passes controller buttons, timer editor X,
  reminder dismissal, dock shortcut click, game click-through, hide/right-drag,
  capture release at 100/125/150-percent game UI scales.
- Native XMake Windows x64 build, policy tests, installer update/rollback/settings
  checks, export audit and updated Defender/ClamAV scans passed.
- SHA-256 checks of all downloaded package files passed. No personal account-name
  markers or actual saved game/panel configuration files were found in the package.

Delivered installer-only ZIP (one top-level executable, unchanged CI setup bytes):
MorbidKitty-Run17-R18-Input-Fix-Setup.zip, 13255980 bytes
SHA256 a4b3e63f9e40ac999e144ef3a1e526fce8b6b11cac1c708d43ed15269c2818cd
Setup SHA256 d508bb6adfb0f16dc6eda53791a47cdd96fbe168a01a61c23062a42a908cceed
DLL SHA256 89ba1ba949853720057a4fad2ac372985886c3fa28b3153c01e55ae6246775e9

Live STFC input, taskbar behavior and actual dock discovery/positions remain
unverified. The fixture does not establish live-game compatibility. Do not lift
the release block or claim all earlier requested features are complete.
Four-account emergency recall/rally and native Unity controller migration are
not implemented in this candidate.

If dock anchoring still fails live, inspect the actual ShipBar hierarchy and
prototype one native manually clicked dock button before migrating the controller.
See PUBLIC-UI-RESEARCH.md for the reviewed non-automation options.
