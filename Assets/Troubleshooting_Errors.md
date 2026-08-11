# Troubleshooting Common Errors

## Script Compilation Errors

### Error: "The type or namespace name 'CustomWeldingController' could not be found"

**Solution:**
1. Make sure `CustomWeldingController.cs` is in your Assets folder
2. In Unity, go to **Assets → Refresh** (or press Ctrl+R)
3. Check the Unity Console for any script compilation errors
4. Make sure the script file isn't corrupted

### Error: "NullReferenceException" at runtime

**Common Causes:**
- Missing component references in Inspector
- Script not attached to correct GameObject

**Solution:**
1. Check that `CustomWeldingController` is attached to your MIG gun
2. In Inspector, verify all required fields are assigned:
   - Welding Tip (Transform)
   - Weld Blob Prefab (GameObject prefab)
3. Check Console for which object is null

### Error: "InputBridge.Instance is null"

**Solution:**
- Make sure BNG Framework is properly imported
- Check that InputBridge component exists in scene
- This is required for VR input

---

## Setup Errors

### Particles don't appear

**Check:**
1. Is `Grabbable` component attached to gun?
2. Is `weldingsparks.cs` attached to gun?
3. Is particle system assigned (as child or on same object)?
4. In Play mode, check Console for errors

### Blobs don't create

**Check:**
1. Is `CustomWeldingController` attached to gun?
2. Is **Welding Tip** transform assigned?
3. Is **Weld Blob Prefab** assigned?
4. Is surface on correct layer (Layer 7)?
5. Wait 1 second after pressing trigger (there's a delay)

### "Welding Tip is not assigned!" warning

**Solution:**
1. Create an empty GameObject at the tip of your gun
2. Name it "WeldingTip"
3. Assign it to the **Welding Tip** field in CustomWeldingController

---

## Android Build Errors (Gradle)

If you're seeing Gradle build errors (like in the image):

### Error: "Gradle initialization failed"

**Possible Solutions:**
1. **Check Java/JDK:**
   - Unity should use its bundled JDK
   - Path: `Unity\Editor\Data\PlaybackEngines\AndroidPlayer\OpenJDK`

2. **Clean Build:**
   - Delete `Library` folder (Unity will regenerate)
   - Delete `Temp` folder
   - Try building again

3. **Check Android SDK:**
   - Unity → Edit → Preferences → External Tools
   - Verify Android SDK path is correct

4. **Gradle Version:**
   - Unity → Edit → Project Settings → Player → Android
   - Try different Gradle settings

5. **Build Settings:**
   - File → Build Settings
   - Make sure "Development Build" isn't causing issues
   - Try "Build" instead of "Build and Run"

### Error: "CommandInvokationFailure"

**Solution:**
- Usually a path issue or permission problem
- Check that Unity has write permissions
- Try building to a different location
- Check antivirus isn't blocking Unity

---

## Quick Fix Checklist

If scripts aren't working:

1. ✅ **Refresh Unity:** Assets → Refresh (Ctrl+R)
2. ✅ **Check Console:** Window → General → Console
3. ✅ **Verify Scripts Attached:**
   - `weldingsparks.cs` on gun
   - `CustomWeldingController.cs` on gun
4. ✅ **Check Inspector Fields:**
   - All required fields assigned?
   - No null references?
5. ✅ **Check Layers:**
   - Layer 6 for blobs
   - Layer 7 for panels
6. ✅ **Test in Play Mode:**
   - Grab gun
   - Press trigger
   - Check Console for errors

---

## Getting Help

If errors persist, check:

1. **Unity Console** - Look for red error messages
2. **Script Location** - Make sure scripts are in Assets folder
3. **Unity Version** - Scripts work with Unity 2020.3+
4. **BNG Framework** - Required for VR input

**Common Error Messages:**
- "CS0246: The type or namespace name 'X' could not be found" → Script missing or not compiled
- "NullReferenceException" → Missing reference in Inspector
- "MissingComponentException" → Script not attached to GameObject
