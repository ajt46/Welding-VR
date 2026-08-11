using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using BNG;

/// <summary>
/// Custom welding controller - original simple behavior from the setup guide.
/// Attach this to your MIG Welding Gun object.
/// </summary>
public class CustomWeldingController : MonoBehaviour
{
    [Header("Welding Tip")]
    [Tooltip("Transform representing the tip of the welding gun (where raycast starts)")]
    public Transform weldingTip;

    [Header("Blob Settings")]
    [Tooltip("Prefab to instantiate when creating a new weld blob")]
    public GameObject weldBlobPrefab;

    [Tooltip("Initial size of the blob when created")]
    public float blobInitialSize = 0.2f;

    [Tooltip("Maximum size before blob overheats")]
    public float blobMaxSize = 0.7f;

    [Tooltip("How fast the blob grows per second")]
    public float blobGrowthRate = 0.2f;

    [Header("Blob Formation Settings")]
    [Tooltip("Number of blobs created per second (constant rate while welding)")]
    [Range(1, 30)]
    public float blobsPerSecond = 10f;

    [Header("Travel Speed Settings")]
    [Tooltip("Enable speed-based blob size/thickness (simulates fast/slow welding)")]
    public bool useSpeedBasedSizing = true;
    
    [Tooltip("Ideal travel speed (units per second) - welds at this speed will be 'OK' quality")]
    public float idealTravelSpeed = 0.1f;
    
    [Tooltip("Speed sensitivity (higher = more dramatic difference between fast/slow)")]
    [Range(0.5f, 5f)]
    public float speedSensitivity = 2f;
    
    [Tooltip("Minimum blob width (when moving too fast - creates narrow 'fast' weld)")]
    public float minBlobWidth = 0.1f;
    
    [Tooltip("Maximum blob width (when moving too slow - creates wide 'slow' weld)")]
    public float maxBlobWidth = 0.5f;
    
    [Tooltip("Minimum blob height/thickness (when moving too fast)")]
    public float minBlobHeight = 0.05f;
    
    [Tooltip("Maximum blob height/thickness (when moving too slow - creates thick 'slow' weld)")]
    public float maxBlobHeight = 0.3f;

    [Header("Welding Settings")]
    [Tooltip("Delay in seconds before welding actually starts after trigger press")]
    public float weldingStartDelay = 1f;

    [Tooltip("Layers that can be welded on (usually your panel layer)")]
    public LayerMask weldableLayers = 1 << 7; // Default to layer 7

    [Tooltip("Layer for welding blobs")]
    public int blobLayer = 6;

    [Header("Raycast Settings")]
    [Tooltip("Maximum distance for raycast from welding tip")]
    public float raycastDistance = 0.5f;

    [Tooltip("How raycasts treat colliders marked as triggers. Use Collide if your weld surface uses trigger colliders.")]
    public QueryTriggerInteraction raycastTriggerInteraction = QueryTriggerInteraction.Collide;

    [Header("Tip contact (blobs)")]
    [Tooltip("Collider on the gun tip used for physical contact with the weld surface. If null, tries weldingTip.GetComponent<Collider>(). When set and Require Tip Contact is on, blobs only form when this collider overlaps or is within gap tolerance of the surface hit by the ray.")]
    public Collider tipContactCollider;

    [Tooltip("If true and Tip Contact Collider is assigned, the raycast alone is not enough — the tip collider must touch the hit surface for welding/blobs.")]
    public bool requireTipColliderContactForBlobs = true;

    [Tooltip("Max gap (meters) between tip and surface colliders to still count as touching when not overlapping (Physics.ComputePenetration is false).")]
    public float tipSurfaceContactGapTolerance = 0.004f;

    [Header("Welding prerequisites")]
    [Tooltip("MIG welder must be ON (second object visible). Leave null to skip this check.")]
    public onoffswitch migWelderSwitch;

    [Tooltip("Work clamp must be grounded (snapped to guide). Leave null to skip.")]
    public clamp clampForGround;

    [Tooltip("Gas knob must read Gas ON. Leave null to skip.")]
    public gasonoff gasOnKnob;

