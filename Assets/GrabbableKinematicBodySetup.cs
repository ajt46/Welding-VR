using UnityEngine;
using BNG;
using TMPro;

/// <summary>
/// BNG <see cref="Grabbable"/> only moves objects that have a <see cref="Rigidbody"/>
/// on the <b>same</b> GameObject (see <c>canBeMoved()</c> in Grabbable).
/// This component adds a minimal <see cref="Rigidbody"/> so you do not have to add one by hand:
/// kinematic, no gravity — so there is no dynamic physics simulation unless you change it.
/// Optional snap mode: remove the Rigidbody while seated/snapped (welding-friendly), then
/// re-add it when a player hand / <see cref="Grabber"/> touches this object's colliders.
/// </summary>
[RequireComponent(typeof(Grabbable))]
[DisallowMultipleComponent]
[DefaultExecutionOrder(-1000)]
public class GrabbableKinematicBodySetup : MonoBehaviour
{
    [Tooltip("New rigidbodies are kinematic (no dynamic simulation) and ignore gravity.")]
    public bool kinematicNoGravity = true;

    [Tooltip("If true, freeze all rotation on the added rigidbody (object only translates when grabbed).")]
    public bool freezeRotation = false;

    [Header("Snap — remove Rigidbody (optional)")]
    [Tooltip("After the panel freezes on snap, also destroy the Rigidbody until a hand touches it (welding tip-friendly). Leave OFF to only use freeze (clamp-style).")]
    public bool removeRigidbodyWhileSnapped = false;

    [Header("Hand — restore Rigidbody")]
    [Tooltip("After the Rigidbody was removed by snap, re-add it when a Grabber/hand collider touches this object's colliders.")]
    public bool restoreRigidbodyOnHandCollision = true;

    [Tooltip("Log snap remove / hand restore to the Console.")]
    public bool debugSnapHandRigidbody = false;

    [Header("Status (TMP)")]
    [Tooltip("Optional TextMeshPro that shows whether this object's Rigidbody is present / frozen / missing.")]
    public TMP_Text rigidbodyStatusText;

    [Tooltip("Shown when a Rigidbody is present and not fully freeze-locked.")]
    public string textWhenRigidbodyEnabled = "Rigidbody: enabled";

    [Tooltip("Shown when a Rigidbody is present and FreezeAll + kinematic (snap freeze).")]
    public string textWhenRigidbodyFrozen = "Rigidbody: frozen";

    [Tooltip("Shown when there is no Rigidbody on this GameObject.")]
    public string textWhenRigidbodyDisabled = "Rigidbody: disabled";

    Grabbable _grabbable;
    ungroundedgrabbable _sheetWorkpiece;
    bool _rigidbodyRemovedForSnap;
    bool _handRelaysInstalled;
    string _lastStatusText;

    /// <summary>True after snap removed the Rigidbody and before a hand collision restores it.</summary>
    public bool IsRigidbodyRemovedForSnap => _rigidbodyRemovedForSnap;

    void Awake()
    {
        _grabbable = GetComponent<Grabbable>();
        _sheetWorkpiece = GetComponent<ungroundedgrabbable>();
        EnsureRigidbody();
        RefreshRigidbodyStatusText(force: true);
    }

    void LateUpdate()
    {
        RefreshRigidbodyStatusText(force: false);
    }

    void OnTriggerEnter(Collider other)
    {
        TryRestoreFromHandCollider(other);
    }

    void OnCollisionEnter(Collision collision)
    {
        if (collision != null)
            TryRestoreFromHandCollider(collision.collider);
    }

    /// <summary>
    /// Called by <see cref="ungroundedgrabbable"/> (or similar) after a successful snap.
    /// Removes the Rigidbody until a hand touches the grabbable colliders.
    /// </summary>
    public void NotifyObjectSnapped()
    {
        if (!removeRigidbodyWhileSnapped)
            return;

        Rigidbody rb = GetComponent<Rigidbody>();
        if (rb == null)
        {
            _rigidbodyRemovedForSnap = true;
            EnsureHandRestoreRelays();
            return;
        }

        Destroy(rb);
        _rigidbodyRemovedForSnap = true;

        if (_sheetWorkpiece != null)
            _sheetWorkpiece.NotifyRigidbodyRemovedByBToggle();

        if (_grabbable != null)
            _grabbable.UpdateRigidbodyReference();

        EnsureHandRestoreRelays();

        if (debugSnapHandRigidbody)
            Debug.Log($"{nameof(GrabbableKinematicBodySetup)}: Rigidbody removed on snap ({name})", this);

        RefreshRigidbodyStatusText(force: true);
    }

    /// <summary>Called from child collider relays when a hand touches a grabbable collider.</summary>
    public void TryRestoreFromHandCollider(Collider other)
    {
        if (!restoreRigidbodyOnHandCollision || !_rigidbodyRemovedForSnap)
            return;

        // Keep Rigidbody off while the work clamp is seated.
        if (_sheetWorkpiece != null && _sheetWorkpiece.IsClampGroundedRigidbodyLockActive())
            return;

        if (other == null || !IsHandOrGrabberCollider(other))
            return;

        RestoreRigidbodyAfterHandTouch();
    }

