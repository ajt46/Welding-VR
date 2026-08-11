using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Put this on the same GameObject as each <b>example</b> placement collider (must be a trigger).
/// Assign the joiner and the slot index that matches <see cref="MultiPartAssemblyJoiner.slots"/>.
/// </summary>
public class AssemblyPlacementTrigger : MonoBehaviour
{
    public MultiPartAssemblyJoiner joiner;
    public int slotIndex;

    [Tooltip("If true, leaving the trigger clears the slot until all pieces are joined.")]
    public bool revokePlacementOnExit;

    [Header("Solid collision (optional)")]
    [Tooltip("If true, also uses OnCollisionEnter — use a non-trigger collider on this object (or child) so the real piece’s Rigidbody physically hits it. Does not replace the trigger; you can use both.")]
    public bool alsoDetectPhysicsCollision;

    [Header("Example visibility")]
    [Tooltip("If set, renderers under this transform are toggled (use when the mesh lives on a parent). If null, only renderers under this object are used.")]
    public Transform renderersRoot;

    [Tooltip("On Start, turns off every Renderer under Renderers Root (or under this object). Colliders stay active so placement still works.")]
    public bool hideRenderersAtStart = true;

    [Tooltip("If true, once this slot is filled in the joiner, this example's renderers remain hidden.")]
    public bool keepHiddenAfterSuccessfulPlacement = true;

    void Start()
    {
        if (hideRenderersAtStart)
            SetExampleRenderersEnabled(false);
    }

    /// <summary>
    /// Enable or disable all <see cref="Renderer"/> components under the visibility root (or this transform).
    /// </summary>
    public void SetExampleRenderersEnabled(bool enabled)
    {
        if (enabled && keepHiddenAfterSuccessfulPlacement && joiner != null && joiner.IsSlotFilled(slotIndex))
            enabled = false;

        Transform root = renderersRoot != null ? renderersRoot : transform;
        foreach (var r in root.GetComponentsInChildren<Renderer>(true))
            r.enabled = enabled;
    }

    void OnTriggerEnter(Collider other)
    {
        if (joiner != null)
        {
            joiner.NotifyPartInPlacementZone(slotIndex, other);
            HideExampleIfSlotIsFilled();
        }
    }

    void OnTriggerStay(Collider other)
    {
        if (joiner != null)
        {
            joiner.NotifyPartInPlacementZone(slotIndex, other);
            HideExampleIfSlotIsFilled();
        }
    }

    void OnTriggerExit(Collider other)
    {
        if (joiner != null)
            joiner.NotifyPartLeftPlacementZone(slotIndex, other, revokePlacementOnExit);
    }

    void OnCollisionEnter(Collision collision)
    {
        if (!alsoDetectPhysicsCollision || joiner == null || collision == null)
            return;

        NotifyPartCollisionInZone(collision);
    }

    void OnCollisionStay(Collision collision)
    {
        if (!alsoDetectPhysicsCollision || joiner == null || collision == null)
            return;

        NotifyPartCollisionInZone(collision);
    }

    void OnCollisionExit(Collision collision)
    {
        if (!alsoDetectPhysicsCollision || joiner == null || collision == null || !revokePlacementOnExit)
            return;

        NotifyPartCollisionLeftZone(collision);
    }

    void NotifyPartCollisionInZone(Collision collision)
    {
        foreach (var col in GetCollisionColliders(collision))
            joiner.NotifyPartInPlacementZone(slotIndex, col);

        HideExampleIfSlotIsFilled();
    }

    void NotifyPartCollisionLeftZone(Collision collision)
    {
        foreach (var col in GetCollisionColliders(collision))
            joiner.NotifyPartLeftPlacementZone(slotIndex, col, true);
    }

    IEnumerable<Collider> GetCollisionColliders(Collision collision)
    {
        var seen = new HashSet<Collider>();

        if (collision.collider != null && seen.Add(collision.collider))
            yield return collision.collider;

        int count = collision.contactCount;
        for (int i = 0; i < count; i++)
        {
            ContactPoint cp = collision.GetContact(i);
            if (cp.thisCollider != null && seen.Add(cp.thisCollider))
                yield return cp.thisCollider;
            if (cp.otherCollider != null && seen.Add(cp.otherCollider))
                yield return cp.otherCollider;
        }
    }

    void HideExampleIfSlotIsFilled()
    {
        if (!keepHiddenAfterSuccessfulPlacement || joiner == null)
            return;
        if (joiner.IsSlotFilled(slotIndex))
            SetExampleRenderersEnabled(false);
    }
}
