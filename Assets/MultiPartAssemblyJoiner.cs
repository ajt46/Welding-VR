using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using BNG;

/// <summary>
/// Place each moving part into its example trigger zone. When every slot reports overlap with the
/// correct collider, parts are snapped (optional), reparented under one root with a single Rigidbody,
/// and wired so BNG can grab any child collider as one assembly via <see cref="GrabbableChild"/>.
/// </summary>
public class MultiPartAssemblyJoiner : MonoBehaviour
{
    [System.Serializable]
    public class AssemblyPartSlot
    {
        [Tooltip("Moving piece root — reparented under the assembly root when complete.")]
        public Transform movingPartRoot;

        [Tooltip("Collider on the moving piece that must overlap the example placement trigger (e.g. its box). If null, any collider under Moving Part Root counts.")]
        public Collider movingPartContactCollider;

        [Tooltip("World pose the real piece moves to when it is placed on this example (empty child on the example, offset as needed).")]
        public Transform optionalSnapTarget;

        [Tooltip("When the correct collider enters the example zone, snap Moving Part Root to Optional Snap Target.")]
        public bool snapWhenPlaced = true;

        [Tooltip("When placed, freeze all rigidbodies under Moving Part Root and disable gravity.")]
        public bool lockPhysicsWhenPlaced = true;

        [Tooltip("When placed, disable Grabbable components under Moving Part Root so the part stays locked.")]
        public bool disableGrabWhenPlaced = true;
    }

    [Header("Slots (e.g. four pairs)")]
    public AssemblyPartSlot[] slots = new AssemblyPartSlot[4];

    [Header("Joined assembly")]
    [Tooltip("If set, children are parented here and this object gets the Rigidbody + Grabbable. If null, a new GameObject is created.")]
    public Transform assemblyRoot;

    [Tooltip("If Assembly Root is assigned, move it to the centroid of all part roots before parenting.")]
    public bool moveAssemblyRootToPartsCenter = true;

    [Header("Physics on joined root")]
    public bool joinedBodyUseGravity = true;

    [Tooltip("Minimum mass for the combined Rigidbody if all child masses were zero.")]
    public float minimumJoinedMass = 0.5f;

    [Header("Events")]
    public UnityEvent onAssemblyCompleted;

    bool[] _filled;
    bool[] _snapApplied;
    bool _completed;

    void Awake()
    {
        if (slots == null || slots.Length == 0)
            slots = new AssemblyPartSlot[0];
        _filled = new bool[slots.Length];
        _snapApplied = new bool[slots.Length];
    }

    /// <summary>
    /// Call from <see cref="AssemblyPlacementTrigger"/> (e.g. OnTriggerStay) when <paramref name="other"/> may be a valid part.
    /// </summary>
    public void NotifyPartInPlacementZone(int slotIndex, Collider other)
    {
        if (_completed || slotIndex < 0 || slotIndex >= slots.Length || other == null)
            return;

        if (!IsPartColliderForSlot(slotIndex, other))
            return;

        if (!_filled[slotIndex])
        {
            if (ShouldApplySlotSnap(slotIndex) && !_snapApplied[slotIndex])
            {
                SnapSlotToTarget(slotIndex);
                _snapApplied[slotIndex] = true;
            }

            ApplyPlacedLockState(slotIndex);
            _filled[slotIndex] = true;
        }

        TryCompleteAssembly();
    }

    /// <summary>
    /// Optional: revoke a slot when the part leaves the example trigger (only before completion).
    /// </summary>
    public void NotifyPartLeftPlacementZone(int slotIndex, Collider other, bool revokeIfMatching)
    {
        if (_completed || !revokeIfMatching || slotIndex < 0 || slotIndex >= slots.Length || other == null)
            return;

        if (!IsPartColliderForSlot(slotIndex, other))
            return;

        _filled[slotIndex] = false;
        _snapApplied[slotIndex] = false;
    }

    public bool IsSlotFilled(int slotIndex)
    {
        if (_filled == null || slotIndex < 0 || slotIndex >= _filled.Length)
            return false;
        return _filled[slotIndex];
    }

    bool ShouldApplySlotSnap(int index)
    {
        var slot = slots[index];
        return slot.snapWhenPlaced && slot.optionalSnapTarget != null && slot.movingPartRoot != null;
    }

