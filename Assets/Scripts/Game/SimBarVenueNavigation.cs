using System.Collections.Generic;
using UnityEngine;

/// <summary>A small, allocation-free search grid over the authored public aisles.</summary>
internal sealed class SimBarVenueNavigation
{
    internal const float Radius = 0.23f;
    private const float Cell = 0.4f;
    private const int Columns = 28, Rows = 24;
    private static readonly Vector2 Origin = new Vector2(-1.6f, -5.8f);
    private readonly Rect[] obstacles = {
        Rect.MinMaxRect(-10.02f, -2.32f, -2.02f, -.88f),
        Rect.MinMaxRect(-9.745f, -2.625f, -2.255f, -2.535f),
        Rect.MinMaxRect(-9.44f, -3.49f, -2.56f, -2.81f),
        Rect.MinMaxRect(-9.54f, -7.14f, -5.66f, -3.64f),
        Rect.MinMaxRect(-4.64f, -7.14f, -.76f, -3.64f),
        Rect.MinMaxRect(-9.615f, 3.41f, -5.985f, 7.07f),
        Rect.MinMaxRect(-5.515f, 3.41f, -1.885f, 7.07f),
        Rect.MinMaxRect(1.4625f, 2.2075f, 9.3375f, 6.5f),
        Rect.MinMaxRect(4.3f, 1.6025f, 6.4f, 2.245f)
    };
    private readonly Vector3[] points = new Vector3[Columns * Rows];
    private readonly bool[] walkable = new bool[Columns * Rows], closed = new bool[Columns * Rows];
    private readonly float[] scores = new float[Columns * Rows];
    private readonly int[] parents = new int[Columns * Rows];
    private readonly List<int> open = new List<int>(Columns * Rows), reverse = new List<int>(Columns * Rows);
    private List<Vector4> solidCrowdBodies;
    private Vector3 solidCrowdGoal;

    internal SimBarVenueNavigation()
    {
        for (int i = 0; i < points.Length; i++)
        {
            points[i] = new Vector3(Origin.x + i % Columns * Cell, 0f, Origin.y + i / Columns * Cell);
            walkable[i] = IsWalkable(points[i]);
        }
    }

    internal bool IsWalkable(Vector3 p)
    {
        if (p.x < -1.65f || p.x > 9.1f || p.z < -5.85f || p.z > 3.45f) return false;
        foreach (Rect obstacle in obstacles)
            if (p.x > obstacle.xMin - Radius && p.x < obstacle.xMax + Radius
                && p.z > obstacle.yMin - Radius && p.z < obstacle.yMax + Radius) return false;
        return true;
    }

    internal bool SegmentClear(Vector3 a, Vector3 b)
    {
        int steps = Mathf.Max(1, Mathf.CeilToInt(Vector3.Distance(a, b) / .12f));
        for (int i = 1; i <= steps; i++) if (!IsWalkable(Vector3.Lerp(a, b, i / (float)steps))) return false;
        return true;
    }

    internal bool FindPath(Vector3 start, Vector3 goal, List<Vector3> result, List<Vector3> people)
        => FindPathCore(start, goal, result, people, false);

    internal bool FindAislePath(Vector3 start, Vector3 goal, List<Vector3> result, List<Vector3> people)
        => FindPathCore(start, goal, result, people, true);

    internal bool FindCrowdPath(Vector3 start, Vector3 goal, List<Vector3> result, List<Vector3> people,
        List<Vector4> bodies)
    {
        solidCrowdBodies = bodies; solidCrowdGoal = goal;
        try { return FindPathCore(start, goal, result, people, false); }
        finally { solidCrowdBodies = null; solidCrowdGoal = Vector3.zero; }
    }

