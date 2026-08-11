using UnityEngine;
using BNG;

/// <summary>
/// Grounding: when <see cref="IsGrounded"/> is true, <see cref="Grabbable"/> is disabled.
/// Welding sheet: optional shared <see cref="exampleweldingsheet"/> — the same component can be assigned on
/// every panel; the sheet's <see cref="exampleweldingsheet.connectedObject"/> shows when this grabbable is held and in range.
/// Only one workpiece may be snapped to that sheet at a time (see <see cref="exampleweldingsheet.SheetAllowsWorkpiece"/>).
/// On sheet snap, freezes like <see cref="clamp"/> (kinematic + FreezeAll) so collisions cannot knock the panel;
/// <see cref="GrabbableKinematicBodySetup"/> is disabled/destroyed per <see cref="kinematicSetupOnSnap"/> and restored on grab.
/// </summary>
public class ungroundedgrabbable : MonoBehaviour
{
    public enum KinematicSetupOnSheetSnap
    {
        Disable,
        DestroyComponent
    }

    [Header("References")]
    [Tooltip("BNG Grabbable on this object (or assign explicitly).")]
    public Grabbable grabbable;

    [Tooltip("If set, grounded follows clamp.IsGrounded() (snap/ground).")]
    public clamp clampSource;

    [Tooltip("When the Clamp Source is snapped/grounded, freeze this panel's Rigidbody (and remove it if GrabbableKinematicBodySetup.Remove Rigidbody While Snapped is on). Stays locked until the clamp is released (unless still sheet-snapped).")]
    public bool lockRigidbodyWhileClampGrounded = true;

    [Tooltip("If set, used when snapped to the sheet. If null, resolved on the same GameObject as Grabbable (required for BNG).")]
    public GrabbableKinematicBodySetup kinematicBodySetup;

    [Tooltip("Disable: only turns the component off (preferred with Freeze After Snap). Destroy: removes the component (Rigidbody can be removed too — see below).")]
    public KinematicSetupOnSheetSnap kinematicSetupOnSnap = KinematicSetupOnSheetSnap.Disable;

    [Tooltip("When using Destroy: also destroy the Rigidbody on the Grabbable GameObject. Leave OFF when Freeze After Snap is on — freeze needs the Rigidbody (same as clamp).")]
    public bool alsoDestroyRigidbodyOnSnap = false;

    [Header("Grounded when no clamp")]
    [Tooltip("Used only if Clamp Source is null. Toggle in Inspector or set from code via SetGroundedManual.")]
    public bool groundedManual = false;

    [Header("Welding sheet — connected object visibility")]
    [Tooltip("The one welding sheet anchor in the scene (same reference on Mild / Stainless / Aluminium panels). Occupancy and exclusivity are handled on this component.")]
    public exampleweldingsheet weldingSheet;

    [Tooltip("Distance from Distance Reference to the sheet's proximity point to show the connected object.")]
    public float revealDistance = 0.35f;

    [Tooltip("If null, uses this transform's position.")]
    public Transform distanceReference;

    [Tooltip("If true, hide the connected object after a successful snap to the sheet.")]
    public bool hideConnectedObjectAfterSnap = true;

    [Header("Welding sheet — auto snap (clamp-like)")]
    [Tooltip("If true, snapping to the sheet is allowed (distance and/or collision).")]
    public bool enableAutoSnapToSheet = true;

    [Tooltip("If true, snap when within Snap Distance of the sheet (proximity).")]
    public bool snapUsingDistance = true;

    [Tooltip("If true, snap when this object's collider hits the welding sheet's snap colliders (like clamp + collision).")]
    public bool snapOnCollision = true;

    [Tooltip("When distance snap is on: snap when at or below this distance to the sheet proximity point.")]
    public float snapDistance = 0.12f;

    [Tooltip("Must match exampleweldingsheet.nameofasset when that field is non-empty (per workpiece key).")]
    public string sheetAssetKey = "";

    [Tooltip("When true, snap only if the grabbable is currently held.")]
    public bool requireHeldForSheetSnap = true;

    [Tooltip("If true, apply the snap transform's local scale when snapping (same idea as clamp).")]
    public bool applySheetSnapScale = false;

