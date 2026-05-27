using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Sets a UI <see cref="Image"/> from <see cref="PlayerDataSO.LoseScreenImage"/> on the active character
/// (<see cref="PlayerDataContainer.ActiveCharacterTemplate"/>).
/// </summary>
public sealed class LoseScreen : MonoBehaviour
{
    [SerializeField] private Image image;

    void Awake()
    {
        if (image == null)
            throw new System.InvalidOperationException($"LoseScreen on '{name}': assign image.");
    }

    void OnEnable()
    {
        ApplyCharacterImage();
    }

    void ApplyCharacterImage()
    {
        var container = PlayerDataContainer.Instance;
        if (container?.ActiveCharacterTemplate == null)
        {
            image.sprite = null;
            image.enabled = false;
            return;
        }

        var sprite = container.ActiveCharacterTemplate.LoseScreenImage;
        image.sprite = sprite;
        image.enabled = sprite != null;
    }
}