    [Header("Debug UI")]
    [Tooltip("Optional TextMeshPro. Shows binary state: power | ground | gas (1=ok, 0=no, -=ref not assigned).")]
    public TMP_Text prerequisiteStatusText;

    [Tooltip("Second line: L/R trigger values, grabbable held, raycast on weldable surface, isWelding (after delay).")]
    public bool showDebugWeldingStateLine;

    [Header("Input & sparks")]
    [Tooltip("Gun Grabbable. If null, uses GetComponent / parent.")]
    public Grabbable gunGrabbable;

    [Tooltip("Sparks particle system (optional). If null, searches children.")]
    public ParticleSystem weldSparks;

    [Tooltip("Trigger axis must exceed this (0–1) on the active hand while holding the gun.")]
    [Range(0f, 1f)]
    public float triggerThreshold = 0.1f;

    [Tooltip("If true, only the index trigger on the hand holding the gun (primary grabber) can weld. If false, either trigger works (legacy).")]
    public bool useHandMatchedTrigger = true;

    [Header("Grab-me cue (optional)")]
    [Tooltip("Highlight material that tells the user to grab the gun. Pulses only BEFORE the gun's first grab, and only once the prerequisite below reports complete. Retires permanently after the gun is grabbed once.")]
    public Material grabMeMaterial;

    [Tooltip("Arm the grab-me cue on Start. Usually leave off and use Grab Me Arm After so the gun only pulses once the 4 bars are snapped.")]
    public bool showGrabMeOnStart = false;

    [Tooltip("If assigned, the cue only arms once this completable reports complete (e.g. a WeldStepGroup of the 4 snapped bars). Drag the component; any component on the same object that reports completion is accepted.")]
    public MonoBehaviour grabMeArmAfter;

    [Tooltip("Renderers to tint with the grab-me material. If empty, uses all child renderers of the gun.")]
    public Renderer[] grabMeRenderers;

    [Tooltip("Pulses per second of the grab-me brightness while the gun is waiting to be grabbed.")]
    public float grabMePulseSpeed = 1.5f;

    [Tooltip("Brightness multiplier at the dim end of the pulse.")]
    public float grabMePulseMinBrightness = 0.6f;

    [Tooltip("Brightness multiplier at the bright end of the pulse.")]
    public float grabMePulseMaxBrightness = 1.8f;

    [Tooltip("Also pulse the material's emission color (if it has one) — usually the strongest visual cue.")]
    public bool grabMePulseEmission = true;

    [Header("Overheating")]
    [Tooltip("Prefab to show when blob overheats (creates a hole)")]
    public GameObject holePrefab;

    [Tooltip("Time to wait after overheating before allowing welding again")]
    public float overheatingCooldown = 0.5f;

    // Private variables
    private bool isWelding = false;
    private float weldTimer = 0f;
    private bool isOnWeldableSurface = false;
    private RaycastHit currentHit;
    private GameObject currentBlob = null;
    private float currentBlobSize = 0f;
    private bool isOverheating = false;
    private Transform currentBlobParent = null;
    private GameObject previousBlob = null;
    
    // Blobs per second tracking
    private float timeSinceLastBlob = 0f;
    private float blobCreationInterval = 0f;
    
    // Travel speed tracking
    private Vector3 lastWeldPosition = Vector3.zero;
    private float travelSpeed = 0f;
    
    // Travel timing for WeldingPanel
    private float travelTimer = 0f;
    private WeldingPanel currentPanel = null;
    
    // External cooldown (e.g. after speed evaluation)
    private Coroutine externalCooldownRoutine;

    // After weld evaluation: no sparks/blobs until cooldown elapses and trigger has been released once.
    private bool _postEvalGunLocked;
    private float _postEvalCooldownEndTime;
    private bool _triggerReleasedAfterEval;

    private InputBridge _inputBridge;
    private bool _sparksEmissionEnabled;