    [Header("Welding sheet — snap alignment")]
    [Tooltip("Extra rotation applied after the snap transform (degrees in the snap's local axes). Typical fix for wrong facing: (0, 180, 0) if the mesh is flipped 180° on Y.")]
    public Vector3 snapRotationOffsetEuler = Vector3.zero;

    [Tooltip("Extra position in snap local space (meters), applied after snap position. Use if the pivot sits off the surface.")]
    public Vector3 snapPositionOffsetLocal = Vector3.zero;

    [Header("Post sheet snap")]
    [Tooltip("Like clamp: after sheet snap, Rigidbody becomes kinematic with FreezeAll so other objects/player cannot knock it out of pose. Unfreezes when grabbed again.")]
    public bool freezeAfterSnap = true;

    [Tooltip("After sheet snap, grabbable stays disabled this many seconds (physics settle), then re-enables.")]
    public float grabCooldownAfterSheetSnapSeconds = 0.5f;

    [Tooltip("After you grab following a sheet snap, snap is blocked for this many seconds.")]
    public float sheetSnapCooldownAfterPickupSeconds = 0.35f;

    [Tooltip("During the post-pickup cooldown (and while seated), ignore physics between this workpiece and the example welding sheet so unsnapping is smooth — collisions pass through.")]
    public bool ignoreCollisionsDuringUnsnapCooldown = true;

    [Tooltip("If true, set Grounded Manual to true after a successful sheet snap (stays put until you clear grounding).")]
    public bool setGroundedManualAfterSheetSnap = false;

    [Header("Debug")]
    public bool debugSheet = false;

    Rigidbody rb;
    bool lastGrounded;

    bool sheetSnapped;
    float nextSheetSnapEligibleTime;
    bool wasHeldLastFrame;
    bool grabCooldownAfterSheetSnapActive;
    float grabCooldownEndsAtTime;
    bool kinematicSetupRemovedBySheetSnap;
    bool kinematicSetupWasDestroyed;
    GameObject kinematicSetupHost;
    bool rigidbodyFrozenBySheetSnap;
    bool rigidbodyLockedByClampGrounded;
    bool sheetCollisionsIgnored;

    void Awake()
    {
        if (grabbable == null)
            grabbable = GetComponent<Grabbable>() ?? GetComponentInParent<Grabbable>();

        ResolveKinematicBodySetupReference();

        rb = GetComponent<Rigidbody>() ?? GetComponentInParent<Rigidbody>();

        if (rb != null && rb.gameObject != gameObject && rb.gameObject.GetComponent<WeldingSheetSnapRelay>() == null)
        {
            var relay = rb.gameObject.AddComponent<WeldingSheetSnapRelay>();
            relay.owner = this;
        }
    }

    void OnDestroy()
    {
        SetUnsnapCollisionsIgnored(false);
        if (weldingSheet != null)
            weldingSheet.NotifySnapReleased(this);
    }

    /// <summary>
    /// BNG expects <see cref="GrabbableKinematicBodySetup"/> on the same object as <see cref="Grabbable"/>; this object may be on a child.
    /// </summary>
    void ResolveKinematicBodySetupReference()
    {
        if (kinematicBodySetup != null)
            return;
        if (grabbable != null)
        {
            kinematicBodySetup = grabbable.GetComponent<GrabbableKinematicBodySetup>();
            if (kinematicBodySetup == null)
                kinematicBodySetup = grabbable.GetComponentInChildren<GrabbableKinematicBodySetup>(true);
        }
        if (kinematicBodySetup == null)
            kinematicBodySetup = GetComponent<GrabbableKinematicBodySetup>();
        if (kinematicBodySetup == null)
            kinematicBodySetup = GetComponentInChildren<GrabbableKinematicBodySetup>(true);
    }

    void OnCollisionEnter(Collision collision)
    {
        if (collision != null)
            HandleSheetSnapContact(collision.collider, "collision");
    }

    void OnCollisionStay(Collision collision)
    {
        if (collision != null)
            HandleSheetSnapContact(collision.collider, "collision stay");
    }

    void OnTriggerEnter(Collider other)
    {
        HandleSheetSnapContact(other, "trigger");
    }

