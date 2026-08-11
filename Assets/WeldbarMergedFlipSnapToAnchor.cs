using BNG;
using TMPro;
using UnityEngine;
using UnityEngine.Serialization;

/// <summary>
/// After merge + optional weld sequence, teleport this joint (<see cref="WeldbarAssemblyRoot"/> GameObject)
/// to <see cref="WeldbarAssemblyRoot.MergeRecenterAnchor"/> pose with extra Euler (default X +180°) when:
/// (1) this root is rotated ~180° on world/local X **or** Z Euler, and
/// (2) this body's physics <b>collision or trigger</b> with a collider whose parent has <see cref="exampleweldbar"/> (no per-frame contact polling).
/// Snaps repeatedly with weldbar-style cooldowns (optional grab cooldown after teleport, pickup cooldown before next flip).
/// </summary>
[DisallowMultipleComponent]
public class WeldbarMergedFlipSnapToAnchor : MonoBehaviour, IWeldStepCompletable
{
    [SerializeField]
    WeldbarAssemblyRoot assemblyRoot;

    [Tooltip("If set, final snap waits until the joint was grabbed once and example guides were revealed.")]
    public WeldbMergedGrabRevealExamples revealAfterGrabDriver;

    [Header("Flip gate (degrees)")]
    [Tooltip("If true, euler check uses Transform.eulerAngles in world space. If false, local euler.")]
    public bool worldSpaceEulerCheck = true;

    [Tooltip("How close axis X or Z must be to 180° to allow snap.")]
    public float euler180ToleranceDegrees = 25f;

    [Tooltip("Either X or Z may satisfy flip; enable if both must approximate 180.")]
    public bool requireBothXAndZNear180 = false;

    [Header("Snap pose (first flip snap)")]
    [Tooltip("World-space position nudge applied after the first flip snap (anchor / final example frame). Use this to manually slide the combined frame into a better fit.")]
    public Vector3 snapPositionOffset = Vector3.zero;

    [Tooltip("Extra Euler multiplied onto the merge-anchor rotation after the first flip snap (default flips ~X).")]
    public Vector3 snapRotationEulerOffset = new Vector3(180f, 0f, 0f);

    [Tooltip("If false and anchor null, snapping is skipped.")]
    public bool requireMergeAnchorAssigned = true;

    [Header("Contact gate")]
    [Tooltip("Collider on other GameObject tree must resolve to exampleweldbar. When on, snap is driven by collision/trigger callbacks (weldbar-style), not by polling overlap each physics frame.")]
    public bool requireContactWithExampleWeldbar = true;

    [Tooltip("If true, flip snap only evaluates while the merged joint Grabbable is held (like weldbar Require Held For Snap). Stops re-snapping as soon as cooldown ends if you are not holding the joint.")]
    public bool requireMergedJointHeldForFlipSnap = true;

    [Header("After snap")]
    [Tooltip("Zero velocity then kinematic freeze after snapping.")]
    public bool freezeRigidbodyAfterSnap = true;

    [Tooltip("Drop from hands - recommended so pose does not fight the grab.")]
    public bool dropGrabBeforeSnapTeleport = true;

    [Header("Cooldowns (like weldbar)")]
    [Tooltip("After flip snap, Grabbable is disabled this long so physics settles. Independent of Freeze Rigidbody After Snap; use 0 to skip.")]
    public float grabCooldownAfterFlipSnapSeconds = 0.5f;

    [Tooltip("After the joint is grabbed following a flip snap, the next flip snap stays blocked until this many seconds pass.")]
    public float snapCooldownAfterJointPickupSeconds = 0.35f;

    [Header("Example frame unsnap pass-through")]
    [Tooltip("While seated on ExampleFrame2 / ExampleFrame3 (and through grab + post-pickup cooldown), ignore physics between this joint and that example-frame hierarchy so pull-out is smooth. Does not change ghost visibility (grab-reveal still shows bars while held). Uses force:true.")]
    public bool ignoreCollisionsDuringExampleFrameUnsnapCooldown = true;

    [Header("After flip snap — pose from example frame")]
    [Tooltip("If set: after anchor snap + euler offset runs, joint moves again to match this transform (e.g. ExampleFrame root).")]
    public Transform finalPoseExampleFrame;

    [Tooltip("If true, joint rotation matches Final Pose Example Frame rotation; if false, keep rotation after snap euler offset only.")]
    public bool finalPoseUseExampleFrameRotation = true;

    [Header("Post-weld reposition (after bottom dots)")]
    [Tooltip("Bottom weld dots sequence. Once it reports HasCompletedAllWeldSteps, the 180° flip gate is dropped and the joint instead snaps to Second Example Frame on collision.")]
    public SequentialWeldRevealSequence bottomWeldDotsForReposition;

    [Tooltip("Target pose the joint snaps to after the bottom dots are welded (e.g. a second ExampleFrame root). If left null, falls back to WeldbarAssemblyRoot.SecondMergeRecenterAnchor.")]
    public Transform secondExampleFrame;

    [Tooltip("If true, joint rotation matches Second Example Frame rotation on the post-weld snap; if false, keep the joint's current rotation then apply Second Snap Rotation Euler Offset.")]
    public bool secondFrameUseRotation = true;

    [Tooltip("World-space position nudge applied after snapping to Example Frame 2 / Second Example Frame.")]
    public Vector3 secondSnapPositionOffset = Vector3.zero;

    [Tooltip("Extra Euler multiplied onto the second-frame rotation after the post-weld snap. Use this to manually twist the combined frame on the second snap.")]
    public Vector3 secondSnapRotationEulerOffset = Vector3.zero;

    [Tooltip("If true, the post-weld snap fires on ANY collision. If false (default), it only fires when colliding with the Second Example Frame's own collider(s).")]
    public bool secondFrameSnapOnAnyCollision = false;

    [System.Serializable]
    public class SecondFrameReorientStep
    {
        [Tooltip("Label only (shown in the Inspector).")]
        public string label = "After Corner A";

        [Tooltip("Unused — kept for old scenes. Snap pose comes from Snap When Euler Is Any Of (whichever entry matched).")]
        [HideInInspector]
        [FormerlySerializedAs("snapEuler")]
        public Vector3 snapToEuler;

        [Tooltip("Gate AND snap pose: when the held joint is within tolerance of ANY of these eulers, it snaps to THAT matched euler (ExampleFrame2 position + this rotation). Quaternion compare so 0≈360.")]
        [FormerlySerializedAs("acceptAnyOf")]
        public Vector3[] snapWhenEulerIsAnyOf;
    }

    [Header("Second-frame reorient (between corner welds)")]
    [Tooltip("After each corner weld (except the last): glow → grab → rotate. When orientation matches ANY entry in that step's Snap When Euler Is Any Of, snap to ExampleFrame2 using that same matched euler.")]
    public bool enableSecondFrameReorientSnap = true;

    [Tooltip("Corner WeldLinesRevealOnSnap that drives the between-corner reorient cycle (e.g. CornerWelds).")]
    public WeldLinesRevealOnSnap cornerWeldsForReorient;

    [Tooltip("ONE ENTRY PER ROTATION. Element 0 = after Corner A, 1 = after Corner B, 2 = after Corner C. Only fill Snap When Euler Is Any Of — the matched entry is both the gate and the snap pose.")]
    public SecondFrameReorientStep[] secondFrameReorientSteps = new SecondFrameReorientStep[]
    {
        new SecondFrameReorientStep
        {
            label = "After Corner A",
            snapWhenEulerIsAnyOf = new Vector3[]
            {
                new Vector3(0f, 180f, 270f),
                new Vector3(0f, 0f, 90f),
            }
        },
        new SecondFrameReorientStep
        {
            label = "After Corner B",
            snapWhenEulerIsAnyOf = new Vector3[]
            {
                new Vector3(0f, 180f, 90f),
                new Vector3(270f, 180f, 270f),
            }
        },
        new SecondFrameReorientStep
        {
            label = "After Corner C",
            snapWhenEulerIsAnyOf = new Vector3[]
            {
                new Vector3(0f, 180f, 90f),
                new Vector3(270f, 180f, 270f),
            }
        },
    };

