using BNG;
using UnityEngine;

/// <summary>
/// Standalone, repeatable 180° flip snap for the merged joint — the same behavior as the "dots" flip in
/// <see cref="WeldbarMergedFlipSnapToAnchor"/>, but with no post-weld reposition and no TMP. When the joint is
/// rotated ~180° on X or Z and a physics contact occurs (with an <see cref="exampleweldbar"/> collider, or any
/// collider if configured), it teleports to <see cref="snapAnchor"/> with an extra Euler offset, then freezes.
/// Arms only after <see cref="requireLinesComplete"/> reports all its weld lines welded (e.g. the 4 top lines).
/// </summary>
[DisallowMultipleComponent]
public class MergedFlip180OnContact : MonoBehaviour, IWeldStepCompletable
{
    [Header("Assembly")]
    [Tooltip("Merged assembly root. If null, tries GetComponent on this object. Used to gate on HasMergedAssembly.")]
    public WeldbarAssemblyRoot assemblyRoot;

    [Header("Start gate")]
    [Tooltip("This flip only arms once these weld lines report HasWeldedAllLines (e.g. the 4 top weld lines).")]
    public WeldLinesRevealOnSnap requireLinesComplete;

    [Header("Flip gate (degrees)")]
    [Tooltip("If true, euler check uses world-space eulerAngles. If false, local euler.")]
    public bool worldSpaceEulerCheck = true;

    [Tooltip("How close axis X or Z must be to 180° to allow the flip snap.")]
    public float euler180ToleranceDegrees = 25f;

    [Tooltip("If true, both X AND Z must be near 180. If false, either one satisfies the gate.")]
    public bool requireBothXAndZNear180 = false;

    [Header("Snap pose")]
    [Tooltip("Transform this joint teleports to on flip. Rotation is anchor.rotation * Euler(Snap Rotation Euler Offset).")]
    public Transform snapAnchor;

    [Tooltip("Extra Euler applied on top of the anchor rotation (default flips ~X like the dots flip).")]
    public Vector3 snapRotationEulerOffset = new Vector3(180f, 0f, 0f);

    [Tooltip("If true and Snap Anchor is null, the flip is skipped.")]
    public bool requireSnapAnchorAssigned = true;

    [Header("Contact gate")]
    [Tooltip("When on, flip is driven by collision/trigger with a collider whose parent has exampleweldbar. When off, the flip is polled each physics frame once the euler gate passes.")]
    public bool requireContactWithExampleWeldbar = true;

    [Tooltip("If true, the flip only evaluates while the merged joint Grabbable is held.")]
    public bool requireMergedJointHeldForFlip = true;

    [Header("After snap")]
    [Tooltip("Zero velocity then kinematic freeze after the flip snap.")]
    public bool freezeRigidbodyAfterSnap = true;

    [Tooltip("Drop from hands before teleporting so the pose does not fight the grab.")]
    public bool dropGrabBeforeSnapTeleport = true;

    [Header("Cooldowns (like weldbar)")]
    [Tooltip("After the flip snap, Grabbable is disabled this long so physics settles. Independent of Freeze; use 0 to skip.")]
    public float grabCooldownAfterFlipSnapSeconds = 0.5f;

    [Tooltip("After the joint is grabbed following a flip snap, the next flip stays blocked until this many seconds pass.")]
    public float snapCooldownAfterJointPickupSeconds = 0.35f;

    Rigidbody _rb;
    Grabbable _grab;

    bool _flipSnapSealedUntilGrab;
    float _nextFlipSnapEligibleTime;
    bool _flipGrabCooldownActive;
    float _flipGrabCooldownEndsAt;
    bool _kinematicLockedByFlipSnap;
    bool _hasEverCompletedFlip;
    bool _wasHeldLastFrame;

    void Awake()
    {
        if (assemblyRoot == null)
            assemblyRoot = GetComponent<WeldbarAssemblyRoot>();
    }

    void OnDisable()
    {
        if (_flipGrabCooldownActive && _grab != null)
        {
            _grab.enabled = true;
            _flipGrabCooldownActive = false;
        }
    }

    void EnsureRbAndGrab()
    {
        if (_rb == null)
            _rb = GetComponent<Rigidbody>();
        if (_grab == null)
            _grab = GetComponent<Grabbable>();
    }

    void Update()
    {
        ManagePickupCooldownGrabAndSeal();
    }

    void FixedUpdate()
    {
        if (requireContactWithExampleWeldbar)
            return;

        AttemptFlipSnap();
    }

