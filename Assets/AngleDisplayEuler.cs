using UnityEngine;
using UnityEngine.Serialization;
using TMPro;

/// <summary>
/// Reads world rotation as pitch (x), yaw (y), roll (z). Displays each axis in signed degrees
/// (typically −180…180) instead of 0–360. Acceptable ranges are set as inclusive min/max in that
/// same signed space (easier than wrap bands near 0°).
/// </summary>
public class AngleDisplayEuler : MonoBehaviour
{
    [Header("Target")]
    [Tooltip("Object whose orientation you want to read (e.g. MIG gun).")]
    public Transform target;

    [Header("UI")]
    [Tooltip("'gun straight' / 'straighten gun'. Assign a TMP in the canvas where you want this message.")]
    public TMP_Text messageText;

    [Tooltip("Optional: Pitch/Yaw/Roll readout on a separate TMP (e.g. corner of screen).")]
    public TMP_Text eulerText;

    [Tooltip("Legacy: if both messageText and eulerText are null, status ± euler are written here.")]
    public TMP_Text angleText;

    [Tooltip("If true, write Pitch/Yaw/Roll to eulerText (or legacy angleText when eulerText is null).")]
    public bool showEulerAngles = true;

    [Header("Pitch (signed degrees, inclusive)")]
    [Tooltip("Minimum acceptable pitch after converting to signed ° (same numbers you see on screen).")]
    [FormerlySerializedAs("pitchMinDegrees")]
    public float pitchMinSigned = 28f;

    [Tooltip("Maximum acceptable pitch (signed °).")]
    [FormerlySerializedAs("pitchMaxDegrees")]
    public float pitchMaxSigned = 55f;

    [Header("Yaw (signed degrees, inclusive)")]
    [Tooltip("Minimum acceptable yaw (e.g. -8 for ±8° around level).")]
    public float yawMinSigned = -8f;

    [Tooltip("Maximum acceptable yaw (signed °).")]
    public float yawMaxSigned = 8f;

    [Header("Roll (signed degrees, inclusive)")]
    [Tooltip("Minimum acceptable roll (e.g. -20 for ±20°).")]
    public float rollMinSigned = -20f;

    [Tooltip("Maximum acceptable roll (signed °).")]
    public float rollMaxSigned = 20f;

    [Header("Messages")]
    [Tooltip("Shown when pitch, yaw, and roll are all within range.")]
    public string textWhenGunStraight = "gun straight";

    [Tooltip("Shown when pitch, yaw, or roll is out of range.")]
    public string textWhenStraightenGun = "straighten gun";

    void Update()
    {
        if (target == null)
            return;

        if (messageText == null && eulerText == null && angleText == null)
            return;

        Vector3 euler = target.rotation.eulerAngles;

        float pitch = EulerToSigned180(euler.x);
        float yaw = EulerToSigned180(euler.y);
        float roll = EulerToSigned180(euler.z);

        bool pitchOk = IsInRange(pitch, pitchMinSigned, pitchMaxSigned);
        bool yawOk = IsInRange(yaw, yawMinSigned, yawMaxSigned);
        bool rollOk = IsInRange(roll, rollMinSigned, rollMaxSigned);

        bool gunStraight = pitchOk && yawOk && rollOk;

        string status = gunStraight ? textWhenGunStraight : textWhenStraightenGun;

        string eulerBlock =
            $"Pitch: {pitch:F1}°\n" +
            $"Yaw:   {yaw:F1}°\n" +
            $"Roll:  {roll:F1}°";

        if (messageText != null)
            messageText.text = status;

        if (eulerText != null && showEulerAngles)
            eulerText.text = eulerBlock;

        // Legacy single field: only when the new split fields are not used
        if (messageText == null && eulerText == null && angleText != null)
        {
            angleText.text = showEulerAngles ? status + "\n" + eulerBlock : status;
        }
        else if (messageText != null && eulerText == null && angleText != null && showEulerAngles)
        {
            // Message on messageText; euler still on legacy angleText if user didn't add eulerText
            angleText.text = eulerBlock;
        }
    }

    /// <summary>
    /// Same rule as the on-screen status: true when pitch, yaw, and roll are all within the configured bands
    /// (i.e. when <see cref="messageText"/> would show <see cref="textWhenGunStraight"/>).
    /// </summary>
    public bool IsGunStraight()
    {
        if (target == null)
            return false;

        Vector3 euler = target.rotation.eulerAngles;
        float pitch = EulerToSigned180(euler.x);
        float yaw = EulerToSigned180(euler.y);
        float roll = EulerToSigned180(euler.z);

        return IsInRange(pitch, pitchMinSigned, pitchMaxSigned)
            && IsInRange(yaw, yawMinSigned, yawMaxSigned)
            && IsInRange(roll, rollMinSigned, rollMaxSigned);
    }

    /// <summary>
    /// Converts a Unity euler angle (any real value) to signed degrees in (−180, 180],
    /// so display matches “negative angles” instead of 0–360.
    /// </summary>
    public static float EulerToSigned180(float eulerDegrees)
    {
        float a = eulerDegrees % 360f;
        if (a < 0f)
            a += 360f;
        if (a > 180f)
            a -= 360f;
        return a;
    }

    static bool IsInRange(float value, float minInclusive, float maxInclusive)
    {
        if (minInclusive <= maxInclusive)
            return value >= minInclusive && value <= maxInclusive;
        // Allow inverted inspector (max < min) by swapping
        return value >= maxInclusive && value <= minInclusive;
    }

    /// <summary>Maps any angle to [0, 360). Kept for callers that still need 0–360.</summary>
    public static float Normalize360(float degrees)
    {
        degrees %= 360f;
        if (degrees < 0f)
            degrees += 360f;
        return degrees;
    }
}
