using UnityEngine;
using TMPro;
using BNG;

/// <summary>
/// Real movable reference piece with snap-to-guide behaviour (same pattern as <see cref="clamp"/>).
/// Pair with <see cref="exrefpiece"/>: the guide stays hidden until this object is grabbed, then snaps
/// the real piece onto the guide pose when a valid collision/trigger occurs.
/// </summary>
public class refpiece : MonoBehaviour, IWeldStepCompletable
{
    [Header("Matching")]
    [Tooltip("Asset key for this ref piece. Must match exrefpiece.nameofasset.")]
    public string nameofasset;

    [Header("References")]
    [Tooltip("The Grabbable on the real movable ref piece (this object usually has it). If null, auto-finds.")]
    public Grabbable realRefPieceGrabbable;

    [Tooltip("Guide object to show/hide and snap to. This should have the exrefpiece script.")]
    public exrefpiece guide;

    [Tooltip("Collider on the stationary box that the ref piece must touch.")]
    public Collider boxColliderToSnapTo;

    [Tooltip("If true, collision with the guide object's collider can also trigger snapping.")]
    public bool allowGuideColliderAsSnapTarget = true;

    [Header("Guide switch (optional)")]
    [Tooltip("Second guide that replaces Guide once a switch trigger below finishes. On pickup this one becomes visible / the snap target instead of the original Guide.")]
    public exrefpiece secondGuide;

    [Tooltip("Dot-style sequence (e.g. BottomWeldDots / SequentialWeldRevealSequence). When HasCompletedAllWeldSteps, switch to Second Guide. Leave empty if this piece uses Switch Guide When Lines Complete instead.")]
    public SequentialWeldRevealSequence switchGuideWhenWeldsComplete;

    [Tooltip("Line-style group (e.g. BottomWelds / WeldLinesRevealOnSnap). When HasWeldedAllLines, switch to Second Guide. Leave empty if this piece uses Switch Guide When Welds Complete instead. You can assign either field (or both — either completing switches the guide).")]
    public WeldLinesRevealOnSnap switchGuideWhenLinesComplete;

    [Header("Behaviour")]
    [Tooltip("Show guide only while the real ref piece is held.")]
    public bool showGuideWhileHeld = true;

    [Tooltip("When true, snap only if the ref piece is currently being held.")]
    public bool requireRefPieceHeldForSnap = true;

    [Tooltip("If true, zero rigidbody velocity/angular velocity right after snapping for stability.")]
    public bool zeroPhysicsOnSnap = true;

    [Tooltip("If true, also apply the guide's local scale when snapping.")]
    public bool applyGuideScaleOnSnap = true;

    [Header("Grab-me cue (optional)")]
    [Tooltip("Highlight material that tells the user to grab this piece. Applied to the piece's renderers until it is first grabbed, then the originals are restored (and the ghost appears via the normal held logic).")]
    public Material grabMeMaterial;

    [Tooltip("Show the grab-me highlight automatically on Start (until first grabbed). Turn off to let a WeldStepSequencer cue it via SetGrabMeHighlight() when it is this piece's turn.")]
    public bool showGrabMeOnStart = true;

    [Tooltip("If assigned, the cue only arms once this completable reports complete (e.g. a WeldStepGroup of prerequisite pieces). Drag the component; any component on the same object that reports completion is accepted. Leave null to use Show Grab Me On Start.")]
    public MonoBehaviour grabMeArmAfter;

    [Tooltip("Renderers to tint with the grab-me material. If empty, uses all child renderers of this piece.")]
    public Renderer[] grabMeRenderers;

    [Tooltip("Pulses per second of the grab-me brightness while the piece is not grabbed.")]
    public float grabMePulseSpeed = 1.5f;

    [Tooltip("Brightness multiplier at the dim end of the pulse.")]
    public float grabMePulseMinBrightness = 0.6f;

    [Tooltip("Brightness multiplier at the bright end of the pulse.")]
    public float grabMePulseMaxBrightness = 1.8f;

    [Tooltip("Also pulse the material's emission color (if it has one) — usually the strongest visual cue.")]
    public bool grabMePulseEmission = true;

    [Header("Grab-me cue — second position (optional)")]
    [Tooltip("Pulse the grab-me cue AGAIN once the guide-switch trigger completes (bottom dots and/or BottomWelds), to guide the user to grab this piece and move it to the second ghost. Stops only while held and resumes whenever the piece is not held and not snapped on the second guide; retires for good once snapped on the Second Guide.")]
    public bool pulseAgainAfterGuideSwitch = true;

