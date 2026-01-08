using GadgetCore;
using GadgetCore.API;
using GadgetCore.Util;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using TerrainGenerators.Generators;
using TerrainGenerators.Helpers;
using TerrainGenerators.Patches;
using TerrainGenerators.Scripts;
using UnityEngine;

namespace TerrainGenerators
{
    [Gadget("TerrainGenerators", RequiredOnClients: true)]
    public class TerrainGenerators : Gadget
    {
        public static AssetBundle TerrainGeneratorsAssetBundle;
        public static GameObject TilePrefab;

        public static Dictionary<int, Dictionary<byte, Texture2D>> GeneratedTextures = new Dictionary<int, Dictionary<byte, Texture2D>>();
        public static Dictionary<string, Texture2D> OtherTextures = new Dictionary<string, Texture2D>();
        public static NetworkView RPCHandler;
        public static Thread TextureThread = null;

        public const string MOD_VERSION = "1.3";
        public const string CONFIG_VERSION = "1.0";

        internal static MinimapAPI MinimapAPI;
        internal static GadgetLogger InternalLogger;
        internal static GadgetConfig InternalConfig;
        public static bool TexturesReady = false;

        protected override void LoadConfig()
        {
            InternalLogger = Logger;
            InternalConfig = Config;
            Log("TerrainGenerators V" + MOD_VERSION);
            Config.Load();

            string fileVersion = Config.ReadString("ConfigVersion", CONFIG_VERSION, comments: "The Config Version (not to be confused with mod version)");

            if (fileVersion != CONFIG_VERSION)
            {
                Config.Reset();
                Config.WriteString("ConfigVersion", CONFIG_VERSION, comments: "The Config Version (not to be confused with mod version)");
            }

            Config.Save();
        }

        public override string GetModDescription()
        {
            return "Adds terrain generators."; // TODO: Write a better gadget description
        }
        
        internal static void Log(string text)
        {
            InternalLogger.Log(text);
        }        

        IEnumerator SetAllPortalUses()
        {
            yield return new WaitForSeconds(12f);
            for (int i = 0; i < 99; i++)
                PreviewLabs.PlayerPrefs.SetInt("portalUses" + i, 99); //FOR TESTING ONLY
            foreach(var planetInfo in PlanetRegistry.Singleton.GetAllEntries())
            {
                PreviewLabs.PlayerPrefs.SetInt("portalUses" + planetInfo.GetID(), 99); //FOR TESTING ONLY
            }
            Log("SET ALL PORTAL USES");
        }

        protected override void Initialize()
        {
            //new GameObject().AddComponent<Chunk>().StartCoroutine(SetAllPortalUses()); // todo: remove

            var prefab = new GameObject("Jdewi Terrain Gen RPCs");
            prefab.AddComponent<RPCHandler>();
            GadgetCoreAPI.AddCustomResource("Jdewi/RPCHandler", prefab);
            //GameObject.Destroy(prefab);

            UMFZipUtils.UnpackMod("TerrainGenerators");
            string filePath = Path.Combine(Path.Combine(Path.Combine(GadgetPaths.ModsPath, "Assets"), "TerrainGenerators"), "TerrainGenerators.assetbundle");
            if (File.Exists(filePath))
            {
                byte[] fileData = File.ReadAllBytes(filePath);
                TerrainGeneratorsAssetBundle = AssetBundle.LoadFromMemory(fileData);
            }

            TilePrefab = TerrainGeneratorsAssetBundle.LoadAsset<GameObject>("assets/jdewi/tile.prefab");

            // testing fix for linux:
            //TilePrefab.GetComponent<MeshRenderer>().material = null;
            var matToUse = Resources.Load<GameObject>("z/midChunk0").GetComponent<Renderer>().material;
            TilePrefab.GetComponent<MeshRenderer>().material = matToUse;


            GadgetCoreAPI.AddCustomResource("Jdewi/Tile", TilePrefab);
            LoadPackedTextures();

            var dummy = new GameObject("Jdewi Dummy");
            GameObject.DontDestroyOnLoad(dummy); // so the coroutine keeps running if the player goes from menu to game scene before done
            var dummyMB = dummy.AddComponent<DummyMonoBehaviour>();
            dummyMB.StartCoroutine(DelayedPlanetHandling(destroyWhenDone: dummy));

            if (Gadgets.GetGadget("Minimap") != null)
            {

                MinimapAPI = new MinimapAPI();
            }

            Log("Gadget Initialised");
        }

