using UnityEngine;
using TMPro;
using BNG;

public class weldbar : MonoBehaviour, IWeldStepCompletable
{
    [Header("Matching")]
    [Tooltip("Asset key for this weld bar. Must match exampleweldbar.nameofasset.")]
    public string nameofasset;

    [Header("References")]
    [Tooltip("The Grabbable on the real movable bar (this object usually has it). If null, auto-finds.")]
    public Grabbable realWeldbarGrabbable;

    [Tooltip("Guide object to show/hide and snap to. This should have the exampleweldbar script.")]
    public exampleweldbar guide;

    [Tooltip("Collider on the stationary surface the bar must touch.")]
    public Collider boxColliderToSnapTo;

    [Tooltip("If true, collision with the guide object's collider can also trigger snapping.")]
    public bool allowGuideColliderAsSnapTarget = true;

    [Header("Behaviour")]
    [Tooltip("Show guide only while the real bar is held.")]
    public bool showGuideWhileHeld = true;

    [Tooltip("When true, snap only if the bar is currently being held.")]
    public bool requireWeldbarHeldForSnap = true;

    [Tooltip("If true, zero rigidbody velocity/angular velocity right after snapping for stability.")]
    public bool zeroPhysicsOnSnap = true;

    [Tooltip("If true, also apply the guide's local scale when snapping.")]
    public bool applyGuideScaleOnSnap = true;

    [Header("Grab-me cue (optional)")]
    [Tooltip("Highlight material that tells the user to grab this bar. Pulses while the bar is armed, not held, and not snapped; restores the originals once snapped.")]
    public Material grabMeMaterial;

    [Tooltip("Arm the grab-me cue on Start. For bars that should wait, leave this off and use Grab Me Arm After.")]
    public bool showGrabMeOnStart = false;

    [Tooltip("If assigned, the cue only arms once this completable reports complete (e.g. a WeldStepGroup of both ref pieces). Drag the component; any component on the same object that reports completion is accepted.")]
    public MonoBehaviour grabMeArmAfter;

    [Tooltip("Renderers to tint with the grab-me material. If empty, uses all child renderers of this bar.")]
    public Renderer[] grabMeRenderers;

    [Tooltip("Pulses per second of the grab-me brightness while the bar is waiting to be grabbed.")]
    public float grabMePulseSpeed = 1.5f;

    [Tooltip("Brightness multiplier at the dim end of the pulse.")]
    public float grabMePulseMinBrightness = 0.6f;

    [Tooltip("Brightness multiplier at the bright end of the pulse.")]
    public float grabMePulseMaxBrightness = 1.8f;

    [Tooltip("Also pulse the material's emission color (if it has one) — usually the strongest visual cue.")]
    public bool grabMePulseEmission = true;

    [Header("Grab-me cue — second round (optional)")]
    [Tooltip("Ref piece whose SECOND-location snap re-arms this bar's pulse. Once it snaps in its second spot, this bar pulses again whenever it is not held and not snapped, resumes if dropped, and retires once it snaps again.")]
    public refpiece pulseAgainAfterRefPieceSecondSnap;

    [Tooltip("Optional generic override for the second-round trigger. If assigned, this completable's completion re-arms the second pulse instead of the ref piece's second snap. Drag any component that reports completion.")]
    public MonoBehaviour pulseAgainArmAfter;

    [Tooltip("After merge, the second-round pulse retires when this reports the joint has snapped to the second example (defaults to WeldbarMergedFlipSnapToAnchor on the assembly). Leave null to auto-find on the merged parent.")]
    public WeldbarMergedFlipSnapToAnchor pulseAgainRetireOnJointSecondSnap;

    [Header("Post snap freeze & grab")]
    [Tooltip("When on, the bar snaps, becomes kinematic, and stays frozen in place until you grab it again (after the grab cooldown). When off, no rigidbody freeze and grab cooldown below is not used.")]
    public bool freezeAfterSnap = true;

    [Tooltip("Only when freeze after snap is on. After snap, grabbing is disabled this many seconds so physics/controllers settle.")]
    public float grabCooldownAfterSnapSeconds = 0.5f;

    [Tooltip("If true, Grabbable is disabled after snap (permanent until you re-enable in editor).")]
    public bool disableGrabAfterSnap = false;