    [Tooltip("Optional override for what re-arms the second pulse. If null, uses Switch Guide When Welds Complete and/or Switch Guide When Lines Complete. Drag any component that reports completion.")]
    public MonoBehaviour pulseAgainArmAfter;

    [Header("Post snap freeze & grab")]
    [Tooltip("When on, the piece snaps, becomes kinematic, and stays frozen in place until you grab it again (after the grab cooldown). When off, no rigidbody freeze and grab cooldown below is not used.")]
    public bool freezeAfterSnap = true;

    [Tooltip("Only when freeze after snap is on. After snap, grabbing is disabled this many seconds so physics/controllers settle; the piece stays frozen until then and only unfreezes when you grab.")]
    public float grabCooldownAfterSnapSeconds = 0.5f;

    [Tooltip("If true, Grabbable is disabled after snap (permanent until you re-enable in editor). Leave false to grab again, remove the piece, and snap again later.")]
    public bool disableGrabAfterSnap = false;

    [Header("Cooldowns")]
    [Tooltip("After you grab a snapped ref piece, snap is blocked for this many seconds (increase if unwanted re-snaps while moving).")]
    public float snapCooldownAfterPickupSeconds = 0.35f;

    [Tooltip("Blocks snapping for this many seconds every time the piece is grabbed (including the very first grab). Use this when the piece and its guide start overlapping in the same position so it doesn't instantly snap on grab — gives you time to move it off the guide.")]
    public float snapCooldownAfterGrabSeconds = 0.5f;

    [Tooltip("During the post-pickup cooldown (and while seated), ignore physics between this piece and its guide / snap box so unsnapping is smooth — collisions pass through.")]
    public bool ignoreCollisionsDuringUnsnapCooldown = true;

    [Header("Jointed frame pass-through")]
    [Tooltip("Merged weldbar assembly (RealFrame / WeldbarAssemblyRoot). While this piece is snapped — and during unsnap cooldown — ignore collisions with the jointed frame so lift-off is not blocked. Leave empty to auto-find.")]
    public WeldbarAssemblyRoot jointedFrameAssembly;

    [Header("Placement status (optional)")]
    [Tooltip("TextMeshPro to show placed vs not placed after snap.")]
    public TMP_Text groundedStatusText;
    [Tooltip("Shown after a snap that counts as placed (see below).")]
    public string textWhenGrounded = "grounded";
    [Tooltip("Shown before snap or when not placed.")]
    public string textWhenNotGrounded = "not grounded";

    [Tooltip("If true, 'grounded' only when the snap was triggered by contact with the guide (exrefpiece) collider. If false, any successful snap shows grounded.")]
    public bool groundedTextOnlyWhenSnapFromGuide = false;

    [Header("Debug (optional)")]
    public bool debug = false;

    /// <summary>True after a successful snap (for relays / UI).</summary>
    public bool IsSnapped => snapped;

    /// <summary>Step is complete once this ref piece has snapped.</summary>
    public bool IsStepComplete => snapped;

    /// <summary>True when the piece is snapped / placed (same as <see cref="IsSnapped"/>).</summary>
    public bool IsGrounded() => snapped;

    /// <summary>True if the snap was triggered by touching the guide (exrefpiece) collider.</summary>
    public bool SnappedViaGuideContact => snappedViaGuideContact;

    /// <summary>True once this piece has snapped onto its second guide (the second-location placement).</summary>
    public bool HasSnappedOnSecondGuide => _snappedOnSecondGuide;

    /// <summary>
    /// Guide currently in use: <see cref="secondGuide"/> once a switch trigger
    /// (<see cref="switchGuideWhenWeldsComplete"/> and/or <see cref="switchGuideWhenLinesComplete"/>)
    /// has finished, otherwise the original <see cref="guide"/>.
    /// </summary>
    public exrefpiece ActiveGuide
    {
        get
        {
            if (secondGuide != null && HasGuideSwitchCompleted())
                return secondGuide;
            return guide;
        }
    }