        public static IEnumerator DelayedPlanetHandling(GameObject destroyWhenDone)
        {
            Stopwatch sw = new Stopwatch();
            yield return null; // wait till after the first frame, to make sure mods have all registered their custom planets
            sw.Start();
            if (GeneratedTextures.Count == 0)
            {
                ModdedTextures.LoadModdedTextures();
                sw.Stop();
                Log("Loaded modded textures in " + sw.ElapsedMilliseconds + "ms on main thread");
                yield return GenerateTextures();
            }


            //GenerateConfig();

            if(destroyWhenDone != null)
                GameObject.Destroy(destroyWhenDone);
        }

        public static void GenerateConfig() // unfinished and unused.
        {
            foreach (int biomeID in Biomes)
            {
                string planetName;
                var planetInfo = PlanetRegistry.Singleton.GetEntry(biomeID);
                if (planetInfo != null)
                {
                    planetName = planetInfo.Name;
                }
                else
                {
                    switch (biomeID)
                    {
                        case 0:
                            planetName = "Desolate Canyon";
                            break;
                        //return new Rooms(); // TODO: FIX THIS!!!!!
                        case 1:
                            planetName = "Deep Jungle";
                            break;
                        case 2:
                            planetName = "Hollow Caverns";
                            break;
                        case 3:
                            planetName = "Shroomtown";
                            break;
                        case 4:
                            planetName = "Ancient Ruins";
                            break;
                        case 5:
                            planetName = "Plaguelands";
                            break;
                        case 6:
                            planetName = "Byfrost";
                            break;
                        case 7:
                            planetName = "Molten Crag";
                            break;
                        //case 8: // Mech City
                        case 9:
                            planetName = "Demon's Rift";
                            break;
                        case 10:
                            planetName = "Whisperwood";
                            break;
                        // case 11: // Old Earth
                        case 12:
                            planetName = "Forbidden Arena";
                            break;
                        case 13:
                            planetName = "Cathedral";
                            break;
                        default:
                            Log("Unknown planet id: " + biomeID);
                            continue;
                    }
                }
                var defaultGeneratorSettings = Patch_SpawnerScript_World.SelectGeneratorForBiome(biomeID);
                InternalConfig.ReadInt(planetName + " width", defaultValue: defaultGeneratorSettings.GridWidth, comments: "Width in chunks.");
                InternalConfig.Save();
            }
        }

        public static List<int> Biomes = new List<int>();
        public static Dictionary<int, Texture2D> TileCorners = new Dictionary<int, Texture2D>();
        public static Dictionary<int, Texture2D> TileFlats = new Dictionary<int, Texture2D>();
        public static Dictionary<int, Texture2D> TileInnerCorners = new Dictionary<int, Texture2D>();
        public static Dictionary<int, Texture2D> TileWalls = new Dictionary<int, Texture2D>();

        const string corner = "Corner";
        const string flat = "Flat";
        const string innerCorner = "InnerCorner";
        const string wall = "Wall";

        const int subTileSize = 32;
        const int fullTileSize = 2 * subTileSize;
        
