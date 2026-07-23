using System;
using System.Collections.Generic;
using UnityEngine;

namespace WorldBuilder.Gameplay.Core
{
    public readonly struct GameplayEventRecord
    {
        public GameplayEventRecord(
            long sequence,
            int frame,
            float time,
            float realtime,
            string category,
            string sourceId,
            string detail)
        {
            Sequence = sequence;
            Frame = frame;
            Time = time;
            Realtime = realtime;
            Category = category;
            SourceId = sourceId;
            Detail = detail;
        }

        public long Sequence { get; }
        public int Frame { get; }
        public float Time { get; }
        public float Realtime { get; }
        public string Category { get; }
        public string SourceId { get; }
        public string Detail { get; }
    }

    public static class GameplayEventLog
    {
        private const int Capacity = 256;
        private static readonly Queue<GameplayEventRecord> Records = new Queue<GameplayEventRecord>(Capacity);
        private static long nextSequence = 1;

        public static event Action<GameplayEventRecord> Published;

        public static IReadOnlyCollection<GameplayEventRecord> Recent => Records;

        public static void Publish(string category, GameObject source, string detail)
        {
            StableId stableId = source != null ? source.GetComponentInParent<StableId>() : null;
            string sourceId = stableId != null ? stableId.Value : source != null ? source.name : "system";
            GameplayEventRecord record = new GameplayEventRecord(
                nextSequence++,
                UnityEngine.Time.frameCount,
                UnityEngine.Time.time,
                UnityEngine.Time.realtimeSinceStartup,
                category,
                sourceId,
                detail);

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
            nextSequence = 1;
        }
    }
}