    // Grab-me cue (pulses only before the gun's first grab, once the prerequisite is complete).
    private bool _grabMeCueEnabled;
    private bool _grabMeActive;
    private bool _gunHasBeenGrabbed;
    private Renderer[] _grabMeResolvedRenderers;
    private Material[] _grabMeOriginals;
    private Material[] _grabMeInstances;
    private string _grabMeColorProp;
    private bool _grabMeHasColorProp;
    private bool _grabMeHasEmission;
    private Color _grabMeBaseColor = Color.white;
    private Color _grabMeBaseEmission = Color.black;
    private IWeldStepCompletable _grabMeArmAfterResolved;

    void Awake()
    {
        if (gunGrabbable == null)
            gunGrabbable = GetComponent<Grabbable>() ?? GetComponentInParent<Grabbable>();

        if (weldSparks == null)
            weldSparks = GetComponentInChildren<ParticleSystem>();

        if (weldSparks != null)
        {
            var emission = weldSparks.emission;
            emission.enabled = false;
            weldSparks.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            _sparksEmissionEnabled = false;
        }

        _inputBridge = InputBridge.Instance;

        if (tipContactCollider == null && weldingTip != null)
            tipContactCollider = weldingTip.GetComponent<Collider>();
    }

    void Start()
    {
        UpdateBlobCreationInterval();

        _grabMeArmAfterResolved = grabMeArmAfter as IWeldStepCompletable;
        if (_grabMeArmAfterResolved == null && grabMeArmAfter != null)
            _grabMeArmAfterResolved = grabMeArmAfter.GetComponent<IWeldStepCompletable>();

        if (showGrabMeOnStart)
            SetGrabMeHighlight(true);
    }

    bool TipHasPhysicalContactWith(Collider surfaceCollider)
    {
        if (tipContactCollider == null || surfaceCollider == null)
            return true;

        Transform tTip = tipContactCollider.transform;
        Transform tSurf = surfaceCollider.transform;

        if (Physics.ComputePenetration(
                tipContactCollider, tTip.position, tTip.rotation,
                surfaceCollider, tSurf.position, tSurf.rotation,
                out _, out _))
            return true;

        Vector3 onSurface = surfaceCollider.ClosestPoint(tipContactCollider.bounds.center);
        Vector3 onTip = tipContactCollider.ClosestPoint(onSurface);
        return Vector3.Distance(onSurface, onTip) <= Mathf.Max(0f, tipSurfaceContactGapTolerance);
    }

    void Update()
    {
        UpdateGrabMeCue();

        UpdateWeldPoint();

        if (IsPostEvalGunLocked())
        {
            if (isWelding)
                StopWelding();
            SetWeldSparksActive(false);
            if (!GetWeldingTriggerHeld())
                _triggerReleasedAfterEval = true;
            if (Time.time >= _postEvalCooldownEndTime && _triggerReleasedAfterEval)
                _postEvalGunLocked = false;
            UpdatePrerequisiteStatusText();
            return;
        }

        if (!AllWeldingConditionsMet())
        {
            if (isWelding)
                StopWelding();
            SetWeldSparksActive(false);
            UpdatePrerequisiteStatusText();
            return;
        }

        if (!GetWeldingTriggerHeld())
        {
            if (isWelding)
                StopWelding();
            SetWeldSparksActive(false);
            UpdatePrerequisiteStatusText();
            return;
        }

        SetWeldSparksActive(true);
        StartWelding();

        // Constant blob formation based on time (blobs per second)
        if (isWelding && isOnWeldableSurface)
        {
            // Calculate travel speed
            CalculateTravelSpeed();
            
            // Accumulate travel time between blobs
            travelTimer += Time.deltaTime;
            
            timeSinceLastBlob += Time.deltaTime;
            
            // Create blob at constant rate
            if (timeSinceLastBlob >= blobCreationInterval)
            {
                CreateNewBlobAtCurrentPosition();
                timeSinceLastBlob = 0f;
            }
        }

        UpdatePrerequisiteStatusText();
    }
    
    /// <summary>
    /// Updates the blob creation interval when blobsPerSecond changes
    /// </summary>
    private void UpdateBlobCreationInterval()
    {
        blobCreationInterval = 1f / Mathf.Max(blobsPerSecond, 0.1f);
    }

