using UnityEngine;

/// <summary>
/// Bad-weld indicator: starts hidden. <see cref="WeldParameterMonitor"/> shows it when settings are out of tolerance.
/// Keep this component on an active GameObject; toggle <see cref="visualRoot"/> or renderers, not this object, unless you use <see cref="deactivateSelfWhenHidden"/>.
/// </summary>
public class badweld : MonoBehaviour
{
    [Header("What to show/hide")]
    [Tooltip("If set, this object's active state is toggled. If null, all Renderers under this object are toggled.")]
    public GameObject visualRoot;

    [Tooltip("If true, sets this GameObject inactive when hidden (script stops updating). Prefer visualRoot or renderers.")]
    public bool deactivateSelfWhenHidden;

    void Awake()
    {
        HideBad();
    }

    public void ShowBad()
    {
        SetVisible(true);
    }

    public void HideBad()
    {
        SetVisible(false);
    }

    void SetVisible(bool visible)
    {
        if (deactivateSelfWhenHidden)
        {
            gameObject.SetActive(visible);
            return;
        }

        if (visualRoot != null)
        {
            visualRoot.SetActive(visible);
            return;
        }

        foreach (var r in GetComponentsInChildren<Renderer>(true))
            r.enabled = visible;
    }
}
