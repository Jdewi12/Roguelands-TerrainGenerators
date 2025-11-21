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
#pragma warning disable CS0618 // Type or member is obsolete
        [RPC]
#pragma warning restore CS0618 // Type or member is obsolete
        public void GenerateWorldClient(int seed, int biome)
        {
            Patch_SpawnerScript_World.GenerateWorldClient(seed, biome);
        }
    }
}
