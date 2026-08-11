using UnityEngine;

/// <summary>
/// Scene toggle for <see cref="SnapGuideCollisionIgnore.Enabled"/>.
/// Leave unchecked so sheet/gun welding contact is not affected by IgnoreCollision.
/// </summary>
[DisallowMultipleComponent]
public class SnapGuideCollisionIgnoreSettings : MonoBehaviour
{
    [Tooltip("Master switch for snap-guide Physics.IgnoreCollision. OFF by default — leave off if blobs / tip contact fail.")]
    public bool useIgnoreCollision = false;

    void Awake() => Apply();
    void OnEnable() => Apply();
    void OnValidate() => Apply();

    void Apply()
    {
        SnapGuideCollisionIgnore.Enabled = useIgnoreCollision;
    }
}
