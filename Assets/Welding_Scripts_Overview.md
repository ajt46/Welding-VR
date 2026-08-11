## Welding Project – Script Overview

This document explains what the main scripts do and how they are typically wired to objects, so another developer can understand and extend the system.

---

### Core Welding Flow (Custom welding path)

- **`CustomWeldingController.cs`**  
  - **Attached to**: MIG welding gun object.  
  - **Purpose**: Handles raycasting from the gun tip, spawning weld blobs, scaling them based on travel speed, overheating into holes, and sending travel timing data to `WeldingPanel`.  
  - **Key fields**:
    - `weldingTip`: child transform at the gun nozzle.
    - `weldBlobPrefab`: prefab for a weld blob (layer = blobLayer, tag = `"WeldObject"`).
    - `weldableLayers`: layer mask for weldable surfaces (usually layer 7).
    - `blobLayer`: layer index used for blobs (usually layer 6).
    - `blobsPerSecond`: rate of blob creation while welding.
    - Speed settings: `useSpeedBasedSizing`, `idealTravelSpeed`, `minBlobWidth/Height`, `maxBlobWidth/Height`.
  - **Behaviour**:
    - `Update()` raycasts from `weldingTip` forward to detect weldable surfaces / blobs and draws a debug ray.
    - When `StartWelding()` is called each frame:
      - Waits for `weldingStartDelay`, then sets `isWelding = true` while tip is on a weldable surface and not overheating.
      - While `isWelding`, increments internal timers and spawns blobs at a fixed interval (`blobsPerSecond`), either growing an existing blob under the tip or instantiating a new one.
    - When a new blob is created on a panel that has `WeldingPanel`, it calls `AddWeldTravel()` with accumulated `travelTimer` so the panel can calculate travel uniformity.
    - `StopWelding()` resets welding state and finalizes the current blob.
    - `BlockWeldingForSeconds(float seconds)` sets a short “cooldown” (used by `WeldSpeedEvaluator`) so no blobs can be added for a brief period after evaluation.

- **`CustomWeldingBlob.cs`**  
  - **Attached to**: the custom weld blob prefab (`weldBlobPrefab`) if used.  
  - **Purpose**: Handles hot → cool material transition and optional mesh swap after cooling.  
  - **Behaviour**:
    - Starts with `hotMaterial` and slowly fades color/emission toward a cooled look over `coolingFadeTime` after `coolingDelay`.
    - Optionally swaps to `cooledMaterial` / `cooledMesh` at the end.
    - `Reheat()` resets the blob to hot and restarts the cooling routine.

- **`CustomWeldingSurface.cs`**  
  - **Attached to**: any object that should be weldable (optional if using example `WeldingPanel` directly).  
  - **Purpose**: Simple helper to ensure the object sits on the correct weldable layer and optionally define a “good weld” area and path.  
  - **Fields**:
    - `surfaceLayer`: the layer index for weldable surfaces (typically 7).
    - `goodWeldArea`: a collider defining the ideal weld area.
    - `weldPathPoints`: waypoints for a scanner to move along the weld path.

---

### Existing Example System (Panel + Handle)

- **`WeldingPanel.cs`**  
  - **Attached to**: parent object of a weldable panel (e.g. `MyWeldPanel`).  
  - **Purpose**: Evaluates weld quality over a panel (uniformity, coverage, travel, bad welds, holes) and runs a scanning visual (`weldScanner`).  
  - **Key fields**:
    - `weldingCollider`: collider that defines the “good weld” line/area.
    - `panels`: child transforms that represent panel surfaces (contain blobs).
    - `blobErrorMat`, `blobGoodMat`: materials used to recolor bad/good blobs after evaluation.
    - `weldScanner`: prefab with `WeldCheckerLight` and audio that scans along `checkingTransforms`.
    - `checkingTransforms`: transforms defining the scanner’s path.
  - **Behaviour**:
    - `PopulateWeldingStats` spawns the scanner, traces along `checkingPoints` with LeanTween, raycasts down to detect blobs, and accumulates statistics in `WeldingStats`.
    - Recolors blobs under `weldingCollider` as good and blobs in `panels` as bad for visual feedback.

