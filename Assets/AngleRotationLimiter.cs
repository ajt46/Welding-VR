using UnityEngine;

/// <summary>
/// Static helpers to clamp knob rotation to min/max effective angles (same space as
/// <see cref="AngleMappingHelper.ApplyAngleTuning"/> output). Use from <see cref="MonoBehaviour.LateUpdate"/>
/// so limits apply after grab/physics scripts.
/// </summary>
public static class AngleRotationLimiter
{
    /// <summary>Inverse of <see cref="AngleMappingHelper.ApplyAngleTuning"/>.</summary>
    public static float InverseAngleTuningToRaw(
        float effectiveAngleDegrees,
        float angleOffsetDegrees,
        bool invertAngle,
        float angleScale)
    {
        if (Mathf.Abs(angleScale) < 1e-6f)
            return -angleOffsetDegrees;

        float u = effectiveAngleDegrees / angleScale;
        if (invertAngle)
            u = -u;
        return u - angleOffsetDegrees;
    }

    /// <summary>
    /// Union of all bracket angle intervals: smallest low, largest high (degrees, effective space).
    /// </summary>
    public static bool TryComputeBoundsFromBrackets(AngleOutputBracket[] brackets, out float minDeg, out float maxDeg)
    {
        minDeg = 0f;
        maxDeg = 0f;
        if (brackets == null || brackets.Length == 0)
            return false;

        float lo = float.MaxValue;
        float hi = float.MinValue;
        for (int i = 0; i < brackets.Length; i++)
        {
            float a = Mathf.Min(brackets[i].minAngleDegrees, brackets[i].maxAngleDegrees);
            float b = Mathf.Max(brackets[i].minAngleDegrees, brackets[i].maxAngleDegrees);
            lo = Mathf.Min(lo, a);
            hi = Mathf.Max(hi, b);
        }

        minDeg = lo;
        maxDeg = hi;
        return true;
    }

    /// <summary>
    /// If effective angle is outside [physicalMin, physicalMax], sets <paramref name="target"/> rotation
    /// so the angle (after tuning) stays inside. Clears <paramref name="rigidbody"/> angular velocity when clamping.
    /// </summary>
    /// <returns>True if the transform was corrected.</returns>
    public static bool EnforceRotationLimits(
        Transform target,
        Quaternion initialLocalRotation,
        Vector3 localAxis,
        float physicalMinDegrees,
        float physicalMaxDegrees,
        float angleOffsetDegrees,
        bool invertAngle,
        float angleScale,
        Rigidbody rigidbody = null,
        float epsilonDegrees = 0.01f)
    {
        if (target == null)
            return false;

        float raw = AngleMappingHelper.GetSignedAngleDegrees(initialLocalRotation, target, localAxis);
        float eff = AngleMappingHelper.ApplyAngleTuning(raw, angleOffsetDegrees, invertAngle, angleScale);
        float lo = Mathf.Min(physicalMinDegrees, physicalMaxDegrees);
        float hi = Mathf.Max(physicalMinDegrees, physicalMaxDegrees);
        float effClamped = Mathf.Clamp(eff, lo, hi);

        if (Mathf.Abs(eff - effClamped) < epsilonDegrees)
            return false;

        float rawDesired = InverseAngleTuningToRaw(effClamped, angleOffsetDegrees, invertAngle, angleScale);
        Vector3 axis = localAxis.sqrMagnitude > 1e-8f ? localAxis.normalized : Vector3.right;
        target.localRotation = initialLocalRotation * Quaternion.AngleAxis(rawDesired, axis);

        if (rigidbody != null)
            rigidbody.angularVelocity = Vector3.zero;

        return true;
    }
}
