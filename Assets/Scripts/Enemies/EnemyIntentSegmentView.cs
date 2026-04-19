using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Enemies
{
    /// <summary>Single row on the enemy intent strip (prefab root). Wire icon + value text here.</summary>
    public class EnemyIntentSegmentView : MonoBehaviour
    {
        [SerializeField] private Image iconImage;
        [SerializeField] private TMP_Text valueText;

        public void Bind(Sprite icon, string value)
        {
            if (iconImage != null)
            {
                iconImage.sprite = icon;
                iconImage.enabled = icon != null;
            }

            if (valueText != null)
            {
                valueText.richText = true;
                valueText.text = value ?? "";
            }
        }
    }
}
