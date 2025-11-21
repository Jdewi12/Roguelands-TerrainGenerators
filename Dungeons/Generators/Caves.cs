using GadgetCore.API;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml.Linq;
using TerrainGenerators.Helpers;
using TerrainGenerators.Patches;
using UnityEngine;

namespace TerrainGenerators.Generators
{
    public class Caves : GeneratorBase
    {
        // todo: Ceiling/wall spawns?
        public override Vector2Int PlayerSpawn => playerSpawn;
        private Vector2Int playerSpawn;
        public override Color MinimapWallsColor => minimapWallsColor;
        private Color minimapWallsColor;
        public override int GridWidth => gridWidth;
        private int gridWidth;
        public override int GridHeight => gridHeight;
        private int gridHeight;
        public override bool[,] WallsGrid => wallsGrid;
        bool[,] wallsGrid;


        public Caves(Color? minimapWallsColor = null, int gridWidth = 26, int gridHeight = 16)
        {
            if (minimapWallsColor != null)
                this.minimapWallsColor = minimapWallsColor.Value;
            else
                this.minimapWallsColor = GetMinimapColor();
            this.gridWidth = gridWidth;
            this.gridHeight = gridHeight;
        }

        public override void GenerateWalls(RNG rng)
        {
            int numRooms = Mathf.RoundToInt((GridWidth / 8f) * (GridHeight / 8f) + rng.Next(0, 3)); // RNG.Next is upper bounds exclusive for ints
            if (numRooms <= 1)
                numRooms = 2;
            float maxRadius = Mathf.Clamp(Mathf.Min(GridWidth, GridHeight) - 2, min: 1, max: 4);
            float maxRadiusSqrt = Mathf.Sqrt(maxRadius);

            List<OneWayGridNode> unconnectedRooms = new List<OneWayGridNode>();
            for (int i = 0; i < numRooms; i++)
            {
                // x^2 distribution; makes large radius rare
                int radius = Mathf.RoundToInt(Mathf.Pow(rng.Next(1f, maxRadiusSqrt), 2));
                // pick a random x/y position while making sure we don't go outside with the selected radius
                unconnectedRooms.Add(new OneWayGridNode(new Vector2Int(
                    x: rng.Next(radius, GridWidth - 1 - radius),
                    y: rng.Next(radius, GridHeight - 1 - radius)),
                    radius));

            }

            List<OneWayGridNode> connectedRooms = new List<OneWayGridNode>()
            {
                unconnectedRooms[0] // pick the first room as the starting point
            };
            unconnectedRooms.RemoveAt(0);

            while (unconnectedRooms.Count > 0)// && loopsAllowed > 0)
            {
                OneWayGridNode nearestUnconnected = null;
                OneWayGridNode nearestConnected = null;
                float minDistance = float.MaxValue;

                // Find closest unconnected room to any connected room
                foreach (OneWayGridNode unconnectedRoom in unconnectedRooms)
                {
                    foreach (OneWayGridNode connectedRoom in connectedRooms)
                    {
                        float distance = Vector2.Distance(unconnectedRoom.Position, connectedRoom.Position);
                        if (distance < minDistance)
                        {
                            minDistance = distance;
                            nearestUnconnected = unconnectedRoom;
                            nearestConnected = connectedRoom;
                        }
                    }
                }
                // we only need a one way connection to draw a tunnel and there will only ever be one parent per node with this algorithm
                nearestUnconnected.Parent = nearestConnected; 
                connectedRooms.Add(nearestUnconnected);
                unconnectedRooms.Remove(nearestUnconnected);
            }
            wallsGrid = new bool[GridWidth, GridHeight];
            for (int x = 0; x < GridWidth; x++)
                for (int y = 0; y < GridHeight; y++)
                    wallsGrid[x, y] = true;

            // draw the rooms and tunnels
            foreach(OneWayGridNode room in connectedRooms)
            {
                Cave(room.Position, room.Radius, ref wallsGrid);
                if (room.Parent != null)
                    Tunnel(room.Position, room.Parent.Position, room.Radius, room.Parent.Radius, ref wallsGrid);
            }

            // pick the left-most top position as the player's spawn
            bool done = false;
            for (int y = gridHeight - 1; y >= 1 && !done; y--)
            {
                for (int x = 0; x < GridWidth; x++)
                {
                
                    if(wallsGrid[x, y] == false && wallsGrid[x, y - 1] == true)
                    {
                        playerSpawn = new Vector2Int(x, y);
                        // move the spawn over towards the closest side of the map
                        int xDirFromCenter = x >= GridWidth / 2 ? 1 : -1;
                        while (!wallsGrid.IsWall(x + xDirFromCenter, y) && wallsGrid.IsWall(x + xDirFromCenter, y - 1)) 
                            x += xDirFromCenter;

                        playerSpawn.x = x;

                        done = true;
                        break;
                    }
                }
            }
            CreateWalls(wallsGrid);
            CreateMinimapIfPresent(wallsGrid);
        }

