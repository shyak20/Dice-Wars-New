using UnityEngine;

/// <summary>
/// When enabled, seeks to a per-instance frame offset each time this Animator state is entered.
/// Added and toggled from the Animator state Inspector via editor tooling.
/// </summary>
public sealed class StartInRandomFrameBehaviour : StateMachineBehaviour
{
    const float ReEntryNormalizedTimeThreshold = 0.001f;

    [SerializeField] bool startInRandomFrame = true;

    public bool StartInRandomFrame
    {
        get => startInRandomFrame;
        set => startInRandomFrame = value;
    }

    public override void OnStateEnter(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        if (!startInRandomFrame || animator == null)
            return;

        if (stateInfo.normalizedTime > ReEntryNormalizedTimeThreshold)
            return;

        StartInRandomFrameUtility.TryApply(animator, layerIndex);
    }

    public override void OnStateMove(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        if (!startInRandomFrame || animator == null)
            return;

        if (stateInfo.normalizedTime > ReEntryNormalizedTimeThreshold)
            return;

        StartInRandomFrameUtility.TryApply(animator, layerIndex);
    }
}
