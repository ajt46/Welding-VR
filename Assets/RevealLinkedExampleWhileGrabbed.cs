using System.Collections;
using BNG;
using UnityEngine;

/// <summary>
/// Add to the same GameObject as <see cref="Grabbable"/>. While this piece is held, the linked
/// example’s renderers are enabled; on release they are disabled again.
/// Pair with <see cref="AssemblyPlacementTrigger"/> (assign it here) so the same visibility root is used.
/// </summary>
public class RevealLinkedExampleWhileGrabbed : GrabbableEvents
{
    [Tooltip("If set, uses AssemblyPlacementTrigger.SetExampleRenderersEnabled (respects its Renderers Root). Preferred when that component is on the example.")]
    public AssemblyPlacementTrigger linkedExamplePlacementTrigger;

    [Tooltip("If no Placement Trigger is assigned, toggles all Renderers under this transform.")]
    public Transform linkedExampleRenderRoot;

    [Tooltip("If false, the example stays visible after the first grab until you handle it elsewhere.")]
    public bool hideLinkedExampleOnRelease = true;

    [Header("Snap to example (while grabbed)")]
    [Tooltip("When true, moves Piece Root so the two align points match exactly in world space (fixes small offsets from different pivots).")]
    public bool snapToExampleOnGrab;

    [Tooltip("Transform that is moved (Rigidbody root / part root). Defaults to this object.")]
    public Transform pieceRoot;

    [Tooltip("A point on THIS piece (usually a child empty) that should land exactly on Align Point On Example. If null, Piece Root is used (same as simple pivot snap).")]
    public Transform alignPointOnThisPiece;

    [Tooltip("The target pose on the EXAMPLE (child empty). World position and rotation of this transform define where the real align point should go.")]
    public Transform alignPointOnExample;

    [Tooltip("If true, snap runs after one frame so the hand / Grabbable can update first (helps remove residual offset in VR).")]
    public bool deferSnapOneFrame;

    void Awake()
    {
        if (pieceRoot == null)
            pieceRoot = transform;
    }

    public override void OnGrab(Grabber grabber)
    {
        base.OnGrab(grabber);
        SetLinkedExampleRenderers(true);

        if (snapToExampleOnGrab && alignPointOnExample != null)
        {
            if (deferSnapOneFrame)
                StartCoroutine(SnapAfterYield());
            else
                ApplyPreciseSnap();
        }
    }

    public override void OnRelease()
    {
        base.OnRelease();
        if (hideLinkedExampleOnRelease)
            SetLinkedExampleRenderers(false);
    }

    IEnumerator SnapAfterYield()
    {
        yield return null;
        ApplyPreciseSnap();
    }

    /// <summary>
    /// Aligns <see cref="pieceRoot"/> so <see cref="alignPointOnThisPiece"/> matches <see cref="alignPointOnExample"/> in world space.
    /// </summary>
    public void ApplyPreciseSnap()
    {
        if (pieceRoot == null || alignPointOnExample == null)
            return;

        Transform root = pieceRoot;
        Transform src = alignPointOnThisPiece != null ? alignPointOnThisPiece : root;
        Transform dst = alignPointOnExample;

        root.position += dst.position - src.position;

        Quaternion delta = dst.rotation * Quaternion.Inverse(src.rotation);
        Vector3 pivot = dst.position;
        root.position = pivot + delta * (root.position - pivot);
        root.rotation = delta * root.rotation;

        foreach (var rb in root.GetComponentsInChildren<Rigidbody>())
        {
            if (rb == null)
                continue;
            rb.velocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }
    }

    void SetLinkedExampleRenderers(bool enabled)
    {
        if (linkedExamplePlacementTrigger != null)
        {
            linkedExamplePlacementTrigger.SetExampleRenderersEnabled(enabled);
            return;
        }

        if (linkedExampleRenderRoot == null)
            return;

        foreach (var r in linkedExampleRenderRoot.GetComponentsInChildren<Renderer>(true))
            r.enabled = enabled;
    }
}
