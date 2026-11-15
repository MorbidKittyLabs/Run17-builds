MORBID KITTY — RUN17 BUILD 18 COMMUNITY BRANCH
Built on NetNiV's STFC Community Mod (STFC-Mod/mod).
Windows x64 | One account | Morbid Kitty Labs

INSTALL
1. Open MorbidKitty-Setup.exe.
2. The setup window finds the game folder. If needed, click Browse and choose Prime.exe beside GameAssembly.dll.
3. Close STFC and click INSTALL.
Setup backs up the existing DLL, installs the new DLL and tools, preserves your existing configuration,
and creates the Morbid Kitty - Star Trek Fleet Command shortcut on your desktop.
If Windows requires permission for a protected game folder, setup offers Retry as admin.
You do not need to copy or switch DLL files yourself.

START THE GAME
Use the Morbid Kitty desktop shortcut. A three-second Morbid Kitty-and-Teddy intro appears once per
new game process, then closes. The normal Community Mod/game loading screens continue underneath.
The intro does not replace the Community Mod's artwork, tips, credits, or loading-screen implementation.
Existing graphics preferences remain in effect.

THE HUD PANEL
A compact horizontal strip uses the original blue gradient button colors.
It appears with the normal HUD in system/galaxy maps and station exterior/interior views, and hides
on other pages such as Events, building details and Claims. It starts near the gap below Claim/Help
and above Fleet Commander. Drag the clock to adjust its position; the position is saved relative to
the game window. Initial placement and individual menu transitions need live confirmation on your UI scale.
The clock and four named reminder timers continue while the strip is hidden.
The panel attaches to this game instance; there is no window-selection box.

BUTTONS AND SHORTCUTS
1–8: select one ship using the normal numeric ship shortcuts.
Store: official STFC web store in your default browser.
Microphone: Windows voice typing (Win+H). Select a text field in the game first.
Disk / F12: save supported live UI/camera settings to this account's configuration. Success is shown only after verified disk read-back.
Diag: collect diagnostic files into a ZIP on the Desktop. The ZIP is not sent anywhere.
Timers: four named reminders with Start/Pause/Reset, saved across restarts.
F6: one select-and-attack request. Another attack needs another press.
F7: recall eligible ships in this account. Busy ships may be skipped by the native game.
V / middle mouse: occupied mining-node view to cargo/treasure, then cargo/treasure and ship-strength views.
Native Scan/Attack controls remain available. Returning to the mining-node card from the ship view remains unresolved.

Custom shortcut settings are preserved. New feature defaults under [shortcuts] are:
action_select_attack_hostile = 'F6'
action_recall_all = 'F7'
save_settings = 'F12'
Optional companion settings under [ui]: community_panel_enabled = true; morbid_intro_enabled = true.

DIAGNOSTICS
Click Diagnostics on the HUD panel, or open Collect-Diagnostics.bat from the downloaded ZIP or the game's MorbidKitty folder to collect
matching logs and crash dumps into a ZIP on your Desktop. Nothing is sent automatically.
Read DIAGNOSTICS.md for the complete category/field list, file locations, collection behavior, privacy
information, limits and unverified readings. The supplied package contains no saved accounts, credentials,
personal configuration, collected game logs, Windows-user profiles or account-specific launcher arguments.
Logs are created from the recipient's own game. They can contain game identifiers, Windows usernames and paths.
Review diagnostic ZIPs before sharing. Crash dumps may contain incidental in-process data.

REMOVE
Use Morbid Kitty — STFC Community R18 in Windows Installed Apps, or open MorbidKitty-Uninstall.exe
in the game's MorbidKitty folder. Close STFC first. Removal restores the original backed-up DLL and
removes the installed shortcut and unchanged tool files. Personal configuration and logs are preserved.
Files that were modified after installation are preserved rather than overwritten.

BUILD AND SCANS
Compiled Windows DLL and tools, regression checks, installer rollback/update/removal checks and rendered
UI checks. Microsoft Defender and ClamAV reports and SHA-256 checksums are provided on the release page.
The build is unsigned. Clean scan results are not a guarantee that every antivirus will agree.
Live F6/F7, mining-node views, HUD placement/visibility, and camera restoration still need in-game verification.

SOURCE AND CREDITS
STFC-Mod/mod (NetNiV's STFC Community Mod), base commit 37e33084b711499c1ae3cc1988b58400c097546f.
https://github.com/STFC-Mod/mod
Transactional TOML save/editor helpers: Guffawaffle/stfc-mod, commit
2395ba5f6599fd06942beffe0ad0212b0151754f (proposed upstream in PR #280).
GPL-3.0; see LICENSE. Corresponding source is a separate release asset.
Morbid Kitty splash and desktop icon use the existing approved Morbid Kitty/Teddy/spaceship artwork.
