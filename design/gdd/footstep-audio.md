# Footstep Audio

> **Status**: Approved
> **Author**: audio-director + sound-designer
> **Last Updated**: 2026-03-06
> **Implements Pillar**: Silence Is a Tool (Pillar 2), The Room Has Rules (Pillar 1)

---

## 1. Overview

The Footstep Audio system is the player-body audio feedback layer of UNSEEN. It
owns every sound the player hears confirming their own physical presence in the
world: footstep impacts keyed to surface type and speed tier, landing thuds,
interaction shuffles, and hide-spot entry and exit rustles. These sounds are not
atmospheric decoration. They are the primary mechanism through which the player
builds a mental model of noise hazards without reading a stat screen: a player
who hears metal ring differently from carpet has already learned the core acoustic
hierarchy of Pillar 2. The Footstep Audio system subscribes to events published
by the Player Noise Emitter (PNE) — never performing its own raycasts or physics
queries — and routes all playback through `AudioManager.Instance.PlayAtPosition`
using a pool-backed `SoundEmitter` architecture. A dedicated `FootstepAudioController`
MonoBehaviour on the Player GameObject selects clips from a `SoundID`-indexed
lookup table, applies per-clip random variation, enforces a minimum-interval gate
against edge-case double-firing, and plays the result as a positional 3D sound.
The system has no write path back to gameplay: it listens, selects, plays. All
detection consequences of footstep noise flow through the PNE and Sound Propagation
Model independently of this system.

---

## 2. Player Fantasy

The player is a ghost who has not yet learned to be silent.

At the start of the first chamber the player is loud without knowing it. Each
footfall confirms they exist in the world — stone answers back with a dry, resonant
clap. The sound is grounding. It says: you are embodied, you are here, you have
weight. This is the entry point. The fantasy is not silence from the start; it
is the journey from weight to weightlessness.

The surface-to-sound relationship is the tutorial. A player who steps onto a
metal grating and hears the ringing carry through the room has just learned
something that no tooltip could teach with the same emotional force. The sound
is the lesson. When that player later tiptoes across a carpet runner and hears
almost nothing, they have internalized the acoustic hierarchy through felt
experience. This is Pillar 2 working as intended: silence as something earned
and managed, not simply avoided.

Speed tier must carry acoustic weight beyond mere loudness. Crouching should
sound deliberate — measured, effortful, careful. Each crouch-step is a breath
held, a foot placed rather than dropped. Walking sounds natural and purposeful.
Sprinting sounds urgent and reckless: impacts that are heavier and less controlled,
a body committing to speed at the cost of silence. Sprint footsteps must not
sound like walk footsteps played faster. They must sound like a different emotional
state expressed through movement.

The emotional arc the audio must support:

**Discovery: "My footsteps make a sound."** The player hears the first footstep
on stone and the HUD noise indicator pulses in sync. Sound confirms mechanics.

**Calibration: "This floor is different."** Stepping from stone to wood or metal
produces an audibly distinct impact. The player stops. Steps back. Steps forward.
The audio is immediately repeatable and consistent. Pillar 1 (The Room Has Rules)
is confirmed through the ears.

**Mastery: "I am composing my movement."** An experienced player route-plans across
carpet specifically to avoid the metal grating, then makes a deliberate loud
interaction as a distraction. They are not avoiding sound; they are spending it.
The audio system must make every step feel like a word spoken in a language the
player has learned.

The target MDA aesthetics: Challenge (moving silently through a room that wants
to betray you), Discovery (acoustic hierarchy as emergent knowledge), Expression
(noise as a tactical resource with a sonic identity the player recognizes and
trusts).

---

## 3. Detailed Design

### 3.1 FootstepAudioController Component

`FootstepAudioController` is a `MonoBehaviour` placed on the Player GameObject
alongside `PlayerNoiseEmitter`. It owns all clip selection logic and playback
routing for player-body audio. It has no write path back to gameplay systems.

**Lifecycle:**

- `Awake`: Cache `Transform playerTransform` via `GetComponent`. Initialize all
  lookup tables and pool references. Perform null checks on all serialized
  references. Set `_lastFootstepTime = -999f` so the first footstep never trips
  the minimum interval gate.
- `OnEnable`: Subscribe to all PNE events (see Section 3.2).
- `OnDisable`: Unsubscribe from all PNE events. Unsubscribing in `OnDisable`
  (not `OnDestroy`) ensures subscriptions are released if the component is
  disabled mid-session (e.g., cutscene, death sequence).
- No logic in `Update`, `FixedUpdate`, or `LateUpdate`. Fully event-driven.

**References (all `[SerializeField]`, no `FindObjectOfType`):**

```csharp
[SerializeField] private FootstepAudioData _audioData;
// FootstepAudioData is the ScriptableObject that maps
// (SurfaceType, SpeedTier) → AudioClip[] pools.
// Defined in Section 3.3.
```

`AudioManager.Instance` is accessed at call time (Service Locator pattern,
acceptable for global audio services per CLAUDE.md conventions). The reference
is not cached — `AudioManager.Instance` is a Singleton that guarantees availability
after Awake order. If `AudioManager.Instance` is null at call time (edge case:
component enabled before AudioManager is initialized), the call is a no-op and
an error is logged.

**No `AudioSource` components on the Player GameObject for footstep audio.**
All audio routed through `AudioManager.Instance.PlayAtPosition`.

---

### 3.2 Event Subscriptions

`FootstepAudioController` subscribes to the following events in `OnEnable` and
unsubscribes in `OnDisable`:

| Event | Publisher | Handler Method | Notes |
|-------|----------|---------------|-------|
| `PlayerNoiseEmitter.OnSurfaceTypeResolved` | PlayerNoiseEmitter | `OnFootstepOccurred(SurfaceType, SpeedTier)` | See Section 6: this event requires SpeedTier in its payload — a new contract on the PNE |
| `PlayerNoiseEmitter.OnLandingOccurred` | PlayerNoiseEmitter | `OnLandingOccurred(SurfaceType)` | Surface-specific heavy thud |
| `PlayerNoiseEmitter.OnInteractOccurred` | PlayerNoiseEmitter | `OnInteractOccurred()` | Surface-agnostic; single clip MVP |
| `PlayerNoiseEmitter.OnHideEntryOccurred` | PlayerNoiseEmitter | `OnHideEntryOccurred()` | Surface-agnostic shuffle |
| `PlayerNoiseEmitter.OnHideExitOccurred` | PlayerNoiseEmitter | `OnHideExitOccurred()` | Surface-agnostic shuffle, distinct clip |

