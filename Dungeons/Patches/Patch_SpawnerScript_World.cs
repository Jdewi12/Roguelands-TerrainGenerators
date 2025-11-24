using GadgetCore.API;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using TerrainGenerators.Generators;
using TerrainGenerators.Helpers;
using TerrainGenerators.Scripts;
using UnityEngine;
using UnityEngine.SocialPlatforms.Impl;

namespace TerrainGenerators.Patches
{
    [HarmonyPatch]
    [HarmonyGadget("TerrainGenerators")]
    public static class Patch_SpawnerScript_World
    {
        public static int BlockSize = 8;
        public static int GridWidth = 32;
        public static int GridHeight = 24;
        public static int SpawnX { get; private set; } // grid position
        public static int SpawnY { get; private set; }
        public static RNG RNG;
        //public static List<List<bool>> worldGrid = new List<List<bool>>();
        public static Dictionary<int, Dictionary<int, bool>> World = new Dictionary<int, Dictionary<int, bool>>(); // x,y,isWall
        public static List<KeyValuePair<int, int>> SpawnSpots = new List<KeyValuePair<int, int>>();
        public static List<GameObject> Spawned = new List<GameObject>();
        public static List<KeyValuePair<int, int>> Teleporters = new List<KeyValuePair<int, int>>();
        public static GeneratorBase CurrentGenerator = null;
        public static bool NeedsToTeleport = false;


        [HarmonyTargetMethod]
        public static MethodBase TargetMethod()
        {
            return typeof(SpawnerScript).GetNestedType("<World>c__Iterator2", BindingFlags.NonPublic).GetMethod("MoveNext", BindingFlags.Public | BindingFlags.Instance);
        }

        // Start of remove:
        // IL_027D: ldfld     int32 SpawnerScript::gridWidth
        // IL_0282: ldarg.0
        // IL_0283: ldfld     class SpawnerScript SpawnerScript/'<World>c__Iterator2'::$this
        // IL_0288: ldfld     int32 SpawnerScript::gridHeight
        // IL_028D: newobj    instance void int32[0..., 0...]::.ctor(int32, int32)

        // End of remove:
        // IL_082A: ldstr     "SpawnEndPortal"
        // IL_082F: callvirt  instance void [UnityEngine]UnityEngine.GameObject::SendMessage(string)

        [HarmonyTranspiler]
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            //TerrainGenerators.Log("Starting SpawnerScript_World Transpiler");
            MethodInfo myGenerator = typeof(Patch_SpawnerScript_World).GetMethod(nameof(GenerateWorldServer), BindingFlags.Public | BindingFlags.Static);
            List<Label> labels = new List<Label>();
            CodeInstruction[] codes = instructions.ToArray();
            List<CodeInstruction> newCodes = new List<CodeInstruction>();
            for (int i = 0; i < codes.Length - 6; i++)
            {
                if ((codes[i] != null && codes[i].opcode == OpCodes.Ldarg_0) &&
                    (codes[i + 1] != null && codes[i + 1].opcode == OpCodes.Ldarg_0) &&
                    (codes[i + 2] != null && codes[i + 2].opcode == OpCodes.Ldfld) &&
                    (codes[i + 3] != null && codes[i + 3].opcode == OpCodes.Ldfld && codes[i + 3].operand.ToString() == "System.Int32 gridWidth") &&
                    (codes[i + 4] != null && codes[i + 4].opcode == OpCodes.Ldarg_0) &&
                    (codes[i + 5] != null && codes[i + 5].opcode == OpCodes.Ldfld) &&
                    (codes[i + 6] != null && codes[i + 6].opcode == OpCodes.Ldfld) &&
                    (codes[i + 7] != null && codes[i + 7].opcode == OpCodes.Newobj))
                {
                    //TerrainGenerators.Log("Found GridWidth and grid initialisation!");
                    newCodes.Add(new CodeInstruction(OpCodes.Call,myGenerator));
                    int j = i + 1;
                    for (; j < codes.Length - 2; j++)
                    {
                        foreach (Label label in codes[j].labels)
                            labels.Add(label);
                        if ((codes[j] != null && codes[j].opcode == OpCodes.Ldstr && (string)codes[j].operand == "SpawnEndPortal")) 
                            // note: Right now it skips this instruction and the next, but we may want to just patch SpawnEndPortal or put a chunk on the stack.
                        {
                            j += 2;
                            foreach (Label label in labels)
                                codes[j].labels.Add(label); // todo: we're modifying codes here but if patching fails we return that?
                            //TerrainGenerators.Log("Moved Labels!");
                            break;
                        }
                    }
                    for (; j < codes.Length; j++)
                    {
                        if (j < codes.Length - 5 
                            && codes[j]?.opcode == OpCodes.Ldstr // "BABYSPAWN"
                            && codes[j + 1]?.opcode == OpCodes.Call // MonoBehaviour.print
                            && codes[j + 2]?.opcode == OpCodes.Ldarg_0 //
                            && codes[j + 3]?.opcode == OpCodes.Ldfld // $this
                            && codes[j + 4]?.opcode == OpCodes.Ldstr // spawn's name
                            && codes[j + 5]?.opcode == OpCodes.Call) // resources.load
                        {
                            newCodes.Add(codes[j + 2]);
                            newCodes.Add(codes[j + 3]);
                            newCodes.Add(codes[j + 4]);
                            newCodes.Add(codes[j + 5]);
                            for (int k = j + 6; k < codes.Length; k++)
                            {
                                if (codes[k]?.opcode == OpCodes.Newobj && codes[k].operand.ToString().Contains(".ctor(Single, Single, Single)"))
                                {

                                    newCodes.Add(new CodeInstruction(OpCodes.Call, Patch_PlayerScript_Spawn_.GetBossSpawnPosMethod));
                                    //TerrainGenerators.Log("Patched spawn of " + codes[j + 4].operand);
                                    j = k; // note: k is skipped because j is incremented by the loop
                                    break;
                                }
                            }
                        }
                        else
                        {
                            newCodes.Add(codes[j]);
                        }
                    }
                    break;
                }
                else
                {
                    newCodes.Add(codes[i]);
                }
            }
            if (newCodes.Count + 10 > codes.Count()) // if the expected large amount of code has not been removed, just return the original
            {
                TerrainGenerators.Log("FAILED TO PATCH SpawnerScript_World! CUSTOM PLANETS WILL NOT GENERATE!");
                foreach (CodeInstruction c in newCodes)
                    TerrainGenerators.Log(c.ToString());
                return codes;
            }
            //foreach (var code in newCodes)
            //    TerrainGenerators.Log(code?.opcode + "   " + code?.operand?.ToString());
            //TerrainGenerators.Log("Finished patching.");

