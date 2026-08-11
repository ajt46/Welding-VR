using UnityEngine;

/// <summary>
/// Forwards physics events to <see cref="ungroundedgrabbable"/> when the <see cref="Rigidbody"/>
/// lives on a child (Unity reports collisions on the body that has the collider + rigidbody).
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class WeldingSheetSnapRelay : MonoBehaviour
{
    public ungroundedgrabbable owner;

    void OnCollisionEnter(Collision collision)
    {
        if (owner != null && collision != null)
            owner.HandleSheetSnapContact(collision.collider, "collision");
    }

    void OnCollisionStay(Collision collision)
    {
        if (owner != null && collision != null)
            owner.HandleSheetSnapContact(collision.collider, "collision stay");
    }

    void OnTriggerEnter(Collider other)
    {
        if (owner != null)
            owner.HandleSheetSnapContact(other, "trigger");
    }
}
