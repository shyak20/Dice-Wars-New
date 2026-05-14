using System;
using System.Collections;
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
        [Header("Hover (HoverTooltipManager)")]
        [Tooltip("Screen-space offset passed to HoverTooltipManager for this row.")]
        [SerializeField] private Vector2 tooltipScreenOffset = new Vector2(0f, 24f);

        HoverTooltipTargetUI _hoverTooltipTarget;
        Vector3 _pulseBaseLocalScale = Vector3.one;

        static Sprite _hoverRaycastSprite;

        void Awake() => _pulseBaseLocalScale = transform.localScale;

        /// <summary>
        /// Ensures this row is active, scales to <paramref name="peakMultiplier"/> × base (optional rise), holds, invokes <paramref name="onAtPeak"/>, then hides this GameObject (e.g. next multi-hit on the same row re-shows for its pulse).
        /// </summary>
        public IEnumerator CoPerformScalePulseWithPeakCallback(float peakMultiplier, float riseSeconds, float peakHoldSeconds, Action onAtPeak)
        {
            if (peakMultiplier <= 0f)
                peakMultiplier = 1f;

            if (!gameObject.activeSelf)
                gameObject.SetActive(true);

            transform.localScale = _pulseBaseLocalScale;
            var peakScale = _pulseBaseLocalScale * peakMultiplier;
            if (riseSeconds > 0f)
            {
                var t = 0f;
                var start = transform.localScale;
                while (t < riseSeconds)
                {
                    t += Time.deltaTime;
                    var k = Mathf.Clamp01(t / riseSeconds);
                    transform.localScale = Vector3.Lerp(start, peakScale, k);
                    yield return null;
                }
            }

            transform.localScale = peakScale;
            if (peakHoldSeconds > 0f)
                yield return new WaitForSeconds(peakHoldSeconds);
            onAtPeak?.Invoke();
            gameObject.SetActive(false);
        }

        /// <param name="tooltipStatusEffect">When set, hover uses <see cref="HoverTooltipManager"/> with this status asset.</param>
        public void Bind(Sprite icon, string value, StatusEffectSO tooltipStatusEffect, string tooltipTitle, string tooltipDescription, bool enableTooltip, Sprite background = null)
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

            EnsureHoverTooltipTarget(enableTooltip);

            if (!enableTooltip)
            {
                _hoverTooltipTarget.SetScriptableSource(null);
                _hoverTooltipTarget.SetContent(string.Empty, string.Empty);
                _hoverTooltipTarget.enabled = false;
                _pulseBaseLocalScale = transform.localScale;
                return;
            }

            _hoverTooltipTarget.enabled = true;
            if (tooltipStatusEffect != null)
            {
                _hoverTooltipTarget.SetScriptableSource(tooltipStatusEffect);
                _hoverTooltipTarget.SetContent(string.Empty, string.Empty);
            }
            else
            {
                _hoverTooltipTarget.SetScriptableSource(null);
                var title = string.IsNullOrWhiteSpace(tooltipTitle) ? "Action" : tooltipTitle;
                var description = tooltipDescription ?? string.Empty;
                _hoverTooltipTarget.SetContent(title, description);
            }

            _pulseBaseLocalScale = transform.localScale;
        }

        void EnsureHoverTooltipTarget(bool wantTooltip)
        {
            var hoverGo = ResolveHoverGameObject();
            if (hoverGo == null)
                return;

            foreach (var orphan in GetComponentsInChildren<HoverTooltipTargetUI>(true))
            {
                if (orphan != null && orphan.gameObject != hoverGo)
                    Destroy(orphan);
            }

            if (_hoverTooltipTarget != null && _hoverTooltipTarget.gameObject != hoverGo)
            {
                Destroy(_hoverTooltipTarget);
                _hoverTooltipTarget = null;
            }

            if (_hoverTooltipTarget == null || _hoverTooltipTarget.gameObject != hoverGo)
                _hoverTooltipTarget = hoverGo.GetComponent<HoverTooltipTargetUI>() ?? hoverGo.AddComponent<HoverTooltipTargetUI>();

            if (wantTooltip)
            {
                EnsureGraphicReceivesPointerHover(hoverGo);
                SoleRaycastTargetOnSegment(hoverGo);
            }

            _hoverTooltipTarget.SetTooltipScreenOffset(tooltipScreenOffset);
        }

        /// <summary>
        /// Later siblings (full-cell button, TMP, BG) sit above the action icon and steal raycasts. Only the hover target may receive hits so <see cref="HoverTooltipTargetUI"/> runs.
        /// </summary>
        void SoleRaycastTargetOnSegment(GameObject hoverGo)
        {
            foreach (var g in GetComponentsInChildren<Graphic>(true))
            {
                if (g == null)
                    continue;
                g.raycastTarget = g.gameObject == hoverGo;
            }
        }

        GameObject ResolveHoverGameObject()
        {
            if (iconImage != null)
                return iconImage.gameObject;
            return gameObject;
        }

        static void EnsureGraphicReceivesPointerHover(GameObject hoverGo)
        {
            if (hoverGo == null)
                return;
            var graphic = hoverGo.GetComponent<Graphic>();
            if (graphic == null)
                return;
            graphic.raycastTarget = true;
            if (graphic is Image image && image.sprite == null)
            {
                if (_hoverRaycastSprite == null)
                {
                    var tex = Texture2D.whiteTexture;
                    _hoverRaycastSprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);
                }

                image.sprite = _hoverRaycastSprite;
                var c = image.color;
                if (c.a <= 0f)
                    image.color = new Color(c.r, c.g, c.b, 0.02f);
            }
        }
    }
}
