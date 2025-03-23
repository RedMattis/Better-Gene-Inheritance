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
        [HarmonyPatch(typeof(PregnancyUtility), nameof(PregnancyUtility.GetInheritedGenes),
        [
            typeof(Pawn),
            typeof(Pawn),
            typeof(bool)
        ],
        [
            ArgumentType.Normal,
            ArgumentType.Normal,
            ArgumentType.Out
        ])]
        [HarmonyPrefix]
        public static bool InheritancePrefix(ref List<GeneDef> __result, Pawn father, Pawn mother, ref bool success)
        {
            try
            {
                Pawn_GeneTracker tracker1 = mother?.genes;
                Pawn_GeneTracker tracker2 = father?.genes;
                tracker1 ??= tracker2;
                tracker2 ??= tracker1;
                if (tracker1 == null || tracker2 == null)
                {
                    Log.Message("No gene-trackers found. Aborting inheritance prefix.");
                    return true; // Continue to vanilla.
                }

                success = true;

                // Get GeneTracker order
                (var primaryTracker, var secondaryTracker) = GetGeneTrackerOrder(tracker1, tracker2);

                __result = GeneFunctions.GetChildGenes(primaryTracker, secondaryTracker);
                Log.Message($"Generated genes for child:\n" +
                            $"Genes: {__result.Count}\n" +
                            $"Metabolism: {__result.Sum(x => x.biostatMet)}\n" +
                            $"Archite: {__result.Sum(x => x.biostatArc)}\n");
                return false;
            }
            catch (Exception e)
            {
                Log.Error("Managed Exception in BGInheritance. Aborting inheritance changes: " + e.Message);
                return true;
            }
        }

        public static (Pawn_GeneTracker, Pawn_GeneTracker) GetGeneTrackerOrder(Pawn_GeneTracker parentA, Pawn_GeneTracker parentB)
        {
            bool pABaseliner = false;
            bool pBBaseliner = false;
            try
            {
                pABaseliner = parentA.pawn?.genes.Xenotype == XenotypeDefOf.Baseliner && parentA.pawn?.genes.GenesListForReading.All(x => x.def.biostatMet == 0 || x.def.biostatArc == 0) == true;
                pBBaseliner = parentB.pawn?.genes.Xenotype == XenotypeDefOf.Baseliner && parentB.pawn?.genes.GenesListForReading.All(x => x.def.biostatMet == 0 || x.def.biostatArc == 0) == true;
            }
            catch (Exception)
            {
                // Honestly don't care. They are probably a robot or something without genes.
            }

            bool parentAHasPrimaryGenes = parentA.GenesListForReading.Any(x => x.def.defName == "BGI_PrimaryGenes");
            bool parentBHasPrimaryGenes = parentB.GenesListForReading.Any(x => x.def.defName == "BGI_PrimaryGenes");

            bool parentAHasSecondaryGenes = parentA.GenesListForReading.Any(x => x.def.defName == "BGI_SecondaryGenes");
            bool parentBHasSecondaryGenes = parentB.GenesListForReading.Any(x => x.def.defName == "BGI_SecondaryGenes");

            // If any pawn is a baseliner, they are always the primary parent.
            if (!parentAHasPrimaryGenes && !parentBHasPrimaryGenes && !parentAHasSecondaryGenes && !parentBHasSecondaryGenes)
            {
                if (pABaseliner && !pBBaseliner)
                {
                    return (parentA, parentB);
                }
                else if (!pABaseliner && pBBaseliner)
                {
                    return (parentB, parentA);
                }
            }

            if (parentAHasPrimaryGenes && !parentBHasPrimaryGenes)
            {
                return (parentA, parentB);
            }
            else if (!parentAHasPrimaryGenes && parentBHasPrimaryGenes)
            {
                return (parentB, parentA);
            }
            else if (parentAHasSecondaryGenes && !parentBHasSecondaryGenes)
            {
                return (parentA, parentB);
            }
            else if (!parentAHasSecondaryGenes && parentBHasSecondaryGenes)
            {
                return (parentB, parentA);
            }

            if (Rand.Chance(0.5f))
            {
                return (parentA, parentB);
            }
            else
            {
                return (parentB, parentA);
            }
        }
    }
}

