using BNG;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Eight weld targets stay hidden until readiness is met (typically four weld bars snapped, or alternatively
/// merged joint flip-snap complete), then the MIG gun is held and power / ground / gas pass. Unwelded ghosts
/// also require an optional clamp grounded at an assigned <see cref="exampleclamp"/> location; picking the
/// clamp up hides them again. Targets appear one-by-one with <see cref="materialBeforeWeld"/>; tip+trigger
/// swaps to <see cref="materialAfterWeld"/> and advances (trigger must be released between steps).
/// </summary>
public class SequentialWeldRevealSequence : MonoBehaviour, IWeldStepCompletable
{
    public const int StepCount = 8;

    [System.Serializable]
    public class WeldRevealStep
    {
        [Tooltip("Mesh to show/weld.")]
        public Renderer targetRenderer;

        [Tooltip("Surface the gun tip must touch (overlap) while triggering.")]
        public Collider weldTouchCollider;

        [Tooltip("If set, toggles visibility on this GO. If null, uses Target Renderer GameObject.")]
        public GameObject visibilityRoot;
    }

    [Header("Start gate")]
    [Tooltip(
        "If set, unlocking requires WeldbMergedFlipSnap — HasCompletedFinalFlipSnap (merged frame has been flip-snapped once). Overrides the bar-snap checks below for a second-phase 8-weld.")]
    public WeldbarMergedFlipSnapToAnchor requireFlipSnapCompletedOn;

    [Tooltip("If set, unlocking requires AreAllWeldbarsSnapped on this assembly. Unused when Flip Snap Gate is assigned.")]
    public WeldbarAssemblyRoot assemblySnapSource;

    [Tooltip("Four weld bars—all must report IsSnapped when Assembly Snap Source is null. Unused when Flip Snap Gate is assigned.")]
    public weldbar[] weldBarsSnapCheck = new weldbar[4];

    [Header("Clamp ground gate (ghost visibility)")]
    [Tooltip("Optional. Unwelded weld-dot ghosts stay hidden until this clamp is grounded. Leave empty to skip the clamp gate. Already-welded dots stay visible.")]
    public clamp requireClampGrounded;

    [Tooltip("Optional respective location: when set with Require Clamp Grounded, ghosts show only while that clamp is grounded on THIS exampleclamp (not another Path 1 / Path 2 slot). Leave empty to accept any grounded location on the assigned clamp.")]
    public exampleclamp requireGroundedAtGuide;

    [Header("Gun")]
    public CustomWeldingController weldingGun;

    [Tooltip("Tip collider inferred from welding gun when null.")]
    public Collider gunTipColliderOverride;

    [Tooltip("Tip-to-surface distance treated as touching (similar to welding sim).")]
    public float tipContactGapTolerance = 0.008f;

    [Header("Visuals")]
    public Material materialBeforeWeld;

    public Material materialAfterWeld;

    [Header("Steps (four parents × two children → order child0,child1 each parent).")]
    public WeldRevealStep[] steps = new WeldRevealStep[StepCount];

    [Header("Events")]
    public UnityEvent onSequenceUnlockedFirstReveal;

    public UnityEvent onAllEightWelded;

    bool _sequenceUnlocked;

    /// <summary>Index 0–7 awaiting weld reveal (only that object is interactively active).</summary>
    int _waitingIndex;

    /// <summary>Player must release trigger after each weld before the next can register.</summary>
    bool _triggerReleasedLatch = true;

    void Awake()
    {
        if (weldingGun == null)
        {
            Debug.LogWarning($"{nameof(SequentialWeldRevealSequence)}: assign {nameof(weldingGun)}.", this);
            return;
        }

        if (requireFlipSnapCompletedOn != null && assemblySnapSource != null)
        {
            Debug.LogWarning($"{nameof(SequentialWeldRevealSequence)} ({name}): {nameof(requireFlipSnapCompletedOn)} gates this sequence — {nameof(assemblySnapSource)} is ignored for unlocking.", this);
        }

        HideAllSteps();
    }

    void Update()
    {
        if (weldingGun == null || materialBeforeWeld == null || materialAfterWeld == null)
            return;

        if (!WeldbarRequirementMet())
            return;

        Collider gunTip = gunTipColliderOverride != null ? gunTipColliderOverride : weldingGun.tipContactCollider;

        bool triggerHeld = weldingGun.IsTriggerHeldForWelding();
        if (!triggerHeld)
            _triggerReleasedLatch = true;

        if (!_sequenceUnlocked)
        {
            if (!weldingGun.gunGrabbable.BeingHeld || !weldingGun.AreWeldingPrerequisitesMet())
                return;

            _sequenceUnlocked = true;
            _waitingIndex = 0;
            _triggerReleasedLatch = triggerHeld ? false : true;
            onSequenceUnlockedFirstReveal?.Invoke();
        }

        if (_waitingIndex < 0 || _waitingIndex >= StepCount)
            return;

        if (!_triggerReleasedLatch)
            return;

        // Welding still uses existing gun/prereq rules; ghosts alone are clamp-gated.
        WeldRevealStep active = steps[_waitingIndex];
        if (active == null || active.weldTouchCollider == null || active.targetRenderer == null)
            return;

        if (gunTip != null &&
            TipHasPhysicalOverlap(gunTip, active.weldTouchCollider, tipContactGapTolerance) &&
            triggerHeld)
        {
            GameObject root = active.visibilityRoot != null ? active.visibilityRoot : active.targetRenderer.gameObject;
            if (root != null)
                root.SetActive(true);
            active.targetRenderer.material = materialAfterWeld;
            _waitingIndex++;
            _triggerReleasedLatch = false;

            if (_waitingIndex >= StepCount)
                onAllEightWelded?.Invoke();
            // Next unwelded ghost is shown (or kept hidden) by LateUpdate → UpdateCurrentGhostVisibility.
        }
    }

    /// <summary>
    /// LateUpdate so clamp unsnap (clears <see cref="clamp.GroundedGuide"/>) in Update is visible
    /// the same frame — unwelded ghosts hide immediately when the clamp is picked up.
    /// </summary>
    void LateUpdate()
    {
        if (!_sequenceUnlocked || materialBeforeWeld == null)
            return;
        UpdateCurrentGhostVisibility();
    }

    /// <summary>
    /// Clamp gate for ghost visibility only — does not block sequence unlock or weld completion.
    /// Empty <see cref="requireClampGrounded"/> = no clamp requirement.
    /// If a guide is assigned without a clamp reference, deny (do not show ghosts).
    /// Partial weld progress never bypasses this gate for remaining unwelded steps.
    /// </summary>
    bool IsClampGhostGateMet()
    {
        if (requireClampGrounded == null)
            return requireGroundedAtGuide == null;
        return requireClampGrounded.IsGroundedAt(requireGroundedAtGuide);
    }

    /// <summary>
    /// Shows the current unwelded step ghost only while the clamp ground gate passes.
    /// Every other unwelded step stays hidden (partial progress must not leave extra ghosts on).
    /// Already-welded steps stay visible with <see cref="materialAfterWeld"/>.
    /// </summary>
    void UpdateCurrentGhostVisibility()
    {
        if (!_sequenceUnlocked || steps == null)
            return;

        bool clampOk = IsClampGhostGateMet();
        int count = Mathf.Min(steps.Length, StepCount);

        for (int i = 0; i < count; i++)
        {
            WeldRevealStep step = steps[i];
            if (step == null || step.targetRenderer == null)
                continue;

            // Welded real visuals — keep on with after-weld material; never treat as ghosts.
            if (i < _waitingIndex)
            {
                EnsureWeldedStepVisible(step);
                continue;
            }

            // Unwelded: only the current waiting index may show, and only while clamp-grounded.
            if (i == _waitingIndex && clampOk && _waitingIndex < StepCount)
                ShowStepInactiveMaterial(i);
            else
                SetStepHidden(step);
        }
    }

    void EnsureWeldedStepVisible(WeldRevealStep step)
    {
        if (step == null || step.targetRenderer == null)
            return;

        GameObject root = step.visibilityRoot != null ? step.visibilityRoot : step.targetRenderer.gameObject;
        if (root != null && !root.activeSelf)
            root.SetActive(true);

        if (materialAfterWeld != null)
            step.targetRenderer.material = materialAfterWeld;
    }

    public bool SequenceUnlocked => _sequenceUnlocked;

    /// <summary>Current step waiting for weld (0–7), or StepCount when finished.</summary>
    public int CurrentWaitingIndex => _waitingIndex;

    /// <summary>True after the eighth weld material swap (gates depend on scene wiring: bars snapped, flip snap, etc.).</summary>
    public bool HasCompletedAllWeldSteps => _waitingIndex >= StepCount;

    /// <summary>Step is complete once all weld steps are done.</summary>
    public bool IsStepComplete => HasCompletedAllWeldSteps;

    /// <summary>
    /// Debug/test: mark every step welded (after material), unlock the sequence, and fire
    /// <see cref="onAllEightWelded"/>. Bypasses gun/trigger/gates so downstream logic can continue.
    /// </summary>
    public void ForceCompleteAllStepsForDebug()
    {
        if (steps == null)
            return;

        if (materialAfterWeld == null)
        {
            Debug.LogWarning($"{nameof(SequentialWeldRevealSequence)} ({name}): ForceComplete needs materialAfterWeld assigned.", this);
            return;
        }

        _sequenceUnlocked = true;
        _triggerReleasedLatch = true;

        for (int i = 0; i < steps.Length; i++)
        {
            WeldRevealStep step = steps[i];
            if (step == null || step.targetRenderer == null)
                continue;

            GameObject root = step.visibilityRoot != null ? step.visibilityRoot : step.targetRenderer.gameObject;
            if (root != null)
                root.SetActive(true);
            step.targetRenderer.material = materialAfterWeld;
        }

        bool alreadyDone = _waitingIndex >= StepCount;
        _waitingIndex = StepCount;

        if (!alreadyDone)
            onAllEightWelded?.Invoke();
    }

    bool WeldbarRequirementMet()
    {
        if (requireFlipSnapCompletedOn != null)
            return requireFlipSnapCompletedOn.HasCompletedFinalFlipSnap;

        if (assemblySnapSource != null)
            return assemblySnapSource.AreAllWeldbarsSnapped;

        if (weldBarsSnapCheck == null || weldBarsSnapCheck.Length < 4)
            return false;

        for (int i = 0; i < 4; i++)
        {
            if (weldBarsSnapCheck[i] == null || !weldBarsSnapCheck[i].IsSnapped)
                return false;
        }

        return true;
    }

    void HideAllSteps()
    {
        if (steps == null)
            return;

        for (int i = 0; i < steps.Length; i++)
            SetStepHidden(steps[i]);
    }

    static void SetStepHidden(WeldRevealStep step)
    {
        if (step == null || step.targetRenderer == null)
            return;

        GameObject root = step.visibilityRoot != null ? step.visibilityRoot : step.targetRenderer.gameObject;
        root.SetActive(false);
    }

    void ShowStepInactiveMaterial(int index)
    {
        WeldRevealStep step = steps[index];
        if (step == null || step.targetRenderer == null)
            return;

        GameObject root = step.visibilityRoot != null ? step.visibilityRoot : step.targetRenderer.gameObject;
        root.SetActive(true);
        step.targetRenderer.material = materialBeforeWeld;
    }

    static bool TipHasPhysicalOverlap(Collider tipCollider, Collider surfaceCollider, float tolerance)
    {
        if (tipCollider == null || surfaceCollider == null)
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
