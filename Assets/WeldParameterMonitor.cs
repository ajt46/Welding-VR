using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;

/// <summary>
/// Compares voltage, wire speed, gas flow (vs panel <see cref="WeldingPanel.MaterialWeldTargets.idealGasFlow"/>), gun angle,
/// and tip-on-surface contact to the active <see cref="WeldingPanel"/> material targets.
/// With <see cref="strictAllMaterialParametersForEvaluation"/> (default), all of those are required; missing knob references fail.
/// When <see cref="badWeldOnlyFromSpeedEvaluator"/> is true (default), <see cref="badweld"/> is driven only by
/// <see cref="WeldSpeedEvaluator"/> during an X1–X2 run (same path as travel-speed evaluation).
/// </summary>
public class WeldParameterMonitor : MonoBehaviour
{
    [Header("Welding")]
    public CustomWeldingController weldingController;

    [Tooltip("Leave null to skip that dimension in checks.")]
    public angletovolt voltageKnob;

    [Tooltip("Leave null to skip that dimension in checks.")]
    public angletowirespeed wireSpeedKnob;

    [Tooltip("Leave null to skip that dimension in checks.")]
    public gasflow gasFlowKnob;

    [Tooltip("Used only if WeldingPanel.angleDisplay is null: tip forward vs panel normal. Leave null to skip surface-normal angle check.")]
    public Transform weldingTip;

    [Header("Bad weld feedback")]
    public badweld badWeldIndicator;

    [Tooltip("If true, bad weld visibility is updated only by WeldSpeedEvaluator during X1–X2 (not by general trigger strokes on the panel).")]
    public bool badWeldOnlyFromSpeedEvaluator = true;

    [Tooltip("If true, evaluation always requires: tip raycast on weldable surface, voltage, wire speed, gas flow vs WeldingPanel ideal/tolerance, and gun angle (AngleDisplayEuler on the panel, or WeldingTip vs weld face here). Unassigned voltage/wire/gas/tip counts as failed.")]
    public bool strictAllMaterialParametersForEvaluation = true;

    [Header("Debug UI")]
    [Tooltip("Optional TextMeshPro: live material, each parameter vs target, and Material Settings met when all checks pass.")]
    public TMP_Text parameterStatusText;

    [Tooltip("Shown on the last line when every assigned parameter is within tolerance.")]
    public string allParametersMetMessage = "Material Settings met";

    readonly StringBuilder _statusBuilder = new StringBuilder(512);

    bool _inputWasActive;
    bool _hadBadParametersThisStroke;
    bool _hadWeldingBlobThisStroke;

    void Update()
    {
        if (weldingController == null)
            return;

        bool inputActive = weldingController.IsTriggerHeldForWelding() && weldingController.AreWeldingPrerequisitesMet();

        if (!badWeldOnlyFromSpeedEvaluator)
        {
            if (inputActive && !_inputWasActive)
                OnStrokeStart();

            if (!inputActive && _inputWasActive)
                OnStrokeEnd();

            _inputWasActive = inputActive;

            if (inputActive && weldingController.IsWeldableSurfaceUnderTip())
                EvaluateWhileWelding();

            if (inputActive && weldingController.IsWelding())
                _hadWeldingBlobThisStroke = true;
        }

        UpdateParameterStatusText();
    }

    void OnStrokeStart()
    {
        _hadBadParametersThisStroke = false;
        _hadWeldingBlobThisStroke = false;
        if (badWeldIndicator != null)
            badWeldIndicator.HideBad();
    }

    void OnStrokeEnd()
    {
        // Legacy: no longer used for speed eval (kept for old scenes with badWeldOnlyFromSpeedEvaluator off).
    }

    void EvaluateWhileWelding()
    {
        WeldingPanel panel = weldingController.CurrentWeldingPanelUnderTip;
        if (panel == null)
            return;

        bool allMet = EvaluateParameters(panel, _statusBuilder, out int checkCount);
        if (checkCount == 0)
            return;

        if (badWeldIndicator != null)
        {
            if (allMet)
                badWeldIndicator.HideBad();
            else
            {
                _hadBadParametersThisStroke = true;
                badWeldIndicator.ShowBad();
            }
        }
        else if (!allMet)
        {
            _hadBadParametersThisStroke = true;
        }
    }

