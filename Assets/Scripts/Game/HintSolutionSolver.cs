using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public static class HintSolutionSolver
{
    private const float NeighborPositionTolerance = 0.08f;

    private sealed class Orientation
    {
        public readonly int[] Ports;

        public Orientation(IEnumerable<int> ports)
        {
            Ports = ports.Distinct().OrderBy(angle => angle).ToArray();
        }

        public bool HasPort(int angle)
        {
            return ArrayContains(Ports, NormalizeAngle(angle));
        }
    }

    private sealed class SearchState
    {
        public readonly Dictionary<WireCell, Orientation> Orientations =
            new Dictionary<WireCell, Orientation>();
        public readonly HashSet<WireCell> ActiveCells = new HashSet<WireCell>();
        public readonly HashSet<WireCell> ProcessedCells = new HashSet<WireCell>();
        public readonly HashSet<WireCell> VisitingCells = new HashSet<WireCell>();

        public SearchState Copy()
        {
            var copy = new SearchState();
            foreach (var orientation in Orientations)
                copy.Orientations.Add(orientation.Key, orientation.Value);
            copy.ActiveCells.UnionWith(ActiveCells);
            copy.ProcessedCells.UnionWith(ProcessedCells);
            copy.VisitingCells.UnionWith(VisitingCells);
            return copy;
        }
    }

    public static bool TrySolve(
        IReadOnlyList<WireCell> cells,
        out Dictionary<WireCell, int[]> targetAngles)
    {
        targetAngles = null;
        if (cells == null || cells.Count == 0)
            return false;

        var sources = cells.Where(cell => cell.State == WireCellState.Source).ToList();
        var bulbs = cells.Where(cell => cell.State == WireCellState.Bulb).ToList();
        if (sources.Count == 0 || bulbs.Count == 0)
            return false;

        var neighbors = BuildNeighbors(cells);
        var orientations = cells.ToDictionary(cell => cell, GetOrientations);
        var searchState = new SearchState();
        searchState = ConnectSources(sources, bulbs, 0, neighbors, orientations, searchState)
            .FirstOrDefault();
        if (searchState == null)
            return false;

        targetAngles = new Dictionary<WireCell, int[]>();
        foreach (var cell in cells)
        {
            targetAngles.Add(
                cell,
                searchState.Orientations.TryGetValue(cell, out var orientation)
                    ? orientation.Ports
                    : NormalizeAngles(cell.OutputAngles));
        }

        return true;
    }

    private static IEnumerable<SearchState> ConnectSources(
        IReadOnlyList<WireCell> sources,
        IReadOnlyList<WireCell> bulbs,
        int sourceIndex,
        Dictionary<WireCell, Dictionary<int, WireCell>> neighbors,
        Dictionary<WireCell, List<Orientation>> availableOrientations,
        SearchState state)
    {
        if (sourceIndex == sources.Count)
        {
            if (bulbs.All(bulb => state.ActiveCells.Contains(bulb)))
                yield return state;

            yield break;
        }

        foreach (var connectedState in ConnectCell(
                     sources[sourceIndex],
                     null,
                     neighbors,
                     availableOrientations,
                     state))
        {
            foreach (var result in ConnectSources(
                         sources,
                         bulbs,
                         sourceIndex + 1,
                         neighbors,
                         availableOrientations,
                         connectedState))
            {
                yield return result;
            }
        }
    }

    private static IEnumerable<SearchState> ConnectCell(
        WireCell cell,
        int? requiredPort,
        Dictionary<WireCell, Dictionary<int, WireCell>> neighbors,
        Dictionary<WireCell, List<Orientation>> availableOrientations,
        SearchState state)
    {
        if (state.Orientations.TryGetValue(cell, out var assignedOrientation))
        {
            if (requiredPort.HasValue && !assignedOrientation.HasPort(requiredPort.Value))
                yield break;

            if (state.ProcessedCells.Contains(cell) || state.VisitingCells.Contains(cell))
            {
                yield return state;
                yield break;
            }

            var branch = state.Copy();
            branch.VisitingCells.Add(cell);
            foreach (var connectedState in ConnectPorts(
                         cell,
                         assignedOrientation.Ports,
                         0,
                         neighbors,
                         availableOrientations,
                         branch))
            {
                var completedState = connectedState.Copy();
                completedState.VisitingCells.Remove(cell);
                completedState.ProcessedCells.Add(cell);
                yield return completedState;
            }

            yield break;
        }

        foreach (var orientation in availableOrientations[cell])
        {
            if (requiredPort.HasValue && !orientation.HasPort(requiredPort.Value))
                continue;

            var branch = state.Copy();
            branch.Orientations.Add(cell, orientation);
            branch.ActiveCells.Add(cell);
            branch.VisitingCells.Add(cell);

            foreach (var connectedState in ConnectPorts(
                         cell,
                         orientation.Ports,
                         0,
                         neighbors,
                         availableOrientations,
                         branch))
            {
                var completedState = connectedState.Copy();
                completedState.VisitingCells.Remove(cell);
                completedState.ProcessedCells.Add(cell);
                yield return completedState;
            }
        }
    }

    private static IEnumerable<SearchState> ConnectPorts(
        WireCell cell,
        IReadOnlyList<int> ports,
        int portIndex,
        Dictionary<WireCell, Dictionary<int, WireCell>> neighbors,
        Dictionary<WireCell, List<Orientation>> availableOrientations,
        SearchState state)
    {
        if (portIndex == ports.Count)
        {
            yield return state;
            yield break;
        }

        var port = ports[portIndex];
        if (!neighbors[cell].TryGetValue(port, out var neighbor))
            yield break;

        foreach (var connectedNeighbor in ConnectCell(
                     neighbor,
                     NormalizeAngle(port + 180),
                     neighbors,
                     availableOrientations,
                     state))
        {
            foreach (var result in ConnectPorts(
                         cell,
                         ports,
                         portIndex + 1,
                         neighbors,
                         availableOrientations,
                         connectedNeighbor))
            {
                yield return result;
            }
        }
    }

    private static Dictionary<WireCell, Dictionary<int, WireCell>> BuildNeighbors(
        IReadOnlyList<WireCell> cells)
    {
        var neighbors = new Dictionary<WireCell, Dictionary<int, WireCell>>();
        foreach (var cell in cells)
        {
            var cellNeighbors = new Dictionary<int, WireCell>();
            foreach (var angle in GetBoardAngles(cell.ShapeType))
            {
                var direction = WireSystem.GetDirection(angle, 1f, GetHeight(cell.ShapeType)) * 2f;
                var expectedPosition = (Vector2)cell.Position + direction;
                var match = cells
                    .Where(candidate => candidate != cell)
                    .Select(candidate => new
                    {
                        Cell = candidate,
                        Distance = Vector2.Distance((Vector2)candidate.Position, expectedPosition)
                    })
                    .Where(candidate => candidate.Distance <= NeighborPositionTolerance)
                    .OrderBy(candidate => candidate.Distance)
                    .FirstOrDefault();

                if (match != null)
                    cellNeighbors.Add(angle, match.Cell);
            }

            neighbors.Add(cell, cellNeighbors);
        }

        return neighbors;
    }

    private static List<Orientation> GetOrientations(WireCell cell)
    {
        var sideCount = cell.ShapeType == ShapeType.Rect ? 4 : 6;
        var rotationCount = cell.IsClickable ? sideCount : 1;
        var orientations = new List<Orientation>();
        for (var rotation = 0; rotation < rotationCount; rotation++)
        {
            var rotationAngle = rotation * 360 / sideCount;
            var orientation = new Orientation(
                cell.OutputAngles.Select(angle => NormalizeAngle(angle + rotationAngle)));
            if (!orientations.Any(existing => existing.Ports.SequenceEqual(orientation.Ports)))
                orientations.Add(orientation);
        }

        return orientations;
    }

    private static IEnumerable<int> GetBoardAngles(ShapeType shapeType)
    {
        var sideCount = shapeType == ShapeType.Rect ? 4 : 6;
        var firstAngle = shapeType == ShapeType.Rect ? 0 : 30;
        for (var side = 0; side < sideCount; side++)
            yield return firstAngle + side * 360 / sideCount;
    }

    private static int[] NormalizeAngles(IEnumerable<int> angles)
    {
        return angles.Select(NormalizeAngle).OrderBy(angle => angle).ToArray();
    }

    private static int NormalizeAngle(int angle)
    {
        return (angle % 360 + 360) % 360;
    }

    private static float GetHeight(ShapeType shapeType)
    {
        return shapeType == ShapeType.Rect ? 1f : 0.8659766f;
    }

    private static bool ArrayContains(int[] values, int value)
    {
        for (var index = 0; index < values.Length; index++)
        {
            if (values[index] == value)
                return true;
        }

        return false;
    }
}
