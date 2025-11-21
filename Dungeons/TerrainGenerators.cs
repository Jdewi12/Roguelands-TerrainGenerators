using GadgetCore;
using GadgetCore.API;
using GadgetCore.Util;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Reflection;
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

        public const string MOD_VERSION = "1.2";
        public const string CONFIG_VERSION = "1.0";

        internal static MinimapAPI MinimapAPI;

        protected override void LoadConfig()
        {
            InternalLogger = base.Logger;
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

        internal static GadgetLogger InternalLogger;
        
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
            dummy.AddComponent<DummyMonoBehaviour>()
                .StartCoroutine(DelayTextureGeneration(dummy));

            if (Gadgets.GetGadget("Minimap") != null)
            {

                MinimapAPI = new MinimapAPI();
            }

            Log("Gadget Initialised");
        }

        public static IEnumerator DelayTextureGeneration(GameObject destroyWhenDone)
        {
            yield return null; // wait till after the first frame, to make sure mods have all registered their custom planets
            if (GeneratedTextures.Count == 0)
            {
                ModdedTextures.GenerateTextures();
                GenerateTextures();
            }
            if(destroyWhenDone != null)
                GameObject.Destroy(destroyWhenDone);
        }

        public static List<int> Biomes = new List<int>();
        public static Dictionary<int, Texture2D> TileCorners = new Dictionary<int, Texture2D>();
        public static Dictionary<int, Texture2D> TileFlats = new Dictionary<int, Texture2D>();
        public static Dictionary<int, Texture2D> TileInnerCorners = new Dictionary<int, Texture2D>();
        public static Dictionary<int, Texture2D> TileWalls = new Dictionary<int, Texture2D>();

        const int tileSize = 64;
        const int tileSize2 = 2 * tileSize;
        
        public static void LoadPackedTextures()
        {
            Log("Generating Textures.");
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
                if (!CheckImageFileName(fileName, "TileCorner", ref TileCorners))
                    if (!CheckImageFileName(fileName, "TileFlat", ref TileFlats))
                        if (!CheckImageFileName(fileName, "TileInnerCorner", ref TileInnerCorners))
                            if (!CheckImageFileName(fileName, "TileWall", ref TileWalls))
                            {
                                OtherTextures.Add(fileName, LoadTexture(fileName));
                                Log("Loaded non-tile texture: " + fileName);
                            }
            }
        }

        public static void GenerateTextures()
        {
            //  Tile 0   Tile 1   Tile 2
            //  Tile 3     ME     Tile 4
            //  Tile 5   Tile 6   Tile 7

            // topRight = (tile1, tile2, tile4)
            // bottomRight = (tile4, tile7, tile6).rotate(270)
            // bottomLeft = (tile6, tile5, tile3).rotate(180)
            // topLeft = (tile3, tile0, tile1).rotate(90)

            Log("Generating Textures...");
            bool[] tileCombination;
            for (int i = 0; i < 256; i++)
            {
                tileCombination = ConvertToBoolArray(i);

                Tuple<string, int> topRight = GenerateTileQuarter(tileCombination[1], tileCombination[2], tileCombination[4]);
                Tuple<string, int> bottomRight = GenerateTileQuarter(tileCombination[4], tileCombination[7], tileCombination[6]); // rotate this 270
                Tuple<string, int> bottomLeft = GenerateTileQuarter(tileCombination[6], tileCombination[5], tileCombination[3]); // rotate this 180
                Tuple<string, int> topLeft = GenerateTileQuarter(tileCombination[3], tileCombination[0], tileCombination[1]); // rotate this 90
                foreach (int biomeID in Biomes)
                {
                    Texture2D tex = new Texture2D(tileSize2, tileSize2)
                    {
                        filterMode = FilterMode.Point
                    };
                    Texture2D topRightTex = GetTex(topRight.Item1, biomeID, topRight.Item2);
                    Texture2D bottomRightTex = GetTex(bottomRight.Item1, biomeID, bottomRight.Item2 + 1); //only rotations of 1, 2, and 3 do anything, so if the texture already rotates then this'll do nothing.
                    Texture2D bottomLeftTex = GetTex(bottomLeft.Item1, biomeID, bottomLeft.Item2 + 2);
                    Texture2D topLeftTex = GetTex(topLeft.Item1, biomeID, topLeft.Item2 + 3);
                    // texture coordinates start in bottom left
                    for (int x = 0; x < tileSize; x++)
                    {
                        for (int y = 0; y < tileSize; y++)
                        {
                            tex.SetPixel(tileSize2 - x - 1, tileSize2 - y - 1, bottomLeftTex.GetPixel(x, y));
                            tex.SetPixel(tileSize - x - 1, tileSize2 - y - 1, bottomRightTex.GetPixel(x, y));
                            tex.SetPixel(tileSize2 - x - 1, tileSize - y - 1, topLeftTex.GetPixel(x, y));
                            tex.SetPixel(tileSize - x - 1, tileSize - y - 1, topRightTex.GetPixel(x, y));
                        }
                    }
                    tex.Apply();
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
            Log("Generated textures for " + Biomes.Count + " planet(s).");
        }

        static bool CheckImageFileName(string fileName, string checkName, ref Dictionary<int, Texture2D> textureDictionary)
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

        public static Texture2D GetTex(string str, int biome, int rotation)
        {
            Texture2D tex;
            if (str == "Corner")
                tex = TileCorners[biome];
            else if (str == "Flat")
                tex = TileFlats[biome];
            else if (str == "InnerCorner")
                tex = TileInnerCorners[biome];
            else if (str == "Wall")
                tex = TileWalls[biome];
            else
            {
                Log("Texture not found: Tile" + str + biome);
                tex = new Texture2D(tileSize, tileSize);
            }
            if (rotation == 1)
            {
                Texture2D newTex = new Texture2D(tileSize, tileSize);
                for (int i = 0; i < tileSize; i++)
                {
                    for (int j = 0; j < tileSize; j++)
                    {
                        newTex.SetPixel(j, tileSize - i - 1, tex.GetPixel(i, j));
                    }
                }
                tex = newTex;
            }
            else if (rotation == 2)
            {
                Texture2D newTex = new Texture2D(tileSize, tileSize);
                for (int i = 0; i < tileSize; i++)
                {
                    for (int j = 0; j < tileSize; j++)
                    {
                        newTex.SetPixel(tileSize - i - 1, tileSize - j - 1, tex.GetPixel(i, j));
                    }
                }
                tex = newTex;
            }
            else if (rotation == 3)
            {
                Texture2D newTex = new Texture2D(tileSize, tileSize);
                for (int i = 0; i < tileSize; i++)
                {
                    for (int j = 0; j < tileSize; j++)
                    {
                        newTex.SetPixel(tileSize - j - 1, i, tex.GetPixel(i, j));
                    }
                }
                tex = newTex;
            }
            return tex;
        }


        public static Tuple<string, int> GenerateTileQuarter(bool up, bool upRight, bool right) //returns tile name and rotation (0-1)
        {
            if (!up && !upRight && !right)
                return ToTuple("Corner",0);
            else if (!up && !upRight && right)
                return ToTuple("Flat", 0);
            else if (!up && upRight && !right)
                return ToTuple("Corner", 0);
            else if (!up && upRight && right)
                return ToTuple("Flat", 0);
            else if (up && !upRight && !right)
                return ToTuple("Flat",1);
            else if (up && !upRight && right)
                return ToTuple("InnerCorner", 0);
            else if (up && upRight && !right)
                return ToTuple("Flat", 1);
            else// if (up && upRight && right)
                return ToTuple("Wall", 0);
        }

        static Tuple<string, int> ToTuple(string string1, int int1)
        {
            return new Tuple<string, int>(string1, int1);
        }

        public static bool[] ConvertToBoolArray(int input)
        {
            string binaryString = Convert.ToString(input, 2).PadLeft(8,'0');
            //TerrainGenerators.Log("Combination: " + binaryString);
            //TerrainGenerators.Log("Binary String: " + binaryString);
            bool[] m = new bool[8];
            for (int i = 0; i < 8; i++)
            {
                //TerrainGenerators.Log("I: " + i);
                m[i] = (binaryString[binaryString.Length - i - 1] == '1');
            }
            return m;
        }
    }
}