    void ManagePickupCooldownGrabAndSeal()
    {
        EnsureRbAndGrab();

        if (_grab == null || !FlipArmed())
            return;

        if (_flipGrabCooldownActive && Time.time >= _flipGrabCooldownEndsAt)
        {
            _grab.enabled = true;
            _flipGrabCooldownActive = false;
        }

        bool held = _grab.BeingHeld;

        if (_flipSnapSealedUntilGrab && held)
        {
            if (_kinematicLockedByFlipSnap && _rb != null && freezeRigidbodyAfterSnap)
            {
                _rb.velocity = Vector3.zero;
                _rb.angularVelocity = Vector3.zero;
                _rb.isKinematic = false;
                _rb.constraints = RigidbodyConstraints.None;
                _kinematicLockedByFlipSnap = false;
                _grab.UpdateRigidbodyReference();
            }

            if (!_wasHeldLastFrame)
            {
                _flipSnapSealedUntilGrab = false;
                _nextFlipSnapEligibleTime =
                    Time.time + Mathf.Max(0f, snapCooldownAfterJointPickupSeconds);
            }
        }

        _wasHeldLastFrame = held;
    }

    bool FlipArmed()
    {
        if (assemblyRoot != null && !assemblyRoot.HasMergedAssembly)
            return false;
        if (requireLinesComplete != null && !requireLinesComplete.HasWeldedAllLines)
            return false;
        return true;
    }

    void AttemptFlipSnap()
    {
        EnsureRbAndGrab();

        if (_flipSnapSealedUntilGrab)
            return;

        if (Time.time < _nextFlipSnapEligibleTime)
            return;

        if (!FlipArmed())
            return;

        if (requireSnapAnchorAssigned && snapAnchor == null)
            return;

        if (requireMergedJointHeldForFlip && (_grab == null || !_grab.BeingHeld))
            return;

        if (!PassesFlipEulerGate())
            return;

        ApplySnap();
    }

    void TryFlipSnapAfterContact(Collider other)
    {
        if (!requireContactWithExampleWeldbar)
            return;

        if (other == null || other.GetComponentInParent<exampleweldbar>() == null)
            return;

        AttemptFlipSnap();
    }

    bool PassesFlipEulerGate()
    {
        Vector3 e = worldSpaceEulerCheck ? transform.eulerAngles : transform.localEulerAngles;
        bool xOk = EulerComponentNear180(e.x);
        bool zOk = EulerComponentNear180(e.z);
        return requireBothXAndZNear180 ? (xOk && zOk) : (xOk || zOk);
    }

    bool EulerComponentNear180(float eulerDeg)
    {
        float n = eulerDeg % 360f;
        if (n > 180f)
            n -= 360f;
        if (n <= -180f)
            n += 360f;

        return Mathf.Abs(Mathf.Abs(n) - 180f) <= euler180ToleranceDegrees;
    }

    void ApplySnap()
    {
        _flipSnapSealedUntilGrab = true;
        _hasEverCompletedFlip = true;

        EnsureRbAndGrab();

        if (dropGrabBeforeSnapTeleport && _grab != null && _grab.BeingHeld)
            _grab.DropItem(true, true);

        if (snapAnchor != null)
        {
            Quaternion snappedRot = snapAnchor.rotation * Quaternion.Euler(snapRotationEulerOffset);
            transform.SetPositionAndRotation(snapAnchor.position, snappedRot);
        }

        if (_rb != null)
        {
            _rb.velocity = Vector3.zero;
            _rb.angularVelocity = Vector3.zero;
            _rb.detectCollisions = true;
            if (freezeRigidbodyAfterSnap)
            {
                _rb.isKinematic = true;
                _rb.constraints = RigidbodyConstraints.FreezeAll;
                _kinematicLockedByFlipSnap = true;
            }
            else
            {
                _rb.isKinematic = false;
                _kinematicLockedByFlipSnap = false;
            }
        }

        if (_grab != null)
        {
            if (grabCooldownAfterFlipSnapSeconds > 0f)
            {
                _flipGrabCooldownActive = true;
                _flipGrabCooldownEndsAt = Time.time + Mathf.Max(0f, grabCooldownAfterFlipSnapSeconds);
                if (_grab.BeingHeld)
                    _grab.DropItem(true, true);
                _grab.enabled = false;
            }

            _grab.UpdateRigidbodyReference();
        }

        Physics.SyncTransforms();
    }

    void OnCollisionEnter(Collision collision)
    {
        if (collision.collider != null)
            TryFlipSnapAfterContact(collision.collider);
    }

    void OnCollisionStay(Collision collision)
    {
        if (collision.collider != null)
            TryFlipSnapAfterContact(collision.collider);
    }

    void OnTriggerEnter(Collider other)
    {
        TryFlipSnapAfterContact(other);
    }

    void OnTriggerStay(Collider other)
    {
        TryFlipSnapAfterContact(other);
    }

    /// <summary>True after at least one successful flip snap (stays true; flip can repeat).</summary>
    public bool HasCompletedFlip => _hasEverCompletedFlip;

    /// <summary>Step is complete once the flip snap has happened.</summary>
    public bool IsStepComplete => _hasEverCompletedFlip;
}
