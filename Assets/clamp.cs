using UnityEngine;
using TMPro;
using BNG;

/// <summary>
/// Real movable clamp with snap-to-guide behaviour. Supports Path 1 (panel) and Path 2 (frame/assembly) ghosts.
/// Path 2 can be an ordered sequence of <see cref="exampleclamp"/> slots (same ghost may appear twice, like
/// reused <see cref="exampleframe"/> sets in <see cref="WeldbMergedGrabRevealExamples"/>). While held, only the
/// closest <em>eligible</em> ghost is shown; eligibility is gated by Inspector-assigned prerequisites.
/// Path 2 step index advances when Inspector-assigned weld completables finish — not when the clamp snaps.
/// </summary>
public class clamp : MonoBehaviour
{
    /// <summary>
    /// Weld-completion gate for leaving one Path 2 guide step. All assigned
    /// <see cref="IWeldStepCompletable"/> sources must report complete before the next
    /// <see cref="path2GuidesInOrder"/> element becomes active. Leave empty on the final step.
    /// </summary>
    [System.Serializable]
    public class Path2StepAdvanceCriteria
    {
        [Tooltip("ALL listed completables must report IsStepComplete to leave this Path 2 step (e.g. TopWeldDots + BottomWeldDots SequentialWeldRevealSequence, CornerWelds / TopWelds / BottomWelds WeldLinesRevealOnSnap, or a WeldStepGroup). Drag the specific components. Leave empty for the final step (no further advance).")]
        public MonoBehaviour[] requireComplete;
    }

    [Header("Matching")]
    [Tooltip("Asset key for this clamp. Must match exampleclamp.nameofasset.")]
    public string nameofasset;

    [Header("References")]
    [Tooltip("The Grabbable on the real movable clamp object (this object usually has it). If null, auto-finds.")]
    public Grabbable realClampGrabbable;

    [Tooltip("Path 1 guide (weld-panel path ghost). Shown while held when Path 1 prerequisites are met, and this guide is closer than Path 2 (if Path 2 is also eligible).")]
    public exampleclamp guide;

    [Tooltip("Collider on the stationary box that the clamp must touch.")]
    public Collider boxColliderToSnapTo;

    [Tooltip("If true, collision with the guide object's collider can also trigger snapping.")]
    public bool allowGuideColliderAsSnapTarget = true;

    [Header("Guide — Path 2 (optional)")]
    [Tooltip("Ordered Path 2 placement steps (typically 4: after refs+bars → Top/Bottom weld dots → Corner welds → Top/Bottom Welds → final). Same exampleclamp may appear at two indices when reused (e.g. [A, B, A, C]). Only the current step's guide is eligible. Step advances when Path 2 Advance When Complete for that index finishes — snap alone does NOT advance. Non-current Path 2 ghosts stay hidden. Prefer this over Second Guide.")]
    public exampleclamp[] path2GuidesInOrder;

    [Tooltip("Per Path 2 step (index-aligned with Path 2 Guides In Order). When ALL Require Complete sources for the CURRENT step report IsStepComplete, path2StepIndex advances to the next guide. Typical: [0]=TopWeldDots+BottomWeldDots, [1]=CornerWelds, [2]=TopWelds+BottomWelds, [3]=empty (final). Snap does not advance.")]
    public Path2StepAdvanceCriteria[] path2AdvanceWhenComplete;

    [Tooltip("Legacy single Path 2 guide. Used only when Path 2 Guides In Order is empty. Prefer Path 2 Guides In Order for multi-step / reused ghosts.")]
    public exampleclamp secondGuide;

    [Header("Path 1 prerequisites — weld panel")]
    [Tooltip("Path 1 (panel path): Guide becomes eligible only when a listed ungroundedgrabbable reports IsSnappedToWeldingSheet. Leave empty (and Path 1 Sheets empty) to always allow Path 1 whenever Guide is assigned.")]
    public ungroundedgrabbable[] path1PanelsRequireSnapped;

    [Tooltip("Path 1 alternate/additional gate: Guide becomes eligible when a listed exampleweldingsheet has any workpiece snapped (GetSnappedWorkpiece). Combined with Path 1 Panels — see Path 1 Require Any.")]
    public exampleweldingsheet[] path1SheetsRequireOccupied;