    [Header("Cooldowns")]
    [Tooltip("After you grab a snapped bar, snap is blocked for this many seconds.")]
    public float snapCooldownAfterPickupSeconds = 0.35f;

    [Tooltip("During the post-pickup cooldown (and while seated), ignore physics between this bar and its guide / snap box so unsnapping is smooth — collisions pass through.")]
    public bool ignoreCollisionsDuringUnsnapCooldown = true;

    [Header("Status (TMP)")]
    [Tooltip("Which bar number 1–4 appears in the status line. Set differently on each of the four real bars.")]
    [Range(1, 4)]
    public int barDisplayNumber = 1;

    [Tooltip("Optional: one TextMeshPro UI line for this bar only (e.g. four TMP texts in the UI, each bar assigns its own).")]
    public TMP_Text statusText;

    [Header("Debug (optional)")]
    public bool debug = false;

    /// <summary>True after a successful snap (for relays / UI).</summary>
    public bool IsSnapped => snapped;

    /// <summary>Step is complete once this bar has snapped.</summary>
    public bool IsStepComplete => snapped;

    bool snapped = false;
    float nextSnapEligibleTime = 0f;
    bool wasHeldLastFrame = false;
    bool grabCooldownActive = false;
    float grabCooldownEndsAtTime = 0f;
    bool rigidbodyFrozenBySnap = false;
    bool guideCollisionsIgnored;

    Rigidbody rb;

    bool _grabMeCueEnabled;
    bool _grabMeActive;
    bool _secondPhaseArmed;
    bool _secondPhaseSnapDone;
    bool _mergedIntoAssembly;
    WeldbarAssemblyRoot _assemblyRoot;
    Grabbable _mergedJointGrabbable;
    Renderer[] _grabMeResolvedRenderers;
    Material[] _grabMeOriginals;
    Material[] _grabMeInstances;
    string _grabMeColorProp;
    bool _grabMeHasColorProp;
    bool _grabMeHasEmission;
    Color _grabMeBaseColor = Color.white;
    Color _grabMeBaseEmission = Color.black;
    IWeldStepCompletable _grabMeArmAfterResolved;
    IWeldStepCompletable _pulseAgainArmAfterResolved;

    void Awake()
    {
        if (realWeldbarGrabbable == null)
            realWeldbarGrabbable = GetComponent<Grabbable>() ?? GetComponentInParent<Grabbable>();

        rb = GetComponent<Rigidbody>() ?? GetComponentInParent<Rigidbody>() ?? GetComponentInChildren<Rigidbody>();

        if (rb != null && rb.gameObject != gameObject)
        {
            if (rb.gameObject.GetComponent<WeldbarCollisionRelay>() == null)
            {
                var relay = rb.gameObject.AddComponent<WeldbarCollisionRelay>();
                relay.owner = this;
            }
        }

        if (guide != null)
            guide.SetVisible(false);
    }

    void OnDisable()
    {
        if (grabCooldownActive && realWeldbarGrabbable != null)
        {
            realWeldbarGrabbable.enabled = true;
            grabCooldownActive = false;
        }
        SetUnsnapCollisionsIgnored(false);
    }

    void OnDestroy()
    {
        SetUnsnapCollisionsIgnored(false);
    }

    void Start()
    {
        wasHeldLastFrame = IsBeingHeld();
        UpdateGuideVisibility(IsBeingHeld());
        RefreshStatusText();

        _grabMeArmAfterResolved = grabMeArmAfter as IWeldStepCompletable;
        if (_grabMeArmAfterResolved == null && grabMeArmAfter != null)
            _grabMeArmAfterResolved = grabMeArmAfter.GetComponent<IWeldStepCompletable>();

        _pulseAgainArmAfterResolved = pulseAgainArmAfter as IWeldStepCompletable;
        if (_pulseAgainArmAfterResolved == null && pulseAgainArmAfter != null)
            _pulseAgainArmAfterResolved = pulseAgainArmAfter.GetComponent<IWeldStepCompletable>();

        if (showGrabMeOnStart)
            SetGrabMeHighlight(true);
    }

