using BNG;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Weld-line reveal driven by snap gates. Lines start hidden. Once gates pass, unwelded ghosts appear in
/// <see cref="previewMaterial"/> only while an optional clamp is grounded at an assigned
/// <see cref="exampleclamp"/> location (and, in sequential mode, optionally while the gun is held).
/// Tip+trigger restores each line&apos;s original material; welded lines stay visible if the clamp is removed.
/// </summary>
public class WeldLinesRevealOnSnap : MonoBehaviour, IWeldStepCompletable
{
    [System.Serializable]
    public class WeldLine
    {
        [Tooltip("If false, this line is ignored entirely by this component (not revealed, welded, or counted). Use to reveal only a chosen subset, e.g. 4 of 12 for example 2.")]
        public bool includeInReveal = true;

        [Tooltip("Mesh/sprite to reveal / weld.")]
        public Renderer targetRenderer;

        [Tooltip("Collider ON THIS CORNER/LINE that the gun tip must touch (not the tip collider itself).")]
        public Collider weldTouchCollider;

        [Tooltip("If set, toggles visibility on this GameObject. If null, uses Target Renderer's GameObject.")]
        public GameObject visibilityRoot;

        [Tooltip("Optional. Material to restore when welded. If null, the renderer's material at startup is captured and restored.")]
        public Material originalMaterialOverride;

        [HideInInspector] public Material capturedOriginal;
        [HideInInspector] public bool welded;
    }

    public enum FlipSnapGate
    {
        [Tooltip("Unlock after the joint has snapped to the SECOND example frame (post-weld reposition).")]
        SecondFrameSnap,

        [Tooltip("Unlock after the first 180° flip snap completed.")]
        FlipSnap,

        [Tooltip("Unlock after the finished frame has snapped back onto ExampleFrame1 (post-corner return).")]
        ExampleFrame1Return,

        [Tooltip("Unlock after the post-TopWelds 180° flip snap (like the beginning flip).")]
        PostTopWeldFlip,

        [Tooltip("Unlock after RealFrame has snapped onto ExampleFrame3 (after ref piece second-guide snap).")]
        ExampleFrame3Snap,
    }

    [Header("Snap gate (ALL assigned gates must pass)")]
    [Tooltip("Unlock once this flip-snap component reports the chosen snap.")]
    public WeldbarMergedFlipSnapToAnchor flipSnapSource;

    [Tooltip("Which snap on Flip Snap Source unlocks the reveal.")]
    public FlipSnapGate flipSnapGate = FlipSnapGate.SecondFrameSnap;

    [Tooltip("Unlock once this ref piece reports IsSnapped.")]
    public refpiece refPieceSource;

    [Tooltip("Unlock once this assembly reports HasMergedAssembly.")]
    public WeldbarAssemblyRoot assemblySource;

    [Tooltip("Chain gate: unlock only once another weld-line group reports HasWeldedAllLines (e.g. top lines wait for corner lines).")]
    public WeldLinesRevealOnSnap requireLineGroupComplete;

    [Tooltip("Chain gate: unlock only once this flip reports HasCompletedFlip (e.g. bottom lines wait for the second flip).")]
    public MergedFlip180OnContact requireFlipComplete;

    [Tooltip("If no gate is assigned, unlock immediately on start (useful for testing).")]
    public bool revealImmediatelyIfNoGate = false;

    [Header("Clamp ground gate (ghost visibility)")]
    [Tooltip("Optional. Unwelded line ghosts stay hidden until this clamp is grounded. Leave empty to skip the clamp gate (existing snap gates still apply). Welded lines always stay visible.")]
    public clamp requireClampGrounded;

    [Tooltip("Optional respective location: when set with Require Clamp Grounded, ghosts show only while that clamp is grounded on THIS exampleclamp (not another Path 1 / Path 2 slot). Leave empty to accept any grounded location on the assigned clamp.")]
    public exampleclamp requireGroundedAtGuide;

    [Header("Sequencing")]
    [Tooltip("If true, lines reveal ONE AT A TIME in array order (only Include-checked lines): weld the current one, then the next appears. Trigger must be released between welds. If false, all unlock at once and can be welded in any order.")]
    public bool sequential = false;

