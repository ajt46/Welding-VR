using UnityEngine;
using UnityEngine.UI;   // or use TextMeshPro if you prefer

public class AngleDisplay : MonoBehaviour
{
    [Header("What to measure")]
    public Transform target;      // The object you are tilting (e.g. MIG gun)

    [Header("UI")]
    public Text angleText;        // Assign a UI Text in your Canvas

    void Update()
    {
        if (target == null || angleText == null)
            return;

        // Compare target's "up" with world up
        Vector3 worldUp = Vector3.up;
        Vector3 objectUp = target.up;

        // Angle between them (0 = perfectly upright, 90 = sideways, 180 = upside down)
        float tiltAngle = Vector3.Angle(objectUp, worldUp);

        angleText.text = $"Tilt : {tiltAngle:F1}°";
    }
}