# r34 independent presentation review preparation

The supervisor contract `OWN-BASE-15-001-SUPERVISOR-R1` is suitable for independent candidate preparation. No material contract blocker was found. This accepts a **nominal finite own-actor presentation policy**, not native neutral eligibility or timing parity. The eligibility package's 154 artifact hashes are independently verified; its full private-flag research and 44 external pins are not repeated. Strict native-neutral qualification remains zero. The supervisor's r33 binding and active-window clarification supersede the source package's r32 baseline and `OwnCombat==null` proposal.

The immutable r33 stage has 177 independently checked source pins and exact product/audio DLL hashes. The helper references the original product DLL without rebuilding it. No in-progress canonical engine or author candidate was inspected. Later final-r33 release binding is a separate pending pin-only step.

## Existing event and rendering route

Pinned `LoginConnection.cs:61-62` invokes `PacketReceived` before `ApplyWorldPacket`. That observation event precedes parsing and heading changes, so it is not a safe replacement for the requested post-parse receipt hook. `WorldEntry.cs:44` passes bytes to public `WorldSession.Receive` and resets item/ground send ownership when actor/world changes. The helper invokes this private transport-independent dispatch method through reflection; it does not construct a socket or run the network loop, and does not claim the pre-parse event was exercised.

`WorldObjects.cs:50` parses S158, handles the special door branch, then routes action15 to `Ground.cs:33` and returns. `ReceivePickupAction` increments the global counter for own, remote and unknown IDs, updates own pending heading only for matching actor/epoch/position, clears that own pending association, and updates the ground notice. It sets no pickup animation state, does not put15 into `OwnCombat`, and does not assign remote `ReceivedAction=15`. Polling the global counter would lose receipt identity and cannot meet the new contract.

`ClientLogin.cs:12-27` reconciles appearance/world/correction changes, stops the presentation model when necessary, and synchronizes own position/direction. `Program.cs:109` advances the model then runs the online synchronization and refresh route. `ObjectRendering.cs:5-16` selects own walk/breath, prepares projectile/magic effects, then overlays the currently visible combat or projectile hit. `RefreshSceneImage` and `RefreshObjects` ultimately use `ComposeObjects` and `Renderer.BitmapOf` before `OnPaint` draws the bitmap. No existing public pickup presentation hook was found.

The helper creates **no ClientForm, Control, window, Application.Run or normal engine entrypoint**. Its empty resident scene is a synthetic Assets object populated through explicit reflection; original actor/shadow/catalog/opacity data are selectively copied and pinned. It calls the real sealed DLL's compositor and BitmapOf, saves PNGs and compares actual RGBA bytes. This demonstrates an offscreen testing route, not GUI timer, camera, full-map occlusion or live gameplay acceptance. Constructor/integrity validation of the synthetic Assets object is deliberately bypassed; actual profile/catalog integrity checks still run.

## Active-window boundary that the candidate must preserve

The original compositor checks `elapsed < (Timing.AnimationMs ?? appearance.Duration(action))` and allows death independently of elapsed time. `ObjectVisual.StepMs` can truncate that duration. The successful probe observes:

| Action | Duration | Last visible integer age | First expired integer age |
|---|---:|---:|---:|
| attack1 | 800 | 799 | 800 |
| damage2 | 583.3333333333334 | 583 | 584 |
| spell18 | 1374 | 1373 | 1374 |
| spell19 | 1539 | 1538 | 1539 |

The original `OwnCombat` report survives each expiry and each subsequent S158/action15. The r34 guard must use the existing displayed-window semantics without clearing that report or shortening a cooldown. Using truncated StepMs583 for damage would admit pickup one clock unit too early. An initial helper assertion made this truncation error; its failed receipt remains in baseline-01 and is excluded from accepted totals.

`ProjectileRendering.cs:28` may select a newer projectile hit even when OwnCombat is null. The helper directly seeds that private cache to exercise visible damage/expired damage/death priority through the real compositor. This is explicitly a synthetic hit fixture, not a test of projectile packet decoding, flight or impact creation. The candidate must cover both receipt-time and presentation-time priority checks; OwnCombat alone is insufficient.

## Baseline observations and immutable behavior

The final run contains 84 rows:32 reference-renderer pixel assertions,16 active-window assertions,5 existing-behavior assertions,17 feature-gap observations and14 state observations. It contains243 synthetic packet occurrences; packet count is not test-pass count. All31 observations have `passed=null`; no names are deduplicated to invent coverage. Earlier development runs are retained but excluded from the totals.

Own S158/action15 at both speeds and all8 directions increments the receipt counter while leaving the actual baseline bitmap unchanged. Pending C112 without S158 also leaves the bitmap unchanged. This is the expected r33 missing-feature observation, never a candidate pass. Remote/unknown, alternate gfx/weapon, unsupported Speed/Brave, death/shop/motion, S76 pending appearance, correction, speed and world/session transitions are separately recorded. Some unsupported appearances are state-only observations because the fixture intentionally supplies only the base profile.

Memory-only send-gate admission produces exact C112 bytes `70507FA380C800000001000000000000` for x32592,y32931,object200,count1. The actual assigned guard and pending deadlines remain900/3000ms relative to the call's clock bracket; this is not a wall-time/server-latency assertion. Immediate repeat is refused. Unknown receipt preserves pending; own receipt resolves heading3 while ground and inventory counts remain8 until separate authoritative quantity packets. Session cancel changes generation/world ownership and clears pending. Existing full4337 packages are neither edited nor rerun here.

## Assets and reference projection

Independent projection from accepted sequence/frame/block maps produces the exact239766-byte resource hash `43b88cec329929484610aceae84566d1a922eb4b8ceba93e17dc44b65922ca90`. This file is a test reference under `expected`, not a product resource. It includes24 new frames and references8 frame0 bodies from the pinned base profile. Each of32 actual baseline Blit→BitmapOf outputs matches the corresponding accepted RGBA, including written black and transparent coverage. The 8×4 contact sheet was visually inspected: directional poses remain distinct, with no obvious missing strips or clipping; pixel equality is the stronger evidence.

New frame provenance remains24 current-only entries; reused frame0 provenance remains8 historical-selected entries. No whole archive/BIN version equivalence is granted. Shadow lookup is exact-key only; `shadow-key-coverage.json` records existing availability. Candidate tests must remove the previous idle shadow when overlaying a new body and must not synthesize or borrow a missing shadow. Existing assets junction and players manifest remain unchanged.

The24-row proposed matrix covers receipt identity, unsupported guards, active/expired windows, hit/death priority, ownership cancellation, overlap/stale callbacks, late recurrence, terminal584 hold, resource failure, exact pixel/shadow rendering, immutable send/completion behavior and manifest completeness. All candidate rows remain unexecuted. Source recurrence and nominal terminal policy provide expected values, not native runtime observations.
