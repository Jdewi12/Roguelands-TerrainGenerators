using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using UnityEngine;

namespace TerrainGenerators.Helpers
{
    public static class ConnectedTextures
    {
        /// <summary>
        /// 
        /// </summary>
        /// <param name="biome"></param>
        /// <param name="tileCombination">
        /// An array of which of the surrounding tiles are walls or not. 
        /// The first index is the position to the left of the tile, then it continues clockwise.</param>
        /// <returns></returns>
        public static Texture2D GetTexture(bool[] tileCombination)
        {
            if (TerrainGenerators.GeneratedTextures.TryGetValue(SpawnerScript.curBiome, out Dictionary<byte, Texture2D> textures))
            {
                if (textures.TryGetValue(EncodeBool(tileCombination), out Texture2D tex))
                {
                    return tex;
                }
                else
                {
                    string s = "";
                    for (int i = 0; i < tileCombination.Length; i++)
                        s += tileCombination[i] ? "1" : "0";
                    TerrainGenerators.Log("Tile combination does not exist in biome's generated textures: " + s);
                }
            }
            else
            {
                TerrainGenerators.Log("Biome " + SpawnerScript.curBiome + " does not exist in generated textures");
            }
            return null;
        }

        public static byte EncodeBool(bool[] arr)
        {
            byte val = 0;
            foreach (bool b in arr)
            {
                val <<= 1;
                if (b) val |= 1;
            }
            return val;
        }
    }
}
