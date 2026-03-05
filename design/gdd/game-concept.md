# Game Concept: UNSEEN

*Created: 2026-03-04*
*Status: Draft*

---

## Elevator Pitch

> A single-player 3D stealth puzzle game where you are the prey — trapped in
> dungeon chambers patrolled by relentless seekers, armed only with your wits,
> the darkness, and the noise you choose to make. Find what they're guarding.
> Then get out.

---

## Core Identity

| Aspect | Detail |
| ---- | ---- |
| **Genre** | Stealth / Puzzle — 3D, single-player |
| **Platform** | PC (primary) |
| **Target Audience** | Explorers and Achievers; stealth/puzzle enthusiasts |
| **Player Count** | Single-player |
| **Session Length** | 30–90 minutes per level |
| **Monetization** | Premium (buy once) |
| **Estimated Scope** | Large (12+ months — long-haul passion project) |
| **Comparable Titles** | Thief (1998), Mark of the Ninja, Hitman (World of Assassination) |

---

## Core Fantasy

You are smarter than the thing hunting you. The dungeon is a system — light
has rules, sound has rules, the seeker's mind has rules — and you are here to
learn them all. Every patrol route is a puzzle waiting to be decoded. Every
shadow is a resource waiting to be spent. When you slip past a seeker who
looked your way a second too late, it doesn't feel like luck. It feels like
mastery.

---

## Unique Hook

It's like Thief's simulation-depth, **AND ALSO** every chamber is hand-crafted
as a puzzle — the level designer pre-loaded the room with a specific logic that
rewards discovery, then freed the systemic tools to let you solve it your way.

The tension between designer intent and player improvisation is where the game
lives. You're not just surviving — you're figuring out the *answer* to a room,
then executing it.

---

## Player Experience Analysis (MDA Framework)

### Target Aesthetics (What the player FEELS)

| Aesthetic | Priority | How We Deliver It |
| ---- | ---- | ---- |
| **Discovery** (exploration, secrets) | 1 | Learnable room rules, seeker behavior patterns, hidden paths, environmental lore |
| **Challenge** (obstacle course, mastery) | 2 | Detection system depth, puzzle gates, escalating level complexity |
| **Fantasy** (make-believe, role-playing) | 3 | Power asymmetry fantasy — you are the clever prey, never the predator |
| **Sensation** (sensory pleasure) | 4 | Audio-visual feedback for stealth (shadows, near-miss sound design) |
| **Narrative** (drama, story arc) | 5 | Environmental storytelling; what are these dungeons? What are the seekers? |
| **Expression** (creativity) | 6 | Multiple valid solutions per room; player chooses their approach |
| **Fellowship** (social) | N/A | Single-player only |
| **Submission** (relaxation) | N/A | Tension is core; this is not a comfort game |

### Key Dynamics (Emergent player behaviors)

- Players will **scout before committing** — observing patrol routes before moving
- Players will **experiment with distractions** to learn seeker reaction radii
- Players will **replay levels** to find more elegant solutions after initial completion
- Players will **share solutions** with the community ("I hid in the barrel the whole second phase")
- Players will **feel pride** when a multi-step plan executes cleanly

### Core Mechanics (Systems we build)

1. **Detection System** — Line-of-sight with light-level modifier; sound propagation with surface material multipliers; suspicion meter with graduated states (Unaware → Alert → Searching → Chase)
2. **Environmental Interaction** — Light sources (breakable, carriable), sound distractables (thrown objects, wind-up toys), environmental puzzle gates (fuse boxes, pressure plates, mirror-beam relays)
3. **Hiding Spot System** — Interactable volumes (wardrobes, barrels, shadows) with enter/exit mechanics; peek-out camera mode; seeker awareness penalty for nearby hiding
4. **Stealth Toolkit** — Limited-use gadgets (smoke puff, creak decoy, silence charm); player noise meter reacts to surface type and movement speed
5. **Two-Phase Level Structure** — Find phase (locate and collect objective under patrol pressure) → Escape phase (exit with increased seeker awareness)

---

## Player Motivation Profile

### Primary Psychological Needs Served

