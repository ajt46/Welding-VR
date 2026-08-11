# Welding-VR
v1

# WeldingDemo — Lab Playbook

How to run and teach the WeldingDemo scene. Written for operators and instructors, not as a developer API reference.

There are **two ways to finish the demo**. You can do either path on its own; they share the same weld gun, power/gas/ground rules, and many of the same “why won’t this snap?” gotchas.

| Path | What you build | Clamp role |
|---|---|---|
| **1. Welding Panel** | Seat a metal panel on the welding sheet, ground the clamp on the panel, weld / grade travel speed | Single panel clamp seat (Path 1) |
| **2. Frame** | Place refs + weld bars, spot-weld, merge into one frame, flip/reposition, weld lines | Ordered clamp seats (Path 2) that advance when **welds** finish |

---

## 1. Welding Panel path

**Goal:** Choose a material panel, snap it to the sheet, ground the work clamp on the panel, set machine settings for that material, then weld. Optionally run the tip from the start mark to the end mark for a speed grade (too fast / good / too slow / bad settings).

### Steps

1. **Pick a panel** — Mild, Stainless, or Aluminium. Only one workpiece can sit on the sheet at a time.
2. **Grab the panel** and bring it near the welding sheet. An example / ghost of the correct placement appears when you are close enough and the sheet is free.
3. **Seat the panel** by holding it into the sheet snap zone (distance and/or collision). Once seated it freezes in place; the placement ghost usually hides.
4. **Grab the work clamp.** The Path 1 clamp ghost only becomes available after a panel is properly on the sheet.
5. **Seat the clamp** on the panel’s clamp ghost (hold it into the snap target until it clicks in). Ground is now good for the gun.
6. **Prep the machine** — MIG power ON, gas ON, and voltage / wire speed / gas flow dialed for that panel’s material.
7. **Weld** — grab the gun, get the tip on the surface, hold the trigger. After a short hold delay, sparks can become weld blobs. Optional: travel the graded path for speed feedback.

### Clamp note (panel)

Path 1 is a single seat: panel must be on the sheet, then clamp on the panel. Snapping the clamp is what makes “ground” true for the gun. If both Path 1 and Path 2 are somehow eligible, the closer ghost wins while you hold the clamp.

### Ghosts & examples (panel)

- **Sheet placement ghost** — shows while you hold an allowed panel near the empty sheet; hides after a successful seat (when configured that way).
- **Path 1 clamp ghost** — shows while you hold the clamp and Path 1 is eligible (panel seated / sheet occupied as wired).
- **Weld guides on the sheet** — scene-specific; usually not tied to the frame’s Path 2 clamp stages.

### Common panel-path problems

| What you see | What usually went wrong |
|---|---|
| No sheet ghost | Panel not held, too far, wrong panel for that sheet, or another panel already seated |
| Won’t snap to sheet | Still in cooldown after a pickup, not held, wrong collider / asset match, or sheet snap colliders off while the ghost is hidden |
| No Path 1 clamp ghost | Panel not seated yet, or Path 2 is closer and also eligible |
| Clamp won’t seat | Not held, cooldown, asset mismatch, or not touching the snap target |
| Gun says ground is off | Clamp is not seated / grounded |
| Panel drifts or tip contact is flaky | Workpiece physics not frozen after seat (see Physics gotchas) |

---

## 2. Frame path

**Goal:** Assemble reference pieces and weld bars, spot-weld the dots, merge into one jointed frame, flip and reposition on example frames, weld corner / top / bottom lines, and move the clamp through Path 2 stages as those welds complete.

Exact order depends on scene wiring, but the usual teaching flow looks like this.

### Steps

1. **Ref pieces**  
   Grab-me cues pulse until first grab. Hold a ref near its ghost, snap it in, let it freeze. Some refs later unlock a **second** placement after certain welds or lines finish.

2. **Weld bars**  
   Often armed after refs are ready. Snap all four bars into their ghosts. Many setups then require the eight spot welds before the bars become one piece.

3. **Spot welds (top / bottom dots)**  
   Sequence unlocks when bars are snapped (and any flip/assembly gate assigned). You usually need gun held plus power / ground / gas. Unwelded ghost dots often show only while the clamp is seated at the required Path 2 location. Touch each step’s weld zone with the tip, hold trigger, then **release** before the next step.

4. **Merge**  
   When every bar is snapped — and, if configured, after the eight spots finish — the bars become one grabbable jointed frame. Separate bar grabs go away; you pick up the whole assembly.

