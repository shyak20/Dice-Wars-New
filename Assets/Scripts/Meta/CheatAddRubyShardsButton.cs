using UnityEngine;
using UnityEngine.UI;

/// <summary>Debug UI: adds meta ruby shards on each button press. Add to a GameObject with a <see cref="Button"/>.</summary>
public sealed class CheatAddRubyShardsButton : MonoBehaviour
{
    [SerializeField] private Button button;
    [SerializeField, Min(1)] private int rubyShardsPerPress = 5;

    void Awake()
    {
        if (button == null)
            button = GetComponent<Button>();

        if (button == null)
        {
            Debug.LogError("CheatAddRubyShardsButton: assign a Button or put this on a Button object.", this);
            return;
        }

        button.onClick.AddListener(OnPressed);
    }

    void OnDestroy()
    {
        if (button != null)
            button.onClick.RemoveListener(OnPressed);
    }

    void OnPressed()
    {
        var meta = MetaProgressionManager.TryGetRuntime();
        if (meta == null)
        {
            Debug.LogError("CheatAddRubyShardsButton: MetaProgressionManager missing.", this);
            return;
        }

        meta.GrantRubyShards(rubyShardsPerPress);
    }
}
