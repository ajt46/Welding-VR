# Custom Welding System Setup Guide

## Overview
This custom welding system gives you full control over your welding objects and is fully integrated with `weldingsparks.cs`.

## Scripts Created

1. **CustomWeldingController.cs** - Main welding logic (attach to MIG gun)
2. **CustomWeldingBlob.cs** - Blob behavior and cooling (attach to blob prefab)
3. **CustomWeldingSurface.cs** - Weldable surface helper (attach to panels)
4. **weldingsparks.cs** - Updated to work with CustomWeldingController

---

## Setup Instructions

### Step 1: Setup MIG Welding Gun

1. **Attach Scripts:**
   - `weldingsparks.cs` (already attached)
   - `CustomWeldingController.cs` (NEW - attach this)

2. **Configure CustomWeldingController:**
   - **Welding Tip:** Assign the transform at the tip of your gun
   - **Weld Blob Prefab:** Create a prefab with a mesh (sphere/capsule) and assign it
   - **Blob Initial Size:** 0.2 (adjust as needed)
   - **Blob Max Size:** 0.7 (when it overheats)
   - **Blob Growth Rate:** 0.2 (how fast it grows)
   - **Welding Start Delay:** 1.0 (seconds before welding starts)
   - **Weldable Layers:** Set to Layer 7 (or your panel layer)
   - **Blob Layer:** Set to Layer 6
   - **Raycast Distance:** 0.5 (how far to check)
   - **Hole Prefab:** (Optional) Prefab to show when blob overheats

### Step 2: Create Weld Blob Prefab

1. **Create GameObject:**
   - Create a Sphere or Capsule (3D Object)
   - Name it "WeldBlob"

2. **Setup Components:**
   - Add `CustomWeldingBlob.cs` script
   - Add MeshRenderer
   - Add MeshFilter (if not already there)

3. **Configure CustomWeldingBlob:**
   - **Hot Material:** Assign a glowing material (e.g., orange/yellow with emission)
   - **Cooled Material:** Assign a cooled material (e.g., gray/dark)
   - **Cooling Delay:** 1.5 seconds
   - **Cooling Fade Time:** 1.5 seconds
   - **Cooled Mesh:** (Optional) Different mesh for cooled state

4. **Set Layer:**
   - Set GameObject layer to 6 (or your blob layer)

5. **Tag:**
   - Tag as "WeldObject" (optional, for identification)

6. **Save as Prefab:**
   - Drag to Project window to create prefab
   - Assign this prefab to CustomWeldingController's "Weld Blob Prefab" field

### Step 3: Setup Weldable Surfaces (Panels)

1. **Attach Script:**
   - Add `CustomWeldingSurface.cs` to your panel objects

2. **Set Layer:**
   - Ensure panel is on Layer 7 (or your weldable layer)
   - The script will auto-set this if different

3. **Optional - Quality Checking:**
   - **Good Weld Area:** Create a collider (Box/Mesh) that defines where "good" welds should be
   - **Weld Path Points:** Create empty GameObjects along the weld line for scanning

### Step 4: Layer Setup

Make sure these layers exist in your project:

- **Layer 6:** "Welding Blobs" (or custom name)
- **Layer 7:** "Welding Panels" (or custom name)

Set these in: Edit → Project Settings → Tags and Layers

---

## How It Works

### Welding Flow:

1. **Grab Gun** → `weldingsparks.cs` detects grab via Grabbable
2. **Press Trigger** → `weldingsparks.cs` shows particles
3. **Point at Surface** → `CustomWeldingController` raycasts from welding tip
4. **Wait 1 Second** → Welding delay (configurable)
5. **Blob Created** → Blob appears at contact point, grows over time
6. **Release Trigger** → Blob finalized, particles stop
7. **Blob Cools** → After delay, blob fades from hot to cool material

### Blob Behavior:

- **New Blob:** Created when welding on a surface
- **Existing Blob:** Grows larger if you weld on it again
- **Overheating:** If blob gets too large, it creates a hole and is destroyed
- **Cooling:** Blob automatically cools down after welding stops

---

## Customization Tips

### Adjust Blob Size:
- **Blob Initial Size:** Make smaller for finer welds, larger for thicker welds
- **Blob Max Size:** Lower = easier to overheat, Higher = more forgiving

### Adjust Welding Speed:
- **Blob Growth Rate:** Higher = faster blob growth
- **Welding Start Delay:** Lower = faster response, Higher = more realistic delay

### Visual Effects:
- Create materials with **Emission** for hot blobs (glowing effect)
- Use **Particle System** on blob for additional effects
- Add **Audio Source** to blob for cooling sounds

### Multiple Surfaces:
- Each surface can have its own `CustomWeldingSurface` component
- Each can define its own "good weld" area
- Useful for complex welding scenarios

---

## Testing Checklist

- [ ] Gun can be grabbed
- [ ] Particles appear when trigger pressed
- [ ] Blobs created when pointing at Layer 7 surface
- [ ] Blobs grow while welding
- [ ] Blobs stop growing when trigger released
- [ ] Blobs cool down after welding
- [ ] Overheating creates hole (if hole prefab assigned)
- [ ] Can weld on existing blobs

---

## Troubleshooting

### No Blobs Created:
- Check if surface is on correct layer (Layer 7)
- Check if welding tip transform is assigned
- Check if weld blob prefab is assigned
- Wait 1 second after pressing trigger (delay)

### Blobs in Wrong Location:
- Check welding tip transform position
- Check raycast direction (should be forward from tip)
- Adjust raycast distance if needed

### Blobs Don't Grow:
- Check blob growth rate (should be > 0)
- Make sure you're holding trigger continuously
- Check if blob max size is too low

### Particles Don't Show:
- Check if Grabbable component is attached
- Check if InputBridge is available
- Check trigger threshold value

---

## Example Setup

```
MIG Welding Gun
├── weldingsparks.cs
├── CustomWeldingController.cs
│   ├── Welding Tip: [Tip Transform]
│   ├── Weld Blob Prefab: [WeldBlob Prefab]
│   └── Settings: [Configured]
└── Particle System (child)

WeldBlob Prefab
├── MeshRenderer
├── MeshFilter (Sphere)
├── CustomWeldingBlob.cs
│   ├── Hot Material: [Glowing Orange]
│   └── Cooled Material: [Dark Gray]
└── Layer: 6

Welding Panel
├── MeshRenderer
├── MeshFilter
├── CustomWeldingSurface.cs
│   ├── Good Weld Area: [Box Collider]
│   └── Weld Path Points: [Empty GameObjects]
└── Layer: 7
```

---

## Next Steps

Once basic welding works, you can:
- Add quality checking system
- Add weld scanning/coverage detection
- Add scoring/feedback system
- Customize blob materials and effects
- Add sound effects
- Add haptic feedback