5. **Grab the merged frame**  
   Example bar / frame ghosts for flipping and repositioning typically appear **only while you hold the joint**.

6. **Flip & snap frames**  
   - First flip: roughly 180° and contact with the example weldbar (often while held) → seats to the first example frame pose.  
   - After bottom dots: reposition to the next example frame (no 180° gate).  
   - Between corner welds: reorient snaps as you go.  
   - After corners / tops / ref second snaps: return or advance to later example frames as wired (including a possible second 180° after top welds).

7. **Weld lines (corners, top, bottom)**  
   Unlock when the right assembly milestone is done. Ghost lines often need the clamp grounded at a specific Path 2 seat. Sequential mode means one at a time; some corner sets wait for a reorient between lines. Tip on the line’s touch zone + trigger (+ machine prereqs if required).

8. **Clamp Path 2 stages**  
   Path 2 is an ordered list of clamp ghosts. The clamp becomes eligible for Path 2 only after listed refs, bars, and any extra prerequisites pass.  
   **Important:** seating the clamp does **not** advance the stage. The stage advances when the welds / steps assigned to that stage all report complete. The same ghost slot can be reused later; the demo re-arms when that ghost is shown again.

### Path 1 vs Path 2 (clamp)

| | Panel (Path 1) | Frame (Path 2) |
|---|---|---|
| How many seats | One | Ordered stages |
| Ready when | Panel on sheet | Refs + bars (+ extras) ready |
| Ghost while held | That one Path 1 ghost (if closest eligible) | Current stage only; others hidden |
| What advances it | N/A | Completing the welds tied to that stage |

Grounding remembers **where** you last seated even after Path 2 advances, so weld ghosts that say “clamp must be here” can still check that location.

### Ghosts & examples (frame)

| Ghost | When it shows |
|---|---|
| Ref / bar placement (pre-merge) | While you hold that real piece |
| Merged example bars / frames | While you hold the merged joint; only the active phase set (1st / 2nd / 3rd) |
| Path 2 clamp | Held + Path 2 ready + current stage |
| Spot / line **unwelded** ghosts | Unlocked by assembly gates, but often **visible only** while clamp is seated at the assigned guide; finished welds stay visible |
| Sequential line preview | May also require the gun held |

### Common frame-path problems

| What you see | What usually went wrong |
|---|---|
| Bars never merge | A bar isn’t snapped, or eight-spot gate isn’t finished |
| No example frame/bar ghosts | Joint not merged yet, joint not held, or wrong phase set |
| Flip won’t snap | Not near 180°, not touching the example weldbar, not held, cooldown, or reveal gate not met |
| Corner / line ghost missing | Clamp not seated at the required Path 2 guide, wrong unlock milestone, or waiting on a reorient |
| Path 2 won’t move on | Assigned welds for that stage aren’t all complete (snap alone never advances) |
| Can’t pull clamp off the frame | Pass-through vs merged joint didn’t apply yet |
| Ref instantly re-snaps when you lift it | Grab-edge snap cooldown too short for that overlap |

---

## Using the weld gun

Sparks only when **all** of these are true (anything left unassigned in the scene is skipped):

1. **MIG power ON** — power switch shows its “on” state.
2. **Work clamp grounded** — clamp is seated.
3. **Gas ON** — gas knob in the on range.
4. **Gun held** with trigger past the threshold (matched to the holding hand when that option is on).
5. **Not locked** after a speed evaluation, and **not in overheat / block** cooldown.

Status text (if present) shows power / ground / gas as on (`1`), off (`0`), or unused (`-`).

**Actually laying blobs** also needs:

- Trigger held long enough on a weldable surface (default about **1 second** start delay).
- Tip aimed at a weldable surface within range.
- Tip **physically close enough** to the surface when tip-contact checking is enabled — raycast alone is not enough.

### Voltage & wire speed

Each panel material has target voltage, wire speed, and gas (plus tolerances). The monitor compares your knobs (and tip / work angle when used) to the panel under the tip. With strict evaluation on (default), missing knob wiring counts as a failed parameter check.

### Tip contact

For free welding and for sequential dots/lines, get the tip into that step’s weld zone and keep contact. If you see sparks but no blobs, you are often still in the start delay, off the weldable layer, or slightly too far from the surface.

### Why the gun “won’t fire”

