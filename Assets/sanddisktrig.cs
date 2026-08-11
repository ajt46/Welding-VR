using BNG;
using UnityEngine;

/// <summary>
/// Trigger driver for an angle grinder sanding wheel. Place on the parent (grabbable) object;
/// the spinning <see cref="sanddisk"/> lives on a child. While the parent is held and the grip-hand
/// trigger is pressed past <see cref="triggerThreshold"/>, the disk spins.
/// </summary>
public class sanddisktrig : MonoBehaviour
{
    [Header("References")]
    [Tooltip("Grabbable on this grinder. If null, auto-finds on this object or its parents.")]
    public Grabbable grinderGrabbable;

    [Tooltip("The child sanding disk to spin. If null, auto-finds in children.")]
    public sanddisk disk;

    [Header("Trigger")]
    [Tooltip("Trigger axis (0-1) must exceed this to spin the disk.")]
    [Range(0f, 1f)]
    public float triggerThreshold = 0.5f;

    [Tooltip("If true, only the trigger on the hand holding the grinder spins it. If false, either trigger works.")]
    public bool useHandMatchedTrigger = true;

    InputBridge _inputBridge;

    void Awake()
    {
        if (grinderGrabbable == null)
            grinderGrabbable = GetComponent<Grabbable>() ?? GetComponentInParent<Grabbable>();

        if (disk == null)
            disk = GetComponentInChildren<sanddisk>();
    }

    void Start()
    {
        _inputBridge = InputBridge.Instance;
    }

    void Update()
    {
        if (disk == null)
            return;

        bool spin = grinderGrabbable != null &&
                    grinderGrabbable.BeingHeld &&
                    GetTriggerValue() > triggerThreshold;

        disk.SetSpinning(spin);
    }

    float GetTriggerValue()
    {
        if (_inputBridge == null)
            _inputBridge = InputBridge.Instance;
        if (_inputBridge == null)
            return 0f;

        if (grinderGrabbable != null && !grinderGrabbable.BeingHeld)
            return 0f;

        if (!useHandMatchedTrigger || grinderGrabbable == null)
            return Mathf.Max(_inputBridge.LeftTrigger, _inputBridge.RightTrigger);

        Grabber grabber = grinderGrabbable.GetPrimaryGrabber();
        if (grabber == null)
            return Mathf.Max(_inputBridge.LeftTrigger, _inputBridge.RightTrigger);

        return grabber.HandSide == ControllerHand.Right
            ? _inputBridge.RightTrigger
            : _inputBridge.LeftTrigger;
    }
}