    // Legacy fields kept only for deserialization; never used to override Second Frame Reorient Steps.
    [HideInInspector] public Vector3 secondFrameReorientTargetEuler;
    [HideInInspector] public Vector3[] secondFrameReorientTargetEulers;
    [HideInInspector] public Vector3[] secondFrameReorientAcceptAnyOf;

    [Tooltip("How close the held orientation must be to an accepted euler (degrees). Whole-orientation compare (0≈360).")]
    public float secondFrameReorientEulerToleranceDegrees = 25f;

    [Tooltip("World-space position nudge applied on each reorient snap to ExampleFrame2.")]
    public Vector3 secondFrameReorientPositionOffset = Vector3.zero;

    [HideInInspector]
    public bool secondFrameReorientUseAbsoluteEuler = true;

    [Header("Grab-me cue (reorient / ExampleFrame1 return)")]
    [Tooltip("Pulse material on the merged joint after a corner is welded, until that between-corner reorient snap completes. Also pulses after all corners until snapped onto ExampleFrame1. Stops while held; resumes if dropped before re-snapping.")]
    public Material grabMeMaterial;

    [Tooltip("Renderers to tint. If empty, uses all child renderers of this joint.")]
    public Renderer[] grabMeRenderers;

    public float grabMePulseSpeed = 1.5f;
    public float grabMePulseMinBrightness = 0.6f;
    public float grabMePulseMaxBrightness = 1.8f;
    public bool grabMePulseEmission = true;

    [Header("Return to ExampleFrame1 (after all corner welds)")]
    [Tooltip("After the last corner weld: glow the combined frame, show ExampleFrame1 while held (via grab-reveal), and snap on contact like the beginning pose.")]
    public bool enableReturnToExampleFrame1AfterCorners = true;

    [Tooltip("ExampleFrame1 root to snap onto. If empty, uses Final Pose Example Frame (same target as the initial flip snap).")]
    public Transform exampleFrame1ReturnTarget;

    [Tooltip("World position = ExampleFrame1.position + this offset (defaults match the beginning snapPositionOffset if you leave them equal in the Inspector).")]
    public Vector3 exampleFrame1SnapPositionOffset;

    [Tooltip("If true, match ExampleFrame1.rotation (then apply Euler offset). If false, use Euler offset as absolute world rotation.")]
    public bool exampleFrame1UseTargetRotation = true;

    [Tooltip("Extra world euler applied after matching ExampleFrame1 (or absolute if Use Target Rotation is off). Leave zero to match the beginning final-pose style.")]
    public Vector3 exampleFrame1SnapRotationEulerOffset;

    [Tooltip("If true, allow grabbing again and re-snapping to ExampleFrame1 after the first return.")]
    public bool exampleFrame1ReturnSnapRepeatable = false;

    [Header("After ExampleFrame1 return — top welds")]
    [Tooltip("Top weld lines (e.g. TopWelds) that unlock after the finished frame snaps back onto ExampleFrame1. Wire the same group with Flip Snap Gate = Example Frame1 Return.")]
    public WeldLinesRevealOnSnap topWeldsAfterExampleFrame1Return;

    [Header("After top welds — 180° flip (like beginning)")]
    [Tooltip("After all TopWelds are done: glow the joint, grab, rotate ~180° on X or Z, contact example weld bar → snap like the first flip. Then bottom welds unlock.")]
    public bool enablePostTopWeldFlipSnap = true;

    [Tooltip("Bottom weld lines (e.g. BottomWelds) that unlock after the post-top 180° flip. Wire with Flip Snap Gate = Post Top Weld Flip.")]
    public WeldLinesRevealOnSnap bottomWeldsAfterPostTopWeldFlip;

    [Tooltip("If true, allow another 180° flip snap after the first post-top flip.")]
    public bool postTopWeldFlipRepeatable = false;

    [Header("ExampleFrame3 (after ref piece second snap)")]
    [Tooltip("When the assigned ref piece finishes snapping onto its SECOND guide, switch the active example frame to Third Example Frame (grab-reveal shows that set while the joint is held), then glow/grab/snap RealFrame onto that frame.")]
    public bool enableThirdExampleFrameAfterRefSecondSnap = true;

    [Tooltip("Reference piece that must report HasSnappedOnSecondGuide (e.g. Reference2 after BottomWelds unlock its second exrefpiece).")]
    public refpiece requireRefPieceSecondSnap;

    [Tooltip("ExampleFrame3 root transform used as the snap target once the ref-piece clause passes.")]
    public Transform thirdExampleFrame;

    [Tooltip("World position = ExampleFrame3.position + this offset.")]
    public Vector3 thirdExampleFrameSnapPositionOffset;

    [Tooltip("If true, match ExampleFrame3.rotation (then apply Euler offset). If false, use Euler offset as absolute world rotation.")]
    public bool thirdExampleFrameUseTargetRotation = true;

    [Tooltip("Extra world euler applied after matching ExampleFrame3 (or absolute if Use Target Rotation is off).")]
    public Vector3 thirdExampleFrameSnapRotationEulerOffset;

    [Tooltip("If true, allow grabbing again and re-snapping to ExampleFrame3 after the first snap.")]
    public bool thirdExampleFrameSnapRepeatable = false;

    [Tooltip("Inner corner weld lines (e.g. InnerCornerWelds) that unlock after RealFrame snaps onto ExampleFrame3. Wire with Flip Snap Gate = Example Frame3 Snap.")]
    public WeldLinesRevealOnSnap innerCornerWeldsAfterExampleFrame3Snap;

    [Header("Third-frame reorient (between inner corner welds)")]
    [Tooltip("After each InnerCorner weld (except the last): glow → grab → rotate. When orientation matches ANY entry in that step's Snap When Euler Is Any Of, snap to ExampleFrame3 using that same matched euler.")]
    public bool enableThirdFrameReorientSnap = true;

    [Tooltip("InnerCorner WeldLinesRevealOnSnap that drives the between-weld reorient cycle. If empty, uses Inner Corner Welds After Example Frame3 Snap.")]
    public WeldLinesRevealOnSnap innerCornerWeldsForReorient;

    [Tooltip("ONE ENTRY PER ROTATION. Element 0 = after InnerCorner A, 1 = after B, 2 = after C. Only fill Snap When Euler Is Any Of — matched entry is both the gate and the snap pose.")]
    public SecondFrameReorientStep[] thirdFrameReorientSteps = new SecondFrameReorientStep[]
    {
        new SecondFrameReorientStep
        {
            label = "After Inner Corner A",
            snapWhenEulerIsAnyOf = new Vector3[]
            {
                new Vector3(0f, 270f, 90f),
            }
        },
        new SecondFrameReorientStep
        {
            label = "After Inner Corner B",
            snapWhenEulerIsAnyOf = new Vector3[]
            {
                new Vector3(0f, 270f, 180f),
            }
        },
        new SecondFrameReorientStep
        {
            label = "After Inner Corner C",
            snapWhenEulerIsAnyOf = new Vector3[]
            {
                new Vector3(0f, 270f, 270f),
            }
        },
    };

    [Tooltip("How close the held orientation must be to an accepted euler (degrees) for third-frame reorient.")]
    public float thirdFrameReorientEulerToleranceDegrees = 25f;

    [Tooltip("World-space position nudge applied on each reorient snap to ExampleFrame3.")]
    public Vector3 thirdFrameReorientPositionOffset = Vector3.zero;

    [Header("Debug (TMP)")]
    [Tooltip("Optional TextMesh Pro label showing euler used by flip gate plus short gate status.")]
    public TMP_Text eulerAngleDebugText;

    [Tooltip("When true (default), euler TMP only refreshes while the assembly has merged.")]
    public bool updateEulerTMPOnlyAfterMerge = true;

    [Tooltip("Decimal places for euler X/Y/Z.")]
    [Range(0, 4)]
    public int eulerDecimalPlaces = 1;

    Rigidbody _rb;
    Grabbable _grab;

    /// <summary>Blocks another flip snap until the player grabs the joint again (weldbar <c>snapped</c>-style seal).</summary>
    bool _flipSnapSealedUntilGrab;

    /// <summary>Weldbar-style delay before flip snap can evaluate again after a grab clears the seal.</summary>
    float _nextFlipSnapEligibleTime;

