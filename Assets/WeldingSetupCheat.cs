using BNG;
using UnityEngine;

/// <summary>
/// Testing helper (Right Controller A / keyboard A):
/// <list type="number">
/// <item>1st press — welder ON, clamp snapped, gas ON, ref pieces snapped, four bars snapped.</item>
/// <item>2nd press — force-complete Top + Bottom weld dots so merge / flip / ExampleFrame2 logic can continue.</item>
/// <item>3rd press — force-complete all corner welds so the combined frame glows for ExampleFrame1 return.</item>
/// <item>4th press — force-complete Top + Bottom face welds, snap Reference2 to its second guide, snap RealFrame to ExampleFrame3.</item>
/// </list>
/// Attach to any always-active object (e.g. RealFrame or an empty "Debug").
/// </summary>
public class WeldingSetupCheat : MonoBehaviour
{
    [Header("Enable")]
    [Tooltip("Master toggle. Turn off for builds / demos.")]
    public bool cheatEnabled = true;

    [Tooltip("Also fire when keyboard A is pressed (editor / desktop testing).")]
    public bool alsoAllowKeyboardA = true;

    [Header("Targets — stage 1 (first A)")]
    public onoffswitch welderSwitch;
    public clamp workClamp;
    public gasonoff gasKnob;

    [Tooltip("Reference pieces that should snap on the first A (same as the start of the real flow).")]
    public refpiece[] refPieces;

    [Tooltip("The four real bars. Leave empty to auto-find from Assembly Root or in the scene.")]
    public weldbar[] weldBars;

    [Tooltip("Optional: used to auto-fill Weld Bars / dots / corners if those arrays are empty.")]
    public WeldbarAssemblyRoot assemblyRoot;

    [Header("Targets — stage 2 (second A)")]
    [Tooltip("Top weld dots (usually mergeAfterEightSpotWelds / TopWeldDots). Leave null to take from Assembly Root.")]
    public SequentialWeldRevealSequence topWeldDots;

    [Tooltip("Bottom weld dots (after flip). Leave null to take from WeldbarMergedFlipSnapToAnchor.bottomWeldDotsForReposition.")]
    public SequentialWeldRevealSequence bottomWeldDots;

    [Header("Targets — stage 3 (third A)")]
    [Tooltip("Corner weld lines (CornerWelds). Leave null to take from WeldbarMergedFlipSnapToAnchor.cornerWeldsForReorient.")]
    public WeldLinesRevealOnSnap cornerWelds;

    [Header("Targets — stage 4 (fourth A)")]
    [Tooltip("Top face weld lines (TopWelds). Leave null to take from FlipSnap.topWeldsAfterExampleFrame1Return.")]
    public WeldLinesRevealOnSnap topFaceWelds;

    [Tooltip("Bottom face weld lines (BottomWelds). Leave null to take from FlipSnap.bottomWeldsAfterPostTopWeldFlip.")]
    public WeldLinesRevealOnSnap bottomFaceWelds;

    [Tooltip("Reference2 (or the piece that moves to a second guide after BottomWelds). Leave null to take from FlipSnap.requireRefPieceSecondSnap.")]
    public refpiece reference2ForSecondSnap;

    [Tooltip("Flip-snap on RealFrame. Leave null to auto-find.")]
    public WeldbarMergedFlipSnapToAnchor flipSnap;

    [Header("What to apply — stage 1")]
    public bool forceWelderOn = true;
    public bool forceClampSnap = true;
    public bool forceGasOn = true;
    public bool forceRefPiecesSnap = true;
    public bool forceBarsSnap = true;

    [Header("What to apply — stage 2")]
    public bool forceTopDotsComplete = true;
    public bool forceBottomDotsComplete = true;

    [Header("What to apply — stage 3")]
    public bool forceCornerWeldsComplete = true;

    [Header("What to apply — stage 4")]
    public bool forceTopFaceWeldsComplete = true;
    public bool forceBottomFaceWeldsComplete = true;
    public bool forceReference2SecondSnap = true;
    public bool forceExampleFrame3Snap = true;

    [Header("Debug")]
    public bool logToConsole = true;

    int _stage;

    void Start()
    {
        if (assemblyRoot == null)
            assemblyRoot = FindObjectOfType<WeldbarAssemblyRoot>();

        if ((weldBars == null || weldBars.Length == 0) && assemblyRoot != null && assemblyRoot.weldbars != null)
            weldBars = assemblyRoot.weldbars;

        if (weldBars == null || weldBars.Length == 0)
            weldBars = FindObjectsOfType<weldbar>();

        if (refPieces == null || refPieces.Length == 0)
            refPieces = FindObjectsOfType<refpiece>();

        if (welderSwitch == null)
            welderSwitch = FindObjectOfType<onoffswitch>();

        if (workClamp == null)
            workClamp = FindObjectOfType<clamp>();

        if (gasKnob == null)
            gasKnob = FindObjectOfType<gasonoff>();

        ResolveDotSequences();
        ResolveCornerWelds();
        ResolveStage4Targets();
    }