    /// <summary>
    /// Called from this component's physics messages or <see cref="WeldingSheetSnapRelay"/> when the rigidbody is on a child.
    /// </summary>
    public void HandleSheetSnapContact(Collider other, string source = "")
    {
        if (!enableAutoSnapToSheet || !snapOnCollision || weldingSheet == null || other == null)
            return;

        if (sheetSnapped)
            return;

        if (Time.time < nextSheetSnapEligibleTime)
            return;

        if (!weldingSheet.IsSnapTargetCollider(other))
            return;

        if (!weldingSheet.SheetAllowsWorkpiece(this))
            return;

        if (IsColliderPartOfThisHierarchy(other))
            return;

        if (requireHeldForSheetSnap && !IsHeld())
            return;

        if (!IsSheetAssetMatch())
            return;

        SnapToWeldingSheet();

        if (debugSheet)
            Debug.Log("ungroundedgrabbable: sheet snap from " + source);
    }

    bool IsColliderPartOfThisHierarchy(Collider other)
    {
        Transform t = other.transform;
        return t == transform || t.IsChildOf(transform);
    }

    void OnDisable()
    {
        if (grabCooldownAfterSheetSnapActive && grabbable != null)
        {
            grabbable.enabled = true;
            grabCooldownAfterSheetSnapActive = false;
        }
        SetUnsnapCollisionsIgnored(false);
    }

    void Start()
    {
        lastGrounded = !IsGroundedNow();
        ApplyGroundedState(IsGroundedNow());
        lastGrounded = IsGroundedNow();
        wasHeldLastFrame = IsHeld();
    }

    void Update()
    {
        if (grabCooldownAfterSheetSnapActive && grabbable != null && Time.time >= grabCooldownEndsAtTime)
        {
            grabbable.enabled = true;
            grabCooldownAfterSheetSnapActive = false;
            if (debugSheet)
                Debug.Log("ungroundedgrabbable: sheet grab cooldown ended");
        }

        // End pass-through once post-pickup cooldown finishes (and not still seated).
        if (sheetCollisionsIgnored && !sheetSnapped && !grabCooldownAfterSheetSnapActive &&
            Time.time >= nextSheetSnapEligibleTime)
            SetUnsnapCollisionsIgnored(false);

        bool held = IsHeld();

        // Same as clamp: unfreeze while held, clear snap on the grab edge —
        // but keep Rigidbody locked while the work clamp is still seated.
        if (sheetSnapped && held)
        {
            if (rigidbodyFrozenBySheetSnap && !IsClampGroundedRigidbodyLockActive())
                UnfreezeRigidbodyAfterSheetSnapGrab();

            if (!wasHeldLastFrame)
            {
                sheetSnapped = false;
                nextSheetSnapEligibleTime = Time.time + Mathf.Max(0f, sheetSnapCooldownAfterPickupSeconds);
                ReleaseSheetSnapKinematicSetup();
                if (weldingSheet != null)
                {
                    weldingSheet.NotifySnapReleased(this);
                    if (hideConnectedObjectAfterSnap)
                        weldingSheet.SetConnectedVisible(false, this);
                }

                // Pass through sheet collisions for the unsnap cooldown window (like clamp).
                if (ignoreCollisionsDuringUnsnapCooldown)
                    SetUnsnapCollisionsIgnored(true);

                // Clamp still grounded: keep freeze/disable even after leaving the sheet seat.
                if (IsClampGroundedRigidbodyLockActive())
                    ApplyClampGroundedRigidbodyLock();

                if (debugSheet)
                    Debug.Log("ungroundedgrabbable: sheet snap cleared on grab");
            }
        }

        if (weldingSheet != null)
            UpdateWeldingSheetProximityAndSnap(held);

        wasHeldLastFrame = held;

        bool g = IsGroundedNow();
        if (g == lastGrounded)
        {
            if (g && grabbable != null && grabbable.enabled)
                grabbable.enabled = false;
        }
        else
        {
            lastGrounded = g;
            ApplyGroundedState(g);
        }
    }

    bool IsHeld()
    {
        return grabbable != null && grabbable.BeingHeld;
    }

