using GadgetCore.API;
using GadgetCore.Util;
using System;
using System.Collections.Generic;
using System.Data.SqlTypes;
using System.Drawing;
using System.Linq;
using System.Reflection;
using System.Text;
using TerrainGenerators.Helpers;
using UnityEngine;

namespace TerrainGenerators.Generators
{
    public class Cathedral : GeneratorBase // TODO: UNFINISHED!
    {
        public override bool[,] WallsGrid => wallsGrid;
        private bool[,] wallsGrid;

        public override Vector2Int PlayerSpawn => playerSpawn;
        private Vector2Int playerSpawn;
        public override int GridWidth => numRoomsHorizontal * (maxRoomWidth + horizontalPadding);
        public override int GridHeight => numRoomsVertical * (maxRoomHeight + verticalPadding) + maxTopperHeight;

        const int maxRoomWidth = 7; // At least 6 to generate every future room type
        const int maxRoomHeight = 8; // At least 8 to generate every future room type
        const int numRoomsHorizontal = 3;
        const int numRoomsVertical = 2;
        const int horizontalPadding = 1;
        const int verticalPadding = 2;
        const int maxTopperHeight = 9;

        public override void GenerateWalls(RNG rng)
        {
            // max internal width/height of rooms

            wallsGrid = new bool[GridWidth, GridHeight];
                

            int[] stairsIndices = new int[numRoomsVertical - 1];
            for (int i = 0; i < stairsIndices.Length; i++)
            {
                if (i == 0)
                    stairsIndices[i] = rng.Next(1, numRoomsHorizontal); // upper bounds exclusive
                else
                    stairsIndices[i] = rng.Next(0, numRoomsHorizontal);
            }
            List<GridNode> unconnectedNodes = new List<GridNode>();
            //TerrainGenerators.Log($"Starting room placements");
            for (int i = 0; i < numRoomsHorizontal; i++)
            {
                int startX = i * (maxRoomWidth + horizontalPadding);
                int endX = startX + maxRoomWidth - 1;
                // create wall between rooms to the right of every room column
                for(int x = startX + maxRoomWidth; x < startX + maxRoomWidth + horizontalPadding; x++)
                {
                    for(int y = 0; y < GridHeight; y++)
                    {
                        wallsGrid[x, y] = true;
                    }
                }

                for (int j = 0; j < numRoomsVertical; j++)
                {
                    int startY = j * (maxRoomHeight + verticalPadding);
                    if (i == 0) // only do it once per row
                    {
                        // create wall between rooms above every room row
                        int fillEndY;
                        if (j < numRoomsVertical - 1)
                        {
                            fillEndY = startY + maxRoomHeight + verticalPadding - 1;
                        }
                        else // above the top row; fill all the way to the top of the grid
                        {
                            fillEndY = GridHeight - 1;
                        }
                        for (int y = startY + maxRoomHeight; y <= fillEndY; y++)
                        {
                            for (int x = 0; x < GridWidth; x++)
                            {
                                wallsGrid[x, y] = true;
                            }
                        }
                    }
                    GridNode botConnection;
                    int endY = startY + maxRoomHeight - 1;
                    if (j < numRoomsVertical - 1 && stairsIndices[j] == i) // top floor doesn't need stairs
                    {
                        GenerateStaircase(startX, startY, endX, endY, rng, out GridNode topConnection, out botConnection);
                        // the top and bottom of the room should always be connected together
                        if (!topConnection.Connections.Contains(botConnection))
                            topConnection.Connections.Add(botConnection);
                        if (!botConnection.Connections.Contains(topConnection))
                            botConnection.Connections.Add(topConnection);
                    }
                    else
                    {
                        GenerateRoom(startX, startY, endX, endY, rng, out botConnection);
                    }
                    AddUnconnectedNodes(botConnection); // this also adds anything connected to it, including any top connections
                }

                // place toppers above the top row of rooms
                int startY2 = (numRoomsVertical - 1) * (maxRoomHeight + verticalPadding) + maxRoomHeight; // just above the top room
                int endY2 = startY2 + maxTopperHeight - 1;
                GenerateTopper(startX, startY2, endX, endY2, rng);

            }
            List<GridNode> connectedNodes = new List<GridNode>();
            // pick first rooma starting room
            AddAllConnectedNodes(unconnectedNodes[0]); 

            // Recursively adds node and all its connections to unconnectedNodes.
            void AddUnconnectedNodes(GridNode node)
            {
                unconnectedNodes.Add(node);
                foreach (var child in node.Connections)
                {
                    if (!unconnectedNodes.Contains(child))
                        AddUnconnectedNodes(child);
                }
            }

            // Recursively adds node and all of its connections to connectedNodes and removes them from unconnectedNodes.
            void AddAllConnectedNodes(GridNode node)
            {
                connectedNodes.Add(node);
                unconnectedNodes.Remove(node);
                foreach(var child in node.Connections)
                {
                    if (!connectedNodes.Contains(child))
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
                        int yDistance = Math.Abs(unconnectedRoom.Position.y - connectedRoom.Position.y);
                        /*
                        if (yDistance > verticalPadding) // only rooms on the same level (within padding) should be connected.
                        {
                            TerrainGenerators.Log("different y: " + unconnectedRoom.ToString() + " -> " + connectedRoom.ToString());
                            continue;
                        }*/
                        int xDistance = Math.Abs(unconnectedRoom.Position.x - connectedRoom.Position.x);
                        int distanceHeuristic = xDistance + 2 * yDistance; // we don't want connections from the bottom of a floor to the bottom of the next floor; adjacent floors should be preferred unless via stairs or there's no better option
                        if (distanceHeuristic < minDistance)
                        {
                            minDistance = distanceHeuristic;
                            nearestUnconnected = unconnectedRoom;
                            nearestConnected = connectedRoom;
                        }
                    }
                }
                // connect them together and add the unconnected one and all its connections to connectedNodes
                nearestUnconnected.Connections.Add(nearestConnected);
                nearestConnected.Connections.Add(nearestUnconnected);
                //TerrainGenerators.Log($"Connected {nearestUnconnected} to {nearestConnected}");
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
            }
            /*
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
            TerrainGenerators.Log(s);*/

