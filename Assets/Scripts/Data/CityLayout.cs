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

// A city map template: the road enemies walk and the props that block building. Cities on the
// world map reuse a handful of these.
[CreateAssetMenu(menuName = "Babel/City Layout", fileName = "CityLayout")]
public class CityLayout : ScriptableObject
{
    public string displayName = "City";
    [Tooltip("Road waypoints in cells, joined by horizontal or vertical segments. The first and last should sit just off the map so enemies walk on and off screen.")]
    public Vector2Int[] pathCells = Array.Empty<Vector2Int>();
    [Tooltip("Houses, trees and rocks. Cells with a prop can't be built on.")]
    public List<LayoutProp> props = new();
}
