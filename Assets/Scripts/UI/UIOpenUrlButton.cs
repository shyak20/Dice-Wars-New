using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

/// <summary>
/// Wire to a <see cref="Button"/> (same GameObject or assigned). Opens <see cref="url"/> in the browser / OS handler (Discord, web, etc.).
/// </summary>
public class UIOpenUrlButton : MonoBehaviour
{
    [SerializeField] private Button button;

    [FormerlySerializedAs("inviteUrl")]
    [Tooltip("Any http(s) or app URL (e.g. Discord invite).")]
    [SerializeField] private string url = "https://discord.gg/e8GswGH5";

    void Awake()
    {
        if (button == null)
            button = GetComponent<Button>();

        if (button == null)
        {
            Debug.LogError($"UIOpenUrlButton on '{name}': assign a Button or place this component on the same GameObject as the Button.");
            return;
        }

        if (string.IsNullOrWhiteSpace(url))
        {
            Debug.LogError($"UIOpenUrlButton on '{name}': url is empty.");
            return;
        }

        button.onClick.AddListener(OpenUrl);
    }

    void OnDestroy()
    {
        if (button != null)
            button.onClick.RemoveListener(OpenUrl);
    }

    void OpenUrl()
    {
        Application.OpenURL(url);
    }
}
