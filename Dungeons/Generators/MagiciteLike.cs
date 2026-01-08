using GadgetCore.API;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml.Linq;
using TerrainGenerators.Helpers;
using UnityEngine;

namespace TerrainGenerators.Generators
{
    public class MagiciteLike : GeneratorBase
    {
        public override Vector2Int PlayerSpawn => playerSpawn;
        private Vector2Int playerSpawn;
        private int mazeCols = 6;
        private int colWidth = 8;
        private int minVerticalTunnelThickness = 2;
        private int mazeRows = 3;
        private int rowHeight = 4;
        private int minHorizontalTunnelThickness = 2;
        private int outerPadding = 5;
        public override int GridWidth => mazeCols * colWidth;
        public override int GridHeight => mazeRows * rowHeight;
        public override int MinimapViewportWidth => Mathf.Clamp(Mathf.RoundToInt(MinimapViewportHeight * 1.5f), 0, GridWidth + outerPadding);
        public override int MinimapViewportHeight => GridHeight + outerPadding;
        public override bool[,] WallsGrid => wallsGrid;
        bool[,] wallsGrid; 

        public override void GenerateWalls(RNG rng)
        {
            Node[,] maze = CreateMaze(rng);
            wallsGrid = new bool[GridWidth, GridHeight];
            //Debug.Log("v");

            for (int i = 0; i < GridWidth; i++)
                for (int j = 0; j < GridHeight; j++)
                    wallsGrid[i, j] = true;

            Vector2Int mazeChunkSize = new Vector2Int(colWidth, rowHeight);
            Vector2Int halfMazeChunkSize = mazeChunkSize / 2; // integerDivision

            //string s = "";
            for (int j = 0; j < mazeRows; j++)
            {
                //s += "\r\n";
                for (int i = 0; i < mazeCols; i++)
                {
                    //s += maze[i, mazeRows - j - 1].IsWall ? "#" : ".";
                    var node = maze[i, j];
                    Vector2Int wallPos = node.MazePosition * mazeChunkSize + halfMazeChunkSize;
                    foreach (var parent in node.Connections)
                    {
                        var connectDirection = (parent.MazePosition - node.MazePosition).normalizedInt;
                        if (connectDirection.x == 0) // direction is vertical or zero
                        {
                            if (connectDirection.y == 0) // the player's starting area/tunnel is the node at 0,0 whose connection is itself
                            {
                                // create a little horizontal tunnel for the player to start in, similar to the start of a vanilla level
                                int tunnelThickness = minHorizontalTunnelThickness;
                                int halfThickness = tunnelThickness / 2;
                                for (int y = -halfThickness; y < tunnelThickness - halfThickness; y++)
                                {
                                    for (int x = 0; x < wallPos.x; x++)
                                    {
                                        wallsGrid[x, y + wallPos.y] = false;
                                    }
                                }
                            }
                            else // vertical tunnel
                            {
                                //Debug.Log("Vertical");
                                int tunnelThickness = rng.Next(minVerticalTunnelThickness, colWidth - 1);
                                int halfThickness = tunnelThickness / 2; // integer division!
                                int offset = 0;
                                if (tunnelThickness % 2 == 2) // if an even thickness, randomly offset by 1 because it can't be centered
                                    offset = rng.Next(0, 2); // upper bounds exclusive

                                for (int l = -halfThickness; l < tunnelThickness - halfThickness; l++)
                                {
                                    for (int k = 0; k <= rowHeight; k++)
                                    {
                                        var gapPos = wallPos + connectDirection * k;
                                        gapPos.x += l + offset;
                                        wallsGrid[gapPos.x, gapPos.y] = false;
                                    }
                                }
                            }
                        }
                        else // direction is horizontal
                        {
                            //Debug.Log("Horizontal");
                            int tunnelThickness = rng.Next(minHorizontalTunnelThickness, rowHeight - 1);
                            int halfThickness = tunnelThickness / 2; // integer division!
                            int offset = 0;
                            if (tunnelThickness % 2 == 2) // if an even thickness, randomly offset by 1 because it can't be centered
                                offset = rng.Next(0, 2); // upper bounds exclusive
                            for (int l = -halfThickness; l < tunnelThickness - halfThickness; l++)
                            {
                                for (int k = 0; k <= colWidth; k++)
                                {
                                    var gapPos = wallPos + connectDirection * k;
                                    gapPos.y += l + offset;
                                    wallsGrid[gapPos.x, gapPos.y] = false;
                                }
                            }
                        }
                    }
                    wallsGrid[wallPos.x, wallPos.y] = node.IsWall;
                }
            }
            //TerrainGenerators.Log(s);
            // set player spawn to the lowest left-most air tile
            bool done = false;
            for (int x = 0; x < GridWidth && !done; x++)
                for (int y = 0; y < GridHeight && !done; y++)
                    if (!wallsGrid[x, y])
                    {
                        playerSpawn = new Vector2Int(x, y);
                        done = true;
                    }
            // create walls before trees because SpawnTrees modifies wallsGrid
            CreateWalls(wallsGrid, outerPadding);
            if(SpawnerScript.curBiome == 1 || SpawnerScript.curBiome == 10) // jungle or whisperwood
                SpawnTrees(ref wallsGrid, rng);
            // Minimap needs to be done after SpawnTrees
            CreateMinimapIfPresent(wallsGrid, outerPadding);
        }

