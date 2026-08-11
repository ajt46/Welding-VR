using System.Collections.Generic;
using UnityEngine;
using TMPro;
using BNG;  // remove if you don't use BNG and replace input code

/// <summary>
/// "No WeldingPanel under tip" means <see cref="CustomWeldingController.CurrentWeldingPanelUnderTip"/> was null
/// while <see cref="treatMissingPanelAsParameterFailure"/> is on — the tip raycast must hit a weldable collider whose
/// hierarchy has <see cref="WeldingPanel"/> (see gun <c>weldableLayers</c>, <c>raycastDistance</c>, and aim).
/// That is separate from <see cref="timeTolerance"/> (travel-speed good window).
/// </summary>
[DefaultExecutionOrder(100)]
public class WeldSpeedEvaluator : MonoBehaviour
{
    /// <summary>Per <see cref="WeldingPanel.PanelMaterial"/> result visuals (fast / good / slow travel, plus optional bad-parameter).</summary>
    [System.Serializable]
    public class MaterialSpeedResultSet
    {
        [Tooltip("Must match the WeldingPanel material on that sheet.")]
        public WeldingPanel.PanelMaterial material;

        [Tooltip("X1 for this material’s run. Both Path Start and Path End must be set for this row to register; otherwise use the global Points to start.")]
        public Transform pathStartPoint;

        [Tooltip("X2 for this material’s run (paired with Path Start).")]
        public Transform pathEndPoint;

        public GameObject fastWeldObject;
        public GameObject goodWeldObject;
        public GameObject slowWeldObject;

        [Tooltip("Shown when X1–X2 ends with wrong voltage/wire/gas/angle for this material (if null, WeldParameterMonitor fallback is used).")]
        public GameObject badParameterWeldObject;
    }

    [Header("Points — legacy default X1 / X2")]
    [Tooltip("Default start (X1) when a material row has no Path Start / End assigned, or when using only legacy paths.")]
    public Transform startPoint;

    [Tooltip("Default end (X2) when a material row has no Path Start / End assigned.")]
    public Transform endPoint;

    [Tooltip("Where timing is measured from (gun tip).")]
    public Transform weldTip;

    [Tooltip("How close the tip must be to X1 / X2 to start or end timing. Larger = easier to register start/end.")]
    public float pointRadius = 0.05f;

    [Header("Result Objects — per material (recommended)")]
    [Tooltip("One entry per sheet/material. Assign optional Path Start/End (X1/X2) per row, plus fast/good/slow/bad objects. Empty path fields fall back to global Points above.")]
    public MaterialSpeedResultSet[] materialSpeedResults;

    [Header("Result Objects — legacy fallback")]
    [Tooltip("Used when no matching material entry or that slot is empty.")]
    public GameObject fastWeldObject;  // has fastweld.cs
    public GameObject goodWeldObject;  // has goodweld.cs
    public GameObject slowWeldObject;  // has slowweld.cs

    [Header("Result Text")]
    [Tooltip("Optional TextMeshPro text for weld results.")]
    public TMP_Text resultText;

    [Header("Result messages (speed / travel time)")]
    [Tooltip("Shown when travel time is below the acceptable window.")]
    public string resultMessageTooFast = "Too fast";

    [Tooltip("Shown when travel time is within the acceptable window.")]
    public string resultMessageGood = "Good";

    [Tooltip("Shown when travel time is above the acceptable window.")]
    public string resultMessageTooSlow = "Too slow";

    [Header("Result messages (material / parameters)")]
    [Tooltip("When multiple parameters failed: {0} = material name (e.g. MildSteel). {1} = list (e.g. Voltage, Wire speed, and Gas flow).")]
    public string materialFailureFormatWithMaterial = "{0}: Wrong — {1}";

    [Tooltip("Same as above but when the material name was not sampled during the run. {0} = list only.")]
    public string materialFailureFormatWithoutMaterial = "Wrong — {0}";

    [Tooltip("Between items in a list of 3+ (after each except the last two).")]
    public string listSeparatorBetween = ", ";

    [Tooltip("Between the last two items when there are 3+ (e.g. ', and ' gives 'a, b, and c').")]
    public string listSeparatorBeforeLast = ", and ";

    [Tooltip("Between exactly two items (e.g. ' and ' gives 'Voltage and Wire speed').")]
    public string listSeparatorTwoItems = " and ";