            /*
            var debugMethod = typeof(Patch_SpawnerScript_World).GetMethod(nameof(DebugLogStep), BindingFlags.Public | BindingFlags.Static);
            for (int i = 0, count = newCodes.Count; i < count; i++)
            {
                newCodes.Insert(i * 3, new CodeInstruction(OpCodes.Ldc_I4, i));
                newCodes.Insert(i * 3 + 1, new CodeInstruction(OpCodes.Call, debugMethod));
            }
            */

            return newCodes.Where(x => x != null);
        }

        /// <summary>
        /// Generates walls and other things that should be duplicated between client and server but
        /// unlike things like enemies, don't need to be synced
        /// </summary>
        public static void GenerateWorldClient(int seed, int biome)
        {
            SpawnerScript.curBiome = biome;
            CurrentGenerator = SelectGeneratorForBiome();
            RNG = new RNG(seed);
            CurrentGenerator.GenerateWalls(RNG);
            SpawnX = CurrentGenerator.PlayerSpawn.x;
            SpawnY = CurrentGenerator.PlayerSpawn.y;
            NeedsToTeleport = true;
        }

        public static void DebugLogStep(int i)
        {
            TerrainGenerators.Log("step " + i);
            Debug.Log("step " + i);
        }

        public static void GenerateWorldServer()
        {
            if (!Network.isServer)
                return;
            if (PlanetRegistry.Singleton.GetAllEntries().Any(planet => planet.GetID() == SpawnerScript.curBiome && planet.Type == PlanetType.SPECIAL))
                return;

            //TerrainGenerators.Log("Starting custom generator");
            //TerrainGenerators.Log("Biome ID: " + SpawnerScript.curBiome);

            if (CurrentGenerator != null) // todo: is this necessary in case the player somehow left the planet without triggering DestroyWorld?
            {
                Patch_SpawnerScript_DestroyWorld.DestroyAllSpawned();
                CurrentGenerator = null;
            }
            if (TerrainGenerators.RPCHandler == null)
                TerrainGenerators.RPCHandler = ((GameObject)Network.Instantiate(Resources.Load("Jdewi/RPCHandler"), Vector3.zero, Quaternion.identity, 0))
                    .GetComponent<NetworkView>();
            int seed = UnityEngine.Random.Range(0, int.MaxValue);

            // AllBuffered includes self
#pragma warning disable CS0618 // Type or member is obsolete
            TerrainGenerators.RPCHandler.RPC(nameof(RPCHandler.GenerateWorldClient),RPCMode.AllBuffered, seed, SpawnerScript.curBiome);
#pragma warning restore CS0618 // Type or member is obsolete
                              //GenerateWorldClient(seed, SpawnerScript.curBiome); // generates walls and picks the generator
            CurrentGenerator.PopulateLevel(RNG);

            /*
            // //todo: make sure commented out
            // Debug code to spawn teleporters to all custom planets
            int offset = -6;
            foreach(var planet in PlanetRegistry.Singleton.GetAllEntries())
            { 
                GameObject gameObject = (GameObject)Network.Instantiate((GameObject)Resources.Load("portal"), CurrentGenerator.PlayerSpawn * CurrentGenerator.BlockSize + new Vector3(offset - 0.5f, 1.45f, 0.2f), Quaternion.identity, 0);
                GameObject gameObject2 = gameObject.transform.GetChild(0).gameObject;
                gameObject.GetComponent<NetworkView>().RPC("Activate", RPCMode.All, new object[0]);
                gameObject2.GetComponent<NetworkView>().RPC("Set", RPCMode.AllBuffered, new object[]
                {
                        planet.GetID(),
                        0,
                        5
                });
                offset += 3;
            }
            */
        }

        private static GeneratorBase SelectGeneratorForBiome()
        {
            switch (SpawnerScript.curBiome)
            {
                case 0: // Desolate Canyon
                    return new DesolateCanyon();
                    //return new Rooms(); // TODO: FIX THIS!!!!!
                case 1: // Deep Jungle
                    return new MagiciteLike();
                case 2: // Hollow Caverns
                    return new Caves();
                case 3: // Shroomtown
                    return new MagiciteLike();
                case 4: // Ancient Ruins
                    return new MagiciteLike();
                case 5: // Plaguelands
                    return new DesolateCanyon();
                case 6: // Byfrost
                    return new Caves();
                case 7: // Molten Crag
                    return new Caves();
                //case 8: // Mech City
                case 9: // Demon's Rift
                    return new MagiciteLike();
                case 10: // Whisperwood
                    return new MagiciteLike();
                // case 11: // Old Earth
                case 12: // Forbidden Arena
                    return new MagiciteLike();
                case 13: // Cathedral
                    return new Rooms();
                default:
                    return new Caves();
            }
        }
    }
}