using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

[Serializable]
public class LayoutProp
{
    public TileBase tile;
    public Vector2Int cell;
}

[Serializable]
public class LayoutRoute
{
    [Tooltip("Road waypoints in cells, joined by horizontal or vertical segments. The first and last should sit just off the map so enemies walk on and off screen.")]
    public Vector2Int[] cells = Array.Empty<Vector2Int>();
}

// A city map template: the roads enemies walk and the props that block building. Cities on the
// world map reuse a handful of these.
[CreateAssetMenu(menuName = "Babel/City Layout", fileName = "CityLayout")]
public class CityLayout : ScriptableObject
{
    public string displayName = "City";
    [Tooltip("The map size in cells. Zero keeps the scene's grid size. Larger maps can have several routes.")]
    public Vector2Int gridSize;
    [Tooltip("The single road of a small map: waypoints in cells, joined by horizontal or vertical segments. The first and last should sit just off the map so enemies walk on and off screen. Ignored when Routes is filled in.")]
    public Vector2Int[] pathCells = Array.Empty<Vector2Int>();
    [Tooltip("Roads from several spawn points. They must all end on the same cell, where the castle stands.")]
    public List<LayoutRoute> routes = new();
    [Tooltip("Houses, trees and rocks. Cells with a prop can't be built on.")]
    public List<LayoutProp> props = new();

    // The roads of this layout: Routes if there are any, otherwise the single path.
    public List<Vector2Int[]> AllRoutes()
    {
        var all = new List<Vector2Int[]>();
        foreach (LayoutRoute route in routes)
            if (route != null && route.cells.Length >= 2) all.Add(route.cells);
        if (all.Count == 0 && pathCells.Length >= 2) all.Add(pathCells);
        return all;
    }

    // Cells on the map, zero when the layout keeps the scene's grid size.
    public int Area => Mathf.Max(0, gridSize.x) * Mathf.Max(0, gridSize.y);
}
