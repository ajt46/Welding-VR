using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Welding sheet anchor (one instance can be shared by several workpieces): holds a <see cref="connectedObject"/>
/// preview shown when any linked <see cref="ungroundedgrabbable"/> is held and in range.
/// Optional <see cref="allowedSnapObjects"/> limits preview + snap to specific panel roots (when non-empty).
/// Only one workpiece may be snapped to this sheet at a time; others are blocked until <see cref="NotifySnapReleased"/>.
/// </summary>
public class exampleweldingsheet : MonoBehaviour
{
    ungroundedgrabbable _snappedWorkpiece;
    readonly HashSet<ungroundedgrabbable> _previewRequesters = new HashSet<ungroundedgrabbable>();

    [Header("Which workpieces use this sheet")]
    [Tooltip("Drag each panel / workpiece root that should show this sheet’s preview and may snap here (e.g. Mild, Stainless, Aluminium). If empty, any ungroundedgrabbable that references this sheet is allowed.")]
    public GameObject[] allowedSnapObjects;

    [Header("Connected object")]
    [Tooltip("Shown only when an allowed ungroundedgrabbable reports held + in range. Starts inactive if Start Hidden is true. Prefer a child preview GO — if this is the same as this GameObject, only renderers/colliders are toggled (so the sheet script stays alive for all panels).")]
    public GameObject connectedObject;

    [Tooltip("If true, connected object is disabled on Start.")]
    public bool startHidden = true;

    [Header("Transforms")]
    [Tooltip("Pose used when the workpiece auto-snaps to this sheet. If null, this object's transform is used.")]
    public Transform snapTransform;

    [Tooltip("Distance checks use this point. If null, uses Snap Transform or this transform.")]
    public Transform proximityAnchor;

    [Header("Snap targets (collision / trigger)")]
    [Tooltip("Like clamp's box collider: only this collider counts as contact. If null, any collider on this object or children can trigger snap (when allow any is true).")]
    public Collider snapTargetCollider;

    [Tooltip("If Snap Target Collider is null, any collider on this GameObject or its children is a valid snap surface for the RealSheet.")]
    public bool allowAnyCollidersOnSheetAsSnapTarget = true;

    [Header("Optional matching")]
    [Tooltip("Must match ungroundedgrabbable.sheetAssetKey when that field is non-empty.")]
    public string nameofasset = "";

    void Start()
    {
        if (snapTransform == null)
            snapTransform = transform;
        if (proximityAnchor == null)
            proximityAnchor = snapTransform;

        if (startHidden)
            ApplyConnectedVisibility(false);
    }

    void ClearStaleSnappedReference()
    {
        if (_snappedWorkpiece != null && !_snappedWorkpiece)
            _snappedWorkpiece = null;
    }

    void ClearStalePreviewRequesters()
    {
        if (_previewRequesters.Count == 0)
            return;

        _previewRequesters.RemoveWhere(r => r == null);
    }

    /// <summary>
    /// True if this workpiece may use proximity preview or start a snap: allowed by <see cref="allowedSnapObjects"/> (if set),
    /// slot is empty or already held by this instance, and not blocked by another snapped piece.
    /// </summary>
    public bool SheetAllowsWorkpiece(ungroundedgrabbable requester)
    {
        ClearStaleSnappedReference();
        if (requester == null)
            return false;
        if (!IsWorkpieceDesignatedForThisSheet(requester))
            return false;
        if (_snappedWorkpiece != null && _snappedWorkpiece != requester)
            return false;
        return true;
    }

    /// <summary>
    /// When <see cref="allowedSnapObjects"/> is empty, any workpiece with this sheet assigned may interact.
    /// When non-empty, only those roots (or children under them) qualify.
    /// </summary>
    public bool IsWorkpieceDesignatedForThisSheet(ungroundedgrabbable workpiece)
    {
        if (workpiece == null)
            return false;
        if (allowedSnapObjects == null || allowedSnapObjects.Length == 0)
            return true;

        Transform t = workpiece.transform;
        for (int i = 0; i < allowedSnapObjects.Length; i++)
        {
            GameObject go = allowedSnapObjects[i];
            if (go == null)
                continue;
            if (workpiece.gameObject == go)
                return true;
            if (t.IsChildOf(go.transform))
                return true;
            if (go.transform.IsChildOf(t))
                return true;
        }

        return false;
    }

    /// <summary>Runtime: which workpiece is snapped to this sheet, if any.</summary>
    public ungroundedgrabbable GetSnappedWorkpiece()
    {
        ClearStaleSnappedReference();
        return _snappedWorkpiece;
    }

    /// <summary>Call after a successful snap so other panels cannot snap until released.</summary>
    public void NotifySnapOccupied(ungroundedgrabbable workpiece)
    {
        _snappedWorkpiece = workpiece;
        if (workpiece != null)
            SetConnectedVisible(false, workpiece);
    }

