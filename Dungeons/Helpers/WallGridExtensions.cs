using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;

namespace TerrainGenerators.Helpers
{
    public static class WallGridExtensions
    {
        /// <returns>Returns true for positions outside the grid, else returns wallsGrid[x, y]</returns>
        public static bool IsWall(this bool[,] wallsGrid, int x, int y)
        {
            if(x < 0 || y < 0 || x >= wallsGrid.GetLength(0) || y >= wallsGrid.GetLength(1))
                return true;
            return wallsGrid[x, y];
        }

        public static void SetWall(this bool[,] wallsGrid, int x, int y, bool isWall = true)
        {
            if (x < 0 || y < 0 || x >= wallsGrid.GetLength(0) || y >= wallsGrid.GetLength(1))
                TerrainGenerators.Log($"Warning: Tried to set out of bounds wall position ({x},{y})");
            else
                wallsGrid[x, y] = isWall;
        }
    }
}
