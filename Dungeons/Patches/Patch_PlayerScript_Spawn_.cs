using GadgetCore;
using GadgetCore.API;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;
using TerrainGenerators.Generators;
using TerrainGenerators.Helpers;
using UnityEngine;

namespace TerrainGenerators.Patches
{
    [HarmonyPatch()]
    [HarmonyGadget("TerrainGenerators")]
    public static class Patch_PlayerScript_Spawn_
    {
        static string[] solidSpawnMethodName = new string[] { "SpawnBully2", "SpawnDemon2", "SpawnGolem2", "SpawnMightShroom2" };
        //static string[] passableSpawnMethodName = new string[] { "SpawnPlague2", "SpawnMykonogre2", "SpawnMoloch2", "SpawnIronclad2", "SpawnGlaedria2", "SpawnFellbug2", "SpawnExodus2", "SpawnDragon2", "SpawnDestroyer", "SpawnDemon2", "SpawnCatastrophia2", "SpawnApocalypse2"};
        [HarmonyTargetMethod]
        public static IEnumerable<MethodBase> TargetMethods()
        {
            var nestedTypes = typeof(PlayerScript).GetNestedTypes(BindingFlags.NonPublic);
            foreach (var type in nestedTypes)
            {
                foreach (var methodName in solidSpawnMethodName)//.Concat(passableSpawnMethodName))
                {
                    if (type.Name.Contains(methodName)) // note: Make sure a methodName can't match multiple methods!
                    {
                        //TerrainGenerators.Log("Found " + type.Name);
                        yield return type.GetMethod("MoveNext", BindingFlags.Public | BindingFlags.Instance);
                    }
                }
            }
            yield return typeof(PlayerScript).GetMethod(nameof(PlayerScript.Treaty), BindingFlags.Public | BindingFlags.Instance);            
        }

        public static MethodInfo GetBossSpawnPosMethod = typeof(Patch_PlayerScript_Spawn_).GetMethod(nameof(GetBossSpawnPosition), BindingFlags.Public | BindingFlags.Static);
        public static MethodInfo AddSpawnToListMethod = typeof(Patch_PlayerScript_Spawn_).GetMethod(nameof(AddSpawnToList), BindingFlags.Public | BindingFlags.Static);
        [HarmonyTranspiler]
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, MethodBase __originalMethod)
        {
            TerrainGenerators.Log("Patching " + __originalMethod.Name);
            List<CodeInstruction> codes = instructions.ToList();
            for (int i = 0; i < codes.Count - 3; i++)
            {
                if (codes[i]?.opcode == OpCodes.Ldstr // spawn's name
                    && codes[i + 1]?.opcode == OpCodes.Call) // resources.load
                {
                    // In vanilla, after loading the resource, the player's position is read and some amount added or
                    // subtracted from x and y to spawn the boss. We want to remove all of that up to the point where the
                    // Vector3 is constructed, to replace it with our own position.

                    // Look ahead until we find newobj Vector3
                    for (int j = i; j < codes.Count; j++)
                    {
                        if (codes[j]?.opcode == OpCodes.Newobj && codes[j].operand.ToString().Contains(".ctor(Single, Single, Single)"))
                        {
                            // Remove all instructions from i + 2 (start of getting player's position) to j (creating the vector3).
                            // Remove from the end because it's a list
                            for (int k = j; k >= i + 2; k--)
                            {
                                codes.RemoveAt(k);
                            }
                            // This method returns a Vector3 onto the stack
                            codes.Insert(i + 2, new CodeInstruction(OpCodes.Call, GetBossSpawnPosMethod));

                            // look for the pop that removes the spawned object from the stack
                            for (int k = i + 2; k < codes.Count; k++)
                            {
                                if (codes[k]?.opcode == OpCodes.Pop)
                                {
                                    // Replace with a method that adds the spawned object to the generator's spawned list
                                    codes[k] = new CodeInstruction(OpCodes.Call, AddSpawnToListMethod);
                                    //TerrainGenerators.Log("Replaced pop");
                                    break;
                                }
                            }
                            //TerrainGenerators.Log("Patched spawn of " + codes[i].operand);
                            goto End;
                        }
                    }
                    // We didn't find the vector3 construction
                    TerrainGenerators.Log("Failed to patch spawn of " +  codes[i].operand);
                }
            }
            // Never even found the resources.load
            TerrainGenerators.Log("Couldn't find Resources.Load for a Spawn2 Method!");
        End:
            {
                //foreach (var code in codes)
                //   TerrainGenerators.Log(code?.opcode + "   " + code?.operand?.ToString());
                return codes;
            }
        }

        public static Vector3 GetBossSpawnPosition()
        {
            var generator = Patch_SpawnerScript_World.CurrentGenerator;
            Vector3 playerWorldPos;
            if (PlayerScript.beaming) // if the level hasn't started yet, spawn relative to the spawn point, else the player.
                playerWorldPos = (Vector3)GeneratorBase.WorldOffset + generator.PlayerSpawn * generator.BlockSize;
            else
                playerWorldPos = InstanceTracker.PlayerScript.transform.position;
            float targetDistance = Mathf.Min(48, generator.GridWidth * generator.BlockSize / 2f);
            float closestDistToTarget = float.PositiveInfinity;
            Vector3? closestToTarget = null;
            for (int x = 0; x < generator.GridWidth; x++) {
                for (int y = 0; y < generator.GridHeight - 1; y++)
                {

                    // Consider wall tiles with air above them to spawn on.

                    // If this is air or the tile above it is a wall, the space above this position won't be a valid spawn spot
                    if (generator.WallsGrid[x, y] == false || generator.WallsGrid[x, y + 1] == true)
                        continue;
                    // don't spawn at a teleporter
                    if (generator.TeleporterPositions.Any(p => p.x == x && p.y == y))
                        continue;
                    Vector3 airSpawnWorldPos = (Vector3)GeneratorBase.WorldOffset + new Vector3(x, y + 1f) * generator.BlockSize; // this spawns it in the middle of the chunk, which is annoying with Baby
                    float distToTarget = Mathf.Abs(Vector3.Distance(airSpawnWorldPos, playerWorldPos) - targetDistance);
                    if (distToTarget < closestDistToTarget)
                    {
                        closestDistToTarget = distToTarget;
                        closestToTarget = airSpawnWorldPos;
                    }
                }
            }
            if (closestToTarget != null)
            {
                return closestToTarget.Value;
            }
            // else
            TerrainGenerators.InternalLogger.LogError("No walls on planet " + SpawnerScript.curBiome + " before boss spawn pos requested!");
            return playerWorldPos + targetDistance * new Vector3(0.8f, 0.2f);
        }

        public static void AddSpawnToList(GameObject spawn)
        {
            Patch_SpawnerScript_World.CurrentGenerator.Spawned.Add(spawn);
        }
    }
}
