using UnityEngine;

/// <summary>
/// Forwards physics messages to <see cref="refpiece"/> when the Rigidbody lives on a child
/// (Unity sends collisions to the Rigidbody GameObject, not the parent that holds <see cref="refpiece"/>).
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class RefPieceCollisionRelay : MonoBehaviour
{
    public refpiece owner;

    void OnCollisionEnter(Collision collision)
    {
        if (owner != null)
            owner.HandleCollision(collision);
    }

    void OnCollisionStay(Collision collision)
    {
        if (owner != null)
            owner.HandleCollision(collision);
    }

    void OnTriggerEnter(Collider other)
    {
        if (owner != null)
            owner.HandleTrigger(other);
    }
}
