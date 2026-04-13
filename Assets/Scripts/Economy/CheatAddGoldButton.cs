using UnityEngine;
using UnityEngine.UI;

/// <summary>Debug UI: adds run gold on each button press. Add to a GameObject with a <see cref="Button"/>.</summary>
public sealed class CheatAddGoldButton : MonoBehaviour
{
    [SerializeField] private Button button;
    [SerializeField] [Min(1)] private int goldPerPress = 100;

    private void Awake()
    {
        if (button == null)
            button = GetComponent<Button>();

        if (button == null)
        {
            Debug.LogError("CheatAddGoldButton: assign a Button or put this on a Button object.", this);
            return;
        }

        button.onClick.AddListener(OnPressed);
    }

    private void OnDestroy()
    {
        if (button != null)
            button.onClick.RemoveListener(OnPressed);
    }

    private void OnPressed()
    {
        var eco = RunEconomyManager.TryGetRuntime();
        if (eco == null)
        {
            Debug.LogError("CheatAddGoldButton: RunEconomyManager missing.", this);
            return;
        }

        eco.GrantGold(goldPerPress, null);
    }
}
