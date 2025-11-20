using GadgetCore.API;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace TerrainGenerators.Patches
{
    [HarmonyPatch(typeof(Chunk))]
    [HarmonyPatch(nameof(Chunk.OnDestroy))]
    [HarmonyGadget("TerrainGenerators")]
    public static class Patch_Chunk_OnDestroy
    {
        [HarmonyPrefix]
        // non-blocking prefix because it doesn't really hurt to run the original and other people's patches might rely on it
        public static void Prefix(Chunk __instance, GameObject[] ___networkStuff) 
        {
            if(Network.isServer)
            {
                for (int i = 0; i < ___networkStuff.Length; i++)
                {
                    if (___networkStuff[i] != null)
                    {
                        Network.RemoveRPCs(___networkStuff[i].GetComponent<NetworkView>().viewID);
                        Network.Destroy(___networkStuff[i].gameObject);
                    }
                }
            }
        }
    }
}
