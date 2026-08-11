using UnityEngine;
using TMPro;
using UnityEngine.Serialization;

/// <summary>
/// Gas on/off from knob angle. Initial state is Gas Off.
/// </summary>
public class gasonoff : MonoBehaviour
{
    public enum GasOnAngleMode
    {
        [Tooltip("Gas ON when effective angle >= threshold (or |angle| >= threshold if using absolute).")]
        AtOrAboveThreshold,
        [Tooltip("Gas ON when effective angle is between min and max (inclusive), after tuning.")]
        BetweenAngles
    }

    [Header("Target")]
    [Tooltip("Knob transform to read local rotation from.")]
    public Transform target;

    [Header("Angle")]
    [Tooltip("Local axis to measure (usually X for a front knob).")]
    public Vector3 localAxis = Vector3.right;

    [Header("Gas ON (effective angle, ° after tuning)")]
    [Tooltip("How the knob angle decides Gas ON vs Gas Off.")]
    public GasOnAngleMode gasOnAngleMode = GasOnAngleMode.AtOrAboveThreshold;

    [FormerlySerializedAs("angleThresholdDegrees")]
    [Tooltip("When mode is At Or Above: Gas ON when effective angle >= this (degrees). When using absolute (below), Gas ON when |angle| >= this.")]
    public float gasOnAngleThresholdDegrees = 15f;

    [Tooltip("If true, Gas ON when effective angle >= threshold. If false, Gas ON when |effective angle| >= threshold. Only used when mode is At Or Above.")]
    public bool useSignedAngle = true;

    [Tooltip("When mode is Between: Gas ON when min <= angle <= max (effective degrees).")]
    public float gasOnAngleMinDegrees = 15f;

    [Tooltip("When mode is Between: inclusive upper bound.")]
    public float gasOnAngleMaxDegrees = 45f;

    [Header("Angle tuning")]
    [Tooltip("Added to raw signed angle before threshold check.")]
    public float angleOffsetDegrees = 0f;

    [Tooltip("If true, flip which direction increases the effective angle.")]
    public bool invertAngle = false;

    [Tooltip("Multiplies the angle after offset/invert (e.g. 0.5 for half sensitivity).")]
    public float angleScale = 1f;

    [Header("Output")]
    public TMP_Text statusText;
    public string textWhenGasOn = "Gas On";
    public string textWhenGasOff = "Gas Off";

    [Tooltip("If true, refresh text every frame so angle display updates.")]
    public bool showAngleInDisplay = true;

    [Tooltip("Format: {0} = status text, {1} = effective angle (degrees).")]
    public string statusFormat = "{0} | {1:F1}°";

    [Header("Physical rotation limits (optional)")]
    [Tooltip("If true, knob rotation is clamped each LateUpdate (effective angle space). Use after grab/physics.")]
    public bool enforcePhysicalRotationLimits = false;

    [Tooltip("Effective angle degrees (after tuning).")]
    public float physicalMinAngleDegrees = -90f;

    [Tooltip("Effective angle degrees (after tuning).")]
    public float physicalMaxAngleDegrees = 90f;

    [Tooltip("Optional: clears angular velocity when the rotation is clamped.")]
    public Rigidbody targetRigidbody;

    private Quaternion initialLocalRotation;
    private bool gasIsOn;

    /// <summary>True when gas is on per the current angle rules (for welding / safety interlocks).</summary>
    public bool IsGasOn() => gasIsOn;

    bool _forceGasOnForDebug;

    void Awake()
    {
        if (target == null)
            target = transform;
        if (targetRigidbody == null)
            targetRigidbody = target.GetComponent<Rigidbody>() ?? target.GetComponentInParent<Rigidbody>() ?? target.GetComponentInChildren<Rigidbody>();
    }

    void Start()
    {
        if (target == null)
            target = transform;

        initialLocalRotation = target.localRotation;
        gasIsOn = false;
        UpdateText();
    }

    void Update()
    {
        if (_forceGasOnForDebug)
        {
            if (!gasIsOn || showAngleInDisplay)
            {
                gasIsOn = true;
                UpdateText();
            }
            return;
        }

        float raw = AngleMappingHelper.GetSignedAngleDegrees(initialLocalRotation, target, localAxis);
        float signedAngle = AngleMappingHelper.ApplyAngleTuning(raw, angleOffsetDegrees, invertAngle, angleScale);
        bool next = EvaluateGasOn(signedAngle);

        if (next != gasIsOn || showAngleInDisplay)
        {
            gasIsOn = next;
            UpdateText(signedAngle);
        }
    }

    /// <summary>Debug/test: force Gas ON (rotates the knob into an ON angle when possible).</summary>
    public void ForceGasOnForDebug()
    {
        _forceGasOnForDebug = true;
        gasIsOn = true;

        if (target != null)
        {
            float desiredEffective;
            if (gasOnAngleMode == GasOnAngleMode.BetweenAngles)
                desiredEffective = 0.5f * (gasOnAngleMinDegrees + gasOnAngleMaxDegrees);
            else
                desiredEffective = gasOnAngleThresholdDegrees + 10f;

            float scale = Mathf.Abs(angleScale) < 0.0001f ? 1f : angleScale;
            float raw = desiredEffective / scale;
            if (invertAngle)
                raw = -raw;
            raw -= angleOffsetDegrees;

            Vector3 axis = localAxis.sqrMagnitude > 0.0001f ? localAxis.normalized : Vector3.right;
            target.localRotation = initialLocalRotation * Quaternion.AngleAxis(raw, axis);

            if (targetRigidbody != null)
            {
                targetRigidbody.angularVelocity = Vector3.zero;
                targetRigidbody.velocity = Vector3.zero;
            }
        }

        UpdateText();
    }

    bool EvaluateGasOn(float effectiveAngle)
    {
        if (gasOnAngleMode == GasOnAngleMode.BetweenAngles)
        {
            float lo = Mathf.Min(gasOnAngleMinDegrees, gasOnAngleMaxDegrees);
            float hi = Mathf.Max(gasOnAngleMinDegrees, gasOnAngleMaxDegrees);
            return effectiveAngle >= lo && effectiveAngle <= hi;
        }

        if (useSignedAngle)
            return effectiveAngle >= gasOnAngleThresholdDegrees;
        return Mathf.Abs(effectiveAngle) >= gasOnAngleThresholdDegrees;
    }

    void UpdateText(float effectiveAngleDegrees = float.NaN)
    {
        if (statusText == null)
            return;

        string status = gasIsOn ? textWhenGasOn : textWhenGasOff;

        if (float.IsNaN(effectiveAngleDegrees))
        {
            float raw = AngleMappingHelper.GetSignedAngleDegrees(initialLocalRotation, target, localAxis);
            effectiveAngleDegrees = AngleMappingHelper.ApplyAngleTuning(raw, angleOffsetDegrees, invertAngle, angleScale);
        }

        statusText.text = AngleMappingHelper.FormatValueAndAngle(statusFormat, showAngleInDisplay, status, effectiveAngleDegrees);
    }

    void LateUpdate()
    {
        if (!enforcePhysicalRotationLimits)
            return;

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

    /// <summary>Wire a UI Button to flip invert at runtime.</summary>
    public void ToggleInvertAngle()
    {
        invertAngle = !invertAngle;
    }

    public void SetInvertAngle(bool value)
    {
        invertAngle = value;
    }
}