    void SnapSlotToTarget(int index)
    {
        var slot = slots[index];
        var target = slot.optionalSnapTarget;
        var root = slot.movingPartRoot;
        if (target == null || root == null)
            return;

        root.SetPositionAndRotation(target.position, target.rotation);

        foreach (var rb in root.GetComponentsInChildren<Rigidbody>())
        {
            if (rb == null)
                continue;
            rb.velocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }
    }

    void ApplyPlacedLockState(int index)
    {
        var slot = slots[index];
        var root = slot.movingPartRoot;
        if (root == null)
            return;

        if (slot.lockPhysicsWhenPlaced)
        {
            foreach (var rb in root.GetComponentsInChildren<Rigidbody>())
            {
                if (rb == null)
                    continue;
                rb.velocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
                rb.isKinematic = true;
                rb.useGravity = false;
            }
        }

        if (slot.disableGrabWhenPlaced)
        {
            foreach (var grab in root.GetComponentsInChildren<Grabbable>(true))
            {
                if (grab == null)
                    continue;
                if (grab.BeingHeld)
                    grab.DropItem(true, true);
                grab.enabled = false;
            }

            foreach (var ug in root.GetComponentsInChildren<ungroundedgrabbable>(true))
                ug.SetGroundedManual(true);
        }
    }

    bool IsPartColliderForSlot(int index, Collider other)
    {
        var slot = slots[index];
        if (slot.movingPartRoot == null)
            return false;

        if (slot.movingPartContactCollider != null)
            return other == slot.movingPartContactCollider;

        return other.transform == slot.movingPartRoot || other.transform.IsChildOf(slot.movingPartRoot);
    }

    void TryCompleteAssembly()
    {
        for (int i = 0; i < _filled.Length; i++)
        {
            if (!_filled[i])
                return;
        }

        CompleteAssembly();
    }

    void CompleteAssembly()
    {
        _completed = true;

        Transform root = assemblyRoot;
        if (root == null)
        {
            var go = new GameObject("JoinedAssembly");
            root = go.transform;
            root.SetPositionAndRotation(ComputePartsCenter(), Quaternion.identity);
        }
        else if (moveAssemblyRootToPartsCenter)
        {
            root.position = ComputePartsCenter();
        }

        float totalMass = 0f;
        var dragSamples = new List<float>();
        var angularDragSamples = new List<float>();

        foreach (var slot in slots)
        {
            if (slot.movingPartRoot == null)
                continue;

            foreach (var rb in slot.movingPartRoot.GetComponentsInChildren<Rigidbody>())
            {
                if (rb == null)
                    continue;
                totalMass += rb.mass;
                dragSamples.Add(rb.drag);
                angularDragSamples.Add(rb.angularDrag);
                Destroy(rb);
            }
        }

        foreach (var slot in slots)
        {
            if (slot.movingPartRoot != null)
                slot.movingPartRoot.SetParent(root, true);
        }

        var parentRb = root.GetComponent<Rigidbody>();
        if (parentRb == null)
            parentRb = root.gameObject.AddComponent<Rigidbody>();

        parentRb.mass = Mathf.Max(totalMass, minimumJoinedMass);
        if (dragSamples.Count > 0)
        {
            float d = 0f, ad = 0f;
            foreach (var v in dragSamples) d += v;
            foreach (var v in angularDragSamples) ad += v;
            parentRb.drag = d / dragSamples.Count;
            parentRb.angularDrag = ad / angularDragSamples.Count;
        }

        parentRb.isKinematic = false;
        parentRb.useGravity = joinedBodyUseGravity;

        var grab = root.GetComponent<Grabbable>();
        if (grab == null)
            grab = root.gameObject.AddComponent<Grabbable>();

        WireChildGrabbables(grab, root);

        onAssemblyCompleted?.Invoke();
    }

    void WireChildGrabbables(Grabbable parentGrab, Transform root)
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

    Vector3 ComputePartsCenter()
    {
        Vector3 sum = Vector3.zero;
        int n = 0;
        foreach (var s in slots)
        {
            if (s.movingPartRoot == null)
                continue;
            sum += s.movingPartRoot.position;
            n++;
        }

        return n > 0 ? sum / n : transform.position;
    }
}
