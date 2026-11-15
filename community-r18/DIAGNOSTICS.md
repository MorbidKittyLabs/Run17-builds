# What the diagnostic tools can record

This document describes the readers included in this build. A reader being installed does not prove that every value is available or correct in every client version. Unavailable values remain null/unknown; they are not converted into success, a percentage, or a missing ship.

## Running the troubleshooting collector

Click **Diag** on the HUD panel, or open `Collect-Diagnostics.bat` in the downloaded package or the installed game's `MorbidKitty` folder. It creates a diagnostic ZIP on your Desktop for you to review and share yourself. The installer's **README / Diagnostics** button opens this documentation; it does not collect or upload data.

The readers and their corresponding source are included for troubleshooting and development. Inclusion does not establish approval from Scopely or the Community Mod maintainers.

## Startup, build identity and patch status

- Mod/file version, source/base revision, build identity and process ID.
- Loaded DLL path, configuration path, main-log path and diagnostic-log path.
- Patch name and enabled/disabled status; entry and return from each installer.
- Warnings/errors seen during installation; exception/failure status; installation summary counts.
- General mod warnings/errors captured from the existing logger.
- These records show that an installer returned. They do not prove its hooks work in the live client.

## Windows crashes

- A best-effort `MiniDumpNormal` dump on an unhandled exception, with process/thread/exception context and loaded-module information.
- The dump filename contains the process ID and timestamp. The previous exception filter is chained.
- This is not a full-memory snapshot or a guarantee of capturing every freeze/crash. Stack memory, paths and other incidental process data may still be present.

## Occupied mining-node presentation

- Viewer pointers and visibility states; occupation state; tracked occupying-fleet ID.
- Mining/ship battle-context and target IDs; Scan/Attack-widget presence/enabled state.
- Cargo/reward panel visibility; pending information-view state.
- Before/after records for the manual V/middle-mouse action and change-driven viewer/context records.
- It can help explain cargo/strength transitions and available native controls. It does not prove the unresolved return-to-node presentation works.

## Hostile and selected-target identity

- Observed target/fleet IDs, names and hull names; levels and strength when exposed by the client.
- Hull, hull-spec, hull-string, reference, icon and deployment identifiers.
- Fleet/hull type values and enum labels; native elite/scout flags if available.
- Whitelisted deployment identity/classification scalars and bounded field/property-name inventories.
- Text category hints are recorded as hints, not fabricated native classification flags.

## Positions, motion and manual engagement evidence

- Visible fleet IDs and model system positions; motion history, age and moving/stationary/unknown status.
- Selected own fleet/dock, current/previous state and selected target.
- Distances, eligibility/exclusion/cooldown reasons and bounded candidate rankings.
- Verified elite-priority evidence; observed impulse-speed estimates and straight-line interception estimates, with a nearest-target fallback.
- Manual F6 request, selection attempt, matching target-card/button checks, handler entry/dispatch and later observed state/outcome evidence.
- An Attack-handler call is not treated as server acceptance, successful combat, a kill, or target arrival.
- Predicted time-to-target depends on observed motion and a constant-velocity assumption. It is an estimate with a bounded horizon, not an authoritative game ETA.

## Pursuit and course observations

- Own fleet/dock/system IDs, state, observed system position, native pursued-by count and pursuit-target ID when readable.
- Active-course fields: recall-course, in-use, cancel-warp eligibility, warp charging, done and course type.
- Visible linked fleets' pursuit-target/count fields, position, course-vector components, battle/state/type/reference values.
- Link acquired/cleared, course-change, lost-visibility/context-reset and stopped/clear candidate observations.
- Missing data or a fleet leaving visibility is recorded as unknown. It is not proof that pursuit ended or that a maneuver succeeded.
- Values come from named client-model members. Field meaning and server freshness still need live verification.

## Own-account docks, health, cargo, mining and timers