        public override void PopulateLevel(RNG rng)
        {
            InstanceTracker.GameScript.StartCoroutine(SpawnVanillaSpawnables(rng, wallsGrid));
        }

        private void SpawnTrees(ref bool[,] wallsGrid, RNG rng)
        {
            const int minTreeHeight = 3;
            const int maxTreeHeight = 6;
            for (int x = 0; x < GridWidth; x++)
            {
                int lastGround = 0;
                int airCount = 0;
                for (int y = 0; y < GridHeight; y++)
                {
                    // if this position or horizontally adjacent are walls
                    if (wallsGrid.IsWall(x - 1, y) || wallsGrid.IsWall(x, y) || wallsGrid.IsWall(x + 1, y))
                    {
                        int bottomToTop = y - lastGround - 1;
                        // we need at least aircount=3 space below this wall for the top of the tree so the player can go around it
                        if (airCount >= 3 && bottomToTop >= minTreeHeight + 1)
                        {
                            int maxHeight = Mathf.Min(maxTreeHeight, bottomToTop - 1); // leave space on top
                            int minHeight = Mathf.Max(bottomToTop - airCount + 2, minTreeHeight); // plus 2 so players can get around from below
                            SpawnTree(x, lastGround + 1, rng.Next(minHeight, maxHeight + 1), wallsGrid, rng);
                        }
                        airCount = 0;

                        if (wallsGrid.IsWall(x, y))
                            lastGround = y;
                    }
                    else
                    {
                        airCount++;
                    }
                }
            }
        }

        private void SpawnTree(int x, int y, int height, bool[,] wallsGrid, RNG rng)
        {
            for (int i = 0; i < height - 1; i++)
            {
                int y2 = y + i;
                SpawnThing("log", x, y2, ref wallsGrid, rng);
                if (rng.Next(0, 3) == 1) // 1/3
                    SpawnThing("branchLeft", x, y2, ref wallsGrid, rng);
                if (rng.Next(0, 3) == 1) // 1/3
                    SpawnThing("branchRight", x, y2, ref wallsGrid, rng);
            }
            SpawnThing("treeTop", x, y + height - 1, ref wallsGrid, rng);
        }

