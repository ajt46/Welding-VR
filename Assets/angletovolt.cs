using UnityEngine;
using TMPro;

/// <summary>
/// Voltage from local knob angle using discrete brackets: each volt (18 … 24)
/// has its own min/max angle. Display only changes when the angle enters another bracket.
/// </summary>
public class angletovolt : MonoBehaviour
{
    [Header("Target")]
    public Transform target;

    [Header("Angle")]
    public Vector3 localAxis = Vector3.right;

    [Tooltip("Optional: clamp effective angle before bracket lookup.")]
    public bool clampEffectiveAngle = false;

    [Tooltip("Used only when clamp effective angle is on.")]
    public float minAngleDegrees = -90f;

    [Tooltip("Used only when clamp effective angle is on.")]
    public float maxAngleDegrees = 90f;

    [Header("Angle tuning")]
    public float angleOffsetDegrees = 0f;
    public bool invertAngle = false;
    public float angleScale = 1f;

    [Header("Voltage brackets")]
    [Tooltip("Each row: voltage (18, 19, … 24) and inclusive angle range. First matching row wins if ranges overlap.")]
    public AngleOutputBracket[] voltageBrackets = new AngleOutputBracket[0];

    [Tooltip("If true, when the angle is between brackets (gap), keep the last output until a new bracket is entered.")]
    public bool holdLastOutputInAngleGaps = true;

    [Header("Physical rotation limits")]
    [Tooltip("If true, the knob cannot rotate past the min/max (same space as bracket angles: after offset/invert/scale).")]
    public bool enforcePhysicalRotationLimits = true;

    [Tooltip("If true, physical min/max are set from the smallest/largest angles in voltage brackets (updated in Awake).")]
    public bool derivePhysicalLimitsFromBrackets = true;

    [Tooltip("Used when derive is off, or as override after refresh. Effective angle degrees.")]
    public float physicalMinAngleDegrees = -90f;

    [Tooltip("Used when derive is off, or as override after refresh. Effective angle degrees.")]
    public float physicalMaxAngleDegrees = 90f;

    [Tooltip("If assigned (or found on target), angular velocity is cleared when the rotation is clamped.")]
    public Rigidbody targetRigidbody;

    [Header("Output")]
    public TMP_Text outputText;

    [Tooltip("{0} = volts, {1} = effective angle (degrees).")]
    public string displayFormat = "{0:F0} V | {1:F1}°";

    public bool showAngleInDisplay = true;

    private Quaternion initialLocalRotation;
    private float lastOutput = 21f;

    void Awake()
    {
        if (derivePhysicalLimitsFromBrackets)
            RefreshDerivedPhysicalLimits();
    }

    void Start()
    {
        if (target == null)
            target = transform;
        initialLocalRotation = target.localRotation;

        if (targetRigidbody == null)
            targetRigidbody = target.GetComponent<Rigidbody>();

        if (derivePhysicalLimitsFromBrackets)
            RefreshDerivedPhysicalLimits();

        lastOutput = AngleMappingHelper.ResolveNearestBracketOutput(
            GetEffectiveAngleDegrees(),
            voltageBrackets,
            21f);

        if (enforcePhysicalRotationLimits)
            EnforcePhysicalRotationLimits();

        UpdateDisplay();
    }

    void LateUpdate()
    {
        if (enforcePhysicalRotationLimits)
            EnforcePhysicalRotationLimits();

        UpdateDisplay();
    }

    void RefreshDerivedPhysicalLimits()
    {
        if (AngleRotationLimiter.TryComputeBoundsFromBrackets(voltageBrackets, out float lo, out float hi))
        {
            physicalMinAngleDegrees = lo;
            physicalMaxAngleDegrees = hi;
        }
    }

    void EnforcePhysicalRotationLimits()
    {
        AngleRotationLimiter.EnforceRotationLimits(
            target,
            initialLocalRotation,
            localAxis,
            physicalMinAngleDegrees,
            physicalMaxAngleDegrees,
            angleOffsetDegrees,
            invertAngle,
            angleScale,
            targetRigidbody);
    }

    float GetEffectiveAngleDegrees()
    {
        float raw = AngleMappingHelper.GetSignedAngleDegrees(initialLocalRotation, target, localAxis);
        float a = AngleMappingHelper.ApplyAngleTuning(raw, angleOffsetDegrees, invertAngle, angleScale);
        if (clampEffectiveAngle)
            a = Mathf.Clamp(a, minAngleDegrees, maxAngleDegrees);
        return a;
    }

    /// <summary>Current voltage from knob brackets (same logic as the display).</summary>
    public float GetCurrentVoltage()
    {
        float angle = GetEffectiveAngleDegrees();
        return AngleMappingHelper.ResolveOutputFromBrackets(
            angle,
            voltageBrackets,
            holdLastOutputInAngleGaps,
            ref lastOutput,
            lastOutput);
    }

    void UpdateDisplay()
    {
        float angle = GetEffectiveAngleDegrees();
        float v = AngleMappingHelper.ResolveOutputFromBrackets(
            angle,
            voltageBrackets,
            holdLastOutputInAngleGaps,
            ref lastOutput,
            lastOutput);

        if (outputText == null)
            return;

        outputText.text = AngleMappingHelper.FormatValueAndAngle(displayFormat, showAngleInDisplay, v, angle);
    }

    [ContextMenu("Fill default brackets (18–24 V, angles -90° to 90° contiguous)")]
    void FillDefaultVoltageBrackets()
    {
        const int n = 7;
        const float v0 = 18f;
        voltageBrackets = new AngleOutputBracket[n];
        float angleSpan = 180f / n;
        for (int i = 0; i < n; i++)
        {
            voltageBrackets[i].outputValue = v0 + i;
            voltageBrackets[i].minAngleDegrees = -90f + i * angleSpan;
            voltageBrackets[i].maxAngleDegrees = i == n - 1 ? 90f : -90f + (i + 1) * angleSpan;
        }

        if (derivePhysicalLimitsFromBrackets)
            RefreshDerivedPhysicalLimits();
    }

    [ContextMenu("Refresh physical limits from brackets")]
    void ContextRefreshPhysicalLimits()
    {
        RefreshDerivedPhysicalLimits();
    }

    public void ToggleInvertAngle()
    {
        invertAngle = !invertAngle;
    }

    public void SetInvertAngle(bool value)
    {
        invertAngle = value;
    }
}