    /// <summary>Hides bad-weld feedback when a new X1–X2 run starts (call from WeldSpeedEvaluator).</summary>
    public void HideBadWeldForSpeedRun()
    {
        if (badWeldIndicator != null)
            badWeldIndicator.HideBad();
    }

    /// <summary>Show bad-weld feedback when material parameters failed during the X1–X2 run.</summary>
    public void ShowBadWeldAfterFailedParameters()
    {
        if (badWeldIndicator != null)
            badWeldIndicator.ShowBad();
    }

    /// <summary>
    /// For each parameter that is currently out of tolerance, adds a short label (e.g. "Voltage", "Wire speed").
    /// Call each frame during X1–X2 to accumulate everything that was wrong at any moment.
    /// </summary>
    public void MergeFailuresForPanel(WeldingPanel panel, HashSet<string> accumulated)
    {
        if (panel == null || accumulated == null)
            return;

        if (strictAllMaterialParametersForEvaluation)
            MergeFailuresStrict(panel, accumulated);
        else
            MergeFailuresOptional(panel, accumulated);
    }

    void MergeFailuresStrict(WeldingPanel panel, HashSet<string> accumulated)
    {
        WeldingPanel.MaterialWeldTargets t = panel.GetActiveMaterialTargets();

        if (weldingController == null || !weldingController.IsWeldableSurfaceUnderTip())
            accumulated.Add("Tip on surface");

        if (voltageKnob == null || Mathf.Abs(voltageKnob.GetCurrentVoltage() - t.idealVoltage) > t.voltageTolerance)
            accumulated.Add("Voltage");

        if (wireSpeedKnob == null || Mathf.Abs(wireSpeedKnob.GetCurrentWireSpeed() - t.idealWireSpeed) > t.wireSpeedTolerance)
            accumulated.Add("Wire speed");

        if (gasFlowKnob == null || Mathf.Abs(gasFlowKnob.GetCurrentGasFlow() - t.idealGasFlow) > t.gasFlowTolerance)
            accumulated.Add("Gas flow");

        if (panel.angleDisplay != null)
        {
            if (!panel.angleDisplay.IsGunStraight())
                accumulated.Add("Gun angle");
        }
        else if (weldingTip != null)
        {
            Vector3 inward = -panel.GetWeldFaceNormalWorld();
            float angle = Vector3.Angle(weldingTip.forward, inward);
            if (Mathf.Abs(angle - t.idealGunToSurfaceAngleDegrees) > t.workAngleToleranceDegrees)
                accumulated.Add("Work angle");
        }
        else
            accumulated.Add("Gun angle");
    }

    void MergeFailuresOptional(WeldingPanel panel, HashSet<string> accumulated)
    {
        WeldingPanel.MaterialWeldTargets t = panel.GetActiveMaterialTargets();

        if (voltageKnob != null && Mathf.Abs(voltageKnob.GetCurrentVoltage() - t.idealVoltage) > t.voltageTolerance)
            accumulated.Add("Voltage");

        if (wireSpeedKnob != null && Mathf.Abs(wireSpeedKnob.GetCurrentWireSpeed() - t.idealWireSpeed) > t.wireSpeedTolerance)
            accumulated.Add("Wire speed");

        if (gasFlowKnob != null && Mathf.Abs(gasFlowKnob.GetCurrentGasFlow() - t.idealGasFlow) > t.gasFlowTolerance)
            accumulated.Add("Gas flow");

        if (panel.angleDisplay != null)
        {
            if (!panel.angleDisplay.IsGunStraight())
                accumulated.Add("Gun angle");
        }
        else if (weldingTip != null)
        {
            Vector3 inward = -panel.GetWeldFaceNormalWorld();
            float angle = Vector3.Angle(weldingTip.forward, inward);
            if (Mathf.Abs(angle - t.idealGunToSurfaceAngleDegrees) > t.workAngleToleranceDegrees)
                accumulated.Add("Work angle");
        }
    }

    /// <summary>Same as <see cref="EvaluateParameters(WeldingPanel, StringBuilder, out int)"/> using an internal buffer.</summary>
    public bool EvaluateParameters(WeldingPanel panel, out int checkCount)
    {
        return EvaluateParameters(panel, _statusBuilder, out checkCount);
    }

