using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

// The match's tile grid. Cell (0,0) sits at world (0,0) and each cell is one world unit (16 px).
// Paints ground and path tiles, and tracks which cells hold a tower.
public class GridMap : MonoBehaviour
{
    [SerializeField] Tilemap groundTilemap;
    [SerializeField] Tilemap pathTilemap;
    [SerializeField] TileBase groundTile;
    [SerializeField] TileBase pathTile;
    [SerializeField] EnemyPath path;
    [SerializeField] Vector2Int size = new(24, 12);

    readonly HashSet<Vector2Int> pathCells = new();
    readonly Dictionary<Vector2Int, Tower> occupants = new();

    public Vector2Int Size => size;

    void Awake() => Paint();

    public void Paint()
    {
        groundTilemap.ClearAllTiles();
        pathTilemap.ClearAllTiles();
        pathCells.Clear();

        for (int x = 0; x < size.x; x++)
            for (int y = 0; y < size.y; y++)
                groundTilemap.SetTile(new Vector3Int(x, y, 0), groundTile);

        Vector3[] points = path.GetPoints();
        for (int i = 1; i < points.Length; i++)
            AddPathSegment(WorldToCell(points[i - 1]), WorldToCell(points[i]));

        foreach (Vector2Int cell in pathCells)
            if (InBounds(cell))
                pathTilemap.SetTile(new Vector3Int(cell.x, cell.y, 0), pathTile);
    }

    void AddPathSegment(Vector2Int from, Vector2Int to)
    {
        Vector2Int cell = from;
        pathCells.Add(cell);
        while (cell != to)
        {
            if (cell.x != to.x) cell.x += System.Math.Sign(to.x - cell.x);
            else cell.y += System.Math.Sign(to.y - cell.y);
            pathCells.Add(cell);
        }
    }

    public bool InBounds(Vector2Int cell) => cell.x >= 0 && cell.y >= 0 && cell.x < size.x && cell.y < size.y;

    public bool IsBuildable(Vector2Int cell) => InBounds(cell) && !pathCells.Contains(cell) && !occupants.ContainsKey(cell);

    public Vector2Int WorldToCell(Vector3 world) => new(Mathf.FloorToInt(world.x), Mathf.FloorToInt(world.y));

    public Vector3 CellCenter(Vector2Int cell) => new(cell.x + 0.5f, cell.y + 0.5f, 0f);

    public Tower GetOccupant(Vector2Int cell) => occupants.TryGetValue(cell, out Tower tower) ? tower : null;

    public void SetOccupant(Vector2Int cell, Tower tower) => occupants[cell] = tower;

    public void ClearOccupant(Vector2Int cell) => occupants.Remove(cell);
}
