using GadgetCore.API;
using HarmonyLib;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using TerrainGenerators.Scripts;
using UnityEngine;

namespace TerrainGenerators.Patches
{
    [HarmonyPatch(typeof(SpawnerScript))]
    [HarmonyPatch("DestroyWorld")]
    [HarmonyGadget("TerrainGenerators")]
    public static class Patch_SpawnerScript_DestroyWorld
    {
        [HarmonyPostfix]
        public static void Prefix()
        {
            if (Patch_SpawnerScript_World.CurrentGenerator == null)
                return;
            DestroyAllSpawned();
            Patch_SpawnerScript_World.CurrentGenerator = null;
            //GameScript.districtLevel += 1;

            return;
        }

        public static void DestroyAllSpawned()
        {
            if (TerrainGenerators.RPCHandler != null)
            {
                // cancel any buffered RPC calls (e.g. to generate terrain)
                Network.RemoveRPCs(TerrainGenerators.RPCHandler.viewID);
            }
            InstanceTracker.GameScript.StartCoroutine(DestroyAllSpawned2());
        }

        private static IEnumerator DestroyAllSpawned2()
        {
            var generator = Patch_SpawnerScript_World.CurrentGenerator; // make a copy before waiting, in case it's replaced/nulled after this is called
            if (generator == null)
                yield break;
            yield return new WaitForSeconds(0.5f); // wait some time for camera to fade out
            var spawned = generator.Spawned;
            for (int i = 0; i < spawned.Count; i++)
            {
                GameObject spawn = spawned[i];
                if (spawn != null)
                {
                    if (spawn.GetComponent<NetworkView>() != null)
                        Network.Destroy(spawn);
                    else
                        GameObject.Destroy(spawn);
                }

            }
            if(generator.ChunkScript != null)
                GameObject.Destroy(generator.ChunkScript); // note: Chunk's OnDestroy
        }
    }
}