    void UpdateWeldingSheetProximityAndSnap(bool held)
    {
        Vector3 from = distanceReference != null ? distanceReference.position : transform.position;
        float dist = Vector3.Distance(from, weldingSheet.GetProximityPoint());

        bool hideBecauseSnapped = sheetSnapped && hideConnectedObjectAfterSnap;
        bool showConnected = held && dist <= revealDistance && !hideBecauseSnapped && weldingSheet.SheetAllowsWorkpiece(this);
        // Requester vote — do not let non-held panels force the shared ghost off for everyone.
        weldingSheet.SetConnectedVisible(showConnected, this);

        if (!enableAutoSnapToSheet || !snapUsingDistance || sheetSnapped)
            return;

        if (Time.time < nextSheetSnapEligibleTime)
            return;

        if (dist > snapDistance)
            return;

        if (requireHeldForSheetSnap && !held)
            return;

        if (!IsSheetAssetMatch())
            return;

        if (!weldingSheet.SheetAllowsWorkpiece(this))
            return;

        SnapToWeldingSheet();
    }

    bool IsSheetAssetMatch()
    {
        if (weldingSheet == null)
            return false;
        if (string.IsNullOrEmpty(weldingSheet.nameofasset))
            return true;
        return weldingSheet.nameofasset == sheetAssetKey;
    }

    void SnapToWeldingSheet()
    {
        if (weldingSheet == null)
            return;

        if (!weldingSheet.SheetAllowsWorkpiece(this))
            return;

        Transform snap = weldingSheet.GetSnapTransform();
        if (snap == null)
            return;

        Quaternion worldRot = snap.rotation * Quaternion.Euler(snapRotationOffsetEuler);
        Vector3 worldPos = snap.position + snap.rotation * snapPositionOffsetLocal;
        transform.SetPositionAndRotation(worldPos, worldRot);

        if (applySheetSnapScale)
            transform.localScale = snap.localScale;

        sheetSnapped = true;
        weldingSheet.NotifySnapOccupied(this);

        if (hideConnectedObjectAfterSnap)
            weldingSheet.SetConnectedVisible(false, this);

        // Ignore while seated so lift-off after grab is already non-blocking.
        if (ignoreCollisionsDuringUnsnapCooldown)
            SetUnsnapCollisionsIgnored(true);

        ApplyKinematicSetupRemovalOnSnap();

        // Always freeze first (clamp-style) so the panel cannot be knocked around.
        FreezeRigidbodyAfterSheetSnap();

        // Optional welding path: then remove the Rigidbody entirely until a hand touches it.
        ResolveKinematicBodySetupReference();
        if (kinematicBodySetup != null && kinematicBodySetup.removeRigidbodyWhileSnapped)
            kinematicBodySetup.NotifyObjectSnapped();

        if (setGroundedManualAfterSheetSnap)
            groundedManual = true;

        if (grabCooldownAfterSheetSnapSeconds > 0f && grabbable != null)
        {
            grabCooldownAfterSheetSnapActive = true;
            grabCooldownEndsAtTime = Time.time + Mathf.Max(0f, grabCooldownAfterSheetSnapSeconds);
            if (grabbable.BeingHeld)
                grabbable.DropItem(true, true);
            grabbable.enabled = false;
            if (debugSheet)
                Debug.Log("ungroundedgrabbable: sheet snap grab cooldown started");
        }

        if (debugSheet)
            Debug.Log("ungroundedgrabbable: snapped to welding sheet");
    }

    void ResolveRigidbody()
    {
        rb = GetComponent<Rigidbody>()
            ?? GetComponentInParent<Rigidbody>()
            ?? (grabbable != null ? grabbable.GetComponent<Rigidbody>() : null);
    }

    /// <summary>Clamp-style lock: kinematic + FreezeAll so collisions cannot shove the seated panel.</summary>
    void FreezeRigidbodyAfterSheetSnap()
    {
        rigidbodyFrozenBySheetSnap = false;
        if (!freezeAfterSnap)
            return;

        ResolveRigidbody();
        if (rb == null)
        {
            if (debugSheet)
                Debug.LogWarning("ungroundedgrabbable: Freeze After Snap is on but no Rigidbody was found (check Also Destroy Rigidbody On Snap).", this);
            return;
        }

        rb.velocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
        rb.isKinematic = true;
        rb.constraints = RigidbodyConstraints.FreezeAll;
        rigidbodyFrozenBySheetSnap = true;

        if (grabbable != null)
            grabbable.UpdateRigidbodyReference();

        if (debugSheet)
            Debug.Log("ungroundedgrabbable: rigidbody frozen after sheet snap (clamp-style)");
    }