| Need | How This Game Satisfies It | Strength |
| ---- | ---- | ---- |
| **Autonomy** (freedom, meaningful choice) | Multiple valid solutions per room; player chooses which tools to use and when | Supporting |
| **Competence** (mastery, skill growth) | Legible failure (always know why you were caught); replay reveals faster routes; ghost-run mastery ceiling | Core |
| **Relatedness** (connection, belonging) | Environmental narrative; seeker characters with implied backstory; player feels "they vs. me" stakes | Supporting |

### Player Type Appeal (Bartle Taxonomy)

- [x] **Explorers** (discovery, understanding systems, finding secrets) — Learnable room rules, hidden routes, seeker behavior as puzzle. This is the primary audience.
- [x] **Achievers** (goal completion, collection, progression) — Ghost-run replays, level grades, completion states. Secondary pull for perfectionist players.
- [ ] **Socializers** — N/A (single-player)
- [ ] **Killers/Competitors** — Minimal; speedrun potential but not designed around it

### Flow State Design

- **Onboarding curve**: Level 1 is a single room with one seeker and one objective — no gadgets. Each subsequent level adds one mechanic. Players learn by doing, not by reading.
- **Difficulty scaling**: Levels add seeker count, seeker variant types, longer patrol routes, darker/more complex rooms. Player toolkit expands in parallel.
- **Feedback clarity**: Detection state is always visible (seeker eye icon + audio cue); near-miss moments use distinct audio; level complete screen shows time + detection count.
- **Recovery from failure**: Detection triggers a chase → if caught, respawn at last checkpoint in same room. No level restart by default. Failure is educational, not punishing.

---

## Core Loop

### Moment-to-Moment (30 seconds)

**Observe → Plan → Move → React**

The player watches a seeker's patrol, identifies a shadow, waits for the gap,
moves to cover, and either succeeds or triggers a new problem. Every 30-second
window is a micro-decision: *do I move now, or wait one more cycle?*

The intrinsic satisfaction lives in the **information game** — reading the seeker
correctly and being right. Sound design amplifies this: the seeker's footsteps
fade, the moment of silence, then the player moves.

### Short-Term (5-15 minutes)

**Zone navigation → objective found or puzzle gate cleared**

A "zone" is one to two rooms with a seeker (or multiple seekers). Clearing a zone
means navigating safely from entry to exit, possibly collecting objective items
or solving one puzzle gate en route.

"One more room" psychology: the objective is always close enough to be tantalizing.

### Session-Level (30-90 minutes)

**Enter level → Learn phase → Execute phase → Escape**

Early runs of a level are exploratory — the player learns patrol routes, finds
the objective, discovers the hiding spots. Later runs feel like controlled
execution. The two-phase escalation (objective collected → heightened seeker
awareness) gives every session a climax.

Natural stopping point: level complete. Reason to return: the next level is
unlocked; or the player wants a cleaner run.

### Long-Term Progression

New levels introduce new seeker types (each with distinct behaviors to learn),
new environment themes (dungeon → castle → catacombs → ???), and new gadgets
that expand the solution space. The meta-progression is *knowledge* — the
player becomes a seeker-behavior expert.

Optional: ghost-run challenge (complete a level with zero detections) unlocks
bonus lore or cosmetic.

### Retention Hooks

- **Curiosity**: What's in the next room? What seeker type is in level 7? What's the lore behind these dungeons?
- **Investment**: Ghost-run records per level. Completion state shows how many levels cleared without detection.
- **Mastery**: Replay reveals faster, cleaner routes. The first run is exploration; the third run is a performance.

---

## Game Pillars

### Pillar 1: The Room Has Rules

Every chamber has a discoverable, internally consistent logic governing light,
sound, and seeker behavior. Mastery feels earned because the rules are fair and
legible. A player should never feel cheated — only outplayed by their own incomplete
understanding.

*Design test*: If we're debating whether to add a seeker that "randomly changes
patrol route," this pillar says NO — unpredictable rules undermine the puzzle-box
contract.

### Pillar 2: Silence Is a Tool, Not an Absence

Darkness, noise, and distraction are things the player actively *creates and deploys*
— not constraints they passively avoid. The player should be making decisions, not
waiting. A good run involves choosing what to do, not just waiting for a gap.

*Design test*: If a feature gives the player more things to avoid but no new tools
to use, this pillar says we either add a counter-tool or cut the feature.

