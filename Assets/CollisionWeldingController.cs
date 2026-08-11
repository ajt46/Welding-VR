using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Collision-based welding controller.
/// Attach this to the MIG welding gun parent.
/// Blobs are created ONLY when:
///  - the welding tip collider is touching the welding panel collider, AND
///  - input code calls StartWelding() (e.g. trigger pressed while gun is grabbed).
/// 
/// You can re-use this alongside weldingsparks.cs by calling StartWelding / StopWelding
/// instead of using raycasts inside the controller.
/// </summary>
public class CollisionWeldingController : MonoBehaviour {

    [Header("References")]
    [Tooltip("Transform at the end of the gun (visual tip)")]
    public Transform weldingTip;

    [Tooltip("Collider representing the welding tip")]
    public Collider tipCollider;

    [Tooltip("Collider on the weldable panel")]
    public Collider panelCollider;

    [Tooltip("Optional: transform whose 'up' is used as the panel surface normal. If null, panelCollider.transform.up is used.")]
    public Transform panelSurface;

    [Header("Blob Settings")]
    [Tooltip("Prefab to instantiate when creating a new weld blob")]
    public GameObject weldBlobPrefab;

    [Tooltip("Initial size of the blob when created")]
    public float blobInitialSize = 0.2f;

    [Tooltip("Maximum size before blob overheats")]
    public float blobMaxSize = 0.7f;

    [Tooltip("How fast the blob grows per second")]
    public float blobGrowthRate = 0.2f;

    [Header("Blob Formation Settings")]
    [Tooltip("Number of blobs created per second while welding")]
    [Range(1, 30)]
    public float blobsPerSecond = 10f;

    [Tooltip("Layer for welding blobs")]
    public int blobLayer = 6;

    [Header("Overheating")]
    [Tooltip("Prefab to show when blob overheats (creates a hole). Optional.")]
    public GameObject holePrefab;

    [Tooltip("Time to wait after overheating before allowing welding again")]
    public float overheatingCooldown = 0.5f;

    [Header("Timing")]
    [Tooltip("Delay in seconds before welding actually starts after trigger press")]
    public float weldingStartDelay = 1f;

    // Internal state
    bool triggerHeld = false;          // set by StartWelding / StopWelding
    bool isWelding = false;            // true once delay is passed and contact is valid
    bool isTipTouchingPanel = false;
    bool isOverheating = false;

    float weldTimer = 0f;
    float timeSinceLastBlob = 0f;
    float blobInterval = 0.1f;

    GameObject currentBlob = null;
    float currentBlobSize = 0f;
    Transform currentBlobParent = null;

    void Start() {
        UpdateBlobInterval();
    }

    void Update() {
        if (tipCollider == null || panelCollider == null || weldingTip == null || weldBlobPrefab == null) {
            return;
        }

        // Check collision contact via bounds overlap
        isTipTouchingPanel = tipCollider.bounds.Intersects(panelCollider.bounds);

        // Handle welding state
        if (isOverheating) {
            // cooling down, do nothing
            return;
        }

        if (triggerHeld && isTipTouchingPanel) {
            weldTimer += Time.deltaTime;

            if (!isWelding && weldTimer >= weldingStartDelay) {
                isWelding = true;
            }
        } else {
            // Either trigger released or no contact: stop welding
            if (isWelding) {
                FinalizeCurrentBlob();
            }
            isWelding = false;
            weldTimer = 0f;
            timeSinceLastBlob = 0f;
            return;
        }

        // When welding is active and in contact, spawn / grow blobs
        if (isWelding) {
            timeSinceLastBlob += Time.deltaTime;

            // Create blobs at a constant rate
            if (timeSinceLastBlob >= blobInterval) {
                CreateOrGrowBlobAtContact();
                timeSinceLastBlob = 0f;
            } else if (currentBlob != null) {
                // Grow current blob even between spawn ticks
                GrowCurrentBlob();
            }
        }
    }

    /// <summary>
    /// Called by input code when trigger is pressed while gun is grabbed.
    /// </summary>
    public void StartWelding() {
        triggerHeld = true;
        // don't reset weldTimer here so delay is continuous
    }

    /// <summary>
    /// Called by input code when trigger is released or gun is dropped.
    /// </summary>
    public void StopWelding() {
        triggerHeld = false;
        isWelding = false;
        weldTimer = 0f;
        timeSinceLastBlob = 0f;
        FinalizeCurrentBlob();
    }

    void UpdateBlobInterval() {
        blobInterval = 1f / Mathf.Max(blobsPerSecond, 0.1f);
    }

    void CreateOrGrowBlobAtContact() {
        Vector3 contactPoint = panelCollider.ClosestPoint(weldingTip.position);
        Vector3 normal = panelSurface != null ? panelSurface.up : panelCollider.transform.up;

        // If we already have a blob, grow it until limit
        if (currentBlob != null) {
            GrowCurrentBlob();
            return;
        }

        // Create new blob at contact point
        Quaternion rotation = Quaternion.FromToRotation(Vector3.up, normal);

        currentBlob = Instantiate(weldBlobPrefab, contactPoint, rotation);
        currentBlob.layer = blobLayer;
        currentBlob.transform.localScale = Vector3.one * blobInitialSize;
        currentBlobSize = blobInitialSize;
        currentBlobParent = panelCollider.transform;

        currentBlob.tag = "WeldObject";

        if (currentBlobParent != null) {
            currentBlob.transform.SetParent(currentBlobParent);
        }
    }

    void GrowCurrentBlob() {
        if (currentBlob == null) return;

        currentBlobSize += blobGrowthRate * Time.deltaTime;

        if (currentBlobSize >= blobMaxSize) {
            OverheatCurrentBlob();
        } else {
            currentBlob.transform.localScale = Vector3.one * currentBlobSize;
        }
    }

    void OverheatCurrentBlob() {
        if (currentBlob == null) return;

        if (holePrefab != null) {
            GameObject hole = Instantiate(holePrefab, currentBlob.transform.position, currentBlob.transform.rotation);
            hole.transform.localScale = currentBlob.transform.localScale;
        }

        Destroy(currentBlob);
        currentBlob = null;
        currentBlobSize = 0f;
        isOverheating = true;

        StartCoroutine(OverheatCooldownCoroutine());
    }

    IEnumerator OverheatCooldownCoroutine() {
        yield return new WaitForSeconds(overheatingCooldown);
        isOverheating = false;
    }

    void FinalizeCurrentBlob() {
        if (currentBlob != null && currentBlobParent != null) {
            currentBlob.transform.SetParent(currentBlobParent);
        }
        currentBlob = null;
        currentBlobSize = 0f;
    }
}

