using UnityEngine;
using TMPro;

public class angletopsi : MonoBehaviour
{
    [Header("Target")]
    [Tooltip("Object whose rotation controls the PSI (e.g. a knob). If left null, this object's transform is used.")]
    public Transform target;

    [Header("Angle → PSI Mapping")]
    [Tooltip("Local axis to measure angle around (typically Y for a knob).")]
    public Vector3 localAxis = Vector3.up;

    [Tooltip("Maximum usable angle (degrees) from the initial rotation that maps to maxPSI (after tuning).")]
    public float maxAngle = 100f;

    [Tooltip("Angle treated as 0 PSI (usually 0).")]
    public float minAngleForZeroPsi = 0f;

    [Tooltip("Maximum PSI when the knob is at maxAngle.")]
    public float maxPSI = 50f;

    [Tooltip("Round PSI to this step (e.g. 5 = 0,5,10,...).")]
    public float psiStep = 5f;

    [Header("Angle tuning")]
    public float angleOffsetDegrees = 0f;
    public bool invertAngle = false;
    public float angleScale = 1f;

    [Header("Output")]
    [Tooltip("TextMeshPro text to display the current PSI value.")]
    public TMP_Text psiText;

    [Tooltip("{0} = PSI, {1} = effective angle (degrees).")]
    public string displayFormat = "{0} PSI | {1:F1}°";

    public bool showAngleInDisplay = true;

    private Quaternion initialRotation;

    void Start()
    {
        if (target == null)
            target = transform;

        initialRotation = target.localRotation;
    }

    void Update()
    {
        if (target == null)
            return;

        float raw = AngleMappingHelper.GetSignedAngleDegrees(initialRotation, target, localAxis);
        float angle = AngleMappingHelper.ApplyAngleTuning(raw, angleOffsetDegrees, invertAngle, angleScale);

        float span = Mathf.Max(maxAngle - minAngleForZeroPsi, 0.001f);
        float clampedAngle = Mathf.Clamp(angle, minAngleForZeroPsi, maxAngle);

        float psi = ((clampedAngle - minAngleForZeroPsi) / span) * maxPSI;

        if (psiStep > 0f)
            psi = Mathf.Round(psi / psiStep) * psiStep;

        psi = Mathf.Clamp(psi, 0f, maxPSI);

        if (psiText != null)
            psiText.text = AngleMappingHelper.FormatValueAndAngle(displayFormat, showAngleInDisplay, psi, angle);
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
