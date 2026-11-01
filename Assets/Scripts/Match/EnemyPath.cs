using UnityEngine;

// The route enemies walk. Each child transform is a waypoint, in order, unless a city layout has
// set the points. Segments are expected to be horizontal or vertical so they line up with the grid.
public class EnemyPath : MonoBehaviour
{
    Vector3[] layoutPoints;

    public void SetPoints(Vector3[] points) => layoutPoints = points;

    public Vector3[] GetPoints()
    {
        if (layoutPoints != null) return (Vector3[])layoutPoints.Clone();
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