    /// <summary>
    /// True once either assigned switch source is done: bottom dots
    /// (<see cref="SequentialWeldRevealSequence.HasCompletedAllWeldSteps"/>) or weld lines
    /// (<see cref="WeldLinesRevealOnSnap.HasWeldedAllLines"/>).
    /// </summary>
    public bool HasGuideSwitchCompleted()
    {
        if (switchGuideWhenLinesComplete != null && switchGuideWhenLinesComplete.HasWeldedAllLines)
            return true;

        if (switchGuideWhenWeldsComplete != null && switchGuideWhenWeldsComplete.HasCompletedAllWeldSteps)
            return true;

        return false;
    }

    private bool snapped = false;
    private bool snappedViaGuideContact = false;
    private float nextSnapEligibleTime = 0f;
    private bool wasHeldLastFrame = false;
    private bool grabCooldownActive = false;
    private float grabCooldownEndsAtTime = 0f;
    private bool rigidbodyFrozenBySnap = false;
    private bool guideCollisionsIgnored;
    private bool jointedFrameCollisionsIgnored;

    private Rigidbody rb;

    private bool _grabMeCueEnabled;
    private bool _grabMeActive;
    private bool _secondPhaseArmed;
    private bool _snappedOnSecondGuide;
    private IWeldStepCompletable _grabMeArmAfterResolved;
    private IWeldStepCompletable _pulseAgainArmAfterResolved;
    private Renderer[] _grabMeResolvedRenderers;
    private Material[] _grabMeOriginals;
    private Material[] _grabMeInstances;
    private string _grabMeColorProp;
    private bool _grabMeHasColorProp;
    private bool _grabMeHasEmission;
    private Color _grabMeBaseColor = Color.white;
    private Color _grabMeBaseEmission = Color.black;

    void Awake()
    {
        if (realRefPieceGrabbable == null)
            realRefPieceGrabbable = GetComponent<Grabbable>() ?? GetComponentInParent<Grabbable>();

        rb = GetComponent<Rigidbody>() ?? GetComponentInParent<Rigidbody>() ?? GetComponentInChildren<Rigidbody>();

        if (rb != null && rb.gameObject != gameObject)
        {
            if (rb.gameObject.GetComponent<RefPieceCollisionRelay>() == null)
            {
                var relay = rb.gameObject.AddComponent<RefPieceCollisionRelay>();
                relay.owner = this;
            }
        }

        if (guide != null)
            guide.SetVisible(false);
        if (secondGuide != null)
            secondGuide.SetVisible(false);
    }

    void OnDisable()
    {
        if (grabCooldownActive && realRefPieceGrabbable != null)
        {
            realRefPieceGrabbable.enabled = true;
            grabCooldownActive = false;
        }
        SetUnsnapCollisionsIgnored(false);
        SetJointedFrameCollisionsIgnored(false);
    }

    void OnDestroy()
    {
        SetUnsnapCollisionsIgnored(false);
        SetJointedFrameCollisionsIgnored(false);
    }

    void Start()
    {
        wasHeldLastFrame = IsBeingHeld();
        UpdateGuideVisibility(IsBeingHeld());
        UpdateGroundedStatusText();

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
        if (grabCooldownActive && realRefPieceGrabbable != null && Time.time >= grabCooldownEndsAtTime)
        {
            realRefPieceGrabbable.enabled = true;
            grabCooldownActive = false;
            if (debug)
                Debug.Log("refpiece: grab cooldown ended");
        }

        // End pass-through once post-pickup cooldown finishes (and not still seated).
        if (guideCollisionsIgnored && !snapped && !grabCooldownActive && Time.time >= nextSnapEligibleTime)
            SetUnsnapCollisionsIgnored(false);
        if (jointedFrameCollisionsIgnored && !snapped && !grabCooldownActive && Time.time >= nextSnapEligibleTime)
            SetJointedFrameCollisionsIgnored(false);

        bool held = IsBeingHeld();
        bool grabbedThisFrame = held && !wasHeldLastFrame;

        // Grab-me cue: pulse while armed, not held, and not yet snapped. Held shows the normal material,
        // dropping before a snap resumes the pulse, and snapping retires the cue for good.
        UpdateGrabMeCue(held);

        // Block snapping briefly on every grab edge so a piece that starts overlapping its guide
        // (same start position) does not instantly snap back the moment it is picked up.
        if (grabbedThisFrame)
        {
            nextSnapEligibleTime = Mathf.Max(
                nextSnapEligibleTime,
                Time.time + Mathf.Max(0f, snapCooldownAfterGrabSeconds));
            if (debug)
                Debug.Log("refpiece: grab edge — snap blocked for " + snapCooldownAfterGrabSeconds + " s");
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
                    Debug.Log("refpiece: released snap freeze (kinematic + constraints) for grab");
            }

            if (!wasHeldLastFrame)
            {
                snapped = false;
                snappedViaGuideContact = false;
                nextSnapEligibleTime = Mathf.Max(
                    nextSnapEligibleTime,
                    Time.time + Mathf.Max(0f, snapCooldownAfterPickupSeconds));
                // Pass through guide / snap-box collisions for the unsnap cooldown window.
                if (ignoreCollisionsDuringUnsnapCooldown)
                {
                    SetUnsnapCollisionsIgnored(true);
                    SetJointedFrameCollisionsIgnored(true);
                }
                else
                {
                    SetJointedFrameCollisionsIgnored(false);
                }
                UpdateGroundedStatusText();
            }
        }