        private void SpawnThing(string name, int x, int y, ref bool[,] wallsGrid, RNG rng)
        {
            const float leafZ = 1.9f;
            const float branchZ = 1.95f;
            const float logZ = 2f;
            float xSubpixelOffset = (x % 2) == 0 ? -1/16f : 1/16f; // for odd-width textures that need to be centered (everything else is on an even-width pixel grid)
            if (name == "log")
            {
                if (wallsGrid.IsWall(x, y))
                    TerrainGenerators.Log($"Not spawning {name} because there's already something here ({x},{y}).");
                string texName = "Log";
                if (SpawnerScript.curBiome == 10) // Whisperwood
                    texName += "2";
                if (TerrainGenerators.OtherTextures.TryGetValue(texName + ".png", out Texture2D tex))
                {
                    GameObject tile = GameObject.Instantiate(TerrainGenerators.TilePrefab, (Vector3)GeneratorBase.WorldOffset + new Vector3(x * BlockSize + xSubpixelOffset, y * BlockSize, logZ), Quaternion.Euler(180, 0, 0));
                    Spawned.Add(tile);
                    tile.transform.localScale = new Vector3(9f / 64f, 1, 1) * BlockSize / 2;
                    tile.GetComponent<MeshRenderer>().material.SetTexture("_MainTex", tex);
                    wallsGrid[x, y] = true;
                }
                else
                    TerrainGenerators.Log("Unable to spawn log because " + texName + ".png was not found.");
                return;
            }
            else if (name == "branchLeft")
            {
                if (wallsGrid.IsWall(x - 1, y))
                    return;// TerrainGenerators.Log($"Not spawning {name} because there's already something here ({x - 1},{y}).");
                string branchName = "BranchLeft";
                if (SpawnerScript.curBiome == 10) // Whisperwood
                    branchName += "2";
                if (TerrainGenerators.OtherTextures.TryGetValue(branchName + ".png", out Texture2D tex))
                {
                    float branchOffset = 0;//Mathf.FloorToInt(rng.Next(-0.25f, 0.25f) * BlockSize * 8) / 8f + 1/16f; // up to 25% of a block vertically
                    GameObject tile = GameObject.Instantiate(TerrainGenerators.TilePrefab, (Vector3)GeneratorBase.WorldOffset + new Vector3((x - 0.25f) * BlockSize, y * BlockSize + branchOffset, branchZ), Quaternion.Euler(180, 0, 0));
                    Spawned.Add(tile);
                    tile.transform.localScale = new Vector3(0.5f, 26f / 128f, 1) * BlockSize / 2;
                    tile.GetComponent<MeshRenderer>().material.SetTexture("_MainTex", tex);
                    string leafName = "TreeLeaf";
                    if (SpawnerScript.curBiome == 10) // Whisperwood
                        leafName += "2";
                    if (TerrainGenerators.OtherTextures.TryGetValue(leafName + ".png", out Texture2D tex2))
                    {
                        GameObject tile2 = GameObject.Instantiate(TerrainGenerators.TilePrefab, (Vector3)GeneratorBase.WorldOffset + new Vector3(Mathf.RoundToInt((x - 0.75f + rng.Next(0, 0.25f)) * BlockSize * 8f) / 8f, y * BlockSize + branchOffset, leafZ), Quaternion.Euler(180, 0, 0));
                        Spawned.Add(tile2);
                        tile2.transform.localScale = new Vector3(0.5f, 54f / 128f, 1) * BlockSize / 2;
                        tile2.GetComponent<MeshRenderer>().material.SetTexture("_MainTex", tex2);
                    }
                }
                else
                    TerrainGenerators.Log("Unable to spawn branch because " + branchName + ".png was not found.");
                return;
            }
            else if (name == "branchRight")
            {
                if (wallsGrid.IsWall(x + 1, y))
                    return;// TerrainGenerators.Log($"Not spawning {name} because there's already something here ({x + 1},{y}).");
                string texName = "BranchRight";
                if (SpawnerScript.curBiome == 10) // Whisperwood
                    texName += "2";
                if (TerrainGenerators.OtherTextures.TryGetValue(texName + ".png", out Texture2D tex))
                {
                    float branchOffset = 0;//Mathf.FloorToInt(rng.Next(-0.25f, 0.25f) * BlockSize * 8) / 8f + 1 / 16f; // up to 25% of a block vertically
                    GameObject tile = GameObject.Instantiate(TerrainGenerators.TilePrefab, (Vector3)GeneratorBase.WorldOffset + new Vector3((x + 0.25f) * BlockSize, y * BlockSize + branchOffset, branchZ), Quaternion.Euler(180, 0, 0));
                    Spawned.Add(tile);
                    tile.transform.localScale = new Vector3(0.5f, 26f / 128f, 1) * BlockSize / 2;
                    tile.GetComponent<MeshRenderer>().material.SetTexture("_MainTex", tex);
                    string leafName = "TreeLeaf";
                    if (SpawnerScript.curBiome == 10) // Whisperwood
                        leafName += "2";
                    if (TerrainGenerators.OtherTextures.TryGetValue(leafName + ".png", out Texture2D tex2))
                    {
                        GameObject tile2 = GameObject.Instantiate(TerrainGenerators.TilePrefab, (Vector3)GeneratorBase.WorldOffset + new Vector3(Mathf.RoundToInt((x + 0.75f + rng.Next(-0.25f, 0)) * BlockSize * 8) / 8f, y * BlockSize + branchOffset, leafZ), Quaternion.Euler(180, 0, 0));
                        Spawned.Add(tile2);
                        tile2.transform.localScale = new Vector3(0.5f, 54f / 128f, 1) * BlockSize / 2;
                        tile2.GetComponent<MeshRenderer>().material.SetTexture("_MainTex", tex2);
                    }
                }
                else
                    TerrainGenerators.Log("Unable to spawn branch because " + texName + ".png was not found.");
                return;
            }
            else if (name == "treeTop")
            {
                if (wallsGrid.IsWall(x, y))
                    TerrainGenerators.Log($"treeTop spawning at ({x},{y}) but there's already something there!"); // no return
                string texName = "TreeTop";
                if (SpawnerScript.curBiome == 10) // Whisperwood
                    texName += "2";
                if (TerrainGenerators.OtherTextures.TryGetValue(texName + ".png", out Texture2D tex))
                {
                    SpawnWall(x, y, tex);
                    wallsGrid[x, y] = true;
                }
                else
                    TerrainGenerators.Log("Unable to spawn treeTop because " + texName + ".png was not found.");
                return;
            }
        }