    [Tooltip("If true (default): Path 1 unlocks when ANY listed panel is sheet-snapped OR ANY listed sheet is occupied. If false: ALL listed panels must be sheet-snapped AND ALL listed sheets must be occupied (empty lists are skipped).")]
    public bool path1RequireAny = true;

    [Header("Path 2 prerequisites — refs + bars")]
    [Tooltip("Path 2 (frame/assembly): Path 2 ghosts become eligible only when ALL listed ref pieces report IsSnapped. Leave empty to skip this check.")]
    public refpiece[] path2RefPiecesRequireSnapped;

    [Tooltip("Path 2: ALL listed weld bars must report IsSnapped. Leave empty to skip this check.")]
    public weldbar[] path2WeldbarsRequireSnapped;

    [Tooltip("Path 2 catch-all: ALL listed completables must report IsStepComplete (e.g. WeldStepGroup of refs+bars, WeldbarAssemblyRoot). Drag the component. Leave empty to skip. If refs/bars/this are all empty, Path 2 is eligible whenever a current Path 2 guide exists.")]
    public MonoBehaviour[] path2AdditionalPrerequisites;

    [Header("Behaviour")]
    [Tooltip("Show guide only while the real clamp is held.")]
    public bool showGuideWhileHeld = true;

    [Tooltip("When true, snap only if the clamp is currently being held.")]
    public bool requireClampHeldForSnap = true;

    [Tooltip("If true, zero rigidbody velocity/angular velocity right after snapping for stability.")]
    public bool zeroPhysicsOnSnap = true;

    [Tooltip("If true, also apply the guide's local scale when snapping.")]
    public bool applyGuideScaleOnSnap = true;

    [Header("Post snap freeze & grab")]
    [Tooltip("When on, the clamp snaps, becomes kinematic, and stays frozen in place until you grab it again (after the grab cooldown). When off, no rigidbody freeze and grab cooldown below is not used.")]
    public bool freezeAfterSnap = true;

    [Tooltip("Only when freeze after snap is on. After snap, grabbing is disabled this many seconds so physics/controllers settle; the clamp stays frozen until then and only unfreezes when you grab.")]
    public float grabCooldownAfterSnapSeconds = 0.5f;

    [Tooltip("If true, Grabbable is disabled after snap (permanent until you re-enable in editor). Leave false to grab again, remove the clamp, and snap again later.")]
    public bool disableGrabAfterSnap = false;

    [Header("Cooldowns")]
    [Tooltip("After you grab a snapped clamp, snap is blocked for this many seconds (increase if unwanted re-snaps while moving).")]
    public float snapCooldownAfterPickupSeconds = 0.35f;

    [Tooltip("During the post-pickup cooldown (and while seated), ignore physics between this clamp and its guide(s) / snap box so unclamping is smooth — collisions pass through.")]
    public bool ignoreCollisionsDuringUnsnapCooldown = true;

    [Header("Jointed frame pass-through")]
    [Tooltip("Merged weldbar assembly (RealFrame / WeldbarAssemblyRoot). While this clamp is snapped — and during unsnap cooldown — ignore collisions with the jointed frame so lift-off is not blocked. Leave empty to auto-find.")]
    public WeldbarAssemblyRoot jointedFrameAssembly;

    [Header("Grounding status (optional)")]
    [Tooltip("TextMeshPro to show grounded vs not grounded after snap.")]
    public TMP_Text groundedStatusText;
    [Tooltip("Shown after a snap that counts as grounded (see below).")]
    public string textWhenGrounded = "grounded";
    [Tooltip("Shown before snap or when not grounded.")]
    public string textWhenNotGrounded = "not grounded";

    [Tooltip("If true, 'grounded' only when the snap was triggered by contact with the guide (exampleclamp) collider. If false, any successful snap shows grounded.")]
    public bool groundedTextOnlyWhenSnapFromGuide = false;

    [Header("Debug (optional)")]
    public bool debug = false;

    /// <summary>True after a successful snap (for relays / UI).</summary>
    public bool IsSnapped => snapped;

    /// <summary>True when the clamp is snapped / grounded (same as <see cref="IsSnapped"/>).</summary>
    public bool IsGrounded() => snapped;

    /// <summary>True if the snap was triggered by touching the guide (exampleclamp) collider.</summary>
    public bool SnappedViaGuideContact => snappedViaGuideContact;