    [Tooltip("Sequential mode: the current corner's ghost/preview is shown only while the gun is held. Release the gun and the unwelded preview hides again. Welded corners stay visible with their original material.")]
    public bool showPreviewOnlyWhileGunHeld = true;

    [Tooltip("Sequential + Flip Snap Source: after each weld (except the last), wait for a reorient snap before the next ghost. For Gate = Second Frame Snap → ExampleFrame2 reorient. For Gate = Example Frame3 Snap → ExampleFrame3 / InnerCorner reorient.")]
    public bool requireReorientBetweenSequentialLines = true;

    [Header("Gun")]
    public CustomWeldingController weldingGun;

    [Tooltip("Tip collider inferred from the welding gun when null.")]
    public Collider gunTipColliderOverride;

    [Tooltip("Tip-to-surface distance still treated as touching (matches the welding sim).")]
    public float tipContactGapTolerance = 0.008f;

    [Tooltip("If true, the gun must be held and its welding prerequisites (power/ground/gas) met to weld a line.")]
    public bool requireGunHeldAndPrereqs = true;

    [Header("Visuals")]
    [Tooltip("Ghost / example color shown on the active line before it is welded.")]
    public Material previewMaterial;

    [Header("Lines (e.g. 12 corner weld lines)")]
    public WeldLine[] lines = new WeldLine[12];

    [Header("Events")]
    [Tooltip("Fired once, the moment snap gates unlock this group.")]
    public UnityEvent onRevealed;

    [Tooltip("Fired each time an individual line is welded (restored to its original material).")]
    public UnityEvent onLineWelded;

    [Tooltip("Fired once, when the last remaining line has been welded.")]
    public UnityEvent onAllLinesWelded;

    /// <summary>Snap gates have passed — sequence may show / weld.</summary>
    bool _unlocked;
    int _weldedCount;

    /// <summary>Sequential mode: player must release the trigger after each weld before the next can register.</summary>
    bool _triggerReleasedLatch = true;

    WeldLine _activePreviewLine;

    void Awake()
    {
        CaptureOriginalsAndHide();
    }

    void CaptureOriginalsAndHide()
    {
        if (lines == null)
            return;

        foreach (var line in lines)
        {
            if (line == null || !line.includeInReveal || line.targetRenderer == null)
                continue;

            line.capturedOriginal = line.originalMaterialOverride != null
                ? line.originalMaterialOverride
                : line.targetRenderer.sharedMaterial;
            line.welded = false;

            SetLineVisible(line, false);
        }
    }

    void Update()
    {
        if (!_unlocked)
        {
            if (!SnapConditionMet())
                return;

            Unlock();
        }

        TryWeldLines();
    }

    /// <summary>
    /// LateUpdate so clamp unsnap (clears <see cref="clamp.GroundedGuide"/>) in Update is visible
    /// the same frame — unwelded ghosts hide immediately when the clamp is picked up.
    /// </summary>
    void LateUpdate()
    {
        if (_unlocked)
            UpdatePreviewVisibility();
    }

    /// <summary>
    /// Clamp gate for ghost visibility only — does not block snap unlock or weld completion.
    /// Empty <see cref="requireClampGrounded"/> = no clamp requirement.
    /// If a guide is assigned without a clamp reference, deny (do not show ghosts).
    /// Partial weld progress never bypasses this gate for remaining unwelded lines.
    /// </summary>
    bool IsClampGhostGateMet()
    {
        if (requireClampGrounded == null)
            return requireGroundedAtGuide == null;
        return requireClampGrounded.IsGroundedAt(requireGroundedAtGuide);
    }

