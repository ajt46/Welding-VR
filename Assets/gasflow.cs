using System;
using UnityEngine;
using TMPro;

/// <summary>
/// Maps knob angle to gas flow 0–50 (same scale as max pressure), in steps of 5.
/// Initial flow at reference angle is 0; increasing angle increases flow.
/// </summary>
public class gasflow : MonoBehaviour
{
    [Header("Target")]
    public Transform target;

    [Header("Angle range")]
    public Vector3 localAxis = Vector3.right;

    [Tooltip("Effective angle (degrees) at which flow reaches max.")]
    public float maxAngleDegrees = 90f;

    [Tooltip("Effective angle treated as zero flow (usually 0).")]
    public float minAngleDegrees = 0f;

    [Header("Angle tuning")]
    public float angleOffsetDegrees = 0f;
    public bool invertAngle = false;
    [Tooltip("Multiplies effective angle span (after offset/invert).")]
    public float angleScale = 1f;

    [Header("Flow")]
    [Tooltip("Maximum flow value (matches your 0–50 scale).")]
    public float maxFlow = 50f;

    [Tooltip("Output steps (e.g. 5).")]
    public float flowStep = 5f;

    [Header("Ideal / reference")]
    [Tooltip("Target gas flow for this knob (same units as readout). Used when no WeldingPanel is assigned below, or as fallback.")]
    public float idealGasFlow = 25f;

    [Tooltip("Optional: read ideal gas flow from the active material on this panel (overrides Ideal Gas Flow while assigned).")]
    public WeldingPanel materialPanelForIdeal;

    [Header("Physical rotation limits (optional)")]
    [Tooltip("If true, knob rotation is clamped each LateUpdate (effective angle space). Use after grab/physics.")]
    public bool enforcePhysicalRotationLimits = false;

    [Tooltip("If true, physical min/max match min/max angle degrees for flow (below). If false, set physical limits manually.")]
    public bool derivePhysicalLimitsFromFlowAngles = true;

    [Tooltip("Effective angle degrees (after tuning). Used when derive is off.")]
    public float physicalMinAngleDegrees = 0f;

    [Tooltip("Effective angle degrees (after tuning). Used when derive is off.")]
    public float physicalMaxAngleDegrees = 90f;

    [Tooltip("Optional: clears angular velocity when the rotation is clamped.")]
    public Rigidbody targetRigidbody;

    [Header("Output")]
    public TMP_Text outputText;

    [Tooltip("{0} = current flow, {1} = effective angle (°). Optional {2} = ideal gas flow (set Ideal Gas Flow or assign Material Panel For Ideal).")]
    public string displayFormat = "{0:F0} | {1:F1}°";

    public bool showAngleInDisplay = true;

    private Quaternion initialLocalRotation;

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
        RefreshPhysicalLimitsFromFlowRange();
        UpdateDisplay();
    }

    void Update()
    {
        UpdateDisplay();
    }

    void LateUpdate()
    {
        if (!enforcePhysicalRotationLimits)
            return;

        RefreshPhysicalLimitsFromFlowRange();
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

    void RefreshPhysicalLimitsFromFlowRange()
    {
        if (derivePhysicalLimitsFromFlowAngles)
        {
            physicalMinAngleDegrees = minAngleDegrees;
            physicalMaxAngleDegrees = maxAngleDegrees;
        }
    }

    float GetEffectiveAngleDegrees()
    {
        float raw = AngleMappingHelper.GetSignedAngleDegrees(initialLocalRotation, target, localAxis);
        return AngleMappingHelper.ApplyAngleTuning(raw, angleOffsetDegrees, invertAngle, angleScale);
    }

    /// <summary>Ideal gas flow: from <see cref="materialPanelForIdeal"/> if set, otherwise <see cref="idealGasFlow"/>.</summary>
    public float GetIdealGasFlow()
    {
        if (materialPanelForIdeal != null)
            return materialPanelForIdeal.GetActiveMaterialTargets().idealGasFlow;
        return idealGasFlow;
    }

    /// <summary>Current gas flow value (same scale as the display).</summary>
    public float GetCurrentGasFlow()
    {
        float angle = GetEffectiveAngleDegrees();
        float span = Mathf.Max(maxAngleDegrees - minAngleDegrees, 0.001f);
        float t = Mathf.Clamp01((angle - minAngleDegrees) / span);
        float flow = t * maxFlow;

        if (flowStep > 0f)
            flow = Mathf.Round(flow / flowStep) * flowStep;

        return Mathf.Clamp(flow, 0f, maxFlow);
    }

    void UpdateDisplay()
    {
        float angle = GetEffectiveAngleDegrees();
        float flow = GetCurrentGasFlow();

        if (outputText == null)
            return;

        if (!string.IsNullOrEmpty(displayFormat) && displayFormat.Contains("{2}"))
        {
            float ideal = GetIdealGasFlow();
            try
            {
                outputText.text = string.Format(displayFormat, flow, angle, ideal);
            }
            catch (FormatException)
            {
                outputText.text = AngleMappingHelper.FormatValueAndAngle(displayFormat, showAngleInDisplay, flow, angle);
            }
        }
        else
            outputText.text = AngleMappingHelper.FormatValueAndAngle(displayFormat, showAngleInDisplay, flow, angle);
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