    /// <summary>
    /// The <see cref="exampleclamp"/> this clamp is currently grounded on (null when not snapped).
    /// Survives Path 2 step advance so weld ghosts can gate on the location that was just used.
    /// </summary>
    public exampleclamp GroundedGuide => snapped ? snappedGuide : null;

    /// <summary>
    /// True when this clamp is grounded. If <paramref name="guide"/> is assigned, also requires
    /// grounding specifically on that <see cref="exampleclamp"/> (respective location).
    /// </summary>
    public bool IsGroundedAt(exampleclamp guide)
    {
        if (!snapped)
            return false;
        if (guide == null)
            return true;
        return snappedGuide == guide;
    }

    /// <summary>
    /// Guide currently in use for visibility + snap: among eligible Path 1 / Path 2 ghosts,
    /// the closest to this clamp. Null when neither path is ready.
    /// </summary>
    public exampleclamp ActiveGuide
    {
        get
        {
            bool path1 = IsPath1Eligible();
            bool path2 = IsPath2Eligible();
            exampleclamp path2Guide = CurrentPath2Guide;

            if (!path1 && !path2)
                return null;
            if (path1 && !path2)
                return guide;
            if (!path1 && path2)
                return path2Guide;

            // Both eligible: pick closer by world distance from this clamp to each guide's snap pose.
            float d1 = DistanceFromClampToGuide(guide);
            float d2 = DistanceFromClampToGuide(path2Guide);
            return d1 <= d2 ? guide : path2Guide;
        }
    }

    /// <summary>
    /// Path 2 ghost for the current placement step (ordered list), or legacy <see cref="secondGuide"/> when the list is empty.
    /// Null when the step index is out of range or no Path 2 guide is assigned. The final ordered slot stays active
    /// (no advance past the last element).
    /// </summary>
    public exampleclamp CurrentPath2Guide
    {
        get
        {
            if (path2GuidesInOrder != null && path2GuidesInOrder.Length > 0)
            {
                if (path2StepIndex < 0 || path2StepIndex >= path2GuidesInOrder.Length)
                    return null;
                return path2GuidesInOrder[path2StepIndex];
            }

            return secondGuide;
        }
    }

    /// <summary>Current index into <see cref="path2GuidesInOrder"/> (advances when that step's weld completables finish).</summary>
    public int Path2StepIndex => path2StepIndex;

    private bool snapped = false;
    private bool snappedViaGuideContact = false;
    private exampleclamp snappedGuide;
    private float nextSnapEligibleTime = 0f;
    private bool wasHeldLastFrame = false;
    private bool grabCooldownActive = false;
    private float grabCooldownEndsAtTime = 0f;
    private bool rigidbodyFrozenBySnap = false;
    private bool guideCollisionsIgnored;
    private bool jointedFrameCollisionsIgnored;
    private int path2StepIndex = 0;

    private Rigidbody rb;

    void Awake()
    {
        if (realClampGrabbable == null)
            realClampGrabbable = GetComponent<Grabbable>() ?? GetComponentInParent<Grabbable>();

        rb = GetComponent<Rigidbody>() ?? GetComponentInParent<Rigidbody>() ?? GetComponentInChildren<Rigidbody>();

        if (rb != null && rb.gameObject != gameObject)
        {
            if (rb.gameObject.GetComponent<ClampCollisionRelay>() == null)
            {
                var relay = rb.gameObject.AddComponent<ClampCollisionRelay>();
                relay.owner = this;
            }
        }

        if (guide != null)
            guide.SetVisible(false);
        HideAllPath2Guides();
    }

    void OnDisable()
    {
        if (grabCooldownActive && realClampGrabbable != null)
        {
            realClampGrabbable.enabled = true;
            grabCooldownActive = false;
        }
        SetUnsnapCollisionsIgnored(false);
        SetJointedFrameCollisionsIgnored(false);
    }

    void OnDestroy()
    {
        SetUnsnapCollisionsIgnored(false);
        SetJointedFrameCollisionsIgnored(false);
    }

    void Start()
    {
        wasHeldLastFrame = IsBeingHeld();
        UpdateGuideVisibility(IsBeingHeld());
        UpdateGroundedStatusText();
    }

