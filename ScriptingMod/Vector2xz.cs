using System;
using System.Diagnostics.CodeAnalysis;

namespace ScriptingMod
{
    /// <summary>
    /// Same as Vector2i, but instead of "y" it's "z", because that is the actual 3D equivalent everywhere.
    /// Examples: chunkXY, areaXZ, etc.
    /// </summary>
    [Obsolete("Use Vector2i or Vector3i instead for simplification")]
    public struct Vector2xz : IEquatable<Vector2xz>
    {
        public static readonly Vector2xz None = new Vector2xz(int.MinValue, int.MinValue);

        public int x;
        public int z;

        public Vector2xz(int x, int z)
        {
            this.x = x;
            this.z = z;
        }

        public static bool operator ==(Vector2xz one, Vector2xz other)
        {
            if (one.x == other.x)
                return one.z == other.z;
            return false;
        }

        public static bool operator !=(Vector2xz one, Vector2xz other)
        {
            return !(one == other);
        }

        public static Vector2i operator +(Vector2xz one, Vector2xz other)
        {
            return new Vector2i(one.x + other.x, one.z + other.z);
        }

        public static Vector2i operator -(Vector2xz one, Vector2xz other)
        {
            return new Vector2i(one.x - other.x, one.z - other.z);
        }

        public void Set(int x, int z)
        {
            this.x = x;
            this.z = z;
        }

        public override bool Equals(object obj)
        {
            return obj != null && Equals((Vector2xz) obj);
        }

        public bool Equals(Vector2xz other)
        {
            if (other.x == x)
                return other.z == z;
            return false;
        }

        [SuppressMessage("ReSharper", "NonReadonlyMemberInGetHashCode")]
        public override int GetHashCode()
        {
            return x * 8976890 + z * 981131;
        }

        public override string ToString()
        {
            return string.Format($"{x}, {z}");
        }
    }
}