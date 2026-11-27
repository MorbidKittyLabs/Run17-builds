OCCUPIED MINING NODE - ISOLATED WINDOWS TEST

1. Run OccupiedNode-Test-Setup.exe.
2. Browse to the existing STFC folder containing prime.exe.
3. Click Install isolated test. A separate game copy is created in:
   %LOCALAPPDATA%\MorbidKittyLabs\OccupiedNode-Test
   Allow disk space for another game copy. The original folder is never edited.
4. Click Launch test game, then use one account for the live test.

On an occupied mining node press V (ActionView) three separate times:
  node -> native loot -> native ship strength -> the original node.
Repeat the cycle. No Scan screen or system reentry should be needed.

Also check an empty node, a changed occupant, an unrelated selection and a
system change. Verify normal native Attack and Scan availability. Never attack
unless the currently displayed target is the one you intended.

The setup copies only native game files, not source-folder mod settings or
controllers. STFC's own machine/account login storage may still be shared.
Close the test game before installing again or using Roll back test DLL.
Rollback changes only the separate test copy. Open the same setup executable
to return to the install/launch/rollback buttons.

This test contains the upstream community mod plus the inspection feature.
No DSE automation, repairs, hostile changes or custom controller are included.

Compile/automated tests do not establish live game compatibility. Upstream
PR #382 must remain unchanged until the live acceptance checks succeed.
Diagnostics are in the test copy's community_patch.log ([OccupiedNode] lines).
