# Session State — Active

**Last Updated:** 2026-03-06
**Current Task:** HUD GDD written and reviewed

---

## Current Task

HUD GDD (design/gdd/hud.md) — COMPLETE

---

## Completed Sections

- [x] Section 1: Overview
- [x] Section 2: Player Fantasy
- [x] Section 3: Detailed Design (7 elements + respawn sequence)
- [x] Section 4: Formulas (F-HUD-1 through F-HUD-5)
- [x] Section 5: Edge Cases (EC-HUD-01 through EC-HUD-10)
- [x] Section 6: Dependencies (3 new upstream event contracts defined)
- [x] Section 7: Tuning Knobs (17 knobs in HUDData asset)
- [x] Section 8: Acceptance Criteria (20 criteria)

---

## Key Decisions Made

- Noise Indicator: 5-segment VU meter, bottom-left, snap-up / lerp-down pattern
- Suspicion Meter: 120-degree arc, bottom-right, Amber/Orange/Red color bands at 25/60/85 thresholds, invisible (alpha=0) when all seekers Unaware
- Light Exposure Indicator: small circle icon, bottom-center-left, bloom at high exposure; NEW event contract `DetectionSystem.OnPlayerLightLevelChanged` defined here
- Objective Counter: icon + "N / M" text, bottom-center-right, micro-pop animation on collection, amber flash on all-collected
- Phase 2 Indicator: "ALARM" text, top-center, slide-in + hold 2.5s + fade-out, one-shot per session
- Chase Vignette: screen-edge only (center 60%x60% transparent), red, slow pulse at 0.8 Hz, 3.0 Hz accessibility hard cap
- Gadget Slot UI: 2 empty stub frames, top-left, awaiting Gadget Inventory GDD
- Respawn sequence: NEW event contracts `CheckpointManager.OnRespawnSequenceStarted / OnRespawnSequenceEnded` defined; HUD fades out and back in around respawn
- All values in `HUDData` ScriptableObject — no hardcoded values
- New SeekerRegistry event contracts: `OnSeekerRegistered / OnSeekerUnregistered` for dynamic per-seeker subscription management

## New Upstream Event Contracts Defined

1. `DetectionSystem.OnPlayerLightLevelChanged` (Action<float>) — Detection System must implement
2. `SeekerRegistry.OnSeekerRegistered` / `OnSeekerUnregistered` (Action<EnemyController>) — Two-Phase Level Structure / SeekerRegistry must implement
3. `CheckpointManager.OnRespawnSequenceStarted` / `OnRespawnSequenceEnded` (Action) — Checkpoint System must implement

## Open Minor Issues (from design review)

1. F-HUD-1 example has a mid-paragraph self-correction — clean up in revision pass
2. `lightExtendedDimDelay` tuning knob (3.0s) missing from Section 7 — add in revision
3. ObjectiveIconSprite read timing ambiguity — clarify it is read in HUDManager.Start
4. Coroutine lifecycle for rapid Noise Indicator events — specify ongoing Coroutine with updated target (not restart per event)
5. Add `respawnHudFadeDuration < caughtFreezeDelay` constraint note to Section 7

---

## Files Modified This Session

| File | Change |
|------|--------|
| `design/gdd/hud.md` | Created — full HUD GDD, all 8 sections, 7 HUD elements, 20 acceptance criteria |
| `design/gdd/systems-index.md` | Updated HUD row: Not Started → In Design; added design doc path; expanded dependencies; incremented docs started count to 13 |

---

## Next Steps

- [ ] Revision pass on hud.md to address the 5 minor review items above
- [ ] Update Detection System GDD Section 6 and Section 9 to reference OnPlayerLightLevelChanged
- [ ] Update Checkpoint System GDD Section 6 to reference OnRespawnSequenceStarted / Ended
- [ ] Update Two-Phase Level Structure GDD (SeekerRegistry API block) to reference OnSeekerRegistered / Unregistered
- [ ] Design Win / Game Over Screens GDD (systems index #21, MVP, Not Started)
