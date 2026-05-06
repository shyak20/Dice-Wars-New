using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Enemies
{
    /// <summary>Single row on the enemy intent strip (prefab root). Wire icon + value text here.</summary>
    public class EnemyIntentSegmentView : MonoBehaviour
    {
        [SerializeField] private Image backgroundImage;
        [SerializeField] private Image iconImage;
        [SerializeField] private TMP_Text valueText;
        [Header("Optional status tooltip")]
        [Tooltip("Optional custom hover target. If unset, uses iconImage GameObject, then this GameObject.")]
        [SerializeField] private GameObject tooltipHoverTargetObject;
        [SerializeField] private HoverTooltipTargetUI hoverTooltipTarget;
        [Tooltip("Can be a scene instance or a prefab asset. If prefab, a runtime instance is created under this segment's Canvas.")]
        [SerializeField] private HoverTooltipPanelUI hoverTooltipPanel;
        [Tooltip("Tooltip offset in screen pixels relative to this segment's hover target.")]
        [SerializeField] private Vector2 tooltipScreenOffset = new Vector2(0f, 24f);
        private HoverTooltipPanelUI _runtimeTooltipPanel;

        private void Awake()
        {
            if (hoverTooltipTarget == null)
            {
                var hoverGo = tooltipHoverTargetObject != null
                    ? tooltipHoverTargetObject
                    : (iconImage != null ? iconImage.gameObject : gameObject);
                hoverTooltipTarget = hoverGo.GetComponent<HoverTooltipTargetUI>() ?? hoverGo.AddComponent<HoverTooltipTargetUI>();
            }

            hoverTooltipTarget.SetTooltipScreenOffset(tooltipScreenOffset);
        }

        public void Bind(Sprite icon, string value, string statusTitle = null, string statusDescription = null, bool enableTooltip = false, Sprite background = null)
        {
            if (backgroundImage != null)
            {
                backgroundImage.sprite = background;
                backgroundImage.enabled = background != null;
            }

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

            if (hoverTooltipTarget == null)
                return;

            if (enableTooltip)
            {
                var title = string.IsNullOrWhiteSpace(statusTitle) ? "Action" : statusTitle;
                var description = statusDescription ?? "";
                var panel = ResolveTooltipPanel();
                if (panel != null)
                    hoverTooltipTarget.Configure(panel, title, description);
                else
                    hoverTooltipTarget.SetContent(title, description);
                hoverTooltipTarget.enabled = true;
            }
            else
            {
                hoverTooltipTarget.SetContent("", "");
                hoverTooltipTarget.enabled = false;
            }
        }

        private HoverTooltipPanelUI ResolveTooltipPanel()
        {
            if (_runtimeTooltipPanel != null)
                return _runtimeTooltipPanel;

            if (hoverTooltipPanel == null)
                return null;

            // Scene object assigned directly.
            if (hoverTooltipPanel.gameObject.scene.IsValid())
            {
                _runtimeTooltipPanel = hoverTooltipPanel;
                return _runtimeTooltipPanel;
            }

            // Prefab asset assigned in inspector: instantiate once under closest canvas.
            var canvas = GetComponentInParent<Canvas>();
            if (canvas == null)
                return null;

            _runtimeTooltipPanel = Instantiate(hoverTooltipPanel, canvas.transform);
            _runtimeTooltipPanel.name = hoverTooltipPanel.name;
            return _runtimeTooltipPanel;
        }
    }
}