        public static void LoadPackedTextures()
        {
            Log("Loading Textures.");
            string filesPath = Path.Combine(GadgetPaths.AssetsPath, "TerrainGenerators");
            string TerrainGeneratorsDll = Path.Combine(GadgetPaths.ModsPath, "TerrainGenerators.dll");
            string manifestIni = Path.Combine(GadgetPaths.ModsPath, "Manifest.ini");
            string modInfoTxt = Path.Combine(GadgetPaths.ModsPath, "ModInfo.txt");
            string pdb = Path.Combine(GadgetPaths.ModsPath, "TerrainGenerators.pdb");
            string unpackedAssetsPath = Path.Combine(GadgetPaths.ModsPath, "Assets");
            string unpackedInnerAssetsPath = Path.Combine(unpackedAssetsPath, "TerrainGenerators");


            if (File.Exists(TerrainGeneratorsDll))
                File.Delete(TerrainGeneratorsDll);
            else
                Log("Warning: File doesn't exist: " + TerrainGeneratorsDll);

            if (File.Exists(manifestIni))
                File.Delete(manifestIni);
            else
                Log("Warning: File doesn't exist: " + manifestIni);

            if (File.Exists(pdb))
                File.Delete(pdb);

            if (File.Exists(modInfoTxt))
                File.Delete(modInfoTxt);
            else
                Log("Warning: File doesn't exist: " + modInfoTxt);

            while (Directory.Exists(filesPath)) // replace previously unpacked files, in case of update
                Directory.Delete(filesPath, true);

            // probably better to keep track of version instead of replacing every time

            if (Directory.Exists(unpackedInnerAssetsPath))
                Directory.Move(unpackedInnerAssetsPath, filesPath);
            else
                Log("Error: Directory doesn't exist: " + unpackedInnerAssetsPath);

            if (Directory.Exists(unpackedAssetsPath))
                Directory.Delete(unpackedAssetsPath);
            else
                Log("Warning: File doesn't exist: " + filesPath);



            foreach (string imagePath in Directory.GetFiles(filesPath, "*.png"))
            {
                string fileName = Path.GetFileName(imagePath);
                // TryGetTexture loads the texture and adds it to the passed dictionary if its name starts with the check name
                if (!TryGetTexture(fileName, "TileCorner", ref TileCorners))
                    if (!TryGetTexture(fileName, "TileFlat", ref TileFlats))
                        if (!TryGetTexture(fileName, "TileInnerCorner", ref TileInnerCorners))
                            if (!TryGetTexture(fileName, "TileWall", ref TileWalls))
                            {
                                OtherTextures.Add(fileName, LoadTexture(fileName));
                                Log("Loaded non-tile texture: " + fileName);
                            }
            }
        }