    void Update()
    {
        if (grabCooldownActive && realClampGrabbable != null && Time.time >= grabCooldownEndsAtTime)
        {
            realClampGrabbable.enabled = true;
            grabCooldownActive = false;
            if (debug)
                Debug.Log("clamp: grab cooldown ended");
        }

        // End pass-through once post-pickup cooldown finishes (and not still seated).
        if (guideCollisionsIgnored && !snapped && !grabCooldownActive && Time.time >= nextSnapEligibleTime)
            SetUnsnapCollisionsIgnored(false);
        if (jointedFrameCollisionsIgnored && !snapped && !grabCooldownActive && Time.time >= nextSnapEligibleTime)
            SetJointedFrameCollisionsIgnored(false);

        bool held = IsBeingHeld();

        if (snapped && held)
        {
            // BNG Grabbable.ResetGrabbing() may set isKinematic = wasKinematic before this runs, but
            // RigidbodyConstraints.FreezeAll from snap is not cleared by the framework — clear it here
            // whenever we had frozen by snap (do not require isKinematic or movement stays locked).
            if (rigidbodyFrozenBySnap && rb != null)
            {
                rb.velocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
                rb.isKinematic = false;
                rb.constraints = RigidbodyConstraints.None;
                rigidbodyFrozenBySnap = false;
                if (debug)
                    Debug.Log("clamp: released snap freeze (kinematic + constraints) for grab");
            }

            if (!wasHeldLastFrame)
            {
                snapped = false;
                snappedViaGuideContact = false;
                snappedGuide = null;
                nextSnapEligibleTime = Time.time + Mathf.Max(0f, snapCooldownAfterPickupSeconds);
                // Pass through guide / snap-box collisions for the unsnap cooldown window.
                if (ignoreCollisionsDuringUnsnapCooldown)
                {
                    SetUnsnapCollisionsIgnored(true);
                    SetJointedFrameCollisionsIgnored(true);
                }
                else
                {
                    SetJointedFrameCollisionsIgnored(false);
                }
                UpdateGroundedStatusText();
            }
        }

        wasHeldLastFrame = held;

        TryAdvancePath2ByWeldCompletion();

        if (!snapped)
            UpdateGuideVisibility(held);
    }

    void UpdateGuideVisibility(bool beingHeld)
    {
        exampleclamp active = ActiveGuide;

        // Keep non-active guides hidden so only one shows at a time (Path 1 + all Path 2 slots).
        if (guide != null && guide != active)
            guide.SetVisible(false);
        HidePath2GuidesExcept(active);

        if (active == null)
            return;

        if (!showGuideWhileHeld)
        {
            active.SetVisible(false);
            return;
        }

        // Rising-edge reshown on a reused Path 2 ghost re-arms like exampleframe.reArmSnapWhenReshown.
        active.SetVisible(beingHeld);
    }

    /// <summary>Path 1 (panel): Guide assigned and panel/sheet prerequisites satisfied (or none configured).</summary>
    public bool IsPath1Eligible()
    {
        if (guide == null)
            return false;

        bool hasPanelGates = path1PanelsRequireSnapped != null && path1PanelsRequireSnapped.Length > 0;
        bool hasSheetGates = path1SheetsRequireOccupied != null && path1SheetsRequireOccupied.Length > 0;
        if (!hasPanelGates && !hasSheetGates)
            return true;

        if (path1RequireAny)
        {
            if (hasPanelGates && AnyPanelSnappedToSheet())
                return true;
            if (hasSheetGates && AnySheetOccupied())
                return true;
            return false;
        }

        // Require all configured gates.
        if (hasPanelGates && !AllPanelsSnappedToSheet())
            return false;
        if (hasSheetGates && !AllSheetsOccupied())
            return false;
        return true;
    }

    /// <summary>
    /// Path 2 (frame/assembly): current step guide exists and all configured ref/bar/completable prerequisites met.
    /// Locked until refs+bars (and optional completables) are done — same gate as before; step sequence only applies after that.
    /// </summary>
    public bool IsPath2Eligible()
    {
        if (CurrentPath2Guide == null)
            return false;

        if (!AllRefPiecesSnapped())
            return false;
        if (!AllWeldbarsSnapped())
            return false;
        if (!AllAdditionalPrerequisitesComplete())
            return false;

        return true;
    }

    bool AnyPanelSnappedToSheet()
    {
        if (path1PanelsRequireSnapped == null)
            return false;
        for (int i = 0; i < path1PanelsRequireSnapped.Length; i++)
        {
            ungroundedgrabbable p = path1PanelsRequireSnapped[i];
            if (p != null && p.IsSnappedToWeldingSheet())
                return true;
        }
        return false;
    }

