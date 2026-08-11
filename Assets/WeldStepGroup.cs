using UnityEngine;

/// <summary>
/// Aggregates several <see cref="IWeldStepCompletable"/> sources into one completion signal so a single
/// <see cref="WeldStepSequencer"/> step can require multiple things — e.g. "both ref pieces snapped" or
/// "all four bars snapped". Drop this in as a step's completion source.
/// </summary>
public class WeldStepGroup : MonoBehaviour, IWeldStepCompletable
{
    [Tooltip("Completion sources to combine (weldbar / refpiece / weld sequences / etc.). Drag the specific components, not the GameObjects.")]
    public MonoBehaviour[] sources;

    [Tooltip("If true (default), this group is complete only when ALL sources are complete. If false, complete when ANY source is complete.")]
    public bool requireAll = true;

    public bool IsStepComplete
    {
        get
        {
            if (sources == null || sources.Length == 0)
                return false;

            bool anyValid = false;
            foreach (var s in sources)
            {
                IWeldStepCompletable c = ResolveCompletable(s);
                if (c != null)
                {
                    anyValid = true;
                    if (requireAll && !c.IsStepComplete)
                        return false;
                    if (!requireAll && c.IsStepComplete)
                        return true;
                }
            }

            // requireAll: every valid source passed. any: none passed.
            return anyValid && requireAll;
        }
    }

    /// <summary>
    /// Accepts a completable directly, or any component on the same GameObject that implements it — so dragging
    /// e.g. the Grabbable (instead of the refpiece) on the same object still resolves correctly.
    /// </summary>
    static IWeldStepCompletable ResolveCompletable(MonoBehaviour source)
    {
        if (source == null)
            return null;
        if (source is IWeldStepCompletable direct)
            return direct;
        return source.GetComponent<IWeldStepCompletable>();
    }
}