    [Header("Timing Settings")]
    [Tooltip("Used with X1–X2 distance to compute ideal segment time. Tune to your scene scale.")]
    public float idealTravelSpeed = 0.15f;

    [Tooltip("Acceptable travel-time band around ideal: e.g. 0.35 = ±35% (wider = easier to get \"Good\" for speed). Does not fix missing WeldingPanel detection.")]
    [Range(0.01f, 0.9f)]
    public float timeTolerance = 0.25f;

    // Trigger threshold (for BNG InputBridge)
    public float triggerThreshold = 0.5f;

    [Header("Material parameters (same X1–X2 path)")]
    [Tooltip("Optional: while moving from X1 to X2, voltage/wire/gas/angle must stay valid on the WeldingPanel under the tip. If any frame fails, bad weld shows when you reach X2 (travel speed result is skipped).")]
    public WeldParameterMonitor parameterMonitor;

    [Tooltip("Gun controller: used for WeldingPanel under tip during X1–X2 and for optional post-eval welding block.")]
    public CustomWeldingController weldingController;

    [Tooltip("If false, travel speed is evaluated only (no voltage/wire/gas checks during the run). Use while setting up or if you only care about timing.")]
    public bool requireMaterialParametersDuringX1X2 = true;

    [Tooltip("If true, any frame during X1–X2 with no WeldingPanel under the tip fails the run and shows No WeldingPanel message (if no other failures). If false, you can still get fast/good/slow from travel time even when the tip is not on a panel.")]
    public bool treatMissingPanelAsParameterFailure = true;

    [Header("Cleanup")]
    [Tooltip("If true, destroy all weld blobs (tag 'WeldObject') when timing ends.")]
    public bool clearBlobsOnEnd = true;

    [Header("Welding Control")]
    [Tooltip("Cooldown on the gun after evaluation: no sparks or blobs until this many seconds pass AND the trigger has been released at least once.")]
    public float postEvaluationBlockSeconds = 1f;

    [Tooltip("Fallback when material failed but no specific labels were recorded (e.g. no knobs assigned).")]
    public string materialParametersFailedMessage = "Material settings wrong";

    [Tooltip("Shown when the tip was not on a WeldingPanel during the timed run (if that counts as failure).")]
    public string noWeldingPanelMessage = "No WeldingPanel under tip";

    bool isTiming = false;
    float startTime = 0f;
    Transform _activeStartPoint;
    Transform _activeEndPoint;
    bool _hasLockedPathMaterial;
    WeldingPanel.PanelMaterial _lockedPathMaterial;

    bool _hadBadParameterDuringX1X2;
    bool _hadNoPanelFailure;

    readonly HashSet<string> _failedParameterLabels = new HashSet<string>();
    bool _hadMaterialSampleDuringRun;
    WeldingPanel.PanelMaterial _lastMaterialSample;

    static readonly string[] FailureLabelOrder = { "Tip on surface", "Voltage", "Wire speed", "Gas flow", "Gun angle", "Work angle" };

    InputBridge input;

    void Start()
    {
        input = InputBridge.Instance;
        SetAllInactive();
    }

    void SetAllInactive()
    {
        SetResultObjectInactive(fastWeldObject);
        SetResultObjectInactive(goodWeldObject);
        SetResultObjectInactive(slowWeldObject);

        if (materialSpeedResults != null)
        {
            for (int i = 0; i < materialSpeedResults.Length; i++)
            {
                MaterialSpeedResultSet s = materialSpeedResults[i];
                if (s == null)
                    continue;
                SetResultObjectInactive(s.fastWeldObject);
                SetResultObjectInactive(s.goodWeldObject);
                SetResultObjectInactive(s.slowWeldObject);
                SetResultObjectInactive(s.badParameterWeldObject);
            }
        }
    }

    static void SetResultObjectInactive(GameObject go)
    {
        if (go != null)
            go.SetActive(false);
    }

    MaterialSpeedResultSet FindMaterialResultSet(WeldingPanel.PanelMaterial material)
    {
        if (materialSpeedResults == null)
            return null;
        for (int i = 0; i < materialSpeedResults.Length; i++)
        {
            MaterialSpeedResultSet s = materialSpeedResults[i];
            if (s != null && s.material == material)
                return s;
        }

        return null;
    }

