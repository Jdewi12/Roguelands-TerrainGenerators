using UnityEngine;
using HarmonyLib;
using GadgetCore.API;
using System.Linq;
using TerrainGenerators.Generators;
using System.Reflection;
using System.Collections;
using System.Collections.Generic;
using System.Reflection.Emit;
using System.ComponentModel;
using GadgetCore.Util;

namespace TerrainGenerators.Patches
{
    [HarmonyPatch()]
    [HarmonyGadget("TerrainGenerators")]
    public static class Patch_LoopPortal_AddPortal
    {
        [HarmonyTargetMethod]
        public static MethodBase TargetMethod()
        {
            var gadget = Gadgets.GetGadget("LoopPortal");
            if(gadget != null)
                return gadget.GetType().Assembly
                    .GetType("LoopPortal.Patches.Patch_EntranceScript_SpawnEndPortal")
                    .GetNestedTypes(BindingFlags.NonPublic | BindingFlags.Instance).First(t => t.Name.Contains("AddPortal"))
                    .GetMethod("MoveNext", BindingFlags.NonPublic | BindingFlags.Instance);
            // else
            return null;
        }

        [HarmonyTranspiler]
        // instead of reusing the first portal spot it now uses the additional one created by the generator,
        // and no longer spawns some distance to the left of it.
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> codeInstructions)
        {
            var instructions = codeInstructions.ToList();
            for(int i = 0; i < instructions.Count; i++)
            {
                var instruction = instructions[i];
                // replace accesses at index 0 with index 3
                if (instruction.opcode == OpCodes.Ldc_I4_0 && instructions[i - 1].opcode == OpCodes.Ldloc_1)
                    instruction.opcode = OpCodes.Ldc_I4_3;
                // skip subtracting a float from x
                else if (instruction.opcode == OpCodes.Ldc_R4
                    && instructions[i - 1].opcode == OpCodes.Ldfld && instructions[i - 1].operand.ToString().Contains("x")
                    && instructions[i + 1].opcode == OpCodes.Sub)
                {
                    // skip both the float and the subtraction
                    i++;
                    continue;
                }
                if (instruction.operand != null)
                    TerrainGenerators.Log("Operand " + instruction.operand.ToString());
                yield return instruction;
            }
        }
    }
}