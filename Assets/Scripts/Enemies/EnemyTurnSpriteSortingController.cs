using System;
using System.Collections.Generic;
using UnityEngine;

namespace Enemies
{
    /// <summary>
    /// Repoints <see cref="SpriteRenderer.sortingOrder"/> during the enemy turn so the acting enemy draws above others.
    /// Optionally swaps to an active-turn material on the acting enemy. Defaults are captured at startup and restored
    /// when the enemy is not acting or when the enemy turn indicator flow finishes.
    /// </summary>
    public class EnemyTurnSpriteSortingController : MonoBehaviour
    {
        [SerializeField] private List<SpriteRenderer> spriteRenderers = new List<SpriteRenderer>();
        [SerializeField] private int notTurnOrderLayer;
        [SerializeField] private int activeTurnOrderLayer;
        [Tooltip("Optional. Applied to sprite renderers while this enemy is the acting enemy during the enemy turn.")]
        [SerializeField] private Material activeTurnMaterial;

        private readonly List<int> _defaultOrderLayers = new List<int>();
        private readonly List<Material> _defaultMaterials = new List<Material>();
        private readonly List<Material> _activeTurnMaterialRuntimes = new List<Material>();
        private EnemyCombatPresentationController _combatPresentation;

        private void Awake()
        {
            CaptureDefaults();
        }

        private void OnDestroy()
        {
            for (var i = 0; i < _activeTurnMaterialRuntimes.Count; i++)
            {
                var runtime = _activeTurnMaterialRuntimes[i];
                if (runtime == null)
                    continue;

                if (Application.isPlaying)
                    Destroy(runtime);
                else
                    DestroyImmediate(runtime);
            }
        }

        public void RefreshDefaultMaterials(EnemyCombatPresentationController combatPresentation)
        {
            _combatPresentation = combatPresentation;
            combatPresentation?.RestoreCombatSpriteMaterial();
            CaptureDefaults();

            if (combatPresentation?.EnemySprite == null)
                return;

            for (var i = 0; i < spriteRenderers.Count; i++)
            {
                if (spriteRenderers[i] == combatPresentation.EnemySprite)
                    _defaultMaterials[i] = combatPresentation.EnemySprite.sharedMaterial;
            }
        }

        public void SetInactiveTurnSorting()
        {
            ApplyTurnVisuals(notTurnOrderLayer, useActiveTurnMaterial: false);
        }

        public void SetActiveTurnSorting()
        {
            ApplyTurnVisuals(activeTurnOrderLayer, useActiveTurnMaterial: true);
        }

        public void RestoreDefaultSorting()
        {
            if (_defaultOrderLayers.Count != spriteRenderers.Count || _defaultMaterials.Count != spriteRenderers.Count)
                CaptureDefaults();

            for (var i = 0; i < spriteRenderers.Count; i++)
            {
                var spriteRenderer = spriteRenderers[i];
                if (spriteRenderer == null)
                    continue;

                spriteRenderer.sortingOrder = _defaultOrderLayers[i];
                ApplyDefaultMaterial(i, spriteRenderer);
            }
        }

        private void CaptureDefaults()
        {
            _defaultOrderLayers.Clear();
            _defaultMaterials.Clear();

            for (var i = 0; i < spriteRenderers.Count; i++)
            {
                var spriteRenderer = spriteRenderers[i];
                if (spriteRenderer == null)
                    throw new InvalidOperationException(
                        $"{nameof(EnemyTurnSpriteSortingController)} on '{name}': sprite renderers[{i}] is not assigned.");

                _defaultOrderLayers.Add(spriteRenderer.sortingOrder);
                _defaultMaterials.Add(spriteRenderer.sharedMaterial);
            }
        }

        private void ApplyTurnVisuals(int orderLayer, bool useActiveTurnMaterial)
        {
            if (spriteRenderers.Count == 0)
                return;

            if (_defaultOrderLayers.Count != spriteRenderers.Count || _defaultMaterials.Count != spriteRenderers.Count)
                CaptureDefaults();

            for (var i = 0; i < spriteRenderers.Count; i++)
            {
                var spriteRenderer = spriteRenderers[i];
                if (spriteRenderer == null)
                    throw new InvalidOperationException(
                        $"{nameof(EnemyTurnSpriteSortingController)} on '{name}': sprite renderers[{i}] is not assigned.");

                spriteRenderer.sortingOrder = orderLayer;

                if (useActiveTurnMaterial)
                {
                    if (activeTurnMaterial != null)
                        spriteRenderer.sharedMaterial = GetOrCreateActiveTurnMaterialRuntime(i);
                }
                else
                    ApplyDefaultMaterial(i, spriteRenderer);
            }
        }

        private void ApplyDefaultMaterial(int index, SpriteRenderer spriteRenderer)
        {
            if (_combatPresentation != null && spriteRenderer == _combatPresentation.EnemySprite)
            {
                _combatPresentation.RestoreCombatSpriteMaterial();
                if (index >= 0 && index < _defaultMaterials.Count)
                    _defaultMaterials[index] = spriteRenderer.sharedMaterial;
                return;
            }

            if (index < 0 || index >= _defaultMaterials.Count)
                return;

            spriteRenderer.sharedMaterial = _defaultMaterials[index];
        }

        private Material GetOrCreateActiveTurnMaterialRuntime(int rendererIndex)
        {
            while (_activeTurnMaterialRuntimes.Count <= rendererIndex)
                _activeTurnMaterialRuntimes.Add(null);

            var runtime = _activeTurnMaterialRuntimes[rendererIndex];
            if (runtime == null)
            {
                runtime = new Material(activeTurnMaterial);
                _activeTurnMaterialRuntimes[rendererIndex] = runtime;
            }

            return runtime;
        }
    }
}
