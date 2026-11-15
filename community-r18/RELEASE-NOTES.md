## Controller correction

- Controller remains available in full-screen chat; mic opens/focuses chat before invoking Windows voice typing. Detail-page visibility refreshes more promptly.

- Clear controller background; rounded double bevels and a boxed clock.
- Full-monitor borderless play by default; F9 restores the desktop/windowed game, F10 quits.
- F8 and right-clicking the right-edge handle restore hidden controls. Reopening the desktop shortcut also restores them.
- Right-drag any control to move the menu. Red X hides it, Store X closes only Store, purple X closes the game.
- Only assigned dock buttons are shown, packed with their actual dock numbers. Each click directly selects and locates that ship.
- Six named corner-popup timers with days/hours/minutes/seconds, preserving existing four-timer state.

**Download `MorbidKitty-Setup.exe`, open it, and click INSTALL.** No manual DLL swapping is needed.

The installer finds the game or lets you select `Prime.exe`, backs up the previous DLL, preserves existing settings, installs the launcher/diagnostic tools, and creates the Morbid Kitty desktop shortcut. Removal restores the backed-up DLL and preserves personal settings/logs.

- Existing Morbid Kitty/Teddy/spaceship artwork and desktop logo; one three-second intro per new game process, then normal loading continues.
- Vertical blue HUD panel: clock, four saved reminders, Store, Windows voice typing, verified Save, Diagnostics and ship buttons 1–8.
- Panel follows map/base HUD visibility and remembers its moved position.
- F6: one select-and-attack request. F7: eligible own ships recall. F12: save local settings.
- Diagnostic tools and source remain included for troubleshooting/development. Click Diagnostics on the panel or run `Collect-Diagnostics.bat`; nothing is uploaded.
- README and DIAGNOSTICS.md document the features, every diagnostic category, unavailable/unverified values, collection scope and privacy considerations.
- Occupied-node cargo/treasure and strength views retain native Scan/Attack. **Returning to the mining-node card remains unresolved.**

Compiled tools and DLL; installer rollback/update/removal and shortcut checks; rendered UI checks; Microsoft Defender and ClamAV reports; SHA-256 checksums. This is an unsigned test release, and live game/HUD verification is still needed. Clean scans do not guarantee every antivirus will agree.

Based on [NetNiV's STFC Community Mod](https://github.com/STFC-Mod/mod) at `37e33084b711499c1ae3cc1988b58400c097546f` (GPL-3.0). Corresponding source is provided in `Community-R18-Source.zip`.