    bool _flipGrabCooldownActive;
    float _flipGrabCooldownEndsAt;
    bool _kinematicLockedByFlipSnap;
    bool _hasEverCompletedFlipSnap;
    bool _hasRepositionedToSecondFrame;
    /// <summary>
    /// Highest corner-weld count for which at least one ExampleFrame2 reorient snap has succeeded.
    /// Lets the next corner unlock, while still allowing more reorient snaps until that next corner is welded.
    /// </summary>
    int _reorientSatisfiedUpToWeldedCount;
    /// <summary>
    /// Highest inner-corner weld count for which at least one ExampleFrame3 reorient snap has succeeded.
    /// </summary>
    int _thirdFrameReorientSatisfiedUpToWeldedCount;
    bool _hasReturnedToExampleFrame1;
    bool _hasCompletedPostTopWeldFlip;
    bool _hasSnappedToThirdExampleFrame;
    bool _wasHeldLastFrame;

    /// <summary>ExampleFrame2 / ExampleFrame3 root currently using IgnoreCollision pass-through.</summary>
    Transform _exampleFrameCollisionsIgnoredWith;
    bool _exampleFrameCollisionsIgnored;

    // Grab-me cue for the reorient phase.
    bool _grabMeCueEnabled;
    bool _grabMeActive;
    Renderer[] _grabMeResolvedRenderers;
    Material[] _grabMeOriginals;
    Material[] _grabMeInstances;
    string _grabMeColorProp;
    bool _grabMeHasColorProp;
    bool _grabMeHasEmission;
    Color _grabMeBaseColor = Color.white;
    Color _grabMeBaseEmission = Color.black;

    /// <summary>
    /// True once the bottom weld dots are done and a second example frame is assigned. In this mode the 180° flip gate
    /// is bypassed and the joint snaps to <see cref="secondExampleFrame"/> on collision instead of the merge anchor.
    /// </summary>
    bool PostWeldRepositionActive =>
        bottomWeldDotsForReposition != null &&
        bottomWeldDotsForReposition.HasCompletedAllWeldSteps &&
        ResolvedSecondFrame != null;

    /// <summary>
    /// After Corner A/B/C are welded (not the last): allow glow + grab + rotate + snap to ExampleFrame2
    /// repeatedly until the next corner is welded. One successful snap unlocks the next corner ghost;
    /// further snaps stay allowed if the user needs to re-seat the frame.
    /// </summary>
    bool SecondFrameReorientActive
    {
        get
        {
            if (!enableSecondFrameReorientSnap || !_hasRepositionedToSecondFrame || ResolvedSecondFrame == null)
                return false;

            if (cornerWeldsForReorient == null)
                return false;

            int welded = cornerWeldsForReorient.WeldedCount;
            int total = cornerWeldsForReorient.ValidLineCount;
            if (total <= 0)
                return false;

            // Between corners: at least one corner done, and not finished with the last corner yet.
            return welded >= 1 && welded < total;
        }
    }

    /// <summary>ExampleFrame1 target: explicit return target, else the first-flip Final Pose Example Frame.</summary>
    Transform ResolvedExampleFrame1 =>
        exampleFrame1ReturnTarget != null ? exampleFrame1ReturnTarget : finalPoseExampleFrame;

    /// <summary>After every corner weld: glow + grab + snap the finished frame onto ExampleFrame1.</summary>
    bool ExampleFrame1ReturnActive
    {
        get
        {
            if (!enableReturnToExampleFrame1AfterCorners || ResolvedExampleFrame1 == null)
                return false;
            if (cornerWeldsForReorient == null || !cornerWeldsForReorient.HasWeldedAllLines)
                return false;

            // Later phases fully supersede the ExampleFrame1 return snap.
            if (_hasCompletedPostTopWeldFlip || _hasSnappedToThirdExampleFrame)
                return false;
            if (topWeldsAfterExampleFrame1Return != null && topWeldsAfterExampleFrame1Return.HasWeldedAllLines)
                return false;
            if (bottomWeldsAfterPostTopWeldFlip != null &&
                (bottomWeldsAfterPostTopWeldFlip.HasRevealed ||
                 bottomWeldsAfterPostTopWeldFlip.HasWeldedAllLines))
                return false;
            if (ShouldUseThirdExampleFrame)
                return false;

            // Once TopWelds unlock, stop re-snapping to ExampleFrame1 — that phase is over.
            if (_hasReturnedToExampleFrame1 &&
                topWeldsAfterExampleFrame1Return != null &&
                topWeldsAfterExampleFrame1Return.HasRevealed)
                return false;

            if (_hasReturnedToExampleFrame1 && !exampleFrame1ReturnSnapRepeatable)
                return false;

            return true;
        }
    }

    /// <summary>
    /// After all TopWelds: glow + grab + rotate ~180° + snap like the beginning (merge anchor + euler offset).
    /// </summary>
    bool PostTopWeldFlipActive
    {
        get
        {
            if (!enablePostTopWeldFlipSnap)
                return false;
            if (topWeldsAfterExampleFrame1Return == null || !topWeldsAfterExampleFrame1Return.HasWeldedAllLines)
                return false;
            if (!_hasReturnedToExampleFrame1)
                return false;
            if (_hasCompletedPostTopWeldFlip && !postTopWeldFlipRepeatable)
                return false;
            return true;
        }
    }

    /// <summary>
    /// After Reference2 (or assigned ref) snaps onto its second guide: glow + grab + snap RealFrame onto ExampleFrame3
    /// (initial seat only — between InnerCorner welds use ThirdFrameReorientActive).
    /// </summary>
    bool ExampleFrame3SnapActive
    {
        get
        {
            if (!enableThirdExampleFrameAfterRefSecondSnap || thirdExampleFrame == null)
                return false;
            if (requireRefPieceSecondSnap == null || !requireRefPieceSecondSnap.HasSnappedOnSecondGuide)
                return false;
            // Initial seat only; between-weld reseats are handled by ThirdFrameReorientActive.
            if (_hasSnappedToThirdExampleFrame)
                return false;
            return true;
        }
    }

    WeldLinesRevealOnSnap ResolvedInnerCornerWeldsForReorient =>
        innerCornerWeldsForReorient != null
            ? innerCornerWeldsForReorient
            : innerCornerWeldsAfterExampleFrame3Snap;

    /// <summary>
    /// After InnerCorner A/B/C are welded (not the last): glow + grab + rotate + snap to ExampleFrame3
    /// when orientation matches that step's Snap When Euler Is Any Of (same pattern as ExampleFrame2 reorient).
    /// </summary>
    bool ThirdFrameReorientActive
    {
        get
        {
            if (!enableThirdFrameReorientSnap || !_hasSnappedToThirdExampleFrame || thirdExampleFrame == null)
                return false;

            WeldLinesRevealOnSnap inner = ResolvedInnerCornerWeldsForReorient;
            if (inner == null)
                return false;

            int welded = inner.WeldedCount;
            int total = inner.ValidLineCount;
            if (total <= 0)
                return false;

            return welded >= 1 && welded < total;
        }
    }

    int CurrentReorientStepIndex
    {
        get
        {
            if (cornerWeldsForReorient == null)
                return 0;
            return Mathf.Max(0, cornerWeldsForReorient.WeldedCount - 1);
        }
    }

    int CurrentThirdFrameReorientStepIndex
    {
        get
        {
            WeldLinesRevealOnSnap inner = ResolvedInnerCornerWeldsForReorient;
            if (inner == null)
                return 0;
            return Mathf.Max(0, inner.WeldedCount - 1);
        }
    }

    SecondFrameReorientStep CurrentReorientStep
    {
        get
        {
            int i = CurrentReorientStepIndex;
            if (secondFrameReorientSteps != null && i >= 0 && i < secondFrameReorientSteps.Length)
                return secondFrameReorientSteps[i];
            return null;
        }
    }

    SecondFrameReorientStep CurrentThirdFrameReorientStep
    {
        get
        {
            int i = CurrentThirdFrameReorientStepIndex;
            if (thirdFrameReorientSteps != null && i >= 0 && i < thirdFrameReorientSteps.Length)
                return thirdFrameReorientSteps[i];
            return null;
        }
    }

