using System.Collections.Generic;
using UnityEngine;

namespace CarEvolution.Track
{
    /// <summary>
    /// Turns a TrackDefinition (a 2D centerline) into physical geometry:
    /// inner wall, outer wall, a road mesh and a series of checkpoint
    /// triggers. Data-driven, so a new track is just a new TrackDefinition
    /// asset - no new code required.
    /// </summary>
    public static class TrackBuilder
    {
        public static GameObject Build(TrackDefinition def, Transform parent, Material wallMaterial, Material roadMaterial)
        {
            var root = new GameObject($"Track_{def.trackName}");
            if (parent != null) root.transform.SetParent(parent, false);

            Vector2[] centerline = def.centerline;
            int count = centerline.Length;
            var innerPts = new List<Vector3>(count);
            var outerPts = new List<Vector3>(count);
            float half = def.trackWidth * 0.5f;

            for (int i = 0; i < count; i++)
            {
                Vector2 curr = centerline[i];
                Vector2 dirIn, dirOut;

                if (def.closedLoop)
                {
                    // Every vertex has a real neighbor on both sides.
                    Vector2 prev = centerline[(i - 1 + count) % count];
                    Vector2 next = centerline[(i + 1) % count];
                    dirIn = (curr - prev).normalized;
                    dirOut = (next - curr).normalized;
                }
                else
                {
                    // IMPORTANT: don't wrap around on an open track - the
                    // first point has no "previous" segment and the last
                    // point has no "next" one. Wrapping (old behavior) made
                    // the endpoint direction point at the far end of the
                    // track, producing a huge bogus spike/crossed geometry
                    // right at the start and finish.
                    dirIn = i > 0 ? (curr - centerline[i - 1]).normalized : (centerline[1] - curr).normalized;
                    dirOut = i < count - 1 ? (centerline[i + 1] - curr).normalized : dirIn;
                }

                Vector2 normalIn = new Vector2(-dirIn.y, dirIn.x);
                Vector2 normalOut = new Vector2(-dirOut.y, dirOut.x);

                Vector2 inner = curr + MiterOffset(normalIn, normalOut, -half);
                Vector2 outer = curr + MiterOffset(normalIn, normalOut, half);

                innerPts.Add(new Vector3(inner.x, 0f, inner.y));
                outerPts.Add(new Vector3(outer.x, 0f, outer.y));
            }

            BuildWall(root.transform, "InnerWall", innerPts, def.closedLoop, wallMaterial);
            BuildWall(root.transform, "OuterWall", outerPts, def.closedLoop, wallMaterial);
            BuildCheckpoints(root.transform, centerline, def.trackWidth, def.closedLoop);
            BuildGround(root.transform, innerPts, outerPts, def.closedLoop, roadMaterial);

            return root;
        }

        /// <summary>
        /// Proper miter-join offset for a polyline corner: instead of just
        /// pushing the vertex sideways by one averaged normal (which pinches
        /// inner corners and gaps outer ones, and can spike badly on sharp
        /// turns), this finds how far along the bisector of the two segment
        /// normals the offset edges actually meet, so a 90-degree turn comes
        /// out as a clean square corner instead of a sharp/pointy one.
        /// </summary>
        static Vector2 MiterOffset(Vector2 normalIn, Vector2 normalOut, float width)
        {
            Vector2 sum = normalIn + normalOut;
            float sumLen = sum.magnitude;

            // Near-180-degree reversal (sum ~ zero): there's no sensible
            // miter direction, just bevel using one of the two normals.
            if (sumLen < 0.001f) return normalIn * width;

            Vector2 miterDir = sum / sumLen;
            float cosHalfAngle = Vector2.Dot(miterDir, normalIn);
            if (Mathf.Abs(cosHalfAngle) < 0.1f) return normalIn * width; // avoid huge spikes on razor-sharp turns

            float scale = width / cosHalfAngle;
            // Cap the miter length so an unusually sharp turn bevels instead
            // of shooting out a long spike.
            float maxScale = Mathf.Abs(width) * 3f;
            scale = Mathf.Clamp(scale, -maxScale, maxScale);

            return miterDir * scale;
        }

        static void BuildWall(Transform parent, string name, List<Vector3> points, bool closed, Material mat)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);

            var lr = go.AddComponent<LineRenderer>();
            lr.positionCount = points.Count + (closed ? 1 : 0);
            lr.SetPositions(points.ToArray());
            if (closed) lr.SetPosition(points.Count, points[0]);
            lr.startWidth = lr.endWidth = 0.3f;
            lr.useWorldSpace = false;
            if (mat != null) lr.material = mat;