    private bool FindPathCore(Vector3 start, Vector3 goal, List<Vector3> result, List<Vector3> people, bool avoidCrowd)
    {
        result.Clear();
        if (!IsWalkable(start) || !IsWalkable(goal)) return false;
        if (avoidCrowd && (SimBarExperience.CrowdFloorReserved(start) || SimBarExperience.CrowdFloorReserved(goal))) return false;
        int first = Closest(start, avoidCrowd), last = Closest(goal, avoidCrowd);
        if (first < 0 || last < 0) return false;
        open.Clear(); reverse.Clear();
        for (int i = 0; i < points.Length; i++) { scores[i] = float.PositiveInfinity; parents[i] = -1; closed[i] = false; }
        scores[first] = 0; open.Add(first);
        while (open.Count > 0)
        {
            int best = 0;
            float cost = float.PositiveInfinity;
            for (int i = 0; i < open.Count; i++)
            {
                int n = open[i]; float f = scores[n] + Vector3.Distance(points[n], points[last]);
                if (f < cost) { cost = f; best = i; }
            }
            int current = open[best]; open.RemoveAt(best);
            if (current == last)
            {
                for (int n = last; n != first && n >= 0; n = parents[n]) reverse.Add(n);
                result.Add(points[first]);
                for (int i = reverse.Count - 1; i >= 0; i--) result.Add(points[reverse[i]]);
                if (IsWalkable(goal) && (result.Count == 0 || SegmentAllowed(result[result.Count - 1], goal, avoidCrowd))) result.Add(new Vector3(goal.x, 0, goal.z));
                return result.Count > 0;
            }
            closed[current] = true;
            int cx = current % Columns, cz = current / Columns;
            for (int dz = -1; dz <= 1; dz++) for (int dx = -1; dx <= 1; dx++)
            {
                if (dx == 0 && dz == 0) continue;
                int x = cx + dx, z = cz + dz;
                if (x < 0 || x >= Columns || z < 0 || z >= Rows) continue;
                int next = z * Columns + x;
                if (!walkable[next] || closed[next] || !SegmentAllowed(points[current], points[next], avoidCrowd)) continue;
                float crowdCost = 0f;
                foreach (Vector3 person in people)
                {
                    float d = Vector3.Distance(points[next], person);
                    if (d < .8f && Vector3.Distance(person, start) > .3f) crowdCost += (.8f - d) * 2f;
                }
                float candidate = scores[current] + Cell * (dx != 0 && dz != 0 ? 1.414214f : 1f) + crowdCost;
                if (candidate >= scores[next]) continue;
                scores[next] = candidate; parents[next] = current;
                if (!open.Contains(next)) open.Add(next);
            }
        }
        return false;
    }

    private bool SegmentAllowed(Vector3 a, Vector3 b, bool avoidCrowd)
    {
        if (!SegmentClear(a, b)) return false;
        if (solidCrowdBodies != null)
        {
            Vector3 travel = b - a; travel.y = 0f;
            foreach (Vector4 body in solidCrowdBodies)
            {
                float minimum = Mathf.Max(0f, body.w - .001f);
                Vector3 fromGoal = new Vector3(body.x - solidCrowdGoal.x, 0f, body.z - solidCrowdGoal.z);
                // An occupied queue destination is temporary: approach its safe edge
                // and let the swept movement guard wait for the preceding body to clear.
                if (fromGoal.sqrMagnitude < minimum * minimum) continue;
                Vector3 offset = new Vector3(body.x - a.x, 0f, body.z - a.z);
                float amount = travel.sqrMagnitude > .0000001f ? Mathf.Clamp01(Vector3.Dot(offset, travel) / travel.sqrMagnitude) : 0f;
                if ((offset - travel * amount).sqrMagnitude < minimum * minimum) return false;
            }
        }
        if (!avoidCrowd) return true;
        int steps = Mathf.Max(1, Mathf.CeilToInt(Vector3.Distance(a, b) / .12f));
        for (int i = 0; i <= steps; i++)
            if (SimBarExperience.CrowdFloorReserved(Vector3.Lerp(a, b, i / (float)steps))) return false;
        return true;
    }

    private int Closest(Vector3 p, bool avoidCrowd)
    {
        int x = Mathf.Clamp(Mathf.RoundToInt((p.x - Origin.x) / Cell), 0, Columns - 1);
        int z = Mathf.Clamp(Mathf.RoundToInt((p.z - Origin.y) / Cell), 0, Rows - 1);
        int nearest = z * Columns + x;
        if (walkable[nearest] && SegmentAllowed(p, points[nearest], avoidCrowd)) return nearest;
        int found = -1; float distance = float.PositiveInfinity;
        for (int i = 0; i < points.Length; i++)
        {
            if (!walkable[i]) continue;
            float d = (p - points[i]).sqrMagnitude;
            if (d < distance && SegmentAllowed(p, points[i], avoidCrowd)) { found = i; distance = d; }
        }
        return found;
    }
}