        wasHeldLastFrame = held;

        if (!snapped)
            UpdateGuideVisibility(held);
    }

    void UpdateGuideVisibility(bool beingHeld)
    {
        exrefpiece active = ActiveGuide;

        // Keep the non-active guide hidden so only one shows at a time.
        if (guide != null && guide != active)
            guide.SetVisible(false);
        if (secondGuide != null && secondGuide != active)
            secondGuide.SetVisible(false);

        if (active == null)
            return;

        if (!showGuideWhileHeld)
        {
            active.SetVisible(false);
            return;
        }

        active.SetVisible(beingHeld);
    }

    bool IsBeingHeld()
    {
        return realRefPieceGrabbable != null && realRefPieceGrabbable.BeingHeld;
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
            Debug.Log("refpiece: " + source + " contact with snap target: " + other.name);

        if (requireRefPieceHeldForSnap && !IsBeingHeld())
        {
            if (debug)
                Debug.Log("refpiece: snap blocked (not held)");
            return;
        }

        if (!IsMatchingGuide())
        {
            if (debug)
                Debug.Log("refpiece: snap blocked (nameofasset mismatch: refpiece='" + nameofasset + "' guide='" + (ActiveGuide != null ? ActiveGuide.nameofasset : "null") + "')");
            return;
        }

        bool contactWasGuide = IsContactWithGuideCollider(other);
        bool snappingOnSecondGuide = secondGuide != null && ActiveGuide == secondGuide;
        if (!SnapToGuide(applyGuideScaleOnSnap))
            return;

        snapped = true;
        snappedViaGuideContact = contactWasGuide;
        if (snappingOnSecondGuide)
            _snappedOnSecondGuide = true;
        UpdateGroundedStatusText();
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

        exrefpiece active = ActiveGuide;
        if (allowGuideColliderAsSnapTarget && active != null)
        {
            Collider[] guideCols = active.GetComponentsInChildren<Collider>(true);
            for (int i = 0; i < guideCols.Length; i++)
            {
                Collider gc = guideCols[i];
                if (gc != null && gc.enabled && IsSameCollider(other, gc))
                    return true;
            }
        }

        return false;
    }

    bool IsContactWithGuideCollider(Collider other)
    {
        exrefpiece active = ActiveGuide;
        if (other == null || active == null || !allowGuideColliderAsSnapTarget)
            return false;

        Collider[] guideCols = active.GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < guideCols.Length; i++)
        {
            Collider gc = guideCols[i];
            if (gc != null && gc.enabled && IsSameCollider(other, gc))
                return true;
        }

        return false;
    }

    bool SnapToGuide(bool applyScale)
    {
        exrefpiece active = ActiveGuide;
        if (active == null)
            return false;

        Transform snap = active.GetSnapTransform();
        if (snap == null)
            return false;

        transform.SetPositionAndRotation(snap.position, snap.rotation);

        if (rb != null && zeroPhysicsOnSnap)
        {
            rb.velocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        if (applyScale && snap.parent == transform.parent)
        {
            transform.localScale = snap.localScale;
        }
        else if (applyScale)
        {
            transform.localScale = snap.localScale;
        }

        if (debug)
            Debug.Log("refpiece: snapped to guide");

        active.SetVisible(false);

        // Ignore while seated so lift-off after grab is already non-blocking.
        if (ignoreCollisionsDuringUnsnapCooldown)
            SetUnsnapCollisionsIgnored(true);

        // Jointed RealFrame must not push a seated ref piece (independent of guide ignore toggle).
        SetJointedFrameCollisionsIgnored(true);

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

            if (disableGrabAfterSnap && realRefPieceGrabbable != null)
            {
                realRefPieceGrabbable.DropItem(true, true);
                realRefPieceGrabbable.enabled = false;
                realRefPieceGrabbable = null;
            }
            else if (!disableGrabAfterSnap && grabCooldownAfterSnapSeconds > 0f && realRefPieceGrabbable != null)
            {
                grabCooldownActive = true;
                grabCooldownEndsAtTime = Time.time + Mathf.Max(0f, grabCooldownAfterSnapSeconds);
                if (realRefPieceGrabbable.BeingHeld)
                    realRefPieceGrabbable.DropItem(true, true);
                realRefPieceGrabbable.enabled = false;
                if (debug)
                    Debug.Log("refpiece: grab cooldown started for " + grabCooldownAfterSnapSeconds + " s");
            }
        }
        else
        {
            if (disableGrabAfterSnap && realRefPieceGrabbable != null)
            {
                realRefPieceGrabbable.DropItem(true, true);
                realRefPieceGrabbable.enabled = false;
                realRefPieceGrabbable = null;
            }
        }

        return true;
    }

    void UpdateGroundedStatusText()
    {
        if (groundedStatusText == null)
            return;

        bool grounded = snapped;
        if (grounded && groundedTextOnlyWhenSnapFromGuide)
            grounded = snappedViaGuideContact;

        groundedStatusText.text = grounded ? textWhenGrounded : textWhenNotGrounded;
    }

    /// <summary>Grab clause helper: arm/disarm the "grab me" cue for this piece (the pulse then shows whenever the piece is not held and not snapped).</summary>
    public void SetGrabMeHighlight(bool on)
    {
        _grabMeCueEnabled = on;
        if (!on)
            ClearGrabMe();
    }

    /// <summary>True once the second-position pulse trigger (guide switch, or the override) reports complete.</summary>
    bool SecondPulseTriggerComplete()
    {
        if (!pulseAgainAfterGuideSwitch || secondGuide == null)
            return false;

        if (_pulseAgainArmAfterResolved != null)
            return _pulseAgainArmAfterResolved.IsStepComplete;

        return HasGuideSwitchCompleted();
    }

    /// <summary>Applies/removes the pulsing highlight based on armed + held + snapped state.</summary>
    void UpdateGrabMeCue(bool held)
    {
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

        // Phase 2: after the guide switch (bottom dot welds done), re-arm the pulse once so the user
        // is cued to grab this piece and move it to the second ghost. Pulses whenever not held, resumes
        // if dropped before the second snap, and retires for good once snapped on the second guide.
        if (!_secondPhaseArmed)
        {
            _secondPhaseArmed = true;
            _grabMeCueEnabled = true;
        }

        if (_snappedOnSecondGuide)
            _grabMeCueEnabled = false;

        SetPulseActive(_grabMeCueEnabled && !held && !_snappedOnSecondGuide);
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
        exrefpiece active = ActiveGuide;
        if (active == null)
            return false;

        if (string.IsNullOrEmpty(nameofasset))
            return true;

        return nameofasset == active.nameofasset;
    }

    /// <summary>Debug/test: snap to the active guide immediately (no grab/collision required).</summary>
    public bool ForceSnapForDebug()
    {
        if (snapped)
            return true;

        if (!SnapToGuide(applyGuideScaleOnSnap))
            return false;

        snapped = true;
        snappedViaGuideContact = true;
        if (secondGuide != null && ActiveGuide == secondGuide)
            _snappedOnSecondGuide = true;

        _grabMeCueEnabled = false;
        ClearGrabMe();
        UpdateGroundedStatusText();
        return true;
    }

    /// <summary>
    /// Debug/test: snap onto <see cref="secondGuide"/> immediately and mark
    /// <see cref="HasSnappedOnSecondGuide"/> (even if already snapped on the first guide).
    /// </summary>
    public bool ForceSnapToSecondGuideForDebug()
    {
        if (secondGuide == null)
            return false;

        if (_snappedOnSecondGuide && snapped)
            return true;

        Transform snap = secondGuide.GetSnapTransform();
        if (snap == null)
            return false;

        if (rigidbodyFrozenBySnap && rb != null)
        {
            rb.velocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.isKinematic = false;
            rb.constraints = RigidbodyConstraints.None;
            rigidbodyFrozenBySnap = false;
        }

        if (realRefPieceGrabbable != null && !realRefPieceGrabbable.enabled)
            realRefPieceGrabbable.enabled = true;

        transform.SetPositionAndRotation(snap.position, snap.rotation);

        if (rb != null && zeroPhysicsOnSnap)
        {
            rb.velocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        if (applyGuideScaleOnSnap)
            transform.localScale = snap.localScale;

        if (guide != null)
            guide.SetVisible(false);
        secondGuide.SetVisible(false);

        // Ignore while seated so lift-off after grab is already non-blocking.
        if (ignoreCollisionsDuringUnsnapCooldown)
            SetUnsnapCollisionsIgnored(true);

        SetJointedFrameCollisionsIgnored(true);

        snapped = true;
        snappedViaGuideContact = true;
        _snappedOnSecondGuide = true;

        if (freezeAfterSnap && rb != null)
        {
            rb.velocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.isKinematic = true;
            rb.constraints = RigidbodyConstraints.FreezeAll;
            rigidbodyFrozenBySnap = true;
        }

        _grabMeCueEnabled = false;
        ClearGrabMe();
        UpdateGroundedStatusText();
        return true;
    }

    /// <summary>
    /// Called by <see cref="WeldbarAssemblyRoot"/> when bars merge into one joint.
    /// If this piece is already snapped (or in unsnap cooldown), start ignoring the jointed frame.
    /// </summary>
    public void OnJointedFrameMerged(WeldbarAssemblyRoot root)
    {
        if (root != null)
            jointedFrameAssembly = root;

        if (snapped || jointedFrameCollisionsIgnored)
            SetJointedFrameCollisionsIgnored(true);
    }

    /// <summary>
    /// Ignore physics between this piece and the merged RealFrame assembly while snapped / unsnap cooldown.
    /// Uses force:true so it works even when the global SnapGuideCollisionIgnore master switch is off.
    /// </summary>
    void SetJointedFrameCollisionsIgnored(bool ignore)
    {
        if (jointedFrameAssembly == null)
            jointedFrameAssembly = FindObjectOfType<WeldbarAssemblyRoot>();

        if (!ignore)
        {
            if (!jointedFrameCollisionsIgnored)
                return;

            if (jointedFrameAssembly != null)
                jointedFrameAssembly.SetCollisionsIgnoredWith(transform, false);

            jointedFrameCollisionsIgnored = false;
            if (debug)
                Debug.Log("refpiece: restored collisions with jointed frame");
            return;
        }

        if (jointedFrameAssembly == null || !jointedFrameAssembly.HasMergedAssembly)
            return;

        jointedFrameAssembly.SetCollisionsIgnoredWith(transform, true);
        jointedFrameCollisionsIgnored = true;
        if (debug)
            Debug.Log("refpiece: ignoring collisions with jointed frame (snapped / unsnap pass-through)");
    }

    /// <summary>
    /// Ignore physics between this piece and its active/second guide + snap box.
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
            if (secondGuide != null)
                SnapGuideCollisionIgnore.SetIgnoredBetween(transform, secondGuide.transform, false, force: true);
            if (boxColliderToSnapTo != null)
                SnapGuideCollisionIgnore.SetIgnoredBetween(transform, boxColliderToSnapTo.transform, false, force: true);

            guideCollisionsIgnored = false;
            if (debug)
                Debug.Log("refpiece: restored collisions with guide(s) / snap box");
            return;
        }

        if (!ignoreCollisionsDuringUnsnapCooldown)
            return;

        exrefpiece active = ActiveGuide;
        if (active != null)
            SnapGuideCollisionIgnore.SetIgnoredBetween(transform, active.transform, true, force: true);
        // Also cover the other guide if both exist (guide switch / second-location pickup).
        if (guide != null && guide != active)
            SnapGuideCollisionIgnore.SetIgnoredBetween(transform, guide.transform, true, force: true);
        if (secondGuide != null && secondGuide != active)
            SnapGuideCollisionIgnore.SetIgnoredBetween(transform, secondGuide.transform, true, force: true);
        if (boxColliderToSnapTo != null)
            SnapGuideCollisionIgnore.SetIgnoredBetween(transform, boxColliderToSnapTo.transform, true, force: true);

        guideCollisionsIgnored = true;
        if (debug)
            Debug.Log("refpiece: ignoring collisions with guide(s) / snap box (unsnap pass-through)");
    }
}
