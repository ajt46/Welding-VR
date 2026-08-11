using UnityEngine;
using BNG;

/// <summary>
/// Bridges VR (or legacy) input to the existing WeldingHandle and ties spark visibility to the trigger.
/// Attach to the same object as WeldingHandle (e.g. MIG gun).
/// - Calls GetWeldPoint() every frame so the handle's raycast stays up to date.
/// - When trigger is held: StartWelding() and sparks ON. When released: StopWelding() and sparks OFF.
/// Sparks are invisible until the trigger is held, then hidden again when released.
/// </summary>
public class WeldingTriggerAndSparks : MonoBehaviour
{
    [Header("Welding Handle")]
    [Tooltip("The WeldingHandle that creates blobs (from the example prefab). If null, will use GetComponent.")]
    public WeldingHandle weldingHandle;

    [Header("Sparks (Particle System)")]
    [Tooltip("Particle system that looks like sparks. Visible only while trigger is held. If null, will search in children.")]
    public ParticleSystem sparks;

    [Header("VR Input (BNG)")]
    [Tooltip("Grabbable on the gun. If null, will use GetComponent on this or parent.")]
    public Grabbable grabbable;
    [Tooltip("Trigger value above this counts as 'pressed' (0-1).")]
    [Range(0f, 1f)]
    public float triggerThreshold = 0.1f;

    [Header("Optional: Desktop / Legacy")]
    [Tooltip("If set, welding and sparks use this when not in VR (e.g. RightHandcontrol with DragImage).")]
    public MonoBehaviour legacyInput;
    [Tooltip("Use legacy input (IsInteracting + IsOn) instead of VR trigger. Turn on for non-VR scenes.")]
    public bool useLegacyInput;

    [Header("Debug")]
    [Tooltip("Log trigger values and spark state each frame (for troubleshooting).")]
    public bool logInputDebug = false;

    private InputBridge _inputBridge;
    private bool _sparksWereOn;

    void Awake()
    {
        if (GetComponent<CustomWeldingController>() != null ||
            GetComponentInParent<CustomWeldingController>() != null ||
            GetComponentInChildren<CustomWeldingController>(true) != null)
        {
            Debug.LogWarning(
                $"Disabling {nameof(WeldingTriggerAndSparks)} on '{gameObject.name}' — {nameof(CustomWeldingController)} already drives sparks and triggers. " +
                $"Remove this component to avoid sparks being forced off every frame.",
                this);
            enabled = false;
            return;
        }

        // Ensure sparks are off before PlayOnAwake / prewarm can show anything
        if (sparks == null)
            sparks = GetComponentInChildren<ParticleSystem>();

        ForceSparksOffImmediate();
    }

    void OnEnable()
    {
        // Scene reloads / enabling object should never flash sparks
        ForceSparksOffImmediate();
    }

    void Start()
    {
        if (weldingHandle == null)
            weldingHandle = GetComponent<WeldingHandle>();
        if (weldingHandle == null)
            weldingHandle = GetComponentInParent<WeldingHandle>();

        if (sparks == null)
            sparks = GetComponentInChildren<ParticleSystem>();

        if (grabbable == null)
            grabbable = GetComponent<Grabbable>();
        if (grabbable == null)
            grabbable = GetComponentInParent<Grabbable>();

        _inputBridge = InputBridge.Instance;

        ForceSparksOffImmediate();
    }

    void Update()
    {
        bool triggerHeld = GetTriggerHeld();

        // Keep weld point up to date (raycast from tip) so WeldingHandle knows if we're on panel/blob
        if (weldingHandle != null)
        {
            weldingHandle.GetWeldPoint();

            if (triggerHeld)
                weldingHandle.StartWelding();
            else
                weldingHandle.StopWelding();
        }

        // Sparks: visible only when trigger held
        SetSparksActive(triggerHeld);
    }

    bool GetTriggerHeld()
    {
        if (useLegacyInput && legacyInput != null)
        {
            if (legacyInput is RightHandcontrol rhc)
                return rhc.IsInteracting() && rhc.IsOn();
        }

        if (_inputBridge == null)
            return false;

        float triggerValue = _inputBridge.RightTrigger;
        bool isHeld = grabbable == null || grabbable.BeingHeld;
        bool pressed = isHeld && triggerValue > triggerThreshold;

        if (logInputDebug)
        {
            Debug.Log($"[WeldingTriggerAndSparks] RightTrigger={triggerValue:F2}, BeingHeld={isHeld}, pressed={pressed}");
        }

        return pressed;
    }

    void SetSparksActive(bool on)
    {
        if (sparks == null) return;
        if (on == _sparksWereOn) return;

        _sparksWereOn = on;
        if (on)
        {
            if (!sparks.gameObject.activeSelf)
                sparks.gameObject.SetActive(true);

            var emission = sparks.emission;
            emission.enabled = true;

            var rend = sparks.GetComponent<ParticleSystemRenderer>();
            if (rend != null) rend.enabled = true;

            sparks.Play(true);
        }
        else
        {
            ForceSparksOffImmediate();
        }
    }

    void ForceSparksOffImmediate()
    {
        if (sparks == null) return;

        _sparksWereOn = false;

        if (!sparks.gameObject.activeSelf)
            sparks.gameObject.SetActive(true); // ensure we can modify modules, then hide via renderer/emission

        // Prevent it from ever auto-starting
        var main = sparks.main;
        main.playOnAwake = false;

        var emission = sparks.emission;
        emission.enabled = false;

        // Stop and clear any already-spawned particles (prewarm / first frame flash)
        sparks.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        sparks.Clear(true);

        var rend = sparks.GetComponent<ParticleSystemRenderer>();
        if (rend != null) rend.enabled = false;

        // Finally, hide the whole object so nothing residual is visible
        sparks.gameObject.SetActive(false);
    }
}