**Subscription code pattern:**

```csharp
private void OnEnable()
{
    PlayerNoiseEmitter.OnSurfaceTypeResolved += OnFootstepOccurred;
    PlayerNoiseEmitter.OnLandingOccurred     += OnLandingOccurred;
    PlayerNoiseEmitter.OnInteractOccurred    += OnInteractOccurred;
    PlayerNoiseEmitter.OnHideEntryOccurred   += OnHideEntryOccurred;
    PlayerNoiseEmitter.OnHideExitOccurred    += OnHideExitOccurred;
}

private void OnDisable()
{
    PlayerNoiseEmitter.OnSurfaceTypeResolved -= OnFootstepOccurred;
    PlayerNoiseEmitter.OnLandingOccurred     -= OnLandingOccurred;
    PlayerNoiseEmitter.OnInteractOccurred    -= OnInteractOccurred;
    PlayerNoiseEmitter.OnHideEntryOccurred   -= OnHideEntryOccurred;
    PlayerNoiseEmitter.OnHideExitOccurred    -= OnHideExitOccurred;
}
```

---

### 3.3 FootstepAudioData ScriptableObject

`FootstepAudioData` is a `ScriptableObject` at
`Assets/_Project/Scripts/Data/FootstepAudioData.asset`. It is the single source
of truth for all clip assignments, volume ranges, pitch ranges, and spatial
settings for the Footstep Audio system.

**Clip storage architecture:**

Each of the 18 footstep entries and 6 landing entries is a `FootstepClipEntry`
— a serializable plain class containing an array of `AudioClip` references (the
random-selection pool) plus per-entry volume and pitch override ranges:

```csharp
[Serializable]
public class FootstepClipEntry
{
    public AudioClip[] Clips;          // 2–4 variants; indexed by random selection
    [Range(0f, 1f)]   public float VolumeMin = 0.85f;
    [Range(0f, 1f)]   public float VolumeMax = 1.00f;
    [Range(0.5f, 2f)] public float PitchMin  = 0.92f;
    [Range(0.5f, 2f)] public float PitchMax  = 1.08f;
}
```

The clip table is stored as a 2D structure indexed by `[SurfaceType][SpeedTier]`.
Because Unity serialization does not support nested arrays directly, the
ScriptableObject uses a flat array of 18 entries with a deterministic index
formula (see Section 4, Formula F-FA-1).

```csharp
// In FootstepAudioData:
[SerializeField] private FootstepClipEntry[] _footstepEntries;
// Length = 18: 6 surfaces × 3 tiers (see F-FA-1 for index formula)

[SerializeField] private FootstepClipEntry[] _landingEntries;
// Length = 6: one per SurfaceType (Stone through Water)

public FootstepClipEntry GetFootstepEntry(SurfaceType surface, SpeedTier tier)
    => _footstepEntries[FootstepIndex(surface, tier)];

public FootstepClipEntry GetLandingEntry(SurfaceType surface)
    => _landingEntries[(int)surface]; // surface enum 0–5; Neutral excluded

[SerializeField] public FootstepClipEntry InteractEntry;
[SerializeField] public FootstepClipEntry HideEntryEntry;
[SerializeField] public FootstepClipEntry HideExitEntry;
```

Additional spatial and gating fields on `FootstepAudioData`:

```csharp
[Header("Minimum Interval Gate")]
[Range(0.01f, 0.5f)] public float MinTimeBetweenFootsteps = 0.08f;

[Header("3D Spatial Settings")]
[Range(0f, 1f)]  public float SpatialBlend       = 1.0f;   // fully 3D
[Range(1f, 50f)] public float MaxDistance         = 20f;
public AudioRolloffMode RolloffMode = AudioRolloffMode.Logarithmic;

[Header("Sprint Volume Bump")]
[Range(0f, 0.3f)] public float SprintVolumeBump = 0.10f;
// Added on top of per-entry VolumeMax when SpeedTier == Sprint.
// Conveys urgency beyond BaseIntensity difference already in detection pipeline.
```

---

### 3.4 Clip Selection Logic

**Footstep playback handler:**

```csharp
private void OnFootstepOccurred(SurfaceType surface, SpeedTier tier)
{
    // Minimum interval gate (protection against edge-case double-fire)
    float now = Time.time;
    if (now - _lastFootstepTime < _audioData.MinTimeBetweenFootsteps) return;
    _lastFootstepTime = now;

    FootstepClipEntry entry = _audioData.GetFootstepEntry(surface, tier);
    if (entry == null || entry.Clips == null || entry.Clips.Length == 0)
    {
        Debug.LogWarning($"[FootstepAudio] No clips for {surface}/{tier}");
        return;
    }

    SoundID id = ResolveFootstepSoundID(surface, tier);
    PlayWithVariation(id, entry, tier == SpeedTier.Sprint);
}
```

**Landing playback handler:**

```csharp
private void OnLandingOccurred(SurfaceType surface)
{
    FootstepClipEntry entry = _audioData.GetLandingEntry(surface);
    if (entry == null || entry.Clips == null || entry.Clips.Length == 0)
    {
        Debug.LogWarning($"[FootstepAudio] No landing clips for {surface}");
        return;
    }

    SoundID id = ResolveLandingSoundID(surface);
    PlayWithVariation(id, entry, applySprintBump: false);
}
```

**Interact, hide entry, hide exit handlers (surface-agnostic):**

```csharp
private void OnInteractOccurred()
    => PlayWithVariation(SoundID.Footstep_Interact, _audioData.InteractEntry, false);

private void OnHideEntryOccurred()
    => PlayWithVariation(SoundID.Footstep_HideEntry, _audioData.HideEntryEntry, false);

private void OnHideExitOccurred()
    => PlayWithVariation(SoundID.Footstep_HideExit, _audioData.HideExitEntry, false);
```

**Shared playback method:**

