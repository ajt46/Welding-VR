using BNG;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// ExampleObjectSnap (robust guide + snap):
/// - Attach to the "example" snap location (guide).
/// - Give the guide object a collider (Trigger or non-Trigger).
/// - The real object should have:
///   - ObjectGrab (for matching) and BNG Grabbable.
///
/// Snap rule (robust):
/// - When a matching object has been in contact recently AND it transitions from Held -> Released,
///   this object snaps to the guide's transform.
/// - Uses OnTriggerStay/OnCollisionStay so snap doesn't depend on exact Enter/Exit timing.
/// </summary>
public class ExampleObjectSnap : MonoBehaviour
{
    [Header("Matching")]
    [Tooltip("Optional string match mode (legacy). If acceptedObjectGrabs has entries, reference matching is used instead.")]
    public string nameofasset = "";

    [Tooltip("If set, this guide ONLY snaps these specific ObjectGrab instances (reference matching).")]
    public ObjectGrab[] acceptedObjectGrabs;

    [Header("Snap")]
    [Tooltip("Higher = faster snapping motion.")]
    public float snapSpeed = 5f;
    public bool applyScale = true;

    [Header("Guide Visibility")]
    [Tooltip("If true, the guide GameObject is only visible while a matching object is held (until snapped).")]
    public bool showGuideWhileHeld = true;
    [Tooltip("If true, guide starts hidden (especially useful if showGuideWhileHeld is enabled).")]
    public bool startHidden = true;
    [Tooltip("If true, guide is hidden after the object snaps.")]
    public bool hideGuideAfterSnap = false;

    [Header("Cleanup Components")]
    [Tooltip("If true, removes grab + physics components from the snapped object so it stays fixed.")]
    public bool removeGrabAndPhysics = true;

    [Header("Robustness")]
    [Tooltip("Seconds after last contact that a 'release' can still trigger snapping.")]
    public float releaseGraceSeconds = 0.15f;

    [Tooltip("How long to keep candidate contacts around before forgetting them.")]
    public float contactRetentionSeconds = 0.4f;

    [Header("Debug")]
    public bool debug = false;
    public bool logDebugDetails = false;

    private class Candidate
    {
        public Transform targetTransform;
        public ObjectGrab objGrab;
        public Grabbable grabbable;
        public bool everHeld;
        public float lastContactTime;
    }

    private readonly Dictionary<Transform, Candidate> _candidates = new Dictionary<Transform, Candidate>();
    private bool _hasSnappedOnce = false;

    void Start()
    {
        if (showGuideWhileHeld && startHidden)
            gameObject.SetActive(false);
    }

    void Update()
    {
        if (_hasSnappedOnce)
            return;

        bool anyHeld = false;

        List<Transform> keys = new List<Transform>(_candidates.Keys);
        for (int i = 0; i < keys.Count; i++)
        {
            Transform key = keys[i];
            Candidate c = _candidates[key];

            if (c == null || c.targetTransform == null)
            {
                _candidates.Remove(key);
                continue;
            }

            bool isHeld = IsHeld(c);
            if (isHeld)
            {
                anyHeld = true;
                c.everHeld = true;
                continue;
            }

            float sinceContact = Time.time - c.lastContactTime;
            if (c.everHeld && sinceContact <= releaseGraceSeconds)
            {
                if (debug) Debug.Log($"ExampleObjectSnap: snapping {c.targetTransform.name} (released after contact)");
                _hasSnappedOnce = true;
                StartCoroutine(SmoothSnapAndLock(c.targetTransform));
                return;
            }

            if (sinceContact > contactRetentionSeconds)
                _candidates.Remove(key);
        }

        if (showGuideWhileHeld)
        {
            if (anyHeld && !gameObject.activeSelf)
                gameObject.SetActive(true);
            else if (!anyHeld && gameObject.activeSelf)
                gameObject.SetActive(false);
        }
    }

    bool IsHeld(Candidate c)
    {
        bool grabbedByBNG = c.grabbable != null && c.grabbable.BeingHeld;
        bool grabbedByHelper = c.objGrab != null && c.objGrab.isGrabbed;
        return grabbedByBNG || grabbedByHelper;
    }

    bool Matches(ObjectGrab grab)
    {
        if (grab == null)
            return false;

        if (acceptedObjectGrabs != null && acceptedObjectGrabs.Length > 0)
        {
            for (int i = 0; i < acceptedObjectGrabs.Length; i++)
            {
                if (acceptedObjectGrabs[i] != null && acceptedObjectGrabs[i] == grab)
                    return true;
            }
            return false;
        }

        if (!string.IsNullOrEmpty(nameofasset))
            return grab.nameofasset == nameofasset;

        return true;
    }

    void RegisterContact(Collider other)
    {
        var objGrab = other.GetComponent<ObjectGrab>() ?? other.GetComponentInParent<ObjectGrab>();
        if (objGrab == null)
            return;

        if (!Matches(objGrab))
            return;

        var grabbable = other.GetComponent<Grabbable>() ?? other.GetComponentInParent<Grabbable>();
        if (grabbable == null)
            return;

        Transform t = objGrab.transform != null ? objGrab.transform : grabbable.transform;
        if (t == null)
            t = other.transform;

        if (!_candidates.TryGetValue(t, out Candidate c))
        {
            c = new Candidate
            {
                targetTransform = t,
                objGrab = objGrab,
                grabbable = grabbable,
                everHeld = false,
                lastContactTime = Time.time
            };
            _candidates[t] = c;

            if (debug && logDebugDetails)
                Debug.Log($"ExampleObjectSnap: candidate created: {t.name}");
        }
        else
        {
            c.lastContactTime = Time.time;
        }
    }

    // Trigger mode
    void OnTriggerEnter(Collider other) => RegisterContact(other);
    void OnTriggerStay(Collider other) => RegisterContact(other);

    // Collision mode (non-trigger)
    void OnCollisionEnter(Collision collision) => RegisterContact(collision.collider);
    void OnCollisionStay(Collision collision) => RegisterContact(collision.collider);

    IEnumerator SmoothSnapAndLock(Transform target)
    {
        if (target == null)
            yield break;

        Vector3 startPos = target.position;
        Quaternion startRot = target.rotation;
        Vector3 startScale = target.localScale;

        Vector3 targetPos = transform.position;
        Quaternion targetRot = transform.rotation;
        Vector3 targetScale = transform.localScale;

        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime * snapSpeed;
            float tt = Mathf.Clamp01(t);

            target.position = Vector3.Lerp(startPos, targetPos, tt);
            target.rotation = Quaternion.Slerp(startRot, targetRot, tt);
            if (applyScale)
                target.localScale = Vector3.Lerp(startScale, targetScale, tt);

            yield return null;
        }

        target.position = targetPos;
        target.rotation = targetRot;
        if (applyScale)
            target.localScale = targetScale;

        if (removeGrabAndPhysics)
        {
            var grabEvents = target.GetComponent<GrabbableUnityEvents>();
            if (grabEvents != null)
                Destroy(grabEvents);

            var gr = target.GetComponent<Grabbable>();
            if (gr != null)
                Destroy(gr);

            var rb = target.GetComponent<Rigidbody>();
            if (rb != null)
                Destroy(rb);

            var cols = target.GetComponentsInChildren<Collider>();
            for (int i = 0; i < cols.Length; i++)
                cols[i].enabled = false;
        }

        if (hideGuideAfterSnap)
            gameObject.SetActive(false);
    }
}