    Vector3 CurrentReorientTargetEuler
    {
        get
        {
            // Debug helper: show first listed euler for this step (actual snap uses the matched entry).
            Vector3[] list = GetAcceptableReorientEulers();
            return list.Length > 0 ? list[0] : Vector3.zero;
        }
    }

    /// <summary>Second-frame target: explicit <see cref="secondExampleFrame"/>, else the assembly root's second recenter anchor.</summary>
    Transform ResolvedSecondFrame =>
        secondExampleFrame != null
            ? secondExampleFrame
            : (assemblyRoot != null ? assemblyRoot.SecondMergeRecenterAnchor : null);

    void Awake()
    {
        if (assemblyRoot == null)
            assemblyRoot = GetComponent<WeldbarAssemblyRoot>();
    }

    void OnDisable()
    {
        if (_flipGrabCooldownActive && _grab != null)
        {
            _grab.enabled = true;
            _flipGrabCooldownActive = false;
        }

        SetExampleFrameCollisionsIgnored(false);
    }

    void OnDestroy()
    {
        SetExampleFrameCollisionsIgnored(false);
    }

    void EnsureRbAndGrabAddedAtMergeRuntime()
    {
        if (_rb == null)
            _rb = GetComponent<Rigidbody>();
        if (_grab == null)
            _grab = GetComponent<Grabbable>();
    }

    void Update()
    {
        ManagePickupCooldownGrabAndSeal();
        UpdateGrabMeCue();
    }

    void LateUpdate()
    {
        RefreshEulerTMP();
    }

    void ManagePickupCooldownGrabAndSeal()
    {
        EnsureRbAndGrabAddedAtMergeRuntime();

        if (_grab == null || assemblyRoot == null || !assemblyRoot.HasMergedAssembly)
            return;

        if (_flipGrabCooldownActive && Time.time >= _flipGrabCooldownEndsAt)
        {
            _grab.enabled = true;
            _flipGrabCooldownActive = false;
        }

        bool held = _grab.BeingHeld;

        if (_flipSnapSealedUntilGrab && held)
        {
            if (_kinematicLockedByFlipSnap && _rb != null && freezeRigidbodyAfterSnap)
            {
                _rb.velocity = Vector3.zero;
                _rb.angularVelocity = Vector3.zero;
                _rb.isKinematic = false;
                _rb.constraints = RigidbodyConstraints.None;
                _kinematicLockedByFlipSnap = false;
                _grab.UpdateRigidbodyReference();
            }

            if (!_wasHeldLastFrame)
            {
                _flipSnapSealedUntilGrab = false;
                _nextFlipSnapEligibleTime =
                    Time.time + Mathf.Max(0f, snapCooldownAfterJointPickupSeconds);
                // Keep pass-through through the post-pickup cooldown (re-assert while bars reappear on hold).
                if (_exampleFrameCollisionsIgnored)
                    SetExampleFrameCollisionsIgnored(true);
            }
        }

        bool inExampleFrameUnsnapWindow =
            _flipSnapSealedUntilGrab ||
            _flipGrabCooldownActive ||
            Time.time < _nextFlipSnapEligibleTime;

        // Re-assert only while an example-frame ignore is already active (do not revive a stale frame after cooldown).
        if (_exampleFrameCollisionsIgnored &&
            inExampleFrameUnsnapWindow &&
            ignoreCollisionsDuringExampleFrameUnsnapCooldown &&
            _exampleFrameCollisionsIgnoredWith != null)
        {
            SetExampleFrameCollisionsIgnored(true);
        }
        else if (_exampleFrameCollisionsIgnored && !inExampleFrameUnsnapWindow)
        {
            SetExampleFrameCollisionsIgnored(false);
        }

        _wasHeldLastFrame = held;
    }

    void RefreshEulerTMP()
    {
        if (eulerAngleDebugText == null)
            return;
        if (updateEulerTMPOnlyAfterMerge && (assemblyRoot == null || !assemblyRoot.HasMergedAssembly))
        {
            eulerAngleDebugText.text = string.Empty;
            return;
        }

        Vector3 e = worldSpaceEulerCheck ? transform.eulerAngles : transform.localEulerAngles;
        string fmt = eulerDecimalPlaces <= 0 ? "F0" : $"F{eulerDecimalPlaces}";
        string space = worldSpaceEulerCheck ? "World" : "Local";

        if (PostTopWeldFlipActive)
        {
            bool postTopFlipEulerOk = FlipEulerPassesForCurrentAngles(e.x, e.z);
            eulerAngleDebugText.text =
                $"{space} euler °\tX:{e.x.ToString(fmt)}  Y:{e.y.ToString(fmt)}  Z:{e.z.ToString(fmt)}\n" +
                $"Post-top flip (like beginning)\n" +
                $"Flip X/Z≈180\t{(postTopFlipEulerOk ? "PASS" : "FAIL")}  tol±{euler180ToleranceDegrees:F0}°";
            return;
        }

        if (ThirdFrameReorientActive)
        {
            bool eulerOk = TryGetMatchedThirdFrameReorientEuler(e, out Vector3 matchedEuler);
            int phase = ResolvedInnerCornerWeldsForReorient != null
                ? ResolvedInnerCornerWeldsForReorient.WeldedCount : 0;
            int stepIdx = CurrentThirdFrameReorientStepIndex;
            string matched = eulerOk
                ? $"{matchedEuler.x:F0},{matchedEuler.y:F0},{matchedEuler.z:F0}"
                : "-";
            eulerAngleDebugText.text =
                $"{space} euler °\tX:{e.x.ToString(fmt)}  Y:{e.y.ToString(fmt)}  Z:{e.z.ToString(fmt)}\n" +
                $"3rd-frame reorient step {stepIdx} (after inner #{phase}) match→{matched}\n" +
                $"Orient gate\t{(eulerOk ? "PASS" : "FAIL")}  tol±{thirdFrameReorientEulerToleranceDegrees:F0}°";
            return;
        }

        if (ExampleFrame3SnapActive)
        {
            eulerAngleDebugText.text =
                $"{space} euler °\tX:{e.x.ToString(fmt)}  Y:{e.y.ToString(fmt)}  Z:{e.z.ToString(fmt)}\n" +
                $"Ref second snap done — glow + snap to ExampleFrame3 on contact\n" +
                $"Snapped\t{(_hasSnappedToThirdExampleFrame ? "YES" : "waiting")}";
            return;
        }

        if (ExampleFrame1ReturnActive)
        {
            eulerAngleDebugText.text =
                $"{space} euler °\tX:{e.x.ToString(fmt)}  Y:{e.y.ToString(fmt)}  Z:{e.z.ToString(fmt)}\n" +
                $"All corners done — glow + snap to ExampleFrame1 on contact\n" +
                $"Returned\t{(_hasReturnedToExampleFrame1 ? "YES" : "waiting")}";
            return;
        }

        if (PostWeldRepositionActive)
        {
            if (SecondFrameReorientActive)
            {
                bool eulerOk = PassesReorientEulerGate(e);
                int phase = cornerWeldsForReorient != null ? cornerWeldsForReorient.WeldedCount : 0;
                int stepIdx = CurrentReorientStepIndex;
                string matched = "-";
                if (TryGetMatchedReorientEuler(out Vector3 matchedEuler))
                    matched = $"{matchedEuler.x:F0},{matchedEuler.y:F0},{matchedEuler.z:F0}";
                eulerAngleDebugText.text =
                    $"{space} euler °\tX:{e.x.ToString(fmt)}  Y:{e.y.ToString(fmt)}  Z:{e.z.ToString(fmt)}\n" +
                    $"Reorient step {stepIdx} (after corner #{phase}) match→{matched}\n" +
                    $"Orient gate\t{(eulerOk ? "PASS" : "FAIL")}  tol±{secondFrameReorientEulerToleranceDegrees:F0}° (snap to matched entry)";
                return;
            }

            string trigger = secondFrameSnapOnAnyCollision ? "any collision" : "2nd frame contact";
            eulerAngleDebugText.text =
                $"{space} euler °\tX:{e.x.ToString(fmt)}  Y:{e.y.ToString(fmt)}  Z:{e.z.ToString(fmt)}\n" +
                $"Post-weld: flip gate OFF\nSnap to 2nd example frame on {trigger}";
            return;
        }

        bool flipEulerOk = FlipEulerPassesForCurrentAngles(e.x, e.z);
        string mode = requireContactWithExampleWeldbar
            ? "| snap on col/trigger+weld"
            : "| snap polled (no weld contact)";
        eulerAngleDebugText.text =
            $"{space} euler °\tX:{e.x.ToString(fmt)}  Y:{e.y.ToString(fmt)}  Z:{e.z.ToString(fmt)}\n" +
            $"Flip X/Z≈180\t{(flipEulerOk ? "PASS" : "FAIL")}  tol±{euler180ToleranceDegrees:F0}\n{mode}";
    }

