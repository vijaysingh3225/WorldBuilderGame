using System;
using System.Collections.Generic;

namespace WorldBuilder.Gameplay.WeaponGrid
{
    public static class ArtifactPatternResolver
    {
        public static IReadOnlyList<string> ResolveCompleted(
            WeaponGridState state,
            IReadOnlyList<ArtifactDefinitionData> definitions)
        {
            var completed = new List<string>();
            if (state == null || state.Placements.Count < 2 || definitions == null)
            {
                return completed;
            }

            var catalog = new Dictionary<string, ArtifactDefinitionData>(
                StringComparer.Ordinal);
            for (int index = 0; index < definitions.Count; index++)
            {
                ArtifactDefinitionData definition = definitions[index];
                if (definition != null)
                {
                    catalog[definition.DefinitionId] = definition;
                }
            }

            var links = BuildLinks(state, catalog);
            AddStatLink(
                completed,
                "EDGE CHAIN",
                ArtifactStat.Damage,
                state,
                catalog,
                links);
            AddStatLink(
                completed,
                "BASTION LINK",
                ArtifactStat.MaxHealth,
                state,
                catalog,
                links);
            AddStatLink(
                completed,
                "GALE CIRCUIT",
                ArtifactStat.MoveSpeed,
                state,
                catalog,
                links);

            if (ContainsConnectedTriune(state, catalog, links))
            {
                completed.Add("TRIUNE WEAVE");
            }
            return completed;
        }

        private static void AddStatLink(
            List<string> completed,
            string name,
            ArtifactStat stat,
            WeaponGridState state,
            IReadOnlyDictionary<string, ArtifactDefinitionData> catalog,
            bool[,] links)
        {
            for (int left = 0; left < state.Placements.Count; left++)
            {
                if (!HasStat(state.Placements[left], stat, catalog))
                {
                    continue;
                }
                for (int right = left + 1; right < state.Placements.Count; right++)
                {
                    if (links[left, right] &&
                        HasStat(state.Placements[right], stat, catalog))
                    {
                        completed.Add(name);
                        return;
                    }
                }
            }
        }

        private static bool ContainsConnectedTriune(
            WeaponGridState state,
            IReadOnlyDictionary<string, ArtifactDefinitionData> catalog,
            bool[,] links)
        {
            for (int start = 0; start < state.Placements.Count; start++)
            {
                var visited = new HashSet<int>();
                var pending = new Queue<int>();
                var stats = new HashSet<ArtifactStat>();
                pending.Enqueue(start);
                while (pending.Count > 0)
                {
                    int current = pending.Dequeue();
                    if (!visited.Add(current))
                    {
                        continue;
                    }
                    AddStats(state.Placements[current], catalog, stats);
                    for (int next = 0; next < state.Placements.Count; next++)
                    {
                        if (links[current, next] && !visited.Contains(next))
                        {
                            pending.Enqueue(next);
                        }
                    }
                }
                if (stats.Contains(ArtifactStat.Damage) &&
                    stats.Contains(ArtifactStat.MaxHealth) &&
                    stats.Contains(ArtifactStat.MoveSpeed))
                {
                    return true;
                }
            }
            return false;
        }

        private static bool[,] BuildLinks(
            WeaponGridState state,
            IReadOnlyDictionary<string, ArtifactDefinitionData> catalog)
        {
            int count = state.Placements.Count;
            var links = new bool[count, count];
            var cells = new List<HashSet<GridCoordinate>>(count);
            for (int index = 0; index < count; index++)
            {
                var occupied = new HashSet<GridCoordinate>();
                ArtifactPlacement placement = state.Placements[index];
                if (catalog.TryGetValue(
                        placement.Artifact.DefinitionId,
                        out ArtifactDefinitionData definition))
                {
                    foreach (GridCoordinate cell in placement.OccupiedCells(definition))
                    {
                        occupied.Add(cell);
                    }
                }
                cells.Add(occupied);
            }
            GridCoordinate[] directions =
            {
                new GridCoordinate(1, 0), new GridCoordinate(-1, 0),
                new GridCoordinate(0, 1), new GridCoordinate(0, -1)
            };
            for (int left = 0; left < count; left++)
            {
                for (int right = left + 1; right < count; right++)
                {
                    bool touching = false;
                    foreach (GridCoordinate cell in cells[left])
                    {
                        for (int direction = 0; direction < directions.Length; direction++)
                        {
                            if (cells[right].Contains(cell + directions[direction]))
                            {
                                touching = true;
                                break;
                            }
                        }
                        if (touching)
                        {
                            break;
                        }
                    }
                    links[left, right] = touching;
                    links[right, left] = touching;
                }
            }
            return links;
        }

        private static bool HasStat(
            ArtifactPlacement placement,
            ArtifactStat stat,
            IReadOnlyDictionary<string, ArtifactDefinitionData> catalog)
        {
            if (!catalog.TryGetValue(
                    placement.Artifact.DefinitionId,
                    out ArtifactDefinitionData definition))
            {
                return false;
            }
            for (int index = 0; index < definition.Modifiers.Count; index++)
            {
                if (definition.Modifiers[index].Stat == stat)
                {
                    return true;
                }
            }
            return false;
        }

        private static void AddStats(
            ArtifactPlacement placement,
            IReadOnlyDictionary<string, ArtifactDefinitionData> catalog,
            ISet<ArtifactStat> stats)
        {
            if (!catalog.TryGetValue(
                    placement.Artifact.DefinitionId,
                    out ArtifactDefinitionData definition))
            {
                return;
            }
            for (int index = 0; index < definition.Modifiers.Count; index++)
            {
                stats.Add(definition.Modifiers[index].Stat);
            }
        }
    }
}
