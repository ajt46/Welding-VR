using System.Collections;
using System.Collections.Generic;
using BNG;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Place on the <b>parent</b> of all real weld bars. When every referenced <see cref="weldbar"/>
/// is snapped (and optionally when <see cref="mergeAfterEightSpotWelds"/> has finished all eight steps),
/// child bars merge into one <see cref="Grabbable"/> on this root.
/// </summary>
public class WeldbarAssemblyRoot : MonoBehaviour, IWeldStepCompletable
{
    [Tooltip("The bars that must all be IsSnapped before merging. Leave empty with Auto Find to collect weldbar under this parent.")]
    public weldbar[] weldbars;

    [Tooltip("Fill Weldbars from GetComponentsInChildren (includes nested children).")]
    public bool autoFindWeldbarsInChildren = true;

    [Header("Merge timing")]
    [Tooltip("If assigned, bars do not merge until this sequence completes all weld steps after the four snaps. Leave empty for legacy: merge immediately when all bars are snapped.")]
    public SequentialWeldRevealSequence mergeAfterEightSpotWelds;

    [Header("Joined body")]
    public bool parentBodyUsesGravity = true;

    [Tooltip("Minimum Rigidbody mass if children had no mass.")]
    public float minimumMass = 0.5f;

    [Header("Merged pivot")]
    [Tooltip("After merge, moves this parent's transform to this empty's world position (optional rotation). Direct children keep their previous world poses so snaps stay aligned.")]
    public Transform mergedAssemblyAnchor;

    [Tooltip("If true, parent.rotation matches the anchor. If false, only parent.position.")]
    public bool mergedAnchorSetsRotation = true;

    [Tooltip("Second recenter empty for the second example-bar position (used for the post-weld snap in WeldbarMergedFlipSnapToAnchor). Not applied at merge time; exposed via SecondMergeRecenterAnchor.")]
    public Transform secondMergedAssemblyAnchor;

    [Header("Pass-through — snapped clamps / refs")]
    [Tooltip("Real clamps that should ignore collisions with this jointed frame while they are snapped (and during their unsnap cooldown). Leave empty with Auto Find to discover all clamps in the scene on merge.")]
    public clamp[] passThroughClamps;

    [Tooltip("Real ref pieces that should ignore collisions with this jointed frame while they are snapped (and during their unsnap cooldown). Leave empty with Auto Find to discover all refpieces in the scene on merge.")]
    public refpiece[] passThroughRefPieces;

    [Tooltip("When Pass Through Clamps / Ref Pieces are empty, FindObjectsOfType on merge and wire them to this assembly.")]
    public bool autoFindPassThroughFixtures = true;

    public UnityEvent onAllBarsSnappedAndMerged;

    bool _merged;
    bool _mergeStarted;

    void Awake()
    {
        if (autoFindWeldbarsInChildren || weldbars == null || weldbars.Length == 0)
            weldbars = GetComponentsInChildren<weldbar>(true);
    }

    void Update()
    {
        if (_merged || _mergeStarted)
            return;

        if (!AllWeldbarsSnapped())
            return;

        if (mergeAfterEightSpotWelds != null && !mergeAfterEightSpotWelds.HasCompletedAllWeldSteps)
            return;

        StartCoroutine(MergeRoutine());
    }

    bool AllWeldbarsSnapped()
    {
        if (weldbars == null || weldbars.Length == 0)
            return false;

        foreach (var b in weldbars)
        {
            if (b == null || !b.IsSnapped)
                return false;
        }

        return true;
    }

    /// <summary>True when every tracked weldbar reports snapped (before or after merge).</summary>
    public bool AreAllWeldbarsSnapped => AllWeldbarsSnapped();

    /// <summary>True once <see cref="MergeRoutine"/> has finished.</summary>
    public bool HasMergedAssembly => _merged;

    /// <summary>Step is complete once the child bars have merged into one assembly.</summary>
    public bool IsStepComplete => _merged;

    /// <summary>
    /// Same empty as <see cref="mergedAssemblyAnchor"/> — used by <see cref="WeldbarMergedFlipSnapToAnchor"/>.
    /// </summary>
    public Transform MergeRecenterAnchor => mergedAssemblyAnchor;

    /// <summary>
    /// Second recenter empty for the second example-bar position — used by <see cref="WeldbarMergedFlipSnapToAnchor"/>
    /// for the post-weld snap after the bottom dots are welded.
    /// </summary>
    public Transform SecondMergeRecenterAnchor => secondMergedAssemblyAnchor;

