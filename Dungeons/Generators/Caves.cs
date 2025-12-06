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


        public Caves(Color? minimapWallsColor = null, int gridWidth = 38, int gridHeight = 18)
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
            int worldArea = gridWidth * BlockSize * gridHeight * BlockSize;
            int numNodes = 2 + Mathf.RoundToInt(worldArea / (60 * 60) * rng.Next(1f, 1.2f)); // imagine a grid of nodes 60 units apart in each direction
            if (numNodes < 2)
                numNodes = 2;
            //int numNodes = 1 + Mathf.RoundToInt((GridWidth / 7f * BlockSize / 16) * (GridHeight / 7f * BlockSize / 16) + rng.Next(0f, 2f));
            float maxRadius = Mathf.Clamp(Mathf.Min(GridWidth, GridHeight) - 2, min: 1, max: 4) * 8 / BlockSize; // scale up radius as blocksize gets smaller
            float maxRadiusSqrt = Mathf.Sqrt(maxRadius);

            List<OneWayGridNode> unconnectedRooms = new List<OneWayGridNode>();
            for (int i = 0; i < numNodes; i++)
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
            CreateMinimapIfPresent(wallsGrid);
            CreateWalls(wallsGrid);
            
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

        public static void Tunnel(Vector2 position1, Vector2 position2, int radius1, int radius2, ref bool[,] grid)
        {
            float x1 = position1.x;
            float y1 = position1.y;
            float x2 = position2.x;
            float y2 = position2.y;
            float thickness = (radius1 + radius2) / 2f;

            float dX = x2 - x1;
            float dY = y2 - y1;
            float incX = Mathf.Sign(Mathf.Sign(dX) + 0.5f);
            float incY = Mathf.Sign(Mathf.Sign(dY) + 0.5f);
            if (dX < 0)
                dX = -dX;
            if (dY < 0)
                dY = -dY;

            float len;
            float sd0x;
            float sd0y;
            float dd0x;
            float dd0y;

            float sd1x;
            float sd1y;
            float dd1x;
            float dd1y;

            float ku;
            float kv;
            float kd;

            float kt; // threshold for error term
            if (dX > dY)
            {
                len = dX;
                sd0x = 0;
                sd0y = incY;
                dd0x = -incX;
                dd0y = incY;

                sd1x = incX;
                sd1y = 0;
                dd1x = incX;
                dd1y = incY;

                ku = 2 * dX;
                kv = 2 * dY;
                kd = kv - ku;

                kt = dX - kv;
            }
            else
            {
                len = dY;
                sd0x = incX;
                sd0y = 0;
                dd0x = incX;
                dd0y = -incY;

                sd1x = 0;
                sd1y = incY;
                dd1x = incX;
                dd1y = incY;

                ku = 2 * dY;
                kv = 2 * dX;
                kd = kv - ku;

                kt = dY - kv;
            }

            float tk = 2 * thickness * Mathf.Sqrt(dX * dX + dY * dY);
            float d0 = 0; // outer loop error term
            float d1 = 0; // inner loop error term
            float dd = 0; // thickness error term

            while (dd < tk)
            {
                BresenhamLineDraw(x1, y1, d1, len, sd1x, sd1y, dd1x, dd1y, d1, kt, kv, kd, ref grid);
                if (d0 < kt)
                {
                    x1 += sd0x;
                    y1 += sd0y;
                }
                else
                {
                    dd += kv;
                    d0 -= ku;
                    if (d1 < kt)
                    {
                        x1 += dd0x;
                        y1 += dd0y;
                        d1 -= kv;
                    }
                    else
                    {
                        if (dX > dY)
                            x1 += dd0x;
                        else
                            y1 += dd0y;
                        d1 = d1 - kd;
                        if (dd > tk)
                            return; // breakout on the extra line (?????)
                        BresenhamLineDraw(x1, y1, d1, len, sd1x, sd1y, dd1x, dd1y, d1, kt, kv, kd, ref grid);
                        if (dX > dY)
                            y1 += dd0y;
                        else
                            x1 += dd0x;
                    }
                }

                dd += ku;
                d0 += kv;

            }
        }

        public static void BresenhamLineDraw(float x, float y, float d, float len, float sd1x, float sd1y, float dd1x, float dd1y, float d1, float kt, float kv, float kd, ref bool[,] grid)
        {
            for (int i = 0; i <= len; i++)
            {
                //grid[Mathf.RoundToInt(x), Mathf.RoundToInt(y)] = false;
                grid.SetWall(Mathf.RoundToInt(x), Mathf.RoundToInt(y), false);
                if (d1 <= kt)
                {
                    x += sd1x;
                    y += sd1y;
                    d1 += kv;
                }
                else
                {
                    x += dd1x;
                    y += dd1y;
                    d1 += kd;
                }
            }
        }
    }
}