        public override void PopulateLevel(RNG rng)
        {
            InstanceTracker.GameScript.StartCoroutine(SpawnVanillaSpawnables(rng, wallsGrid));

        }

        public static void Cave(Vector2Int position, int radius, ref bool[,] grid)
        {
            int radiusSquared = (int)Mathf.Pow(radius, 2);
            for (int i = -radius; i <= radius; i++)
                for (int j = -radius; j <= radius; j++)
                {
                    if (Mathf.Pow(i, 2) + Mathf.Pow(j, 2) < radiusSquared)
                    {
                        grid[position.x + i, position.y + j] = false;
                    }
                }
        }

        public static void Tunnel(Vector2Int position1, Vector2Int position2, int radius1, int radius2, ref bool[,] grid)
        {
            if (radius1 <= 0 && radius2 <= 0)
                return;
            int x1 = position1.x;
            int y1 = position1.y;
            int x2 = position2.x;
            int y2 = position2.y;
            int thickness = (radius1 + radius2) / 2;
            if (thickness < 1)
                thickness = 1;

            int xLen = x2 - x1;
            int yLen = y2 - y1;
            int dirX = xLen >= 0 ? -1 : 1;
            int dirY = yLen >= 0 ? -1 : 1;
            if (xLen < 0)
                xLen = -xLen;
            if (yLen < 0)
                yLen = -yLen;

            int primaryLength; // The primary axis length
            int stepOuterX0;
            int stepOuterY0;
            int stepOuterX1;
            int stepOuterY1;

            int stepInnerX0;
            int stepInnerY0;
            int stepInnerX1 = dirX;
            int stepInnerY1 = dirY;

            int errorStepX = 2 * xLen;
            int errorStepY = 2 * yLen;
            int errorStepDiagonal;

            int errorThreshold;
            
            if (xLen > yLen) // line is primary horizontal
            {
                // Outer loop steps
                primaryLength = xLen;
                stepOuterX0 = 0;
                stepOuterY0 = dirY;
                stepOuterX1 = -dirX;
                stepOuterY1 = dirY;

                // Inner loop steps
                stepInnerX0 = dirX;
                stepInnerY0 = 0;
                errorStepDiagonal = errorStepY - errorStepX;

                errorThreshold = xLen - errorStepY;
            }
            else // line is primarily vertical
            {
                primaryLength = yLen;
                stepOuterX0 = dirX;
                stepOuterY0 = 0;
                stepOuterX1 = dirX;
                stepOuterY1 = -dirY;

                stepInnerX0 = 0;
                stepInnerY0 = dirY;
                errorStepDiagonal = errorStepY - errorStepX;

                errorThreshold = yLen - errorStepY;
            }

            float totalThickness = 2 * thickness * Mathf.Sqrt(xLen * xLen + yLen * yLen); // the "area" of the line (thickness*length), doubled because
                                                                                          // errorStepX and errorStepY are too.
            int outerError = 0;
            int innerLoopError = 0;
            int thicknessDone = 0;

            while (thicknessDone < totalThickness)
            {
                BresenhamLineDraw(x1, y1, primaryLength, stepInnerX0, stepInnerY0, stepInnerX1, stepInnerY1, innerLoopError, errorThreshold, errorStepY, errorStepDiagonal, ref grid);
                if (outerError < errorThreshold)
                {
                    x1 += stepOuterX0;
                    y1 += stepOuterY0;
                }
                else
                {
                    thicknessDone += errorStepY;
                    outerError -= errorStepX;
                    if (innerLoopError < errorThreshold)
                    {
                        x1 += stepOuterX1;
                        y1 += stepOuterY1;
                        innerLoopError -= errorStepY;
                    }
                    else
                    {
                        if (xLen > yLen)
                            x1 += stepOuterX1;
                        else
                            y1 += stepOuterY1;
                        innerLoopError -= errorStepDiagonal;
                        if (thicknessDone > totalThickness)
                            return; // breakout on the extra line (?)
                        BresenhamLineDraw(x1, y1, primaryLength, stepInnerX0, stepInnerY0, stepInnerX1, stepInnerY1, innerLoopError, errorThreshold, errorStepY, errorStepDiagonal, ref grid);
                        if (xLen > yLen)
                            y1 += stepOuterY1;
                        else
                            x1 += stepOuterX1;
                    }
                }

                thicknessDone += errorStepX;
                outerError += errorStepY;

            }
        }

        public static void BresenhamLineDraw(int x, int y, int primaryLength, int stepInnerX0, int stepInnerY0, int stepInnerX1, int stepInnerY1, int innerLoopError, int errorThreshold, int errorStepY, int errorStepDiagonal, ref bool[,] grid)
        {
            for (int i = 0; i <= primaryLength; i++)
            {
                grid.SetWall(x, y, false);
                if (innerLoopError <= errorThreshold)
                {
                    x += stepInnerX0;
                    y += stepInnerY0;
                    innerLoopError += errorStepY;
                }
                else
                {
                    x += stepInnerX1;
                    y += stepInnerY1;
                    innerLoopError += errorStepDiagonal;
                }
            }
        }
    }
}
