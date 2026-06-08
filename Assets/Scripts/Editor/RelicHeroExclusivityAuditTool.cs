#if UNITY_EDITOR
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Reports which relics are Generic vs hero-exclusive for authoring review.
/// </summary>
public static class RelicHeroExclusivityAuditTool
{
    [MenuItem("DiceGame/Relics/Audit Hero Exclusivity (Report)")]
    public static void AuditHeroExclusivity()
    {
        var guids = AssetDatabase.FindAssets("t:RelicSO");
        var generic = new StringBuilder();
        var exclusive = new StringBuilder();
        var issues = new StringBuilder();
        var genericCount = 0;
        var exclusiveCount = 0;
        var issueCount = 0;

        for (var i = 0; i < guids.Length; i++)
        {
            var path = AssetDatabase.GUIDToAssetPath(guids[i]);
            var relic = AssetDatabase.LoadAssetAtPath<RelicSO>(path);
            if (relic == null)
                continue;

            if (relic.IsGenericRelic)
            {
                genericCount++;
                generic.AppendLine($"  {relic.name} ({path})");
                continue;
            }

            exclusiveCount++;
            var hero = relic.ExclusiveHero;
            if (hero == null)
            {
                issueCount++;
                issues.AppendLine($"  {relic.name} ({path}) — exclusiveHero reference is missing or broken");
                continue;
            }

            exclusive.AppendLine($"  {relic.name} → {hero.DisplayName} ({AssetDatabase.GetAssetPath(hero)})");
        }

        var report = new StringBuilder();
        report.AppendLine($"Relic hero exclusivity audit — Generic: {genericCount}, Exclusive: {exclusiveCount}, Issues: {issueCount}");
        report.AppendLine();
        report.AppendLine("=== Generic (all heroes) ===");
        report.Append(generic.Length > 0 ? generic.ToString() : "  (none)\n");
        report.AppendLine();
        report.AppendLine("=== Exclusive (single hero) ===");
        report.Append(exclusive.Length > 0 ? exclusive.ToString() : "  (none)\n");
        if (issueCount > 0)
        {
            report.AppendLine();
            report.AppendLine("=== Issues ===");
            report.Append(issues.ToString());
        }

        Debug.Log(report.ToString());
        EditorUtility.DisplayDialog(
            "Relic Hero Exclusivity Audit",
            $"Generic: {genericCount}\nExclusive: {exclusiveCount}\nIssues: {issueCount}\n\nFull report logged to Console.",
            "OK");
    }
}
#endif
