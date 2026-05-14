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

        if (button == null)
        {
            Debug.LogError("UnknownMapEventChoiceRowView: assign Button.", this);
            return;
        }

        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(onClick);
    }
}
