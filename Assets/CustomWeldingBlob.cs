using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Simple script for welding blobs - handles visual effects and cooling
/// Attach this to your weld blob prefab
/// </summary>
public class CustomWeldingBlob : MonoBehaviour
{
    [Header("Materials")]
    [Tooltip("Material to use when blob is hot (glowing)")]
    public Material hotMaterial;
    
    [Tooltip("Material to use when blob has cooled")]
    public Material cooledMaterial;

    [Header("Cooling Settings")]
    [Tooltip("Time in seconds before blob starts cooling")]
    public float coolingDelay = 1.5f;
    
    [Tooltip("Time in seconds for blob to fade from hot to cool")]
    public float coolingFadeTime = 1.5f;

    [Header("Mesh Settings")]
    [Tooltip("Optional: Different mesh to use when cooled (e.g., flat blob)")]
    public Mesh cooledMesh;

    private MeshRenderer meshRenderer;
    private Material currentMaterial;
    private bool isCooling = false;

    void Start()
    {
        meshRenderer = GetComponent<MeshRenderer>();
        
        // Start with hot material
        if (hotMaterial != null && meshRenderer != null)
        {
            currentMaterial = new Material(hotMaterial);
            meshRenderer.material = currentMaterial;
        }

        // Start cooling after delay
        StartCoroutine(StartCooling());
    }

    /// <summary>
    /// Start the cooling process
    /// </summary>
    private IEnumerator StartCooling()
    {
        yield return new WaitForSeconds(coolingDelay);
        
        if (isCooling)
            yield break; // Already cooling

        isCooling = true;
        StartCoroutine(CoolDown());
    }

    /// <summary>
    /// Cool down animation
    /// </summary>
    private IEnumerator CoolDown()
    {
        float elapsed = 0f;
        Color startColor = currentMaterial != null ? currentMaterial.color : Color.white;
        Color targetColor = new Color(0.5f, 0.5f, 0.3f, 1f); // Cooled color

        while (elapsed < coolingFadeTime)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / coolingFadeTime;

            if (currentMaterial != null)
            {
                // Fade color
                currentMaterial.color = Color.Lerp(startColor, targetColor, t);
                
                // Fade emission
                if (currentMaterial.HasProperty("_EmissionColor"))
                {
                    Color emission = Color.Lerp(Color.white, Color.black, t);
                    currentMaterial.SetColor("_EmissionColor", emission);
                }
            }

            yield return null;
        }

        // Switch to cooled material
        if (cooledMaterial != null && meshRenderer != null)
        {
            meshRenderer.material = cooledMaterial;
        }

        // Switch to cooled mesh if provided
        if (cooledMesh != null)
        {
            MeshFilter meshFilter = GetComponent<MeshFilter>();
            if (meshFilter != null)
            {
                meshFilter.mesh = cooledMesh;
            }
        }

        // Clean up
        if (currentMaterial != null)
        {
            Destroy(currentMaterial);
        }
    }

    /// <summary>
    /// Make blob glow again (when re-welding)
    /// </summary>
    public void Reheat()
    {
        if (meshRenderer != null && hotMaterial != null)
        {
            currentMaterial = new Material(hotMaterial);
            meshRenderer.material = currentMaterial;
        }

        // Reset cooling
        isCooling = false;
        StopAllCoroutines();
        StartCoroutine(StartCooling());
    }
}