    void Update()
    {
        if (grabCooldownActive && realWeldbarGrabbable != null && Time.time >= grabCooldownEndsAtTime)
        {
            realWeldbarGrabbable.enabled = true;
            grabCooldownActive = false;
            if (debug)
                Debug.Log("weldbar: grab cooldown ended");
        }

        // End pass-through once post-pickup cooldown finishes (and not still seated).
        if (guideCollisionsIgnored && !snapped && !grabCooldownActive && Time.time >= nextSnapEligibleTime)
            SetUnsnapCollisionsIgnored(false);

        bool held = IsBeingHeld();

        // Grab-me cue keeps running after merge (assembly root no longer disables this component).
        UpdateGrabMeCue(held);

        // After merge into the joint, individual bar snap / guide / unfreeze no longer apply.
        if (_mergedIntoAssembly)
        {
            wasHeldLastFrame = held;
            return;
        }

        if (snapped && held)
        {
            if (rigidbodyFrozenBySnap && rb != null)
            {
                rb.velocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
                rb.isKinematic = false;
                rb.constraints = RigidbodyConstraints.None;
                rigidbodyFrozenBySnap = false;
                if (debug)
                    Debug.Log("weldbar: released snap freeze for grab");
            }

            if (!wasHeldLastFrame)
            {
                snapped = false;
                nextSnapEligibleTime = Time.time + Mathf.Max(0f, snapCooldownAfterPickupSeconds);
                // Pass through guide / snap-box collisions for the unsnap cooldown window.
                if (ignoreCollisionsDuringUnsnapCooldown)
                    SetUnsnapCollisionsIgnored(true);
                RefreshStatusText();
            }
        }

        wasHeldLastFrame = held;

        if (!snapped)
            UpdateGuideVisibility(held);
    }

    /// <summary>
    /// Called by <see cref="WeldbarAssemblyRoot"/> when this bar is absorbed into the merged joint.
    /// Keeps this component enabled for grab-me pulsing; disables per-bar snap behaviour.
    /// </summary>
    public void NotifyMergedIntoAssembly(WeldbarAssemblyRoot root)
    {
        _mergedIntoAssembly = true;
        _assemblyRoot = root;
        realWeldbarGrabbable = null;
        rb = null;
        rigidbodyFrozenBySnap = false;
        grabCooldownActive = false;
        SetUnsnapCollisionsIgnored(false);

        if (root != null)
        {
            _mergedJointGrabbable = root.GetComponent<Grabbable>();
            if (pulseAgainRetireOnJointSecondSnap == null)
                pulseAgainRetireOnJointSecondSnap = root.GetComponent<WeldbarMergedFlipSnapToAnchor>();
        }

        // Individual guide no longer applies once the joint owns placement.
        if (guide != null)
            guide.SetVisible(false);

        if (debug)
            Debug.Log("weldbar: merged into assembly — grab-me cue stays active, snap gated off");
    }

    void UpdateGuideVisibility(bool beingHeld)
    {
        if (guide == null)
            return;

        if (!showGuideWhileHeld)
        {
            guide.SetVisible(false);
            return;
        }

        guide.SetVisible(beingHeld);
    }

    bool IsBeingHeld()
    {
        // After merge, the bar's own Grabbable is destroyed — use the joint Grabbable instead.
        if (_mergedIntoAssembly)
        {
            if (_mergedJointGrabbable == null && _assemblyRoot != null)
                _mergedJointGrabbable = _assemblyRoot.GetComponent<Grabbable>();
            return _mergedJointGrabbable != null && _mergedJointGrabbable.BeingHeld;
        }

        return realWeldbarGrabbable != null && realWeldbarGrabbable.BeingHeld;
    }

    void OnTriggerEnter(Collider other)
    {
        HandleTrigger(other);
    }

    public void HandleTrigger(Collider other)
    {
        TrySnapFromCollider(other, "trigger");
    }

    void OnCollisionEnter(Collision collision)
    {
        HandleCollision(collision);
    }

    void OnCollisionStay(Collision collision)
    {
        HandleCollision(collision);
    }

    public void HandleCollision(Collision collision)
    {
        if (collision == null)
            return;
        TrySnapFromCollider(collision.collider, "collision");
    }

