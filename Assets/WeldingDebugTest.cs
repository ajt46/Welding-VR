using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using BNG;

/// <summary>
/// Quick debug script to test MIG welder setup
/// Attach this to your MIG Welding Gun object for testing
/// </summary>
public class WeldingDebugTest : MonoBehaviour
{
    private Grabbable grabbable;
    private WeldingHandle weldingHandle;
    private ParticleSystem particleSystem;
    private InputBridge inputBridge;

    void Start()
    {
        grabbable = GetComponent<Grabbable>();
        weldingHandle = GetComponent<WeldingHandle>();
        particleSystem = GetComponentInChildren<ParticleSystem>();
        inputBridge = InputBridge.Instance;
    }

    void Update()
    {
        // Press 'T' key to print debug info
        if (Input.GetKeyDown(KeyCode.T))
        {
            PrintDebugInfo();
        }

        // Press 'W' key to simulate welding (for testing without VR)
        if (Input.GetKeyDown(KeyCode.W))
        {
            if (weldingHandle != null)
            {
                Debug.Log("Manually starting welding...");
                weldingHandle.StartWelding();
            }
        }

        if (Input.GetKeyUp(KeyCode.W))
        {
            if (weldingHandle != null)
            {
                Debug.Log("Manually stopping welding...");
                weldingHandle.StopWelding();
            }
        }
    }

    void PrintDebugInfo()
    {
        Debug.Log("=== MIG WELDER DEBUG INFO ===");
        Debug.Log($"Grabbable Component: {(grabbable != null ? "✓ Found" : "✗ MISSING")}");
        Debug.Log($"WeldingHandle Component: {(weldingHandle != null ? "✓ Found" : "✗ MISSING")}");
        Debug.Log($"ParticleSystem: {(particleSystem != null ? "✓ Found" : "✗ MISSING")}");
        Debug.Log($"InputBridge: {(inputBridge != null ? "✓ Found" : "✗ MISSING")}");

        if (grabbable != null)
        {
            Debug.Log($"Being Held: {grabbable.BeingHeld}");
        }

        if (inputBridge != null)
        {
            Debug.Log($"Right Trigger Value: {inputBridge.RightTrigger}");
            Debug.Log($"Right Trigger Down: {inputBridge.RightTriggerDown}");
        }

        if (weldingHandle != null)
        {
            Debug.Log($"Welding Tip: {(weldingHandle.weldingTip != null ? "✓ Assigned" : "✗ NOT ASSIGNED")}");
            Debug.Log($"Weld Blob Set: {(weldingHandle.weldBlobSet != null ? "✓ Assigned" : "✗ NOT ASSIGNED")}");
            Debug.Log($"Weld Hole Mask: {(weldingHandle.weldHoleMask != null ? "✓ Assigned" : "✗ NOT ASSIGNED")}");
        }

        if (particleSystem != null)
        {
            var emission = particleSystem.emission;
            Debug.Log($"Particle Emission Enabled: {emission.enabled}");
            Debug.Log($"Particle System Playing: {particleSystem.isPlaying}");
        }

        // Check layers
        Debug.Log($"GameObject Layer: {gameObject.layer} (Name: {LayerMask.LayerToName(gameObject.layer)})");

        Debug.Log("=== END DEBUG INFO ===");
    }

    void OnDrawGizmos()
    {
        // Draw raycast from welding tip in editor
        if (weldingHandle != null && weldingHandle.weldingTip != null)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawRay(weldingHandle.weldingTip.position, weldingHandle.weldingTip.forward * 0.5f);
        }
    }
}