    GameObject PickFastForMaterial(WeldingPanel.PanelMaterial material)
    {
        MaterialSpeedResultSet s = FindMaterialResultSet(material);
        if (s != null && s.fastWeldObject != null)
            return s.fastWeldObject;
        return fastWeldObject;
    }

    GameObject PickGoodForMaterial(WeldingPanel.PanelMaterial material)
    {
        MaterialSpeedResultSet s = FindMaterialResultSet(material);
        if (s != null && s.goodWeldObject != null)
            return s.goodWeldObject;
        return goodWeldObject;
    }

    GameObject PickSlowForMaterial(WeldingPanel.PanelMaterial material)
    {
        MaterialSpeedResultSet s = FindMaterialResultSet(material);
        if (s != null && s.slowWeldObject != null)
            return s.slowWeldObject;
        return slowWeldObject;
    }

    GameObject PickBadParameterForMaterial(WeldingPanel.PanelMaterial material)
    {
        MaterialSpeedResultSet s = FindMaterialResultSet(material);
        if (s != null && s.badParameterWeldObject != null)
            return s.badParameterWeldObject;
        return null;
    }

    WeldingPanel.PanelMaterial ResolveMaterialForSpeedResult()
    {
        if (_hasLockedPathMaterial)
            return _lockedPathMaterial;

        if (_hadMaterialSampleDuringRun)
            return _lastMaterialSample;

        if (weldingController != null)
        {
            WeldingPanel p = weldingController.CurrentWeldingPanelUnderTip;
            if (p != null)
                return p.GetPanelMaterial();
        }

        return WeldingPanel.PanelMaterial.MildSteel;
    }

    bool HasAnyPathConfigured()
    {
        if (startPoint != null && endPoint != null)
            return true;

        if (materialSpeedResults != null)
        {
            for (int i = 0; i < materialSpeedResults.Length; i++)
            {
                MaterialSpeedResultSet s = materialSpeedResults[i];
                if (s != null && s.pathStartPoint != null && s.pathEndPoint != null)
                    return true;
            }
        }

        return false;
    }

    bool TryGetClosestStartWithinRadius(out Transform pathStart, out Transform pathEnd, out bool lockMaterial, out WeldingPanel.PanelMaterial lockedMat)
    {
        Transform bestStart = null;
        Transform bestEnd = null;
        bool bestLock = false;
        WeldingPanel.PanelMaterial bestMat = WeldingPanel.PanelMaterial.MildSteel;

        float bestDist = float.MaxValue;
        bool found = false;

        void Consider(Transform st, Transform en, bool hasMat, WeldingPanel.PanelMaterial mat)
        {
            if (st == null || en == null || weldTip == null)
                return;

            float d = Vector3.Distance(weldTip.position, st.position);
            if (d > pointRadius || d >= bestDist)
                return;

            bestDist = d;
            bestStart = st;
            bestEnd = en;
            bestLock = hasMat;
            bestMat = mat;
            found = true;
        }

        if (startPoint != null && endPoint != null)
            Consider(startPoint, endPoint, false, default);

        if (materialSpeedResults != null)
        {
            for (int i = 0; i < materialSpeedResults.Length; i++)
            {
                MaterialSpeedResultSet s = materialSpeedResults[i];
                if (s == null || s.pathStartPoint == null || s.pathEndPoint == null)
                    continue;
                Consider(s.pathStartPoint, s.pathEndPoint, true, s.material);
            }
        }

        pathStart = bestStart;
        pathEnd = bestEnd;
        lockMaterial = bestLock;
        lockedMat = bestMat;
        return found;
    }

    void ClearActivePath()
    {
        _activeStartPoint = null;
        _activeEndPoint = null;
        _hasLockedPathMaterial = false;
    }

    bool IsReservedResultObject(GameObject go)
    {
        if (go == null)
            return false;
        if (go == fastWeldObject || go == goodWeldObject || go == slowWeldObject)
            return true;
        if (materialSpeedResults == null)
            return false;
        for (int i = 0; i < materialSpeedResults.Length; i++)
        {
            MaterialSpeedResultSet s = materialSpeedResults[i];
            if (s == null)
                continue;
            if (go == s.fastWeldObject || go == s.goodWeldObject || go == s.slowWeldObject || go == s.badParameterWeldObject)
                return true;
        }

        return false;
    }