```csharp
private void PlayWithVariation(SoundID id, FootstepClipEntry entry, bool applySprintBump)
{
    if (AudioManager.Instance == null)
    {
        Debug.LogError("[FootstepAudio] AudioManager.Instance is null.");
        return;
    }

    // Random clip selection from pool (see F-FA-2)
    int clipIndex = Random.Range(0, entry.Clips.Length);

    // Volume and pitch variation (see F-FA-3)
    float volumeMax = applySprintBump
        ? Mathf.Min(1f, entry.VolumeMax + _audioData.SprintVolumeBump)
        : entry.VolumeMax;
    float volume = Random.Range(entry.VolumeMin, volumeMax);
    float pitch  = Random.Range(entry.PitchMin,  entry.PitchMax);

    // AudioManager handles clip-to-SoundEmitter binding; it resolves
    // the clip from the assigned SoundID in SoundLibrary/SoundData.
    // Volume and pitch are applied to the SoundEmitter before Play.
    AudioManager.Instance.PlayAtPosition(id, _playerTransform.position, volume, pitch);
}
```

Note: `AudioManager.PlayAtPosition` is extended to accept optional `volume` and
`pitch` overrides. If the current AudioManager signature does not include these
parameters, `FootstepAudioController` checks out a `SoundEmitter` directly from
the pool and configures it — this is a coordination item for the lead-programmer
(see Section 6, Dependencies).

---

### 3.5 Clip Matrix: 18 Footstep Entries

Each cell is a random-selection pool of 2–4 `AudioClip` variants. The acoustic
character notes define what the sound-designer must deliver for each entry.

| Surface | Walk | CrouchWalk | Sprint |
|---------|------|-----------|--------|
| **Stone** | Dry resonant clap. Medium weight. Slightly reverberant. 2–3 variants. | Hushed stone scrape. Deliberate. Very quiet — nearly whispered. 2–3 variants. | Heavy flagstone impact. Faster, urgent. Feet slapping hard. 2–4 variants. |
| **Wood** | Hollow floorboard thud. Occasional low creak. 3–4 variants (creak frequency via random pool). | Careful toe-placement. Muffled creak suppressed but not absent. 2–3 variants. | Rapid wooden impacts. Boards rattle. Louder, higher frequency energy. 3–4 variants. |
| **Metal** | Ringing iron clang. Sharp transient, short decay. Perceptibly brightest surface. 2–3 variants. | Controlled heel-toe on grating. Steel muted but still bright. 2–3 variants. | Clanging urgency. Rapid metallic ringing. Unmistakably loud. 3–4 variants (priority surface for distinctiveness). |
| **Dirt** | Soft earthen thud. No resonance. Muffled. 2–3 variants. | Near-silent earth compression. Barely audible impact. 2 variants. | Heavier earth impact. Slight scatter/gravel sound possible. 2–3 variants. |
| **Carpet** | Muffled fabric compression. Almost no impact transient. 2 variants. | Nearly inaudible. Fabric whisper. 2 variants (minimum pool; both very quiet). | Rapid carpet strike. Some impact energy still absorbed. 2–3 variants. |
| **Water** | Shallow splash with bright transient. Distinctive and carries. 2–3 variants. | Careful water displacement. Slower splash. 2–3 variants. | Rapid splashing. Spray character. 3–4 variants. |

**Perceptual loudness ordering (same speed tier, normalized to Stone = baseline):**

Metal > Wood > Water > Stone > Dirt > Carpet

This ordering must be confirmed in the mix. At equal playback distance, a listener
must be able to rank surfaces by perceived loudness without seeing the screen.
The ordering mirrors the SPM surface multiplier hierarchy and is intentional:
players learn one consistent acoustic hierarchy.

---

### 3.6 Landing SFX Design

6 entries, one per physical surface type (Neutral excluded).

Landing SFX are designed to be perceptibly heavier than the walk footstep on the
same surface. The player has dropped mass onto a surface from height; the acoustic
character is a full-body impact, not a foot placement.

| Surface | Landing Character |
|---------|------------------|
| Stone | Dense flagstone impact. Full-body thud with short reverb tail. Noticeably heavier than walk. |
| Wood | Deep board impact. Possible creak-burst. Hollow resonance at lower frequency than walk. |
| Metal | Sharp, loud iron strike. Ringing sustains slightly longer than walk. Hardest-hitting landing. |
| Dirt | Heavy earth compression. Low thud. Dust/scatter character optional. |
| Carpet | Fabric compression with partial floor impact. Less absorbed than walk; mass still carries. |
| Water | Large splash. Spray and wave character. Most acoustically distinctive landing. |

Landing clips: single clip per surface (no random pool required — landing is a
one-shot impact where variation matters less than character). If variation is
desired during production, 2 variants per surface is sufficient.

---

### 3.7 Interact, Hide Entry, and Hide Exit SFX Design

These events are surface-agnostic at MVP. The sound is the player's body interacting
with a prop or hiding spot, not the surface beneath them.

| Cue | Character | Clip Count | Notes |
|-----|-----------|-----------|-------|
| Interact | A generic prop-handling sound: light hand-on-object, subtle effort. Neutral. Does not imply a specific prop type. | 1 MVP clip (2–3 variants acceptable if production budget allows) | Per-prop audio overrides are Environmental Interaction system scope (Vertical Slice). The `FootstepAudioController` plays this only when no per-prop override is active. |
| Hide Entry | Soft fabric shuffle and body compression. The sound of squeezing into cover. Distinct from footsteps. | 1 clip (single variant acceptable at MVP) | Must not sound like a footstep. Short duration (< 0.5 s recommended). |
| Hide Exit | The sound of emerging from cover — a slight rustle and weight shift. Distinct from hide entry. | 1 dedicated clip (not a reuse of hide entry) | Justification for distinct clip: entry is compression (inward) and exit is expansion (outward). These are different physical motions with different acoustic signatures. Using the same clip flattens the directionality of hiding as a mechanic. The player should hear a difference and build a mental model of each transition. |

---

### 3.8 Mixing Architecture and AudioMixer Group Structure

**Mixer group hierarchy:**

```
Master
└── SFX
    ├── Footstep          <-- all FootstepAudioController output
    │   ├── Footstep_Steps     (footstep and landing clips)
    │   └── Footstep_Actions   (interact, hide entry, hide exit clips)
    ├── Combat            (future: out of scope for MVP)
    └── UI
```

All output from `FootstepAudioController` routes to the `Footstep` submixer
group. This allows:

- Independent volume tuning of footstep audio without touching the SFX master.
- Separate EQ on footstep bus (low-cut below 80 Hz to prevent mud on stone/metal
  impacts; presence boost at 2–4 kHz for step intelligibility in reverberant dungeon
  mix).
- Sidechain ducking source: when a seeker vocalizes (Seeker Audio, Vertical Slice
  scope), the `Footstep` group can duck 2–4 dB to ensure seeker lines cut through.
