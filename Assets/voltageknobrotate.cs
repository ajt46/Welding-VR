using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

public class voltageknobrotate : MonoBehaviour
{
    private float currentRotationY = 0f;
    private float rotationIncrement = 10f; // 10 degrees per increment
    private bool isDragging = false;
    private Vector3 lastMousePosition;

    [Header("Angle Limits (degrees)")]
    [Tooltip("Minimum allowed angle from the starting pose (negative, along X).")]
    public float minAngleX = -90f;
    [Tooltip("Maximum allowed angle from the starting pose (positive, along X).")]
    public float maxAngleX = 0f;

    private Quaternion initialLocalRotation;

    [Header("Voltage Mapping")]
    [Tooltip("Minimum voltage when the knob is at maxAngleX (usually 0 degrees).")]
    public float voltageMin = 18f;

    [Tooltip("Maximum voltage when the knob is at minAngleX (usually negative).")]
    public float voltageMax = 24f;

    [Tooltip("Round the displayed voltage to this step (e.g. 0.5). Set 0 to disable rounding.")]
    public float voltageStep = 0.5f;

    [Header("Output")]
    [Tooltip("TextMeshPro text to display the current voltage.")]
    public TMP_Text voltageText;

    [Tooltip("Format used when writing to the text (use {0} for the numeric value).")]
    public string voltageTextFormat = "{0:F1}V";

    // Start is called before the first frame update
    void Start()
    {
        // Initialize rotation to current local Y rotation
        currentRotationY = transform.localEulerAngles.y;

        // Store starting pose as 0� reference for X clamping
        initialLocalRotation = transform.localRotation;
    }

    // Update is called once per frame
    void Update()
    {
        // Handle mouse input
        if (Input.GetMouseButtonDown(0))
        {
            // Check if clicking on this object
            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            RaycastHit hit;
            
            if (Physics.Raycast(ray, out hit))
            {
                if (hit.collider.gameObject == gameObject)
                {
                    isDragging = true;
                    lastMousePosition = Input.mousePosition;
                }
            }
        }
        
        if (Input.GetMouseButtonUp(0))
        {
            if (isDragging)
            {
                // Snap to nearest 10-degree increment when releasing
                SnapToIncrement();
                isDragging = false;
            }
        }
        
        if (isDragging && Input.GetMouseButton(0))
        {
            // Calculate rotation based on mouse movement
            Vector3 mouseDelta = Input.mousePosition - lastMousePosition;
            float rotationDelta = mouseDelta.x * 0.5f; // Adjust sensitivity as needed
            
            currentRotationY += rotationDelta;
            ApplyRotation();
            ClampLocalX();
            
            lastMousePosition = Input.mousePosition;
        }

        // Update output regardless of whether we're dragging (works with VR too).
        UpdateVoltageText();
    }
    
    // Method to rotate by a specific increment (can be called externally)
    public void RotateIncrement(int direction)
    {
        currentRotationY += rotationIncrement * direction;
        SnapToIncrement();
        ApplyRotation();
    }
    
    // Snap rotation to nearest 10-degree increment
    private void SnapToIncrement()
    {
        currentRotationY = Mathf.Round(currentRotationY / rotationIncrement) * rotationIncrement;
    }
    
    // Apply the rotation to the transform (using local rotation to keep position stationary)
    private void ApplyRotation()
    {
        transform.localRotation = Quaternion.Euler(transform.localEulerAngles.x, currentRotationY, transform.localEulerAngles.z);
    }
    
    // OnMouseDown alternative (if you prefer this approach)
    void OnMouseDown()
    {
        // Rotate 10 degrees on click
        RotateIncrement(1);
    }

    // Clamp local X rotation between minAngleX and maxAngleX relative to the starting pose
    private void ClampLocalX()
    {
        Quaternion relative = Quaternion.Inverse(initialLocalRotation) * transform.localRotation;
        Vector3 euler = relative.eulerAngles;

        float xAngle = euler.x;
        if (xAngle > 180f)
            xAngle -= 360f;

        float clampedX = Mathf.Clamp(xAngle, minAngleX, maxAngleX);

        Quaternion clampedRelative = Quaternion.Euler(clampedX, 0f, 0f);
        transform.localRotation = initialLocalRotation * clampedRelative;
    }

    void UpdateVoltageText()
    {
        if (voltageText == null)
            return;

        // Signed local X angle relative to the starting pose
        Quaternion relative = Quaternion.Inverse(initialLocalRotation) * transform.localRotation;
        Vector3 euler = relative.eulerAngles;

        float xAngle = euler.x;
        if (xAngle > 180f)
            xAngle -= 360f;

        float clampedX = Mathf.Clamp(xAngle, minAngleX, maxAngleX);

        // t=0 at maxAngleX, t=1 at minAngleX
        float t = Mathf.InverseLerp(maxAngleX, minAngleX, clampedX);
        float voltage = Mathf.Lerp(voltageMin, voltageMax, t);

        if (voltageStep > 0f)
            voltage = Mathf.Round(voltage / voltageStep) * voltageStep;

        voltage = Mathf.Clamp(voltage, Mathf.Min(voltageMin, voltageMax), Mathf.Max(voltageMin, voltageMax));

        voltageText.text = string.Format(voltageTextFormat, voltage);
    }
}
