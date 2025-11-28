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
        public override int GridWidth => numRoomsHorizontal * (maxRoomWidth + horizontalPadding);
        public override int GridHeight => numRoomsVertical * (maxRoomHeight + verticalPadding);

        const int maxRoomWidth = 6;
        const int maxRoomHeight = 6;
        const int numRoomsHorizontal = 3;
        const int numRoomsVertical = 2;
        const int horizontalPadding = 2;
        const int verticalPadding = 3;

        public override void GenerateWalls(RNG rng)
        {
            // max internal width/height of rooms

            wallsGrid = new bool[GridWidth, GridHeight];
                

            int[] stairsIndices = new int[numRoomsVertical - 1];
            for(int i = 0; i < stairsIndices.Length; i++)
            {
                stairsIndices[i] = rng.Next(0, numRoomsHorizontal); // upper bounds exclusive
            }
            List<GridNode> unconnectedNodes = new List<GridNode>();
            TerrainGenerators.Log($"Starting room placements");
            for (int i = 0; i < numRoomsHorizontal; i++)
            {
                int startX = i * (maxRoomWidth + horizontalPadding);

                    TerrainGenerators.Log($"Starting vertical wall (i={i})");
                    // create wall between rooms to the right of every room column
                    for(int x = startX + maxRoomWidth; x < startX + maxRoomWidth + horizontalPadding; x++)
                    {
                        for(int y = 0; y < GridWidth; y++)
                        {
                            wallsGrid[x, y] = true;
                        }
                    }
                
                for(int j = 0; j < numRoomsVertical; j++)
                {
                    int startY = j * (maxRoomHeight + verticalPadding);
                    if (i == 0)
                    {
                        TerrainGenerators.Log($"Starting horizontal wall (j={j})");
                        // create wall between rooms above every room row
                        for (int y = startY + maxRoomHeight; y < startY + maxRoomHeight + verticalPadding; y++)
                        {
                            for (int x = 0; x < GridWidth; x++)
                            {
                               wallsGrid[x, y] = true;
                            }
                        }
                    }
                    TerrainGenerators.Log("A");
                    GridNode botConnection;
                    GridNode topConnection;
                    if (j < numRoomsVertical - 1 && stairsIndices[j] == i)
                        GenerateStaircase(startX, startY, startX + maxRoomWidth - 1, startY + maxRoomHeight - 1, rng, out topConnection, out botConnection);
                    else
                        //GenerateRoom(startX, startY, startX + maxRoomWidth - 1, startY + maxRoomHeight - 1, rng, out topConnection, out botConnection);
                        GenerateStaircase(startX, startY, startX + maxRoomWidth - 1, startY + maxRoomHeight - 1, rng, out topConnection, out botConnection);
                    // the top and bottom of the room should always be connected together
                    TerrainGenerators.Log("B");
                    if (!topConnection.Connections.Contains(botConnection))
                        topConnection.Connections.Add(botConnection);
                    if (!botConnection.Connections.Contains(topConnection))
                        botConnection.Connections.Add(topConnection);
                    unconnectedNodes.Add(botConnection);
                    unconnectedNodes.Add(topConnection);
                    TerrainGenerators.Log("C");
                }
            }
            List<GridNode> connectedNodes = new List<GridNode>();
            // pick first rooma starting room
            playerSpawn = unconnectedNodes[0].Position;
            AddAllConnectedNodes(unconnectedNodes[0]); 
            TerrainGenerators.Log("D");
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
            TerrainGenerators.Log("E");
            while (unconnectedNodes.Count > 0)
            {
                TerrainGenerators.Log("F");
                GridNode nearestUnconnected = null;
                GridNode nearestConnected = null;
                float minDistance = float.MaxValue;

                // Find closest unconnected node to any connected node
                foreach (GridNode unconnectedRoom in unconnectedNodes)
                {
                    foreach (GridNode connectedRoom in connectedNodes)
                    {
                        float yDistance = Math.Abs(unconnectedRoom.Position.y - connectedRoom.Position.y);
                        if (yDistance > verticalPadding) // only rooms on the same level (within padding) should be connected.
                        {
                            TerrainGenerators.Log("different y: " + unconnectedRoom.ToString() + " -> " + connectedRoom.ToString());
                            continue;
                        }
                        float xDistance = Math.Abs(unconnectedRoom.Position.x - connectedRoom.Position.x);
                        if (xDistance < minDistance)
                        {
                            minDistance = xDistance;
                            nearestUnconnected = unconnectedRoom;
                            nearestConnected = connectedRoom;
                        }
                    }
                }
                TerrainGenerators.Log("G");
                // connect them together and add the unconnected one and all its connections to connectedNodes
                nearestUnconnected.Connections.Add(nearestConnected);
                nearestConnected.Connections.Add(nearestUnconnected);
                TerrainGenerators.Log($"Connected {nearestUnconnected} to {nearestConnected}");
                AddAllConnectedNodes(nearestUnconnected);

                // Clear a tunnel in wallsGrid between the two nodes (not any of the others in the room since those should already be connected).
                int x1 = nearestConnected.Position.x;
                int x2 = nearestUnconnected.Position.x;
                int y1 = nearestConnected.Position.y;
                int y2 = nearestUnconnected.Position.y;

                if (rng.Next(0, 2) == 1)
                {
                    ClearRectangle(x1, x2, y1, y1); // horizontal at y1
                    ClearRectangle(x2, x2, y1, y2); // vertical at x2
                }
                else
                {
                    ClearRectangle(x1, x1, y1, y2); // vertical at x1
                    ClearRectangle(x1, x2, y2, y2); // horizontal at y2
                }
                void ClearRectangle(int _x1, int _x2, int _y1, int _y2)
                {
                    if(_x2 < _x1)
                    {
                        int tmp = _x2;
                        _x2 = _x1;
                        _x1 = tmp;
                    }
                    if (_y2 < _y1)
                    {
                        int tmp = _y2;
                        _y2 = _y1;
                        _y1 = tmp;
                    }

                    for (int x = _x1; x < _x2 + 1; x++)
                    {
                        for (int y = _y1; y < _y2 + 1; y++)
                        {
                            wallsGrid[x, y] = false;
                        }
                    }
                }
                TerrainGenerators.Log("H");
            }
            string s = "";
            for (int j = 0; j < GridHeight; j++)
            {
                string row = "\r\n";
                for(int i = 0; i < GridWidth; i++)
                {
                    row += wallsGrid[i, j] ? "#" : "."; 
                }
                s = row + s; // prepend row because loop starts from bottom
            }
            TerrainGenerators.Log(s);

            CreateMinimapIfPresent(wallsGrid);
            CreateWalls(wallsGrid);
        }

        public void GenerateStaircase(int startX, int startY, int endX, int endY, RNG rng, out GridNode topConnection, out GridNode botConnection)
        {
            int width = endX - startX + 1;
            int height = endY - startY + 1;
            switch (rng.Next(1, 3 + 1)) // upper bound exclusive
            {

                case 1:
                    Staircase1(out topConnection, out botConnection);
                    break;
                case 2:
                    CentreBlocksStaircase2(out topConnection, out botConnection);
                    break;
                case 3:
                    ZigZagStaircase3(out topConnection, out botConnection);
                    break;
                default:
                    throw new Exception("Case not handled!");
            }
            /* A staircase between two opposing corners of the room
                ....#
                ...##
                ..###
                .####
                #####
            */
            void Staircase1(out GridNode _topConnection, out GridNode _botConnection)
            {
                bool flipX = rng.Next(0, 2) == 1;
                int maxSteps = Math.Min(width, height) - 1; // the maximum steps we could fit in the room if the step height was 1.
                int stepHeight = Mathf.CeilToInt(height / (float)maxSteps); // The step height needed to make a staircase from bottom to top.
                                                                            // If height < width, this will just be 1.
                int actualSteps = Mathf.CeilToInt(height / (float)stepHeight); // The number of steps after accounting for step height.
                int stairStartX = startX + (width - actualSteps) / 2 + 1;
                // floor in front of stairs (to be removed if connecting there)
                for(int x = startX; x < stairStartX; x++)
                {
                    if (flipX)
                        wallsGrid[endX - x + startX, startY] = true;
                    else
                        wallsGrid[x, startY] = true;
                }
                for (int i = 0; i < endX - stairStartX + 1; i++)
                {
                    int x = i + stairStartX;
                    int stairY = startY + (i * stepHeight);
                    for (int y = startY; y < stairY + 1; y++)
                    {
                        if (y > endY)
                            break;
                        if (flipX)
                            wallsGrid[endX - x + startX, y] = true;
                        else
                            wallsGrid[x, y] = true;

                    }
                }
                int stairTopX = stairStartX + actualSteps - 1;
                if (flipX)
                {
                    stairTopX = endX - stairTopX + startX;
                    startX = endX;
                }
                _botConnection = new GridNode(new Vector2Int(startX, startY));
                _topConnection = new GridNode(new Vector2Int(stairTopX, endY + 1));
            }

            /* Floating platforms up the center of the room
                ...
                .#.
                ...
                .#.
                ...
             */
            void CentreBlocksStaircase2(out GridNode _topConnection, out GridNode _botConnection)
            {
                int platformWidth;
                if (width % 2 == 0)
                {
                    platformWidth = 2;
                }
                else
                {
                    platformWidth = width - 2;
                    if (platformWidth > 3)
                        platformWidth = 3;
                }
                int usedWidth = platformWidth + 2;

                int internalStartX = (width - usedWidth) / 2 + startX;
                int internalEndX = endX - (width - usedWidth) / 2;
                for(int x = startX; x <= endX; x++)
                {
                    for(int j = 0; j < height; j++)
                    //for(int y = startY; y <= endY; y++)
                    {
                        int y = startY + j;
                        if (x == internalStartX || x == internalEndX) // the gaps on either side of the platforms
                        {
                            continue;
                        }
                        if(j % 2 == 0 && x >= internalStartX && x <= internalEndX) // the rows between platforms, except the outer walls
                        {
                            continue;
                        }
                        wallsGrid[x, y] = true;
                    }
                }

                if (height % 2 == 0) // top layer would have a center platform
                {
                    // place connections on both sides of the top platform
                    _topConnection = new GridNode(new Vector2Int(internalStartX, endY + 1));
                    var _topConnection2 = new GridNode(new Vector2Int(internalEndX, endY + 1),
                        connections: new List<GridNode>() { _topConnection });
                    _topConnection.Connections.Add(_topConnection2);
                }
                else
                {
                    _topConnection = new GridNode(new Vector2Int(startX + width / 2, endY + 1));
                }

                int _botX = startX + width / 2;
                if (width % 2 == 0) // even width
                    _botX -= rng.Next(0, 2); // 50% chance to move to left centre tile rather than right
                _botConnection = new GridNode(new Vector2Int(_botX, startY));
                
            }

            /*
                ..#
                #..
                ..#
                #..
                ..#
            */
            void ZigZagStaircase3(out GridNode _topConnection, out GridNode _botConnection)
            {
                int leftCentreX = startX + (width - 1) / 2; // if odd, left and right centres will be the same
                int rightCentreX = startX + width / 2;
                bool flipX = rng.Next(0, 2) == 1;
                for (int x = startX; x <= endX; x++)
                {
                    for (int j = 0; j < height; j++)
                    {
                        int y = startY + j;
                        int side;
                        if (flipX)
                            side = (j + 1) % 2;
                        else
                            side = j % 2;
                        if (side == 1 && x >= leftCentreX)
                            continue;
                        else if (side == 0 && x <= rightCentreX)
                            continue;
                        wallsGrid[x, y] = true;
                    }
                }
                int _topX = startX + width / 2;
                if (width % 2 == 0) // even width
                    _topX -= rng.Next(0, 2); // 50% chance to move to left centre tile rather than right
                _topConnection = new GridNode(new Vector2Int(_topX, endY + 1));

                int _botX = startX + width / 2;
                if (width % 2 == 0) // even width
                    _botX -= rng.Next(0, 2); // 50% chance to move to left centre tile rather than right
                _botConnection = new GridNode(new Vector2Int(_botX, startY));
            }
        }


        public void GenerateRoom(int startX, int startY, int endX, int endY, RNG rng, out GridNode topConnection, out GridNode botConnection)
        {
            int width = endX - startX + 1;
            int height = endY - startY + 1;
            botConnection = new GridNode(new Vector2Int(startX, startY));
            topConnection = new GridNode(new Vector2Int(endX, endY));
        }

        public override void PopulateLevel(RNG rng)
        {
            InstanceTracker.GameScript.StartCoroutine(SpawnVanillaSpawnables(rng, wallsGrid));
        }
    }
}