            // spawn at the first node that's not a wall position
            playerSpawn = connectedNodes.First().Position;
            if (wallsGrid.IsWall(playerSpawn.x, playerSpawn.y))
            {
                int x = playerSpawn.x;
                int y = playerSpawn.y;
                if (!wallsGrid.IsWall(x, y + 1))
                {
                    playerSpawn = new Vector2Int(x, y + 1);
                }
                else if (!wallsGrid.IsWall(x + 1, y))
                {
                    playerSpawn = new Vector2Int(x + 1, y);
                }
                else if (!wallsGrid.IsWall(x, y - 1))
                {
                    playerSpawn = new Vector2Int(x, y - 1);
                }
                else if (!wallsGrid.IsWall(x - 1, y))
                {
                    playerSpawn = new Vector2Int(x - 1, y);
                }
                else
                {
                    TerrainGenerators.InternalLogger.LogError("Connecting point at " + x + ", " + y + " had no non-wall tiles around it!");
                    playerSpawn = connectedNodes[rng.Next(0, connectedNodes.Count)].Position;
                    wallsGrid[playerSpawn.x, playerSpawn.y] = false;
                }
            }

            CreateMinimapIfPresent(wallsGrid);
            CreateWalls(wallsGrid);
        }

        public void GenerateStaircase(int startX, int startY, int endX, int endY, RNG rng, out GridNode topConnection, out GridNode botConnection)
        {
            int width = endX - startX + 1;
            int height = endY - startY + 1;
            switch (rng.Next(1, 4 + 1)) // upper bound exclusive
            {
                // todo: Change this so we can log which method is called
                case 1:
                    //TerrainGenerators.Log(nameof(Staircase1));
                    Staircase1(out topConnection, out botConnection);
                    break;
                case 2:
                    //TerrainGenerators.Log(nameof(CentreBlocksStaircase2));
                    CentreBlocksStaircase2(out topConnection, out botConnection);
                    break;
                case 3:
                    //TerrainGenerators.Log(nameof(ZigZagStaircase3));
                    ZigZagStaircase3(out topConnection, out botConnection);
                    break;
                case 4:
                    //TerrainGenerators.Log(nameof(RandomPlatformsStaircase4));
                    RandomPlatformsStaircase4(out topConnection, out botConnection);
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
                bool flipX;
                if (startX < GridWidth / 3 && startY < GridHeight / 2) // stairs near the start climb from bottom left to top right
                    flipX = false;
                else
                    flipX = rng.Next(0, 2) == 1;
                int maxSteps = Math.Min(width, height) - 1; // the maximum steps we could fit in the room if the step height was 1.
                int stepHeight = Mathf.CeilToInt(height / (float)maxSteps); // The step height needed to make a staircase from bottom to top.
                                                                            // If height < width, this will just be 1.
                int actualSteps = Mathf.CeilToInt(height / (float)stepHeight); // The number of steps after accounting for step height.
                int stairStartX = startX + (width - actualSteps) / 2 + 1;
                for(int x = startX; x < stairStartX; x++)
                {
                    // floor in front of stairs (to be removed if connecting there)
                    if (flipX)
                        wallsGrid[endX - x + startX, startY] = true;
                    else
                        wallsGrid[x, startY] = true;

                    // ceiling in front of stairs
                    for(int y = startY + stepHeight + 2; y <= endY; y++)
                    {
                        if (y > endY)
                            break;
                        if (flipX)
                            wallsGrid[endX - x + startX, y] = true;
                        else
                            wallsGrid[x, y] = true;
                    }
                }
                for (int i = 0; i < endX - stairStartX + 1; i++)
                {
                    int x = i + stairStartX;
                    int stairY = startY + (i * stepHeight);
                    for (int y = startY; y <= stairY; y++)
                    {
                        if (y > endY)
                            break;
                        if (flipX)
                            wallsGrid[endX - x + startX, y] = true;
                        else
                            wallsGrid[x, y] = true;

                    }

                    int counterY = startY + (i + 1) * stepHeight + 2;
                    for (int y = counterY; y <= endY; y++)
                    {
                        if (y > endY)
                            break;
                        if (flipX)
                            wallsGrid[endX - x + startX, y] = true;
                        else
                            wallsGrid[x, y] = true;

                    }
                }
                int stairTopX = stairStartX + actualSteps - 2;
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
                    _topConnection = new GridNode(new Vector2Int(internalStartX + width / 2, endY + 1));
                }

                _botConnection = new GridNode(new Vector2Int(internalStartX, startY));
                var _botConnection2 = new GridNode(new Vector2Int(internalEndX, startY), connections: new List<GridNode> { _botConnection} );
                _botConnection.Connections.Add(_botConnection2);
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
                int internalStartX = startX;
                int internalWidth = width;
                if (internalWidth > 4)
                {
                    internalWidth = rng.Next(3, width);
                    if (internalWidth > 5)
                        internalWidth = 5;
                    internalStartX += rng.Next(0, width - internalWidth + 1);
                }
                int internalEndX = internalStartX + internalWidth - 1;

                int leftCentreX = internalStartX + (internalWidth - 1) / 2; // if odd, left and right centres will be the same
                int rightCentreX = internalStartX + internalWidth / 2;
                bool flipX = rng.Next(0, 2) == 1;
                //TerrainGenerators.Log("ZigZagStaircase3 leftCentre " + leftCentreX + ", rightCentre " + rightCentreX + ", internalStartX " + internalStartX + ", internalEndX" + internalEndX + ", startY " + startY + ", internalRoomWidth " + internalWidth + ", flipx " + flipX.ToString());
                for (int x = internalStartX; x <= internalEndX; x++)
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

                for (int y = startY; y <= endY; y++)
                {
                    // fill to left of internal area
                    for (int x = startX; x < internalStartX; x++)
                    {
                        wallsGrid[x, y] = true;
                    }
                    // fill to right of internal area
                    for (int x = internalEndX + 1; x <= endX; x++)
                    {
                        wallsGrid[x, y] = true;
                    }
                }

                int _topX = internalStartX + internalWidth / 2;
                if (internalWidth % 2 == 0) // even width
                    _topX -= rng.Next(0, 2); // 50% chance to move to left centre tile rather than right
                _topConnection = new GridNode(new Vector2Int(_topX, endY + 1));

                int _botX = internalStartX + internalWidth / 2;
                if (internalWidth % 2 == 0) // even width
                    _botX -= rng.Next(0, 2); // 50% chance to move to left centre tile rather than right
                _botConnection = new GridNode(new Vector2Int(_botX, startY));
            }