        /*
        private Node[,] CreateMaze2(RNG rng)
        {
            Node[,] maze = new Node[mazeCols, mazeRows];
            int pathLevel = 0;
            for (int i = 0; i < mazeCols; i++)
            {
                // pathLevel can go up or down one in each column
                pathLevel += rng.Next(-1, 2);
                for (int j = 0; j < mazeRows; j++)
                {
                    bool isWall = j == pathLevel;
                    maze[i, j] = new Node(new Vector2Int(i, j), isWall);
                }
            }


            return maze;

        }
        */

        private Node[,] CreateMaze(RNG rng)
        {
            Node[,] maze = new Node[mazeCols, mazeRows];
            List<Node> frontierNodes = new List<Node>();
            List<Node> connectedNodes = new List<Node>();

            /*
            for (int i = 0; i < mazeCols; i++)
            {
                bool isMidWall = rng.Next(0f, 1f) < 0.3f; // chance to make the middle section a wall
                for (int j = 0; j < mazeRows; j++)
                {
                    maze[i, j] = new Node(new Vector2Int(i, j), false);
                    if (j == mazeRows / 2 && isMidWall)
                        maze[i, j].IsWall = true;
                }
            }
            */
            int pathLevel = 0;
            int prevPathLevel = 0;
            for (int i = 0; i < mazeCols; i++)
            {
                for (int j = 0; j < mazeRows; j++)
                {
                    bool isWall = (j > pathLevel && j > prevPathLevel) || (j < pathLevel && j < prevPathLevel); // true when not between or at path levels
                    //if(rng.Next(0f, 1f) < 0.1f)  // additional chance to not be wall
                    //    isWall = false;
                    maze[i, j] = new Node(new Vector2Int(i, j), isWall); // the first column will always have only room 0,0 as non-wall
                }
                prevPathLevel = pathLevel;
                pathLevel = rng.Next(0, mazeRows);
                if (pathLevel == prevPathLevel) // reroll to reduce flatness
                    pathLevel = rng.Next(0, mazeRows);
            }


            Node startNode = maze[0, 0];
            startNode.Connections.Add(startNode); // when we get frontier nodes we check they don't have a parent
            connectedNodes.Add(startNode);
            AddSurroundingFrontierNodes(startNode, maze, ref frontierNodes);

            while (frontierNodes.Count > 0)
            {
                Node toConnect = frontierNodes[rng.Next(0, frontierNodes.Count)];
                List<Node> possibleConnections = GetPossibleConnections(maze, connectedNodes, toConnect);
                //Debug.Log(possibleConnections.Count);
                if (possibleConnections.Count == 0)
                {
                    frontierNodes.Remove(toConnect);
                    continue;
                }
                Node connection = possibleConnections[rng.Next(0, possibleConnections.Count)];
                toConnect.Connections.Add(connection);
                connectedNodes.Add(toConnect);
                frontierNodes.Remove(toConnect);
                AddSurroundingFrontierNodes(toConnect, maze, ref frontierNodes);
            }

            // reduce the maze-ness by performing some additional connections randomly
            const float additionalConnectionChance = 0.2f;
            foreach (var node in connectedNodes)
            {
                if (rng.Next(0f, 1f) < additionalConnectionChance)
                {
                    // pick one random adjacent node to connect to
                    var options = GetPossibleConnections(maze, connectedNodes, node)
                        .Where(n => !node.Connections.Contains(n) && !n.Connections.Contains(node))
                        .ToList();
                    if (options.Count > 0)
                    {
                        var chosen = options[rng.Next(0, options.Count)];
                        node.Connections.Add(chosen);
                    }
                }
            }

            /*
            // connect all nodes on the outside columns to the bottom node of those columns
            for (int y = 1; y < mazeRows; y++)
            {
                maze[0, y].Connections.Add(maze[0, 0]);
                maze[0, y].IsWall = false;
                maze[mazeCols - 1, y].Connections.Add(maze[mazeCols - 1, 0]);
                maze[mazeCols - 1, y].IsWall = false;
            }
            */

            return maze;
        }

