using UnityEngine;

public static class StartInRandomFrameUtility
{
    const float DefaultFrameRate = 60f;
    const float ReEntryNormalizedTimeThreshold = 0.001f;

    public static bool IsRandomStartEnabledOnController(Animator animator)
    {
        if (animator == null)
            return false;

        var behaviours = animator.GetBehaviours<StartInRandomFrameBehaviour>();
        for (var i = 0; i < behaviours.Length; i++)
        {
            if (behaviours[i] != null && behaviours[i].StartInRandomFrame)
                return true;
        }

        return false;
    }

    /// <summary>
    /// Seeks the current state to a per-instance frame offset. Call after <see cref="Animator.Rebind"/>
    /// when combat setup would otherwise reset every copy to frame 0.
    /// </summary>
    public static bool TryApply(Animator animator, int layerIndex = 0)
    {
        if (animator == null || !IsRandomStartEnabledOnController(animator))
            return false;

        var stateInfo = animator.GetCurrentAnimatorStateInfo(layerIndex);
        if (stateInfo.length <= 0f)
            return false;

        if (stateInfo.normalizedTime > ReEntryNormalizedTimeThreshold)
            return false;

        if (!TryGetNormalizedStartTime(animator, stateInfo, layerIndex, out var normalizedTime))
            return false;

        if (animator.IsInTransition(layerIndex))
        {
            var transition = animator.GetAnimatorTransitionInfo(layerIndex);
            animator.CrossFade(
                stateInfo.fullPathHash,
                transition.duration,
                layerIndex,
                normalizedTime,
                transition.normalizedTime);
        }
        else
        {
            animator.Play(stateInfo.fullPathHash, layerIndex, normalizedTime);
        }

        animator.Update(0f);
        return true;
    }

    static bool TryGetNormalizedStartTime(
        Animator animator,
        AnimatorStateInfo stateInfo,
        int layerIndex,
        out float normalizedTime)
    {
        normalizedTime = 0f;

        var stateLength = stateInfo.length;
        if (stateLength <= 0f)
            return false;

        var clip = ResolveClip(animator, layerIndex);
        var frameRate = clip != null && clip.frameRate > 0f ? clip.frameRate : DefaultFrameRate;
        var clipLength = clip != null && clip.length > 0f
            ? clip.length
            : stateLength * Mathf.Max(stateInfo.speedMultiplier, 0.0001f);

        var frameCount = Mathf.Max(1, Mathf.RoundToInt(clipLength * frameRate));
        var frameIndex = PickFrameIndexForInstance(animator, frameCount);
        var clipTime = frameIndex / frameRate;
        normalizedTime = clipTime / stateLength;
        return true;
    }

    static int PickFrameIndexForInstance(Animator animator, int frameCount)
    {
        if (frameCount <= 1)
            return 0;

        var hash = animator.GetInstanceID();
        hash ^= hash >> 16;
        hash *= 0x45d9f3b;
        hash ^= hash >> 16;
        return Mathf.Abs(hash) % frameCount;
    }

    static AnimationClip ResolveClip(Animator animator, int layerIndex)
    {
        if (animator.IsInTransition(layerIndex))
        {
            var transitionClip = FirstClip(animator.GetNextAnimatorClipInfo(layerIndex));
            if (transitionClip != null)
                return transitionClip;
        }

        var currentClip = FirstClip(animator.GetCurrentAnimatorClipInfo(layerIndex));
        if (currentClip != null)
            return currentClip;

        return FirstClip(animator.GetNextAnimatorClipInfo(layerIndex));
    }

    static AnimationClip FirstClip(AnimatorClipInfo[] clipInfos)
    {
        if (clipInfos == null || clipInfos.Length == 0)
            return null;

        for (var i = 0; i < clipInfos.Length; i++)
        {
            if (clipInfos[i].clip != null)
                return clipInfos[i].clip;
        }

        return null;
    }
}
