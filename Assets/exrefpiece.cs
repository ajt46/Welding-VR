using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Ghost guide for a real <see cref="refpiece"/> (same role as <see cref="exampleclamp"/> for <see cref="clamp"/>).
/// Starts hidden and is shown by its paired <see cref="refpiece"/> only while the real object is held;
/// supplies the snap pose via <see cref="GetSnapTransform"/>.
/// </summary>
public class exrefpiece : MonoBehaviour
{
    [Header("Matching")]
    [Tooltip("Asset key used by refpiece.cs to match this guide.")]
    public string nameofasset;

    [Header("Visibility")]
    [Tooltip("If true, the guide starts hidden. It should be visible only while the real ref piece is held.")]
    public bool startHidden = true;

    [Header("Snap Pose")]
    [Tooltip("Transform used as the target pose when snapping the real ref piece. If null, this transform is used.")]
    public Transform snapTransform;

    void Start()
    {
        if (snapTransform == null)
            snapTransform = transform;

        SetVisible(!startHidden);
    }

    /// <summary>
    /// Sets guide visibility. Dedicated ghost GOs use SetActive so renderers and colliders stay in sync
    /// (hidden = not collidable).
    /// </summary>
    public void SetVisible(bool visible)
    {
        gameObject.SetActive(visible);
    }

    /// <summary>Returns the transform that represents the desired snap pose.</summary>
    public Transform GetSnapTransform()
    {
        return snapTransform != null ? snapTransform : transform;
    }

    /// <summary>Colliders used for IgnoreCollision with a seated ref piece.</summary>
    public Collider[] GetCollidersForIgnore()
    {
        var list = new List<Collider>();
        Collider[] cols = GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < cols.Length; i++)
        {
            if (cols[i] != null && !list.Contains(cols[i]))
                list.Add(cols[i]);
        }
        return list.ToArray();
    }
}