    /// <summary>Same X/Z‑only check as <see cref="PassesFlipEulerGate"/> (uses configured tolerance).</summary>
    bool FlipEulerPassesForCurrentAngles(float eulerXDeg, float eulerZDeg)
    {
        bool xOk = EulerComponentNear180(eulerXDeg);
        bool zOk = EulerComponentNear180(eulerZDeg);
        return requireBothXAndZNear180 ? (xOk && zOk) : (xOk || zOk);
    }

    void FixedUpdate()
    {
        // ExampleFrame3 initial seat + between-inner-corner reorient: collision-driven.
        if (ExampleFrame3SnapActive || ThirdFrameReorientActive)
            return;

        // Post-top 180° flip: collision-driven when contact required; otherwise poll.
        if (PostTopWeldFlipActive)
        {
            if (!requireContactWithExampleWeldbar)
                AttemptFlipSnap();
            return;
        }

        // Post-weld reposition is strictly collision-driven; never poll it.
        if (PostWeldRepositionActive)
            return;

        if (requireContactWithExampleWeldbar)
            return;

        AttemptFlipSnap();
    }

    void AttemptFlipSnap()
    {
        EnsureRbAndGrabAddedAtMergeRuntime();

        if (_flipSnapSealedUntilGrab)
            return;

        if (Time.time < _nextFlipSnapEligibleTime)
            return;

        if (assemblyRoot == null || !assemblyRoot.HasMergedAssembly)
            return;

        if (WeldEightStepSequenceIncomplete())
            return;

        // Reveal-after-grab only gates the classic 180° flip — not ExampleFrame2 / ExampleFrame3 / reorients.
        if (!PostWeldRepositionActive && !PostTopWeldFlipActive &&
            !ExampleFrame3SnapActive && !ThirdFrameReorientActive &&
            revealAfterGrabDriver != null &&
            !revealAfterGrabDriver.HasRevealedExampleBars)
            return;

        if (requireMergedJointHeldForFlipSnap && (_grab == null || !_grab.BeingHeld))
            return;

        // After TopWelds: 180° flip snap like the beginning, then bottom welds unlock.
        if (PostTopWeldFlipActive)
        {
            Transform postTopAnchor = assemblyRoot.MergeRecenterAnchor;
            if (requireMergeAnchorAssigned && postTopAnchor == null)
                return;
            if (!PassesFlipEulerGate())
                return;
            ApplySnap(postTopAnchor);
            _hasCompletedPostTopWeldFlip = true;
            return;
        }

        // Between InnerCorner welds: euler-gated re-snap onto ExampleFrame3.
        if (ThirdFrameReorientActive)
        {
            if (!TryGetMatchedThirdFrameReorientEuler(out _))
                return;
            ApplyThirdFrameReorientSnap();
            return;
        }

        // After ref piece second-guide snap: seat RealFrame onto ExampleFrame3.
        if (ExampleFrame3SnapActive)
        {
            ApplySnapToThirdExampleFrame();
            return;
        }

        // After all corner welds: snap finished frame back onto ExampleFrame1 (beginning pose).
        if (ExampleFrame1ReturnActive)
        {
            ApplySnapToExampleFrame1();
            return;
        }

        // After the bottom dots are welded: first snap to ExampleFrame2 (no euler gate),
        // then optional reorient snap that requires the target euler + contact.
        if (PostWeldRepositionActive)
        {
            if (!_hasRepositionedToSecondFrame)
            {
                ApplySnapToSecondFrame();
                return;
            }

            if (SecondFrameReorientActive)
            {
                if (!TryGetMatchedReorientEuler(out _))
                    return;
                ApplySecondFrameReorientSnap();
            }
            return;
        }

        Transform anchor = assemblyRoot.MergeRecenterAnchor;
        if (requireMergeAnchorAssigned && anchor == null)
            return;

        if (!PassesFlipEulerGate())
            return;

        ApplySnap(anchor);
    }

    void TryFlipSnapAfterContact(Collider other)
    {
        if (other == null)
            return;

        // After TopWelds: beginning-style 180° flip on exampleweldbar contact.
        if (PostTopWeldFlipActive)
        {
            if (requireContactWithExampleWeldbar &&
                other.GetComponentInParent<exampleweldbar>() == null)
                return;
            AttemptFlipSnap();
            return;
        }

        // Between InnerCorner welds / initial ExampleFrame3 seat: contact with ExampleFrame3 hierarchy.
        if (ThirdFrameReorientActive || ExampleFrame3SnapActive)
        {
            if (!IsColliderInHierarchy(other, thirdExampleFrame))
                return;
            AttemptFlipSnap();
            return;
        }

        // Finished corners: snap on contact with ExampleFrame1 (beginning pose).
        if (ExampleFrame1ReturnActive)
        {
            if (!IsColliderInHierarchy(other, ResolvedExampleFrame1))
                return;
            AttemptFlipSnap();
            return;
        }

        // Post-weld / reorient: snap on collision with the second frame (or any, if configured).
        if (PostWeldRepositionActive)
        {
            bool contactOk = secondFrameSnapOnAnyCollision || IsColliderInHierarchy(other, ResolvedSecondFrame);
            if (!contactOk)
                return;

            // First placement or reorient — AttemptFlipSnap branches on state.
            if (!_hasRepositionedToSecondFrame || SecondFrameReorientActive)
                AttemptFlipSnap();
            return;
        }

        if (!requireContactWithExampleWeldbar)
            return;

        if (other.GetComponentInParent<exampleweldbar>() == null)
            return;

        AttemptFlipSnap();
    }

    static bool IsColliderInHierarchy(Collider other, Transform root)
    {
        if (other == null || root == null)
            return false;

        Transform t = other.transform;
        return t == root || t.IsChildOf(root);
    }
    bool WeldEightStepSequenceIncomplete()
    {
        return assemblyRoot.mergeAfterEightSpotWelds != null &&
               !assemblyRoot.mergeAfterEightSpotWelds.HasCompletedAllWeldSteps;
    }

    bool PassesFlipEulerGate()
    {
        Vector3 e = worldSpaceEulerCheck ? transform.eulerAngles : transform.localEulerAngles;
        return FlipEulerPassesForCurrentAngles(e.x, e.z);
    }

    bool PassesReorientEulerGate()
    {
        return TryGetMatchedReorientEuler(out _);
    }

    bool PassesReorientEulerGate(Vector3 currentEuler)
    {
        return TryGetMatchedReorientEuler(currentEuler, out _);
    }

    /// <summary>
    /// Picks the closest entry in the current step's Snap When Euler Is Any Of that is within tolerance.
    /// That entry is both the gate pass and the snap pose.
    /// </summary>
    bool TryGetMatchedReorientEuler(out Vector3 matchedEuler)
    {
        Vector3 e = worldSpaceEulerCheck ? transform.eulerAngles : transform.localEulerAngles;
        return TryGetMatchedReorientEuler(e, out matchedEuler);
    }

