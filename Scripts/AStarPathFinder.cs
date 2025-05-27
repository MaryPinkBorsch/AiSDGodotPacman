using Godot;
using System;
using System.Collections.Generic;

public static class AStarPathfinder
{
    public static List<Vector2I> FindPath(
        Vector2I start,
        Vector2I goal,
        Func<Vector2I, bool> Traversable,
        int gridWidth,
        int gridHeight)
    {
        // openSet - фронт в поиске А*
        var openSet = new PriorityQueue<Vector2I, int>();

        // cameFrom - значение предыдущей клетки для каждой клетки
        var cameFrom = new Dictionary<Vector2I, Vector2I>();

        // мапа стоимости для клеток
        var gScore = new Dictionary<Vector2I, int> { [start] = 0 };

        // евклидово расстояние 
        int startToGoalDistance = (int)Math.Sqrt((goal.X - start.X) * (goal.X - start.X) + (goal.Y - start.Y) * (goal.Y - start.Y));
        openSet.Enqueue(start, startToGoalDistance);

        while (openSet.Count > 0)
        {
            var current = openSet.Dequeue();

            if (current == goal)
                return ReconstructPath(cameFrom, current);

            foreach (var neighbor in GetNeighbors(current, gridWidth, gridHeight))
            {
                if (!Traversable(new Vector2I(neighbor.X, neighbor.Y)))
                    continue;

                // стоимость до текущей клетки current
                int GScore = gScore[current] + 1;

                if (!gScore.ContainsKey(neighbor) || GScore < gScore[neighbor])
                {
                    cameFrom[neighbor] = current;
                    gScore[neighbor] = GScore;
                    // эвристика - оценка от текущей до цели
                    int neighborToGoalDistance = (int)Math.Sqrt((goal.X - neighbor.X) * (goal.X - neighbor.X) + (goal.Y - neighbor.Y) * (goal.Y - neighbor.Y));
                    int fScore = GScore + neighborToGoalDistance;
                    openSet.Enqueue(neighbor, fScore);
                }
            }
        }

        return null;
    }

    private static List<Vector2I> ReconstructPath(Dictionary<Vector2I, Vector2I> cameFrom, Vector2I current)
    {
        var path = new List<Vector2I> { current };
        while (cameFrom.ContainsKey(current))
        {
            current = cameFrom[current];
            path.Add(current);
        }
        path.Reverse();
        return path;
    }

    private static IEnumerable<Vector2I> GetNeighbors(Vector2I pos, int width, int height)
    {
        var directions = new[]
        {
            new Vector2I(0, 1),
            new Vector2I(1, 0),
            new Vector2I(0, -1),
            new Vector2I(-1, 0)
        };

        foreach (var dir in directions)
        {
            var neighbor = new Vector2I(pos.X + dir.X, pos.Y + dir.Y);
            if (neighbor.X >= 0 && neighbor.X < width &&
                neighbor.Y >= 0 && neighbor.Y < height)
            {
                yield return neighbor;
            }
        }
    }
}
