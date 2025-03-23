using HarmonyLib;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Diagnostics.Eventing.Reader;
using System.Linq;
using UnityEngine;
using Verse;


namespace BGInheritance
{

    //[HarmonyPatch(typeof(PregnancyUtility), nameof(PregnancyUtility.GetInheritedGenes), new Type[]
    //{
    //    typeof(Pawn),
    //    typeof(Pawn),
    //    typeof(bool)
    //}, new ArgumentType[]
    //{
    //    ArgumentType.Normal,
    //    ArgumentType.Normal,
    //    ArgumentType.Out
    //})]
    [StaticConstructorOnStartup]
    public static class BGI_HarmonyPatches
    {
        private static readonly Type patchType;

        static BGI_HarmonyPatches()
        {
            var harmony = new Harmony("RedMattis.BGInheritance");
            patchType = typeof(BGI_HarmonyPatches);
            harmony.PatchAll();
            //harmony.Patch(AccessTools.Method(typeof(PregnancyUtility), name: nameof(PregnancyUtility.GetInheritedGenes), new Type[]
            //{
            //    typeof(Pawn),
            //    typeof(Pawn),
            //    typeof(bool).MakeByRefType()
            //}), prefix: new HarmonyMethod(patchType, nameof(InheritancePostfix)));
        }
        //[HarmonyPostfix]

    }
}