using UnityEngine;

namespace CarEvolution.Pathfinding
{
    /// <summary>
    /// Precomputes (once, at generation start) the true in-maze distance
    /// from every point on the track to the finish, via
    /// AStarPathfinder.ComputeDistanceField. Cars look this up every frame
    /// instead of straight-line distance, so a wall standing between a car
    /// and the goal is never mistaken for progress.
    /// </summary>
    public class GoalDistanceField
    {
        readonly MazeGrid grid;
        readonly float[,] distance;

        public GoalDistanceField(Vector2[] centerline, float trackWidth, bool closedLoop, Vector2 goalWorld, float cellSize = 1f)
        {
            grid = new MazeGrid(centerline, trackWidth, closedLoop, cellSize);
            Vector2Int goalCell = grid.WorldToCell(goalWorld);
            distance = AStarPathfinder.ComputeDistanceField(grid, goalCell);
        }

        /// <summary>Geodesic (maze-aware) distance in meters from this world position to the goal. A large fallback value means "off the walkable area / unreachable".</summary>
        public float DistanceTo(Vector3 worldPos)
        {
            Vector2Int cell = grid.WorldToCell(new Vector2(worldPos.x, worldPos.z));
            float d = distance[cell.x, cell.y];
            return float.IsInfinity(d) ? 9999f : d * grid.cellSize;
        }
    }
}
