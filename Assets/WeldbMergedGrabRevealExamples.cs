using BNG;
using UnityEngine;

/// <summary>
/// After <see cref="WeldbarAssemblyRoot"/> merges: <see cref="exampleweldbar"/> guides are visible only while the merged
/// joint <see cref="Grabbable"/> is held — hidden on release. Optional <see cref="exampleframe"/> objects can mirror
/// the same grab-held rule. Supports up to three example sets (initial → after bottom dots → after ref second snap).
/// Attach to the same GameObject as <see cref="WeldbarAssemblyRoot"/> / merged <see cref="Grabbable"/>.
/// </summary>
[DisallowMultipleComponent]
public class WeldbMergedGrabRevealExamples : MonoBehaviour
{
    [Tooltip("Same object that ran merge — used to gate on HasMergedAssembly.")]
    public WeldbarAssemblyRoot assemblyRoot;

    [Tooltip("Merged joint grab. If empty, tries GetComponent on this object.")]
    public Grabbable jointGrabbable;

    [Header("Example weld bars")]
    [Tooltip("First set of exampleweldbar guides toggled SetVisible(grabHeld) while merged.")]
    public exampleweldbar[] exampleWeldbars = new exampleweldbar[4];

    [Header("Example weld bars — switch set (optional)")]
    [Tooltip("Second set of exampleweldbar guides that replaces the first set once Switch Example Bars When Welds Complete has finished all steps.")]
    public exampleweldbar[] secondExampleWeldbars;

    [Tooltip("Bottom weld dots (any SequentialWeldRevealSequence). When it reports HasCompletedAllWeldSteps, the reveal switches from the first set to the second set.")]
    public SequentialWeldRevealSequence switchExampleBarsWhenWeldsComplete;

    [Header("Example — revert to first set (optional)")]
    [Tooltip("Corner weld lines group. Once it reports HasWeldedAllLines, the reveal reverts to the FIRST set (example 1) and the second set stops showing.")]
    public WeldLinesRevealOnSnap revertToFirstSetWhenComplete;

    [Header("Example — third set (optional)")]
    [Tooltip("Third set of exampleweldbar guides (e.g. under ExampleFrame3). Used once Switch To Third Set When Ref Second Snap (or the flip-snap clause) reports the ref piece snapped onto its second guide.")]
    public exampleweldbar[] thirdExampleWeldbars;

    [Tooltip("Direct gate: when this ref piece reports HasSnappedOnSecondGuide, switch to Third Example Weldbars / Frames. If null, uses WeldbarMergedFlipSnapToAnchor.ShouldUseThirdExampleFrame on this object.")]
    public refpiece switchToThirdSetWhenRefSecondSnap;

    [Header("Example frames")]
    [Tooltip("First set of example frame guides tied to merged joint grab (e.g. full frame ghost).")]
    public exampleframe[] exampleFrames;

    [Tooltip("Second set of example frames, used during the same phase as Second Example Weldbars. Reverts to the first set with the bars.")]
    public exampleframe[] secondExampleFrames;

    [Tooltip("Third set of example frames, used during the same phase as Third Example Weldbars.")]
    public exampleframe[] thirdExampleFrames;

    [Tooltip("When true (default): frames visible only while merged joint Grab is held; hide on release.")]
    public bool syncExampleFramesToGrabHeld = true;

    /// <summary>Set true permanently after merged joint grab was held once (<see cref="WeldbarMergedFlipSnapToAnchor"/>).</summary>
    bool _grabbedMergedAtLeastOnceForFlipGate;

    WeldbarMergedFlipSnapToAnchor _flipSnap;

    void Awake()
    {
        if (assemblyRoot == null)
            assemblyRoot = GetComponent<WeldbarAssemblyRoot>();
        _flipSnap = GetComponent<WeldbarMergedFlipSnapToAnchor>();
    }

    /// <summary>
    /// <see cref="WeldbarAssemblyRoot"/> adds <see cref="Grabbable"/> at merge time — not in Awake — so resolve lazily.
    /// </summary>
    Grabbable ResolveJointGrabbable()
    {
        if (jointGrabbable != null)
            return jointGrabbable;
        if (assemblyRoot != null)
            jointGrabbable = assemblyRoot.GetComponent<Grabbable>();
        if (jointGrabbable == null)
            jointGrabbable = GetComponent<Grabbable>();
        return jointGrabbable;
    }

