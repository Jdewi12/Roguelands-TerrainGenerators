using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace TerrainGenerators.Helpers
{
    /// <summary>
    /// An incomplete implementation. May add other helpers like Clamp and RoundToInt as needed.
    /// </summary>
    public struct Vector2Int
    {
#pragma warning disable IDE1006 // Naming Styles. Matching Unity's naming
        public int x;
        public int y;

        public Vector2Int(int x, int y)
        {
            this.x = x;
            this.y = y;
        }

        public float magnitude => Mathf.Sqrt(magnitude);
        public float sqrMagnitude => x * x + y * y;
        public Vector2Int normalizedInt => new Vector2Int(x == 0 ? 0 : x > 0 ? 1 : -1, y == 0 ? 0 : y > 0 ? 1 : -1);

        public int this[int index] => index == 0 ? x : index == 1 ? y : throw new IndexOutOfRangeException("Index must be 0 for x or 1 for y.");
        public bool Equals(Vector2Int other) => other.x == x && other.y == y;

        public static Vector2Int down => new Vector2Int(0, -1);
        public static Vector2Int left => new Vector2Int(-1, 0);
        public static Vector2Int up => new Vector2Int(0, 1);
        public static Vector2Int right => new Vector2Int(1, 0);
        public static Vector2Int one => new Vector2Int(1, 1);
        public static Vector2Int zero => new Vector2Int(0, 0);
        public static Vector2Int operator +(Vector2Int left, Vector2Int right) => new Vector2Int(left.x + right.x, left.y + right.y);
        public static Vector2Int operator -(Vector2Int left, Vector2Int right) => new Vector2Int(left.x - right.x, left.y - right.y);
        public static Vector2Int operator *(Vector2Int left, int right) => new Vector2Int(left.x * right, left.y * right);
        public static Vector2Int operator /(Vector2Int left, int right) => new Vector2Int(left.x / right, left.y / right); // int division
        public static Vector2 operator /(Vector2Int left, float right) => new Vector2(left.x / right, left.y / right);
        public static Vector2Int operator *(Vector2Int left, Vector2Int right) => new Vector2Int(left.x * right.x, left.y * right.y);
        public static implicit operator Vector2(Vector2Int vec) => new Vector2(vec.x, vec.y);
        public static implicit operator Vector3(Vector2Int vec) => new Vector3(vec.x, vec.y);
        public static float Distance(Vector2Int vec1, Vector2Int vec2)
        {
            return Mathf.Sqrt(SqrDistance(vec1, vec2));
        }
        public static float SqrDistance(Vector2Int vec1, Vector2Int vec2)
        {
            
            int diffX = vec1.x - vec2.x;
            int diffY = vec1.y - vec2.y;
            return diffX * diffX + diffY * diffY;
        }
#pragma warning restore IDE1006 // Naming Styles
    }

    public static class Vector2Extensions
    {
        public static Vector2Int RoundToInt(this Vector2 vec)
        {
            return new Vector2Int(Mathf.RoundToInt(vec.x), Mathf.RoundToInt(vec.y));
        }
    }
}
