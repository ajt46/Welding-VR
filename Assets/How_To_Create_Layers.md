# How to Create Layers in Unity

## Step-by-Step Instructions

### Step 1: Open Layer Settings

1. In Unity, go to **Edit → Project Settings**
2. In the left sidebar, click **Tags and Layers**
3. You'll see a list of **Layers** (User Layer 8, User Layer 9, etc.)

### Step 2: Create Welding Layers

You need to create two layers:

#### Layer 6 - Welding Blobs
1. Find **User Layer 6** in the list
2. Click in the text field next to it
3. Type: `WeldingBlobs` (or `Welding Blobs`)
4. Press Enter

#### Layer 7 - Welding Panel
1. Find **User Layer 7** in the list
2. Click in the text field next to it
3. Type: `WeldingPanel` (or `Welding Panel`)
4. Press Enter

### Step 3: Apply Layers to Your Objects

#### For Welding Panels:
1. Select your panel GameObject
2. In the Inspector, at the top, find the **Layer** dropdown
3. Select **WeldingPanel** (Layer 7)

#### For Weld Blob Prefab:
1. Select your weld blob prefab
2. In the Inspector, set **Layer** to **WeldingBlobs** (Layer 6)

### Step 4: Configure CustomWeldingController

1. Select your MIG Welding Gun GameObject
2. In the Inspector, find **CustomWeldingController** component
3. For **Weldable Layers**:
   - Click the dropdown (it will show checkboxes)
   - **Check** the box for **WeldingPanel** (Layer 7)
   - **Uncheck** everything else
4. For **Blob Layer**:
   - Set the number to **6** (or use the slider)

## Visual Guide

```
Unity Editor:
┌─────────────────────────────────┐
│ Edit → Project Settings         │
│   └─ Tags and Layers            │
│                                 │
│   Layers:                       │
│   ┌─────────────────────────┐ │
│   │ User Layer 6: [WeldingBlobs] │
│   │ User Layer 7: [WeldingPanel] │
│   └─────────────────────────┘ │
└─────────────────────────────────┘
```

## Troubleshooting

### "I don't see the layers in the dropdown"

**Solution:**
- Make sure you created the layers in **Project Settings → Tags and Layers**
- Close and reopen the Inspector
- The LayerMask dropdown shows layers differently - it's a multi-select checkbox list

### "LayerMask shows numbers instead of names"

**This is normal!** LayerMask in Unity Inspector shows:
- A dropdown with checkboxes
- Layer names appear when you expand it
- You can select multiple layers (but usually just select one)

### "How do I use the LayerMask dropdown?"

1. Click on the **Weldable Layers** field
2. A dropdown appears with all layers
3. **Check** the layer you want (WeldingPanel)
4. **Uncheck** all others
5. The field will show the layer name or a number

## Alternative: Use Layer Number Directly

If the LayerMask is confusing, you can also:
- Set **Blob Layer** to `6` (this is a simple number field)
- The **Weldable Layers** LayerMask will work once layers are created

## Quick Reference

| Layer Number | Layer Name | Used For |
|-------------|------------|----------|
| 6 | WeldingBlobs | Weld blob objects |
| 7 | WeldingPanel | Weldable surfaces/panels |

## After Creating Layers

Once layers are created:
1. **Refresh Unity** (Assets → Refresh or Ctrl+R)
2. **Select your gun** with CustomWeldingController
3. **Weldable Layers** dropdown will now show "WeldingPanel"
4. **Check the box** for WeldingPanel
5. Set **Blob Layer** to `6`

Your welding system should now work!
