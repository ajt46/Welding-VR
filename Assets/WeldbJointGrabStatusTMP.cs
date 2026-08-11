using BNG;
using TMPro;
using UnityEngine;

/// <summary>
/// Drives a TextMesh Pro label from weld completion and merged joint grab state:
/// "Joint not complete" until eight spot welds finish; once complete:
/// "Joint Grabbed" while holding the joint, otherwise "Joint not grabbed".
/// </summary>
public class WeldbJointGrabStatusTMP : MonoBehaviour
{
    [Tooltip("WeldbarAssemblyRoot parent of the welded joint (usually where Grabbable lives after merge).")]
    public WeldbarAssemblyRoot assemblyRoot;

    [Tooltip("If set, overrides mergeAfterEightSpotWelds on WeldbarAssemblyRoot.")]
    public SequentialWeldRevealSequence weldSequenceOverride;

    [Tooltip("Merged joint Grabbable. If empty, uses GetComponent on assembly root.")]
    public Grabbable jointGrabbable;

    public TMP_Text statusText;

    [Header("Copy (edit if needed)")]
    public string jointGrabbedMessage = "Joint Grabbed";
    public string jointNotCompleteMessage = "Joint not complete";
    public string jointNotGrabbedMessage = "Joint not grabbed";

    SequentialWeldRevealSequence WeldSequence =>
        weldSequenceOverride != null ? weldSequenceOverride
        : assemblyRoot != null ? assemblyRoot.mergeAfterEightSpotWelds
        : null;

    bool EightWeldsComplete()
    {
        SequentialWeldRevealSequence seq = WeldSequence;
        return seq != null && seq.HasCompletedAllWeldSteps;
    }

    /// <summary>Merged joint gets <see cref="Grabbable"/> added at runtime during merge.</summary>
    Grabbable ResolveJointGrabbable()
    {
        if (jointGrabbable != null)
            return jointGrabbable;
        if (assemblyRoot != null)
            jointGrabbable = assemblyRoot.GetComponent<Grabbable>();
        return jointGrabbable;
    }

    string CurrentMessage(bool weldsDone, bool holding)
    {
        if (!weldsDone)
            return jointNotCompleteMessage;
        if (holding)
            return jointGrabbedMessage;
        return jointNotGrabbedMessage;
    }

    void Awake()
    {
        if (WeldSequence == null)
            Debug.LogWarning($"{nameof(WeldbJointGrabStatusTMP)} ({name}): assign {nameof(weldSequenceOverride)} or " +
                             $"{nameof(WeldbarAssemblyRoot.mergeAfterEightSpotWelds)} so eight weld completion can be read.", this);
    }

    string _last;

    void Update()
    {
        if (statusText == null)
            return;

        bool weldsDone = EightWeldsComplete();
        Grabbable grab = ResolveJointGrabbable();
        bool holding = grab != null && grab.BeingHeld;

        string msg = CurrentMessage(weldsDone, holding);
        if (msg != _last)
        {
            _last = msg;
            statusText.text = msg;
        }
    }
}