- Up to 16 bounded observed dock slots; exact fleet IDs, dock index/number, current state and whether selected.
- Roster source, enumeration/lookup status, sampled times, ages, partial-fallback and detail-availability flags.
- Current/max hull and shield values through typed native stats; percentages only when current/max are valid.
- Shield-regeneration duration stat as raw data; semantics/units may need verification.
- Current active-course remaining time; native repair-cost model duration/remaining time when exposed.
- Cargo fill fraction/percent, current amount, protected limit and over-protected status when valid.
- Mining active/model-observed status, resource ID, amounts and speed as exposed by the client. Unverified units/meanings are explicitly labeled.
- Active native timer remaining seconds, previous state and sample freshness.
- Missing dock data does not establish whether a dock is locked, unlocked or nonexistent.

## Native action-queue observations

- Whether a queue manager was found and how it was resolved.
- Enabled/unlocked status, capacity, per-fleet queued count and full status when supported.
- Queue/combat conditions used to explain why a manual request was paused or rejected.
- No server-side queue history is available through these readers.

## Screens, focus, HUD and station cards

- Current section ID and known section name; foreground/text-input focus.
- Top-canvas availability, name/type and visible flag; UI transitions and selected fleet ID.
- The panel's read-only HUD signal: process creation identity, process ID, sample time, section, observed/active state and panel visibility. It is temporary shared memory, not a gameplay-command channel.
- Visible native station/outpost card and button types; selected own fleet; native engage enabled/interactable flags.
- Whitelisted station/outpost/player/target/system/address/position/eligibility/shield/destroyed/ownership values, plus bounded metadata inventories.
- Manual/native station Engage-handler entry. Station identity semantics still need live validation; entry is not proof of an accepted attack.

## Manual recall and settings saving

- F7 per-fleet native recall-request result for eligible own fleets. This is request evidence, not a promise that all ships reached home.
- Settings-save readiness, supported setting count, camera observations/restoration, backup/commit/read-back result and save errors.
- Graphics/camera values and configuration paths can appear in the logs. The collector does not intentionally copy the TOML configuration file itself.

## Files, retention and collection

- `community_patch.log` and matching mod logs: normal upstream logging/rotation rules.
- `*_collective_diagnostic.log`: startup/patch/warning records.
- `Run17OccupiedMinerDiag.log`: occupied-node viewer/context transitions.
- `*_hostile_action*.log`: bounded rotating target/motion/engagement/pursuit records (8 MiB per file, two rotated files).
- `*_fleet_diagnostics*.log`: bounded rotating dock/UI/station records (4 MiB per file, three rotated files); bounded asynchronous queue; overrun/oversize/error indicators.
- `*_collective_crash_*.dmp`: best-effort crash dumps.
- Collection checks the selected game installation and its `stfc-mod` log tree, copies matching logs/dumps, records the installed DLL's description/version/identity/SHA-256 and selected game directory, and writes `COLLECTION-INFO.txt`.
- Rotated matching target/fleet/diagnostic logs are included. Read/copy failures are noted. Existing logs can include records made before this package was installed.
- The collector creates `STFC-Community-Diagnostics-<date-time>.zip` on the recipient's Desktop. It does not send/upload the ZIP, modify settings, change antivirus exclusions, or collect browser profiles.

## Personal information and practical limits

The distributed installer contains code, default examples, documentation and branding—not the author's saved accounts, credentials, configuration, collected logs or Windows profiles.

Generated diagnostics can contain **the recipient's** Windows username, game/account/player/fleet/target/system identifiers, paths, native object addresses, timestamps, graphics settings and gameplay activity. Existing upstream log content may contain additional incidental data. Crash dumps can contain sensitive in-process fragments. Review the archive and remove material you do not want to share.

These readers do not provide a complete view of every game event, every hostile, all inventory, chat history, accounts on other machines, account credentials, or authoritative server state. They report only the named client data and matching files described above. A null, stale, partial, missing or unverified value must remain labeled that way.
