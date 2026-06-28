using UnityEngine;

/// <summary>
/// Verbose logging for <see cref="IncreaseOtherElementsAction"/> + reroll batch ordering.
/// Set <see cref="Enabled"/> false to silence once the stall is diagnosed.
/// </summary>
public static class IncreaseOtherRerollFlowDebug
{
    public const bool Enabled = true;

    public static void Log(string message)
    {
        if (!Enabled)
            return;

        Debug.Log($"[IncreaseOther+Reroll] {message}");
    }

    public static void LogWarning(string message)
    {
        if (!Enabled)
            return;

        Debug.LogWarning($"[IncreaseOther+Reroll] {message}");
    }

    public static string DescribeFace(FaceResult face)
    {
        if (face == null)
            return "null";

        var faceName = face.Face != null ? face.Face.name : "?";
        return $"{faceName} batchIdx={face.BatchGatherIndex} batchId={face.BatchId} " +
               $"awaitPostSubmitReroll={face.AwaitingPostSubmitTriggeringReroll} " +
               $"awaitRerollOther={face.AwaitingPostBatchOtherDiceReroll}";
    }
}