    void Update()
    {
        if (weldTip == null || !HasAnyPathConfigured())
            return;

        // Match hand holding gun when CustomWeldingController is assigned (see GetActiveWeldingTriggerValue).
        float trigger = 0f;
        if (weldingController != null)
            trigger = weldingController.GetActiveWeldingTriggerValue();
        else if (input != null)
            trigger = Mathf.Max(input.LeftTrigger, input.RightTrigger);

        // 1) START timing : trigger pressed near an X1 (closest if several overlap)
        if (!isTiming &&
            trigger >= triggerThreshold &&
            TryGetClosestStartWithinRadius(out Transform pathStart, out Transform pathEnd, out bool lockMat, out WeldingPanel.PanelMaterial lockedMat))
        {
            BeginTiming(pathStart, pathEnd, lockMat, lockedMat);
        }

        // While timing: sample material parameters every frame (same path as travel evaluation)
        if (isTiming && requireMaterialParametersDuringX1X2 && parameterMonitor != null)
        {
            SampleMaterialParametersDuringRun();
        }

        // 2) END timing : still holding trigger, reach the locked X2
        if (isTiming &&
            trigger >= triggerThreshold &&
            _activeEndPoint != null &&
            Vector3.Distance(weldTip.position, _activeEndPoint.position) <= pointRadius)
        {
            EndTiming();
        }

        // Optional safety: if you fully release trigger mid-way, cancel timing
        if (isTiming && trigger < 0.01f)
        {
            CancelTiming();
        }
    }

    void SampleMaterialParametersDuringRun()
    {
        if (weldingController == null)
            return;

        WeldingPanel panel = weldingController.CurrentWeldingPanelUnderTip;
        if (panel == null)
        {
            if (treatMissingPanelAsParameterFailure)
            {
                _hadBadParameterDuringX1X2 = true;
                _hadNoPanelFailure = true;
            }
            return;
        }

        _hadMaterialSampleDuringRun = true;
        _lastMaterialSample = panel.GetPanelMaterial();

        bool ok = parameterMonitor.EvaluateParameters(panel, out int checkCount);
        if (checkCount == 0)
            return;

        if (!ok)
            _hadBadParameterDuringX1X2 = true;

        parameterMonitor.MergeFailuresForPanel(panel, _failedParameterLabels);
    }

    void BeginTiming(Transform activeStart, Transform activeEnd, bool lockMaterialFromPath, WeldingPanel.PanelMaterial lockedMat)
    {
        _activeStartPoint = activeStart;
        _activeEndPoint = activeEnd;
        _hasLockedPathMaterial = lockMaterialFromPath;
        _lockedPathMaterial = lockedMat;

        _hadBadParameterDuringX1X2 = false;
        _hadNoPanelFailure = false;
        _hadMaterialSampleDuringRun = false;
        _failedParameterLabels.Clear();

        if (parameterMonitor != null)
            parameterMonitor.HideBadWeldForSpeedRun();

        isTiming = true;
        startTime = Time.time;
        SetAllInactive(); // hide previous result
        SetResultText(string.Empty);
    }

