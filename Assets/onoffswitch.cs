using UnityEngine;
using TMPro;
using BNG;

public class onoffswitch : MonoBehaviour
{
    [Header("Toggle Objects")]
    [Tooltip("First state object (visible when switch is in state A).")]
    public GameObject firstObject;

    [Tooltip("Second state object (visible when switch is in state B).")]
    public GameObject secondObject;

    [Tooltip("If true, firstObject starts visible. If false, secondObject starts visible.")]
    public bool startWithFirstVisible = true;

    [Header("MIG Welder UI (optional)")]
    [Tooltip("Text shown when firstObject is visible (welder off).")]
    public TMP_Text migStatusText;
    [TextArea(2, 4)]
    public string messageWhenFirstVisible = "MIG Welder is Off — turn on the MIG Welder.";
    [TextArea(1, 3)]
    public string messageWhenSecondVisible = "";

    private Grabbable firstGrabbable;
    private Grabbable secondGrabbable;
    private bool wasHoldingActiveObject;

    void Start()
    {
        if (firstObject != null)
            firstGrabbable = firstObject.GetComponent<Grabbable>() ?? firstObject.GetComponentInParent<Grabbable>();

        if (secondObject != null)
            secondGrabbable = secondObject.GetComponent<Grabbable>() ?? secondObject.GetComponentInParent<Grabbable>();

        SetState(startWithFirstVisible);
        wasHoldingActiveObject = IsHoldingActiveObject();
        UpdateMigStatusText();
    }

    void Update()
    {
        bool isHoldingNow = IsHoldingActiveObject();

        // Toggle once on grab start (rising edge), not every frame while held.
        if (isHoldingNow && !wasHoldingActiveObject)
        {
            ToggleState();
        }

        wasHoldingActiveObject = isHoldingNow;
    }

    void ToggleState()
    {
        bool firstIsActive = firstObject != null && firstObject.activeSelf;
        SetState(!firstIsActive);
    }

    void SetState(bool firstVisible)
    {
        if (firstObject != null)
            firstObject.SetActive(firstVisible);

        if (secondObject != null)
            secondObject.SetActive(!firstVisible);

        UpdateMigStatusText();
    }

    /// <summary>
    /// True when the welder is considered ON (second state visible).
    /// Use this from CustomWeldingController to gate welding.
    /// </summary>
    public bool IsWelderOn()
    {
        return secondObject != null && secondObject.activeSelf;
    }

    /// <summary>Debug/test: force the welder ON (second object visible).</summary>
    public void ForceWelderOn()
    {
        SetState(false);
    }

    void UpdateMigStatusText()
    {
        if (migStatusText == null)
            return;

        if (firstObject != null && firstObject.activeSelf)
            migStatusText.text = messageWhenFirstVisible;
        else if (secondObject != null && secondObject.activeSelf)
            migStatusText.text = messageWhenSecondVisible;
        else
            migStatusText.text = "";
    }

    bool IsHoldingActiveObject()
    {
        bool firstActive = firstObject != null && firstObject.activeSelf;
        bool secondActive = secondObject != null && secondObject.activeSelf;

        if (firstActive && firstGrabbable != null)
            return firstGrabbable.BeingHeld;

        if (secondActive && secondGrabbable != null)
            return secondGrabbable.BeingHeld;

        return false;
    }
}
