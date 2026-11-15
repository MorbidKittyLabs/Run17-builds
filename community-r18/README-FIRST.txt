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
The game opens borderless across the monitor, covering the taskbar area and removing the title bar,
minimize/maximize controls and resize border. F9 restores the original windowed game and desktop;
press F9 again to return to borderless play. Alt+Tab still switches applications normally.
The controller has a clear background, rounded double-bevel buttons and a boxed clock.
It starts below Claim/Help and above Fleet Commander. Right-drag any controller button or the clock
to move it; a right-drag never executes the button's normal action. Placement is saved relative to STFC.
The panel follows the normal HUD, stays available in full-screen chat and hides on Events, Claims and other detail pages. It attaches to this one game instance.

HIDE AND RESTORE
Red X beside Morbid Kitty: hides the controller without ending it or stopping timers.
F8: shows the controller again while STFC is active. The narrow green-arrow handle at the right edge
also stays available on the HUD: right-click that handle to restore the controller. Clicks elsewhere
inside the game keep their normal game behavior. Reopening the desktop shortcut also restores controls.
The red X beside Store closes only the separate Store window.
Purple X at the bottom closes this STFC game, its Store window and controller.
F10 retains the mod's game-quit action. F9 is reserved for borderless/windowed display while the
companion is enabled, instead of the mod's old F9 restart action.
The tray icon also offers Show controller, timers, diagnostics and Restore windowed game.

BUTTONS AND SHORTCUTS
Ship buttons: show only assigned docks the game currently reports, with original dock numbers and no
blank gaps. One mouse click selects and locates that exact ship through the mod; it does not simulate
a number-key press or require keyboard double-tap timing. Empty/unavailable dock buttons are hidden.
Store: opens the official STFC store in a separate Edge/Chrome app window, using its own persistent
local browser profile. Its X never targets normal browser windows; store sign-in is separate.
Microphone: opens side chat when needed, focuses the chat message field, then starts Windows voice typing (Win+H). It also works in full-screen chat; the controller remains visible there. Windows may take time to start voice typing.
Disk / F12: save supported live UI/camera settings. Success is shown only after verified disk read-back.
Diagnostics: collect diagnostic files into a ZIP on the Desktop. Nothing is sent automatically.
Timers: six named reminders, each with days/hours/minutes/seconds, Start/Pause and Reset. The timer
menu opens in the lower corner over the game; its own close button returns to normal play without
closing the controller. Existing four-timer names, durations and running countdowns are preserved.
F6: one select-and-attack request. Another attack needs another press.
F7: recall eligible ships in this account. Busy ships may be skipped by the native game.
V / middle mouse: occupied mining-node view to cargo/treasure, then cargo/treasure and ship-strength.
Native Scan/Attack controls remain available. Returning to the node card from the ship view remains unresolved.

Custom shortcut settings are preserved. New feature defaults under [shortcuts] are:
action_select_attack_hostile = 'F6'; action_recall_all = 'F7'; save_settings = 'F12'.
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
Live ship location, F6/F7, mining-node views, borderless taskbar coverage, HUD placement/visibility, and camera restoration still need in-game verification.

SOURCE AND CREDITS
STFC-Mod/mod (NetNiV's STFC Community Mod), base commit 37e33084b711499c1ae3cc1988b58400c097546f.
https://github.com/STFC-Mod/mod
Transactional TOML save/editor helpers: Guffawaffle/stfc-mod, commit
2395ba5f6599fd06942beffe0ad0212b0151754f (proposed upstream in PR #280).
GPL-3.0; see LICENSE. Corresponding source is a separate release asset.
Morbid Kitty splash and desktop icon use the existing approved Morbid Kitty/Teddy/spaceship artwork.
