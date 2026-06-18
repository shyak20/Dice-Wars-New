using System;
using System.Collections.Generic;
using UnityEngine;

namespace Enemies
{
    /// <summary>
    /// Repoints <see cref="SpriteRenderer.sortingOrder"/> during the enemy turn so the acting enemy draws above others.
    /// Defaults are captured at startup and restored when the enemy turn indicator flow finishes.
    /// </summary>
    public class EnemyTurnSpriteSortingController : MonoBehaviour
    {
        [SerializeField] private List<SpriteRenderer> spriteRenderers = new List<SpriteRenderer>();
        [SerializeField] private int notTurnOrderLayer;
        [SerializeField] private int activeTurnOrderLayer;

        private readonly List<int> _defaultOrderLayers = new List<int>();

        private void Awake()
        {
            CaptureDefaultOrderLayers();
        }

        public void SetInactiveTurnSorting()
        {
            ApplySortingOrder(notTurnOrderLayer);
        }

        public void SetActiveTurnSorting()
        {
            ApplySortingOrder(activeTurnOrderLayer);
        }

        public void RestoreDefaultSorting()
        {
            if (_defaultOrderLayers.Count != spriteRenderers.Count)
                CaptureDefaultOrderLayers();

            for (var i = 0; i < spriteRenderers.Count; i++)
            {
                var spriteRenderer = spriteRenderers[i];
                if (spriteRenderer == null)
                    continue;

                spriteRenderer.sortingOrder = _defaultOrderLayers[i];
            }
        }

        private void CaptureDefaultOrderLayers()
        {
            _defaultOrderLayers.Clear();
            for (var i = 0; i < spriteRenderers.Count; i++)
            {
                var spriteRenderer = spriteRenderers[i];
                if (spriteRenderer == null)
                    throw new InvalidOperationException(
                        $"{nameof(EnemyTurnSpriteSortingController)} on '{name}': sprite renderers[{i}] is not assigned.");

                _defaultOrderLayers.Add(spriteRenderer.sortingOrder);
            }
        }

        private void ApplySortingOrder(int orderLayer)
        {
            if (spriteRenderers.Count == 0)
                return;

            if (_defaultOrderLayers.Count != spriteRenderers.Count)
                CaptureDefaultOrderLayers();

            for (var i = 0; i < spriteRenderers.Count; i++)
            {
                var spriteRenderer = spriteRenderers[i];
                if (spriteRenderer == null)
                    throw new InvalidOperationException(
                        $"{nameof(EnemyTurnSpriteSortingController)} on '{name}': sprite renderers[{i}] is not assigned.");

                spriteRenderer.sortingOrder = orderLayer;
            }
        }
    }
}
