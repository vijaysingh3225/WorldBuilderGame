using System;
using UnityEngine;

namespace WorldBuilder.Gameplay.WeaponGrid
{
    /// <summary>
    /// Serializable integer coordinate used by weapon grids and artifact shapes.
    /// </summary>
    [Serializable]
    public struct GridCoordinate : IEquatable<GridCoordinate>
    {
        [SerializeField] private int x;
        [SerializeField] private int y;

        public GridCoordinate(int x, int y)
        {
            this.x = x;
            this.y = y;
        }

        public int X => x;
        public int Y => y;

        public static GridCoordinate Root => new GridCoordinate(0, 0);

        public static GridCoordinate operator +(GridCoordinate left, GridCoordinate right)
        {
            return new GridCoordinate(left.x + right.x, left.y + right.y);
        }

        public static GridCoordinate operator -(GridCoordinate left, GridCoordinate right)
        {
            return new GridCoordinate(left.x - right.x, left.y - right.y);
        }

        public GridCoordinate RotateClockwise(int quarterTurns)
        {
            int turns = NormalizeRotation(quarterTurns);
            switch (turns)
            {
                case 1:
                    return new GridCoordinate(y, -x);
                case 2:
                    return new GridCoordinate(-x, -y);
                case 3:
                    return new GridCoordinate(-y, x);
                default:
                    return this;
            }
        }

        public bool Equals(GridCoordinate other)
        {
            return x == other.x && y == other.y;
        }

        public override bool Equals(object obj)
        {
            return obj is GridCoordinate other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (x * 397) ^ y;
            }
        }

        public override string ToString()
        {
            return $"({x}, {y})";
        }

        public static bool operator ==(GridCoordinate left, GridCoordinate right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(GridCoordinate left, GridCoordinate right)
        {
            return !left.Equals(right);
        }

        public static int NormalizeRotation(int quarterTurns)
        {
            int normalized = quarterTurns % 4;
            return normalized < 0 ? normalized + 4 : normalized;
        }
    }
}
