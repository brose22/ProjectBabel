using System.Collections.Generic;
using UnityEngine;

// The routes enemies walk. With no city layout the child transforms are the one route's
// waypoints, in order; a layout sets the routes instead. Segments are expected to be horizontal or
// vertical so they line up with the grid. All routes end at the same castle.
public class EnemyPath : MonoBehaviour
{
    List<Vector3[]> layoutRoutes;

    public int RouteCount => layoutRoutes != null ? layoutRoutes.Count : 1;

    // One route, for maps with a single road.
    public void SetPoints(Vector3[] points) => layoutRoutes = new List<Vector3[]> { points };

    public void SetRoutes(IEnumerable<Vector3[]> routes) => layoutRoutes = new List<Vector3[]>(routes);

    public Vector3[] GetPoints(int route = 0)
    {
        if (layoutRoutes != null) return (Vector3[])layoutRoutes[Mathf.Clamp(route, 0, layoutRoutes.Count - 1)].Clone();
        var points = new Vector3[transform.childCount];
        for (int i = 0; i < points.Length; i++)
            points[i] = transform.GetChild(i).position;
        return points;
    }

    void OnDrawGizmos()
    {
        Gizmos.color = Color.yellow;
        for (int i = 1; i < transform.childCount; i++)
            Gizmos.DrawLine(transform.GetChild(i - 1).position, transform.GetChild(i).position);
    }
}