        public static IEnumerator GenerateTextures()
        {
            Stopwatch sw = new Stopwatch();
            sw.Start();
            // Textures need to be created on the main thread.
            for (int i = 0; i < 256; i++)
            {
                foreach (int biomeID in Biomes)
                {
                    Texture2D tex = new Texture2D(fullTileSize, fullTileSize)
                    {
                        filterMode = FilterMode.Point
                    };
                    if (GeneratedTextures.TryGetValue(biomeID, out Dictionary<byte, Texture2D> dict2))
                    {
                        GeneratedTextures[biomeID].Add((byte)i, tex);
                    }
                    else
                    {
                        var innerDict = new Dictionary<byte, Texture2D> { { (byte)i, tex } };
                        GeneratedTextures.Add(biomeID, innerDict);
                    }
                }
            }
            Texture2D[] reusableSubTextures = new Texture2D[4];
            for (int i = 0; i < 4; i++)
                reusableSubTextures[i] = new Texture2D(subTileSize, subTileSize);

            var thread = new Thread(AsyncGeneration) { IsBackground = true };
            thread.Start();
            sw.Stop();
            bool errored = false;
            Log("Preparing blank textures took " + sw.ElapsedMilliseconds + "ms on main thread");
            // wait for texture generation to finish
            while (thread.IsAlive)
                yield return null;
            if (errored)
            {
                InternalLogger.LogWarning("Async errored; trying non-async");
                AsyncGeneration(); // run on main thread
            }

            sw.Reset();
            sw.Start();
            // Textures need to applied on the main thread.
            foreach(var dict in GeneratedTextures.Values) // each biome's texture dict
            {
                foreach(var texture in dict.Values)
                {
                    texture.Apply();
                }
            }

            TexturesReady = true;
            sw.Stop();
            Log("Applying generated textures took " + sw.ElapsedMilliseconds + "ms on main thread");

            void AsyncGeneration()
            {
                try
                {
                    Stopwatch sw2 = new Stopwatch();
                    sw2.Start();
                    // Relative tile positions:
                    // Tile 0   Tile 1   Tile 2
                    // Tile 3     ME     Tile 4
                    // Tile 5   Tile 6   Tile 7

                    // topRight = (tile1, tile2, tile4)
                    // bottomRight = (tile4, tile7, tile6).rotate(270)
                    // bottomLeft = (tile6, tile5, tile3).rotate(180)
                    // topLeft = (tile3, tile0, tile1).rotate(90)
                    Log("Generating Textures...");
                    bool[] tileCombination;
                    for (int i = 0; i < 256; i++)
                    {
                        tileCombination = ConvertToBoolArray(i);

                        // Corner, Flat, InnerCorner, or Wall
                        Tuple<string, int> topRightNameAndRot = DetermineSubtileType(tileCombination[1], tileCombination[2], tileCombination[4]);
                        Tuple<string, int> bottomRightNameAndRot = DetermineSubtileType(tileCombination[4], tileCombination[7], tileCombination[6]);
                        Tuple<string, int> bottomLeftNameAndRot = DetermineSubtileType(tileCombination[6], tileCombination[5], tileCombination[3]);
                        Tuple<string, int> topLeftNameAndRot = DetermineSubtileType(tileCombination[3], tileCombination[0], tileCombination[1]);
                        foreach (int biomeID in Biomes)
                        {
                            Texture2D tex = GeneratedTextures[biomeID][(byte)i];
                            Texture2D topRightTex = GetRotatedSourceTex(biomeID, 1, topRightNameAndRot, topLeftNameAndRot, bottomRightNameAndRot, bottomLeftNameAndRot, ref reusableSubTextures[0]);
                            Texture2D bottomRightTex = GetRotatedSourceTex(biomeID, 2, bottomRightNameAndRot, topRightNameAndRot, bottomLeftNameAndRot, topLeftNameAndRot, ref reusableSubTextures[1]);
                            Texture2D bottomLeftTex = GetRotatedSourceTex(biomeID, 3, bottomLeftNameAndRot, bottomRightNameAndRot, topLeftNameAndRot, topRightNameAndRot, ref reusableSubTextures[2]);
                            Texture2D topLeftTex = GetRotatedSourceTex(biomeID, 0, topLeftNameAndRot, bottomLeftNameAndRot, topRightNameAndRot, bottomRightNameAndRot, ref reusableSubTextures[3]);
                            // texture coordinates start in bottom left
                            for (int x = 0; x < subTileSize; x++)
                            {
                                for (int y = 0; y < subTileSize; y++)
                                {
                                    // why is everything inverted??? :(
                                    tex.SetPixel(fullTileSize - 1 - x, fullTileSize - 1 - y, bottomLeftTex.GetPixel(x, y));
                                    tex.SetPixel(subTileSize - 1 - x, fullTileSize - 1 - y, bottomRightTex.GetPixel(x, y));
                                    tex.SetPixel(fullTileSize - 1 - x, subTileSize - 1 - y, topLeftTex.GetPixel(x, y));
                                    tex.SetPixel(subTileSize - 1 - x, subTileSize - 1 - y, topRightTex.GetPixel(x, y));
                                }
                            }
                            GeneratedTextures[biomeID][(byte)i] = tex;
                        }
                    }
                    sw2.Stop();
                    Log("Generated textures for " + Biomes.Count + " planet(s) in " + sw2.ElapsedMilliseconds + "ms on background thread.");
                }
                catch(Exception e)
                {
                    errored = true;
                    InternalLogger.LogError(e.ToString());
                }
            }
        }

        static bool TryGetTexture(string fileName, string checkName, ref Dictionary<int, Texture2D> textureDictionary)
        {
            if (fileName.StartsWith(checkName))
            {
                //Log(fileName + " starts with " + checkName + ", adding as texture.");
                if (int.TryParse(fileName.Substring(checkName.Length, fileName.Length - checkName.Length - 4), out int num))
                {
                    if (!Biomes.Contains(num))
                        Biomes.Add(num);
                    textureDictionary.Add(num, LoadTexture(fileName));
                    return true;
                    //TerrainGenerators.Log("Biome: " + num);
                }
            }
            return false;
        }

        void CopyAsset(string asset)
        {
            Stream stream = Info.Mod.ReadModFile(Path.Combine("Assets", asset));
            byte[] data = new byte[stream.Length];
            stream.Read(data, 0, (int)stream.Length);
            stream.Close();
            File.Create(Path.Combine(GadgetPaths.AssetsPath, asset));
            File.WriteAllBytes(Path.Combine(GadgetPaths.AssetsPath, asset), data);
        }