    bool AllPanelsSnappedToSheet()
    {
        if (path1PanelsRequireSnapped == null || path1PanelsRequireSnapped.Length == 0)
            return true;
        bool anyValid = false;
        for (int i = 0; i < path1PanelsRequireSnapped.Length; i++)
        {
            ungroundedgrabbable p = path1PanelsRequireSnapped[i];
            if (p == null)
                continue;
            anyValid = true;
            if (!p.IsSnappedToWeldingSheet())
                return false;
        }
        return anyValid;
    }

    bool AnySheetOccupied()
    {
        if (path1SheetsRequireOccupied == null)
            return false;
        for (int i = 0; i < path1SheetsRequireOccupied.Length; i++)
        {
            exampleweldingsheet s = path1SheetsRequireOccupied[i];
            if (s != null && s.GetSnappedWorkpiece() != null)
                return true;
        }
        return false;
    }

    bool AllSheetsOccupied()
    {
        if (path1SheetsRequireOccupied == null || path1SheetsRequireOccupied.Length == 0)
            return true;
        bool anyValid = false;
        for (int i = 0; i < path1SheetsRequireOccupied.Length; i++)
        {
            exampleweldingsheet s = path1SheetsRequireOccupied[i];
            if (s == null)
                continue;
            anyValid = true;
            if (s.GetSnappedWorkpiece() == null)
                return false;
        }
        return anyValid;
    }

    bool AllRefPiecesSnapped()
    {
        if (path2RefPiecesRequireSnapped == null || path2RefPiecesRequireSnapped.Length == 0)
            return true;
        bool anyValid = false;
        for (int i = 0; i < path2RefPiecesRequireSnapped.Length; i++)
        {
            refpiece r = path2RefPiecesRequireSnapped[i];
            if (r == null)
                continue;
            anyValid = true;
            if (!r.IsSnapped)
                return false;
        }
        return anyValid;
    }

    bool AllWeldbarsSnapped()
    {
        if (path2WeldbarsRequireSnapped == null || path2WeldbarsRequireSnapped.Length == 0)
            return true;
        bool anyValid = false;
        for (int i = 0; i < path2WeldbarsRequireSnapped.Length; i++)
        {
            weldbar b = path2WeldbarsRequireSnapped[i];
            if (b == null)
                continue;
            anyValid = true;
            if (!b.IsSnapped)
                return false;
        }
        return anyValid;
    }

    bool AllAdditionalPrerequisitesComplete()
    {
        if (path2AdditionalPrerequisites == null || path2AdditionalPrerequisites.Length == 0)
            return true;
        bool anyValid = false;
        for (int i = 0; i < path2AdditionalPrerequisites.Length; i++)
        {
            IWeldStepCompletable c = ResolveCompletable(path2AdditionalPrerequisites[i]);
            if (c == null)
                continue;
            anyValid = true;
            if (!c.IsStepComplete)
                return false;
        }
        return anyValid;
    }

    /// <summary>
    /// Advances <see cref="path2StepIndex"/> when the current step's
    /// <see cref="path2AdvanceWhenComplete"/> sources are all complete. Cascades if later steps
    /// are already done. Stops on the final step (empty criteria / last index). Snap alone never calls this.
    /// </summary>
    void TryAdvancePath2ByWeldCompletion()
    {
        if (path2GuidesInOrder == null || path2GuidesInOrder.Length == 0)
            return;
        if (path2AdvanceWhenComplete == null || path2AdvanceWhenComplete.Length == 0)
            return;

        while (path2StepIndex >= 0 && path2StepIndex < path2GuidesInOrder.Length - 1
               && path2StepIndex < path2AdvanceWhenComplete.Length)
        {
            Path2StepAdvanceCriteria criteria = path2AdvanceWhenComplete[path2StepIndex];
            if (criteria == null || criteria.requireComplete == null || criteria.requireComplete.Length == 0)
                return;
            if (!AllCompletablesComplete(criteria.requireComplete))
                return;

            path2StepIndex++;
            if (debug)
                Debug.Log("clamp: Path 2 step advanced by weld completion to " + path2StepIndex + " / " + path2GuidesInOrder.Length);
        }
    }

