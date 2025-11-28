using UnityEngine;
using HarmonyLib;
using GadgetCore.API;
using System.Linq;
using TerrainGenerators.Generators;

namespace TerrainGenerators.Patches
{
    [HarmonyPatch(typeof(PlayerScript))]
    [HarmonyPatch("Beam")]
    [HarmonyGadget("TerrainGenerators")]
    public static class Patch_PlayerScript_Beam
    {
        [HarmonyPostfix]
        public static void Postfix(int a)
        {
            //TerrainGenerators.Log("New World? " + Patch_SpawnerScript_World.NeedsToTeleport);
            // only teleport after a new world is generated
            if (!Patch_SpawnerScript_World.NeedsToTeleport)
                return;
            //TerrainGenerators.Log("Beam Prefix");

            //var entries = PlanetRegistry.Singleton.GetAllEntries();
            //if (entries.Any(planet => planet.GetID() == SpawnerScript.curBiome && planet.Type == PlanetType.SPECIAL))
            var entry = PlanetRegistry.Singleton.GetEntry(SpawnerScript.curBiome);
            if(entry != null && entry.Type == PlanetType.SPECIAL)
                return;
            //TerrainGenerators.Log($"GameScript.isTown: {GameScript.isTown}; GameScript.inInstance: {GameScript.inInstance}; SpawnerScript.curBiome: {SpawnerScript.curBiome}");
            if (!GameScript.isTown && GameScript.inInstance
                && SpawnerScript.curBiome != 8 // mech city
                && SpawnerScript.curBiome != 11 // old earth
                )
            {
                int blockSize = Patch_SpawnerScript_World.BlockSize;
                int spawnX = Patch_SpawnerScript_World.SpawnX;
                int spawnY = Patch_SpawnerScript_World.SpawnY;
                MenuScript.player.transform.position = (Vector3)GeneratorBase.WorldOffset + new Vector3((spawnX) * blockSize, (spawnY - 0.5f) * blockSize, MenuScript.player.transform.position.z);
                Patch_SpawnerScript_World.NeedsToTeleport = false;
                //TerrainGenerators.Log("Teleported player to " + (spawnX * blockSize) + ", " + ((spawnY - 0.5f) * blockSize));
            }
        }
    }
}