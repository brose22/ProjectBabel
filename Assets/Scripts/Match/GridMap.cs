using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

// The match's tile grid. Cell (0,0) sits at world (0,0) and each cell is one world unit (16 px).
// Paints ground and path tiles, and tracks which cells hold a tower.
public class GridMap : MonoBehaviour
{
    [SerializeField] Tilemap groundTilemap;
    [SerializeField] Tilemap pathTilemap;
    [Tooltip("Optional: houses, trees and rocks. Any cell with a prop can't be built on.")]
    [SerializeField] Tilemap propTilemap;
    [SerializeField] TileBase groundTile;
    [SerializeField] TileBase pathTile;
    [Tooltip("Optional variety tile scattered over the ground (flowers and the like).")]
    [SerializeField] TileBase groundAccentTile;
    [SerializeField, Range(0f, 1f)] float groundAccentChance = 0.12f;
    [SerializeField] EnemyPath path;
    [SerializeField] Vector2Int size = new(24, 12);
    [Tooltip("Optional: the gate and castle, moved to the ends of the road when a city layout is applied.")]
    [SerializeField] Transform spawnMarker;
    [SerializeField] Transform baseMarker;

    readonly HashSet<Vector2Int> pathCells = new();
    // The road's on-map cells from the gate to the castle.
    readonly List<Vector2Int> orderedPathCells = new();
    readonly Dictionary<Vector2Int, Tower> occupants = new();

    public Vector2Int Size => size;
    // The road's on-map cells from the gate to the castle.
    public IReadOnlyList<Vector2Int> RoadCells => orderedPathCells;

    void Awake() => Paint();

    public void Paint()
    {
        groundTilemap.ClearAllTiles();
        pathTilemap.ClearAllTiles();
        pathCells.Clear();
        orderedPathCells.Clear();

        for (int x = 0; x < size.x; x++)
            for (int y = 0; y < size.y; y++)
                groundTilemap.SetTile(new Vector3Int(x, y, 0), IsAccentCell(x, y) ? groundAccentTile : groundTile);

        Vector3[] points = path.GetPoints();
        for (int i = 1; i < points.Length; i++)
            AddPathSegment(WorldToCell(points[i - 1]), WorldToCell(points[i]));

        foreach (Vector2Int cell in pathCells)
            if (InBounds(cell))
                pathTilemap.SetTile(new Vector3Int(cell.x, cell.y, 0), pathTile);
    }

    // Swaps in a city layout's road and props, moves the gate and castle to the road's ends and
    // repaints. Called by the match before play starts.
    public void ApplyLayout(CityLayout layout)
    {
        if (layout == null || layout.pathCells.Length < 2) return;
        var points = new Vector3[layout.pathCells.Length];
        for (int i = 0; i < points.Length; i++)
            points[i] = CellCenter(layout.pathCells[i]);
        path.SetPoints(points);

        if (propTilemap != null)
        {
            propTilemap.ClearAllTiles();
            foreach (LayoutProp prop in layout.props)
                if (prop.tile != null) propTilemap.SetTile(new Vector3Int(prop.cell.x, prop.cell.y, 0), prop.tile);
        }

        Paint();
        if (orderedPathCells.Count == 0) return;
        if (spawnMarker != null) spawnMarker.position = CellCenter(orderedPathCells[0]);
        if (baseMarker != null) baseMarker.position = CellCenter(orderedPathCells[^1]);
    }

    void AddPathSegment(Vector2Int from, Vector2Int to)
    {
        Vector2Int cell = from;
        AddPathCell(cell);
        while (cell != to)
        {
            if (cell.x != to.x) cell.x += System.Math.Sign(to.x - cell.x);
            else cell.y += System.Math.Sign(to.y - cell.y);
            AddPathCell(cell);
        }
    }

    void AddPathCell(Vector2Int cell)
    {
        if (pathCells.Add(cell) && InBounds(cell)) orderedPathCells.Add(cell);
    }

    // A fixed hash rather than Random, so the ground looks the same every time the map is painted.
    bool IsAccentCell(int x, int y) =>
        groundAccentTile != null && (uint)(x * 73856093 ^ y * 19349663) % 1000 < groundAccentChance * 1000f;

    public bool InBounds(Vector2Int cell) => cell.x >= 0 && cell.y >= 0 && cell.x < size.x && cell.y < size.y;

    public bool IsBuildable(Vector2Int cell) =>
        InBounds(cell) && !pathCells.Contains(cell) && !occupants.ContainsKey(cell) && !HasProp(cell);

    public bool IsRoad(Vector2Int cell) => pathCells.Contains(cell);

    // Soldiers may wait on the road, unlike towers, but not on a tower or a prop.
    public bool CanStandOn(Vector2Int cell) => InBounds(cell) && !occupants.ContainsKey(cell) && !HasProp(cell);

    bool HasProp(Vector2Int cell) => propTilemap != null && propTilemap.HasTile(new Vector3Int(cell.x, cell.y, 0));

    public Vector2Int WorldToCell(Vector3 world) => new(Mathf.FloorToInt(world.x), Mathf.FloorToInt(world.y));

    public Vector3 CellCenter(Vector2Int cell) => new(cell.x + 0.5f, cell.y + 0.5f, 0f);

    public Tower GetOccupant(Vector2Int cell) => occupants.TryGetValue(cell, out Tower tower) ? tower : null;

    public void SetOccupant(Vector2Int cell, Tower tower) => occupants[cell] = tower;

    public void ClearOccupant(Vector2Int cell) => occupants.Remove(cell);
}
