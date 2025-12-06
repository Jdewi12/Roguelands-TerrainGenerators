using GadgetCore.API;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TerrainGenerators.Helpers;
using TerrainGenerators.Patches;
using UnityEngine;

namespace TerrainGenerators.Generators
{
    public class Canyon : GeneratorBase
    {
        public override Vector2Int PlayerSpawn => playerSpawn;
        private Vector2Int playerSpawn;
        //public override Color MinimapWallsColor => new Color(0f, 1f, 0f);
        public override int GridWidth => gridWidth;
        private int gridWidth;
        public override int GridHeight => gridHeight;
        private int gridHeight;
        public override bool[,] WallsGrid => wallsGrid;
        private bool[,] wallsGrid;

        public Canyon(int gridWidth = 32, int gridHeight = 24)
        {
            this.gridWidth = gridWidth;
            this.gridHeight = gridHeight;
        }

        public override void GenerateWalls(RNG rng)
        {
            //TerrainGenerators.Log("Generating Desolate Canyon");
            int[] terrainHeights = new int[GridWidth];
            for (int x = 0; x < GridWidth; x++)
            {
                terrainHeights[x] = CanyonParabola(x, GridWidth, GridHeight);
            }


            // When the parabola is so steep the height increases by more than 3 chunks, just make that height vertical.
            int halfWidth = GridWidth / 2;
            int prevLeftHeight = terrainHeights[halfWidth];
            int prevRightHeight = prevLeftHeight;
            int minX = 0; // both of these should be overwritten
            int maxX = GridWidth;
            for (int dist = 0; dist < halfWidth; dist++)
            {
                int leftOfCenter = halfWidth - 1 - dist;
                int rightOfCenter = halfWidth + dist;
                int leftHeight = terrainHeights[leftOfCenter];
                int leftDiff = leftHeight - prevLeftHeight;
                if (leftDiff > 3)
                {
                    terrainHeights[leftOfCenter] = GridHeight;
                    prevLeftHeight = -1; // ensure all further iterations do the same
                }
                else
                {
                    prevLeftHeight = leftHeight;
                    minX = leftOfCenter;
                }
                int rightHeight = terrainHeights[rightOfCenter];
                int rightDiff = rightHeight - prevRightHeight;
                if (rightDiff > 3)
                {
                    terrainHeights[rightOfCenter] = GridHeight;
                    prevRightHeight = -1; // ensure all further iterations do the same
                }
                else
                {
                    prevRightHeight = rightHeight;
                    maxX = rightOfCenter;
                }
            }
            int baseY = terrainHeights[GridWidth / 2]; // The lowest part of the parabola.
            int lastX = minX;
            int lastYIncrease = 0;
            for(bool done = false; !done;)
            {
                int xStep = rng.Next(2, 11);
                // The next x position to interpolate to
                int nextX = lastX + xStep;
                if (nextX >= maxX + 1)
                {
                    nextX = maxX + 1;
                    xStep = nextX - lastX;
                    done = true;
                }
                // The next y to interpolate to.
                // Note the bounds are effectively doubled in terms of max steepness because it could go from high to low or vice versa
                int max = Mathf.Min(GridHeight, terrainHeights[nextX - 1] + xStep + 1);
                int min = Mathf.Max(baseY, terrainHeights[nextX - 1] - xStep - 1);
                int nextY = rng.Next(
                    //Mathf.Max(0, Mathf.RoundToInt(terrainHeights[nextX - 1] - baseY * Mathf.Sin((nextX - 1) / ((gridWidth / 2) / Mathf.PI)))),
                    min,
                    max);

                for (int i = lastX; i < nextX; i++)
                {
                    int existingHeight = terrainHeights[i];
                    // interpolate linearly from lastY to nextY
                    //terrainHeights[i] = Mathf.RoundToInt(curHeight + (nextY - curHeight) * ((float)(i - lastX) / (nextX - lastX)) + lastYIncrease * (1 - (float)(i - lastX) / (nextX - lastX)));
                    terrainHeights[i] += Mathf.RoundToInt(Mathf.Lerp(
                        lastYIncrease,
                        nextY - existingHeight,
                        (float)(i - lastX) / (nextX - lastX)));
                    // add slight variation within the interpolation
                    terrainHeights[i] += Mathf.RoundToInt(rng.Next(-0.75f, 0.75f)); // 25% -1, 50% same, 25% +1
                    terrainHeights[i] = Mathf.Clamp(terrainHeights[i], 0, GridHeight);
                    if (i == nextX - 1)
                        lastYIncrease = terrainHeights[i] - existingHeight;
                }
                lastX = nextX;
            }

            // emphasize canyon shape
            //terrainHeights[minX] += 1;
            //terrainHeights[maxX] += 1;

            // Fill out wallsGrid
            wallsGrid = new bool[GridWidth, GridHeight];
            for (int x = 0; x < GridWidth; x++)
            {
                int height = terrainHeights[x];
                for (int y = 0; y < height; y++)
                {
                    wallsGrid[x, y] = true;
                }
            }

            playerSpawn = new Vector2Int(minX, terrainHeights[minX]);




            //GenerateFloatingIslands(rng);
            CreateMinimapIfPresent(wallsGrid);
            CreateWalls(wallsGrid);
        }

