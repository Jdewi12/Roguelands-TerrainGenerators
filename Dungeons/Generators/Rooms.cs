using GadgetCore.API;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TerrainGenerators.Helpers;

namespace TerrainGenerators.Generators
{
    public class Rooms : GeneratorBase
    {
        public override bool[,] WallsGrid => wallsGrid;
        private bool[,] wallsGrid;

        public override Vector2Int PlayerSpawn => playerSpawn;
        private Vector2Int playerSpawn;

        public override void GenerateWalls(RNG rng)
        {

            CreateWalls(wallsGrid);
            CreateMinimapIfPresent(wallsGrid);
        }

        public override void PopulateLevel(RNG rng)
        {
            InstanceTracker.GameScript.StartCoroutine(SpawnVanillaSpawnables(rng, wallsGrid));
        }
    }
}
