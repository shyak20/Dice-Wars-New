using System;
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

        [Tooltip("Pause after one enemy finishes their full intent, before the next enemy's turn begins.")]
        [SerializeField, Min(0f)] private float delayBetweenEnemies = 0.5f;

        [Tooltip("Multi-enemy only: after an enemy's turn begins, show their intent rows for this long before the first effect resolves.")]
        [SerializeField, Min(0f)] private float delayBeforeMultiEnemyIntentActionSeconds = 1f;

        public float DelayBetweenEnemies => delayBetweenEnemies;

        [Header("Turn indicator animator")]
        [Tooltip("Optional. When assigned, the appear clip plays until the hold pose, stays frozen while enemy actions resolve, then fires Close and waits before the screen is hidden.")]
        [SerializeField] private Animator turnIndicatorAnimator;
        [SerializeField] private string turnIndicatorAppearStateName = "Enemy Turn Appear";
        [Tooltip("Seconds into the appear clip where the intro finishes and the hold pose begins.")]
        [SerializeField, Min(0f)] private float turnIndicatorAppearEndSeconds = 0.73333335f;
        [Tooltip("Animator trigger fired when all enemy actions finish (must exist on Enemy's Turn CTRL).")]
        [SerializeField] private string turnIndicatorCloseTriggerName = "Close";
        [Tooltip("Seconds to wait after firing Close before the indicator root is disabled.")]
        [SerializeField, Min(0f)] private float turnIndicatorCloseWaitSeconds = 0.45f;
        [Tooltip("Pause after the intent screen is shown, before the first enemy action resolves.")]
        [SerializeField, Min(0f)] private float delayBeforeFirstActionSeconds = 0f;
        [Tooltip("Pause after the last enemy action finishes, before the Close animator trigger fires.")]
        [SerializeField, Min(0f)] private float delayBeforeCloseTriggerSeconds = 0f;

        public bool UsesTurnIndicatorAnimator =>
            turnIndicatorAnimator != null && !string.IsNullOrWhiteSpace(turnIndicatorAppearStateName);

        public IEnumerator CoPresentTurnIndicatorIntro()
        {
            if (!UsesTurnIndicatorAnimator)
                yield break;

            turnIndicatorAnimator.enabled = true;
            turnIndicatorAnimator.speed = 1f;
            turnIndicatorAnimator.Rebind();
            turnIndicatorAnimator.Update(0f);
            turnIndicatorAnimator.Play(turnIndicatorAppearStateName, 0, 0f);

            yield return CoWaitUntilAnimatorClipTime(turnIndicatorAppearEndSeconds);

            var holdNormalized = SecondsToClipNormalizedTime(turnIndicatorAppearEndSeconds);
            turnIndicatorAnimator.Play(turnIndicatorAppearStateName, 0, holdNormalized);
            turnIndicatorAnimator.Update(0f);
            turnIndicatorAnimator.speed = 0f;
        }

        public IEnumerator CoPresentTurnIndicatorOutro()
        {
            if (!UsesTurnIndicatorAnimator)
                yield break;

            turnIndicatorAnimator.speed = 1f;
            turnIndicatorAnimator.SetTrigger(turnIndicatorCloseTriggerName);

            if (turnIndicatorCloseWaitSeconds > 0f)
                yield return new WaitForSeconds(turnIndicatorCloseWaitSeconds);
        }

        public IEnumerator CoWaitBeforeFirstAction()
        {
            if (delayBeforeFirstActionSeconds > 0f)
                yield return new WaitForSeconds(delayBeforeFirstActionSeconds);
        }

        public void PresentActingEnemyIntent(EnemyController enemy, EnemyActionSO action)
        {
            if (actionUI == null || enemy == null || action == null)
                return;

            actionUI.PresentIntent(enemy, action);
        }

        public IEnumerator CoWaitBeforeMultiEnemyIntentAction()
        {
            if (delayBeforeMultiEnemyIntentActionSeconds > 0f)
                yield return new WaitForSeconds(delayBeforeMultiEnemyIntentActionSeconds);
        }

        public IEnumerator CoWaitBeforeCloseTrigger()
        {
            if (delayBeforeCloseTriggerSeconds > 0f)
                yield return new WaitForSeconds(delayBeforeCloseTriggerSeconds);
        }

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
                yield return RunStepWithOptionalPulse(step, action, combat, enemy);

                if (combat.EvaluateEnemyTurnCombatEnded())
                    yield break;

                if (s < steps.Count - 1 && delayBetweenIntentSteps > 0f)
                    yield return new WaitForSeconds(delayBetweenIntentSteps);
            }
        }

        private IEnumerator RunStepWithOptionalPulse(Step step, EnemyActionSO action, CombatManager combat, EnemyController enemy)
        {
            if (step.Kind == StepKind.GameAction &&
                combat.TryGetPlayerDebuffFlyoutForIntent(action, step.GameActionListIndex, out _))
            {
                yield return RunPlayerDebuffGameActionStep(step, action, combat, enemy);
                yield break;
            }

            void ApplyStep()
            {
                switch (step.Kind)
                {
                    case StepKind.PhysicalHit:
                        combat.ApplySingleEnemyPhysicalHitFromIntent(action, enemy);
                        break;
                    case StepKind.Armor:
                        combat.ApplyEnemyArmorFromIntent(action, enemy);
                        break;
                    case StepKind.GameAction:
                        combat.ExecuteEnemyIntentGameActionAtIndex(action, step.GameActionListIndex, enemy);
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

        private IEnumerator RunPlayerDebuffGameActionStep(Step step, EnemyActionSO action, CombatManager combat, EnemyController enemy)
        {
            RectTransform flySource = null;
            if (actionUI != null && actionUI.TryGetSegment(step.SegmentIndex, out var segment))
            {
                flySource = segment.IconRect;
                yield return StartCoroutine(segment.CoPerformScalePulseWithPeakCallback(
                    performStepPulseScale,
                    pulseRiseSeconds,
                    pulsePeakHoldSeconds,
                    null));
            }

            yield return combat.CoExecuteEnemyIntentGameActionAtIndex(action, step.GameActionListIndex, enemy, flySource);
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

                    var hits = ga is LeechPhysicalDamageAction leech ? Mathf.Max(1, leech.NumberOfAttacks) : 1;
                    for (var h = 0; h < hits; h++)
                    {
                        steps.Add(new Step
                        {
                            Kind = StepKind.GameAction,
                            SegmentIndex = seg,
                            GameActionListIndex = i
                        });
                    }

                    seg++;
                }
            }

            return steps;
        }

        private IEnumerator CoWaitUntilAnimatorClipTime(float targetSeconds)
        {
            while (true)
            {
                if (TryGetAnimatorClipTime(out var clipTime) && clipTime >= targetSeconds)
                    break;

                yield return null;
            }
        }

        private bool TryGetAnimatorStateInfo(out AnimatorStateInfo info)
        {
            info = turnIndicatorAnimator.GetCurrentAnimatorStateInfo(0);
            return info.IsName(turnIndicatorAppearStateName);
        }

        private bool TryGetAnimatorClipTime(out float clipTime)
        {
            clipTime = 0f;
            if (!TryGetAnimatorStateInfo(out var info) || info.length <= 0f)
                return false;

            clipTime = info.normalizedTime * info.length;
            return true;
        }

        private float SecondsToClipNormalizedTime(float seconds)
        {
            if (!TryGetAnimatorStateInfo(out var info) || info.length <= 0f)
                throw new InvalidOperationException(
                    $"{nameof(EnemyTurnIntentSequencePlayer)} on '{name}': cannot read clip length from animator state '{turnIndicatorAppearStateName}'.");

            return Mathf.Clamp01(seconds / info.length);
        }
    }
}
