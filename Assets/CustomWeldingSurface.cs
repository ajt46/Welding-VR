using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Simple script for weldable surfaces
/// Attach this to objects that can be welded on
/// </summary>
public class CustomWeldingSurface : MonoBehaviour
{
    [Header("Surface Settings")]
    [Tooltip("Layer this surface should be on (should match weldableLayers in CustomWeldingController)")]
    public int surfaceLayer = 7;

    [Header("Optional: Quality Checking")]
    [Tooltip("Collider that defines the 'good weld' area (for quality checking)")]
    public Collider goodWeldArea;

    [Tooltip("List of transforms that define the weld path (for scanning)")]
    public Transform[] weldPathPoints;

    void Start()
    {
        // Ensure object is on correct layer
        if (gameObject.layer != surfaceLayer)
        {
            gameObject.layer = surfaceLayer;
        }
    }

    /// <summary>
    /// Check if a position is within the 'good weld' area
    /// </summary>
    public bool IsInGoodWeldArea(Vector3 position)
    {
        if (goodWeldArea == null)
            return true; // If no area defined, all welds are considered good

        return goodWeldArea.bounds.Contains(position);
    }

    /// <summary>
    /// Get weld path points for scanning
    /// </summary>
    public Vector3[] GetWeldPathPoints()
    {
        if (weldPathPoints == null || weldPathPoints.Length == 0)
            return new Vector3[0];

        Vector3[] points = new Vector3[weldPathPoints.Length];
        for (int i = 0; i < weldPathPoints.Length; i++)
        {
            if (weldPathPoints[i] != null)
                points[i] = weldPathPoints[i].position;
        }
        return points;
    }
}