    bool TryGetMatchedReorientEuler(Vector3 currentEuler, out Vector3 matchedEuler)
    {
        matchedEuler = Vector3.zero;
        Vector3[] accept = GetAcceptableReorientEulers();
        if (accept == null || accept.Length == 0)
            return false;

        float tol = Mathf.Max(0f, secondFrameReorientEulerToleranceDegrees);
        Quaternion current = worldSpaceEulerCheck ? transform.rotation : transform.localRotation;

        float bestAngle = float.MaxValue;
        int bestIndex = -1;

        for (int i = 0; i < accept.Length; i++)
        {
            float ang = Quaternion.Angle(current, Quaternion.Euler(accept[i]));
            if (ang <= tol && ang < bestAngle)
            {
                bestAngle = ang;
                bestIndex = i;
            }
        }

        if (bestIndex < 0)
        {
            // Fallback: per-axis DeltaAngle (handles some display-spelling mismatches).
            for (int i = 0; i < accept.Length; i++)
            {
                Vector3 t = accept[i];
                if (Mathf.Abs(Mathf.DeltaAngle(currentEuler.x, t.x)) <= tol
                    && Mathf.Abs(Mathf.DeltaAngle(currentEuler.y, t.y)) <= tol
                    && Mathf.Abs(Mathf.DeltaAngle(currentEuler.z, t.z)) <= tol)
                {
                    matchedEuler = t;
                    return true;
                }
            }
            return false;
        }

        matchedEuler = accept[bestIndex];
        return true;
    }

    Vector3[] GetAcceptableReorientEulers()
    {
        SecondFrameReorientStep step = CurrentReorientStep;
        if (step == null || step.snapWhenEulerIsAnyOf == null || step.snapWhenEulerIsAnyOf.Length == 0)
            return System.Array.Empty<Vector3>();

        return step.snapWhenEulerIsAnyOf;
    }

    bool TryGetMatchedThirdFrameReorientEuler(out Vector3 matchedEuler)
    {
        Vector3 e = worldSpaceEulerCheck ? transform.eulerAngles : transform.localEulerAngles;
        return TryGetMatchedThirdFrameReorientEuler(e, out matchedEuler);
    }

    bool TryGetMatchedThirdFrameReorientEuler(Vector3 currentEuler, out Vector3 matchedEuler)
    {
        matchedEuler = Vector3.zero;
        Vector3[] accept = GetAcceptableThirdFrameReorientEulers();
        if (accept == null || accept.Length == 0)
            return false;

        float tol = Mathf.Max(0f, thirdFrameReorientEulerToleranceDegrees);
        Quaternion current = worldSpaceEulerCheck ? transform.rotation : transform.localRotation;

        float bestAngle = float.MaxValue;
        int bestIndex = -1;

        for (int i = 0; i < accept.Length; i++)
        {
            float ang = Quaternion.Angle(current, Quaternion.Euler(accept[i]));
            if (ang <= tol && ang < bestAngle)
            {
                bestAngle = ang;
                bestIndex = i;
            }
        }

        if (bestIndex < 0)
        {
            for (int i = 0; i < accept.Length; i++)
            {
                Vector3 t = accept[i];
                if (Mathf.Abs(Mathf.DeltaAngle(currentEuler.x, t.x)) <= tol
                    && Mathf.Abs(Mathf.DeltaAngle(currentEuler.y, t.y)) <= tol
                    && Mathf.Abs(Mathf.DeltaAngle(currentEuler.z, t.z)) <= tol)
                {
                    matchedEuler = t;
                    return true;
                }
            }
            return false;
        }

        matchedEuler = accept[bestIndex];
        return true;
    }

    Vector3[] GetAcceptableThirdFrameReorientEulers()
    {
        SecondFrameReorientStep step = CurrentThirdFrameReorientStep;
        if (step == null || step.snapWhenEulerIsAnyOf == null || step.snapWhenEulerIsAnyOf.Length == 0)
            return System.Array.Empty<Vector3>();

        return step.snapWhenEulerIsAnyOf;
    }

    bool EulerComponentNear180(float eulerDeg)
    {
        float n = eulerDeg % 360f;
        if (n > 180f)
            n -= 360f;
        if (n <= -180f)
            n += 360f;

        return Mathf.Abs(Mathf.Abs(n) - 180f) <= euler180ToleranceDegrees;
    }

    void ApplySnap(Transform anchor)
    {
        _flipSnapSealedUntilGrab = true;
        _hasEverCompletedFlipSnap = true;

        EnsureRbAndGrabAddedAtMergeRuntime();

        if (dropGrabBeforeSnapTeleport && _grab != null && _grab.BeingHeld)
            _grab.DropItem(true, true);

        Quaternion snappedRot = anchor.rotation * Quaternion.Euler(snapRotationEulerOffset);
        transform.SetPositionAndRotation(anchor.position + snapPositionOffset, snappedRot);

        ApplyFinalPoseFromExampleFrameIfAssigned();

        ApplyPostSnapPhysicsAndCooldown();

        Physics.SyncTransforms();
    }

    /// <summary>
    /// Post-weld snap: once the bottom dots are done, take the second example frame's pose
    /// plus <see cref="secondSnapPositionOffset"/> / <see cref="secondSnapRotationEulerOffset"/>.
    /// Arms the grab-me glow for the optional reorient phase.
    /// </summary>
    void ApplySnapToSecondFrame()
    {
        Transform target = ResolvedSecondFrame;
        if (target == null)
            return;

        _flipSnapSealedUntilGrab = true;
        _hasRepositionedToSecondFrame = true;

        EnsureRbAndGrabAddedAtMergeRuntime();

        if (dropGrabBeforeSnapTeleport && _grab != null && _grab.BeingHeld)
            _grab.DropItem(true, true);

        Quaternion baseRot = secondFrameUseRotation ? target.rotation : transform.rotation;
        Quaternion rot = baseRot * Quaternion.Euler(secondSnapRotationEulerOffset);
        transform.SetPositionAndRotation(target.position + secondSnapPositionOffset, rot);

        BeginExampleFrameCollisionIgnore(target);
        ApplyPostSnapPhysicsAndCooldown();

        // Glow arms later — after each corner weld (see UpdateGrabMeCue / SecondFrameReorientActive).

        Physics.SyncTransforms();
    }

    /// <summary>
    /// Between-corner ExampleFrame2 snap: when held orientation matches an entry in
    /// Snap When Euler Is Any Of, teleport to ExampleFrame2 and set rotation to THAT matched entry.
    /// </summary>
    void ApplySecondFrameReorientSnap()
    {
        Transform target = ResolvedSecondFrame;
        if (target == null)
            return;

        if (!TryGetMatchedReorientEuler(out Vector3 euler))
            return;

        _flipSnapSealedUntilGrab = true;

        if (cornerWeldsForReorient != null)
        {
            _reorientSatisfiedUpToWeldedCount = Mathf.Max(
                _reorientSatisfiedUpToWeldedCount,
                cornerWeldsForReorient.WeldedCount);
        }

        EnsureRbAndGrabAddedAtMergeRuntime();

        if (dropGrabBeforeSnapTeleport && _grab != null && _grab.BeingHeld)
            _grab.DropItem(true, true);

        Vector3 pos = target.position + secondFrameReorientPositionOffset;
        transform.position = pos;
        transform.rotation = Quaternion.Euler(euler);
        transform.eulerAngles = euler;

        BeginExampleFrameCollisionIgnore(target);
        ApplyPostSnapPhysicsAndCooldown();

        Physics.SyncTransforms();
    }

    /// <summary>
    /// After all corners: teleport the finished joint onto ExampleFrame1, same idea as the beginning final pose.
    /// </summary>
    void ApplySnapToExampleFrame1()
    {
        Transform target = ResolvedExampleFrame1;
        if (target == null)
            return;

        _flipSnapSealedUntilGrab = true;
        _hasReturnedToExampleFrame1 = true;

        EnsureRbAndGrabAddedAtMergeRuntime();

        if (dropGrabBeforeSnapTeleport && _grab != null && _grab.BeingHeld)
            _grab.DropItem(true, true);

        Quaternion rot = exampleFrame1UseTargetRotation
            ? target.rotation * Quaternion.Euler(exampleFrame1SnapRotationEulerOffset)
            : Quaternion.Euler(exampleFrame1SnapRotationEulerOffset);

        // Match the beginning final-pose nudge (snapPositionOffset), plus any extra return nudge.
        Vector3 pos = target.position + snapPositionOffset + exampleFrame1SnapPositionOffset;
        transform.SetPositionAndRotation(pos, rot);

        ApplyPostSnapPhysicsAndCooldown();

        Physics.SyncTransforms();
    }

