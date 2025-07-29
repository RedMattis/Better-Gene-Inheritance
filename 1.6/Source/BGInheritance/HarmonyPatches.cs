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
        [HarmonyPatch(typeof(PregnancyUtility), nameof(PregnancyUtility.ApplyBirthOutcome))]
        [HarmonyPostfix]
        [HarmonyPriority(Priority.Low)]
        public static void ApplyBirthOutcomePostfix(Thing __result, RitualOutcomePossibility outcome, float quality, Precept_Ritual ritual, List<GeneDef> genes, Pawn geneticMother, Thing birtherThing, Pawn father = null, Pawn doctor = null, LordJob_Ritual lordJobRitual = null, RitualRoleAssignments assignments = null, bool preventLetter = false)
        {
            if (__result is Pawn baby && baby.genes?.GenesListForReading.Any() == true)
            {
                var parents = new List<Pawn> { father, geneticMother }.Where(x => x != null && x.genes?.GenesListForReading?.Any() == true).ToList();
                if (parents.Count > 0)
                {
                    List<(Pawn pawn, float score)> parentScores = [];
                    foreach (var parent in parents.Where(x => x.genes.GenesListForReading.Any()))
                    {
                        var pXenotype = parent.genes.Xenotype;
                        if (parent.genes.Xenotype != null && pXenotype != XenotypeDefOf.Baseliner && pXenotype.genes.Any())
                        {
                            var babyGeneDefs = baby.genes.GenesListForReading.Select(x => x.def);
                            var parentXeno = parent.genes.Xenotype;
                            var parentGenes = parentXeno.genes;
                            GetScore(parentScores, parent, babyGeneDefs, parentGenes);
                        }
                        else if (parent.genes.CustomXenotype != null)
                        {
                            var babyGeneDefs = baby.genes.GenesListForReading.Select(x => x.def);
                            var parentXeno = parent.genes.CustomXenotype;
                            var parentGenes = parentXeno.genes;
                            GetScore(parentScores, parent, babyGeneDefs, parentGenes);
                        }
                        else if (pXenotype != XenotypeDefOf.Baseliner)
                        {
                            var babyGeneDefs = baby.genes.GenesListForReading.Select(x => x.def);
                            var parentGenes = parent.genes.GenesListForReading.Select(x=>x.def).ToList();
                            GetScore(parentScores, parent, babyGeneDefs, parentGenes);
                        }
                    }
                    if (parentScores.Count > 0)
                    {
                        var (parent, score) = parentScores.OrderByDescending(x => x.score).First();
                        
                        baby.genes.hybrid = false;
                        var pXenotype = parent.genes.Xenotype;
                        if (pXenotype != null && pXenotype != XenotypeDefOf.Baseliner && pXenotype.genes.Any())
                        {
                            baby.genes.SetXenotypeDirect(parent.genes.Xenotype);
                            if (score < 0.8f)
                            {
                                baby.genes.xenotypeName = "Hybrid".Translate() + " " + parent.genes.Xenotype.LabelCap;
                                if (TryFindIconDef(parent) is XenotypeIconDef iconDef)
                                {
                                    baby.genes.iconDef = iconDef;
                                }
                            }
                        }
                        else if (parent.genes.CustomXenotype != null)
                        {
                            baby.genes.xenotypeName = parent.genes.CustomXenotype.name;
                            if (score < 0.8f)
                            {
                                baby.genes.xenotypeName = "Hybrid".Translate() + " " + parent.genes.CustomXenotype.name;
                            }
                            baby.genes.iconDef = parent.genes.CustomXenotype.iconDef == XenotypeIconDefOf.Basic ? parent.genes.iconDef
                                : parent.genes.CustomXenotype.iconDef;
                        }
                        else if (parent.genes.xenotypeName != null)
                        {
                            baby.genes.xenotypeName = parent.genes.xenotypeName;
                            string xenoName = parent.genes.xenotypeName.Replace("Hybrid".Translate(), "").Trim();
                            if (score < 0.9f && xenoName.Any())
                            {
                                baby.genes.xenotypeName = "Hybrid".Translate() + " " + xenoName;
                            }
                            baby.genes.iconDef = parent.genes.iconDef;
                        }
                        baby.genes.hybrid = false;
                    }
                }
            }

            static void GetScore(List<(Pawn pawn, float score)> parentScores, Pawn parent, IEnumerable<GeneDef> babyGeneDefs, List<GeneDef> parentGenes)
            {
                const float negFactor = 0.35f;
                float score = parentGenes.Sum(x => babyGeneDefs.Contains(x) ? 1 : 0) / (float)parentGenes.Count;
                var notRandomNotCosmeticBabyGenes = babyGeneDefs.Where(x => !x.randomChosen && !(x.biostatMet == 0 && x.biostatArc == 0));
                float negativeScore = 0;
                if (notRandomNotCosmeticBabyGenes.Any())
                {
                    negativeScore = notRandomNotCosmeticBabyGenes.Sum(x => !parentGenes.Contains(x) ? 1 : 0) / (float)parentGenes.Count;
                }
                parentScores.Add((parent, score - (negativeScore * negFactor)));
            }
        }

        private static XenotypeIconDef TryFindIconDef(Pawn parent)
        {
            var iconPath = parent.genes.Xenotype.iconPath;
            var xIconDef = DefDatabase<XenotypeIconDef>.AllDefsListForReading
                .FirstOrDefault(x => string.Equals(x.texPath, iconPath, StringComparison.OrdinalIgnoreCase));
            return xIconDef;
        }

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
                //Log.Message($"Generated genes for child:\n" +
                //            $"Genes: {__result.Count}\n" +
                //            $"Metabolism: {__result.Sum(x => x.biostatMet)}\n" +
                //            $"Archite: {__result.Sum(x => x.biostatArc)}\n");
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