| Symptom | Likely cause |
|---|---|
| No sparks | Power, ground, or gas off; gun not held; trigger too light; post-eval or overheat lock |
| Sparks but no blobs | Tip not on weldable surface; tip gap too large; still inside start delay |
| Weld ghosts won’t complete | Tip not in that line/dot’s touch zone; prereqs off; sequential step still needs a trigger release |

---

## Reading weld quality

On the graded panel path, travel is timed from the start mark (X1) to the end mark (X2). Ideal time comes from ideal travel speed and distance; a tolerance band decides the result.

| Result | Meaning |
|---|---|
| **Too fast** | You finished the segment quicker than the allowed band |
| **Good** | Travel time within the band, and parameters stayed OK if that check is required |
| **Too slow** | You took longer than the allowed band |
| **Bad weld / bad parameters** | Voltage, wire speed, gas, angle, and/or tip-on-surface failed during the run |

After a grade, the gun locks briefly until the cooldown finishes **and** you release the trigger — that can feel like the gun is stuck if you keep squeezing.

Blob thickness while free-welding can also thicken/thin with travel speed versus ideal. That is separate from the X1–X2 grade messages.

---

## Physics 

Snapped parts (clamp, bars, refs, panel, flipped frame) usually **freeze**: kinematic + fully constrained so the piece stays seated. Grab is often disabled for a short moment after snap so physics can settle. When you grab again, freeze clears so you can move.

| Bad state | What it feels like |
|---|---|
| Dynamic rigidbody while “seated” | Piece gets bumped out of place; tip contact and clamps feel unstable |
| Rigidbody destroyed while the scene expects a freeze | Nothing left to lock; panel or part drifts |
| Rigidbody removed while clamp is still grounded | Hand restore can stay blocked until the clamp ungrounds |
| Freeze left on after grab | Part won’t move even though you “have” it |

**Rule of thumb for teaching:** seated workpieces should be frozen; welding tip contact hates a jittery dynamic body. Prefer freeze-on-snap over destroying the rigidbody on clamp-style seats.

### Cooldowns - this is to ensure the real object does not get stuck on the example objects

Short timers are intentional:

- After snap → grab disabled briefly so the seat sticks.
- After you unsnap / pick up → you cannot instantly re-snap into the same ghost.
- Refs often block snap on **every** grab edge so overlapping a guide doesn’t suck the piece back in.
- While seated and during unsnap cooldown, collisions with guides / examples / merged frame are often ignored so you can pull the piece or clamp **through** without getting trapped.
- Gun start delay, overheat block, and post-evaluation lock are separate “wait / release trigger” pauses.

If something feels glued for half a second after seating or after a grade, wait the cooldown and release the trigger before assuming it is broken.

### Unsnap pass-through

While a piece is seated (and for a short time after you grab it off), the demo ignores collisions between that piece and its guides / example geometry / merged joint so you can extract it. Pass-through restores after you are free, grab cooldown is done, and the short re-snap block has elapsed. The global “always ignore snap guides” switch is normally **off** so tip contact still works; snap scripts force ignore only when seating / unsnapping needs it.

---

## Ghosts & clamps (shared mental model)

- **Example / ghost pieces** show where the real part should go. They usually appear while you **hold** the matching real object and are close enough / past the right unlock.
- **Clamp must be seated** for many weld-guide ghosts (dots and lines). Unlocking a sequence is not the same as seeing the ghost — visibility often waits on ground at a specific clamp location.
- **Path 2 advances on weld completion**, not on clamp snaps. Teach students: “finish the welds for this stage, then the next clamp seat appears.”
- Finished welds stay visible; unwelded ghosts hide again if you unground the clamp when that gate is used.
- Only one sheet workpiece, one active Path 2 clamp stage, and one active merged example set at a time — if the “wrong” ghost is showing, check hold state and which stage you are in.

---

## Quick checklist before blaming the scene

**To snap clamp / bar / ref**

- Not already snapped; past cooldown  
- Usually held  
- Touching the snap target / guide  
- Asset name matches the active ghost when both are set  
- For the clamp: some path is actually eligible  

**To show Path 2 clamp ghost**

- Holding the clamp  
- Frame prerequisites met  
- Current stage has a guide  
- Closer than Path 1 if both are eligible  

**To fire sparks → blobs**

Power + ground + gas + held + trigger → wait start delay → tip on weldable with contact → not locked / overheating  

**To finish a sequential dot or line**

Sequence unlocked → tip in that step’s zone → trigger → release before next → machine prereqs if required → reorient caught up when the corner set asks for it  

