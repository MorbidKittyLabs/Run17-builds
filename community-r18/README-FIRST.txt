STFC Community R18 — Single Account
Morbid Kitty Labs | Windows x64 | Community Mod extension

INSTALL
1. Close STFC.
2. Back up the existing version.dll beside Prime.exe.
3. Copy version.dll, Community-Panel.exe and Collect-Diagnostics.ps1 into that folder.
4. Open STFC normally. Open Community-Panel.exe when you want the optional panel.
The game also works without the panel. Closing the panel leaves STFC running.
Keep your existing community_patch_settings.toml. The included example is a reference.
Do not install the PDB or source archive to play.

MANUAL CONTROLS
F6: one select-and-attack request for the selected ship. A fresh press is required for another attack.
F7: recall eligible ships belonging to this account. Ships busy in combat/warp are skipped; there is no retry.
F12 / disk button: save this account's live UI, camera and supported settings, with a backup and verified read-back.
1–8 / panel buttons: select one ship in the chosen STFC window.
V / middle mouse: occupied mining-node card to cargo/treasure, then cargo/treasure and ship-strength views.
The native Scan/Attack controls remain available. The return from the ship view to the mining-node card is still unresolved in this R18 baseline.

PANEL
Clock: local time/date; drag the clock or title bar to move the panel.
Timers: four named reminder timers with Start/Pause/Reset, saved across restarts. Timers only remind you.
Store: official STFC web store in your normal resizable browser.
Microphone: Windows voice typing (Win+H). Select a chat/text field in STFC first.
Disk: save and show confirmation only after the selected game's DLL verifies the file.
Diag: collect logs from the selected STFC installation into a Desktop ZIP. Nothing is sent automatically.
Choose the game window in the panel. No keys are broadcast to other game windows.

If an existing TOML has different bindings, put these in its existing [shortcuts] section:
action_select_attack_hostile = 'F6'
action_recall_all = 'F7'
save_settings = 'F12'
The panel's 1–8 buttons use the standard numeric ship bindings.

DIAGNOSTICS
Patch installation and errors; crash reports; occupied-miner viewer/context transitions; hostile identities,
movement, targeting and attack-handler evidence; pursuit observations; all observed docks' hull/shield,
cargo, mining and timing readings; action queue observations; named screen/canvas/focus and station-card readings.
Readers preserve unknown/unavailable values. Live game state and client updates can affect availability.
Diagnostic archives can contain game identifiers, Windows usernames, paths, and crash data. Review before sharing.

VALIDATION
This package is compiled from a pinned Community Mod base with the included source changes.
Microsoft Defender and ClamAV reports and SHA256SUMS.txt accompany the release.
Passing scans means no detections in those scans; it does not guarantee every antivirus will agree.
The package is unsigned. No antivirus exclusions or disabled protections are needed by this installer.
Automated checks cover input-request limits, reader policies, settings persistence and panel rendering.
Live F6/F7, occupied-node views, voice typing and camera restoration still need in-game verification.

SOURCE AND LICENSE
Based on STFC-Mod/mod, commit 37e33084b711499c1ae3cc1988b58400c097546f.
https://github.com/STFC-Mod/mod
GPL-3.0; see LICENSE. Corresponding clean source is supplied as a separate release asset.
Public download page: https://github.com/MorbidKittyLabs/Run17-builds/releases
