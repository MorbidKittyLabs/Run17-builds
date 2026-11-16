# Public Run17 R18: Borg.Box findings

Reviewed 2026-11-16. This review does not install or enable any external mod.

| Component | Useful part | Decision |
| --- | --- | --- |
| Transformers 1.5.0 | Native RectTransform anchors, menu size and viewer scale changes | Use as a reference for native UI work; GPL-3.0 public source is available. |
| UnityExplorer 4.13.6 | Inspect live Unity hierarchy, components and layout | Development diagnostic only; leave it out of the public installer. |
| Borg.Box | Signed packages, manifest file lists, catalog versions, custom sources | Optional distribution path after live validation. Its type-1 package supports native client-root patches, so our current DLL does not require a BepInEx rewrite just for packaging. |
| BepInEx + configuration manager | C# plugin development and manually edited in-game settings | Alternative runtime implementation to evaluate in the test client; not installed by this repair. |
| Miner / Berserker / Ratchet / Cybertron | Evidence that other plugins attach controls inside native game UI | Do not bundle their automation or network dependencies. Their complete gameplay plugins are excluded. |
| Optimus.STFC.API | Shared game-server/session and protocol layer | Not needed for clock, timers, controller or manual dock shortcuts; excluded. |

The public candidate removes held-F6 repeat behavior. F6 accepts one explicit
press for one target/attack request; ordinary subsequent frames only complete
that same request. No automatic hunting, mining, repairing, shielding, exchanges,
or repeated gameplay actions are added.

Transformers explicitly disables its overall UI scaling and extended zoom by
default to avoid collisions with Community Patch Mod hooks. We should keep one
owner for those hooks instead of installing both implementations.

Miner's downloaded assembly includes ShipBar_Canvas, ActionPanelContainer,
BottomToggleButton, Instantiate, RectTransform and Button_Press references.
These are useful implementation leads, not proof of compatibility with our
current game build. Native integration still needs a live hierarchy inspection.

Recommended next native experiment: one manually clicked dock-number control,
parented to the corresponding real dock, with native scale/visibility and a
single select/locate action. Verify it in the test client before migrating the
whole controller. Do not ship a general-purpose runtime inspector.

Sources:
- https://github.com/Plurimus/Borg.Box
- https://github.com/Plurimus/stfc-mod-source
- https://github.com/Plurimus/Optimus.STFC.Transformers
- https://github.com/yukieiji/UnityExplorer
- Existing BorgBox-DSE-Research-Pack.zip, catalog and bundled package READMEs.