### Pillar 3: Legible Jeopardy

The player must always be able to read their own danger level. Detection states,
patrol paths, and sound radii are communicated through the world's visuals and
audio — not hidden in menus or invisible calculations. When caught, the player
immediately understands why.

*Design test*: If we're debating whether to show the seeker's vision cone, this
pillar says YES — hidden information creates frustration, not tension.

### Pillar 4: Two-Beat Tension

Every level earns its relief. The find-phase (exploration under patrol threat)
gives way to an escape-phase (objective in hand, stakes raised) — and the
emotional payoff of escape depends on that escalation. Levels that don't change
stakes after the objective is found are structurally flat.

*Design test*: If a level design doesn't have a distinct second act after the
objective is collected, it needs a redesign.

### Anti-Pillars (What This Game Is NOT)

- **NOT a combat game**: The player cannot reliably neutralize seekers. A gadget might stun, divert, or briefly blind — but killing undermines the power asymmetry at the heart of the fantasy.
- **NOT procedurally generated**: Hand-crafted levels are load-bearing for the puzzle-box pillar. A generator cannot pre-load intentional room logic. This is always a scope trap.
- **NOT a survival/attrition game**: No hunger timers, no resource starvation, no meta-progression currencies. The challenge is intellectual, not punitive.
- **NOT a horror game**: Tension and dread are present but the tone is *clever thriller*, not oppressive terror. The player should feel smart, not helpless.

---

## Inspiration and References

| Reference | What We Take From It | What We Do Differently | Why It Matters |
| ---- | ---- | ---- | ---- |
| **Thief (1998)** | Sound simulation as primary stealth mechanic; information asymmetry | 3D modern, shorter level scope, explicit detection feedback UI | Validates simulation-depth stealth as compelling |
| **Mark of the Ninja** | Enemy awareness as fully legible visual system; moment-of-silence rhythm | 3D perspective; puzzle gates as level structure | Validates that legible detection reduces frustration without reducing tension |
| **Hitman (World of Assassination)** | Systemic sandbox where player discovers emergent solutions; replay rewards mastery | No disguise system; no combat fallback; smaller scope per level | Validates that puzzle-box design within a systemic engine creates the deepest engagement |

**Non-game inspirations**:
- *Dungeon architecture* — the language of dungeons as spaces designed to trap and test intruders (inverted: now we are the intruder)
- *Heist films* (Heat, Rififi, Thief 1981) — slow-build tension, the cost of improvisation, the elegance of the plan
- *Creature horror* (Alien, Jaws) — the seeker should feel like a *thing with a logic*, not a random obstacle

---

## Target Player Profile

| Attribute | Detail |
| ---- | ---- |
| **Age range** | 18–40 |
| **Gaming experience** | Mid-core to Hardcore |
| **Time availability** | 30–90 minute sessions; willing to replay levels |
| **Platform preference** | PC (Steam) |
| **Current games they play** | Hitman 3, Dishonored, Mark of the Ninja, Return of the Obra Dinn |
| **What they're looking for** | A game that respects their intelligence; one where failure teaches and mastery feels earned |
| **What would turn them away** | Randomness that punishes without teaching; overly long tutorials; combat as a bailout |

---

## Technical Considerations

| Consideration | Assessment |
| ---- | ---- |
| **Recommended Engine** | Unity 6.3 LTS (already in use; URP 17.3.0; NavMesh AI package) |
| **Key Technical Challenges** | Detection system legibility (communicating LoS + sound in 3D without clutter); NavMesh integration with dynamic obstacles; performance with multiple simultaneous seekers |
| **Art Style** | Stylized 3D — KayKit Dungeon Pack as base; supplemented with custom and additional low/mid-poly assets |
| **Art Pipeline Complexity** | Medium — asset-store base reduces modeling load; custom assets added iteratively |
| **Audio Needs** | High — sound IS a mechanic; footstep surface differentiation, seeker audio cues, and near-miss audio are load-bearing for the fantasy |
| **Networking** | None |
| **Content Volume** | 8–12 hand-crafted levels; 4–6 seeker variant types; ~8–15 hours first playthrough; 20+ hours for ghost-run completionists |
| **Procedural Systems** | None — intentionally excluded by pillar |

---