    bool SnapConditionMet()
    {
        bool anyAssigned = false;

        if (flipSnapSource != null)
        {
            anyAssigned = true;
            bool ok;
            switch (flipSnapGate)
            {
                case FlipSnapGate.ExampleFrame3Snap:
                    ok = flipSnapSource.HasSnappedToThirdExampleFrame;
                    break;
                case FlipSnapGate.PostTopWeldFlip:
                    ok = flipSnapSource.HasCompletedPostTopWeldFlip;
                    break;
                case FlipSnapGate.ExampleFrame1Return:
                    ok = flipSnapSource.HasReturnedToExampleFrame1;
                    break;
                case FlipSnapGate.FlipSnap:
                    ok = flipSnapSource.HasCompletedFinalFlipSnap;
                    break;
                default:
                    ok = flipSnapSource.HasRepositionedToSecondFrame;
                    break;
            }
            if (!ok)
                return false;
        }

        if (refPieceSource != null)
        {
            anyAssigned = true;
            if (!refPieceSource.IsSnapped)
                return false;
        }

        if (assemblySource != null)
        {
            anyAssigned = true;
            if (!assemblySource.HasMergedAssembly)
                return false;
        }

        if (requireLineGroupComplete != null)
        {
            anyAssigned = true;
            if (!requireLineGroupComplete.HasWeldedAllLines)
                return false;
        }

        if (requireFlipComplete != null)
        {
            anyAssigned = true;
            if (!requireFlipComplete.HasCompletedFlip)
                return false;
        }

        return anyAssigned || revealImmediatelyIfNoGate;
    }

    void Unlock()
    {
        _unlocked = true;

        if (sequential)
        {
            // Do not force-show yet — UpdatePreviewVisibility handles gun-held + clamp gating.
            _triggerReleasedLatch =
                !(weldingGun != null && weldingGun.IsTriggerHeldForWelding());
            _activePreviewLine = null;
        }
        // Non-sequential: UpdatePreviewVisibility shows ghosts when the clamp gate allows.

        onRevealed?.Invoke();
    }

    /// <summary>
    /// Shows unwelded ghosts only while unlocked AND clamp grounded at the assigned location
    /// (and, in sequential mode, optionally only while the gun is held). Welded lines stay visible.
    /// </summary>
    void UpdatePreviewVisibility()
    {
        if (!_unlocked)
            return;

        if (sequential)
        {
            UpdateSequentialPreviewVisibility();
            return;
        }

        UpdateNonSequentialPreviewVisibility();
    }

    void UpdateNonSequentialPreviewVisibility()
    {
        if (lines == null)
            return;

        bool clampOk = IsClampGhostGateMet();
        foreach (var line in lines)
        {
            if (line == null || !line.includeInReveal || line.targetRenderer == null)
                continue;

            if (line.welded)
            {
                // Welded real visual — keep on with original material (never leave preview/ghost mat).
                if (line.capturedOriginal != null)
                    line.targetRenderer.material = line.capturedOriginal;
                SetLineVisible(line, true);
                continue;
            }

            if (clampOk)
                ShowLinePreview(line);
            else
                SetLineVisible(line, false);
        }
    }

    /// <summary>
    /// Sequential: show only the current unwelded line's ghost while the gun is held (optional)
    /// and the clamp ground gate passes. Welded lines stay visible with original material.
    /// When the clamp gate fails, every unwelded preview is forced off (not only the last active one).
    /// </summary>
    void UpdateSequentialPreviewVisibility()
    {
        if (!IsClampGhostGateMet() || !CanAdvanceToCurrentSequentialLine())
        {
            HideAllUnweldedPreviews();
            _activePreviewLine = null;
            return;
        }

        WeldLine next = GetNextSequentialLine();
        bool gunHeld = IsGunHeld();
        bool wantShow = next != null && (!showPreviewOnlyWhileGunHeld || gunHeld);

        if (!wantShow)
        {
            HideAllUnweldedPreviews();
            _activePreviewLine = null;
            return;
        }

        // Hide every other unwelded line so partial progress cannot leave stray ghosts on.
        if (lines != null)
        {
            foreach (var line in lines)
            {
                if (line == null || !line.includeInReveal || line.welded || line.targetRenderer == null)
                    continue;
                if (line != next)
                    SetLineVisible(line, false);
            }
        }

        if (_activePreviewLine != next)
        {
            ShowLinePreview(next);
            _activePreviewLine = next;
        }
        else if (_activePreviewLine != null)
        {
            SetLineVisible(_activePreviewLine, true);
            if (previewMaterial != null && !_activePreviewLine.welded)
                _activePreviewLine.targetRenderer.material = previewMaterial;
        }
    }

