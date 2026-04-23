using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// One gem socket view inside a die tooltip.
/// Shows gem icon when filled, otherwise shows empty-slot icon.
/// </summary>
public sealed class DieTooltipGemSlotView : MonoBehaviour
{
    [SerializeField] private Image gemIconImage;
    [SerializeField] private Image emptySlotIconImage;

    public void Bind(GemSO gem)
    {
        var hasGem = gem != null && gem.icon != null;

        if (gemIconImage != null)
        {
            gemIconImage.sprite = hasGem ? gem.icon : null;
            gemIconImage.enabled = hasGem;
            gemIconImage.gameObject.SetActive(hasGem);
        }

        if (emptySlotIconImage != null)
        {
            emptySlotIconImage.enabled = !hasGem;
            emptySlotIconImage.gameObject.SetActive(!hasGem);
        }
    }

    public GameObject GetHoverTarget()
    {
        if (gemIconImage != null)
            return gemIconImage.gameObject;
        return gameObject;
    }
}