## Risks and Open Questions

### Design Risks

- **Waiting vs. Playing**: Stealth games risk devolving into "watch the patrol, wait for the gap, move." Mitigation: Pillar 2 (Silence as Tool) requires the player to always have active options; gadgets and distractions must always provide a "do something" alternative to pure waiting.
- **Legibility ceiling**: 3D LoS is harder to communicate than 2D (Mark of the Ninja solved this trivially). A 3D vision cone that feels fair requires significant design investment in camera positioning, audio cues, and UI.
- **Level fatigue**: 8–12 hand-crafted levels is substantial content for a solo dev. Each level requires patrol design, puzzle gate placement, and hide-spot placement. Risk of homogeneity over time.

### Technical Risks

- **NavMesh dynamic obstacles**: Seekers pathfinding around player-moved furniture or triggered environmental changes is non-trivial in Unity NavMesh.
- **Detection system complexity**: The full detection model (LoS + light level + sound surface + suspicion meter + seeker type modifiers) is a complex simulation. Bugs here are invisible to the player and destroy trust.
- **3D sound propagation**: True sound occlusion in 3D is expensive. A simplified model that *feels* accurate is harder to design than it sounds.

### Market Risks

- **Stealth niche**: Pure stealth (no combat fallback) has a smaller audience than stealth-action. The puzzle framing broadens appeal but may confuse genre positioning.
- **Solo dev scope**: 8–12 polished, hand-crafted levels is ambitious for one developer. Risk of never shipping.

### Scope Risks

- **Audio**: Sound is a mechanic — this means the sound design budget (time, iteration) is much higher than a typical small game. Cannot be deferred.
- **Level design iteration**: Every level will require multiple playtesting passes. Without a team, this means the developer is their own playtester — a known bias problem.

### Open Questions

- **What makes a seeker variant interesting?** What behavioral axes create meaningful differentiation without adding opaque rules? *Answer via: prototype 2-3 seeker types and playtest with fresh players.*
- **How legible is 3D LoS?** Does the player feel informed or guessing? *Answer via: Sprint 1 detection prototype with user testing.*
- **How long should levels be?** Is 30-minute first-run level design right, or should levels be shorter and more numerous? *Answer via: playtest Level_1 for time-to-complete data.*

---

## MVP Definition

**Core hypothesis**: *A player can enter a dungeon room, observe a seeker's patrol, use the environment to navigate safely, collect an objective, and escape — and this 5-minute loop is intrinsically satisfying.*

**Required for MVP**:
1. One complete level (Level_1) with one seeker, one objective item, one escape trigger
2. Detection system: LoS + basic sound propagation + suspicion meter + chase state
3. One hiding spot type (Wardrobe) — enter, peek, exit
4. One distraction item (throwable)
5. Win and lose states with basic UI

**Explicitly NOT in MVP**:
- Multiple seeker types (validate one first)
- Gadgets beyond one distraction type
- Puzzle gates / environmental puzzles
- Light manipulation (breakable light sources)
- Narrative / lore
- Level progression / unlock system

### Scope Tiers (if momentum shifts)

| Tier | Content | Features | Milestone |
| ---- | ---- | ---- | ---- |
| **MVP** | Level_1 only | Core detection loop, 1 seeker, 1 hiding spot, win/lose | Sprint 1 (2 weeks) |
| **Vertical Slice** | 3 levels, 2 seeker types | Full toolkit, puzzle gates, light manipulation | Sprint 3–4 |
| **Alpha** | 6 levels, all seeker types | All systems, rough balance, placeholder audio | Month 4–6 |
| **Full Vision** | 8–12 levels, complete narrative | Polished detection, full audio, ghost-run system, all gadgets | 12–18 months |

---

## Next Steps

- [ ] Run `/design-review design/gdd/game-concept.md` to validate this document
- [ ] Run `/design-systems` to decompose the concept into individual system GDDs and map dependencies
- [ ] Create detection system GDD (`design/gdd/detection-system.md`) — this is the most risk-loaded system
- [ ] Create an architecture decision record for the detection system architecture (`/architecture-decision`)
- [ ] Playtest Level_1 MVP loop and capture data with `/playtest-report`
- [ ] Plan Sprint 2 against this validated concept (`/sprint-plan`)