    void EndTiming()
    {
        isTiming = false;

        Transform segStart = _activeStartPoint;
        Transform segEnd = _activeEndPoint;

        if (_hadBadParameterDuringX1X2)
        {
            SetAllInactive();
            SetResultText(BuildMaterialFailureMessage());

            GameObject badForMaterial = PickBadParameterForMaterial(ResolveMaterialForSpeedResult());
            if (badForMaterial != null)
                badForMaterial.SetActive(true);
            else if (parameterMonitor != null)
                parameterMonitor.ShowBadWeldAfterFailedParameters();

            if (clearBlobsOnEnd)
                ClearAllBlobs();

            BlockWeldingAfterEval();

            Debug.Log("WeldSpeedEvaluator: X1–X2 finished with bad material parameters.");
            ClearActivePath();
            return;
        }

        float totalTime = Time.time - startTime;

        // Compute ideal time from distance / speed (locked segment for this run)
        float distance = 1f;
        if (segStart != null && segEnd != null)
            distance = Vector3.Distance(segStart.position, segEnd.position);
        float idealTime = distance / Mathf.Max(idealTravelSpeed, 0.001f);

        float minOkTime = idealTime * (1f - timeTolerance); // too fast below this
        float maxOkTime = idealTime * (1f + timeTolerance); // too slow above this

        SetAllInactive();

        WeldingPanel.PanelMaterial matForResult = ResolveMaterialForSpeedResult();

        if (totalTime < minOkTime)
        {
            GameObject fastGo = PickFastForMaterial(matForResult);
            if (fastGo != null)
                fastGo.SetActive(true);
            SetResultText(resultMessageTooFast);
        }
        else if (totalTime > maxOkTime)
        {
            GameObject slowGo = PickSlowForMaterial(matForResult);
            if (slowGo != null)
                slowGo.SetActive(true);
            SetResultText(resultMessageTooSlow);
        }
        else
        {
            GameObject goodGo = PickGoodForMaterial(matForResult);
            if (goodGo != null)
                goodGo.SetActive(true);
            SetResultText(resultMessageGood);
        }

        // Clear blobs after evaluation if requested
        if (clearBlobsOnEnd)
        {
            ClearAllBlobs();
        }

        BlockWeldingAfterEval();

        // Debug info in Console if you want
        Debug.Log($"Weld time : {totalTime:F2}s, ideal : {idealTime:F2}s");

        ClearActivePath();
    }

    void BlockWeldingAfterEval()
    {
        if (weldingController != null)
            weldingController.LockGunAfterEvaluation(postEvaluationBlockSeconds);
    }

    void CancelTiming()
    {
        isTiming = false;
        startTime = 0f;
        _hadBadParameterDuringX1X2 = false;
        _hadNoPanelFailure = false;
        _hadMaterialSampleDuringRun = false;
        _failedParameterLabels.Clear();
        ClearActivePath();
        if (parameterMonitor != null)
            parameterMonitor.HideBadWeldForSpeedRun();
    }

    string BuildMaterialFailureMessage()
    {
        if (_failedParameterLabels.Count > 0)
        {
            string listText = FormatNaturalList(OrderFailedLabels());
            bool haveMaterialName = _hadMaterialSampleDuringRun || _hasLockedPathMaterial;
            if (haveMaterialName)
            {
                if (!string.IsNullOrEmpty(materialFailureFormatWithMaterial))
                    return string.Format(materialFailureFormatWithMaterial, ResolveMaterialForSpeedResult(), listText);
            }
            else
            {
                if (!string.IsNullOrEmpty(materialFailureFormatWithoutMaterial))
                    return string.Format(materialFailureFormatWithoutMaterial, listText);
            }

            // Fallback if user cleared formats
            string mat = haveMaterialName ? ResolveMaterialForSpeedResult() + ": " : "";
            return mat + "Wrong — " + listText;
        }

        if (_hadNoPanelFailure)
            return noWeldingPanelMessage;

        return materialParametersFailedMessage;
    }

    List<string> OrderFailedLabels()
    {
        var list = new List<string>();
        foreach (string label in FailureLabelOrder)
        {
            if (_failedParameterLabels.Contains(label))
                list.Add(label);
        }
        foreach (string label in _failedParameterLabels)
        {
            if (!list.Contains(label))
                list.Add(label);
        }
        return list;
    }

    string FormatNaturalList(List<string> items)
    {
        if (items == null || items.Count == 0)
            return string.Empty;
        if (items.Count == 1)
            return items[0];
        if (items.Count == 2)
            return items[0] + (listSeparatorTwoItems ?? " and ") + items[1];

        string between = listSeparatorBetween ?? ", ";
        string beforeLast = listSeparatorBeforeLast ?? ", and ";
        var head = string.Join(between, items.GetRange(0, items.Count - 1));
        return head + beforeLast + items[items.Count - 1];
    }

    void SetResultText(string message)
    {
        if (resultText != null)
        {
            resultText.text = message;
        }
    }

    /// <summary>
    /// Destroy all weld blobs (tag "WeldObject"), but keep any result objects that
    /// might also share that tag.
    /// </summary>
    void ClearAllBlobs()
    {
        GameObject[] blobs = GameObject.FindGameObjectsWithTag("WeldObject");
        for (int i = 0; i < blobs.Length; i++)
        {
            GameObject go = blobs[i];

            // Don't destroy the result indicators if they happen to use the same tag
            if (IsReservedResultObject(go))
                continue;

            Destroy(go);
        }
    }
}