    /// <summary>
    /// Returns true if all evaluated parameters pass. <paramref name="checkCount"/> is how many dimensions were evaluated
    /// (0 if optional mode and none assigned; strict mode always evaluates five dimensions when the panel is valid).
    /// </summary>
    public bool EvaluateParameters(WeldingPanel panel, StringBuilder sb, out int checkCount)
    {
        sb.Clear();
        checkCount = 0;
        bool allPass = true;

        WeldingPanel.MaterialWeldTargets t = panel.GetActiveMaterialTargets();

        sb.Append("Material: ").Append(panel.GetPanelMaterial()).Append('\n');

        if (strictAllMaterialParametersForEvaluation)
        {
            checkCount++;
            bool tipOk = weldingController != null && weldingController.IsWeldableSurfaceUnderTip();
            if (!tipOk)
                allPass = false;
            sb.Append("Tip on surface: ").Append(tipOk ? "contact" : "no contact").Append("  ").AppendLine(tipOk ? "✓" : "✗");

            checkCount++;
            if (voltageKnob == null)
            {
                allPass = false;
                sb.AppendLine("Voltage: (not assigned)  ✗");
            }
            else
            {
                float v = voltageKnob.GetCurrentVoltage();
                bool ok = Mathf.Abs(v - t.idealVoltage) <= t.voltageTolerance;
                if (!ok)
                    allPass = false;
                sb.Append("Voltage: ").Append(v.ToString("F1")).Append(" V  (target ").Append(t.idealVoltage.ToString("F1"))
                    .Append(" ±").Append(t.voltageTolerance.ToString("F1")).Append(")  ").AppendLine(ok ? "✓" : "✗");
            }

            checkCount++;
            if (wireSpeedKnob == null)
            {
                allPass = false;
                sb.AppendLine("Wire speed: (not assigned)  ✗");
            }
            else
            {
                float w = wireSpeedKnob.GetCurrentWireSpeed();
                bool ok = Mathf.Abs(w - t.idealWireSpeed) <= t.wireSpeedTolerance;
                if (!ok)
                    allPass = false;
                sb.Append("Wire speed: ").Append(w.ToString("F0")).Append("  (target ").Append(t.idealWireSpeed.ToString("F0"))
                    .Append(" ±").Append(t.wireSpeedTolerance.ToString("F0")).Append(")  ").AppendLine(ok ? "✓" : "✗");
            }

            checkCount++;
            if (gasFlowKnob == null)
            {
                allPass = false;
                sb.AppendLine("Gas flow: (not assigned)  ✗");
            }
            else
            {
                float g = gasFlowKnob.GetCurrentGasFlow();
                bool ok = Mathf.Abs(g - t.idealGasFlow) <= t.gasFlowTolerance;
                if (!ok)
                    allPass = false;
                sb.Append("Gas flow: ").Append(g.ToString("F0")).Append("  (target ").Append(t.idealGasFlow.ToString("F0"))
                    .Append(" ±").Append(t.gasFlowTolerance.ToString("F0")).Append(")  ").AppendLine(ok ? "✓" : "✗");
            }

            checkCount++;
            if (panel.angleDisplay != null)
            {
                bool ok = panel.angleDisplay.IsGunStraight();
                if (!ok)
                    allPass = false;
                string status = ok ? panel.angleDisplay.textWhenGunStraight : panel.angleDisplay.textWhenStraightenGun;
                sb.Append("Gun angle (Euler): ").Append(status).Append("  ").AppendLine(ok ? "✓" : "✗");
            }
            else if (weldingTip != null)
            {
                Vector3 inward = -panel.GetWeldFaceNormalWorld();
                float angle = Vector3.Angle(weldingTip.forward, inward);
                bool ok = Mathf.Abs(angle - t.idealGunToSurfaceAngleDegrees) <= t.workAngleToleranceDegrees;
                if (!ok)
                    allPass = false;
                sb.Append("Work angle (tip vs surface): ").Append(angle.ToString("F1")).Append("°  (target ").Append(t.idealGunToSurfaceAngleDegrees.ToString("F1"))
                    .Append(" ±").Append(t.workAngleToleranceDegrees.ToString("F1")).Append("°)  ").AppendLine(ok ? "✓" : "✗");
            }
            else
            {
                allPass = false;
                sb.AppendLine("Gun angle: (assign AngleDisplayEuler on panel or WeldingTip here)  ✗");
            }
        }
        else
        {
            if (voltageKnob != null)
            {
                checkCount++;
                float v = voltageKnob.GetCurrentVoltage();
                bool ok = Mathf.Abs(v - t.idealVoltage) <= t.voltageTolerance;
                if (!ok)
                    allPass = false;
                sb.Append("Voltage: ").Append(v.ToString("F1")).Append(" V  (target ").Append(t.idealVoltage.ToString("F1"))
                    .Append(" ±").Append(t.voltageTolerance.ToString("F1")).Append(")  ").AppendLine(ok ? "✓" : "✗");
            }

            if (wireSpeedKnob != null)
            {
                checkCount++;
                float w = wireSpeedKnob.GetCurrentWireSpeed();
                bool ok = Mathf.Abs(w - t.idealWireSpeed) <= t.wireSpeedTolerance;
                if (!ok)
                    allPass = false;
                sb.Append("Wire speed: ").Append(w.ToString("F0")).Append("  (target ").Append(t.idealWireSpeed.ToString("F0"))
                    .Append(" ±").Append(t.wireSpeedTolerance.ToString("F0")).Append(")  ").AppendLine(ok ? "✓" : "✗");
            }

            if (gasFlowKnob != null)
            {
                checkCount++;
                float g = gasFlowKnob.GetCurrentGasFlow();
                bool ok = Mathf.Abs(g - t.idealGasFlow) <= t.gasFlowTolerance;
                if (!ok)
                    allPass = false;
                sb.Append("Gas flow: ").Append(g.ToString("F0")).Append("  (target ").Append(t.idealGasFlow.ToString("F0"))
                    .Append(" ±").Append(t.gasFlowTolerance.ToString("F0")).Append(")  ").AppendLine(ok ? "✓" : "✗");
            }

            if (panel.angleDisplay != null)
            {
                checkCount++;
                bool ok = panel.angleDisplay.IsGunStraight();
                if (!ok)
                    allPass = false;
                string status = ok ? panel.angleDisplay.textWhenGunStraight : panel.angleDisplay.textWhenStraightenGun;
                sb.Append("Gun angle (Euler): ").Append(status).Append("  ").AppendLine(ok ? "✓" : "✗");
            }
            else if (weldingTip != null)
            {
                checkCount++;
                Vector3 inward = -panel.GetWeldFaceNormalWorld();
                float angle = Vector3.Angle(weldingTip.forward, inward);
                bool ok = Mathf.Abs(angle - t.idealGunToSurfaceAngleDegrees) <= t.workAngleToleranceDegrees;
                if (!ok)
                    allPass = false;
                sb.Append("Work angle (tip vs surface): ").Append(angle.ToString("F1")).Append("°  (target ").Append(t.idealGunToSurfaceAngleDegrees.ToString("F1"))
                    .Append(" ±").Append(t.workAngleToleranceDegrees.ToString("F1")).Append("°)  ").AppendLine(ok ? "✓" : "✗");
            }

            if (checkCount == 0)
            {
                sb.Append("(No parameters: assign voltage / wire / gas / tip in WeldParameterMonitor.)");
                return false;
            }
        }

        sb.Append('\n');
        if (allPass)
            sb.Append(allParametersMetMessage);
        else
            sb.Append("Adjust settings to match material targets.");

        return allPass;
    }

    void UpdateParameterStatusText()
    {
        if (parameterStatusText == null || weldingController == null)
            return;

        if (!weldingController.IsWeldableSurfaceUnderTip())
        {
            parameterStatusText.text = "Aim tip at WeldingPanel surface to see parameters.";
            return;
        }

        WeldingPanel panel = weldingController.CurrentWeldingPanelUnderTip;
        if (panel == null)
        {
            parameterStatusText.text = "Hit surface has no WeldingPanel.";
            return;
        }

        EvaluateParameters(panel, _statusBuilder, out _);
        parameterStatusText.text = _statusBuilder.ToString();
    }
}