    void TrySnapFromCollider(Collider other, string source)
    {
        if (_mergedIntoAssembly)
            return;

        if (snapped)
            return;

        if (Time.time < nextSnapEligibleTime)
            return;

        if (other == null)
            return;

        if (IsColliderPartOfThisHierarchy(other))
            return;

        if (!IsSnapTargetCollider(other))
            return;

        if (debug)
            Debug.Log("weldbar: " + source + " contact with snap target: " + other.name);

        if (requireWeldbarHeldForSnap && !IsBeingHeld())
        {
            if (debug)
                Debug.Log("weldbar: snap blocked (not held)");
            return;
        }

        if (!IsMatchingGuide())
        {
            if (debug)
                Debug.Log("weldbar: snap blocked (nameofasset mismatch: bar='" + nameofasset + "' guide='" + (guide != null ? guide.nameofasset : "null") + "')");
            return;
        }

        if (!SnapToGuide(applyGuideScaleOnSnap))
            return;

        snapped = true;
        if (_secondPhaseArmed)
            _secondPhaseSnapDone = true;
        RefreshStatusText();
    }

    bool IsColliderPartOfThisHierarchy(Collider other)
    {
        Transform t = other.transform;
        return t == transform || t.IsChildOf(transform);
    }

    bool IsSameCollider(Collider a, Collider b)
    {
        if (a == null || b == null)
            return false;

        return a == b || a.transform == b.transform || a.transform.IsChildOf(b.transform) || b.transform.IsChildOf(a.transform);
    }

    bool IsSnapTargetCollider(Collider other)
    {
        if (other == null)
            return false;

        if (boxColliderToSnapTo != null && IsSameCollider(other, boxColliderToSnapTo))
            return true;

        if (allowGuideColliderAsSnapTarget && guide != null)
        {
            Collider[] guideCols = guide.GetComponentsInChildren<Collider>(true);
            for (int i = 0; i < guideCols.Length; i++)
            {
                Collider gc = guideCols[i];
                if (gc != null && gc.enabled && IsSameCollider(other, gc))
                    return true;
            }
        }

        return false;
    }