    /// <summary>
    /// Between InnerCorner welds: when held orientation matches an entry in
    /// Third Frame Reorient Steps → Snap When Euler Is Any Of, teleport to ExampleFrame3
    /// and set rotation to THAT matched entry.
    /// </summary>
    void ApplyThirdFrameReorientSnap()
    {
        Transform target = thirdExampleFrame;
        if (target == null)
            return;

        if (!TryGetMatchedThirdFrameReorientEuler(out Vector3 euler))
            return;

        _flipSnapSealedUntilGrab = true;

        WeldLinesRevealOnSnap inner = ResolvedInnerCornerWeldsForReorient;
        if (inner != null)
        {
            _thirdFrameReorientSatisfiedUpToWeldedCount = Mathf.Max(
                _thirdFrameReorientSatisfiedUpToWeldedCount,
                inner.WeldedCount);
        }

        EnsureRbAndGrabAddedAtMergeRuntime();

        if (dropGrabBeforeSnapTeleport && _grab != null && _grab.BeingHeld)
            _grab.DropItem(true, true);

        Vector3 pos = target.position + thirdFrameReorientPositionOffset;
        transform.position = pos;
        transform.rotation = Quaternion.Euler(euler);
        transform.eulerAngles = euler;

        BeginExampleFrameCollisionIgnore(target);
        ApplyPostSnapPhysicsAndCooldown();

        Physics.SyncTransforms();
    }

    /// <summary>
    /// After ref piece second-guide snap: teleport RealFrame onto ExampleFrame3.
    /// </summary>
    void ApplySnapToThirdExampleFrame()
    {
        Transform target = thirdExampleFrame;
        if (target == null)
            return;

        _flipSnapSealedUntilGrab = true;
        _hasSnappedToThirdExampleFrame = true;

        EnsureRbAndGrabAddedAtMergeRuntime();

        if (dropGrabBeforeSnapTeleport && _grab != null && _grab.BeingHeld)
            _grab.DropItem(true, true);

        Quaternion rot = thirdExampleFrameUseTargetRotation
            ? target.rotation * Quaternion.Euler(thirdExampleFrameSnapRotationEulerOffset)
            : Quaternion.Euler(thirdExampleFrameSnapRotationEulerOffset);

        Vector3 pos = target.position + thirdExampleFrameSnapPositionOffset;
        transform.SetPositionAndRotation(pos, rot);

        BeginExampleFrameCollisionIgnore(target);
        ApplyPostSnapPhysicsAndCooldown();

        Physics.SyncTransforms();
    }

    /// <summary>
    /// Start ignoring collisions with <paramref name="frameRoot"/> (ExampleFrame2/3 + child example weldbars).
    /// Visibility is untouched — <see cref="WeldbMergedGrabRevealExamples"/> still shows ghosts while held.
    /// </summary>
    void BeginExampleFrameCollisionIgnore(Transform frameRoot)
    {
        if (!ignoreCollisionsDuringExampleFrameUnsnapCooldown || frameRoot == null)
            return;

        if (_exampleFrameCollisionsIgnored &&
            _exampleFrameCollisionsIgnoredWith != null &&
            _exampleFrameCollisionsIgnoredWith != frameRoot)
        {
            SetExampleFrameCollisionsIgnored(false);
        }

        _exampleFrameCollisionsIgnoredWith = frameRoot;
        SetExampleFrameCollisionsIgnored(true);
    }

    /// <summary>
    /// Ignore / restore physics between this jointed RealFrame and the active example-frame hierarchy.
    /// Uses force:true so it works even when the global SnapGuideCollisionIgnore master switch is off.
    /// </summary>
    void SetExampleFrameCollisionsIgnored(bool ignore)
    {
        if (_exampleFrameCollisionsIgnoredWith == null)
        {
            _exampleFrameCollisionsIgnored = false;
            return;
        }

        if (!ignore)
        {
            if (!_exampleFrameCollisionsIgnored)
                return;

            SnapGuideCollisionIgnore.SetIgnoredBetween(
                transform, _exampleFrameCollisionsIgnoredWith, false, force: true);
            _exampleFrameCollisionsIgnored = false;
            _exampleFrameCollisionsIgnoredWith = null;
            return;
        }

        if (!ignoreCollisionsDuringExampleFrameUnsnapCooldown)
            return;

        SnapGuideCollisionIgnore.SetIgnoredBetween(
            transform, _exampleFrameCollisionsIgnoredWith, true, force: true);
        _exampleFrameCollisionsIgnored = true;
    }

    Vector3 ResolveReorientEulerForIndex(int index)
    {
        if (secondFrameReorientSteps == null || index < 0 || index >= secondFrameReorientSteps.Length)
            return Vector3.zero;

        SecondFrameReorientStep step = secondFrameReorientSteps[index];
        if (step == null || step.snapWhenEulerIsAnyOf == null || step.snapWhenEulerIsAnyOf.Length == 0)
            return Vector3.zero;

        return step.snapWhenEulerIsAnyOf[0];
    }

    void ApplyPostSnapPhysicsAndCooldown()
    {
        if (_rb != null)
        {
            _rb.velocity = Vector3.zero;
            _rb.angularVelocity = Vector3.zero;
            _rb.detectCollisions = true;
            if (freezeRigidbodyAfterSnap)
            {
                _rb.isKinematic = true;
                _rb.constraints = RigidbodyConstraints.FreezeAll;
                _kinematicLockedByFlipSnap = true;
            }
            else
            {
                _rb.isKinematic = false;
                _kinematicLockedByFlipSnap = false;
            }
        }

        if (_grab != null)
        {
            if (grabCooldownAfterFlipSnapSeconds > 0f)
            {
                _flipGrabCooldownActive = true;
                _flipGrabCooldownEndsAt = Time.time + Mathf.Max(0f, grabCooldownAfterFlipSnapSeconds);
                if (_grab.BeingHeld)
                    _grab.DropItem(true, true);
                _grab.enabled = false;
            }

            _grab.UpdateRigidbodyReference();
        }
    }

    void ApplyFinalPoseFromExampleFrameIfAssigned()
    {
        if (finalPoseExampleFrame == null)
            return;

        Quaternion rot = finalPoseUseExampleFrameRotation
            ? finalPoseExampleFrame.rotation
            : transform.rotation;

        // Keep the first-snap position nudge even when a final example-frame pose is used.
        transform.SetPositionAndRotation(finalPoseExampleFrame.position + snapPositionOffset, rot);
    }

    void OnCollisionEnter(Collision collision)
    {
        if (collision.collider != null)
            TryFlipSnapAfterContact(collision.collider);
    }

    void OnCollisionStay(Collision collision)
    {
        if (collision.collider != null)
            TryFlipSnapAfterContact(collision.collider);
    }

    void OnTriggerEnter(Collider other)
    {
        TryFlipSnapAfterContact(other);
    }

    void OnTriggerStay(Collider other)
    {
        TryFlipSnapAfterContact(other);
    }

    /// <summary>True after at least one successful flip snap (stays true; joint can flip-snap repeatedly).</summary>
    public bool HasCompletedFinalFlipSnap => _hasEverCompletedFlipSnap;

    /// <summary>True after at least one successful post-weld snap to the second example frame.</summary>
    public bool HasRepositionedToSecondFrame => _hasRepositionedToSecondFrame;

    /// <summary>How many between-corner ExampleFrame2 reorient snaps have unlocked the next corner (max welded-count satisfied).</summary>
    public int CompletedSecondFrameReorientCount => _reorientSatisfiedUpToWeldedCount;

    /// <summary>True once every between-corner reorient that the corner weld group requires is done (or corners are finished).</summary>
    public bool HasCompletedSecondFrameReorient
    {
        get
        {
            if (cornerWeldsForReorient == null)
                return _reorientSatisfiedUpToWeldedCount > 0;

            int total = cornerWeldsForReorient.ValidLineCount;
            if (total <= 1)
                return true;

            return _reorientSatisfiedUpToWeldedCount >= total - 1
                   || cornerWeldsForReorient.HasWeldedAllLines;
        }
    }

    /// <summary>True once the bottom dots are welded and the joint is in the "snap to second frame" (no-flip) mode.</summary>
    public bool IsInPostWeldRepositionMode => PostWeldRepositionActive;

    /// <summary>True while waiting for the user to grab/rotate and re-snap to ExampleFrame2 between corner welds.</summary>
    public bool IsInSecondFrameReorientMode => SecondFrameReorientActive;

