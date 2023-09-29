using System;
using System.Text;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
//using UnityEngine;

namespace RuleOfCoolStudios
{
    /// <summary>
    /// Simple reference type for a grid point for internal use. Includes translations for hex based
    /// grid points based on: https://www.redblobgames.com/grids/hexagons/
    /// Unity's convention for tile mapping: odd-R; Cube/Axial Coordinates are provided (Q, R, S).
    /// As big as this struct is, it only contains two actual members, X and Y.
    /// </summary>
    public struct GridPoint : IEquatable<GridPoint>
    {
        public readonly int X { get; }

        public readonly int Y { get; }

        /// <summary>
        /// Gets the vector3 int coordinates (with z = 0). Uncomment to use in Unity.
        /// </summary>
        //public Vector3Int Vector3IntCoordinates => new Vector3Int(X, Y, 0);

        /// <summary>
        /// If searching for paths or radius greater than this number, the methods will return default values.
        /// </summary>
        public const int MaxSearchRange = 50;

        /// <summary>
        /// Each Hex GridPoint belongs to one and only one "chunk". We're going to use this approach to determine
        /// which chunk each tile belongs to. We could cache this value or calculate it on demand. The math
        /// comes from https://observablehq.com/@sanderevers/hexagon-tiling-of-an-hexagonal-grid
        /// Radius 0 is "1 tile", radius 1 is 7 tiles, etc.
        /// </summary>
        public const int HexChunkRadius = 10;

        /// <summary>
        /// Convenience array for list allocation of the proper size (typically before calling GetHexesInRange).
        /// The index of this array is the radius and contains the number of tiles in a chunk of that size
        /// ([0] = 1, [1] = 7, etc). If you need more values, either add them to this array or use
        /// CountHexTilesInRadius().
        /// </summary>
        public static readonly int[] TilesInHexChunkOfRadius =
        {
            1, 7, 19, 37, 61, 91, 127, 169, 217, 271, //0-9
            331, 397, 469, 547, 631, 721, 817, 919, 1027, 1141, //10-19
            1261, 1387, 1519, 1657, 1801, 1951, 2107, 2269, 2437, 2611, //20-29
            2791, 2977, 3169, 3367, 3571, 3781, 3997, 4219, 4447, 4681, //30-39
            4921, 5167, 5419, 5677, 5941, 6211, 6487, 6769, 7057, 7351, // 40-49
            7651, //50
        };

        public static int CountHexTilesInRadius(int radius) => (radius * radius * 3) + (3 * radius) + 1;

        // Cube/Axial coordinates
        public int Q => X - ((Y - (Y & 1)) / 2); // Positive to the right or upright

        public int R => Y; // Positive to the downright or downleft

        public int S => 0 - Q - R; // Positive to the left or upleft

        /// <summary>
        /// Construct a point with hex (cube/axial) coordinates.
        /// S must be included as two integers matches the signature of offset constructor: (x, y).
        /// </summary>
        public GridPoint(int q, int r, int s)
        {
            if (s + r + q != 0) throw new ArgumentException($"Invalid S provided to HexGridPoint constructor. (q:{q}, r:{r}, s:{s}).");
            X = q + ((r - (r & 1)) / 2); // yeh.
            Y = r;
        }

        public GridPoint(int x, int y) { X = x; Y = y; }

        public GridPoint(GridPoint p) { X = p.X; Y = p.Y; }

        public bool Equals(GridPoint p) => X == p.X && Y == p.Y;

        public override bool Equals(object obj) => obj is GridPoint other && Equals(other);

        public static bool operator ==(GridPoint lhs, GridPoint rhs) => lhs.Equals(rhs);

        public static bool operator !=(GridPoint lhs, GridPoint rhs) => !(lhs == rhs);

        public override int GetHashCode() => (X, Y).GetHashCode();

        // Hex adjacency using hex coordinate constructor
        public GridPoint UpRightHex => new(Q + 1, R - 1, S);
        public GridPoint UpLeftHex => new(Q, R - 1, S + 1);
        public GridPoint DownLeftHex => new(Q - 1, R + 1, S);
        public GridPoint DownRightHex => new(Q, R + 1, S - 1);
        public GridPoint RightHex => new(Q + 1, R, S - 1);
        public GridPoint LeftHex => new(Q - 1, R, S + 1);

