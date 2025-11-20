using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TerrainGenerators.Patches;
using UnityEngine;

namespace TerrainGenerators.Scripts
{
    public class RPCHandler : MonoBehaviour
    {
        [RPC]
        public void GenerateWorldClient(int seed, int biome)
        {
            Patch_SpawnerScript_World.GenerateWorldClient(seed, biome);
        }
    }
}