    void ResolveDotSequences()
    {
        if (topWeldDots == null && assemblyRoot != null)
            topWeldDots = assemblyRoot.mergeAfterEightSpotWelds;

        if (bottomWeldDots == null)
        {
            WeldbarMergedFlipSnapToAnchor flip = ResolveFlipSnap();
            if (flip != null)
                bottomWeldDots = flip.bottomWeldDotsForReposition;
        }
    }

    void ResolveCornerWelds()
    {
        if (cornerWelds != null)
            return;

        WeldbarMergedFlipSnapToAnchor flip = ResolveFlipSnap();
        if (flip != null)
            cornerWelds = flip.cornerWeldsForReorient;

        if (cornerWelds == null)
            cornerWelds = FindObjectOfType<WeldLinesRevealOnSnap>();
    }

    void ResolveStage4Targets()
    {
        WeldbarMergedFlipSnapToAnchor flip = ResolveFlipSnap();
        if (flip == null)
            return;

        if (topFaceWelds == null)
            topFaceWelds = flip.topWeldsAfterExampleFrame1Return;

        if (bottomFaceWelds == null)
            bottomFaceWelds = flip.bottomWeldsAfterPostTopWeldFlip;

        if (reference2ForSecondSnap == null)
            reference2ForSecondSnap = flip.requireRefPieceSecondSnap;
    }

    WeldbarMergedFlipSnapToAnchor ResolveFlipSnap()
    {
        if (flipSnap != null)
            return flipSnap;

        if (assemblyRoot != null)
            flipSnap = assemblyRoot.GetComponent<WeldbarMergedFlipSnapToAnchor>();
        if (flipSnap == null)
            flipSnap = FindObjectOfType<WeldbarMergedFlipSnapToAnchor>();
        return flipSnap;
    }

    void Update()
    {
        if (!cheatEnabled)
            return;

        bool pressed = false;

        InputBridge ib = InputBridge.Instance;
        if (ib != null && ib.AButtonDown)
            pressed = true;

        if (!pressed && alsoAllowKeyboardA && Input.GetKeyDown(KeyCode.A))
            pressed = true;

        if (!pressed)
            return;

        if (_stage <= 0)
        {
            ApplySetupCheat();
            _stage = 1;
        }
        else if (_stage == 1)
        {
            ApplyDotsCheat();
            _stage = 2;
        }
        else if (_stage == 2)
        {
            ApplyCornersCheat();
            _stage = 3;
        }
        else if (_stage == 3)
        {
            ApplyFaceWeldsAndExampleFrame3Cheat();
            _stage = 4;
        }
        else if (logToConsole)
        {
            Debug.Log("WeldingSetupCheat: A pressed again — stages already applied (setup + dots + corners + face/EF3).");
        }
    }

    /// <summary>Stage 1: power / ground / gas / ref pieces / four bars.</summary>
    public void ApplySetupCheat()
    {
        if (forceWelderOn && welderSwitch != null)
        {
            welderSwitch.ForceWelderOn();
            if (logToConsole)
                Debug.Log("WeldingSetupCheat [1]: welder ON");
        }

        if (forceClampSnap && workClamp != null)
        {
            bool ok = workClamp.ForceSnapForDebug();
            if (logToConsole)
                Debug.Log(ok ? "WeldingSetupCheat [1]: clamp snapped" : "WeldingSetupCheat [1]: clamp snap FAILED (missing guide?)");
        }

        if (forceGasOn && gasKnob != null)
        {
            gasKnob.ForceGasOnForDebug();
            if (logToConsole)
                Debug.Log("WeldingSetupCheat [1]: gas ON");
        }

        if (forceRefPiecesSnap)
        {
            if (refPieces == null || refPieces.Length == 0)
                refPieces = FindObjectsOfType<refpiece>();

            int okCount = 0;
            int total = 0;
            if (refPieces != null)
            {
                for (int i = 0; i < refPieces.Length; i++)
                {
                    refpiece piece = refPieces[i];
                    if (piece == null)
                        continue;
                    total++;
                    if (piece.ForceSnapForDebug())
                        okCount++;
                }
            }

            if (logToConsole)
                Debug.Log($"WeldingSetupCheat [1]: snapped {okCount}/{total} ref pieces");
        }

        if (forceBarsSnap && weldBars != null)
        {
            int okCount = 0;
            for (int i = 0; i < weldBars.Length; i++)
            {
                weldbar bar = weldBars[i];
                if (bar == null)
                    continue;
                if (bar.ForceSnapForDebug())
                    okCount++;
            }

            if (logToConsole)
                Debug.Log($"WeldingSetupCheat [1]: snapped {okCount}/{weldBars.Length} weld bars");
        }
    }

