using UnityEngine;

/// <summary>
/// Ghost guide for the first reference piece. Behaves exactly like <see cref="exrefpiece"/>
/// (starts hidden, shown by its paired <see cref="refpiece"/> while held, supplies the snap pose
/// via <see cref="exrefpiece.GetSnapTransform"/>), but is a distinct type so it can be assigned to
/// the matching real <see cref="refpiece"/> in the Inspector.
/// </summary>
public class examplereference1 : exrefpiece
{
}
