using System;
using System.Collections.Generic;
using UnityEngine;

namespace WorldBuilder.Gameplay.Core
{
    public readonly struct GameplayEventRecord
    {
        public GameplayEventRecord(float time, string category, string sourceId, string detail)
        {
            Time = time;
            Category = category;
            SourceId = sourceId;
            Detail = detail;
        }

        public float Time { get; }
        public string Category { get; }
        public string SourceId { get; }
        public string Detail { get; }
    }

    public static class GameplayEventLog
    {
        private const int Capacity = 64;
        private static readonly Queue<GameplayEventRecord> Records = new Queue<GameplayEventRecord>(Capacity);

        public static event Action<GameplayEventRecord> Published;

        public static IReadOnlyCollection<GameplayEventRecord> Recent => Records;

        public static void Publish(string category, GameObject source, string detail)
        {
            StableId stableId = source != null ? source.GetComponentInParent<StableId>() : null;
            string sourceId = stableId != null ? stableId.Value : source != null ? source.name : "system";
            GameplayEventRecord record = new GameplayEventRecord(UnityEngine.Time.time, category, sourceId, detail);

            if (Records.Count >= Capacity)
            {
                Records.Dequeue();
            }

            Records.Enqueue(record);
            Published?.Invoke(record);
        }

        public static void Clear()
        {
            Records.Clear();
        }
    }
}
