using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// One row in the unknown-event option list. Assign on the prefab root: a <see cref="Button"/> and the <see cref="TextMeshProUGUI"/> that shows the choice label from <see cref="UnknownMapEventOptionEntry.label"/>.
/// </summary>
public sealed class UnknownMapEventChoiceRowView : MonoBehaviour
{
    [SerializeField] private Button button;
    [SerializeField] private TextMeshProUGUI labelText;

    public void Bind(string label, UnityAction onClick)
    {
        if (labelText == null)
        {
            Debug.LogError("UnknownMapEventChoiceRowView: assign Label Text (TextMeshProUGUI).", this);
            return;
        }

        labelText.text = string.IsNullOrWhiteSpace(label) ? "Choose" : label.Trim();
        labelText.raycastTarget = false;

        if (button == null)
        {
            Debug.LogError("UnknownMapEventChoiceRowView: assign Button.", this);
            return;
        }

        ConfigureClickRouting(button, onClick);
    }

    /// <summary>
    /// Prefab has a root <see cref="Button"/> plus a child "BG" button and decorative images; route all hits to <paramref name="primary"/>.
    /// </summary>
    private void ConfigureClickRouting(Button primary, UnityAction onClick)
    {
        foreach (var extra in GetComponentsInChildren<Button>(true))
        {
            if (extra != primary)
                extra.enabled = false;
        }

        Image hitTarget = null;
        foreach (var img in GetComponentsInChildren<Image>(true))
        {
            var useForHits = img.gameObject.name == "BG";
            if (useForHits && hitTarget == null)
                hitTarget = img;
            img.raycastTarget = useForHits;
        }

        if (hitTarget == null)
        {
            hitTarget = primary.GetComponent<Image>();
            if (hitTarget != null)
                hitTarget.raycastTarget = true;
        }

        if (hitTarget != null)
            primary.targetGraphic = hitTarget;

        primary.interactable = true;
        primary.onClick.RemoveAllListeners();
        primary.onClick.AddListener(onClick);
    }
}
