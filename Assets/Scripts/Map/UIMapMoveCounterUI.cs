using TMPro;
using UnityEngine;

/// <summary>Displays "Moves: current/limit" and tints red when over limit.</summary>
public class UIMapMoveCounterUI : MonoBehaviour
{
    [SerializeField] private TMP_Text movesText;
    [SerializeField] private Color normalColor = Color.white;
    [SerializeField] private Color overLimitColor = new Color(0.95f, 0.2f, 0.2f, 1f);

    private MapMovementManager _manager;

    public void Bind(MapMovementManager manager)
    {
        _manager = manager;
        Refresh();
    }

    public void Refresh()
    {
        if (movesText == null)
            return;

        if (_manager == null)
        {
            movesText.text = "Moves: —";
            movesText.color = normalColor;
            return;
        }

        var taken = _manager.MovesTaken;
        var limit = _manager.MoveLimit;
        movesText.text = $"Moves: {taken}/{limit}";
        movesText.color = taken > limit ? overLimitColor : normalColor;
    }
}
