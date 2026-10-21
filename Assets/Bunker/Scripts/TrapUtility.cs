using System;
using System.Collections.Generic;

namespace BunkerAI
{
    // Pure C#: no Unity objects, physics, rendering or input in the decision model.
    public struct CellKey : IEquatable<CellKey>
    {
        public int X, Z;
        public CellKey(int x, int z) { X = x; Z = z; }
        public bool Equals(CellKey other) { return X == other.X && Z == other.Z; }
        public override bool Equals(object other) { return other is CellKey && Equals((CellKey)other); }
        public override int GetHashCode() { unchecked { return X * 397 ^ Z; } }
        public override string ToString() { return X + "," + Z; }
    }

    public sealed class VisitMemory
    {
        public CellKey Key;
        public float X, Z;
        public int Entries;
        public double Heat, LastVisit, BlockedUntil;
    }

    public sealed class PathMemory
    {
        public const int MaximumCells = 512;
        public readonly Dictionary<CellKey, VisitMemory> Cells = new Dictionary<CellKey, VisitMemory>();
        private CellKey previous;
        private bool hasPrevious;
        public double HalfLife = 45;
        public float CellSize = 2;

        public double HeatAt(VisitMemory cell, double now)
        {
            return cell.Heat * Math.Pow(0.5, Math.Max(0, now - cell.LastVisit) / Math.Max(1, HalfLife));
        }

        // Count re-entry, not frames spent standing on a tile. A short debounce
        // prevents boundary jitter from artificially boosting a location.
        public void Observe(float x, float z, double now)
        {
            CellKey key = new CellKey((int)Math.Floor(x / CellSize), (int)Math.Floor(z / CellSize));
            if (hasPrevious && key.Equals(previous)) return;
            previous = key; hasPrevious = true;
            VisitMemory cell;
            if (!Cells.TryGetValue(key, out cell))
            {
                if (Cells.Count >= MaximumCells)
                {
                    VisitMemory oldest = null;
                    foreach (VisitMemory entry in Cells.Values)
                        if (oldest == null || entry.LastVisit < oldest.LastVisit) oldest = entry;
                    Cells.Remove(oldest.Key);
                }
                cell = new VisitMemory { Key = key, X = x, Z = z, LastVisit = now - 2 };
                Cells.Add(key, cell);
            }
            if (now - cell.LastVisit < 1.5) return;
            cell.Heat = Math.Min(6, HeatAt(cell, now) + 1);
            cell.LastVisit = now; cell.Entries++;
        }

        public void Clear() { Cells.Clear(); hasPrevious = false; }
    }

    public struct UtilityBreakdown
    {
        public double Heat, Recency, Proximity;
        public double Total { get { return 0.65 * Heat + 0.25 * Recency + 0.10 * Proximity; } }
    }

    public enum TrapAction { Wait, Place, Relocate }

    public static class TrapUtility
    {
        public const double MinimumScore = 0.50;
        public const double RelocationMargin = 0.18;
        public static UtilityBreakdown Score(PathMemory memory, VisitMemory cell, double now, double distance)
        {
            return new UtilityBreakdown {
                Heat = Math.Min(1, memory.HeatAt(cell, now) / 4),
                Recency = Math.Pow(0.5, Math.Max(0, now - cell.LastVisit) / Math.Max(1, memory.HalfLife)),
                Proximity = Math.Max(0, Math.Min(1, 1 - distance / 20))
            };
        }
        public static bool HasEvidence(PathMemory memory, VisitMemory cell, double now)
        {
            return cell.Entries >= 2 && memory.HeatAt(cell, now) >= 1.25 && now >= cell.BlockedUntil;
        }
        public static TrapAction Choose(double bestScore, int active, int capacity, bool cooldownReady, double weakestScore)
        {
            if (!cooldownReady || capacity <= 0 || bestScore < MinimumScore) return TrapAction.Wait;
            if (active < capacity) return TrapAction.Place;
            if (!double.IsInfinity(weakestScore) && bestScore > weakestScore + RelocationMargin) return TrapAction.Relocate;
            return TrapAction.Wait;
        }
    }
}
