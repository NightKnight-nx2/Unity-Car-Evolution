using UnityEngine;

namespace CarEvolution.Pathfinding
{
    /// <summary>
    /// A coarse walkable/blocked grid built from a track's centerline - the
    /// search space AStarPathfinder runs over. Cell (x,y) covers a
    /// cellSize x cellSize square in world XZ space. A cell is walkable if
    /// it's within trackWidth/2 (minus a small wall margin) of the
    /// centerline polyline.
    /// </summary>
    public class MazeGrid
    {
        public readonly float cellSize;
        public readonly int width;
        public readonly int height;
        public readonly Vector2 originWorld;

        readonly bool[,] walkable;

        public MazeGrid(Vector2[] centerline, float trackWidth, bool closedLoop, float cellSize = 1f, float wallMargin = 0.6f)
        {
            this.cellSize = cellSize;

            Vector2 min = centerline[0];
            Vector2 max = centerline[0];
            foreach (var p in centerline)
            {
                min = Vector2.Min(min, p);
                max = Vector2.Max(max, p);
            }
            float pad = trackWidth * 0.5f + 1f;
            min -= new Vector2(pad, pad);
            max += new Vector2(pad, pad);
            originWorld = min;

            width = Mathf.Max(1, Mathf.CeilToInt((max.x - min.x) / cellSize));
            height = Mathf.Max(1, Mathf.CeilToInt((max.y - min.y) / cellSize));
            walkable = new bool[width, height];

            float halfWidth = Mathf.Max(0.1f, trackWidth * 0.5f - wallMargin);
            int segCount = closedLoop ? centerline.Length : centerline.Length - 1;

            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < height; y++)
                {
                    Vector2 world = CellToWorld(x, y);
                    float best = float.MaxValue;
                    for (int i = 0; i < segCount; i++)
                    {
                        Vector2 a = centerline[i];
                        Vector2 b = centerline[(i + 1) % centerline.Length];
                        float d = DistancePointToSegment(world, a, b);
                        if (d < best) best = d;
                        if (best <= halfWidth) break;
                    }
                    walkable[x, y] = best <= halfWidth;
                }
            }
        }

        public Vector2Int WorldToCell(Vector2 world)
        {
            int x = Mathf.FloorToInt((world.x - originWorld.x) / cellSize);
            int y = Mathf.FloorToInt((world.y - originWorld.y) / cellSize);
            return new Vector2Int(Mathf.Clamp(x, 0, width - 1), Mathf.Clamp(y, 0, height - 1));
        }

        public Vector2 CellToWorld(int x, int y)
        {
            return originWorld + new Vector2((x + 0.5f) * cellSize, (y + 0.5f) * cellSize);
        }

        public bool IsWalkable(int x, int y) => x >= 0 && y >= 0 && x < width && y < height && walkable[x, y];

        public bool IsWalkable(Vector2Int c) => IsWalkable(c.x, c.y);

        static float DistancePointToSegment(Vector2 p, Vector2 a, Vector2 b)
        {
            Vector2 ab = b - a;
            float t = ab.sqrMagnitude > 0.0001f ? Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude) : 0f;
            Vector2 closest = a + ab * t;
            return Vector2.Distance(p, closest);
        }
    }
}
