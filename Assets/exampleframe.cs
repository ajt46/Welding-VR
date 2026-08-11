using UnityEngine;
using BNG;

public class exampleframe : MonoBehaviour
{
    [Header("Assigned real object")]
    [Tooltip("The real object root that should snap onto this frame and cause this frame to disappear.")]
    public Transform realObjectRoot;

    [Tooltip("Optional specific collider on the real object. If set, only this collider is accepted.")]
    public Collider realObjectCollider;

    [Header("Snap behavior")]
    [Tooltip("If true, snap when a trigger overlap occurs.")]
    public bool snapOnTrigger = true;

    [Tooltip("If true, snap when a solid collision occurs.")]
    public bool snapOnCollision = true;

    [Tooltip("If true, this frame disables all colliders after successful placement.")]
    public bool disableFrameCollidersOnSnap = true;

    [Tooltip("If true, this frame disables all renderers after successful placement.")]
    public bool hideFrameRenderersOnSnap = true;

    [Header("Grab cooldown after placement")]
    [Tooltip("If true, grabbing the placed real object is disabled for a short time after snap.")]
    public bool applyGrabCooldownAfterSnap = true;

    [Tooltip("Cooldown duration in seconds before grab is enabled again.")]
    public float grabCooldownSeconds = 1.0f;

    [Tooltip("While seated (and through grab cooldown), ignore physics between the real object and this frame so lift-off / re-grab is smooth. Uses force:true so it works even when the global SnapGuideCollisionIgnore master is off.")]
    public bool ignoreCollisionsDuringUnsnapCooldown = true;

    [Header("Re-arm")]
    [Tooltip("If true, when the frame is shown again via SetVisible after it already snapped, its snap re-arms so the object can snap onto it again (used when reverting to a reused example frame).")]
    public bool reArmSnapWhenReshown = true;

    bool snapped;
    bool _isVisible = true;
    bool guideCollisionsIgnored;

    void OnDisable()
    {
        SetUnsnapCollisionsIgnored(false);
    }

    void OnDestroy()
    {
        SetUnsnapCollisionsIgnored(false);
    }

    void OnTriggerEnter(Collider other)
    {
        if (snapOnTrigger)
            TrySnapFromCollider(other);
    }

    void OnCollisionEnter(Collision collision)
    {
        if (!snapOnCollision || collision == null)
            return;

        if (collision.collider != null)
            TrySnapFromCollider(collision.collider);

        if (collision.contactCount > 0)
        {
            var cp = collision.GetContact(0);
            if (cp.otherCollider != null)
                TrySnapFromCollider(cp.otherCollider);
            if (cp.thisCollider != null)
                TrySnapFromCollider(cp.thisCollider);
        }
    }

    void TrySnapFromCollider(Collider other)
    {
        if (snapped || realObjectRoot == null || other == null)
            return;

        if (!IsAssignedRealObjectCollider(other))
            return;

        realObjectRoot.SetPositionAndRotation(transform.position, transform.rotation);
        snapped = true;
        HideFrame();

        // Ignore while seated so re-grab / lift-off is already non-blocking.
        if (ignoreCollisionsDuringUnsnapCooldown)
            SetUnsnapCollisionsIgnored(true);

        if (applyGrabCooldownAfterSnap)
            StartCoroutine(ApplyGrabCooldown());
    }

    bool IsAssignedRealObjectCollider(Collider other)
    {
        if (realObjectCollider != null)
            return other == realObjectCollider;

        return other.transform == realObjectRoot || other.transform.IsChildOf(realObjectRoot);
    }

    void HideFrame()
    {
        if (hideFrameRenderersOnSnap)
        {
            foreach (var r in GetComponentsInChildren<Renderer>(true))
                r.enabled = false;
        }

        if (disableFrameCollidersOnSnap)
        {
            foreach (var c in GetComponentsInChildren<Collider>(true))
                c.enabled = false;
        }
    }

    /// <summary>
    /// Show or hide guides (inverse of snap <see cref="HideFrame"/>). Does not trigger snap logic.
    /// When hidden: renderers off AND colliders off (sheet-style — do not rely on SetActive on a shared root).
    /// </summary>
    public void SetVisible(bool visible)
    {
        // Rising edge (hidden -> shown): re-arm the one-shot snap so a reused frame can snap again.
        if (visible && !_isVisible && reArmSnapWhenReshown && snapped)
        {
            snapped = false;
            // Re-arming means the frame is interactive again — restore collisions.
            SetUnsnapCollisionsIgnored(false);
        }

        _isVisible = visible;

        foreach (var r in GetComponentsInChildren<Renderer>(true))
        {
            if (r != null)
                r.enabled = visible;
        }

        foreach (var c in GetComponentsInChildren<Collider>(true))
        {
            if (c != null)
                c.enabled = visible;
        }
    }

    /// <summary>Manually re-arm the snap so the object can snap onto this frame again.</summary>
    public void ResetSnap()
    {
        snapped = false;
        SetUnsnapCollisionsIgnored(false);
    }

    System.Collections.IEnumerator ApplyGrabCooldown()
    {
        if (realObjectRoot == null)
            yield break;

        var grabs = realObjectRoot.GetComponentsInChildren<Grabbable>(true);
        if (grabs == null || grabs.Length == 0)
            yield break;

        foreach (var g in grabs)
        {
            if (g == null)
                continue;
            if (g.BeingHeld)
                g.DropItem(true, true);
            g.enabled = false;
        }

        float wait = Mathf.Max(0f, grabCooldownSeconds);
        if (wait > 0f)
            yield return new WaitForSeconds(wait);

        foreach (var g in grabs)
        {
            if (g != null)
                g.enabled = true;
        }

        // No pickup-unsnap path on this frame: keep ignore while still seated; clear only if re-armed.
        if (!snapped)
            SetUnsnapCollisionsIgnored(false);
    }

    /// <summary>
    /// Ignore physics between the assigned real object and this frame.
    /// Uses force:true so it works even when the global SnapGuideCollisionIgnore master switch is off.
    /// </summary>
    void SetUnsnapCollisionsIgnored(bool ignore)
    {
        if (realObjectRoot == null)
        {
            guideCollisionsIgnored = false;
            return;
        }

        if (!ignore)
        {
            if (!guideCollisionsIgnored)
                return;

            SnapGuideCollisionIgnore.SetIgnoredBetween(realObjectRoot, transform, false, force: true);
            guideCollisionsIgnored = false;
            return;
        }

        if (!ignoreCollisionsDuringUnsnapCooldown)
            return;

        SnapGuideCollisionIgnore.SetIgnoredBetween(realObjectRoot, transform, true, force: true);
        guideCollisionsIgnored = true;
    }
}