    public void StartWelding()
    {
        if (isOverheating || IsPostEvalGunLocked())
            return;

        if (!AllWeldingConditionsMet())
        {
            isWelding = false;
            weldTimer = 0f;
            return;
        }

        weldTimer += Time.deltaTime;

        if (weldTimer >= weldingStartDelay && isOnWeldableSurface)
        {
            // Enable welding; blob creation handled in Update via blobs-per-second
            isWelding = true;
        }
        else
        {
            isWelding = false;
        }
    }

    /// <summary>
    /// After weld evaluation: disables sparks and blobs until <paramref name="cooldownSeconds"/> passes
    /// and the player has released the trigger (then a new weld can start).
    /// </summary>
    public void LockGunAfterEvaluation(float cooldownSeconds)
    {
        _postEvalGunLocked = true;
        _postEvalCooldownEndTime = Time.time + Mathf.Max(0f, cooldownSeconds);
        _triggerReleasedAfterEval = !GetWeldingTriggerHeld();
        StopWelding();
    }

    bool IsPostEvalGunLocked()
    {
        return _postEvalGunLocked;
    }

    /// <summary>True while the gun is locked after an evaluation (cooldown and/or waiting for trigger release).</summary>
    public bool IsGunLockedAfterEvaluation => _postEvalGunLocked;

    /// <summary>
    /// Block welding for a short duration (used by external systems like speed evaluation).
    /// </summary>
    public void BlockWeldingForSeconds(float seconds)
    {
        if (externalCooldownRoutine != null)
        {
            StopCoroutine(externalCooldownRoutine);
        }
        externalCooldownRoutine = StartCoroutine(ExternalBlockRoutine(seconds));
    }

    private IEnumerator ExternalBlockRoutine(float duration)
    {
        isOverheating = true;
        yield return new WaitForSeconds(duration);
        isOverheating = false;
        externalCooldownRoutine = null;
    }

    void UpdatePrerequisiteStatusText()
    {
        if (prerequisiteStatusText == null)
            return;

        string power = migWelderSwitch == null ? "-" : (migWelderSwitch.IsWelderOn() ? "1" : "0");
        string ground = clampForGround == null ? "-" : (clampForGround.IsGrounded() ? "1" : "0");
        string gas = gasOnKnob == null ? "-" : (gasOnKnob.IsGasOn() ? "1" : "0");
        var sb = new System.Text.StringBuilder();
        sb.Append(power).Append('/').Append(ground).Append('/').Append(gas);

        if (showDebugWeldingStateLine)
        {
            if (_inputBridge == null)
                _inputBridge = InputBridge.Instance;
            float lt = _inputBridge != null ? _inputBridge.LeftTrigger : 0f;
            float rt = _inputBridge != null ? _inputBridge.RightTrigger : 0f;
            float active = GetActiveWeldingTriggerValue();
            bool held = gunGrabbable == null || gunGrabbable.BeingHeld;
            bool trigOk = GetWeldingTriggerHeld();
            sb.Append("\nL:").Append(lt.ToString("F2")).Append(" R:").Append(rt.ToString("F2"));
            sb.Append(" | act:").Append(active.ToString("F2"));
            sb.Append(" | hold:").Append(held ? "1" : "0");
            sb.Append(" | surf:").Append(isOnWeldableSurface ? "1" : "0");
            sb.Append(" | weld:").Append(isWelding ? "1" : "0");
            sb.Append(" | t>:").Append(trigOk ? "1" : "0");
            sb.Append(" | IB:").Append(_inputBridge != null ? "ok" : "null");
        }

        prerequisiteStatusText.text = sb.ToString();
    }

    bool AllWeldingConditionsMet()
    {
        if (migWelderSwitch != null && !migWelderSwitch.IsWelderOn())
            return false;
        if (clampForGround != null && !clampForGround.IsGrounded())
            return false;
        if (gasOnKnob != null && !gasOnKnob.IsGasOn())
            return false;
        return true;
    }

