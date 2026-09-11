using System.Collections.Generic;
using UnityEngine;

namespace CarEvolution.Pathfinding
{
    /// <summary>
    /// Grid-based A* search over a MazeGrid. Two entry points:
    ///
    ///  - FindPath: classic point-to-point A* with an octile-distance
    ///    heuristic - handy for visualizing/debugging the shortest route
    ///    through a maze.
    ///  - ComputeDistanceField: runs the same search outward from a single
    ///    goal cell to every reachable cell at once (A* with a zero
    ///    heuristic degenerates to Dijkstra, which is exactly what you want
    ///    when you need "distance to goal" from everywhere, not just one
    ///    start). This is what GoalDistanceField uses so every car can look
    ///    up its distance-to-finish in O(1) each frame instead of the whole
    ///    population re-running a search every tick.
    /// </summary>
    public static class AStarPathfinder
    {
        static readonly Vector2Int[] Neighbors =
        {
            new Vector2Int(1, 0), new Vector2Int(-1, 0), new Vector2Int(0, 1), new Vector2Int(0, -1),
            new Vector2Int(1, 1), new Vector2Int(1, -1), new Vector2Int(-1, 1), new Vector2Int(-1, -1),
        };

        const float DiagonalCost = 1.4142136f;

        public static float[,] ComputeDistanceField(MazeGrid grid, Vector2Int goal)
        {
            var dist = new float[grid.width, grid.height];
            for (int x = 0; x < grid.width; x++)
                for (int y = 0; y < grid.height; y++)
                    dist[x, y] = float.PositiveInfinity;

            if (!grid.IsWalkable(goal)) return dist;

            var open = new MinHeap<Vector2Int>();
            dist[goal.x, goal.y] = 0f;
            open.Enqueue(goal, 0f);

            while (open.Count > 0)
            {
                Vector2Int current = open.Dequeue();
                float currentDist = dist[current.x, current.y];

                foreach (var d in Neighbors)
                {
                    Vector2Int next = current + d;
                    if (!grid.IsWalkable(next)) continue;

                    float step = (d.x != 0 && d.y != 0) ? DiagonalCost : 1f;
                    float nd = currentDist + step;
                    if (nd < dist[next.x, next.y])
                    {
                        dist[next.x, next.y] = nd;
                        open.Enqueue(next, nd);
                    }
                }
            }

            return dist;
        }

        public static List<Vector2Int> FindPath(MazeGrid grid, Vector2Int start, Vector2Int goal)
        {
            if (!grid.IsWalkable(start) || !grid.IsWalkable(goal)) return null;

            var open = new MinHeap<Vector2Int>();
            var gScore = new Dictionary<Vector2Int, float> { [start] = 0f };
            var cameFrom = new Dictionary<Vector2Int, Vector2Int>();
            var closed = new HashSet<Vector2Int>();

            open.Enqueue(start, Heuristic(start, goal));

            while (open.Count > 0)
            {
                Vector2Int current = open.Dequeue();
                if (current == goal) return ReconstructPath(cameFrom, current);
                if (!closed.Add(current)) continue;

                foreach (var d in Neighbors)
                {
                    Vector2Int next = current + d;
                    if (!grid.IsWalkable(next) || closed.Contains(next)) continue;

                    float step = (d.x != 0 && d.y != 0) ? DiagonalCost : 1f;
                    float tentative = gScore[current] + step;
                    if (!gScore.TryGetValue(next, out float existing) || tentative < existing)
                    {
                        gScore[next] = tentative;
                        cameFrom[next] = current;
                        open.Enqueue(next, tentative + Heuristic(next, goal));
                    }
                }
            }

            return null; // no path
        }

        static float Heuristic(Vector2Int a, Vector2Int b)
        {
            int dx = Mathf.Abs(a.x - b.x);
            int dy = Mathf.Abs(a.y - b.y);
            return (dx + dy) + (DiagonalCost - 2f) * Mathf.Min(dx, dy); // octile distance
        }

        static List<Vector2Int> ReconstructPath(Dictionary<Vector2Int, Vector2Int> cameFrom, Vector2Int current)
        {
            var path = new List<Vector2Int> { current };
            while (cameFrom.TryGetValue(current, out var prev))
            {
                current = prev;
                path.Add(current);
            }
            path.Reverse();
            return path;
        }

        /// <summary>Minimal binary-heap priority queue (Unity's API compatibility level may predate System.Collections.Generic.PriorityQueue).</summary>
        class MinHeap<T>
        {
            readonly List<(T item, float priority)> heap = new List<(T, float)>();
            public int Count => heap.Count;

            public void Enqueue(T item, float priority)
            {
                heap.Add((item, priority));
                int i = heap.Count - 1;
                while (i > 0)
                {
                    int parent = (i - 1) / 2;
                    if (heap[parent].priority <= heap[i].priority) break;
                    (heap[parent], heap[i]) = (heap[i], heap[parent]);
                    i = parent;
                }
            }

            public T Dequeue()
            {
                var root = heap[0];
                int last = heap.Count - 1;
                heap[0] = heap[last];
                heap.RemoveAt(last);

                int i = 0;
                while (true)
                {
                    int left = i * 2 + 1, right = i * 2 + 2, smallest = i;
                    if (left < heap.Count && heap[left].priority < heap[smallest].priority) smallest = left;
                    if (right < heap.Count && heap[right].priority < heap[smallest].priority) smallest = right;
                    if (smallest == i) break;
                    (heap[smallest], heap[i]) = (heap[i], heap[smallest]);
                    i = smallest;
                }

                return root.item;
            }
        }
    }
}
