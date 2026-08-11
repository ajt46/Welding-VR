using UnityEngine;
using TMPro;

/// <summary>
/// Wire speed from local X angle relative to the starting pose.
/// Angle 0 (at Start) = startSpeed. Higher angle → higher speed, lower angle → lower speed.
/// Read-only: does not change the transform.
/// </summary>
public class wirespeedrotation : MonoBehaviour
{
    [Header("Reference")]
    [Tooltip("Stores current local rotation at Start as angle 0.")]
    public bool captureInitialRotationOnStart = true;

    [Header("Speed from angle")]
    [Tooltip("Wire speed when the signed local X angle from reference is 0.")]
    public float startSpeed = 200f;

    [Tooltip("Speed change per 1° of signed local X rotation (positive angle → add speed).")]
    public float speedPerDegree = 2f;

    [Tooltip("Flip if your model increases speed when rotating the other way.")]
    public bool invertDirection = true;

    [Header("Optional")]
    [Tooltip("Clamp final speed to this range.")]
    public bool clampSpeed = true;

    public float speedMin = 50f;
    public float speedMax = 500f;

    [Tooltip("Round displayed speed (0 = no rounding).")]
    public float speedStep = 10f;

    [Header("Output")]
    public TMP_Text speedText;

    [Tooltip("{0} = speed, {1} = accumulated angle (degrees) used for speed.")]
    public string speedTextFormat = "{0:F0} | {1:F1}°";

    public bool showAngleInDisplay = true;

    private Quaternion initialLocalRotation;
    private float currentSpeed;
    private float lastRelativeX;
    private float accumulatedRelativeX;

    void Start()
    {
        if (captureInitialRotationOnStart)
            initialLocalRotation = transform.localRotation;

        lastRelativeX = GetSignedRelativeXAngle();
        accumulatedRelativeX = 0f;

        UpdateSpeedFromAngle();
        UpdateSpeedText();
    }

    void Update()
    {
        UpdateSpeedFromAngle();
        UpdateSpeedText();
    }

    /// <summary>Signed local X angle in degrees from the initial reference, wrapped to -180..180.</summary>
    float GetSignedRelativeXAngle()
    {
        Quaternion relative = Quaternion.Inverse(initialLocalRotation) * transform.localRotation;
        Vector3 euler = relative.eulerAngles;

        float xAngle = euler.x;
        if (xAngle > 180f)
            xAngle -= 360f;

        return xAngle;
    }

    void UpdateSpeedFromAngle()
    {
        // Unwrap angle continuously so values beyond 360° are stable.
        float currentRelativeX = GetSignedRelativeXAngle();
        float delta = Mathf.DeltaAngle(lastRelativeX, currentRelativeX);
        accumulatedRelativeX += delta;
        lastRelativeX = currentRelativeX;

        float angle = accumulatedRelativeX; // continuous angle from reference
        float dir = invertDirection ? -1f : 1f;
        float speed = startSpeed + angle * speedPerDegree * dir;

        if (clampSpeed)
            speed = Mathf.Clamp(speed, Mathf.Min(speedMin, speedMax), Mathf.Max(speedMin, speedMax));

        currentSpeed = speed;
    }

    void UpdateSpeedText()
    {
        if (speedText == null)
            return;

        float speed = currentSpeed;
        if (speedStep > 0f)
            speed = Mathf.Round(speed / speedStep) * speedStep;

        float displayAngle = accumulatedRelativeX * (invertDirection ? -1f : 1f);
        speedText.text = AngleMappingHelper.FormatValueAndAngle(speedTextFormat, showAngleInDisplay, speed, displayAngle);
    }

    public void ToggleInvertAngle()
    {
        invertDirection = !invertDirection;
    }

    public void SetInvertAngle(bool value)
    {
        invertDirection = value;
    }

    /// <summary>Use current pose as new angle 0; keeps current displayed speed as new startSpeed.</summary>
    public void RecalibrateReference()
    {
        initialLocalRotation = transform.localRotation;
        startSpeed = currentSpeed;
        lastRelativeX = GetSignedRelativeXAngle();
        accumulatedRelativeX = 0f;
    }
}