    /// <summary>
    /// Trigger axis (0–1) for welding: matched to the hand holding the gun when <see cref="useHandMatchedTrigger"/> is on.
    /// </summary>
    public float GetActiveWeldingTriggerValue()
    {
        if (_inputBridge == null)
            _inputBridge = InputBridge.Instance;
        if (_inputBridge == null)
            return 0f;

        if (gunGrabbable != null && !gunGrabbable.BeingHeld)
            return 0f;

        if (!useHandMatchedTrigger || gunGrabbable == null)
            return Mathf.Max(_inputBridge.LeftTrigger, _inputBridge.RightTrigger);

        Grabber grabber = gunGrabbable.GetPrimaryGrabber();
        if (grabber == null)
            return Mathf.Max(_inputBridge.LeftTrigger, _inputBridge.RightTrigger);

        return grabber.HandSide == ControllerHand.Right
            ? _inputBridge.RightTrigger
            : _inputBridge.LeftTrigger;
    }

    bool GetWeldingTriggerHeld()
    {
        return GetActiveWeldingTriggerValue() > triggerThreshold;
    }

    void SetWeldSparksActive(bool on)
    {
        if (weldSparks == null)
            return;

        if (on == _sparksEmissionEnabled)
            return;

        _sparksEmissionEnabled = on;
        var emission = weldSparks.emission;
        if (on)
        {
            if (!weldSparks.gameObject.activeSelf)
                weldSparks.gameObject.SetActive(true);
            emission.enabled = true;
            weldSparks.Play();
        }
        else
        {
            emission.enabled = false;
            weldSparks.Stop();
        }
    }

    public void StopWelding()
    {
        SetWeldSparksActive(false);
        isWelding = false;
        weldTimer = 0f;
        timeSinceLastBlob = 0f;
        travelSpeed = 0f;
        lastWeldPosition = Vector3.zero;
        travelTimer = 0f;
        currentPanel = null;

        if (currentBlob != null)
        {
            if (currentBlobParent != null)
            {
                currentBlob.transform.SetParent(currentBlobParent);
            }

            previousBlob = currentBlob;
            currentBlob = null;
            currentBlobSize = 0f;
            currentBlobParent = null;
        }
    }

    /// <summary>Arm/disarm the "grab me" cue for the gun (only meaningful before the first grab).</summary>
    public void SetGrabMeHighlight(bool on)
    {
        _grabMeCueEnabled = on;
        if (!on)
            ClearGrabMe();
    }