    /// <summary>
    /// Third-set phase: Reference2 (or assigned ref) finished its second-guide snap.
    /// Takes priority over first/second sets.
    /// </summary>
    bool UseThirdSet
    {
        get
        {
            if (switchToThirdSetWhenRefSecondSnap != null)
                return switchToThirdSetWhenRefSecondSnap.HasSnappedOnSecondGuide;

            if (_flipSnap == null)
                _flipSnap = GetComponent<WeldbarMergedFlipSnapToAnchor>();

            return _flipSnap != null && _flipSnap.ShouldUseThirdExampleFrame;
        }
    }

    /// <summary>
    /// True during the second-set phase: after <see cref="switchExampleBarsWhenWeldsComplete"/> completes,
    /// but before <see cref="revertToFirstSetWhenComplete"/> welds all its lines (which reverts to example 1).
    /// Ignored while the third set is active.
    /// </summary>
    bool UseSecondSet
    {
        get
        {
            if (UseThirdSet)
                return false;

            bool switched = switchExampleBarsWhenWeldsComplete != null &&
                            switchExampleBarsWhenWeldsComplete.HasCompletedAllWeldSteps;
            bool reverted = revertToFirstSetWhenComplete != null &&
                            revertToFirstSetWhenComplete.HasWeldedAllLines;
            return switched && !reverted;
        }
    }

    /// <summary>
    /// Active example weld bars: third set → second set → first set.
    /// </summary>
    exampleweldbar[] ActiveExampleWeldbars
    {
        get
        {
            if (UseThirdSet && thirdExampleWeldbars != null && thirdExampleWeldbars.Length > 0)
                return thirdExampleWeldbars;
            if (UseSecondSet && secondExampleWeldbars != null && secondExampleWeldbars.Length > 0)
                return secondExampleWeldbars;
            return exampleWeldbars;
        }
    }

    /// <summary>Example frames matching the active phase.</summary>
    exampleframe[] ActiveExampleFrames
    {
        get
        {
            if (UseThirdSet && thirdExampleFrames != null && thirdExampleFrames.Length > 0)
                return thirdExampleFrames;
            if (UseSecondSet && secondExampleFrames != null && secondExampleFrames.Length > 0)
                return secondExampleFrames;
            return exampleFrames;
        }
    }

    static void SetBarsVisible(exampleweldbar[] bars, bool visible)
    {
        if (bars == null)
            return;
        foreach (var g in bars)
        {
            if (g != null)
                g.SetVisible(visible);
        }
    }

    static void SetFramesVisible(exampleframe[] frames, bool visible)
    {
        if (frames == null)
            return;
        foreach (var f in frames)
        {
            if (f != null)
                f.SetVisible(visible);
        }
    }

    void Update()
    {
        if (assemblyRoot == null || !assemblyRoot.HasMergedAssembly)
            return;

        Grabbable grab = ResolveJointGrabbable();
        bool held = grab != null && grab.BeingHeld;

        if (held)
            _grabbedMergedAtLeastOnceForFlipGate = true;

        exampleweldbar[] active = ActiveExampleWeldbars;

        // Keep non-active sets hidden so only one set shows at a time.
        if (exampleWeldbars != active)
            SetBarsVisible(exampleWeldbars, false);
        if (secondExampleWeldbars != active)
            SetBarsVisible(secondExampleWeldbars, false);
        if (thirdExampleWeldbars != active)
            SetBarsVisible(thirdExampleWeldbars, false);

        SetBarsVisible(active, held);

        if (syncExampleFramesToGrabHeld)
        {
            exampleframe[] activeFrames = ActiveExampleFrames;

            if (exampleFrames != activeFrames)
                SetFramesVisible(exampleFrames, false);
            if (secondExampleFrames != activeFrames)
                SetFramesVisible(secondExampleFrames, false);
            if (thirdExampleFrames != activeFrames)
                SetFramesVisible(thirdExampleFrames, false);

            SetFramesVisible(activeFrames, held);
        }
    }

    /// <summary>True after merged joint grab was held at least once (guides may toggle off afterward).</summary>
    public bool HasRevealedExampleBars => _grabbedMergedAtLeastOnceForFlipGate;

    /// <summary>Allows re-arming flip snap gate/testing.</summary>
    public void ResetRevealLatch()
    {
        _grabbedMergedAtLeastOnceForFlipGate = false;
    }
}