            /*
            ##..
            ....
            .##.
            ....
            ##..
            ....
            ..##
            */
            void RandomPlatformsStaircase4(out GridNode _topConnection, out GridNode _botConnection)
            {
                int usedWidth = width;
                // limit width to 4 for even or 5 for odd
                if (usedWidth > 5) 
                {
                    if (width % 2 == 0) 
                        usedWidth = 4;
                    else
                        usedWidth = 5;
                }
                int internalStartX = (width - usedWidth) / 2 + startX;
                int internalEndX = endX - (width - usedWidth) / 2;
                int platformWidth = 2;
                int platformX = -1; // assigned later
                for (int j = 0; j < height; j++) 
                {
                    int y = startY + j;
                    bool isPlatformLayer = j % 2 == 1 && j != height - 1; // platforms only on odd layers and not on the last layer
                    if (isPlatformLayer) 
                        platformX = rng.Next(internalStartX, internalEndX - platformWidth + 2);
                    for(int i = 0; i < width; i++)
                    {
                        int x = startX + i;
                        if (x < internalStartX) // left wall
                            wallsGrid[x, y] = true;
                        else if (x > internalEndX) // right wall
                            wallsGrid[x, y] = true;
                        else if (isPlatformLayer && x >= platformX && x < platformX + platformWidth) // platform
                            wallsGrid[x, y] = true;
                        else if (height % 2 == 0 && j == height - 1) // top layer of an even-height room
                        {
                            //if((x < platformX || x >= platformX + platformWidth)) //fill everything except hole above top platform
                            int topHoleDistanceFromSide = (width - 1) / 2;
                            if (x < startX + topHoleDistanceFromSide || x > endX - topHoleDistanceFromSide)
                                wallsGrid[x, y] = true;
                        }
                    }
                }


                // place bottom connection at the middle of the bottom row
                int centreX = startX + width / 2;
                if (width % 2 == 0) // even width
                    centreX -= rng.Next(0, 2); // 50% chance to move to left centre tile rather than right

                if (height % 2 == 0) // place top connection in centre
                    _topConnection = new GridNode(new Vector2Int(centreX, endY + 1));
                else // place top connection above middle of highest platform
                    _topConnection = new GridNode(new Vector2Int(platformX + platformWidth / 2, endY + 1));
                _botConnection = new GridNode(new Vector2Int(centreX, startY));

            }
        }


        public void GenerateRoom(int startX, int startY, int endX, int endY, RNG rng, out GridNode botConnection)
        {
            if (rng.Next(0, 10) == 0) // 10% chance to generate a staircase instead
            {
                GenerateStaircase(startX, startY, endX, endY, rng, out _, out botConnection);
                return;
            }

            //int roomWidth = endX - startX + 1;
            //int roomHeight = endY - startY + 1;

            /*
            // todo: remove
            if (rng.Next(0, 2) == 0)
                botConnection = TOnT();
            else
                botConnection = DoubleTOnT();
            return;
            */

            int maxWidth = endX - startX + 1;
            int maxHeight = endY - startY + 1;
            // These are assigned further down but need to be defined here to be captured by some of the generator Funcs
            int roomWidth = -1;
            int roomHeight = -1;

            if (maxWidth < 4 || maxHeight < 4)
                TerrainGenerators.InternalLogger.LogError("Rooms aren't meant to be less than 4x4!");

            // all possible generator methods and the minimum room size they require
            List<Tuple<Func<GridNode>, Vector2Int>> possibleGeneratorsWithMinSizes = new List<Tuple<Func<GridNode>, Vector2Int>>
            {
                new Tuple<Func<GridNode>, Vector2Int>( SmallArchAndIsland, new Vector2Int(4, 5) ),
                new Tuple<Func<GridNode>, Vector2Int>( SmallArchAndIsland, new Vector2Int(6, 4) ),
                new Tuple<Func<GridNode>, Vector2Int>( BigArchAndIsland, new Vector2Int(4, 8) ),
                new Tuple<Func<GridNode>, Vector2Int>( BigArchAndIsland, new Vector2Int(5, 5) ),
                new Tuple<Func<GridNode>, Vector2Int>( Octagon, new Vector2Int(5, 5) ),
                new Tuple<Func<GridNode>, Vector2Int>( TOnT, new Vector2Int(5, 5) ),
                new Tuple<Func<GridNode>, Vector2Int>( DoubleTOnT, new Vector2Int(5, 5) ),
                new Tuple<Func<GridNode>, Vector2Int>( FloatingCross, new Vector2Int(5, 8) ),
            };

            // find all distinct generators that fit within the max room size
            List<Func<GridNode>> validGenerators = possibleGeneratorsWithMinSizes.Where(g => maxWidth >= g.Item2.x && maxHeight >= g.Item2.y)
                .Select(g => g.Item1) // the generator func
                .Distinct() // so generators with multiple possible sizes aren't weighted higher
                .ToList();
            Func<GridNode> chosenGenerator = validGenerators[rng.Next(0, validGenerators.Count)];
            List<Tuple<Func<GridNode>, Vector2Int>> possibleSizes = possibleGeneratorsWithMinSizes.Where(g => g.Item1 == chosenGenerator).ToList();
            Vector2Int minSize = possibleSizes[rng.Next(0, possibleSizes.Count)].Item2;

            roomWidth = rng.Next(minSize.x, maxWidth + 1);
            roomHeight = rng.Next(minSize.y, maxHeight + 1);
            int newStartX = startX + rng.Next(0, maxWidth - roomWidth); // upper bound exclusive
            int newEndX = newStartX + roomWidth - 1;
            int newEndY = startY + roomHeight - 1;
            // fill to the left and right of room
            for (int y = startY; y <= endY; y++)
            {
                // fill to the left of room
                for (int x = startX; x < newStartX; x++)
                {
                    wallsGrid[x, y] = true;
                }
                // right of room
                for (int x = newEndX + 1; x <= endX; x++)
                {

                    wallsGrid[x, y] = true;
                }
            }
            // fill above the room
            for (int x = newStartX; x <= newEndX; x++)
            {
                for (int y = newEndY + 1; y <= endY; y++)
                {
                    wallsGrid[x, y] = true;
                }
            }
            startX = newStartX;
            endX = newEndX;
            endY = newEndY;

            // run the chosen generator
            botConnection = chosenGenerator();
            //TerrainGenerators.Log(chosenGenerator.Method.Name + ", w=" + roomWidth + ", h=" + roomHeight);
            return;

            // Probably requires width of 6+
            GridNode SmallArchAndIsland()
            {
                int platformStart;
                int platformEnd;
                platformStart = roomWidth / 2 - 1; // integer division, will round down on odd-width room

                int leftArchStart = Mathf.Min(roomWidth / 4, platformStart - 2);
                int rightArchEnd = roomWidth - leftArchStart - 1;
                int archWidth = rightArchEnd - leftArchStart + 1;
                int archFromTop = archWidth; // vertical radius
                if (archWidth > roomHeight - 4)
                {
                    archFromTop = roomHeight - 4;
                }

                float archRadius = archWidth / 2f;
                float archYOffset = roomHeight - archFromTop - 1;

                if (roomWidth % 2 == 0) // even width; 2-tile centre
                {
                    platformEnd = roomWidth / 2;
                }
                /*
                else if (width == 5 && height <= 5) // narrow odd-width; 1-tile center
                {
                    platformStart += 1;
                    platformEnd = platformStart;
                }*/
                else // odd width; 3-tile centre
                {
                    platformEnd = roomWidth / 2 + 1;
                }
                for (int i = 0; i < roomWidth; i++)
                {
                    int x = startX + i;
                    float centreRelativeX = i - roomWidth / 2f + 0.5f;
                    float archY = Mathf.Sqrt(archRadius * archRadius - centreRelativeX * centreRelativeX) + archYOffset;
                    if (centreRelativeX <= -archRadius || centreRelativeX >= archRadius)
                        archY = 4;
                    for (int j = 0; j < roomHeight; j++)
                    {
                        int y = startY + j;
                        // first 2 layers are air
                        if (j < 2) 
                        {
                            continue;
                        }
                        // floating platform on 3rd layer
                        if (j == 2 && i >= platformStart && i <= platformEnd)
                        {
                            wallsGrid[x, y] = true;
                            continue;
                        }
                        // walls on left/right of layers 4+
                        if(i < leftArchStart || i > rightArchEnd)
                        {
                            WallsGrid[x, y] = true;
                            continue;
                        }
                        // arch over top of middle of room
                        {
                            if (j >= archY)
                                WallsGrid[x, y] = true;
                        }
                    }
                }

                // one connection in the bottom left, another in the bottom right
                var _botConnection = new GridNode(new Vector2Int(startX, startY));
                GridNode _botConnection2 = new GridNode(new Vector2Int(endX, startY), connections: new List<GridNode> { _botConnection});
                _botConnection.Connections.Add(_botConnection2);
                return _botConnection;
            }

            // probably requires width and height of at least 5 or 6.
            GridNode BigArchAndIsland()
            {
                int platformStart;
                int platformEnd;
                platformStart = roomWidth / 2 - 1; // integer division, will round down on odd-width room

                if (roomWidth % 2 == 0) // even width; 2-tile centre
                {
                    platformEnd = roomWidth / 2;
                }
                else if(roomWidth == 5) // narrow odd width; 1-tile center
                {
                    platformStart += 1;
                    platformEnd = platformStart;
                }
                else // normal odd width; 3-tile centre
                {
                    platformEnd = roomWidth / 2 + 1;
                }

                for (int i = 0; i < roomWidth; i++)
                {
                    float archY = (roomHeight - 0.5f) * (1 - Mathf.Pow((2 * i + 1) / ((float)roomWidth) - 1, 2));
                    int x = startX + i;
                    for (int j = 0; j < roomHeight; j++)
                    {
                        int y = startY + j;
                        // floating platform on 3rd layer
                        if (j == 2 && i >= platformStart && i <= platformEnd)
                        {
                            wallsGrid[x, y] = true;
                            continue;
                        }
                        // arch
                        {
                            if (j >= archY)
                                WallsGrid[x, y] = true;
                        }
                    }
                }

                // one connection in the bottom left, another in the bottom right
                var _botConnection = new GridNode(new Vector2Int(startX, startY));
                GridNode _botConnection2 = new GridNode(new Vector2Int(endX, startY), connections: new List<GridNode> { _botConnection });
                _botConnection.Connections.Add(_botConnection2);
                return _botConnection;
            }

            // requires width of 5+
            GridNode Octagon()
            {
                bool generateCross = rng.Next(0, 2) == 1; // 50% chance
                int size = Math.Min(roomWidth, roomHeight);
                int octEndX = startX + size - 1;
                int octEndY = startY + size - 1;

                int stepsDiagonal = Mathf.FloorToInt(0.3f * size); // approximation for octagon; 40% of the room in the middle and 30% on either side for angled parts
                // draw the diagonal parts of the octagon
                for (int i = 0; i < stepsDiagonal; i++)
                {
                    int LineLeftX = startX + i;
                    int LineRightX = octEndX - i;
                    int LineBotY = startY + stepsDiagonal - 1 - i;
                    int LineTopY = octEndY - stepsDiagonal + 1 + i;

                    // fill the bottom corners
                    for (int y = startY; y <= LineBotY; y++)
                    {
                        wallsGrid[LineLeftX, y] = true;
                        wallsGrid[LineRightX, y] = true;
                        //TerrainGenerators.Log(LineLeftX + "," + y);
                        //TerrainGenerators.Log(LineRightX + "," + y);
                    }
                    // fill the top corners
                    for (int y = LineTopY; y <= endY; y++)
                    {
                        WallsGrid[LineLeftX, y] = true;
                        wallsGrid[LineRightX, y] = true;
                        //TerrainGenerators.Log(LineLeftX + "," + y);
                        //TerrainGenerators.Log(LineRightX + "," + y);
                    }
                }
                // fill the centre area above the octagon if it's not using the full height
                if (size < roomHeight)
                {
                    for (int x = startX + stepsDiagonal; x <= octEndX - stepsDiagonal; x++)
                    {
                        for (int y = octEndY + 1; y <= endY; y++)
                        {
                            WallsGrid[x, y] = true;
                            //TerrainGenerators.Log(x + "," + y);
                        }
                    }
                }
                // fill to the right of the octagon if it's not using the full width
                if (size < roomWidth)
                {
                    for (int x = octEndX + 1; x <= endX; x++)
                    {
                        for(int y = startY; y <= endY; y++)
                        {
                            wallsGrid[x, y] = true;
                            //TerrainGenerators.Log(x + "," + y);
                        }
                    }
                }

                // draw the centre cross
                if (generateCross)
                {
                    int lineThick = 2 - (size % 2); // 2 for even room; 1 for odd
                    int crossHeight = size - 1; // from the bottom to 1 below the top
                    int crossHorizontalFromLine = (crossHeight * 3 / 4 - lineThick) / 2; // integer division; total cross width should be around 3/4 size of height
                    if (crossHorizontalFromLine * 2 + lineThick < size - 2) // make sure there's space to go around both sides
                        crossHorizontalFromLine = (size - lineThick - 2) / 2;

                    // place the vertical line in the middle
                    int x1 = startX + size / 2; // int division; centre in odd-width; 1 right of centre in even-width
                    for (int y = startY; y < startY + crossHeight; y++)
                    {
                        wallsGrid[x1, y] = true;
                        //TerrainGenerators.Log(x1 + "," + y);
                        if (lineThick == 2)
                        {
                            wallsGrid[x1 - 1, y] = true;
                            //TerrainGenerators.Log(x1 - 1 + "," + y);
                        }
                    }

                    // place the horizontal line just below the top angled bits of the octagon
                    int crossHorizontalYEnd = startY + size - stepsDiagonal - 2;
                    for (int x = x1 - crossHorizontalFromLine - lineThick + 1; x < x1 + crossHorizontalFromLine + 1; x++)
                    {
                        wallsGrid[x, crossHorizontalYEnd] = true;
                        //TerrainGenerators.Log(x + "," + crossHorizontalYEnd);
                        if (lineThick == 2)
                        {
                            wallsGrid[x, crossHorizontalYEnd - 1] = true;
                            //TerrainGenerators.Log(x + "," + (crossHorizontalYEnd - 1));
                        }
                    }
                }
                var _botConnection = new GridNode(new Vector2Int(startX + stepsDiagonal - 1, startY));
                var _botConnection2 = new GridNode(new Vector2Int(octEndX - stepsDiagonal + 1, startY), connections: new List<GridNode> { _botConnection });
                _botConnection.Connections.Add(_botConnection2);
                return _botConnection;
            }

            GridNode TOnT()
            {
                int centreX = startX + roomWidth / 2; // int division; if even-width this will be the right half of the centre
                int verticalLineThickness = 2 - (roomWidth % 2);
                int line1DistanceFromCentre = Mathf.Min(
                    (roomWidth * 2 / 3 - verticalLineThickness) / 2, // 2/3rds of the room
                    (roomWidth - verticalLineThickness - 2) / 2); // 1 space in from each edge
                int line2Y = startY + ((roomHeight - 1) * 2 / 3);
                //TerrainGenerators.Log("TOnT centre " + centreX + ", thick " + verticalLineThickness + ", startX " + startX + ", endX" + endX + ", startY " + startY + ", roomWidth " + roomWidth + ", roomHeight " + roomHeight);
                for (int x = startX; x <= endX; x++)
                {
                    for(int y = startY; y <= endY; y++)
                    {
                        if (x == centreX) // centre vertical
                            continue;
                        if (verticalLineThickness == 2 && x == centreX - 1) // other centre vertical when 2-wide
                            continue;
                        if (y == line2Y) // line2 horizontal
                            continue;
                        if (y == endY && x >= centreX - line1DistanceFromCentre - verticalLineThickness + 1 && x <= centreX + line1DistanceFromCentre)
                            continue; // line 1 horizontal
                        //TerrainGenerators.Log("TOnT Wall " + x + "," + y);
                        wallsGrid[x, y] = true; // wall everywhere else
                    }
                }

                var _botConnection = new GridNode(new Vector2Int(centreX, startY));
                return _botConnection;
            }

            GridNode DoubleTOnT()
            {
                int centreX = startX + roomWidth / 2; // int division; if even-width this will be the right half of the centre
                int verticalLineThickness = 2 - (roomWidth % 2);
                int topLineY = startY + 4;
                int midLineY = startY + 2;
                for (int x = startX; x <= endX; x++)
                {
                    for (int y = startY; y <= endY; y++)
                    {
                        if (y == topLineY)
                            continue;
                        if (y == midLineY)
                            continue;
                        // centre vertical below middle
                        if(y < midLineY)
                        {
                            if (x == centreX) // centre vertical
                                continue;
                            if (verticalLineThickness == 2 && x == centreX - 1) // other centre vertical when 2-wide
                                continue;
                        }
                        if (y > midLineY && y < topLineY)
                        {
                            if (x == centreX + 1) // right of centre upwards line
                                continue;
                            if (verticalLineThickness == 2)
                            {
                                if (x == centreX - 2) // left of centre upwards line when 2-wide
                                    continue;
                            }
                            else
                            {
                                if (x == centreX - 1) // left of centre upwards line when 1-wide
                                    continue;
                            }
                        }

                        wallsGrid[x, y] = true; // wall everywhere else
                    }
                }

                var _botConnection = new GridNode(new Vector2Int(centreX, startY));
                if (verticalLineThickness == 2)
                {
                    var _botConnection2 = new GridNode(new Vector2Int(centreX - 1, startY));
                    _botConnection.Connections.Add(_botConnection2);
                    _botConnection2.Connections.Add(_botConnection);
                }
                return _botConnection;
            }


            GridNode FloatingCross() // requires 5x8 area
            {
                bool generateCross = rng.Next(0, 3) > 0; // 2/3 chance
                // create centre shaft
                int centreX = startX + roomWidth / 2;
                if (roomWidth % 2 == 0) // even width; centreX is the right centre pixel
                    centreX -= rng.Next(0, 2); // 50% chance instead be left centre pixel
                for(int i = 0; i < roomHeight; i++)
                {
                    int y = startY + i;
                    if(i >= 8) // above room; fill with wall
                    {
                        for(int x = startX; x <= endX; x++)
                        {
                            wallsGrid[x, y] = true;
                        }
                        continue;
                    }
                    // fill before left of room
                    for (int x = startX; x <= centreX - 3; x++)
                    {
                        wallsGrid[x, y] = true;
                    }
                    // fill after right of room
                    for (int x = centreX + 3; x <= endX; x++)
                    {
                        wallsGrid[x, y] = true;
                    }

                    // draw centre column
                    if (i == 1 || (generateCross && i >= 3 && i <= 6))
                    {
                        wallsGrid[centreX, y] = true;
                    }
                    // draw left and right arms
                    if(i == 5)
                    {
                        wallsGrid[centreX - 1, y] = true;
                        wallsGrid[centreX + 1, y] = true;
                    }
                    // the left and right columns except around the top of cross
                    {
                        if (i == 7 || i <= 3)
                        {
                            wallsGrid[centreX - 2, y] = true;
                            wallsGrid[centreX + 2, y] = true;
                        }
                    }
                }




                var _botConnection = new GridNode(new Vector2Int(centreX - 2, startY));
                var _botConnection2 = new GridNode(new Vector2Int(centreX + 2, startY), connections: new List<GridNode> { _botConnection });
                _botConnection.Connections.Add(_botConnection2);
                return _botConnection;
            }
        }

        private void GenerateTopper(int startX, int startY, int endX, int endY, RNG rng)
        {
            bool foundStart = false;
            int minX = endX;
            int maxX = startX;
            int searchCountDown = 4; // search this many layers below the first empty space to determine minX and maxX
            // search downwards until we find the top of the room below, then search a bit further to determine width
            int searchY = startY;
            while (searchCountDown > 0 && startY > 0)
            {
                searchY--;
                if (foundStart)
                    searchCountDown--;
                else
                    startY = searchY;
                for(int x = startX; x <= endX; x++)
                {
                    if (wallsGrid[x, searchY] == false)
                    {
                        if (!foundStart)
                        {
                            startY++; // we start above the first empty space
                            foundStart = true;
                        }
                        if (x < minX)
                            minX = x;
                        if (x > maxX)
                            maxX = x;
                    }
                }
            }
            startX = minX;
            endX = maxX;
            int maxWidth = endX - startX + 1;
            while(maxWidth < 4 && startX > 0 && endX < GridWidth - 1)
            {
                startX--;
                endX++;
                maxWidth += 2;
            }
            int maxHeight = endY - startY + 1;
            switch (rng.Next(1, 2 + 1)) // upper bound exclusive
            {
                // todo: Change this so we can log which method is called
                case 1:
                    TriangleTopper();
                    break;
                case 2:
                    CrossTopper();
                    break;
                default:
                    throw new Exception("Case not handled!");
            }

            void TriangleTopper()
            {
                int width = rng.Next(3, maxWidth + 1); // upper bound exclusive
                if (width % 2 != maxWidth % 2)
                    width += 1; // this won't go over maxWidth because if width == maxWidth they will have the same parity.
                int height = rng.Next(4 - (width % 2), maxHeight + 1);
                //TerrainGenerators.Log(nameof(TriangleTopper) + " with maxWidth " + maxWidth + ", maxHeight " + maxHeight + ", selected width " + width + ", height " + height);
                int middle = startX + maxWidth / 2; // integer division
                int maxI = (width - 1) / 2;
                for(int i = 0; i <= maxI; i++)
                {
                    int leftX = middle - i;
                    if (width % 2 == 0)
                        leftX -= 1;
                    int rightX = middle + i;
                    for(int y = startY; y <= startY + (maxI - i + 1) * (height - 1) / (maxI + 1); y++)
                    {
                        wallsGrid.SetWall(leftX, y, false);
                        wallsGrid.SetWall(rightX, y, false);
                    }
                }
                for(int x = startX; x <= endX; x++)
                {
                    // make sure we can connect to the room below
                    wallsGrid.SetWall(x, startY - 1, false);
                }
            }
            void CrossTopper()
            {
                int maxWidth2 = maxWidth;
                if (maxWidth > maxHeight - 1)
                {
                    if (maxWidth % 2 != maxHeight % 2)
                        maxWidth2 = maxHeight - 1;
                    else
                        maxWidth2 = maxHeight - 2;
                }
                
                int width = rng.Next(5, maxWidth2 + 1); // upper bound exclusive
                if (width > maxWidth) // in case maxWidth is less than 5
                    width = maxWidth;
                if (width % 2 != maxWidth % 2)
                    width += 1; // this won't go over maxWidth because if width == maxWidth they will have the same parity.
                int centreSize = 2 - width % 2 + ((width / 7) * 2); // int division before doubling preserves parity
                int armWidth = (width - centreSize) / 2;
                int height = centreSize + armWidth - 1;
                if (armWidth <= 1)
                    height += 1;
                int centreX = startX + maxWidth / 2; // integer division
                //TerrainGenerators.Log(nameof(CrossTopper) + " with maxWidth " + maxWidth + ", maxHeight " + maxHeight + ", selected width " + width + ", armWidth " + armWidth + ", height " + height);
                for (int i = 0; i < (width + 1) / 2; i++)
                {
                    int leftX = centreX - i;
                    if (width % 2 == 0)
                        leftX -= 1;
                    int rightX = centreX + i;

                    // clear out the space underneath the topper to make sure it connects
                    wallsGrid[leftX, startY - 1] = false;
                    wallsGrid[rightX, startY - 1] = false;

                    if (i < (centreSize + 1) / 2)
                    {
                        for(int j = 0; j < centreSize; j++)
                        {
                            wallsGrid[leftX, startY + j] = false;
                            wallsGrid[rightX, startY + j] = false;
                        }
                    }
                    else
                    {
                        int y1 = startY + centreSize / 2;
                        int y2 = startY + (centreSize - 1) / 2;
                        WallsGrid[leftX, y1] = false;
                        WallsGrid[rightX, y1] = false;
                        WallsGrid[leftX, y2] = false;
                        WallsGrid[rightX, y2] = false;
                    }
                    if (i == 0)
                    {
                        for (int j = 0; j < height; j++)
                        {
                            wallsGrid[leftX, startY + j] = false;
                            wallsGrid[rightX, startY + j] = false;
                        }
                    }
                }
            }
        }

        public override void PopulateLevel(RNG rng)
        {
            InstanceTracker.GameScript.StartCoroutine(SpawnVanillaSpawnables(rng, wallsGrid));
        }
    }
}