- **`WeldingBlobSet.cs`** (from example)  
  - **Attached to**: example blob prefab used by `WeldingHandle`.  
  - **Purpose**: Controls mesh/material switching between hot and cooled states, plus visual tilt between blobs.

- **`WeldingHandle.cs`** (from example)  
  - **Attached to**: example welding gun prefab.  
  - **Purpose**: Original welding logic provided by the example project: raycasts, blob creation/growth, overheating, and travel logging.  
  - **Note**: In this project, `CustomWeldingController` provides an alternative implementation, but the handle is still a reference for how the example integrates with `WeldingPanel`.

---

### VR Input + Sparks

- **`weldingsparks.cs`**  
  - **Attached to**: MIG gun object (or a child containing the sparks particle system).  
  - **Purpose**: Ties BNG `Grabbable` + `InputBridge` trigger input to:
    - Show/hide sparks.
    - Start/stop `CustomWeldingController` welding.  
  - **Behaviour**:
    - Finds a `ParticleSystem`, `Grabbable`, and optionally a `CustomWeldingController` on this or parent objects.
    - If the gun is being held and the right trigger is pressed above `triggerThreshold`, it:
      - Enables particle emission and plays the sparks.
      - Calls `StartWelding()` on `CustomWeldingController` every frame.
    - When conditions are not met, it stops emission and calls `StopWelding()` once.

- **`WeldingTriggerAndSparks.cs`**  
  - **Attached to**: welding gun (especially when using the original `WeldingHandle`).  
  - **Purpose**: Bridge VR (BNG) input or optional “legacy” input to `WeldingHandle`, and strictly control spark visibility.  
  - **Key behaviour**:
    - On `Awake/OnEnable/Start`, forcibly turns sparks off and clears any prewarmed particles.
    - Each frame:
      - Uses `Grabbable.BeingHeld` + `InputBridge.RightTrigger` (or legacy input) to compute `triggerHeld`.
      - Calls `weldingHandle.GetWeldPoint()` and then `StartWelding()` while pressed, `StopWelding()` when released.
      - Turns the sparks object fully on/off (including emission, renderer, and GameObject active state) based on trigger state.

---

### Weld Speed Evaluation (Fast / Good / Slow)

- **`WeldSpeedEvaluator.cs`**  
  - **Attached to**: a manager object in the scene.  
  - **Purpose**: Measures how long it takes the weld tip to move from a start point (X1) to an end point (X2) while the trigger is held, then classifies the weld as “Too fast”, “Good”, or “Too slow”. Also handles clearing blobs and short blocking of welding after evaluation.  
  - **Key fields**:
    - `startPoint`, `endPoint`: empty transforms for X1 / X2.
    - `weldTip`: transform at the gun tip (same used by the welding controller).
    - `fastWeldObject`, `goodWeldObject`, `slowWeldObject`: GameObjects that visually represent each result (set inactive by default).
    - `resultText`: TextMeshPro text to show `"Too fast"`, `"Good"`, or `"Too slow"`.
    - `idealTravelSpeed`, `timeTolerance`: define the ideal time window.
    - `clearBlobsOnEnd`: if true, destroys all objects tagged `"WeldObject"` (except the result objects).
    - `weldingController`: optional reference to `CustomWeldingController`.
    - `postEvaluationBlockSeconds`: duration to block welding (via `BlockWeldingForSeconds`) after evaluation finishes.
  - **Behaviour**:
    - When the tip is within `pointRadius` of `startPoint` and the trigger goes above `triggerThreshold`, it starts timing.
    - While timing and still holding the trigger, when the tip reaches `endPoint`, it stops timing and:
      - Computes total time vs. ideal time; decides fast/slow/good.
      - Activates the corresponding result object and writes matching text.
      - Optionally clears all weld blobs.
      - Optionally blocks welding for a short time via `CustomWeldingController`.

---

### Angle / PSI and Orientation Displays

