# MIG Welder Sanity Check / Test Checklist

## Pre-Test Setup Verification

### ✅ Component Setup
- [ ] `weldingsparks.cs` is attached to MIG Welding Gun object
- [ ] `WeldingHandle.cs` is attached to MIG Welding Gun object
- [ ] `Grabbable.cs` (BNG Framework) is attached to MIG Welding Gun object
- [ ] Particle System is attached (as child or on same object)
- [ ] All required references in `WeldingHandle` are assigned:
  - [ ] `weldBlobSet` prefab
  - [ ] `weldHoleMask` prefab
  - [ ] `weldingTip` transform
  - [ ] `tipRenderer` MeshRenderer
  - [ ] Visual effects (weldingPP, weldingLight, envLights, glowEffect)

### ✅ Layer Setup
- [ ] Layer 6 is set to "Welding Blobs" (or custom name)
- [ ] Layer 7 is set to "Welding Panels" (or custom name)
- [ ] Weldable surfaces are on Layer 7
- [ ] Welding blob prefab is on Layer 6

### ✅ Panel Setup
- [ ] `WeldingPanel.cs` is attached to your panel
- [ ] Welding Collider is assigned and positioned along weld line
- [ ] Checking Transforms are set up (at least start and end points)
- [ ] Weld Scanner prefab is assigned

---

## Test 1: Basic Grab & Trigger Detection

### Test Steps:
1. Enter Play mode
2. Grab the MIG Welding Gun with VR controller
3. Press and hold the right trigger

### Expected Results:
- [ ] Particle system becomes visible when trigger is pressed
- [ ] Particle system stops and disappears when trigger is released
- [ ] Particle system remains invisible when gun is not grabbed (even if trigger pressed)
- [ ] Particle system remains invisible when gun is grabbed but trigger not pressed

---

## Test 2: Welding Blob Creation

### Test Steps:
1. Grab the gun
2. Point the welding tip at a surface on Layer 7 (welding panel)
3. Press and hold right trigger
4. Wait 1 second (welding delay)
5. Keep trigger held for a few seconds

### Expected Results:
- [ ] After 1 second delay, welding blob appears at contact point
- [ ] Blob is created with correct rotation (perpendicular to surface)
- [ ] Blob starts small and grows over time while welding continues
- [ ] Blob is parented to the panel
- [ ] Blob is on Layer 6 (Welding Blobs)

---

## Test 3: Blob Growth & Overheating

### Test Steps:
1. Start welding on a panel (as in Test 2)
2. Keep trigger held without moving the gun
3. Watch blob grow

### Expected Results:
- [ ] Blob grows gradually while welding
- [ ] When blob reaches size limit (~0.7 magnitude), it creates a hole mask
- [ ] Original blob is destroyed when hole is created
- [ ] Welding stops temporarily after overheating (holdOn period)

---

## Test 4: Welding on Existing Blobs

### Test Steps:
1. Create a blob (as in Test 2)
2. Release trigger to finalize blob
3. Point gun at the existing blob
4. Press trigger again

### Expected Results:
- [ ] Existing blob glows (hot material applied)
- [ ] Blob grows larger
- [ ] Blob is not duplicated

---

## Test 5: Movement & Travel Time

### Test Steps:
1. Start welding on a panel
2. Move the gun slowly along the weld line while holding trigger
3. Create multiple blobs

### Expected Results:
- [ ] New blob is created when moving to new location
- [ ] Previous blob is finalized when moving away
- [ ] Blobs are connected/look at each other
- [ ] Travel time is tracked between blobs

---

## Test 6: Corner Weld Detection

### Test Steps:
1. Point gun at a corner (where two panels meet)
2. Start welding

### Expected Results:
- [ ] `isCornerWeld` flag is set to true
- [ ] Blobs are thicker (1.3x scale) for corner welds
- [ ] Regular welds are thinner (1/3 scale)

---

## Test 7: Visual & Audio Effects

### Test Steps:
1. Start welding on a panel

### Expected Results:
- [ ] Welding light turns on
- [ ] Post-processing effects activate
- [ ] Glow effect appears
- [ ] Environment lights turn off
- [ ] Audio plays (welding sound)
- [ ] Tip material changes to hot material
- [ ] All effects stop when welding stops

---

## Test 8: Weld Quality Checking

### Test Steps:
1. Create several blobs along the weld line
2. Call `PopulateWeldingStats()` on WeldingPanel (or trigger quality check)

### Expected Results:
- [ ] Scanner appears at first checking transform
- [ ] Scanner moves along checking transforms path
- [ ] Scanner light turns green when blob detected
- [ ] Scanner light turns red when no blob detected
- [ ] Coverage percentage is calculated
- [ ] Blobs inside welding collider turn green (good welds)
- [ ] Blobs outside welding collider turn red (bad welds)
- [ ] Statistics are populated correctly

---

## Test 9: Edge Cases

### Test Steps:
1. Try welding on non-weldable surfaces (not Layer 7)
2. Try welding while not grabbing the gun
3. Rapidly press/release trigger
4. Weld very close to panel edge

### Expected Results:
- [ ] No blobs created on wrong layers
- [ ] Welding only works when gun is grabbed
- [ ] System handles rapid trigger changes gracefully
- [ ] No errors in console

---

## Common Issues & Solutions

### Issue: Particles don't appear
- **Check:** Is `Grabbable` component attached?
- **Check:** Is `InputBridge.Instance` available?
- **Check:** Is right trigger threshold correct?

### Issue: No blobs created
- **Check:** Is surface on Layer 7?
- **Check:** Is `weldingTip` transform assigned correctly?
- **Check:** Is `weldBlobSet` prefab assigned?
- **Check:** Wait 1 second after pressing trigger (delay)

### Issue: Blobs created in wrong location
- **Check:** Is `weldingTip` transform positioned correctly?
- **Check:** Is raycast direction correct (weldingTip.forward)?

### Issue: Scanner doesn't work
- **Check:** Are checking transforms assigned?
- **Check:** Is weld scanner prefab assigned?
- **Check:** Does scanner have `WeldCheckerLight` component?

---

## Quick Debug Test Script

Add this to a test button or console command to verify setup:

```csharp
// Quick verification
Debug.Log("Grabbable: " + (grabbable != null));
Debug.Log("WeldingHandle: " + (weldingHandle != null));
Debug.Log("ParticleSystem: " + (particleSystem != null));
Debug.Log("InputBridge: " + (InputBridge.Instance != null));
Debug.Log("BeingHeld: " + grabbable.BeingHeld);
Debug.Log("RightTrigger: " + InputBridge.Instance.RightTrigger);
```
