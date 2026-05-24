using UnityEngine;

/// <summary>Shared panel visibility rules for Dice Select progression popups.</summary>
public abstract class ProgressionCelebrationPopupViewBase : MonoBehaviour
{
    [SerializeField] protected GameObject panelRoot;

    protected void ResolvePanelRootInAwake()
    {
        if (panelRoot != null && panelRoot != gameObject)
            return;

        var panel = transform.Find("Panel");
        if (panel != null)
        {
            panelRoot = panel.gameObject;
            return;
        }

        if (panelRoot == gameObject)
        {
            Debug.LogError(
                $"{GetType().Name} on '{name}': panelRoot must be the child Panel, not this GameObject. " +
                "Assign the Panel object or add a child named 'Panel'.",
                this);
        }
    }

    protected void ShowPanel()
    {
        gameObject.SetActive(true);
        if (panelRoot != null)
            panelRoot.SetActive(true);
    }

    protected void HidePanelImmediate()
    {
        if (panelRoot != null && panelRoot != gameObject)
            panelRoot.SetActive(false);
    }
}
