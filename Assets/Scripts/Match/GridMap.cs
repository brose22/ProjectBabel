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
    [Tooltip("Optional: fills the padding around the playable map, where nothing can be placed.")]
    [SerializeField] TileBase borderTreeTile;
    [SerializeField] EnemyPath path;
    [SerializeField] Vector2Int size = new(24, 12);
    [Tooltip("Optional: the gate and castle, moved to the ends of the road when a city layout is applied.")]
    [SerializeField] Transform spawnMarker;
    [SerializeField] Transform baseMarker;

    readonly HashSet<Vector2Int> pathCells = new();
    // Each route's on-map cells from its gate to the castle.
    readonly List<List<Vector2Int>> routeCells = new();
    // The same cells as plain coordinates, for the route maths.
    readonly List<IReadOnlyList<(int x, int y)>> plainRoutes = new();
    // Every on-map road cell once, from furthest from the castle to the castle itself.
    readonly List<Vector2Int> orderedPathCells = new();
    // Gates added for routes beyond the first, copies of spawnMarker.
    readonly List<Transform> extraGateMarkers = new();
    readonly Dictionary<Vector2Int, Tower> occupants = new();
    // The gates' and the castle's tiles: the routes' ends plus wherever the markers stand.
    readonly HashSet<Vector2Int> gateAndCastleCells = new();

    // Decorative ground painted this many tiles beyond the playable map on every side, so the
    // camera can look past the edge. Nothing can be built or walked there.
    public const int Padding = 6;

    public Vector2Int Size => size;
    // The playable map plus its padding, in world units: how far the camera may look.
    public Rect ViewBounds => new(-Padding, -Padding, size.x + 2 * Padding, size.y + 2 * Padding);
    // Every on-map road cell once, from the furthest from the castle to the castle. With several
    // routes the cells are ordered by their fewest steps to the castle.
    public IReadOnlyList<Vector2Int> RoadCells => orderedPathCells;
    public int RouteCount => routeCells.Count;
    // Each route's on-map cells from its gate to the castle.
    public IReadOnlyList<IReadOnlyList<(int x, int y)>> Routes => plainRoutes;

    void Awake() => Paint();

    public void Paint()
    {
        groundTilemap.ClearAllTiles();
        pathTilemap.ClearAllTiles();
        pathCells.Clear();
        routeCells.Clear();
        plainRoutes.Clear();
        orderedPathCells.Clear();

        for (int x = -Padding; x < size.x + Padding; x++)
            for (int y = -Padding; y < size.y + Padding; y++)
                groundTilemap.SetTile(new Vector3Int(x, y, 0), IsAccentCell(x, y) ? groundAccentTile : groundTile);

        for (int route = 0; route < path.RouteCount; route++)
        {
            Vector3[] points = path.GetPoints(route);
            var cells = new List<Vector2Int>();
            var seen = new HashSet<Vector2Int>();
            for (int i = 1; i < points.Length; i++)
                AddPathSegment(WorldToCell(points[i - 1]), WorldToCell(points[i]), cells, seen);
            routeCells.Add(cells);
            plainRoutes.Add(cells.ConvertAll(c => (c.x, c.y)));
        }

        foreach ((int x, int y) in RouteMath.OrderedRoadCells(plainRoutes))
            orderedPathCells.Add(new Vector2Int(x, y));

        foreach (Vector2Int cell in pathCells)
            if (InBounds(cell))
                pathTilemap.SetTile(new Vector3Int(cell.x, cell.y, 0), pathTile);

        PaintBorderTrees();
        RefreshGateAndCastleCells();
    }

    void RefreshGateAndCastleCells()
    {
        gateAndCastleCells.Clear();
        foreach ((int x, int y) in RouteMath.EndCells(plainRoutes))
            gateAndCastleCells.Add(new Vector2Int(x, y));
        AddMarkerCell(spawnMarker);
        AddMarkerCell(baseMarker);
        foreach (Transform gate in extraGateMarkers) AddMarkerCell(gate);
    }

    void AddMarkerCell(Transform marker)
    {
        if (marker != null) gateAndCastleCells.Add(WorldToCell(marker.position));
    }

    void PaintBorderTrees()
    {
        if (propTilemap == null || borderTreeTile == null) return;
        for (int x = -Padding; x < size.x + Padding; x++)
            for (int y = -Padding; y < size.y + Padding; y++)
                if (!InBounds(new Vector2Int(x, y)) && !pathCells.Contains(new Vector2Int(x, y)))
                    propTilemap.SetTile(new Vector3Int(x, y, 0), borderTreeTile);
    }

    // Swaps in a city layout's roads and props, moves the gates and the castle to
    // the roads' ends and repaints. Called by the match before play starts.
    public void ApplyLayout(CityLayout layout)
    {
        if (layout == null) return;
        List<Vector2Int[]> routes = layout.AllRoutes();
        if (routes.Count == 0) return;

        var routePoints = new List<Vector3[]>();
        foreach (Vector2Int[] cells in routes)
            routePoints.Add(System.Array.ConvertAll(cells, CellCenter));
        path.SetRoutes(routePoints);

        if (propTilemap != null)
        {
            propTilemap.ClearAllTiles();
            foreach (LayoutProp prop in layout.props)
                if (prop.tile != null) propTilemap.SetTile(new Vector3Int(prop.cell.x, prop.cell.y, 0), prop.tile);
        }

        Paint();
        if (!RouteMath.EndAtTheSameCell(plainRoutes))
            Debug.LogWarning($"Layout {layout.displayName}: its routes do not all end on the same cell, so they have no common castle.");
        PlaceMarkers();
    }

    // The gate sits at the start of each route (extra gates are copies of the first) and the castle
    // at the end of the first.
    void PlaceMarkers()
    {
        foreach (Transform extra in extraGateMarkers)
            if (extra != null) Destroy(extra.gameObject);
        extraGateMarkers.Clear();
        if (routeCells.Count == 0 || routeCells[0].Count == 0) return;

        if (spawnMarker != null)
        {
            spawnMarker.position = CellCenter(routeCells[0][0]);
            for (int route = 1; route < routeCells.Count; route++)
            {
                if (routeCells[route].Count == 0) continue;
                Transform gate = Instantiate(spawnMarker, spawnMarker.parent);
                gate.position = CellCenter(routeCells[route][0]);
                extraGateMarkers.Add(gate);
            }
        }
        if (baseMarker != null) baseMarker.position = CellCenter(routeCells[0][^1]);
        RefreshGateAndCastleCells();
    }

    // The side of the map this spawn point's gate is on, such as "north", or empty with one gate.
    public string GateName(Vector3 spawnPoint)
    {
        var gates = new List<(int x, int y)>();
        foreach (IReadOnlyList<(int x, int y)> route in plainRoutes)
            if (route.Count > 0) gates.Add(route[0]);
        if (gates.Count == 0) return "";
        (int gate, _) = RouteMath.Nearest(gates.ConvertAll(g => (IReadOnlyList<(int x, int y)>)new[] { g }), spawnPoint.x, spawnPoint.y);
        return RouteMath.GateDirection(gates, gate);
    }

    void AddPathSegment(Vector2Int from, Vector2Int to, List<Vector2Int> cells, HashSet<Vector2Int> seen)
    {
        Vector2Int cell = from;
        AddPathCell(cell, cells, seen);
        while (cell != to)
        {
            if (cell.x != to.x) cell.x += System.Math.Sign(to.x - cell.x);
            else cell.y += System.Math.Sign(to.y - cell.y);
            AddPathCell(cell, cells, seen);
        }
    }

    void AddPathCell(Vector2Int cell, List<Vector2Int> cells, HashSet<Vector2Int> seen)
    {
        pathCells.Add(cell);
        if (InBounds(cell) && seen.Add(cell)) cells.Add(cell);
    }

    // A fixed hash rather than Random, so the ground looks the same every time the map is painted.
    bool IsAccentCell(int x, int y) =>
        groundAccentTile != null && (uint)(x * 73856093 ^ y * 19349663) % 1000 < groundAccentChance * 1000f;

    public bool InBounds(Vector2Int cell) => cell.x >= 0 && cell.y >= 0 && cell.x < size.x && cell.y < size.y;

    public bool IsBuildable(Vector2Int cell) =>
        InBounds(cell) && !pathCells.Contains(cell) && !occupants.ContainsKey(cell) && !HasProp(cell) && !IsGateOrCastle(cell);

    // A gate's or the castle's tile, on every route of the map.
    public bool IsGateOrCastle(Vector2Int cell) => gateAndCastleCells.Contains(cell);

    public bool IsRoad(Vector2Int cell) => pathCells.Contains(cell);

    // Soldiers may wait on the road, unlike towers, but not on a tower, a prop, a gate or the castle.
    public bool CanStandOn(Vector2Int cell) =>
        InBounds(cell) && !occupants.ContainsKey(cell) && !HasProp(cell) && !IsGateOrCastle(cell);

    bool HasProp(Vector2Int cell) => propTilemap != null && propTilemap.HasTile(new Vector3Int(cell.x, cell.y, 0));

    public Vector2Int WorldToCell(Vector3 world) => new(Mathf.FloorToInt(world.x), Mathf.FloorToInt(world.y));

    public Vector3 CellCenter(Vector2Int cell) => new(cell.x + 0.5f, cell.y + 0.5f, 0f);

    public Tower GetOccupant(Vector2Int cell) => occupants.TryGetValue(cell, out Tower tower) ? tower : null;

    public void SetOccupant(Vector2Int cell, Tower tower) => occupants[cell] = tower;

    public void ClearOccupant(Vector2Int cell) => occupants.Remove(cell);
}