        // Cartesian adjacency
        public GridPoint Up => new(X, Y + 1);
        public GridPoint Down => new(X, Y - 1);
        public GridPoint Right => new(X + 1, Y);
        public GridPoint Left => new(X - 1, Y);

        // Fixed locations
        public static GridPoint Origin => new(0, 0);

        /// <summary>
        /// Get hexes within range (0 to MaxSearchRange) and append results to output list. List should be empty
        /// and new()'d outside of this method (for performance reasons). Method will return the input list
        /// if range is outside of valid range. Range 0 returns 1 tile, Range 1 returns 7 tiles, Range 2
        /// returns 19 tiles, etc.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public List<GridPoint> GetHexesInRange(int range, List<GridPoint> resultList)
        {
            if (range < 0) return resultList;
            if (range > MaxSearchRange) return resultList;
            int negativeRange = range * -1;
            for (int q = negativeRange; q <= range; q++)
            {
                int min = Math.Max(negativeRange, 0 - q - range);
                int max = Math.Min(range, 0 - q + range);
                for (int r = min; r <= max; r++)
                {
                    GridPoint newPoint = new(Q + q, R + r, S - q - r);
                    resultList.Add(newPoint);
                }
            }
            return resultList;
        }

        /// <summary>
        /// Integer distance by "hexwalking" to another gridpoint (ie, UpLeft, UpRight, UpLeft, UpRight).
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int HexDistanceTo(GridPoint dest)
        {
            int q = Math.Abs(Q - dest.Q);
            int r = Math.Abs(R - dest.R);
            int s = Math.Abs(S - dest.S);
            return (q + r + s) / 2;
        }

        /// <summary>
        /// Returns the HEX CHUNK COORDINATE of the tile, not the tile coordinate! This can
        /// be used for biome generation and chunk caching, but not pathing or individual
        /// tile drawing. For example, all of the tiles within "HexChunkRadius" of the origin
        /// will have the same HexChunk.
        /// Details: https://observablehq.com/@sanderevers/hexagon-tiling-of-an-hexagonal-grid
        /// Since the chunk coordinate is kind of it's own system ("strips"), it's nontrivial
        /// to find the center of a chunk with the chunk coordinates, so it's best to
        /// manually find the center of a chunk from the known center of adjacent chunks.
        /// Each HexChunk is guaranteed to contain exactly GridPoint.TilesInHexChunkOfRadius[HexChunkRadius]
        /// tiles, and each tile belongs to exactly one HexChunk.
        /// </summary>
        public GridPoint HexChunk => GetHexChunk(this);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static GridPoint GetHexChunk(GridPoint src)
        {
            // black GD magic
            const int _chunkArea = (HexChunkRadius * HexChunkRadius * 3) + (3 * HexChunkRadius) + 1;
            const int _chunkShift = (3 * HexChunkRadius) + 2;
            double tmpQ = Math.Floor((double)(src.R + (_chunkShift * src.Q)) / _chunkArea);
            double tmpR = Math.Floor((double)(src.S + (_chunkShift * src.R)) / _chunkArea);
            double tmpS = Math.Floor((double)(src.Q + (_chunkShift * src.S)) / _chunkArea);
            int q = (int)Math.Floor((1 + tmpQ - tmpR) / 3);
            int r = (int)Math.Floor((1 + tmpR - tmpS) / 3);
            int s = (int)Math.Floor((1 + tmpS - tmpQ) / 3);
            return new(q, r, s);
        }

        /// <summary>
        /// (X,Y)
        /// </summary>
        public override string ToString() => ToString(true);

        /// <summary>
        /// (X,Y) for cartesian coordinates, (Q,R,S) for hex coordinates. Note that Unity will still
        /// use cartesian coordinates for hex based Tile Grids (using "odd-R" convention - odd rows are
        /// shoved left by one half unit).
        /// </summary>
        public string ToString(bool isCartesian)
        {
            StringBuilder sb = new(7);
            sb.Append('(');
            if (isCartesian)
            {
                sb.Append(X);
                sb.Append(",");
                sb.Append(Y);
            }
            else
            {
                sb.Append(Q);
                sb.Append(",");
                sb.Append(R);
                sb.Append(",");
                sb.Append(S);
            }
            sb.Append(')');
            return sb.ToString();
        }
    }
}