    /// <summary>Stage 2: force-complete top + bottom weld dots; merge / later gates can continue.</summary>
    public void ApplyDotsCheat()
    {
        ResolveDotSequences();

        if (forceTopDotsComplete)
        {
            if (topWeldDots != null)
            {
                topWeldDots.ForceCompleteAllStepsForDebug();
                if (logToConsole)
                    Debug.Log($"WeldingSetupCheat [2]: TOP dots force-completed ({topWeldDots.name})");
            }
            else if (logToConsole)
            {
                Debug.LogWarning("WeldingSetupCheat [2]: topWeldDots not assigned / not found");
            }
        }

        if (forceBottomDotsComplete)
        {
            if (bottomWeldDots != null)
            {
                bottomWeldDots.ForceCompleteAllStepsForDebug();
                if (logToConsole)
                    Debug.Log($"WeldingSetupCheat [2]: BOTTOM dots force-completed ({bottomWeldDots.name})");
            }
            else if (logToConsole)
            {
                Debug.LogWarning("WeldingSetupCheat [2]: bottomWeldDots not assigned / not found");
            }
        }
    }

    /// <summary>Stage 3: force-complete all corner welds → combined frame glows for ExampleFrame1 return.</summary>
    public void ApplyCornersCheat()
    {
        ResolveCornerWelds();

        if (!forceCornerWeldsComplete)
            return;

        if (cornerWelds != null)
        {
            cornerWelds.ForceCompleteAllLinesForDebug();
            if (logToConsole)
                Debug.Log($"WeldingSetupCheat [3]: corner welds force-completed ({cornerWelds.name}) — ExampleFrame1 glow should start");
        }
        else if (logToConsole)
        {
            Debug.LogWarning("WeldingSetupCheat [3]: cornerWelds not assigned / not found");
        }
    }

    /// <summary>
    /// Stage 4: Top + Bottom face welds complete, Reference2 on its second guide, RealFrame on ExampleFrame3.
    /// </summary>
    public void ApplyFaceWeldsAndExampleFrame3Cheat()
    {
        ResolveStage4Targets();
        WeldbarMergedFlipSnapToAnchor flip = ResolveFlipSnap();

        if (forceTopFaceWeldsComplete)
        {
            if (topFaceWelds != null)
            {
                topFaceWelds.ForceCompleteAllLinesForDebug();
                if (logToConsole)
                    Debug.Log($"WeldingSetupCheat [4]: TOP face welds force-completed ({topFaceWelds.name})");
            }
            else if (logToConsole)
            {
                Debug.LogWarning("WeldingSetupCheat [4]: topFaceWelds not assigned / not found");
            }
        }

        if (forceBottomFaceWeldsComplete)
        {
            if (bottomFaceWelds != null)
            {
                bottomFaceWelds.ForceCompleteAllLinesForDebug();
                if (logToConsole)
                    Debug.Log($"WeldingSetupCheat [4]: BOTTOM face welds force-completed ({bottomFaceWelds.name})");
            }
            else if (logToConsole)
            {
                Debug.LogWarning("WeldingSetupCheat [4]: bottomFaceWelds not assigned / not found");
            }
        }

        if (forceReference2SecondSnap)
        {
            if (reference2ForSecondSnap != null)
            {
                bool ok = reference2ForSecondSnap.ForceSnapToSecondGuideForDebug();
                if (logToConsole)
                    Debug.Log(ok
                        ? $"WeldingSetupCheat [4]: Reference2 snapped to second guide ({reference2ForSecondSnap.name})"
                        : "WeldingSetupCheat [4]: Reference2 second-guide snap FAILED (missing secondGuide?)");
            }
            else if (logToConsole)
            {
                Debug.LogWarning("WeldingSetupCheat [4]: reference2ForSecondSnap not assigned / not found");
            }
        }

        if (forceExampleFrame3Snap)
        {
            if (flip != null)
            {
                flip.ForceSnapToThirdExampleFrameForDebug();
                if (logToConsole)
                    Debug.Log("WeldingSetupCheat [4]: RealFrame snapped to ExampleFrame3 — InnerCornerWelds can unlock");
            }
            else if (logToConsole)
            {
                Debug.LogWarning("WeldingSetupCheat [4]: flipSnap not assigned / not found");
            }
        }
    }
}