- `Footstep_Steps` sub-group and `Footstep_Actions` sub-group allow independent
  level setting during mix sessions (actions are generally quieter than steps).

**Routing in `SoundEmitter`:** When `AudioManager` checks out a `SoundEmitter`
for a footstep `SoundID`, it assigns the clip's `AudioMixerGroup` to `Footstep_Steps`
or `Footstep_Actions` based on the `SoundID` category. This mapping is authored
in `SoundLibrary` / `SoundData`.

**Loudness targets (LUFS):**

| Category | Target Integrated LUFS | True Peak |
|----------|----------------------|-----------|
| Footstep walk (Stone reference) | -22 LUFS | -3 dBTP |
| Footstep walk (Metal, loudest) | -18 LUFS | -1 dBTP |
| Footstep walk (Carpet, quietest) | -30 LUFS | -6 dBTP |
| Landing (Stone reference) | -16 LUFS | -1 dBTP |
| Interact / Hide | -26 LUFS | -6 dBTP |

These are pre-mix clip targets for the sound-designer. Final mix may raise or
lower the `Footstep` group fader without requiring clip re-exports.

**Volume relationship between surface types (same speed tier, in-game playback):**

The perceptual loudness ordering (Metal > Wood > Water > Stone > Dirt > Carpet)
must be audible at equal playback distance. Approximate relative levels from
Stone reference (0 dB):

| Surface | Relative Level (Walk, same listener distance) |
|---------|----------------------------------------------|
| Metal | +4 to +5 dB |
| Wood | +2 to +3 dB |
| Water | +1 to +2 dB |
| Stone | 0 dB (reference) |
| Dirt | -3 to -4 dB |
| Carpet | -8 to -10 dB |

These levels are achieved through a combination of clip normalization at export
and per-entry `VolumeMax` values in `FootstepAudioData`. The SPM surface multiplier
affects detection only; it does not drive audio volume. Audio volume is authored
independently to match the SPM's hierarchy — the two systems reinforce each other
but do not share parameters.

---

### 3.9 3D Spatial Audio Settings

Applied to every `SoundEmitter` checked out for footstep playback:

| Parameter | Value | Rationale |
|-----------|-------|-----------|
| `SpatialBlend` | 1.0 (fully 3D) | Player footsteps are positional in the world; they must feel grounded at the player's feet, not broadcast from speakers |
| `RolloffMode` | Logarithmic | Dungeon acoustics are stone-walled and reverberant; logarithmic falloff models short-range impact better than linear for close footfalls |
| `MaxDistance` | 20 m | Footsteps should not be audible across large dungeon chambers; 20 m fades to inaudible at dungeon scale |
| `MinDistance` | 1 m | Full volume within 1 m of source (effectively: at the player's feet); begins rolling off beyond |
| `DopplerLevel` | 0 | Doppler on footsteps creates unnatural pitch glide during direction changes; disabled |
| `Spread` | 0 | No angular spread; footsteps are point sources at foot position |

`AudioListener` is on the Camera (or a Camera-attached component). The player's
footsteps will typically be heard at very close range (0.5–2 m from listener),
placing them near full volume in mix. This is correct — the player must clearly
hear their own surface type to plan routes (Pillar 2 requirement).

---

## 4. Formulas

### F-FA-1: Footstep SoundID Index Formula

The 18-entry flat array in `FootstepAudioData` is indexed by:

```
footstepIndex = (int)surface * SpeedTierCount + (int)tier
```

Where:
- `surface` is `SurfaceType` enum value (0 = Stone, 1 = Wood, 2 = Metal, 3 = Dirt, 4 = Carpet, 5 = Water)
- `tier` is `SpeedTier` enum value (0 = Walk, 1 = CrouchWalk, 2 = Sprint)
- `SpeedTierCount` = 3

| Variable | Type | Range | Description |
|----------|------|-------|-------------|
| `surface` | int (enum) | 0–5 | SurfaceType; Neutral (6) is excluded from footstep matrix |
| `tier` | int (enum) | 0–2 | SpeedTier: Walk/CrouchWalk/Sprint |
| `footstepIndex` | int | 0–17 | Flat array index into `_footstepEntries` |

**Full index table:**

| | Walk (0) | CrouchWalk (1) | Sprint (2) |
|-|---------|--------------|----------|
| Stone (0) | 0 | 1 | 2 |
| Wood (1) | 3 | 4 | 5 |
| Metal (2) | 6 | 7 | 8 |
| Dirt (3) | 9 | 10 | 11 |
| Carpet (4) | 12 | 13 | 14 |
| Water (5) | 15 | 16 | 17 |

**Example: Metal Sprint → index = 2 × 3 + 2 = 8**
**Example: Carpet Walk → index = 4 × 3 + 0 = 12**

The corresponding `SoundID` is computed by the `ResolveFootstepSoundID` method
using the same index offset from a base enum value (see F-FA-1b below).

---

### F-FA-1b: SoundID Resolution from Surface and Tier

`SoundID` enum values for footsteps are laid out contiguously in the same row-major
order as the index formula. The base ID for footstep entries is `SoundID.Footstep_Stone_Walk`:

```
SoundID id = (SoundID)((int)SoundID.Footstep_Stone_Walk + footstepIndex)
```

This is valid only if the SoundID enum values for footstep entries are declared
contiguously and in the order defined by F-FA-1. The `SoundLibrary` must enforce
this ordering (see Section 6.3 for the full SoundID list). Any reordering of
footstep enum values will break this formula. The `FootstepAudioData` ScriptableObject
indexing is the authoritative source; the enum is secondary.

---

### F-FA-2: Random Clip Selection

```
clipIndex = Random.Range(0, entry.Clips.Length)
selectedClip = entry.Clips[clipIndex]
```

| Variable | Type | Range | Description |
|----------|------|-------|-------------|
| `entry.Clips.Length` | int | 2–4 | Number of variants in the pool for this entry |
| `clipIndex` | int | 0 – (Clips.Length - 1) | Randomly selected index (uniform distribution) |

**Anti-repetition constraint:** A simple `Random.Range` uniform distribution is
used at MVP. On pools of 2 variants, there is a 50% chance of repeating the same
clip on consecutive steps — acceptable for MVP. If audible repetition is observed
in playtesting, upgrade to a "no-repeat-last" selection:

```csharp
// No-repeat-last (Vertical Slice upgrade):
int clipIndex;
do { clipIndex = Random.Range(0, entry.Clips.Length); }
while (clipIndex == _lastClipIndex[entryKey] && entry.Clips.Length > 1);
_lastClipIndex[entryKey] = clipIndex;
```

This requires a `Dictionary<int, int> _lastClipIndex` keyed by `footstepIndex`.
Implementation deferred to Vertical Slice unless audible repetition is observed
in MVP playtesting.

---

### F-FA-3: Volume and Pitch Variation

```
volume = Random.Range(entry.VolumeMin, entry.VolumeMax)
pitch  = Random.Range(entry.PitchMin,  entry.PitchMax)
```

Sprint volume bump applied after range selection:

```
if (tier == SpeedTier.Sprint):
    volume = min(1.0, volume + _audioData.SprintVolumeBump)
```

| Variable | Type | Range | Description |
|----------|------|-------|-------------|
| `entry.VolumeMin` | float | 0.0–1.0 | Minimum volume for this entry (default 0.85) |
| `entry.VolumeMax` | float | 0.0–1.0 | Maximum volume for this entry (default 1.00) |
| `entry.PitchMin` | float | 0.5–2.0 | Minimum pitch for this entry (default 0.92) |
| `entry.PitchMax` | float | 0.5–2.0 | Maximum pitch for this entry (default 1.08) |
| `SprintVolumeBump` | float | 0.0–0.3 | Additional volume added for sprint tier (default 0.10) |
| `volume` | float | 0.0–1.0 | Final volume passed to AudioManager (clamped to 1.0) |
| `pitch` | float | 0.5–2.0 | Final pitch multiplier passed to AudioManager |

**Default variation ranges rationale:**

- Volume: ±7.5% (0.85–1.00). Subtle enough not to suggest different impact energies;
  enough to prevent metronomic uniformity.
- Pitch: ±8% (0.92–1.08). Sufficient to distinguish consecutive identical clips;
  not so wide as to suggest different shoe materials or body weights.
- Sprint bump: +10% volume. Conveys urgency and impact weight beyond what the
  detection system's BaseIntensity difference already represents.

**Example calculation — Metal Sprint step:**

```
entry = _audioData.GetFootstepEntry(SurfaceType.Metal, SpeedTier.Sprint)
clipIndex = Random.Range(0, 3) → clips[1] selected
volume = Random.Range(0.85, 1.00) → 0.91; +SprintVolumeBump(0.10) → min(1.0, 1.01) = 1.0
pitch  = Random.Range(0.92, 1.08) → 1.03
AudioManager.Instance.PlayAtPosition(SoundID.Footstep_Metal_Sprint, playerPos, 1.0, 1.03)
```

**Example calculation — Carpet CrouchWalk step:**

```
entry = _audioData.GetFootstepEntry(SurfaceType.Carpet, SpeedTier.CrouchWalk)
clipIndex = Random.Range(0, 2) → clips[0] selected
volume = Random.Range(0.85, 1.00) → 0.87; no sprint bump
pitch  = Random.Range(0.92, 1.08) → 0.95
AudioManager.Instance.PlayAtPosition(SoundID.Footstep_Carpet_CrouchWalk, playerPos, 0.87, 0.95)
```

Note: The acoustic quietness of carpet crouch-walk is achieved through clip
normalization at export (-30 LUFS target), not by reducing playback volume here.
Playback volume remains in the 0.85–1.00 range for all entries. Surface loudness
differentiation lives in the clip, not the playback path.

---

### F-FA-4: Minimum Interval Gate

```
if (Time.time - _lastFootstepTime < _audioData.MinTimeBetweenFootsteps):
    return  // suppress; do not play
else:
    _lastFootstepTime = Time.time
    // proceed to clip selection and playback
```

| Variable | Type | Range | Description |
|----------|------|-------|-------------|
| `_lastFootstepTime` | float | 0 – Time.time | Time of last footstep playback; initialized to -999f |
| `MinTimeBetweenFootsteps` | float | 0.01–0.5 s | Minimum interval between consecutive footstep plays (default 0.08 s) |

**Normal operation:** At sprint (6.7 events/s), the interval between PNE events
is ~0.15 s. The gate threshold of 0.08 s is comfortably below this, so no sprint
steps are suppressed in normal operation. The gate is a safety net, not a cadence
controller.

**When the gate fires:** If `OnSurfaceTypeResolved` fires twice within 0.08 s
(e.g., two subscribers firing in the wrong order, or a surface transition coinciding
with a step threshold crossing), the second call is suppressed silently. No warning
is logged in production builds — the gate is expected and invisible in normal play.

---

## 5. Edge Cases

| # | Scenario | Expected Behavior | Rationale |
|---|----------|------------------|-----------|
| EC-FA-01 | `AudioManager.Instance` is null when a footstep event fires (e.g., scene unload timing). | `PlayWithVariation` checks `AudioManager.Instance != null` before proceeding. If null: log `Debug.LogError`, return without playing. No `NullReferenceException`. | Fail gracefully. Audio silence during edge-case timing is preferable to a runtime crash. |
| EC-FA-02 | `AudioManager` pool is exhausted (all `SoundEmitter` instances are currently playing). | `AudioManager` handles pool exhaustion internally per its existing architecture (ObjectPool with `collectionCheck` disabled at runtime to avoid GC). If no emitter is available, the footstep is dropped silently. No exception in `FootstepAudioController`. | A missed footstep sound is an acceptable degradation. The detection consequence of the step has already been emitted by the PNE and is independent of audio playback. |
| EC-FA-03 | A `SoundID` has no `AudioClip` assigned in `SoundData` (null clip reference). | `GetFootstepEntry` returns an entry with a null or empty `Clips` array. `FootstepAudioController` checks `entry.Clips != null && entry.Clips.Length > 0` before calling `PlayWithVariation`. If check fails: log `Debug.LogWarning` with the SoundID name, return without playing. | Missing clip assignment is a production error (sound-designer's responsibility), not a runtime invariant. Warning is appropriate; crash is not. |
| EC-FA-04 | `OnSurfaceTypeResolved` fires twice within `MinTimeBetweenFootsteps` (0.08 s). | Second call is suppressed by the minimum interval gate (see F-FA-4). No audio plays. `_lastFootstepTime` is not updated by the suppressed call. | Double-firing is an edge case arising from event ordering or surface transitions at step-threshold coincidence. The gate prevents audible stutter. |
| EC-FA-05 | Player lands on a surface that has no `SurfaceTypeTag` (untagged collider). | PNE defaults to `SurfaceType.Stone` per PNE Edge Case EC-PNE-07. `FootstepAudioController` receives `SurfaceType.Stone` from the PNE event. Stone landing clip plays. No special handling in `FootstepAudioController`. | Stone is the fail-loud default. The audio consequence is consistent with the detection consequence (both default to Stone). |
| EC-FA-06 | Player moves across a surface type boundary between two PNE cadence ticks (e.g., half-step on stone, half-step on wood). | PNE fires one event per step threshold crossing with the surface type at the moment of crossing. `FootstepAudioController` plays the clip for whichever surface the PNE resolved. No blending or interpolation. | The PNE GDD specifies one raycast per cadence tick. Audio follows this contract exactly. No surface blending is in scope. |
| EC-FA-07 | Player spams hide entry/exit rapidly (multiple `OnHideEntryOccurred` / `OnHideExitOccurred` events in quick succession). | Each event fires its respective handler independently. No minimum interval gate on hide events (they are not footstep cadence; rapid toggling is gated at `PlayerHiding` level per PNE GDD). Both entry and exit clips play each time the events fire. If rapid toggling becomes an exploit, the gate is added at `PlayerHiding` — outside `FootstepAudioController` scope. | `FootstepAudioController` is not the exploit-prevention layer. It plays what PNE tells it. |
| EC-FA-08 | `FootstepAudioData` ScriptableObject is null at Awake (missing asset reference). | `FootstepAudioController.Awake` performs null check on `_audioData`. If null: log `Debug.LogError("[FootstepAudio] FootstepAudioData asset is not assigned.")`, set `enabled = false`. No further audio playback from this component. | Missing asset reference is a configuration error. Disabling the component prevents per-event NullReferenceExceptions flooding the console. |
| EC-FA-09 | Sprint step fires and `volume + SprintVolumeBump` exceeds 1.0. | Volume is clamped to `Mathf.Min(1f, volume + SprintVolumeBump)` in F-FA-3. No distortion or exception. | AudioSource volume above 1.0 causes distortion in Unity. The clamp is mandatory. |
| EC-FA-10 | `FootstepAudioController` is on a pooled Player prefab that is returned to pool mid-playback. | `OnDisable` unsubscribes all events. Any in-flight `SoundEmitter` from the pool continues playing to completion independently (it has already checked out from the AudioManager pool and is not coupled to the Player's lifecycle). | `SoundEmitter` is designed to play-to-completion independently. Player pooling does not cause clipped audio. |

---

## 6. Dependencies

### 6.1 Upstream: Systems This Depends On

| System | Direction | Contract | Notes |
|--------|----------|---------|-------|
| Player Noise Emitter | FAC subscribes | 5 C# events on `PlayerNoiseEmitter` (see 6.2) | FAC never raycasts; all surface data comes from PNE events |
| AudioManager | FAC calls | `AudioManager.Instance.PlayAtPosition(SoundID, Vector3, float volume, float pitch)` | AudioManager must expose optional volume and pitch parameters; see 6.4 |
| SoundLibrary / SoundID enum | FAC reads | All 27 SoundID values defined in Section 6.3 | SoundID enum ordering must match F-FA-1b |
| FootstepAudioData ScriptableObject | FAC reads | Clip arrays, volume/pitch ranges, spatial settings, gate value | Asset path: `Assets/_Project/Scripts/Data/FootstepAudioData.asset` |
| AudioMixer | FAC routes | `Footstep_Steps` and `Footstep_Actions` AudioMixerGroup references | Mixer asset: `Assets/_Project/Audio/AudioMixer.mixer` |

### 6.2 New Event Contract Requirements on PlayerNoiseEmitter

The following changes to `PlayerNoiseEmitter.cs` are required to support `FootstepAudioController`.
These are documented here as requirements on the PNE and must be communicated to the
game-designer / systems-designer who owns the PNE implementation.

**Change 1: Extend `OnSurfaceTypeResolved` payload to include `SpeedTier`.**

Current PNE GDD spec (Section 3.5):
```csharp
public static event Action<SurfaceType> OnSurfaceTypeResolved;
```

Required spec:
```csharp
public static event Action<SurfaceType, SpeedTier> OnSurfaceTypeResolved;
```

Rationale: `FootstepAudioController` must select the correct clip from the 18-entry
matrix (6 surfaces × 3 tiers). Without `SpeedTier` in the event payload, the FAC
would need to independently track the current speed tier — duplicating state already
owned by the PNE. Publishing both together keeps surface and tier authoritatively
co-located at the moment of emission, and eliminates any risk of a race condition
where speed tier changes between the PNE event and the FAC reading it.

**Change 2: Add four new dedicated typed events for non-footstep actions.**

```csharp
// In PlayerNoiseEmitter.cs:
public static event Action<SurfaceType> OnLandingOccurred;
public static event Action              OnInteractOccurred;
public static event Action              OnHideEntryOccurred;
public static event Action              OnHideExitOccurred;
```

These events are fired by the PNE at the same time as (or immediately after) the
corresponding `NoiseEmitter.Emit()` call for each action. They carry no additional
data beyond what is listed — landing carries `SurfaceType` because the landing
clip is surface-specific; interact, hide entry, and hide exit are surface-agnostic
at MVP.

Rationale for dedicated typed events over filtering `NoiseEmitter.OnNoiseEmitted`:
- Filtering the raw `NoiseEmitted` bus by intensity or SurfaceType to identify
  "this is a landing event" is fragile — intensity values could overlap, and Neutral
  SurfaceType is shared by throw-origin events as well.
- Typed events are self-documenting and zero-overhead (no filtering logic in FAC).
- The pattern is consistent with the PNE's existing `OnSurfaceTypeResolved` and
  `OnPlayerNoiseLevelChanged` events.

**Change 3: `OnLandingOccurred` requires surface type.**

The PNE already performs a downward raycast at landing time (per Section 3.4 of
the PNE GDD: "Downward raycast result" for SurfaceType on landing). The resolved
`SurfaceType` from that raycast must be published in `OnLandingOccurred`. No
additional raycast in FAC.

### 6.3 SoundID Enum Additions

The following 27 values must be added to the `SoundID` enum in `SoundLibrary.cs`.
They must be declared contiguously in the specified order to support the F-FA-1b
index formula. Do not insert other enum values between `Footstep_Stone_Walk` and
`Footstep_Water_Sprint`.

```csharp
// In SoundID enum (SoundLibrary.cs) — add after existing entries:

// ---- Footstep Audio: 18 footstep entries (surface × tier, row-major) ----
// Order: Stone, Wood, Metal, Dirt, Carpet, Water × Walk, CrouchWalk, Sprint
Footstep_Stone_Walk,            // index 0
Footstep_Stone_CrouchWalk,      // index 1
Footstep_Stone_Sprint,          // index 2
Footstep_Wood_Walk,             // index 3
Footstep_Wood_CrouchWalk,       // index 4
Footstep_Wood_Sprint,           // index 5
Footstep_Metal_Walk,            // index 6
Footstep_Metal_CrouchWalk,      // index 7
Footstep_Metal_Sprint,          // index 8
Footstep_Dirt_Walk,             // index 9
Footstep_Dirt_CrouchWalk,       // index 10
Footstep_Dirt_Sprint,           // index 11
Footstep_Carpet_Walk,           // index 12
Footstep_Carpet_CrouchWalk,     // index 13
Footstep_Carpet_Sprint,         // index 14
Footstep_Water_Walk,            // index 15
Footstep_Water_CrouchWalk,      // index 16
Footstep_Water_Sprint,          // index 17

// ---- Landing: 6 entries (one per surface, Neutral excluded) ----
Landing_Stone,
Landing_Wood,
Landing_Metal,
Landing_Dirt,
Landing_Carpet,
Landing_Water,

// ---- Actions: 3 entries (surface-agnostic) ----
Footstep_Interact,
Footstep_HideEntry,
Footstep_HideExit,
```

Total new SoundIDs: **27** (18 footstep + 6 landing + 3 actions).

### 6.4 AudioManager API Coordination

`AudioManager.PlayAtPosition` must accept optional `volume` and `pitch` override
parameters. If the current implementation signature is:

```csharp
public void PlayAtPosition(SoundID soundId, Vector3 worldPosition)
```

It must be extended to:

```csharp
public void PlayAtPosition(SoundID soundId, Vector3 worldPosition,
                           float volume = 1f, float pitch = 1f)
```

This is a non-breaking additive change (default parameters preserve backward
compatibility). Delegate to `lead-programmer` for implementation. Do not modify
AudioManager code directly from `FootstepAudioController`.

### 6.5 Downstream: Systems That Depend on This

| System | Direction | Notes |
|--------|----------|-------|
| None (this system has no downstream dependents) | — | FootstepAudioController is a leaf node in the dependency graph. It produces audio output only. |

---

## 7. Tuning Knobs

All values authored in `FootstepAudioData.asset`. No values hardcoded in `FootstepAudioController`.

| Knob | Category | Default | Safe Range | Effect of Increase | Effect of Decrease |
|------|---------|---------|------------|-------------------|--------------------|
| `MinTimeBetweenFootsteps` | Gate | 0.08 s | 0.01–0.5 s | Wider gate; more suppression (do not exceed sprint interval ~0.15 s or sprint steps will be silenced) | Narrower gate; double-fire more likely on edge cases |
| `SprintVolumeBump` | Mix | 0.10 | 0.0–0.3 | Sprint footsteps louder; urgency more pronounced | Sprint sounds same as walk in volume; tier distinction reduced |
| Per-entry `VolumeMin` | Mix | 0.85 | 0.5–1.0 | Less low-end variation; steps feel more uniform | More variation; some steps noticeably quieter than others |
| Per-entry `VolumeMax` | Mix | 1.00 | 0.85–1.0 | Ceiling for variation; diminishing returns above 1.0 | Reduces overall footstep loudness for that entry |
| Per-entry `PitchMin` | Variation | 0.92 | 0.8–1.0 | More pitch variation downward; heavier irregular steps | Minimal pitch variation; very consistent pitch per step |
| Per-entry `PitchMax` | Variation | 1.08 | 1.0–1.3 | More pitch variation upward; lighter, irregular steps | Minimal pitch variation |
| `SpatialBlend` | Spatial | 1.0 | 0.7–1.0 | Fully 3D; footsteps are clearly positional | Partially 2D; footsteps feel less grounded in the world |
| `MaxDistance` | Spatial | 20 m | 10–40 m | Footsteps audible across larger distances | Footsteps fade very close; intimate but may feel limited |
| `MinDistance` | Spatial | 1 m | 0.5–3 m | Full-volume zone larger; footsteps feel bigger | Footsteps begin fading immediately from source |
| Clip export LUFS (Stone Walk) | Asset | -22 LUFS | -28 to -16 LUFS | Footstep bus overall louder; adjust mixer fader proportionally | Footsteps quieter at the source; require mixer boost which reduces headroom |
| Metal / Carpet relative loudness spread | Mix | 12 dB spread | 8–18 dB | Greater surface distinction; easier to learn hierarchy | Less distinction; carpet and metal become hard to tell apart |

---

## 8. Acceptance Criteria

| # | Criterion | Test Method | Pass | Fail |
|---|-----------|------------|------|------|
| AC-FA-01 | Each of the 6 surface types produces an audibly distinct footstep sound at Walk tier. | Playtest with 5 testers blind to surface type; ask to rank surfaces by sound. | All 5 testers correctly identify at least 4 of 6 surfaces as distinct. | Any two surfaces are indistinguishable by all testers. |
| AC-FA-02 | Metal Walk footstep is perceptibly louder than Carpet Walk footstep at the same playback position. | A/B playback test: play Metal Walk and Carpet Walk clips at equal AudioSource volume. | At least 4 of 5 testers identify Metal as louder. | Carpet is rated equal to or louder than Metal. |
| AC-FA-03 | Sprint footstep sounds differ in character from Walk footstep on the same surface — not merely louder. | Blind A/B listening test: play Walk and Sprint clips for Stone. Ask "different recordings or same recording louder?" | At least 4 of 5 testers identify Sprint as a different recording. | Testers report Sprint sounds like Walk played louder. |
| AC-FA-04 | Sprint footsteps fire more frequently per second than Walk footsteps (audio events, not detection events). | PlayMode test: count `OnFootstepOccurred` handler invocations in 5 simulated seconds at Walk speed and Sprint speed. | Sprint invocation count > Walk invocation count × 2 (PNE cadence: 6.7 vs 2.1 events/s). | Sprint count ≤ Walk count, or approximately equal. |
| AC-FA-05 | Landing SFX on any surface is perceptibly heavier than the Walk footstep on the same surface. | A/B listening test: play Walk and Landing clips for Stone, Wood, Metal. | At least 4 of 5 testers identify Landing as heavier/more impactful on all 3 tested surfaces. | Landing rated equal to or lighter than Walk on any surface. |
| AC-FA-06 | No two consecutive footstep events within 0.08 s produce two audio clips playing (minimum interval gate is active). | EditMode test: fire `OnSurfaceTypeResolved` twice with 0.05 s gap; assert `AudioManager.PlayAtPosition` called exactly once. | PlayAtPosition called once. Second call suppressed. | PlayAtPosition called twice within minimum interval. |
| AC-FA-07 | Hide Entry SFX is audibly distinct from any footstep clip. | A/B listening test: play Hide Entry clip and Stone Walk clip. | At least 4 of 5 testers identify them as different sound types. | Testers report Hide Entry sounds like a footstep. |
| AC-FA-08 | Hide Exit SFX is audibly distinct from Hide Entry SFX. | A/B listening test. | At least 4 of 5 testers identify them as different sounds. | Testers cannot distinguish entry from exit. |
| AC-FA-09 | `FootstepAudioController` does not call `AudioSource.Play` directly anywhere in its implementation. | Static code analysis: grep for `AudioSource.Play` in `FootstepAudioController.cs`. | Zero occurrences. | Any direct `AudioSource.Play` call present. |
| AC-FA-10 | `FootstepAudioController` does not call `Physics.Raycast` or any physics query in any method. | Static code analysis: grep for `Raycast`, `OverlapSphere`, `Physics.` in `FootstepAudioController.cs`. | Zero occurrences. | Any physics query present. |
| AC-FA-11 | `FootstepAudioController` unsubscribes all events in `OnDisable` with no missed events. | EditMode test: enable component, fire all 5 events, assert 5 handlers called. Disable component, fire all 5 events, assert 0 handlers called. | 5 calls when enabled. 0 calls when disabled. | Any call reaches a handler after `OnDisable`. |
| AC-FA-12 | Null `FootstepAudioData` asset disables the component and logs an error without throwing. | EditMode test: assign null to `_audioData`, call `Awake`, assert `enabled == false` and no unhandled exception. | Component disabled. Error logged. No exception. | Exception thrown, or component remains enabled and crashes on next event. |
| AC-FA-13 | Null `SoundID` clip assignment logs a warning and does not crash. | EditMode test: assign a `FootstepClipEntry` with empty `Clips` array for `Footstep_Stone_Walk`, fire event, assert warning logged and no exception. | Warning logged. No exception. No audio plays. | Exception thrown, or warning not logged. |
| AC-FA-14 | All 6 surface landing sounds are registered (no missing landing entry crashes). | EditMode test: for each SurfaceType 0–5, call `GetLandingEntry(surface)` and assert non-null with at least 1 clip. | All 6 return valid entries. | Any returns null or empty. |
| AC-FA-15 | At a listening distance of 15 m, footstep volume has rolled off to near-inaudible (< -20 dB of close-range level) using logarithmic rolloff at MaxDistance = 20 m. | PlayMode test: position AudioListener 15 m from player, record peak amplitude of footstep playback, compare to 1 m distance measurement. | 15 m amplitude < 10% of 1 m amplitude (logarithmic: approximately -20 dB). | Footstep still clearly audible at 15 m (linear rolloff or incorrect MaxDistance). |
| AC-FA-16 | The same footstep clip variant does not play on 3 or more consecutive steps in normal walk cadence (random pool operating correctly). | PlayMode test: walk 20 steps on Stone, record clip indices selected. Assert no run of 3 identical consecutive clips. | Maximum identical run = 2. | Run of 3 or more identical consecutive clips. |
| AC-FA-17 | `FootstepAudioController` events do not fire when the component is on a disabled GameObject. | PlayMode test: disable player GameObject, fire PNE events from external test harness, assert `PlayAtPosition` not called. | Zero PlayAtPosition calls. | Audio plays from a disabled GameObject. |

---

## Asset Naming Convention

All audio files for this system follow the project naming convention:
`[category]_[context]_[name]_[variant].[ext]`

**Footstep clips:**
`sfx_footstep_[surface]_[tier]_[variant].ogg`

Examples:
- `sfx_footstep_stone_walk_01.ogg`
- `sfx_footstep_stone_walk_02.ogg`
- `sfx_footstep_metal_sprint_01.ogg`
- `sfx_footstep_carpet_crouchwalk_01.ogg`
- `sfx_footstep_water_walk_03.ogg`

**Landing clips:**
`sfx_footstep_[surface]_land_01.ogg`

Examples:
- `sfx_footstep_stone_land_01.ogg`
- `sfx_footstep_metal_land_01.ogg`

**Action clips:**
- `sfx_footstep_interact_01.ogg`
- `sfx_footstep_hideentry_01.ogg`
- `sfx_footstep_hideexit_01.ogg`

**Asset delivery path:** `Assets/_Project/Audio/Footstep/`

**Format:** OGG Vorbis, quality 7 (approx 160 kbps). Mono for all footstep clips
(footstep sources are positional — stereo field is irrelevant and doubles file size).
Sample rate: 44.1 kHz. No compression artifacts on transients (verify landing clips
in particular — short, impactful transients are susceptible to OGG ringing).

---

## Open Questions

| Question | Owner | Deadline | Resolution |
|----------|-------|----------|-----------|
| Does `AudioManager.PlayAtPosition` currently accept `volume` and `pitch` override parameters? If not, what is the preferred API extension pattern — default parameters or an overload? | lead-programmer | Before FAC implementation begins | TBD |
| Should `FootstepAudioController` track `SpeedTier` internally (redundant state), or is the PNE event payload extension (adding `SpeedTier` to `OnSurfaceTypeResolved`) the agreed contract? This GDD recommends the payload extension. | game-designer + audio-director | Before PNE implementation begins | Recommended: extend payload per Section 6.2 |
| Water footstep: does the water surface require a particle/VFX splash trigger on footstep in addition to audio? If so, what system owns the VFX trigger? | game-designer + vfx-designer | Vertical Slice planning | TBD |
| No-repeat-last algorithm for random clip selection (F-FA-2 upgrade): implement at MVP or defer to Vertical Slice after playtest evidence? | audio-director + sound-designer | First playable build playtest | Recommended: defer to Vertical Slice unless pool size = 2 causes audible repetition |