    void UnfreezeRigidbodyAfterSheetSnapGrab()
    {
        if (!rigidbodyFrozenBySheetSnap)
            return;

        ResolveRigidbody();
        if (rb == null)
        {
            rigidbodyFrozenBySheetSnap = false;
            return;
        }

        rb.velocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
        rb.isKinematic = false;
        rb.constraints = RigidbodyConstraints.None;
        rigidbodyFrozenBySheetSnap = false;

        if (grabbable != null)
            grabbable.UpdateRigidbodyReference();

        if (debugSheet)
            Debug.Log("ungroundedgrabbable: released snap freeze for grab");
    }

    void ApplyKinematicSetupRemovalOnSnap()
    {
        ResolveKinematicBodySetupReference();
        if (kinematicBodySetup == null)
        {
            if (debugSheet)
                Debug.LogWarning("ungroundedgrabbable: No GrabbableKinematicBodySetup found. Add it to the same GameObject as Grabbable (or assign the field).", this);
            return;
        }

        // Keep enabled so snap can remove the Rigidbody and hand contact can restore it.
        if (kinematicBodySetup.removeRigidbodyWhileSnapped || kinematicBodySetup.restoreRigidbodyOnHandCollision)
        {
            if (debugSheet)
                Debug.Log("ungroundedgrabbable: leaving GrabbableKinematicBodySetup enabled for snap/hand Rigidbody toggle");
            return;
        }

        kinematicSetupRemovedBySheetSnap = true;
        kinematicSetupHost = kinematicBodySetup.gameObject;

        if (kinematicSetupOnSnap == KinematicSetupOnSheetSnap.Disable)
        {
            kinematicBodySetup.enabled = false;
            kinematicSetupWasDestroyed = false;
        }
        else
        {
            Destroy(kinematicBodySetup);
            kinematicBodySetup = null;
            kinematicSetupWasDestroyed = true;

            // Destroying the RB prevents clamp-style freeze; skip when freezeAfterSnap is on.
            if (alsoDestroyRigidbodyOnSnap && !freezeAfterSnap && grabbable != null)
            {
                Rigidbody orb = grabbable.GetComponent<Rigidbody>();
                if (orb != null)
                    Destroy(orb);
            }
        }

        if (grabbable != null)
            grabbable.UpdateRigidbodyReference();
    }

    /// <summary>Called when snap/hand logic removes the Rigidbody while sheet-snapped.</summary>
    public void NotifyRigidbodyRemovedByBToggle()
    {
        rigidbodyFrozenBySheetSnap = false;
        rb = null;
    }

    /// <summary>Re-apply clamp-style freeze after a Rigidbody is restored while still sheet-snapped.</summary>
    public void ReapplySheetSnapFreezeIfNeeded()
    {
        if (!sheetSnapped || !freezeAfterSnap)
            return;
        FreezeRigidbodyAfterSheetSnap();
    }

    void ReleaseSheetSnapKinematicSetup()
    {
        if (!kinematicSetupRemovedBySheetSnap)
            return;

        if (kinematicSetupWasDestroyed)
        {
            if (kinematicSetupHost != null && kinematicSetupHost.GetComponent<GrabbableKinematicBodySetup>() == null)
                kinematicBodySetup = kinematicSetupHost.AddComponent<GrabbableKinematicBodySetup>();
            kinematicSetupWasDestroyed = false;
            kinematicSetupHost = null;
        }
        else if (kinematicBodySetup != null)
        {
            kinematicBodySetup.enabled = true;
        }

        kinematicSetupRemovedBySheetSnap = false;
        if (grabbable != null)
            grabbable.UpdateRigidbodyReference();
    }