        public static Texture2D LoadTexture(string fileName)
        {
            string filePath = Path.Combine(Path.Combine(GadgetPaths.AssetsPath, "TerrainGenerators"), fileName);
            var file = File.ReadAllBytes(filePath);
            Texture2D tex = new Texture2D(2, 2);
            tex.LoadImage(file);
            tex.filterMode = FilterMode.Point;
            return tex; 
        }


        // todo: reusableTexture probably doesn't need to be ref?
        /// <param name="subTilePos">Basically rotation; 0=topLeft, 1=topRight, 2=botRight, 3=botLeft</param>
        public static Texture2D GetRotatedSourceTex(int biome, int subtilePos, Tuple<string, int> tileType, Tuple<string, int> relLeftType, Tuple<string, int> relRightType, Tuple<string, int> oppType, ref Texture2D reusableTexture)
        {
            string type = tileType.Item1;
            int rotation = tileType.Item2 + subtilePos - 1;
            Texture2D sourceTex;
            int sourceSubPos = 0;
            if (type == corner)
            {
                sourceTex = TileCorners[biome];
                sourceSubPos = 1; // top right
            }
            else if (type == flat)
            {
                sourceTex = TileFlats[biome];
                sourceSubPos = (subtilePos + rotation) % 2; // top left or top right of flat
            }
            else if (type == innerCorner)
            {
                sourceTex = TileInnerCorners[biome];
                sourceSubPos = 1; // top right
            }
            else if (type == wall) // we are wall
            {
                if(relLeftType.Item1 == wall) // left is wall
                {
                    if(relRightType.Item1 == wall) // right is also wall
                    {
                        if (oppType.Item1 == wall) // this whole tile is wall
                        {
                            rotation = 2; // ??????
                            sourceTex = TileWalls[biome];
                            sourceSubPos = (subtilePos + 2) % 4; // ????
                        }
                        else // surface opposite
                        {
                            rotation += 2; // rotate to match opposite corner
                            sourceTex = TileInnerCorners[biome];
                            sourceSubPos = 3; // the inside corner of the inner corner tex
                        }
                    }
                    else // wall on left but surface on right
                    {
                        rotation += 2; // Why 2?? // rotate to match right surface
                        sourceTex = TileFlats[biome];
                        sourceSubPos = 3;
                    }    
                }
                else // surface on left
                {
                    if(relRightType.Item1 == wall) // wall on right
                    {
                        rotation -= 1; // rotate to match left surface
                        sourceTex = TileFlats[biome];
                        sourceSubPos = 2;
                    }
                    else // surface on right
                    {
                        rotation += 2; // rotate to match opposite corner
                        sourceTex = TileCorners[biome];
                        sourceSubPos = 3;
                    }
                }
            }
            else
            {
                Log("Texture not found: Tile" + type + biome);
                sourceTex = reusableTexture; // todo: this might end up weird lol
            }
            rotation = ((rotation % 4) + 4) % 4; // clamp rotation to 0-3

            int xOffSet = sourceSubPos == 0 || sourceSubPos == 3 ? 0 : subTileSize;
            int yOffSet = sourceSubPos == 2 || sourceSubPos == 3 ? 0 : subTileSize;
            // rotate the subsection of the texture
            if (rotation == 1)
            {
                for (int i = 0; i < subTileSize; i++)
                {
                    for (int j = 0; j < subTileSize; j++)
                    {
                        reusableTexture.SetPixel(j, subTileSize - i - 1, sourceTex.GetPixel(i + xOffSet, j + yOffSet));
                    }
                }
            }
            else if (rotation == 2)
            {
                for (int i = 0; i < subTileSize; i++)
                {
                    for (int j = 0; j < subTileSize; j++)
                    {
                        reusableTexture.SetPixel(subTileSize - i - 1, subTileSize - j - 1, sourceTex.GetPixel(i + xOffSet, j + yOffSet));
                    }
                }
            }
            else if (rotation == 3)
            {
                for (int i = 0; i < subTileSize; i++)
                {
                    for (int j = 0; j < subTileSize; j++)
                    {
                        reusableTexture.SetPixel(subTileSize - j - 1, i, sourceTex.GetPixel(i + xOffSet, j + yOffSet));
                    }
                }
            }
            else // else: 0 (no rotation)
            {
                for (int i = 0; i < subTileSize; i++)
                {
                    for (int j = 0; j < subTileSize; j++)
                    {
                        reusableTexture.SetPixel(i, j, sourceTex.GetPixel(i + xOffSet, j + yOffSet));
                    }
                }
            }
                
            return reusableTexture;
        }


