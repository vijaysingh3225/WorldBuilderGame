# Combat Lab diagnostic harness

The diagnostic harness is the shared interface between creator taste, AI implementation, and the running game. It does not decide whether movement or combat feels good. It makes the exact implementation state reproducible and legible so creator feedback can be translated into smaller, evidence-backed changes.

## Three capture levels

### Free-play capture

Play the Combat Lab normally and press **F9** to begin or end a recording. Press **F10** at the moment something looks right or wrong. Each marker records the synchronized frame and a gameplay screenshot. The red recording indicator confirms that capture is active.

Free-play capture observes the current game without replacing player input. It is the best tool for a behavior that only appears during natural play or an unusual input sequence. In the Editor, the recorder is stamped with the current source revision when Play mode begins. If Play mode or the recorder stops before the closing F9 press, the partial evidence is still written as an explicitly aborted run instead of being lost or reported as complete.

### Deterministic full-scene suite

Open **WorldBuilder -> Diagnostics -> Combat Lab Diagnostics** and select **Run Deterministic Full Suite**. The suite opens the real generated Combat Lab and drives the production `PlayerInputSource`, `ThirdPersonMotor`, Animator, CharacterController, `MeleeWeapon`, `DamageService`, `Health`, and `GameplayEventLog` at a frame-locked 60 Hz.

The current suite covers:

- idle, forward walk, stop, sprint, sprint release, and stop;
- 180-degree sprint reversal, independent right and left sprint turns, and alternating sprint direction changes;
- crouch idle, crouch movement, and crouch exit;
- idle and running jumps through takeoff, apex, fall, and landing;
- passive-dummy behavior, accepted out-of-range miss, in-range hit, cooldown rejection, lethal damage, overkill, death, and event ordering.

The suite temporarily replaces device input at the existing input boundary. It does not call a second motor or fake combat implementation. Its scenario runner is an Update-driven transaction: each phase and frame command is committed before production input sampling and motor simulation, then the recorder samples the resulting frame in `LateUpdate`. This keeps recorded intent, motor state, animation, events, and screenshots on the same sample instead of shifting intent one frame ahead.

Suite time is derived from the sample index at the fixed 60 Hz step. Wall-clock time and wall delta are preserved separately for diagnosing capture stalls and editor overhead, but they do not affect duration, acceleration, jerk, jump timing, or other simulation metrics. Timing, input overrides, transforms, and temporary Animator settings are restored when a run finishes or aborts.

A deterministic run is valid only after every required named phase completes exactly once and the runner publishes its completion event. Stopping Play mode early, timing out, encountering an unfiltered Unity error, missing a required phase, or otherwise aborting produces an incomplete failed report with an abort reason; a partial run cannot report success. The scenario runner and recorder use idempotent completion so cleanup cannot write a second contradictory result.

For unattended validation, use:

```text
Unity.exe -batchmode -projectPath <project> -executeMethod WorldBuilder.Editor.CombatLabDiagnosticsOrchestrator.RunBatch -logFile <log>
```

The deterministic combat sequence also marks `slash-windup`, `slash-contact`, `slash-follow-through`, `slash-recovery`, and `slash-hit-contact` screenshots. Use those together with the attack-start, attack-resolved, and damage events when changing weapon animation or hit timing.

### Isolated animation-cycle capture

Use **Capture Isolated Animator Cycles** for high-resolution gait analysis. It samples 60 points across each walk, jog, and sprint cycle and renders 16 front/side/rear poses. This capture is now immutable: it validates the assets already present and never rebuilds or changes them while measuring.

## Artifact contract

Every full-scene run is preserved under:

```text
Artifacts/CombatLabDiagnostics/runs/<timestamp>-<kind>/
```

`Artifacts/CombatLabDiagnostics/latest.json` points to the newest run, and `AI_LATEST.md` contains its compact handoff. A run contains:

- `report.json`: schema version, source revision, completion or abort status, captured controller/Animator/weapon/camera configuration, environment, capabilities, automated checks, and per-phase summaries;
- `ai_report.md`: ranked failures/warnings and the most useful phase metrics;
- `frames.csv`: synchronized deterministic and wall clocks, device intent, motor, physics, current/next Animator state, calibrated pose bones and contact probes, camera, weapon, enemy, and health state for every frame;
- `phase_summary.csv`: compact rows suitable for rapid comparisons;
- `events.csv` and `events.jsonl`: exact-sample ordered attack, rejection, overlap, requested/effective/overkill damage, death, and gameplay events;
- `markers.csv` and `screenshots/`: event-aligned visual evidence;
- `timeline.svg`: aligned speed, target speed, vertical velocity, foot gaps, enemy health, weapon cooldown, and phase boundaries;
- `comparison.json` and `comparison.md`: changes from the creator-accepted baseline, when one exists;
- `creator_review.json`: the creator's own verdict and language, when saved in the window.

The current artifact contract is schema v2 and is versioned by `GameplayDiagnosticSchema.Version`. Change the version when fields or meanings become incompatible. Screenshot capability is reported only for files that were actually written and verified, not merely requested.

## Accepted baselines and taste

The harness never silently treats the newest run as correct. In the diagnostics window:

1. Save an **Accepted**, **Mixed**, or **Rejected** review with the creator's words.
2. Only a completed, functionally passing deterministic full suite using the current schema can be promoted.
3. Promotion requires a persisted `creator_review.json` whose **Accepted** verdict and run ID match that exact run; an in-memory selection, free-play capture, stale review, or newest-run assumption is insufficient.
4. The accepted report is stored at `Assets/_Project/Diagnostics/AcceptedCombatLabBaseline.json` and is therefore durable and version-controlled.
5. Later runs compare the union of named behaviors and metrics against that accepted state, explicitly reporting missing and newly introduced phases.

A baseline is evidence of what was accepted, not a universal threshold. A change may deliberately move a metric away from the baseline when the creator asks for a different result.

No schema-v2 Combat Lab baseline has been accepted yet. Current reports are diagnostic candidates only until the creator explicitly accepts one.

## Required AI iteration loop

For movement, animation, camera, or combat changes:

1. Read the newest creator review and accepted baseline before editing.
2. Capture the relevant current behavior if no comparable run exists.
3. Make one bounded observable change.
4. Run the isolated animation capture for clip-only work or the full suite for controller, transition, physics, camera, or combat work.
5. Read `AI_LATEST.md`, `report.json`, `comparison.md`, relevant phase rows, correlated events, and marked screenshots.
6. Reject a candidate when the data exposes an unintended regression, even if it compiles.
7. Hand the candidate to the creator for taste validation. Never promote the baseline without explicit acceptance.

## Metric interpretation

- Calibrated heel and toe sole probes expose hovering or penetration without treating an arbitrary ankle transform as the floor. Calibration is captured from stable standing samples.
- Contact slip measures horizontal world-space travel only across contiguous samples near the lowest portion of each foot's gait cycle, and reports the qualifying contact sample count and rate. It does not count the whole gait or vertical foot travel as planted slip.
- Crouch summaries combine rear-knee surface gap, pelvis standing-height ratio, rear hip-to-heel distance and offset, knee flexion, front-foot plant error, split stance, and position-derived spine pitch. These are geometric review aids, not an automatic definition of a tasteful tactical pose.
- Frame travel, acceleration, and jerk expose discontinuities and snapping.
- Velocity-, desired-, pose-, and shoulder-facing errors separate motor direction from visual alignment.
- Elbow lateral range and head/chest motion expose unwanted side pumping or unstable posture.
- Left/right hand travel, sword direction, sword-to-forearm angle, blade-plane normal, attack activity, and plane-alignment error expose damped weapon-arm motion, an unlocked wrist, or a slash whose edge rolls away from its intended cutting plane.
- Reversal braking ratio distinguishes deliberate stop-turn behavior from instant rotation.
- Requested, effective, and overkill damage keep combat presentation honest at low health.
- Source revision and captured configuration distinguish a behavioral change from a different controller, Animator, weapon, or camera setup.
- Automated failures protect completion and functional invariants. Warnings identify likely review targets and are not substitutes for creator judgment.

The known Unity Search indexing `ArgumentOutOfRangeException` is excluded from diagnostic failure status because it is unrelated to gameplay. All other errors, exceptions, and assertions fail an orchestrated run.