        private void GenerateFloatingIslands(RNG rng)
        {
            int numLayers = 2;
            int islandHeight = Mathf.Max(1, Mathf.Min(Mathf.FloorToInt(GridHeight / 2f / numLayers) - 3 + rng.Next(-1, 2), GridWidth / 6));
            for (int layer = 0; layer < numLayers; layer++)
            {
                int numIslands = rng.Next(1, 3);
                if (numIslands != 0) // currently unnecessary
                {
                    int islandWidth = Mathf.Max(1, Mathf.FloorToInt(GridWidth / 2f / numIslands) - 2 + rng.Next(-1, 2));

                    for (int island = 0; island < numIslands; island++)
                    {
                        float islandSeed = rng.Next(0, 2 * Mathf.PI);
                        for (int localX = 0; localX < islandWidth; localX++)
                        {
                            bool[] islandColumn = IslandColumn(localX, islandWidth, islandHeight, islandSeed);
                            for (int localY = 0; localY < islandHeight; localY++)
                            {
                                if (islandColumn[localY])
                                {
                                    int x;
                                    if (island == 0)
                                        x = Mathf.RoundToInt(0.25f * GridWidth) + localX + 1; // from 0.25w + 1 to 0.5w - 1 if 2 islands, or to 0.75w - 1 if 1 island
                                    else
                                        x = Mathf.RoundToInt(0.5f * GridWidth) + localX + 1; // from 0.5w + 1 to 0.75w - 1
                                    int y = localY + GridHeight / 2 + (islandHeight + 3) * layer;
                                    wallsGrid[x, y] = true;
                                }
                            }
                        }
                    }
                }
            }
        }

        public override void PopulateLevel(RNG rng)
        {
            InstanceTracker.GameScript.StartCoroutine(SpawnVanillaSpawnables(rng, wallsGrid));
            //SpawnGroundSpawnables(rng, wallsGrid);
            //SpawnAirSpawnables(rng, wallsGrid);
        }


        /// <param name="exponent">Higher number = more square. Should be even (odd numbers won't work, though some decimal ones should).</param>
        /// <returns></returns>
        public static int CanyonParabola(int x, int width, int height, int minY = 1, float exponent = 4f)
        {
            if (x < 1 || x > width - 2)
                return height;
            /* this parabola is more square than the one we're currently using
            //tan((pi*(x+w/2-2))/(w-2))^2/(48/h)+(w+16*h-34)/112 + 1
            float innertan = (Mathf.PI * (x + width / 2 - 2)) / (width - 2);
            if (Mathf.Abs(innertan - Mathf.PI / 2) < 0.00001) // if would be close to undefined/infinity
                return height;
            int res = Mathf.RoundToInt(Mathf.Pow(Mathf.Tan(innertan), 2) / (48 / height) + (width + 16 * height - 34) / 112 + 1);
            */
            // Desmos: \left(h-1\right)\left(\frac{\left(x-\frac{w}{2}\right)}{\frac{w}{2}}\right)^{4}+1
            int res = Mathf.RoundToInt(
                Mathf.Pow(x / ((width - 1) / 2f) - 1, exponent)
                * (height - minY) + minY);
            //TerrainGenerators.Log(res.ToString());
            if (res < 0)
                return 0;
            else if (res < height)
                return res;
            else
                return height;
        }

        public static bool[] IslandColumn(int x, int width, int height, float seed) // 0 < seed <= 2pi
        {
            // I don't think this sin function is doing what I want. In desmos it looks okay in deg rather than rad but applies basically no distortion
            // h*(sqrt((1-(2x/w-1)^2)/40)+sin((5x/w-k)/30)+1/15)
            // h*(-sqrt((1-(2x/w-1)^2)/3)+sin((10x/w-k)/15)-1/15)
            int top = Mathf.RoundToInt(height * (Mathf.Sqrt((1 - Mathf.Pow(2f * x / width - 1, 2)) / 40f) + Mathf.Sin(5f * x / width - seed) / 30 + 1 / 15f + 0.73f));
            int bot = Mathf.RoundToInt(height * (-Mathf.Sqrt((1 - Mathf.Pow(2f * x / width - 1, 2)) / 3f) + Mathf.Sin(10f * x / width - seed) / 15 - 1 / 15f + 0.73f));
            bool[] result = new bool[height];
            for (int i = bot; i < top; i++)
            {
                result[i] = true;
            }
            return result;
        }

        //private List<Spawnable> groundSpawns = new List<Spawnable>
        //{
        //    new Spawnable("e/shmoo", 13.125, 0.375f),
        //    new Spawnable("e/wasp", 7.875, 1),
        //    new Spawnable("e/spider", 9, 0.375f),
        //    new Spawnable("obj/chest", 0.933, 0.375f),
        //    new Spawnable("obj/chestGold", 0.067, 0.375f),
        //    new Spawnable("haz/haz0", 9, 0.375f),
        //    new Spawnable("obj/wormEgg1", 10, 0.375f),
        //    new Spawnable("obj/wormEgg2", 10, 0.375f),
        //    new Spawnable("obj/tree0", 10, 0.375f),
        //    new Spawnable("obj/bugspot0", 10, 0.375f),
        //    new Spawnable("obj/plant0", 6, 0.375f),
        //    new Spawnable("obj/ore0", 9, 0.375f),
        //    new Spawnable("obj/ore1", 1, 0.375f),
        //    new Spawnable("npc/ringabolt", 3),
        //    new Spawnable("e/tyrannog", 0.5, 0.375f),
        //    new Spawnable("obj/relic", 0.5, 0.375f),

        //    new Spawnable("shop", 3, 0.375f)
        //};

        //public override List<Spawnable> AirSpawns => airSpawns;
        //private List<Spawnable> airSpawns = new List<Spawnable>
        //{
        //    new Spawnable("e/wasp", 2),
        //    new Spawnable("haz/haz0", 10),
        //    new Spawnable("", 88)
        //};
    }
}