- **`angletopsi.cs`**  
  - **Attached to**: a knob or rotating control object.  
  - **Purpose**: Maps the object’s rotation to a PSI value, and displays it in TextMeshPro text.  
  - **Key fields**:
    - `target`: transform whose rotation drives PSI (defaults to this transform).
    - `localAxis`: axis around which to measure rotation (e.g. `Vector3.up`).
    - `maxAngle`: angle (degrees) from the initial rotation that maps to `maxPSI`.
    - `maxPSI`: maximum PSI value (e.g. 50).
    - `psiStep`: rounding step (e.g. 5 → 0, 5, 10, …).
    - `psiText`: TextMeshPro text for output.
  - **Behaviour**:
    - Stores `initialRotation` in `Start()`.
    - In `Update()`, computes signed angle from that start around `localAxis`, clamps it to `[0, maxAngle]`, maps linearly to `[0, maxPSI]`, snaps to `psiStep`, and writes `"NN PSI"` into the text.

- **`AngleDisplayEuler.cs`**  
  - **Attached to**: any GameObject (often a UI manager or Canvas).  
  - **Purpose**: Displays pitch, yaw, and roll of a target transform as TextMeshPro text.  
  - **Fields**:
    - `target`: transform to read orientation from (e.g. MIG gun).
    - `angleText`: TextMeshPro text to show the angles.
  - **Behaviour**:
    - Reads `target.rotation.eulerAngles` and prints:
      - `Pitch` from `x`
      - `Yaw` from `y`
      - `Roll` from `z`

---

### Clamping / Mating System

- **`ClampAttach.cs`**  
  - **Attached to**: the **male** parent object (movable / grabbable).  
  - **Purpose**: Locks the male object to a female point when its `malePoint` reaches the `femalePoint`, and unlocks when the male object is grabbed.  
  - **Key ideas**:
    - Uses `malePoint` (child transform) and `femalePoint` (a transform on the female object) to compute the correct position and rotation for the parent.
    - Once locked, applies the lock transform in `FixedUpdate` so physics and interaction stay aligned.

- **`ClampAttachIn.cs`**  
  - **Attached to**: the **female** parent object (stationary or reference).  
  - **Purpose**: Provides a `femalePoint` transform that `ClampAttach` can snap to. Also draws Gizmos in the editor to visualize the attach point.

---

### Miscellaneous / Utility

- **`voltageknobrotate.cs`**  
  - **Attached to**: a knob object.  
  - **Purpose**: Rotates a knob in 10‑degree increments around its local Y axis while keeping position fixed (used for voltage or similar discrete settings).

- **`MIG_Welder_Sanity_Check.md`**, **`Custom_Welding_Setup_Guide.md`**, **`How_To_Create_Layers.md`**, **`Troubleshooting_Errors.md`**  
  - **Purpose**: Documentation and checklists for setting up layers, verifying behaviour, and diagnosing common Unity / Android build issues.

---

### Typical Object Wiring Summary

- **MIG Gun**:
  - `CustomWeldingController` (or `WeldingHandle` in the example rig).
  - `weldingsparks` or `WeldingTriggerAndSparks` (depending on which welding logic is used).
  - `Grabbable` (BNG).
  - Tip child: assigned to `weldingTip` / `weldTip`.

- **Weldable Panel**:
  - Parent object with `WeldingPanel`.
  - Child meshes/colliders on weldable layer (e.g. 7).
  - Blob prefab on blob layer (e.g. 6) with tag `"WeldObject"` and blob script (`CustomWeldingBlob` or `WeldingBlobSet`).

- **Speed Evaluation Setup**:
  - `WeldSpeedEvaluator` on a manager object.
  - `startPoint` and `endPoint` empties in the scene.
  - `weldTip` = gun tip transform.
  - `fastWeldObject`, `goodWeldObject`, `slowWeldObject` = result visuals (inactive by default).
  - `resultText` = TextMeshPro text for “Too fast / Good / Too slow”.
  - `weldingController` = reference to `CustomWeldingController` (optional but recommended).

This overview should give a new developer enough context to trace how player input leads to welding effects (blobs, sparks), how panels evaluate weld quality, and how timing / angle / PSI displays are driven from object transforms.  