    /// <summary>Force-hide every include-checked, not-yet-welded preview (clamp ungrounded / gun released).</summary>
    void HideAllUnweldedPreviews()
    {
        if (lines == null)
            return;

        foreach (var line in lines)
        {
            if (line == null || !line.includeInReveal || line.welded || line.targetRenderer == null)
                continue;
            SetLineVisible(line, false);
        }
    }

    /// <summary>
    /// First line is free after unlock. Later lines wait until the flip-snap has completed one
    /// reorient per already-welded line (glow → grab → rotate → snap).
    /// Applies for SecondFrameSnap (ExampleFrame2) and ExampleFrame3Snap (InnerCorner) gates.
    /// </summary>
    bool CanAdvanceToCurrentSequentialLine()
    {
        if (!requireReorientBetweenSequentialLines || flipSnapSource == null)
            return true;

        if (_weldedCount <= 0)
            return true;

        if (flipSnapGate == FlipSnapGate.SecondFrameSnap)
            return flipSnapSource.CompletedSecondFrameReorientCount >= _weldedCount;

        if (flipSnapGate == FlipSnapGate.ExampleFrame3Snap)
            return flipSnapSource.CompletedThirdFrameReorientCount >= _weldedCount;

        // Other gates (TopWelds / BottomWelds / etc.) do not wait on reorient.
        return true;
    }

    bool IsGunHeld()
    {
        if (weldingGun == null)
            return false;

        if (weldingGun.gunGrabbable == null)
            weldingGun.gunGrabbable = weldingGun.GetComponent<Grabbable>()
                ?? weldingGun.GetComponentInParent<Grabbable>();

        return weldingGun.gunGrabbable != null && weldingGun.gunGrabbable.BeingHeld;
    }

    void ShowLinePreview(WeldLine line)
    {
        if (line == null || line.targetRenderer == null)
            return;

        SetLineVisible(line, true);

        if (previewMaterial != null)
            line.targetRenderer.material = previewMaterial;
    }

    static void SetLineVisible(WeldLine line, bool visible)
    {
        if (line == null || line.targetRenderer == null)
            return;

        GameObject root = line.visibilityRoot != null ? line.visibilityRoot : line.targetRenderer.gameObject;
        if (root != null)
            root.SetActive(visible);
    }

    /// <summary>First Include-checked, not-yet-welded, fully-wired line in array order (used by sequential mode).</summary>
    WeldLine GetNextSequentialLine()
    {
        if (lines == null)
            return null;

        foreach (var line in lines)
        {
            if (line == null || !line.includeInReveal || line.welded)
                continue;
            if (line.targetRenderer == null || line.weldTouchCollider == null)
                continue;
            return line;
        }
        return null;
    }

    void TryWeldLines()
    {
        if (!_unlocked || lines == null || weldingGun == null)
            return;

        if (requireGunHeldAndPrereqs)
        {
            if (!IsGunHeld())
                return;
            if (!weldingGun.AreWeldingPrerequisitesMet())
                return;
        }

        bool triggerHeld = weldingGun.IsTriggerHeldForWelding();

        if (sequential && !triggerHeld)
            _triggerReleasedLatch = true;

        if (!triggerHeld)
            return;

        Collider gunTip = gunTipColliderOverride != null ? gunTipColliderOverride : weldingGun.tipContactCollider;
        if (gunTip == null)
            return;

        if (sequential)
        {
            if (!_triggerReleasedLatch)
                return;

            if (!CanAdvanceToCurrentSequentialLine())
                return;

            WeldLine active = GetNextSequentialLine();
            if (active == null)
                return;

            if (TipHasPhysicalOverlap(gunTip, active.weldTouchCollider, tipContactGapTolerance))
            {
                WeldLineNow(active);
                _triggerReleasedLatch = false;
                _activePreviewLine = null;
                // Next preview waits for reorient (if required) then appears via UpdateSequentialPreviewVisibility.
            }
            return;
        }

        foreach (var line in lines)
        {
            if (line == null || !line.includeInReveal || line.welded ||
                line.targetRenderer == null || line.weldTouchCollider == null)
                continue;

            if (!TipHasPhysicalOverlap(gunTip, line.weldTouchCollider, tipContactGapTolerance))
                continue;

            WeldLineNow(line);
        }
    }

