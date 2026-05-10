using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Enemies
{
    /// <summary>
    /// Executes the current enemy intent as separate steps (each physical hit, armor, then each game action)
    /// with configurable delay between steps. When <see cref="actionUI"/> is assigned, the matching intent
    /// segment scales by <see cref="performStepPulseScale"/> at the moment the step resolves, then that segment is hidden.
    /// </summary>
    public class EnemyTurnIntentSequencePlayer : MonoBehaviour
    {
        [SerializeField] private EnemyActionUIController actionUI;

        [Tooltip("Pause after each intent step completes (each hit, armor gain, individual game action).")]
        [SerializeField, Min(0f)] private float delayBetweenIntentSteps = 0.35f;

        [Header("Intent segment pulse (step resolution)")]
        [Tooltip("Scale multiplier on the segment root at the peak of the pulse.")]
        [SerializeField, Min(0.01f)] private float performStepPulseScale = 1.2f;

        [SerializeField, Min(0f)] private float pulseRiseSeconds = 0.06f;
        [SerializeField, Min(0f)] private float pulsePeakHoldSeconds = 0.05f;

        private enum StepKind
        {
            PhysicalHit,
            Armor,
            GameAction
        }

        private struct Step
        {
            public StepKind Kind;
            public int SegmentIndex;
            public int GameActionListIndex;
        }

        public IEnumerator CoExecuteIntent(EnemyController enemy, EnemyActionSO action, CombatManager combat)
        {
            if (enemy == null || action == null || combat == null)
                yield break;

            var steps = BuildSteps(action);
            for (var s = 0; s < steps.Count; s++)
            {
                var step = steps[s];
                yield return RunStepWithOptionalPulse(step, action, combat);

                if (combat.EvaluateEnemyTurnCombatEnded())
                    yield break;

                if (s < steps.Count - 1 && delayBetweenIntentSteps > 0f)
                    yield return new WaitForSeconds(delayBetweenIntentSteps);
            }
        }

        private IEnumerator RunStepWithOptionalPulse(Step step, EnemyActionSO action, CombatManager combat)
        {
            void ApplyStep()
            {
                switch (step.Kind)
                {
                    case StepKind.PhysicalHit:
                        combat.ApplySingleEnemyPhysicalHitFromIntent(action);
                        break;
                    case StepKind.Armor:
                        combat.ApplyEnemyArmorFromIntent(action);
                        break;
                    case StepKind.GameAction:
                        combat.ExecuteEnemyIntentGameActionAtIndex(action, step.GameActionListIndex);
                        break;
                }
            }

            if (actionUI != null && actionUI.TryGetSegment(step.SegmentIndex, out var segment))
            {
                yield return StartCoroutine(segment.CoPerformScalePulseWithPeakCallback(
                    performStepPulseScale,
                    pulseRiseSeconds,
                    pulsePeakHoldSeconds,
                    ApplyStep));
            }
            else
                ApplyStep();
        }

        private static List<Step> BuildSteps(EnemyActionSO action)
        {
            var steps = new List<Step>();
            var seg = 0;

            if (action.damage > 0)
            {
                var hits = Mathf.Max(1, action.numberOfAttacks);
                for (var h = 0; h < hits; h++)
                {
                    steps.Add(new Step
                    {
                        Kind = StepKind.PhysicalHit,
                        SegmentIndex = 0,
                        GameActionListIndex = 0
                    });
                }

                seg = 1;
            }

            if (action.armor > 0)
            {
                steps.Add(new Step { Kind = StepKind.Armor, SegmentIndex = seg, GameActionListIndex = 0 });
                seg++;
            }

            if (action.actions != null)
            {
                for (var i = 0; i < action.actions.Count; i++)
                {
                    var ga = action.actions[i];
                    if (ga == null || ga is FaceResolveModifierBase)
                        continue;
                    steps.Add(new Step
                    {
                        Kind = StepKind.GameAction,
                        SegmentIndex = seg,
                        GameActionListIndex = i
                    });
                    seg++;
                }
            }

            return steps;
        }
    }
}