    IEnumerator MergeRoutine()
    {
        _mergeStarted = true;

        foreach (var b in weldbars)
        {
            if (b == null)
                continue;
            foreach (var g in b.GetComponentsInChildren<Grabbable>())
            {
                if (g != null && g.BeingHeld)
                    g.DropItem(true, true);
            }
        }

        float totalMass = 0f;
        var drag = new List<float>();
        var angularDrag = new List<float>();

        foreach (var b in weldbars)
        {
            if (b == null)
                continue;

            // Keep weldbar enabled so grab-me / second-round pulse can still run after merge.
            // Snap physics on the bar is gated off via NotifyMergedIntoAssembly.
            b.NotifyMergedIntoAssembly(this);

            foreach (var g in b.GetComponentsInChildren<Grabbable>())
            {
                if (g != null)
                    Destroy(g);
            }

            foreach (var rb in b.GetComponentsInChildren<Rigidbody>())
            {
                if (rb == null)
                    continue;
                totalMass += rb.mass;
                drag.Add(rb.drag);
                angularDrag.Add(rb.angularDrag);
                Destroy(rb);
            }
        }

        yield return null;

        ApplyMergedAssemblyAnchorPose();

        var parentRb = GetComponent<Rigidbody>();
        if (parentRb == null)
            parentRb = gameObject.AddComponent<Rigidbody>();

        parentRb.mass = Mathf.Max(totalMass, minimumMass);
        if (drag.Count > 0)
        {
            float d = 0f, ad = 0f;
            foreach (var v in drag) d += v;
            foreach (var v in angularDrag) ad += v;
            parentRb.drag = d / drag.Count;
            parentRb.angularDrag = ad / angularDrag.Count;
        }

        parentRb.isKinematic = false;
        parentRb.useGravity = parentBodyUsesGravity;

        var parentGrab = GetComponent<Grabbable>();
        if (parentGrab == null)
            parentGrab = gameObject.AddComponent<Grabbable>();

        WireChildGrabbables(parentGrab, transform);

        onAllBarsSnappedAndMerged?.Invoke();
        _merged = true;

        // Snapped clamps / refs must pass through this joint so unsnap is not blocked by the merged body.
        NotifySnappedFixturesOfMerge();
    }

    /// <summary>
    /// Ignore or restore Physics collisions between this merged assembly and <paramref name="otherRoot"/>
    /// (force:true so it works even when the SnapGuideCollisionIgnore master switch is off).
    /// </summary>
    public void SetCollisionsIgnoredWith(Transform otherRoot, bool ignore)
    {
        if (otherRoot == null)
            return;

        SnapGuideCollisionIgnore.SetIgnoredBetween(transform, otherRoot, ignore, force: true);
    }

    void NotifySnappedFixturesOfMerge()
    {
        if (autoFindPassThroughFixtures && (passThroughClamps == null || passThroughClamps.Length == 0))
            passThroughClamps = FindObjectsOfType<clamp>();

        if (autoFindPassThroughFixtures && (passThroughRefPieces == null || passThroughRefPieces.Length == 0))
            passThroughRefPieces = FindObjectsOfType<refpiece>();

        if (passThroughClamps != null)
        {
            for (int i = 0; i < passThroughClamps.Length; i++)
            {
                if (passThroughClamps[i] != null)
                    passThroughClamps[i].OnJointedFrameMerged(this);
            }
        }

        if (passThroughRefPieces != null)
        {
            for (int i = 0; i < passThroughRefPieces.Length; i++)
            {
                if (passThroughRefPieces[i] != null)
                    passThroughRefPieces[i].OnJointedFrameMerged(this);
            }
        }
    }

    void ApplyMergedAssemblyAnchorPose()
    {
        if (mergedAssemblyAnchor == null)
            return;

        Transform p = transform;
        var directChildren = new List<Transform>();
        foreach (Transform c in p)
            directChildren.Add(c);

        var worldPos = new Vector3[directChildren.Count];
        var worldRot = new Quaternion[directChildren.Count];
        for (int i = 0; i < directChildren.Count; i++)
        {
            worldPos[i] = directChildren[i].position;
            worldRot[i] = directChildren[i].rotation;
        }

        Vector3 anchorPos = mergedAssemblyAnchor.position;
        Quaternion anchorRot = mergedAssemblyAnchor.rotation;

        if (mergedAnchorSetsRotation)
            p.SetPositionAndRotation(anchorPos, anchorRot);
        else
            p.position = anchorPos;

        for (int i = 0; i < directChildren.Count; i++)
            directChildren[i].SetPositionAndRotation(worldPos[i], worldRot[i]);

        Physics.SyncTransforms();
    }

    static void WireChildGrabbables(Grabbable parentGrab, Transform root)
    {
        foreach (var col in root.GetComponentsInChildren<Collider>())
        {
            if (col == null || col.isTrigger)
                continue;

            if (col.GetComponent<Grabbable>() != null)
                continue;
            if (col.GetComponent<GrabbableChild>() != null)
                continue;

            var gc = col.gameObject.AddComponent<GrabbableChild>();
            gc.ParentGrabbable = parentGrab;
        }

        parentGrab.UpdateRigidbodyReference();
    }
}
