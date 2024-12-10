using HarmonyLib;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using Verse;
using Verse.AI;

namespace BGInheritance
{
    [HarmonyPatch]
    public static class HarmonyPatches
    {
        public static int metabolismLimit = 0;
        [HarmonyPatch(typeof(JobDriver_FertilizeOvum), "MakeNewToils", MethodType.Enumerator)]
        [HarmonyPrefix]
        public static void MakeNewToils()
        {
            SetBiostatRange(-99);
        }
        [HarmonyPatch(typeof(JobDriver_FertilizeOvum), "MakeNewToils", MethodType.Enumerator)]
        [HarmonyPostfix]
        public static void MakeNewToilsPostfix()
        {
            ResetBiostatRange();
        }

        [HarmonyPatch(typeof(PregnancyUtility), nameof(PregnancyUtility.GetInheritedGenes))]
        [HarmonyPrefix]
        public static void GetInheritedGenes(Pawn father, Pawn mother, bool success)
        {
            SetBiostatRange(-99);
        }

        [HarmonyPatch(typeof(PregnancyUtility), nameof(PregnancyUtility.GetInheritedGenes))]
        [HarmonyPostfix]
        public static void GetInheritedGenesPostfix(Pawn father, Pawn mother, bool success)
        {
            ResetBiostatRange();
        }

        private static void SetBiostatRange(int value)
        {
            FieldInfo bioStatRangeI = typeof(GeneTuning).GetField("BiostatRange");

            // Get previous values of the IntRange. Just in case some other mod is messing with them.
            IntRange bioStatRange = (IntRange)bioStatRangeI.GetValue(null);
            metabolismLimit = bioStatRange.min;
            int max = Mathf.Max(5, bioStatRange.max);
            int min = Mathf.Min(value, bioStatRange.min);

            // Set minimum value of the IntRange to -99
            bioStatRangeI.SetValue(null, new IntRange(min, max));
        }

        private static void ResetBiostatRange()
        {
            FieldInfo bioStatRangeI = typeof(GeneTuning).GetField("BiostatRange");

            // Get previous values of the IntRange. Just in case some other mod is messing with them.
            IntRange bioStatRange = (IntRange)bioStatRangeI.GetValue(null);
            int max = Mathf.Max(5, bioStatRange.max);
            int min = metabolismLimit;

            // Set minimum value of the IntRange to -99
            bioStatRangeI.SetValue(null, new IntRange(min, max));
        }
    }
}