    /// <summary>Call when the snapped workpiece is picked up or destroyed.</summary>
    public void NotifySnapReleased(ungroundedgrabbable workpiece)
    {
        if (workpiece != null && _snappedWorkpiece == workpiece)
            _snappedWorkpiece = null;
        if (workpiece != null)
            SetConnectedVisible(false, workpiece);
    }

    /// <summary>World point used for distance-to-sheet checks.</summary>
    public Vector3 GetProximityPoint()
    {
        if (proximityAnchor != null)
            return proximityAnchor.position;
        return snapTransform != null ? snapTransform.position : transform.position;
    }

    /// <summary>Target pose when snapping the grabbable to the sheet.</summary>
    public Transform GetSnapTransform()
    {
        return snapTransform != null ? snapTransform : transform;
    }

    /// <summary>
    /// Request show/hide of the shared ghost. Multiple panels vote; the ghost stays visible
    /// while any requester still wants it on (fixes the old last-writer-wins bug).
    /// </summary>
    public void SetConnectedVisible(bool visible, ungroundedgrabbable requester)
    {
        if (requester == null)
            return;

        ClearStalePreviewRequesters();

        if (visible)
            _previewRequesters.Add(requester);
        else
            _previewRequesters.Remove(requester);

        ApplyConnectedVisibility(AnyPreviewRequesterActive());
    }

    /// <summary>Legacy: force visibility without requester tracking (prefer the requester overload).</summary>
    public void SetConnectedVisible(bool visible)
    {
        if (!visible)
        {
            _previewRequesters.Clear();
            ApplyConnectedVisibility(false);
            return;
        }

        ApplyConnectedVisibility(true);
    }

    /// <summary>True while at least one allowed held workpiece is requesting the ghost.</summary>
    public bool IsConnectedVisible() => AnyPreviewRequesterActive();

    bool AnyPreviewRequesterActive()
    {
        ClearStalePreviewRequesters();
        return _previewRequesters.Count > 0;
    }

    /// <summary>
    /// Ghost is only visible AND collidable while an allowed snap object is held (and in range).
    /// When nothing is requesting it (or after snap), hide visuals and disable all sheet colliders.
    /// </summary>
    void ApplyConnectedVisibility(bool visible)
    {
        // Preview mesh / child GO
        if (connectedObject != null && connectedObject != gameObject)
        {
            connectedObject.SetActive(visible);
        }
        else
        {
            // Connected Object is this root (or unset): never SetActive(false) on the sheet itself
            // (that would disable this script for every panel). Toggle renderers instead.
            foreach (var r in GetComponentsInChildren<Renderer>(true))
            {
                if (r == null)
                    continue;
                if (connectedObject != null && connectedObject != gameObject &&
                    !r.transform.IsChildOf(connectedObject.transform) && r.transform != connectedObject.transform)
                    continue;
                r.enabled = visible;
            }
        }

        // Snap / interaction colliders on the sheet: off unless a defined object is held.
        SetSheetCollidersEnabled(visible);
    }

    void SetSheetCollidersEnabled(bool enabled)
    {
        Collider[] cols = GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < cols.Length; i++)
        {
            Collider c = cols[i];
            if (c == null)
                continue;

            // If preview is a separate child already deactivated via SetActive, skip; still drive
            // colliders that live on this sheet root / other children used for snap.
            c.enabled = enabled;
        }

        if (snapTargetCollider != null)
            snapTargetCollider.enabled = enabled;
    }

    /// <summary>
    /// True if <paramref name="other"/> is a collider that should count as RealSheet touching this sheet (for snap).
    /// </summary>
    public bool IsSnapTargetCollider(Collider other)
    {
        if (other == null)
            return false;

        if (snapTargetCollider != null)
            return IsSameCollider(other, snapTargetCollider);

        if (allowAnyCollidersOnSheetAsSnapTarget)
        {
            Transform t = other.transform;
            return t == transform || t.IsChildOf(transform);
        }

        return false;
    }

    /// <summary>
    /// Colliders used for IgnoreCollision with a seated workpiece (sheet root, snap target, connected preview if separate).
    /// </summary>
    public Collider[] GetCollidersForIgnore()
    {
        var list = new List<Collider>();
        CollectColliders(transform, list);

        if (snapTargetCollider != null && !list.Contains(snapTargetCollider))
            list.Add(snapTargetCollider);

        if (connectedObject != null && connectedObject.transform != transform &&
            !connectedObject.transform.IsChildOf(transform))
            CollectColliders(connectedObject.transform, list);

        return list.ToArray();
    }

    static void CollectColliders(Transform root, List<Collider> into)
    {
        if (root == null || into == null)
            return;

        Collider[] cols = root.GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < cols.Length; i++)
        {
            Collider c = cols[i];
            if (c != null && !into.Contains(c))
                into.Add(c);
        }
    }

    static bool IsSameCollider(Collider a, Collider b)
    {
        if (a == null || b == null)
            return false;
        return a == b || a.transform == b.transform || a.transform.IsChildOf(b.transform) || b.transform.IsChildOf(a.transform);
    }
}
