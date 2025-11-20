using GadgetCore.API;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace TerrainGenerators.Helpers
{
    public static class ModdedTextures
    {
        public static Dictionary<int, Color> CustomPlanetIDColors = new Dictionary<int, Color>();

        public static void GenerateTextures()
        {
            foreach (var planetInfo in PlanetRegistry.Singleton.GetAllEntries())
            {
                int id = planetInfo.GetID();
                var zoneTex = planetInfo.ZoneTex as Texture2D;// ?? planetInfo.EntranceMat.mainTexture;
                if (zoneTex == null)
                {
                    TerrainGenerators.Log(id + " did not have a zoneTex");
                    continue;
                }
                if (zoneTex.width != 1024 || zoneTex.height != 512)
                    TerrainGenerators.Log("Warning: Zone for custom planet " + id + " is sized " + zoneTex.width + "x" + zoneTex.height);
                TerrainGenerators.Biomes.Add(id);
                var cornerTex = GetBlock(zoneTex, 384, 64, 64, 64);
                List<Color> edgeColors = new List<Color>();
                int minCornerDist = 64 - 4; // max last 4 pixels of transparency
                for(int x = 32; x < minCornerDist; x++)
                {
                    var color = cornerTex.GetPixel(x, 63);
                    if(!edgeColors.Contains(color))
                        edgeColors.Add(color);
                }
                for(int y = 32; y < minCornerDist; y++)
                {
                    var color = cornerTex.GetPixel(63, y);
                    if (!edgeColors.Contains(color))
                        edgeColors.Add(color);
                }
                Color transparent = new Color(0, 0, 0, 0);
                int edgeX = 64;
                for (int x = minCornerDist; x < 64; x++ )
                {
                    var color = cornerTex.GetPixel(x, 63);
                    if(edgeX > x && !edgeColors.Contains(color))
                        edgeX = x;
                    if(x >= edgeX)
                    {
                        cornerTex.SetPixel(x, 63, transparent);
                    }
                }
                int edgeY = 64;
                for (int y = minCornerDist; y < 64; y++)
                {
                    var color = cornerTex.GetPixel(63, y);
                    if (edgeY > y && !edgeColors.Contains(color))
                        edgeY = y;
                    if (y >= edgeY)
                    {
                        cornerTex.SetPixel(63, y, transparent);
                    }
                }

                if (edgeX != edgeY)
                    TerrainGenerators.Log("Warning: Asymmetrical corner texture for planet " + id);
                for(int x = Mathf.Max(edgeX, edgeY); x < 64; x++)
                {
                    for(int y = 63 - (x - edgeX); y < 64; y++)
                    {
                        var color = cornerTex.GetPixel(x, y);
                        if (!edgeColors.Contains(color))
                            cornerTex.SetPixel(x, y, transparent);
                    }

                }

                TerrainGenerators.TileCorners.Add(id, cornerTex);
                TerrainGenerators.TileFlats.Add(id, GetBlock(zoneTex, 320, 64, 64, 64));
                TerrainGenerators.TileInnerCorners.Add(id, GetBlock(zoneTex, 160, 64, 64, 64));
                TerrainGenerators.TileWalls.Add(id, GetBlock(zoneTex, 0, 0, 64, 64));

                CustomPlanetIDColors.Add(id, edgeColors.Last());

                TerrainGenerators.Log("Generated sub-textures for custom planet " + planetInfo.Name + " (" + id + ")");
            }
        }
        public static Texture2D GetBlock(Texture2D tex, int x, int y, int blockWidth, int blockHeight)
        {
            Texture2D result = new Texture2D(blockWidth, blockHeight) { filterMode = FilterMode.Point };
            result.SetPixels(tex.GetPixels(x, y, blockWidth, blockHeight));
            return result;
        }
    }
}
