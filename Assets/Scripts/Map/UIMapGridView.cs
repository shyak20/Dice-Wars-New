using UnityEngine;
using UnityEngine.UI;

/// <summary>Spawns a tile UI per cell and tracks the player icon.</summary>
public class UIMapGridView : MonoBehaviour
{
    [SerializeField] private RectTransform tilesParent;
    [SerializeField] private GridLayoutGroup gridLayout;
    [SerializeField] private UIMapTileView tilePrefab;
    [SerializeField] private RectTransform playerIcon;
    [Tooltip("Empty child on each tile where the player icon parents while standing on that cell.")]
    [SerializeField] private string playerAnchorChildName = "PlayerAnchor";

    private UIMapTileView[,] _tiles;
    private MapMovementManager _manager;

    private void Awake()
    {
        if (tilesParent == null)
            Debug.LogError("UIMapGridView: assign tilesParent.", this);
        if (tilePrefab == null)
            Debug.LogError("UIMapGridView: assign tilePrefab.", this);
        if (gridLayout == null && tilesParent != null)
            gridLayout = tilesParent.GetComponent<GridLayoutGroup>();
        if (gridLayout == null)
            Debug.LogError("UIMapGridView: assign gridLayout (or add GridLayoutGroup to tilesParent).", this);
    }

    public void Present(MapGrid grid, MapMovementManager manager)
    {
        if (grid == null || manager == null || tilesParent == null || tilePrefab == null || gridLayout == null)
            return;

        // Icon is under a tile child of tilesParent; ClearChildren would destroy it.
        // Do not use `transform` here — it is often the same as tilesParent on the grid object.
        DetachPlayerIconBeforeClearingTiles();

        _manager = manager;
        ClearChildren();

        gridLayout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        gridLayout.constraintCount = grid.Width;

        _tiles = new UIMapTileView[grid.Width, grid.Height];

        for (var y = 0; y < grid.Height; y++)
        {
            for (var x = 0; x < grid.Width; x++)
            {
                var cell = new Vector2Int(x, y);
                var tile = grid.Get(x, y);
                var view = Instantiate(tilePrefab, tilesParent);
                _tiles[x, y] = view;
                var isStart = manager.IsStart(cell);
                var isBoss = manager.IsBoss(cell);
                view.Setup(cell, tile, isStart, isBoss, manager);
            }
        }

        RefreshPlayerIcon();
        RefreshArrowHighlights();
    }

    public void RefreshAllTileExits()
    {
        var grid = _manager != null ? _manager.Grid : null;
        if (grid == null || _tiles == null)
            return;

        for (var y = 0; y < grid.Height; y++)
        {
            for (var x = 0; x < grid.Width; x++)
            {
                var v = _tiles[x, y];
                if (v != null)
                    v.RefreshExits(grid);
            }
        }
    }

    public void RefreshPlayerIcon()
    {
        if (playerIcon == null || _manager == null || _tiles == null)
            return;

        var p = _manager.PlayerGridPosition;
        if (p.x < 0 || p.x >= _tiles.GetLength(0) || p.y < 0 || p.y >= _tiles.GetLength(1))
            return;

        var tileView = _tiles[p.x, p.y];
        if (tileView == null)
            return;

        Transform anchor = tileView.transform;
        if (!string.IsNullOrEmpty(playerAnchorChildName))
        {
            var found = tileView.transform.Find(playerAnchorChildName);
            if (found != null)
                anchor = found;
        }

        playerIcon.SetParent(anchor, false);
        playerIcon.anchorMin = new Vector2(0.5f, 0.5f);
        playerIcon.anchorMax = new Vector2(0.5f, 0.5f);
        playerIcon.anchoredPosition = Vector2.zero;
        playerIcon.localScale = Vector3.one;

        RefreshArrowHighlights();
    }

    private void RefreshArrowHighlights()
    {
        if (_manager == null || _tiles == null)
            return;

        var p = _manager.PlayerGridPosition;
        var w = _tiles.GetLength(0);
        var h = _tiles.GetLength(1);
        for (var y = 0; y < h; y++)
        {
            for (var x = 0; x < w; x++)
            {
                var v = _tiles[x, y];
                if (v != null)
                    v.SetPlayerStandingHere(p.x == x && p.y == y);
            }
        }
    }

    private void DetachPlayerIconBeforeClearingTiles()
    {
        if (playerIcon == null || tilesParent == null)
            return;

        Transform safe = tilesParent.parent;
        if (safe == null)
            safe = transform.parent;
        if (safe == null || safe == tilesParent)
            safe = playerIcon.root;

        playerIcon.SetParent(safe, false);
    }

    private void ClearChildren()
    {
        if (tilesParent == null) return;
        for (var i = tilesParent.childCount - 1; i >= 0; i--)
            Destroy(tilesParent.GetChild(i).gameObject);
    }
}
