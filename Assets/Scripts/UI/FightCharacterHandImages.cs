using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Fight scene: sets left/right hand UI images from <see cref="PlayerDataSO.LeftHandImage"/> /
/// <see cref="PlayerDataSO.RightHandImage"/> on the active character
/// (<see cref="PlayerDataContainer.ActiveCharacterTemplate"/>).
/// </summary>
public sealed class FightCharacterHandImages : MonoBehaviour
{
    [SerializeField] private Image leftHandImage;
    [SerializeField] private Image rightHandImage;

    void Awake()
    {
        if (leftHandImage == null)
            throw new System.InvalidOperationException($"FightCharacterHandImages on '{name}': assign leftHandImage.");
        if (rightHandImage == null)
            throw new System.InvalidOperationException($"FightCharacterHandImages on '{name}': assign rightHandImage.");
    }

    void OnEnable()
    {
        ApplyCharacterHands();
    }

    void ApplyCharacterHands()
    {
        var container = PlayerDataContainer.Instance;
        if (container?.ActiveCharacterTemplate == null)
        {
            ApplyHandImage(leftHandImage, null);
            ApplyHandImage(rightHandImage, null);
            return;
        }

        var character = container.ActiveCharacterTemplate;
        ApplyHandImage(leftHandImage, character.LeftHandImage);
        ApplyHandImage(rightHandImage, character.RightHandImage);
    }

    static void ApplyHandImage(Image image, Sprite sprite)
    {
        image.sprite = sprite;
        image.enabled = sprite != null;
    }
}