    void WeldLineNow(WeldLine line)
    {
        line.welded = true;
        if (line.capturedOriginal != null)
            line.targetRenderer.material = line.capturedOriginal;

        SetLineVisible(line, true);

        _weldedCount++;
        onLineWelded?.Invoke();

        if (_weldedCount >= CountValidLines())
            onAllLinesWelded?.Invoke();
    }

    int CountValidLines()
    {
        if (lines == null)
            return 0;

        int n = 0;
        foreach (var line in lines)
        {
            if (line != null && line.includeInReveal &&
                line.targetRenderer != null && line.weldTouchCollider != null)
                n++;
        }
        return n;
    }

    /// <summary>True after snap gates unlocked this group.</summary>
    public bool HasRevealed => _unlocked;

    /// <summary>Number of lines welded back to their original material so far.</summary>
    public int WeldedCount => _weldedCount;

    /// <summary>How many include-checked, fully-wired lines this group expects.</summary>
    public int ValidLineCount => CountValidLines();

    /// <summary>True once every valid line has been welded.</summary>
    public bool HasWeldedAllLines => _unlocked && _weldedCount >= CountValidLines() && CountValidLines() > 0;

    /// <summary>Step is complete once every line in this group has been welded.</summary>
    public bool IsStepComplete => HasWeldedAllLines;

    /// <summary>
    /// Debug/test: unlock and weld every include-checked line (restores original materials).
    /// Bypasses gun/reorient gates so the post-corner ExampleFrame1 glow can start immediately.
    /// </summary>
    public void ForceCompleteAllLinesForDebug()
    {
        if (lines == null || lines.Length == 0)
            return;

        bool alreadyDone = HasWeldedAllLines;

        if (!_unlocked)
        {
            _unlocked = true;
            onRevealed?.Invoke();
        }

        _activePreviewLine = null;
        _triggerReleasedLatch = true;

        for (int i = 0; i < lines.Length; i++)
        {
            WeldLine line = lines[i];
            if (line == null || !line.includeInReveal ||
                line.targetRenderer == null || line.weldTouchCollider == null)
                continue;

            if (line.welded)
            {
                SetLineVisible(line, true);
                continue;
            }

            if (line.capturedOriginal == null && line.originalMaterialOverride != null)
                line.capturedOriginal = line.originalMaterialOverride;

            line.welded = true;
            if (line.capturedOriginal != null)
                line.targetRenderer.material = line.capturedOriginal;
            else if (line.originalMaterialOverride != null)
                line.targetRenderer.material = line.originalMaterialOverride;

            SetLineVisible(line, true);
            onLineWelded?.Invoke();
        }

        _weldedCount = CountValidLines();

        if (!alreadyDone && HasWeldedAllLines)
            onAllLinesWelded?.Invoke();
    }

    static bool TipHasPhysicalOverlap(Collider tipCollider, Collider surfaceCollider, float tolerance)
    {
        if (tipCollider == null || surfaceCollider == null)
            return false;

        // Same collider assigned for tip and surface can never mean a real weld contact.
        if (tipCollider == surfaceCollider)
            return false;

        Transform tipT = tipCollider.transform;
        Transform surfT = surfaceCollider.transform;

        if (Physics.ComputePenetration(
                tipCollider, tipT.position, tipT.rotation,
                surfaceCollider, surfT.position, surfT.rotation,
                out _, out _))
            return true;

        Vector3 onSurface = surfaceCollider.ClosestPoint(tipCollider.bounds.center);
        Vector3 onTip = tipCollider.ClosestPoint(onSurface);
        return Vector3.Distance(onSurface, onTip) <= Mathf.Max(tolerance, 0f);
    }
}