    /// <summary>Grounded from clamp if assigned, otherwise <see cref="groundedManual"/>.</summary>
    public bool IsGrounded()
    {
        return IsGroundedNow();
    }

    bool IsGroundedNow()
    {
        if (clampSource != null)
            return clampSource.IsGrounded();
        return groundedManual;
    }

    /// <summary>Set grounded when not using <see cref="clampSource"/>.</summary>
    public void SetGroundedManual(bool grounded)
    {
        groundedManual = grounded;
    }

    /// <summary>True after auto-snap to the welding sheet until the next grab clears it.</summary>
    public bool IsSnappedToWeldingSheet() => sheetSnapped;

    /// <summary>
    /// Ignore physics between this workpiece and the example welding sheet (guide / snap / connected preview).
    /// Uses force:true so it works even when the global SnapGuideCollisionIgnore master switch is off.
    /// </summary>
    void SetUnsnapCollisionsIgnored(bool ignore)
    {
        if (weldingSheet == null)
        {
            sheetCollisionsIgnored = false;
            return;
        }

        if (!ignore)
        {
            if (!sheetCollisionsIgnored)
                return;

            SnapGuideCollisionIgnore.SetIgnoredBetween(
                GetComponentsInChildren<Collider>(true),
                weldingSheet.GetCollidersForIgnore(),
                false,
                force: true);

            sheetCollisionsIgnored = false;
            if (debugSheet)
                Debug.Log("ungroundedgrabbable: restored collisions with example welding sheet");
            return;
        }

        if (!ignoreCollisionsDuringUnsnapCooldown)
            return;

        SnapGuideCollisionIgnore.SetIgnoredBetween(
            GetComponentsInChildren<Collider>(true),
            weldingSheet.GetCollidersForIgnore(),
            true,
            force: true);

        sheetCollisionsIgnored = true;
        if (debugSheet)
            Debug.Log("ungroundedgrabbable: ignoring collisions with example welding sheet (unsnap pass-through)");
    }

    /// <summary>True while clamp-grounded lock should keep this panel's Rigidbody frozen/disabled.</summary>
    public bool IsClampGroundedRigidbodyLockActive()
    {
        return lockRigidbodyWhileClampGrounded && clampSource != null && clampSource.IsGrounded();
    }

    void ApplyGroundedState(bool grounded)
    {
        if (grabbable != null)
        {
            if (grounded)
            {
                if (grabbable.BeingHeld)
                    grabbable.DropItem(false, true);

                grabbable.enabled = false;
            }
            else if (!grabCooldownAfterSheetSnapActive)
            {
                grabbable.enabled = true;
            }
        }

        if (grounded)
            ApplyClampGroundedRigidbodyLock();
        else
            ReleaseClampGroundedRigidbodyLock();
    }

    void ApplyClampGroundedRigidbodyLock()
    {
        if (!lockRigidbodyWhileClampGrounded)
            return;

        FreezeRigidbodyAfterSheetSnap();

        ResolveKinematicBodySetupReference();
        if (kinematicBodySetup != null && kinematicBodySetup.removeRigidbodyWhileSnapped)
            kinematicBodySetup.NotifyObjectSnapped();

        rigidbodyLockedByClampGrounded = true;

        if (debugSheet)
            Debug.Log("ungroundedgrabbable: Rigidbody locked because clamp is grounded");
    }

    void ReleaseClampGroundedRigidbodyLock()
    {
        if (!rigidbodyLockedByClampGrounded)
            return;

        rigidbodyLockedByClampGrounded = false;

        // Sheet seat still owns the lock.
        if (sheetSnapped)
        {
            FreezeRigidbodyAfterSheetSnap();
            ResolveKinematicBodySetupReference();
            if (kinematicBodySetup != null && kinematicBodySetup.removeRigidbodyWhileSnapped)
                kinematicBodySetup.NotifyObjectSnapped();
            return;
        }

        ResolveKinematicBodySetupReference();
        if (kinematicBodySetup != null)
            kinematicBodySetup.ForceRestoreRigidbody();

        UnfreezeRigidbodyAfterSheetSnapGrab();

        if (debugSheet)
            Debug.Log("ungroundedgrabbable: Rigidbody unlocked because clamp was released");
    }
}