    bool SnapToGuide(bool applyScale)
    {
        if (guide == null)
            return false;

        Transform snap = guide.GetSnapTransform();
        if (snap == null)
            return false;

        transform.SetPositionAndRotation(snap.position, snap.rotation);

        if (rb != null && zeroPhysicsOnSnap)
        {
            rb.velocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        if (applyScale && snap.parent == transform.parent)
            transform.localScale = snap.localScale;
        else if (applyScale)
            transform.localScale = snap.localScale;

        if (debug)
            Debug.Log("weldbar: snapped to guide");

        guide.SetVisible(false);

        // Ignore while seated so lift-off after grab is already non-blocking.
        if (ignoreCollisionsDuringUnsnapCooldown)
            SetUnsnapCollisionsIgnored(true);

        rigidbodyFrozenBySnap = false;

        if (freezeAfterSnap)
        {
            if (rb != null)
            {
                rb.velocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
                rb.isKinematic = true;
                rb.constraints = RigidbodyConstraints.FreezeAll;
                rigidbodyFrozenBySnap = true;
            }

            if (disableGrabAfterSnap && realWeldbarGrabbable != null)
            {
                realWeldbarGrabbable.DropItem(true, true);
                realWeldbarGrabbable.enabled = false;
                realWeldbarGrabbable = null;
            }
            else if (!disableGrabAfterSnap && grabCooldownAfterSnapSeconds > 0f && realWeldbarGrabbable != null)
            {
                grabCooldownActive = true;
                grabCooldownEndsAtTime = Time.time + Mathf.Max(0f, grabCooldownAfterSnapSeconds);
                if (realWeldbarGrabbable.BeingHeld)
                    realWeldbarGrabbable.DropItem(true, true);
                realWeldbarGrabbable.enabled = false;
                if (debug)
                    Debug.Log("weldbar: grab cooldown started for " + grabCooldownAfterSnapSeconds + " s");
            }
        }
        else
        {
            if (disableGrabAfterSnap && realWeldbarGrabbable != null)
            {
                realWeldbarGrabbable.DropItem(true, true);
                realWeldbarGrabbable.enabled = false;
                realWeldbarGrabbable = null;
            }
        }

        return true;
    }

    /// <summary>Grab clause helper: arm/disarm the "grab me" cue for this bar.</summary>
    public void SetGrabMeHighlight(bool on)
    {
        _grabMeCueEnabled = on;
        if (!on)
            ClearGrabMe();
    }

    /// <summary>Debug/test: snap to the guide immediately (no grab/collision required).</summary>
    public bool ForceSnapForDebug()
    {
        if (_mergedIntoAssembly)
            return false;

        if (snapped)
            return true;

        if (!SnapToGuide(applyGuideScaleOnSnap))
            return false;

        snapped = true;
        if (_secondPhaseArmed)
            _secondPhaseSnapDone = true;

        _grabMeCueEnabled = false;
        ClearGrabMe();
        RefreshStatusText();
        return true;
    }

    /// <summary>True once the second-round trigger (ref piece's second snap, or the override) reports complete.</summary>
    bool SecondPulseTriggerComplete()
    {
        if (_pulseAgainArmAfterResolved != null)
            return _pulseAgainArmAfterResolved.IsStepComplete;

        if (pulseAgainAfterRefPieceSecondSnap != null)
            return pulseAgainAfterRefPieceSecondSnap.HasSnappedOnSecondGuide;

        return false;
    }

    void UpdateGrabMeCue(bool held)
    {
        // After merge, the joint's second-example snap retires the second-round pulse.
        if (_mergedIntoAssembly && _secondPhaseArmed && !_secondPhaseSnapDone)
        {
            if (pulseAgainRetireOnJointSecondSnap == null && _assemblyRoot != null)
                pulseAgainRetireOnJointSecondSnap = _assemblyRoot.GetComponent<WeldbarMergedFlipSnapToAnchor>();

            if (pulseAgainRetireOnJointSecondSnap != null &&
                pulseAgainRetireOnJointSecondSnap.HasRepositionedToSecondFrame)
                _secondPhaseSnapDone = true;
        }

        if (!SecondPulseTriggerComplete())
        {
            // Phase 1: pulse until the first snap; retire the cue on snap.
            if (snapped && _grabMeCueEnabled)
                _grabMeCueEnabled = false;

            // Auto-arm once the prerequisite (e.g. both ref pieces snapped) reports complete.
            if (!snapped && !_grabMeCueEnabled &&
                _grabMeArmAfterResolved != null && _grabMeArmAfterResolved.IsStepComplete)
                _grabMeCueEnabled = true;

            SetPulseActive(_grabMeCueEnabled && !snapped && !held);
            return;
        }

        // Phase 2: after the ref piece snaps in its second location, re-arm the pulse once so the bar
        // is cued again. Pulses whenever not held, resumes if dropped before re-snapping, and retires
        // for good once the bar snaps again (or, after merge, once the joint snaps to the second example).
        if (!_secondPhaseArmed)
        {
            _secondPhaseArmed = true;
            _grabMeCueEnabled = true;
        }

        if (_secondPhaseSnapDone)
            _grabMeCueEnabled = false;

        SetPulseActive(_grabMeCueEnabled && !held && !_secondPhaseSnapDone);
    }

    void SetPulseActive(bool want)
    {
        if (want && !_grabMeActive)
            ApplyGrabMe();
        else if (!want && _grabMeActive)
            ClearGrabMe();

        if (_grabMeActive)
            UpdateGrabMePulse();
    }

    void ApplyGrabMe()
    {
        if (_grabMeActive || grabMeMaterial == null)
            return;

        _grabMeColorProp = grabMeMaterial.HasProperty("_BaseColor")
            ? "_BaseColor"
            : (grabMeMaterial.HasProperty("_Color") ? "_Color" : null);
        _grabMeHasColorProp = _grabMeColorProp != null;
        _grabMeBaseColor = _grabMeHasColorProp ? grabMeMaterial.GetColor(_grabMeColorProp) : Color.white;
        _grabMeHasEmission = grabMeMaterial.HasProperty("_EmissionColor");
        _grabMeBaseEmission = _grabMeHasEmission ? grabMeMaterial.GetColor("_EmissionColor") : Color.black;

        _grabMeResolvedRenderers = (grabMeRenderers != null && grabMeRenderers.Length > 0)
            ? grabMeRenderers
            : GetComponentsInChildren<Renderer>(true);

        _grabMeOriginals = new Material[_grabMeResolvedRenderers.Length];
        _grabMeInstances = new Material[_grabMeResolvedRenderers.Length];
        for (int i = 0; i < _grabMeResolvedRenderers.Length; i++)
        {
            Renderer r = _grabMeResolvedRenderers[i];
            if (r == null)
                continue;
            _grabMeOriginals[i] = r.sharedMaterial;
            r.material = grabMeMaterial;
            _grabMeInstances[i] = r.material;
            if (grabMePulseEmission && _grabMeHasEmission)
                _grabMeInstances[i].EnableKeyword("_EMISSION");
        }

        _grabMeActive = true;
        UpdateGrabMePulse();
    }

    void UpdateGrabMePulse()
    {
        if (_grabMeInstances == null)
            return;

        float phase = 0.5f + 0.5f * Mathf.Sin(Time.time * grabMePulseSpeed * Mathf.PI * 2f);
        float b = Mathf.Lerp(grabMePulseMinBrightness, grabMePulseMaxBrightness, phase);

        for (int i = 0; i < _grabMeInstances.Length; i++)
        {
            Material inst = _grabMeInstances[i];
            if (inst == null)
                continue;

            if (_grabMeHasColorProp)
            {
                Color c = _grabMeBaseColor * b;
                c.a = _grabMeBaseColor.a;
                inst.SetColor(_grabMeColorProp, c);
            }

            if (grabMePulseEmission && _grabMeHasEmission)
                inst.SetColor("_EmissionColor", _grabMeBaseEmission * b);
        }
    }

    void ClearGrabMe()
    {
        if (!_grabMeActive)
            return;

        if (_grabMeResolvedRenderers != null && _grabMeOriginals != null)
        {
            for (int i = 0; i < _grabMeResolvedRenderers.Length; i++)
            {
                Renderer r = _grabMeResolvedRenderers[i];
                if (r != null && i < _grabMeOriginals.Length && _grabMeOriginals[i] != null)
                    r.material = _grabMeOriginals[i];
            }
        }

        _grabMeInstances = null;
        _grabMeActive = false;
    }

    /// <summary>True while the grab-me highlight is showing.</summary>
    public bool IsGrabMeHighlightActive => _grabMeActive;

    bool IsMatchingGuide()
    {
        if (guide == null)
            return false;

        if (string.IsNullOrEmpty(nameofasset))
            return true;

        return nameofasset == guide.nameofasset;
    }

    void RefreshStatusText()
    {
        if (statusText == null)
            return;

        int x = Mathf.Clamp(barDisplayNumber, 1, 4);
        statusText.text = snapped ? $"Bar{x} snapped" : $"Bar{x} is not snapped";
    }

    /// <summary>
    /// Ignore physics between this bar and its example guide + snap box.
    /// Uses force:true so it works even when the global SnapGuideCollisionIgnore master switch is off.
    /// </summary>
    void SetUnsnapCollisionsIgnored(bool ignore)
    {
        if (!ignore)
        {
            if (!guideCollisionsIgnored)
                return;

            if (guide != null)
                SnapGuideCollisionIgnore.SetIgnoredBetween(transform, guide.transform, false, force: true);
            if (boxColliderToSnapTo != null)
                SnapGuideCollisionIgnore.SetIgnoredBetween(transform, boxColliderToSnapTo.transform, false, force: true);

            guideCollisionsIgnored = false;
            if (debug)
                Debug.Log("weldbar: restored collisions with guide / snap box");
            return;
        }

        if (!ignoreCollisionsDuringUnsnapCooldown)
            return;

        if (guide != null)
            SnapGuideCollisionIgnore.SetIgnoredBetween(transform, guide.transform, true, force: true);
        if (boxColliderToSnapTo != null)
            SnapGuideCollisionIgnore.SetIgnoredBetween(transform, boxColliderToSnapTo.transform, true, force: true);

        guideCollisionsIgnored = true;
        if (debug)
            Debug.Log("weldbar: ignoring collisions with guide / snap box (unsnap pass-through)");
    }
}