    static bool AllCompletablesComplete(MonoBehaviour[] sources)
    {
        if (sources == null || sources.Length == 0)
            return false;

        bool anyValid = false;
        for (int i = 0; i < sources.Length; i++)
        {
            IWeldStepCompletable c = ResolveCompletable(sources[i]);
            if (c == null)
                continue;
            anyValid = true;
            if (!c.IsStepComplete)
                return false;
        }
        return anyValid;
    }

    static IWeldStepCompletable ResolveCompletable(MonoBehaviour source)
    {
        if (source == null)
            return null;
        if (source is IWeldStepCompletable direct)
            return direct;
        return source.GetComponent<IWeldStepCompletable>();
    }

    float DistanceFromClampToGuide(exampleclamp g)
    {
        if (g == null)
            return float.PositiveInfinity;
        Transform snap = g.GetSnapTransform();
        Vector3 target = snap != null ? snap.position : g.transform.position;
        return Vector3.Distance(transform.position, target);
    }

    bool IsBeingHeld()
    {
        return realClampGrabbable != null && realClampGrabbable.BeingHeld;
    }

    void OnTriggerEnter(Collider other)
    {
        HandleTrigger(other);
    }

    public void HandleTrigger(Collider other)
    {
        TrySnapFromCollider(other, "trigger");
    }

    void OnCollisionEnter(Collision collision)
    {
        HandleCollision(collision);
    }

    void OnCollisionStay(Collision collision)
    {
        HandleCollision(collision);
    }

    public void HandleCollision(Collision collision)
    {
        if (collision == null)
            return;
        TrySnapFromCollider(collision.collider, "collision");
    }

    void TrySnapFromCollider(Collider other, string source)
    {
        if (snapped)
            return;

        if (Time.time < nextSnapEligibleTime)
            return;

        if (other == null)
            return;

        if (IsColliderPartOfThisHierarchy(other))
            return;

        if (!IsSnapTargetCollider(other))
            return;

        if (debug)
            Debug.Log("clamp: " + source + " contact with snap target: " + other.name);

        if (requireClampHeldForSnap && !IsBeingHeld())
        {
            if (debug)
                Debug.Log("clamp: snap blocked (not held)");
            return;
        }

        if (!IsMatchingGuide())
        {
            exampleclamp active = ActiveGuide;
            if (debug)
                Debug.Log("clamp: snap blocked (nameofasset mismatch: clamp='" + nameofasset + "' guide='" + (active != null ? active.nameofasset : "null") + "')");
            return;
        }

        bool contactWasGuide = IsContactWithGuideCollider(other);
        exampleclamp guideAtSnap = ActiveGuide;
        if (!SnapToGuide(applyGuideScaleOnSnap))
            return;

        snapped = true;
        snappedViaGuideContact = contactWasGuide;
        snappedGuide = guideAtSnap;
        UpdateGroundedStatusText();
    }

    bool IsColliderPartOfThisHierarchy(Collider other)
    {
        Transform t = other.transform;
        return t == transform || t.IsChildOf(transform);
    }

    bool IsSameCollider(Collider a, Collider b)
    {
        if (a == null || b == null)
            return false;

        return a == b || a.transform == b.transform || a.transform.IsChildOf(b.transform) || b.transform.IsChildOf(a.transform);
    }

    bool IsSnapTargetCollider(Collider other)
    {
        if (other == null)
            return false;

        if (boxColliderToSnapTo != null && IsSameCollider(other, boxColliderToSnapTo))
            return true;

        exampleclamp active = ActiveGuide;
        if (allowGuideColliderAsSnapTarget && active != null)
        {
            Collider[] guideCols = active.GetComponentsInChildren<Collider>(true);
            for (int i = 0; i < guideCols.Length; i++)
            {
                Collider gc = guideCols[i];
                if (gc != null && gc.enabled && IsSameCollider(other, gc))
                    return true;
            }
        }

        return false;
    }

    bool IsContactWithGuideCollider(Collider other)
    {
        exampleclamp active = ActiveGuide;
        if (other == null || active == null || !allowGuideColliderAsSnapTarget)
            return false;

        Collider[] guideCols = active.GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < guideCols.Length; i++)
        {
            Collider gc = guideCols[i];
            if (gc != null && gc.enabled && IsSameCollider(other, gc))
                return true;
        }

