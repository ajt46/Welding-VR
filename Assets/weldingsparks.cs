using UnityEngine;

/// <summary>
/// Deprecated: spark emission, triggers, and prerequisites are handled by <see cref="CustomWeldingController"/>.
/// Remove this component from the MIG gun prefab to avoid duplicate logic.
/// </summary>
[DisallowMultipleComponent]
public class weldingsparks : MonoBehaviour
{
    void Reset()
    {
        Debug.LogWarning($"Remove {nameof(weldingsparks)} from '{gameObject.name}' — use CustomWeldingController (sparks + left/right trigger) instead.", this);
    }

    void Awake()
    {
        enabled = false;
    }
}