        private List<Node> GetPossibleConnections(Node[,] maze, List<Node> connectedNodes, Node toConnect)
        {
            List<Node> possibleConnections = new List<Node>();
            if (toConnect.MazePosition.x > 0)
                possibleConnections.Add(maze[toConnect.MazePosition.x - 1, toConnect.MazePosition.y]);
            if (toConnect.MazePosition.x < mazeCols - 1)
                possibleConnections.Add(maze[toConnect.MazePosition.x + 1, toConnect.MazePosition.y]);
            if (toConnect.MazePosition.y > 0)
                possibleConnections.Add(maze[toConnect.MazePosition.x, toConnect.MazePosition.y - 1]);
            if (toConnect.MazePosition.y < mazeRows - 1)
                possibleConnections.Add(maze[toConnect.MazePosition.x, toConnect.MazePosition.y + 1]);
            //Debug.Log(possibleConnections.Count);
            // filter down to only non-walls that are part of the maze
            possibleConnections = possibleConnections.Where(n => !n.IsWall && connectedNodes.Contains(n)).ToList();
            return possibleConnections;
        }

        public void AddSurroundingFrontierNodes(Node newlyConnected, Node[,] maze, ref List<Node> frontierNodes)
        {
            TryAddToFrontier(newlyConnected.MazePosition + new Vector2Int(1, 0), maze, ref frontierNodes);
            TryAddToFrontier(newlyConnected.MazePosition + new Vector2Int(-1, 0), maze, ref frontierNodes);
            TryAddToFrontier(newlyConnected.MazePosition + new Vector2Int(0, 1), maze, ref frontierNodes);
            TryAddToFrontier(newlyConnected.MazePosition + new Vector2Int(0, -1), maze, ref frontierNodes);
        }
        public void TryAddToFrontier(Vector2Int position, Node[,] maze, ref List<Node> frontierNodes)
        {
            if (position.x < 0)
                return;
            if (position.y < 0)
                return;
            if (position.x >= maze.GetLength(0))
                return;
            if (position.y >= maze.GetLength(1))
                return;
            var node = maze[position.x, position.y];
            if (node.IsWall)
                return;
            if((node.Connections == null || node.Connections.Count == 0) && !frontierNodes.Contains(node))
                frontierNodes.Add(node);
        }

        public class Node
        {
            public Vector2Int MazePosition;
            public bool IsWall;
            // the first connection is the parent
            public List<Node> Connections = new List<Node>();

            public Node(Vector2Int gridPosition, bool isWall, Node connection = null)
            {
                this.MazePosition = gridPosition;
                this.IsWall = isWall;
                if(connection != null)
                    Connections.Add(connection);
            }
        }
    }
}
