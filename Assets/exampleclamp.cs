using UnityEngine;

/// <summary>
/// Ghost guide for a real <see cref="clamp"/>. Visibility is driven by the clamp:
/// Path 1 (panel) vs Path 2 (frame/assembly) eligibility, then proximity while held.
/// Path 2 may reuse the same guide for two ordered steps (like a reshown <see cref="exampleframe"/>);
/// <see cref="SetVisible"/> rising-edge re-arms so the ghost is interactive again after a prior snap hide.
/// Supplies the snap pose via <see cref="GetSnapTransform"/>.
/// </summary>
public class exampleclamp : MonoBehaviour
{
    [Header("Matching")]
    [Tooltip("Asset key used by clamp.cs to match this guide. Must match the real clamp's nameofasset.")]
    public string nameofasset;

    [Header("Visibility")]
    [Tooltip("If true, the guide starts hidden. The paired clamp shows it only while held and Path 1 / Path 2 prerequisites allow this ghost.")]
    public bool startHidden = true;

    [Header("Snap Pose")]
    [Tooltip("Transform used as the target pose when snapping the real clamp. If null, this transform is used.")]
    public Transform snapTransform;

    [Header("Re-arm")]
    [Tooltip("If true, when this guide is shown again via SetVisible after it was hidden (e.g. Path 2 reused slot like exampleframe), treat the rising edge as a fresh guide reveal so snap/collision state is clean.")]
    public bool reArmSnapWhenReshown = true;

    bool _isVisible = true;

    void Start()
    {
        if (snapTransform == null)
            snapTransform = transform;

        // startHidden path: SetVisible(false) from true default — do not treat as a re-arm edge.
        _isVisible = true;
        SetVisible(!startHidden);
    }

    /// <summary>
    /// Sets guide visibility (activates/deactivates the guide GameObject).
    /// Rising edge (hidden → shown) with <see cref="reArmSnapWhenReshown"/> mirrors
    /// <see cref="exampleframe.SetVisible"/> so a reused Path 2 ghost can be used again.
    /// </summary>
    public void SetVisible(bool visible)
    {
        if (visible && !_isVisible && reArmSnapWhenReshown)
            ResetSnap();

        _isVisible = visible;
        gameObject.SetActive(visible);
    }

    /// <summary>
    /// Manually clear any per-guide latch so the clamp can target this ghost again
    /// (parity with <see cref="exampleframe.ResetSnap"/>; clamp owns the real snap flag).
    /// </summary>
    public void ResetSnap()
    {
        // exampleclamp has no one-shot snapped latch (clamp.cs owns IsSnapped).
        // Hook kept for Inspector/API parity with exampleframe and future guide-side state.
    }

    /// <summary>Returns the transform that represents the desired snap pose.</summary>
    public Transform GetSnapTransform()
    {
        return snapTransform != null ? snapTransform : transform;
    }
}