        /*
        /// <param name="subtilePos">Basically rotation; 0=topLeft, 1=topRight, 2=botRight, 3=botLeft</param>
        /// <returns>(string) Tile name and (int) relative rotation (0 or 1)</returns>
        public static Tuple<string, int> GenerateTileQuarter2(bool[] neighbours, int subtilePos)
        {
            int[] singleRotationIndices = new int[8] { 2, 4, 7, 6, 5, 3, 0, 1 };
            void RotateNeighbours(int rotations)
            {
                // rotate the neighbours array subTilePos times
                for (int i = 0; i < rotations; i++)
                {
                    bool[] newNeighbours = new bool[8];
                    for (int j = 0; j < 8; j++)
                    {
                        newNeighbours[j] = neighbours[singleRotationIndices[j]];
                    }
                    neighbours = newNeighbours;
                }
            }
            RotateNeighbours(subtilePos);
            // *Most* subtiles now only need index 1, 2, and 4 (up, upRight, right) to determine their texture
            // Relative tile positions:
            // Tile 0   Tile 1   Tile 2
            //
            //          ??  ME
            // Tile 3            Tile 4
            //          ??  ??
            //
            // Tile 5   Tile 6   Tile 7
            bool up = neighbours[1];
            bool upRight = neighbours[2];
            bool right = neighbours[2];


            if (!up && !upRight && !right)
                return ToTuple("Corner", 0); // simple
            else if (!up && !upRight && right)
                return ToTuple("Flat", 0); // todo: pick depending on subtilePos?
            else if (!up && upRight && !right)
                return ToTuple("Corner", 0); // simple. Could be simplified. Note: this could be changed to a custom tile for a diagonal connection instead
            else if (!up && upRight && right)
                return ToTuple("Flat", 0); // todo: pick depending on subtilePos? Could be simplified to !up && right?
            else if (up && !upRight && !right)
                return ToTuple("Flat", 1); // todo: pick depending on subtilePos?
            else if (up && !upRight && right)
                return ToTuple("InnerCorner", 0); // simple
            else if (up && upRight && !right)
                return ToTuple("Flat", 1); // todo: pick depending on subtilePos? Could be simplified to up && !right?
            else // if (up && upRight && right)
            {
                // Inside subtile; these are more complicated because they depend on the whole tile shape.
            }
        }*/

        /// <returns>(string) Tile name and (int) relative rotation (0 or 1)</returns>
        public static Tuple<string, int> DetermineSubtileType(bool up, bool upRight, bool right) 
        {
            if (!up && !upRight && !right)
                return ToTuple(corner,0);
            else if (!up && !upRight && right)
                return ToTuple(flat, 0);
            else if (!up && upRight && !right)
                return ToTuple(corner, 0);
            else if (!up && upRight && right)
                return ToTuple(flat, 0);
            else if (up && !upRight && !right)
                return ToTuple(flat, 1);
            else if (up && !upRight && right)
                return ToTuple(innerCorner, 0);
            else if (up && upRight && !right)
                return ToTuple(flat, 1);
            else// if (up && upRight && right)
                return ToTuple(wall, 0);
        }

        static Tuple<string, int> ToTuple(string string1, int int1)
        {
            return new Tuple<string, int>(string1, int1);
        }

        /// <summary>
        /// Converts an int (0-255) to 8 bools.
        /// </summary>
        /// <param name="input"></param>
        /// <returns></returns>
        public static bool[] ConvertToBoolArray(int input)
        {
            string binaryString = Convert.ToString(input, 2).PadLeft(8,'0');
            bool[] m = new bool[8];
            for (int i = 0; i < 8; i++)
            {
                m[i] = (binaryString[binaryString.Length - i - 1] == '1');
            }
            return m;
        }
    }
}