    /// <summary>Re-add the Rigidbody if missing (e.g. clamp released and panel is no longer sheet-snapped).</summary>
    public void ForceRestoreRigidbody()
    {
        EnsureRigidbody();
        _rigidbodyRemovedForSnap = false;
        if (_grabbable != null)
            _grabbable.UpdateRigidbodyReference();
        RefreshRigidbodyStatusText(force: true);
    }

    static bool IsHandOrGrabberCollider(Collider other)
    {
        if (other.GetComponentInParent<Grabber>() != null)
            return true;

        // Some BNG setups put hand physics on a child without Grabber on that exact collider.
        Transform t = other.transform;
        string n = t.name;
        if (n.IndexOf("Grabber", System.StringComparison.OrdinalIgnoreCase) >= 0)
            return true;
        if (n.IndexOf("Hand", System.StringComparison.OrdinalIgnoreCase) >= 0)
            return true;

        return false;
    }

    void RestoreRigidbodyAfterHandTouch()
    {
        EnsureRigidbody();
        _rigidbodyRemovedForSnap = false;

        if (_grabbable != null)
            _grabbable.UpdateRigidbodyReference();

        if (_sheetWorkpiece != null)
        {
            // Clamp still grounded: remove/freeze again immediately.
            if (_sheetWorkpiece.IsClampGroundedRigidbodyLockActive())
            {
                _sheetWorkpiece.ReapplySheetSnapFreezeIfNeeded();
                if (removeRigidbodyWhileSnapped)
                    NotifyObjectSnapped();
            }
            else
            {
                _sheetWorkpiece.ReapplySheetSnapFreezeIfNeeded();
            }
        }

        if (debugSnapHandRigidbody)
            Debug.Log($"{nameof(GrabbableKinematicBodySetup)}: Rigidbody restored on hand contact ({name})", this);

        RefreshRigidbodyStatusText(force: true);
    }

    void EnsureRigidbody()
    {
        if (GetComponent<Rigidbody>() != null)
            return;

        var rb = gameObject.AddComponent<Rigidbody>();
        rb.mass = 1f;
        rb.drag = 0f;
        rb.angularDrag = 0.05f;
        rb.isKinematic = kinematicNoGravity;
        rb.useGravity = !kinematicNoGravity;
        rb.interpolation = RigidbodyInterpolation.None;
        rb.collisionDetectionMode = CollisionDetectionMode.Discrete;

        if (freezeRotation)
            rb.constraints = RigidbodyConstraints.FreezeRotation;

        if (_grabbable == null)
            _grabbable = GetComponent<Grabbable>();
        if (_grabbable != null)
            _grabbable.UpdateRigidbodyReference();

        RefreshRigidbodyStatusText(force: true);
    }

    void RefreshRigidbodyStatusText(bool force)
    {
        if (rigidbodyStatusText == null)
            return;

        string next = GetRigidbodyStatusLabel();
        if (!force && next == _lastStatusText)
            return;

        _lastStatusText = next;
        rigidbodyStatusText.text = next;
    }

    string GetRigidbodyStatusLabel()
    {
        Rigidbody rb = GetComponent<Rigidbody>();
        if (rb == null || _rigidbodyRemovedForSnap)
            return textWhenRigidbodyDisabled;

        if (rb.isKinematic && rb.constraints == RigidbodyConstraints.FreezeAll)
            return textWhenRigidbodyFrozen;

        return textWhenRigidbodyEnabled;
    }

    void EnsureHandRestoreRelays()
    {
        if (_handRelaysInstalled || !restoreRigidbodyOnHandCollision)
            return;

        Collider[] cols = GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < cols.Length; i++)
        {
            Collider col = cols[i];
            if (col == null)
                continue;

            // Skip colliders on other grabbables deeper in a hierarchy if any.
            Grabbable gOnCol = col.GetComponentInParent<Grabbable>();
            if (_grabbable != null && gOnCol != null && gOnCol != _grabbable)
                continue;

            if (col.GetComponent<KinematicBodyHandRestoreRelay>() == null)
            {
                var relay = col.gameObject.AddComponent<KinematicBodyHandRestoreRelay>();
                relay.owner = this;
            }
        }

        _handRelaysInstalled = true;
    }
}

/// <summary>
/// Forwards hand/grabber trigger and collision hits to <see cref="GrabbableKinematicBodySetup"/>
/// when colliders live on children (common once the parent Rigidbody is removed).
/// </summary>
public class KinematicBodyHandRestoreRelay : MonoBehaviour
{
    [HideInInspector]
    public GrabbableKinematicBodySetup owner;

    void OnTriggerEnter(Collider other)
    {
        if (owner != null)
            owner.TryRestoreFromHandCollider(other);
    }

    void OnCollisionEnter(Collision collision)
    {
        if (owner != null && collision != null)
            owner.TryRestoreFromHandCollider(collision.collider);
    }
}
