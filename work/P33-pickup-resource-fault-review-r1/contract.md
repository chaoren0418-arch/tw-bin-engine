# P33 bounded contract, frozen before runtime

| Path | Contract | Reachability / evidence claim |
|---|---|---|
| PickupAssets(byte[]) | size239766 + SHA before JSON; valid base-only contains only 0/0 | True internal constructor via reflection invocation; modified independent byte arrays fail InvalidDataException |
| PickupAssets(byte[],byte[]) | base constructor, then extension size2473547 + SHA before JSON; no partially returned catalog | True constructor; extension failure cannot return a base fallback object |
| Load | Resource(base), Resource(extension), full constructor, always both required even for 0/0 | Missing embedded resource cannot be injected without DLL alteration; not executed |
| ReadBody | RGB555 coordinates/range, direction uniqueness, step metadata/references, complete24 frames/8 directions | Two direct private parser mutations only; impossible to reach with changed JSON through fixed SHA constructor |
| For | missing exact profile, sprite/path/hash mismatch, invalid direction or missing current terminal key returns null | Actual method; catalog corruption requires explicit in-memory seam after successful load |
| For missing body group or embedded key | dictionary index throws KeyNotFoundException | Explicit post-load memory seam; authenticated constructor prevents these malformed catalog states from disk |
| ObjectAssets.PickupFrames | supported pair AND reference-equal current exact appearance, then Load/For | Identical re-parsed appearance rejected; no unrelated weapon/0/0 borrowing |
| PickupContextAt | null -> renderer/assets-unavailable; IOException/InvalidDataException/KeyNotFoundException/JsonException -> asset-integrity/key | Exercise through bound real Renderer and WorldSession, without replacing delegate or guard |
| active next ComposeObjects validation | cancel active; no pickup visual; stale serial inert; fault receipt suppressed | Two profiles, direction0, speed0; minimal synthetic empty scene and packets |
| repaired catalog | repair alone never revives old serial; legal new own receipt starts fresh serial/exact frame | Actual bound S158 parsing, compositor and snapshot |

In-memory base-only catalog injection distinguishes constructor capabilities. It is explicitly NOT a production Load fallback. All production resources/DLLs/asset files remain untouched. No parser exception here is presented as a reachable malformed-file exploit or product defect.
