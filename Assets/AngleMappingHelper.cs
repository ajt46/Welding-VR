using System;
using System.Text.RegularExpressions;
using UnityEngine;

/// <summary>
/// Maps a discrete output value (wire speed, volts, etc.) to an inclusive angle range [min, max].
/// </summary>
[Serializable]
public struct AngleOutputBracket
{
    public float outputValue;
    public float minAngleDegrees;
    public float maxAngleDegrees;
}

/// <summary>
/// Shared helpers for angle-to-value scripts: signed local angle, tuning, display.
/// </summary>
public static class AngleMappingHelper
{
    /// <summary>
    /// Squared distance from <paramref name="angle"/> to the closed interval [lo, hi].
    /// </summary>
    public static float DistanceToClosedInterval(float angle, float lo, float hi)
    {
        if (lo > hi)
        {
            float t = lo;
            lo = hi;
            hi = t;
        }

        if (angle < lo)
            return lo - angle;
        if (angle > hi)
            return angle - hi;
        return 0f;
    }

    /// <summary>
    /// If angle lies in a bracket (inclusive), returns that bracket's output and updates <paramref name="lastOutput"/>.
    /// If in a gap: returns <paramref name="lastOutput"/> when <paramref name="holdLastOutputWhenInGap"/> is true,
    /// otherwise the output from the nearest bracket by angle distance.
    /// </summary>
    public static float ResolveOutputFromBrackets(
        float angle,
        AngleOutputBracket[] brackets,
        bool holdLastOutputWhenInGap,
        ref float lastOutput,
        float fallbackWhenEmpty)
    {
        if (brackets == null || brackets.Length == 0)
            return fallbackWhenEmpty;

        for (int i = 0; i < brackets.Length; i++)
        {
            float lo = Mathf.Min(brackets[i].minAngleDegrees, brackets[i].maxAngleDegrees);
            float hi = Mathf.Max(brackets[i].minAngleDegrees, brackets[i].maxAngleDegrees);
            if (angle >= lo && angle <= hi)
            {
                lastOutput = brackets[i].outputValue;
                return lastOutput;
            }
        }

        if (holdLastOutputWhenInGap)
            return lastOutput;

        float bestOut = brackets[0].outputValue;
        float bestDist = float.MaxValue;
        for (int i = 0; i < brackets.Length; i++)
        {
            float lo = Mathf.Min(brackets[i].minAngleDegrees, brackets[i].maxAngleDegrees);
            float hi = Mathf.Max(brackets[i].minAngleDegrees, brackets[i].maxAngleDegrees);
            float d = DistanceToClosedInterval(angle, lo, hi);
            if (d < bestDist - 1e-6f)
            {
                bestDist = d;
                bestOut = brackets[i].outputValue;
            }
        }

        lastOutput = bestOut;
        return bestOut;
    }

    /// <summary>Output of the bracket whose interval is closest to <paramref name="angle"/>.</summary>
    public static float ResolveNearestBracketOutput(float angle, AngleOutputBracket[] brackets, float fallbackWhenEmpty)
    {
        if (brackets == null || brackets.Length == 0)
            return fallbackWhenEmpty;

        float bestOut = brackets[0].outputValue;
        float bestDist = float.MaxValue;
        for (int i = 0; i < brackets.Length; i++)
        {
            float lo = Mathf.Min(brackets[i].minAngleDegrees, brackets[i].maxAngleDegrees);
            float hi = Mathf.Max(brackets[i].minAngleDegrees, brackets[i].maxAngleDegrees);
            float d = DistanceToClosedInterval(angle, lo, hi);
            if (d < bestDist - 1e-6f)
            {
                bestDist = d;
                bestOut = brackets[i].outputValue;
            }
        }

        return bestOut;
    }
    static readonly Regex StripAnglePlaceholder = new Regex(@"\s*\|\s*\{1[^}]*\}", RegexOptions.Compiled);

    /// <summary>
    /// Formats value + optional angle. Old scenes often keep a single-placeholder format; two-arg string.Format then throws.
    /// This catches that and appends the angle. When angle is hidden, strips <c>| {1...}</c> so one-arg format works.
    /// </summary>
    public static string FormatValueAndAngle(string format, bool showAngle, object valueObj, float angleDegrees)
    {
        if (string.IsNullOrEmpty(format))
            format = "{0}";

        if (!showAngle)
        {
            try
            {
                return string.Format(format, valueObj);
            }
            catch (FormatException)
            {
                string trimmed = StripAnglePlaceholder.Replace(format, "");
                try
                {
                    return string.Format(trimmed, valueObj);
                }
                catch (FormatException)
                {
                    return valueObj != null ? valueObj.ToString() : string.Empty;
                }
            }
        }

        try
        {
            return string.Format(format, valueObj, angleDegrees);
        }
        catch (FormatException)
        {
            try
            {
                string main = string.Format(format, valueObj);
                return main + " | " + angleDegrees.ToString("F1") + "°";
            }
            catch (FormatException)
            {
                return (valueObj != null ? valueObj.ToString() : string.Empty) + " | " + angleDegrees.ToString("F1") + "°";
            }
        }
    }
    public static float GetSignedAngleDegrees(Quaternion initialLocalRotation, Transform target, Vector3 localAxis)
    {
        if (target == null)
            return 0f;

        Quaternion relative = Quaternion.Inverse(initialLocalRotation) * target.localRotation;
        Vector3 euler = relative.eulerAngles;

        float raw;
        if (localAxis == Vector3.right)
            raw = euler.x;
        else if (localAxis == Vector3.up)
            raw = euler.y;
        else
            raw = euler.z;

        if (raw > 180f)
            raw -= 360f;

        return raw;
    }

    /// <summary>Apply offset, optional invert, and scale to raw angle.</summary>
    public static float ApplyAngleTuning(float rawDegrees, float angleOffsetDegrees, bool invertAngle, float angleScale)
    {
        float a = rawDegrees + angleOffsetDegrees;
        if (invertAngle)
            a = -a;
        a *= angleScale;
        return a;
    }
}