    void UpdateGrabMeCue()
    {
        bool held = gunGrabbable != null && gunGrabbable.BeingHeld;

        // Once the gun has been grabbed at least once, retire the cue permanently.
        if (held)
            _gunHasBeenGrabbed = true;

        if (_gunHasBeenGrabbed && _grabMeCueEnabled)
            _grabMeCueEnabled = false;

        // Auto-arm once the prerequisite (e.g. the 4 bars snapped) reports complete.
        if (!_gunHasBeenGrabbed && !_grabMeCueEnabled &&
            _grabMeArmAfterResolved != null && _grabMeArmAfterResolved.IsStepComplete)
            _grabMeCueEnabled = true;

        bool wantPulse = _grabMeCueEnabled && !_gunHasBeenGrabbed && !held;

        if (wantPulse && !_grabMeActive)
            ApplyGrabMe();
        else if (!wantPulse && _grabMeActive)
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

    private void UpdateWeldPoint()
    {
        if (weldingTip == null)
        {
            Debug.LogWarning("CustomWeldingController: Welding Tip is not assigned!");
            return;
        }

        // Only consider weldable + blob layers so a closer collider on Default/Hands/etc. does not block the panel behind.
        int rayMask = weldableLayers.value | (1 << blobLayer);

        isOnWeldableSurface = false;
        currentPanel = null;

        if (!Physics.Raycast(weldingTip.position, weldingTip.forward, out RaycastHit hit, raycastDistance, rayMask, raycastTriggerInteraction))
        {
            Debug.DrawRay(weldingTip.position, weldingTip.forward * raycastDistance,
                Color.red);
            return;
        }

        int hitLayer = hit.transform.gameObject.layer;

        if ((weldableLayers.value & (1 << hitLayer)) != 0)
        {
            isOnWeldableSurface = true;
            currentHit = hit;
            currentPanel = hit.transform.GetComponentInParent<WeldingPanel>();
        }

        if (hitLayer == blobLayer)
        {
            if (currentBlob != hit.transform.gameObject)
            {
                if (currentBlob != null)
                    FinalizeBlob();
                currentBlob = hit.transform.gameObject;
                currentBlobSize = currentBlob.transform.localScale.x;
                currentBlobParent = hit.transform.parent;
            }

            isOnWeldableSurface = true;
            currentHit = hit;
            if (currentPanel == null)
                currentPanel = hit.transform.GetComponentInParent<WeldingPanel>();
        }

        if (requireTipColliderContactForBlobs && tipContactCollider != null && isOnWeldableSurface)
        {
            if (!TipHasPhysicalContactWith(hit.collider))
            {
                isOnWeldableSurface = false;
                currentPanel = null;
            }
        }

        Debug.DrawRay(weldingTip.position, weldingTip.forward * raycastDistance,
            isOnWeldableSurface ? Color.green : Color.red);
    }

    private void CreateOrGrowBlob()
    {
        if (!isOnWeldableSurface || weldBlobPrefab == null)
            return;

        if (currentHit.transform.gameObject.layer == blobLayer)
        {
            GrowBlob(currentHit.transform.gameObject);
        }
        else if (currentBlob == null)
        {
            CreateNewBlob();
        }
        else
        {
            float distance = Vector3.Distance(currentBlob.transform.position, currentHit.point);
            if (distance > 0.1f)
            {
                FinalizeBlob();
                CreateNewBlob();
            }
            else
            {
                GrowBlob(currentBlob);
            }
        }
    }

    private void CreateNewBlob()
    {
        if (weldBlobPrefab == null)
        {
            Debug.LogWarning("CustomWeldingController: Weld Blob Prefab is not assigned!");
            return;
        }

        Quaternion rotation = Quaternion.FromToRotation(Vector3.up, currentHit.normal);

        // Calculate blob dimensions based on travel speed
        float blobWidth, blobHeight;
        if (useSpeedBasedSizing)
        {
            blobWidth = CalculateSpeedBasedWidth();
            blobHeight = CalculateSpeedBasedHeight();
        }
        else
        {
            // Use constant size for "OK" weld
            blobWidth = blobInitialSize;
            blobHeight = blobInitialSize;
        }

        currentBlob = Instantiate(weldBlobPrefab, currentHit.point, rotation);
        currentBlob.layer = blobLayer;

        // Set scale: width affects X and Z (weld bead width), height affects Y (weld bead height/thickness)
        currentBlob.transform.localScale = new Vector3(blobWidth, blobHeight, blobWidth);
        currentBlobSize = blobWidth; // Store width as reference size

        currentBlobParent = currentHit.transform;

        currentBlob.tag = "WeldObject";
    }

    private void GrowBlob(GameObject blob)
    {
        if (blob == null)
            return;

        currentBlobSize += blobGrowthRate * Time.deltaTime;

        if (currentBlobSize >= blobMaxSize)
        {
            OverheatBlob(blob);
        }
        else
        {
            blob.transform.localScale = Vector3.one * currentBlobSize;
        }
    }

    private void OverheatBlob(GameObject blob)
    {
        if (holePrefab != null)
        {
            GameObject hole = Instantiate(holePrefab, blob.transform.position, blob.transform.rotation);
            hole.transform.localScale = blob.transform.localScale;
        }

        Destroy(blob);
        currentBlob = null;
        currentBlobSize = 0f;
        isOverheating = true;

        StartCoroutine(OverheatingCooldown());
        StopWelding();
    }

    /// <summary>
    /// Creates a new blob at the current weld position (called by time-based system)
    /// </summary>
    private void CreateNewBlobAtCurrentPosition()
    {
        if (!isOnWeldableSurface || weldBlobPrefab == null)
            return;

        // If we hit an existing blob, grow it instead of creating new one
        if (currentHit.transform.gameObject.layer == blobLayer)
        {
            GrowBlob(currentHit.transform.gameObject);
            return;
        }

        // Create new blob at current position
        RecordTravelTimeForPanel();
        CreateNewBlob();
    }

    /// <summary>
    /// Sends accumulated travel time to the current WeldingPanel, if any.
    /// This mirrors WeldingHandle.SetBlobTravelTime so WeldingPanel can
    /// compute travel uniformity stats.
    /// </summary>
    private void RecordTravelTimeForPanel()
    {
        if (currentPanel != null && travelTimer > 0f)
        {
            currentPanel.AddWeldTravel(travelTimer);
            travelTimer = 0f;
        }
    }

    private void FinalizeBlob()
    {
        if (currentBlob != null)
        {
            if (currentBlobParent != null)
            {
                currentBlob.transform.SetParent(currentBlobParent);
            }

            previousBlob = currentBlob;
        }

        currentBlob = null;
        currentBlobSize = 0f;
    }

    private IEnumerator OverheatingCooldown()
    {
        yield return new WaitForSeconds(overheatingCooldown);
        isOverheating = false;
    }

    public Vector3 GetWeldPoint()
    {
        if (isOnWeldableSurface)
            return currentHit.point;
        return weldingTip.position + weldingTip.forward * raycastDistance;
    }

    public bool IsWelding()
    {
        return isWelding && isOnWeldableSurface;
    }

    /// <summary>Panel under the tip from the last raycast (when on a weldable surface).</summary>
    public WeldingPanel CurrentWeldingPanelUnderTip => currentPanel;

    public bool IsWeldableSurfaceUnderTip() => isOnWeldableSurface;

    public bool AreWeldingPrerequisitesMet() => AllWeldingConditionsMet();

    public bool IsTriggerHeldForWelding() => GetWeldingTriggerHeld();
    
    /// <summary>
    /// Calculates travel speed based on movement
    /// </summary>
    private void CalculateTravelSpeed()
    {
        if (lastWeldPosition == Vector3.zero)
        {
            lastWeldPosition = currentHit.point;
            travelSpeed = idealTravelSpeed; // Default to ideal speed
            return;
        }
        
        float distance = Vector3.Distance(lastWeldPosition, currentHit.point);
        travelSpeed = distance / Mathf.Max(Time.deltaTime, 0.0001f);
        
        // Update last position
        lastWeldPosition = currentHit.point;
    }
    
    /// <summary>
    /// Calculates blob width based on travel speed
    /// Fast = narrow (minBlobWidth), Slow = wide (maxBlobWidth), OK = blobInitialSize
    /// </summary>
    private float CalculateSpeedBasedWidth()
    {
        if (travelSpeed <= 0.01f)
            return maxBlobWidth; // Very slow = wide
        
        // Normalize speed relative to ideal speed
        float speedRatio = travelSpeed / idealTravelSpeed;
        
        // Fast movement = narrow blobs, slow movement = wide blobs
        // Inverse relationship: width decreases as speed increases
        float widthRatio = 1f / (1f + (speedRatio - 1f) * speedSensitivity);
        
        // Map to min/max width range
        float width = Mathf.Lerp(minBlobWidth, maxBlobWidth, widthRatio);
        
        return width;
    }
    
    /// <summary>
    /// Calculates blob height/thickness based on travel speed
    /// Fast = thin (minBlobHeight), Slow = thick (maxBlobHeight), OK = medium
    /// </summary>
    private float CalculateSpeedBasedHeight()
    {
        if (travelSpeed <= 0.01f)
            return maxBlobHeight; // Very slow = thick
        
        // Normalize speed relative to ideal speed
        float speedRatio = travelSpeed / idealTravelSpeed;
        
        // Fast = thin, slow = thick
        // Inverse relationship: height decreases as speed increases
        float heightRatio = 1f / (1f + (speedRatio - 1f) * speedSensitivity);
        
        // Map to min/max height range
        float height = Mathf.Lerp(minBlobHeight, maxBlobHeight, heightRatio);
        
        return height;
    }
}