        return false;
    }

    bool SnapToGuide(bool applyScale)
    {
        exampleclamp active = ActiveGuide;
        if (active == null)
            return false;

        Transform snap = active.GetSnapTransform();
        if (snap == null)
            return false;

        transform.SetPositionAndRotation(snap.position, snap.rotation);

        if (rb != null && zeroPhysicsOnSnap)
        {
            rb.velocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        if (applyScale && snap.parent == transform.parent)
        {
            transform.localScale = snap.localScale;
        }
        else if (applyScale)
        {
            transform.localScale = snap.localScale;
        }

        if (debug)
            Debug.Log("clamp: snapped to guide");

        active.SetVisible(false);
        if (guide != null && guide != active)
            guide.SetVisible(false);
        HidePath2GuidesExcept(null);

        // Path 2 step index advances only via TryAdvancePath2ByWeldCompletion (weld dots / lines),
        // not when the clamp snaps onto the current ghost.

        // Ignore while seated so lift-off after grab is already non-blocking.
        if (ignoreCollisionsDuringUnsnapCooldown)
            SetUnsnapCollisionsIgnored(true);

        // Jointed RealFrame must not push a seated clamp (independent of guide ignore toggle).
        SetJointedFrameCollisionsIgnored(true);

        rigidbodyFrozenBySnap = false;

        if (freezeAfterSnap)
        {
            if (rb != null)
            {
                rb.velocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
                rb.isKinematic = true;
                rb.constraints = RigidbodyConstraints.FreezeAll;
                rigidbodyFrozenBySnap = true;
            }

            if (disableGrabAfterSnap && realClampGrabbable != null)
            {
                realClampGrabbable.DropItem(true, true);
                realClampGrabbable.enabled = false;
                realClampGrabbable = null;
            }
            else if (!disableGrabAfterSnap && grabCooldownAfterSnapSeconds > 0f && realClampGrabbable != null)
            {
                grabCooldownActive = true;
                grabCooldownEndsAtTime = Time.time + Mathf.Max(0f, grabCooldownAfterSnapSeconds);
                if (realClampGrabbable.BeingHeld)
                    realClampGrabbable.DropItem(true, true);
                realClampGrabbable.enabled = false;
                if (debug)
                    Debug.Log("clamp: grab cooldown started for " + grabCooldownAfterSnapSeconds + " s");
            }
        }
        else
        {
            if (disableGrabAfterSnap && realClampGrabbable != null)
            {
                realClampGrabbable.DropItem(true, true);
                realClampGrabbable.enabled = false;
                realClampGrabbable = null;
            }
        }

        return true;
    }

    void UpdateGroundedStatusText()
    {
        if (groundedStatusText == null)
            return;

        bool grounded = snapped;
        if (grounded && groundedTextOnlyWhenSnapFromGuide)
            grounded = snappedViaGuideContact;

        groundedStatusText.text = grounded ? textWhenGrounded : textWhenNotGrounded;
    }

    bool IsMatchingGuide()
    {
        exampleclamp active = ActiveGuide;
        if (active == null)
            return false;

        if (string.IsNullOrEmpty(nameofasset))
            return true;

        return nameofasset == active.nameofasset;
    }

    /// <summary>Debug/test: snap to the active guide immediately (no grab/collision required).</summary>
    public bool ForceSnapForDebug()
    {
        if (snapped)
            return true;

        exampleclamp guideAtSnap = ActiveGuide;
        if (!SnapToGuide(applyGuideScaleOnSnap))
            return false;

        snapped = true;
        snappedViaGuideContact = true;
        snappedGuide = guideAtSnap;
        UpdateGroundedStatusText();
        return true;
    }

    /// <summary>
    /// Called by <see cref="WeldbarAssemblyRoot"/> when bars merge into one joint.
    /// If this clamp is already snapped (or in unsnap cooldown), start ignoring the jointed frame.
    /// </summary>
    public void OnJointedFrameMerged(WeldbarAssemblyRoot root)
    {
        if (root != null)
            jointedFrameAssembly = root;

        if (snapped || jointedFrameCollisionsIgnored)
            SetJointedFrameCollisionsIgnored(true);
    }

    /// <summary>
    /// Ignore physics between this clamp and the merged RealFrame assembly while snapped / unsnap cooldown.
    /// Uses force:true so it works even when the global SnapGuideCollisionIgnore master switch is off.
    /// </summary>
    void SetJointedFrameCollisionsIgnored(bool ignore)
    {
        if (jointedFrameAssembly == null)
            jointedFrameAssembly = FindObjectOfType<WeldbarAssemblyRoot>();

        if (!ignore)
        {
            if (!jointedFrameCollisionsIgnored)
                return;

            if (jointedFrameAssembly != null)
                jointedFrameAssembly.SetCollisionsIgnoredWith(transform, false);

            jointedFrameCollisionsIgnored = false;
            if (debug)
                Debug.Log("clamp: restored collisions with jointed frame");
            return;
        }

        if (jointedFrameAssembly == null || !jointedFrameAssembly.HasMergedAssembly)
            return;

        jointedFrameAssembly.SetCollisionsIgnoredWith(transform, true);
        jointedFrameCollisionsIgnored = true;
        if (debug)
            Debug.Log("clamp: ignoring collisions with jointed frame (snapped / unsnap pass-through)");
    }

    /// <summary>
    /// Ignore physics between this clamp and its assigned guide(s) + snap box.
    /// Uses force:true so it works even when the global SnapGuideCollisionIgnore master switch is off.
    /// </summary>
    void SetUnsnapCollisionsIgnored(bool ignore)
    {
        if (!ignore)
        {
            if (!guideCollisionsIgnored)
                return;

            if (guide != null)
                SnapGuideCollisionIgnore.SetIgnoredBetween(transform, guide.transform, false, force: true);
            ForEachPath2Guide(g =>
                SnapGuideCollisionIgnore.SetIgnoredBetween(transform, g.transform, false, force: true));
            if (boxColliderToSnapTo != null)
                SnapGuideCollisionIgnore.SetIgnoredBetween(transform, boxColliderToSnapTo.transform, false, force: true);

            guideCollisionsIgnored = false;
            if (debug)
                Debug.Log("clamp: restored collisions with guide(s) / snap box");
            return;
        }

        if (!ignoreCollisionsDuringUnsnapCooldown)
            return;

        // Cover Path 1 + every Path 2 guide involved (including reused slots) while seated/cooldown.
        if (guide != null)
            SnapGuideCollisionIgnore.SetIgnoredBetween(transform, guide.transform, true, force: true);
        ForEachPath2Guide(g =>
            SnapGuideCollisionIgnore.SetIgnoredBetween(transform, g.transform, true, force: true));
        if (boxColliderToSnapTo != null)
            SnapGuideCollisionIgnore.SetIgnoredBetween(transform, boxColliderToSnapTo.transform, true, force: true);

        guideCollisionsIgnored = true;
        if (debug)
            Debug.Log("clamp: ignoring collisions with guide(s) / snap box (unsnap pass-through)");
    }

    void HideAllPath2Guides()
    {
        HidePath2GuidesExcept(null);
    }

    /// <summary>Hide every Path 2 ghost except <paramref name="keep"/> (null hides all).</summary>
    void HidePath2GuidesExcept(exampleclamp keep)
    {
        ForEachPath2Guide(g =>
        {
            if (g != keep)
                g.SetVisible(false);
        });
    }

    /// <summary>
    /// Invokes <paramref name="action"/> once per unique Path 2 guide (ordered list entries + legacy secondGuide).
    /// </summary>
    void ForEachPath2Guide(System.Action<exampleclamp> action)
    {
        if (action == null)
            return;

        if (path2GuidesInOrder != null)
        {
            for (int i = 0; i < path2GuidesInOrder.Length; i++)
            {
                exampleclamp g = path2GuidesInOrder[i];
                if (g == null)
                    continue;

                bool seenEarlier = false;
                for (int j = 0; j < i; j++)
                {
                    if (path2GuidesInOrder[j] == g)
                    {
                        seenEarlier = true;
                        break;
                    }
                }

                if (!seenEarlier)
                    action(g);
            }
        }

        if (secondGuide != null)
        {
            bool alreadyInOrder = false;
            if (path2GuidesInOrder != null)
            {
                for (int i = 0; i < path2GuidesInOrder.Length; i++)
                {
                    if (path2GuidesInOrder[i] == secondGuide)
                    {
                        alreadyInOrder = true;
                        break;
                    }
                }
            }

            if (!alreadyInOrder)
                action(secondGuide);
        }
    }
}
