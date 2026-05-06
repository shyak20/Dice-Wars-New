using System.Collections.Generic;
using UniRx;
using UnityEngine;

namespace Enemies
{
    /// <summary>One <see cref="EnemyIntentSegmentView"/> per damage, armor, and each <see cref="IGameAction"/> on the current <see cref="EnemyActionSO"/>.</summary>
    public class EnemyActionUIController : MonoBehaviour
    {
        [SerializeField] private Transform segmentContainer;
        [SerializeField] private EnemyIntentSegmentView segmentPrefab;

        [SerializeField] private EnemyController _enemyController;
        [Header("Physical strike intent")]
        [Tooltip("TMP rich-text color for the per-hit damage number when it differs from the action asset (e.g. Strength).")]
        [SerializeField] private Color intentBuffDamageColor = new Color(1f, 0.55f, 0.35f, 1f);

        private readonly List<EnemyIntentSegments.Row> _rowsScratch = new List<EnemyIntentSegments.Row>();
        private CombatManager _combatManager;

        private void Start()
        {
            if (segmentContainer == null || segmentPrefab == null)
                Debug.LogError("EnemyActionUIController: Assign segment Container (layout parent) and Segment Prefab.");
            if (_enemyController == null)
                Debug.LogError("EnemyActionUIController: Assign enemy controller.");

            _combatManager = FindObjectOfType<CombatManager>();
            if (_enemyController.StatusEffects != null)
                _enemyController.StatusEffects.OnEffectsChanged += OnEnemyStatusEffectsChanged;

            _enemyController.CurrentIntent
                .Subscribe(OnCurrentIntentChanged)
                .AddTo(this);
        }

        private void OnDestroy()
        {
            if (_enemyController != null && _enemyController.StatusEffects != null)
                _enemyController.StatusEffects.OnEffectsChanged -= OnEnemyStatusEffectsChanged;
        }

        private void OnEnemyStatusEffectsChanged()
        {
            if (_enemyController != null)
                OnCurrentIntentChanged(_enemyController.CurrentIntent.Value);
        }

        private void OnCurrentIntentChanged(EnemyActionSO intent)
        {
            ClearSegments();

            if (intent == null || segmentContainer == null || segmentPrefab == null)
                return;

            EnemyIntentSegments.BuildRows(intent, _rowsScratch, _enemyController, _combatManager, intentBuffDamageColor);
            foreach (var row in _rowsScratch)
            {
                var seg = Instantiate(segmentPrefab, segmentContainer);
                seg.Bind(row.Icon, row.ValueText, row.StatusTitle, row.StatusDescription, row.EnableTooltip);
            }
        }

        private void ClearSegments()
        {
            if (segmentContainer == null) return;
            for (var i = segmentContainer.childCount - 1; i >= 0; i--)
                Destroy(segmentContainer.GetChild(i).gameObject);
        }
    }
}