    /// <summary>
    /// Step completion: after post-top flip, waits for bottom welds when assigned;
    /// else top welds / ExampleFrame1 return / corners / ExampleFrame2 / flip.
    /// </summary>
    public bool IsStepComplete
    {
        get
        {
            if (innerCornerWeldsAfterExampleFrame3Snap != null)
                return _hasSnappedToThirdExampleFrame &&
                       innerCornerWeldsAfterExampleFrame3Snap.HasWeldedAllLines;

            if (bottomWeldsAfterPostTopWeldFlip != null)
                return _hasCompletedPostTopWeldFlip && bottomWeldsAfterPostTopWeldFlip.HasWeldedAllLines;

            if (topWeldsAfterExampleFrame1Return != null)
                return _hasReturnedToExampleFrame1 && topWeldsAfterExampleFrame1Return.HasWeldedAllLines;

            if (enableReturnToExampleFrame1AfterCorners && ResolvedExampleFrame1 != null &&
                cornerWeldsForReorient != null)
                return _hasReturnedToExampleFrame1;

            if (ResolvedSecondFrame != null)
            {
                if (enableSecondFrameReorientSnap && cornerWeldsForReorient != null)
                    return cornerWeldsForReorient.HasWeldedAllLines;
                return _hasRepositionedToSecondFrame;
            }
            return _hasEverCompletedFlipSnap;
        }
    }

    /// <summary>True while waiting for the finished frame to be grabbed and snapped onto ExampleFrame1.</summary>
    public bool IsInExampleFrame1ReturnMode => ExampleFrame1ReturnActive;

    /// <summary>True after at least one successful snap onto ExampleFrame1.</summary>
    public bool HasReturnedToExampleFrame1 => _hasReturnedToExampleFrame1;

    /// <summary>True while waiting for the post-TopWelds 180° flip (glow / grab / rotate / snap).</summary>
    public bool IsInPostTopWeldFlipMode => PostTopWeldFlipActive;

    /// <summary>True after the post-TopWelds 180° flip snap completed (unlocks BottomWelds).</summary>
    public bool HasCompletedPostTopWeldFlip => _hasCompletedPostTopWeldFlip;

    /// <summary>
    /// True once ExampleFrame3 should replace the earlier example-frame set: gate enabled,
    /// ref piece has snapped onto its second guide, and Third Example Frame is assigned.
    /// </summary>
    public bool ShouldUseThirdExampleFrame =>
        enableThirdExampleFrameAfterRefSecondSnap &&
        thirdExampleFrame != null &&
        requireRefPieceSecondSnap != null &&
        requireRefPieceSecondSnap.HasSnappedOnSecondGuide;

    /// <summary>True when the assigned ref piece has completed its second-guide snap (clause only).</summary>
    public bool HasRefPieceCompletedSecondSnap =>
        requireRefPieceSecondSnap != null && requireRefPieceSecondSnap.HasSnappedOnSecondGuide;

    /// <summary>ExampleFrame3 transform when the ref-piece clause has passed; otherwise null.</summary>
    public Transform ResolvedThirdExampleFrame =>
        ShouldUseThirdExampleFrame ? thirdExampleFrame : null;

    /// <summary>True while waiting for RealFrame to be grabbed and snapped onto ExampleFrame3.</summary>
    public bool IsInExampleFrame3SnapMode => ExampleFrame3SnapActive;

    /// <summary>True after at least one successful snap onto ExampleFrame3 (unlocks InnerCornerWelds).</summary>
    public bool HasSnappedToThirdExampleFrame => _hasSnappedToThirdExampleFrame;

    /// <summary>
    /// Debug/test: mark prior return/post-top flip steps done and teleport RealFrame onto ExampleFrame3.
    /// </summary>
    public void ForceSnapToThirdExampleFrameForDebug()
    {
        _hasReturnedToExampleFrame1 = true;
        _hasCompletedPostTopWeldFlip = true;
        ApplySnapToThirdExampleFrame();
    }

    /// <summary>How many between-inner-corner ExampleFrame3 reorient snaps have unlocked the next inner corner.</summary>
    public int CompletedThirdFrameReorientCount => _thirdFrameReorientSatisfiedUpToWeldedCount;

    /// <summary>True while waiting for glow/grab/rotate/re-snap to ExampleFrame3 between InnerCorner welds.</summary>
    public bool IsInThirdFrameReorientMode => ThirdFrameReorientActive;

    void UpdateGrabMeCue()
    {
        bool waitingForGrab =
            SecondFrameReorientActive || ExampleFrame1ReturnActive || PostTopWeldFlipActive ||
            ExampleFrame3SnapActive || ThirdFrameReorientActive;

        if (waitingForGrab)
            _grabMeCueEnabled = true;
        else if (_grabMeCueEnabled)
            _grabMeCueEnabled = false;

        bool held = _grab != null && _grab.BeingHeld;
        bool wantPulse = waitingForGrab && _grabMeCueEnabled && !held;

        if (wantPulse && !_grabMeActive)
            ApplyGrabMe();
        else if (!wantPulse && _grabMeActive)
            ClearGrabMe();

        if (_grabMeActive)
            UpdateGrabMePulse();
    }

    void ApplyGrabMe()
    {
        if (_grabMeActive || grabMeMaterial == null)
            return;

        _grabMeColorProp = grabMeMaterial.HasProperty("_BaseColor")
            ? "_BaseColor"
            : (grabMeMaterial.HasProperty("_Color") ? "_Color" : null);
        _grabMeHasColorProp = _grabMeColorProp != null;
        _grabMeBaseColor = _grabMeHasColorProp ? grabMeMaterial.GetColor(_grabMeColorProp) : Color.white;
        _grabMeHasEmission = grabMeMaterial.HasProperty("_EmissionColor");
        _grabMeBaseEmission = _grabMeHasEmission ? grabMeMaterial.GetColor("_EmissionColor") : Color.black;

        _grabMeResolvedRenderers = (grabMeRenderers != null && grabMeRenderers.Length > 0)
            ? grabMeRenderers
            : GetComponentsInChildren<Renderer>(true);

        _grabMeOriginals = new Material[_grabMeResolvedRenderers.Length];
        _grabMeInstances = new Material[_grabMeResolvedRenderers.Length];
        for (int i = 0; i < _grabMeResolvedRenderers.Length; i++)
        {
            Renderer r = _grabMeResolvedRenderers[i];
            if (r == null)
                continue;
            _grabMeOriginals[i] = r.sharedMaterial;
            r.material = grabMeMaterial;
            _grabMeInstances[i] = r.material;
            if (grabMePulseEmission && _grabMeHasEmission)
                _grabMeInstances[i].EnableKeyword("_EMISSION");
        }

        _grabMeActive = true;
        UpdateGrabMePulse();
    }

    void UpdateGrabMePulse()
    {
        if (_grabMeInstances == null)
            return;

        float phase = 0.5f + 0.5f * Mathf.Sin(Time.time * grabMePulseSpeed * Mathf.PI * 2f);
        float b = Mathf.Lerp(grabMePulseMinBrightness, grabMePulseMaxBrightness, phase);

        for (int i = 0; i < _grabMeInstances.Length; i++)
        {
            Material inst = _grabMeInstances[i];
            if (inst == null)
                continue;

            if (_grabMeHasColorProp)
            {
                Color c = _grabMeBaseColor * b;
                c.a = _grabMeBaseColor.a;
                inst.SetColor(_grabMeColorProp, c);
            }

            if (grabMePulseEmission && _grabMeHasEmission)
                inst.SetColor("_EmissionColor", _grabMeBaseEmission * b);
        }
    }

    void ClearGrabMe()
    {
        if (!_grabMeActive)
            return;

        if (_grabMeResolvedRenderers != null && _grabMeOriginals != null)
        {
            for (int i = 0; i < _grabMeResolvedRenderers.Length; i++)
            {
                Renderer r = _grabMeResolvedRenderers[i];
                if (r != null && i < _grabMeOriginals.Length && _grabMeOriginals[i] != null)
                    r.material = _grabMeOriginals[i];
            }
        }

        _grabMeInstances = null;
        _grabMeActive = false;
    }
}