            int segCount = closed ? points.Count : points.Count - 1;
            for (int i = 0; i < segCount; i++)
            {
                Vector3 a = points[i];
                Vector3 b = points[(i + 1) % points.Count];
                var seg = new GameObject($"WallSeg_{i}");
                seg.transform.SetParent(go.transform, false);
                seg.tag = "Wall";
                Vector3 mid = (a + b) * 0.5f;
                float length = Vector3.Distance(a, b);
                seg.transform.position = mid;
                seg.transform.rotation = Quaternion.LookRotation(b - a, Vector3.up);
                var box = seg.AddComponent<BoxCollider>();
                // Tall + reasonably thick: a car that gets launched off a
                // sharp corner at speed needs a wall it can't hop or
                // tunnel over/through, not just a 1.5m curb.
                box.size = new Vector3(0.6f, 6f, length);
                box.center = new Vector3(0f, 3f, 0f);
            }
        }

        static void BuildCheckpoints(Transform parent, Vector2[] centerline, float width, bool closed)
        {
            var cpRoot = new GameObject("Checkpoints");
            cpRoot.transform.SetParent(parent, false);

            int count = centerline.Length;
            // Closed loops need one checkpoint per segment (no distinct
            // finish line). Open tracks need a checkpoint at EVERY point
            // including the last one, so there's a real finish-line trigger
            // at the end of the road, not just at count-2.
            int limit = count;
            for (int i = 0; i < limit; i++)
            {
                Vector2 curr = centerline[i];
                Vector2 dir;
                if (!closed && i == count - 1)
                {
                    // Last point on an open track has no "next" point to aim
                    // at - use the arrival direction instead.
                    Vector2 prev = centerline[i - 1];
                    dir = (curr - prev).normalized;
                }
                else
                {
                    Vector2 next = centerline[(i + 1) % count];
                    dir = (next - curr).normalized;
                }
                Vector2 normal = new Vector2(-dir.y, dir.x);

                var cp = new GameObject($"Checkpoint_{i}");
                cp.transform.SetParent(cpRoot.transform, false);
                cp.transform.position = new Vector3(curr.x, 0.5f, curr.y);
                cp.transform.rotation = Quaternion.LookRotation(new Vector3(normal.x, 0f, normal.y), Vector3.up);

                var box = cp.AddComponent<BoxCollider>();
                box.isTrigger = true;
                box.size = new Vector3(width, 2f, 0.5f);

                var trig = cp.AddComponent<CheckpointTrigger>();
                trig.index = i;
            }
        }

        static void BuildGround(Transform parent, List<Vector3> inner, List<Vector3> outer, bool closed, Material mat)
        {
            var go = new GameObject("Road");
            go.transform.SetParent(parent, false);
            var mf = go.AddComponent<MeshFilter>();
            var mr = go.AddComponent<MeshRenderer>();
            if (mat != null) mr.sharedMaterial = mat;

            int count = inner.Count;

            // Build the strip twice (front copy + a back copy at the same
            // positions) and wind them opposite ways, so the road renders
            // whether it's viewed from above or below and regardless of
            // which direction the centerline happens to wind in. Using two
            // separate vertex copies (rather than sharing vertices between
            // both windings) keeps RecalculateNormals from averaging two
            // opposite-facing normals into a degenerate zero vector.
            var verts = new List<Vector3>(count * 4);
            for (int i = 0; i < count; i++)
            {
                verts.Add(inner[i]);
                verts.Add(outer[i]);
            }
            int backOffset = verts.Count;
            for (int i = 0; i < count; i++)
            {
                verts.Add(inner[i]);
                verts.Add(outer[i]);
            }

            var tris = new List<int>();
            int segCount = closed ? count : count - 1;
            for (int i = 0; i < segCount; i++)
            {
                int a = (i * 2) % backOffset;
                int b = (i * 2 + 1) % backOffset;
                int c = (i * 2 + 2) % backOffset;
                int d = (i * 2 + 3) % backOffset;

                tris.Add(a); tris.Add(c); tris.Add(b);
                tris.Add(b); tris.Add(c); tris.Add(d);

                int a2 = a + backOffset, b2 = b + backOffset, c2 = c + backOffset, d2 = d + backOffset;
                tris.Add(a2); tris.Add(b2); tris.Add(c2);
                tris.Add(b2); tris.Add(d2); tris.Add(c2);
            }

            var mesh = new Mesh { name = "RoadMesh" };
            mesh.SetVertices(verts);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            mf.sharedMesh = mesh;

            // WheelColliders need something physical to push against - without
            // a collider here the cars simply fall through the road forever.
            var mc = go.AddComponent<MeshCollider>();
            mc.sharedMesh = mesh;
        }
    }
}
