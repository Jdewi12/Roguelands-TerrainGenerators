using GadgetCore.API;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TerrainGenerators.Helpers;
using UnityEngine;

namespace TerrainGenerators.Generators
{
    public class Rooms : GeneratorBase // TODO: UNFINISHED!
    {
        public override bool[,] WallsGrid => wallsGrid;
        private bool[,] wallsGrid;

        public override Vector2Int PlayerSpawn => playerSpawn;
        private Vector2Int playerSpawn;

        public override void GenerateWalls(RNG rng)
        {
            // max internal width/height of rooms
            const int maxRoomWidth = 6;
            const int maxRoomHeight = 12;
            const int numRoomsHorizontal = 3;
            const int numRoomsVertical = 2;
            const int minHorizontalPadding = 1;
            const int minVerticalPadding = 1;

            wallsGrid = new bool[
                numRoomsHorizontal * (maxRoomWidth + minHorizontalPadding),
                numRoomsVertical * (maxRoomHeight + minVerticalPadding)];

            int[] stairsIndices = new int[numRoomsVertical - 1];
            for(int i = 0; i < stairsIndices.Length; i++)
            {
                stairsIndices[i] = rng.Next(0, numRoomsHorizontal); // upper bounds exclusive
            }
            List<GridNode> unconnectedNodes = new List<GridNode>();
            for (int i = 0; i < numRoomsHorizontal; i++)
            {
                for(int j = 0; j < numRoomsVertical; j++)
                {
                    int startX = i * (maxRoomWidth + minHorizontalPadding);
                    int startY = j * (maxRoomHeight + minHorizontalPadding);
                    GridNode topConnection;
                    GridNode botConnection;
                    if (j < numRoomsVertical - 1 && stairsIndices[j] == i)
                        GenerateStaircase(startX, startY, startX + maxRoomWidth, startY + maxRoomHeight, out topConnection, out botConnection);
                    else
                        GenerateRoom(startX, startY, startX + maxRoomWidth, startY + maxRoomHeight, out topConnection, out botConnection);
                    // the top and bottom of the room should always be connected together
                    if(!topConnection.Connections.Contains(botConnection))
                        topConnection.Connections.Add(botConnection);
                    if (!botConnection.Connections.Contains(topConnection))
                        botConnection.Connections.Add(topConnection);
                    unconnectedNodes.Add(topConnection);
                    unconnectedNodes.Add(botConnection);

                }
            }
            List<GridNode> connectedNodes = new List<GridNode>();
            AddAllConnectedNodes(unconnectedNodes[0]); // pick first rooma starting room
            playerSpawn = unconnectedNodes[0].Position;
            // Recursively adds node and all of it's connections to connectedNodes and removes them from unconnectedNodes.
            void AddAllConnectedNodes(GridNode node)
            {
                connectedNodes.Add(node);
                unconnectedNodes.Remove(node);
                foreach(var child in node.Connections)
                {
                    if (unconnectedNodes.Contains(child))
                        AddAllConnectedNodes(child);
                }
            }

            while (unconnectedNodes.Count > 0)
            {
                GridNode nearestUnconnected = null;
                GridNode nearestConnected = null;
                float minDistance = float.MaxValue;

                // Find closest unconnected node to any connected node
                foreach (GridNode unconnectedRoom in unconnectedNodes)
                {
                    foreach (GridNode connectedRoom in connectedNodes)
                    {
                        float yDistance = Math.Abs(unconnectedRoom.Position.y - connectedRoom.Position.y);
                        if (yDistance > 1) // only rooms on the same level should be connected. 1 tile off is fine because it'll still open up a path.
                            continue;
                        float xDistance = Math.Abs(unconnectedRoom.Position.x - connectedRoom.Position.x);
                        if (xDistance < minDistance)
                        {
                            minDistance = xDistance;
                            nearestUnconnected = unconnectedRoom;
                            nearestConnected = connectedRoom;
                        }
                    }
                }
                // connect them together and add the unconnected one and all its connections to connectedNodes
                nearestUnconnected.Connections.Add(nearestConnected);
                nearestConnected.Connections.Add(nearestUnconnected);
                AddAllConnectedNodes(nearestUnconnected);
                // TODO: clear a tunnel in wallsGrid between the two nodes (not any of the others in the room since those should already be connected).
                // if both nodes are the same y, use that as the y
                // if they are different y, use y pos between rooms (the padding)
            }



            CreateWalls(wallsGrid);
            CreateMinimapIfPresent(wallsGrid);
        }

        public void GenerateStaircase(int startX, int startY, int endX, int endY, out GridNode topConnection, out GridNode botConnection)
        {
            int width = endX - startX;
            int height = endY - startY;
            Staircase1();
            void Staircase1()
            {
                int maxSteps = Math.Min(width, height); // the maximum steps we could fit in the room if the step height was 1.
                int stepHeight = Mathf.CeilToInt(height / (float)maxSteps); // The step height needed to make a staircase from bottom to top.
                                                                            // If height < width, this will just be 1.
                int actualSteps = Mathf.CeilToInt(height / (float)stepHeight); // The number of steps after accounting for step height.
                int stairStartX = startX + (width - actualSteps) / 2;
                for (int i = 0; i < actualSteps; i++)
                {
                    int x = i + stairStartX;
                    int y = startY + (i * stepHeight);
                    if (y > endY)
                        continue;
                    wallsGrid[x, y] = true;
                }
            }
            topConnection = default;
            botConnection = default;
        }


        public void GenerateRoom(int startX, int startY, int endX, int endY, out GridNode topConnection, out GridNode botConnection)
        {
            topConnection = default;
            botConnection = default;
        }

        public override void PopulateLevel(RNG rng)
        {
            InstanceTracker.GameScript.StartCoroutine(SpawnVanillaSpawnables(rng, wallsGrid));
        }
    }
}
