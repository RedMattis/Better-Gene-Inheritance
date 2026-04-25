using HarmonyLib;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using Verse;
using static BGInheritance.BGIDefs;

namespace BGInheritance
{

    public static class GeneFunctions
    {
        public static List<GeneDef> GetChildGenes(Pawn_GeneTracker parentA, Pawn_GeneTracker parentB)
        {
            var settings = BGInheritanceMain.settings;

            var pawnA = parentA.pawn;
            var pawnB = parentB.pawn;

            List<Gene> geneSetA;
            List<Gene> geneSetB;
            if (settings.inheritXenoGenes >= 0.99f)
            {
                geneSetA = [.. parentA.GenesListForReading];
                geneSetB = [.. parentB.GenesListForReading];
            }
            else
            {
                geneSetA = [.. parentA.Endogenes];
                geneSetB = [.. parentB.Endogenes];
                foreach (var gene in parentA.Xenogenes.Where(x => Rand.Chance(settings.inheritXenoGenes)))
                {
                    geneSetA.Add(gene);
                }
                foreach (var gene in parentB.Xenogenes.Where(x => Rand.Chance(settings.inheritXenoGenes)))
                {
                    geneSetB.Add(gene);
                }
            }

            var geneDefsA = geneSetA.Select(x => x.def).ToList();
            var geneDefsB = geneSetB.Select(x => x.def).ToList();

            bool parentAHasDominantGenes = geneSetA.Any(x => x.Active && (x.def == BGI_DominantGenes || x.def.defName == "VRE_DominantGenome"));
            bool parentAHasRecessiveGenes = geneSetA.Any(x => x.Active && (x.def == BGI_RecessiveGenes || x.def.defName == "VRE_RecessiveGenome"));
            bool parentAHasBinaryGenes = geneSetA.Any(x => x.Active && x.def == BGI_BinaryInheritance);

            bool parentBHasDominantGenes = geneSetB.Any(x => x.Active && (x.def == BGI_DominantGenes || x.def.defName == "VRE_DominantGenome"));
            bool parentBHasRecessiveGenes = geneSetB.Any(x => x.Active && (x.def == BGI_RecessiveGenes || x.def.defName == "VRE_RecessiveGenome"));
            bool parentBHasBinaryGenes = geneSetB.Any(x => x.Active && x.def == BGI_BinaryInheritance);

            if (settings.useBinaryInheritanceOnly)
            {
                parentAHasBinaryGenes = true;
                parentBHasBinaryGenes = true;
            }

            if (parentAHasDominantGenes && !parentBHasDominantGenes)
            {
                return geneDefsA;
            }
            else if (!parentAHasDominantGenes && parentBHasDominantGenes)
            {
                return geneDefsB;
            }
            else if (parentAHasRecessiveGenes && !parentBHasRecessiveGenes)
            {
                return geneDefsB;
            }
            else if (!parentAHasRecessiveGenes && parentBHasRecessiveGenes)
            {
                return geneDefsA;
            }

            bool binaryInheritance = parentAHasBinaryGenes && parentBHasBinaryGenes;

            if (binaryInheritance)
            {
                // Randomly select one of the parents to inherit from.
                if (Rand.Chance(0.5f))
                {
                    return geneDefsA;
                }
                else
                {
                    return geneDefsB;
                }
            }

            for (int i = geneDefsA.Count - 1; i >= 0; i--)
            {
                if (geneDefsA[i].biostatArc > 0 && !Rand.Chance(settings.inheritArchiteGenes))
                {
                    if (settings.inheritSharedArchiteGenes && geneDefsB.Contains(geneDefsA[i]))
                    {
                        continue;
                    }
                    geneDefsA.RemoveAt(i);
                }
            }

            for (int i = geneDefsB.Count - 1; i >= 0; i--)
            {
                if (geneDefsB[i].biostatArc > 0 && !Rand.Chance(settings.inheritArchiteGenes))
                {
                    if (settings.inheritSharedArchiteGenes && geneDefsA.Contains(geneDefsB[i]))
                    {
                        continue;
                    }
                    geneDefsB.RemoveAt(i);
                }
            }

            // Remove other blacklisted genetypes
            // Check each gene via reflection to see if they have a property named "IsMutation" or "IsEvolution" if the property exists at all, remove them.
            geneDefsA.RemoveAll(x => AccessTools.Property(x.GetType(), "IsMutation") != null || AccessTools.Property(x.GetType(), "IsEvolution") != null);
            geneDefsB.RemoveAll(x => AccessTools.Property(x.GetType(), "IsMutation") != null || AccessTools.Property(x.GetType(), "IsEvolution") != null);

            var genesSharedByBothParents = geneDefsA.Intersect(geneDefsB).ToList();
            int parentBGeneCount = geneDefsB.Count() - 1;

            // Add 20% to 100% of ParentB's Genes to transfer:
            int minimum = (int)(parentBGeneCount * settings.secondMinPercent);
            int maximum = (int)(parentBGeneCount * settings.secondMaxPercent);
            int numberOfGenesToTransfer = Mathf.Clamp(Rand.RangeInclusive(minimum, maximum), 0, parentBGeneCount);

            var request = new PawnGenerationRequest(PawnKindDefOf.Colonist, Faction.OfPlayer, PawnGenerationContext.NonPlayer, -1, forceGenerateNewPawn: true, allowDead: false, allowDowned: true, canGeneratePawnRelations: false, mustBeCapableOfViolence: false, 1f, forceAddFreeWarmLayerIfNeeded: false, allowGay: true, allowPregnant: false, allowFood: true, allowAddictions: true, inhabitant: false, certainlyBeenInCryptosleep: false, forceRedressWorldPawnIfFormerColonist: false, worldPawnFactionDoesntMatter: false, 0f, 0f, null, 1f, null, null, null, null, null, null, null, null, null, null, null, null, forceNoIdeo: false, forceNoBackstory: false, forbidAnyTitle: false, forceDead: false, null, null, null, null, null, 0f, DevelopmentalStage.Baby);
            Pawn fakeBaby = PawnGenerator.GeneratePawn(request);

            var geneTracker = fakeBaby.genes;

            // Remove all genes from the dummy pawn
            RemoveAllGenes(geneTracker);
            ClearCachedGenes(geneTracker);
            if (geneTracker.GenesListForReading.Any())
            {
                RemoveAllGenes(geneTracker);
            }
            ClearCachedGenes(geneTracker);

            // Just to be sure.
            geneTracker.Xenogenes.Clear();
            geneTracker.Endogenes.Clear();
            

            // Add all genes from parentA to the dummy pawn
            foreach (var gene in geneDefsA)
            {
                geneTracker.AddGene(gene, false);
            }

            // If the dummy pawn has any gene not from the mother delete it. It is probably a hair or skin gene that the 
            // game pulled out of a magic hat.
            foreach (var gene in geneTracker.GenesListForReading.Where(x => !geneDefsA.Contains(x.def)).ToList())
            {
                geneTracker.RemoveGene(gene);
            }
            if (parentA != parentB)
            {
                // Add 25-75% of genes from ParentB to the dummy pawn as xenogenes
                int count = 0;
                var bGenes = new List<GeneDef>();
                while (count < numberOfGenesToTransfer && geneDefsB.Count > 0)
                {
                    var gene = geneDefsB.RandomElement();
                    geneDefsB.Remove(gene);
                    if (!geneTracker.GenesListForReading.Select(x => x.def).Contains(gene))
                    {
                        bGenes.Add(gene);
                    }
                    count++;
                }

                foreach (var gene in bGenes)
                {
                    geneTracker.AddGene(gene, true);
                }

                // Remove all overriden genes from the baby pawn. If it is a random chosen gene, there is a 50% chance it will be kept anyway.
                // Not keeping all, because that just bloats the gene list too much.
                foreach (var gene in geneTracker.GenesListForReading.Where(x => x.Overridden && (Rand.Chance(0.5f) && !x.def.randomChosen)).ToList())
                {
                    geneTracker.RemoveGene(gene);
                }

                RemoveDueToMissingPrerequsite(geneTracker);
                RemoveRandomToMetabolism(geneTracker, minMet: settings.metabolismLimit, exclusionList: genesSharedByBothParents);
                RemoveDueToMissingPrerequsite(geneTracker);
                RemoveRandomToMetabolism(geneTracker, minMet: settings.metabolismLimit);
                RemoveDueToMissingPrerequsite(geneTracker);

                // Integrate all the Xenogenes, turning them into Endogenes.
                IntegrateGenes(geneTracker);
                if (settings.removeOverride)
                {
                    List<Gene> allGenes = [.. geneTracker.GenesListForReading];
                    // Remove all overridden genes from the baby pawn.
                    foreach (var gene in geneTracker.GenesListForReading
                        .Where(x =>
                            x.Overridden &&
                            // Make sure we don't remove overriden genes that are overriden via the prerequisite system or something.
                            allGenes.Contains(x.overriddenByGene))
                        .ToList())
                    {
                        geneTracker.RemoveGene(gene);
                    }
                }
            }
            else
            {
                parentB = null;
            }

            // Destroy fake baby
            fakeBaby.Destroy();

            var allGeneDefsOnDummy = geneTracker.GenesListForReading.Select(x => x.def).ToList();

            if (pawnA != pawnB
                && Rand.Value < PregnancyUtility.InbredChanceFromParents(pawnA, pawnB, out var _)
                && !allGeneDefsOnDummy.Contains(GeneDefOf.Inbred))
            {
                allGeneDefsOnDummy.Add(GeneDefOf.Inbred);
            }

            return allGeneDefsOnDummy;

            static void RemoveAllGenes(Pawn_GeneTracker geneTracker)
            {
                var allGenes = geneTracker.GenesListForReading.ToList();
                for (int i = 0; i < allGenes.Count; i++)
                {
                    Gene gene = allGenes[i];
                    geneTracker.RemoveGene(gene);
                }
            }
        }

        private static void RemoveDueToMissingPrerequsite(Pawn_GeneTracker geneTracker)
        {
            var toRemoveDueToMissingPrerequsite = new List<Gene>();
            // Iterrate through all the genes and make sure all prerequisites are met.
            foreach (var gene in geneTracker.GenesListForReading.Where(x => x.def.prerequisite != null))
            {
                if (geneTracker.GenesListForReading.Any(x => x.def == gene.def.prerequisite) == false)
                {
                    toRemoveDueToMissingPrerequsite.Add(gene);
                }
            }
            for (int i = toRemoveDueToMissingPrerequsite.Count - 1; i >= 0; i--)
            {
                Gene toRemove = toRemoveDueToMissingPrerequsite[i];
                geneTracker.RemoveGene(toRemove);
            }
        }

        /// <summary>
        /// In case of something messing with the GeneListForReading state.
        /// </summary>
        static void ClearCachedGenes(Pawn_GeneTracker gTracker) => Traverse.Create(gTracker).Field("cachedGenes").SetValue(null);

        public static void RemoveRandomToMetabolism(Pawn_GeneTracker genes, int minMet = -6, List<GeneDef> exclusionList = null)
        {
            var settings = BGInheritanceMain.settings;
            int GetCurrentMetabolism()
            {
                if (settings.ignoreCustomGenCats)
                {
                    return genes.Endogenes.Where(x => x.Overridden == false).Sum(x => x.def.biostatMet)
                        + genes.Xenogenes.Where(x => x.Overridden == false).Sum(x => x.def.biostatMet);
                }
                else
                {
                    ClearCachedGenes(genes);
                    return genes.GenesListForReading.Where(x => x.Overridden == false).Sum(x => x.def.biostatMet);
                }
            }
            Gene TryGetGeneWithCost()
            {
                var possibleGenes = genes.GenesListForReading.Where(x => x.def.biostatMet < 0 && !exclusionList.Contains(x.def));
                return possibleGenes.Any() ? possibleGenes.RandomElement() : null;
            }
            int metabolismInitial = GetCurrentMetabolism();

            if (metabolismInitial >= minMet)
                return;

            exclusionList ??= [];
            int idx = 0;

            // Sum up the metabolism cost of the new genes
            while (GetCurrentMetabolism() < minMet)
            {
                
                if (genes.GenesListForReading.Count <= 1)
                    break;
                // Pick a random gene from the newGenes with a negative metabolism cost and remove it.
                var geneToRemove = TryGetGeneWithCost();
                if (geneToRemove != null)
                {
                    //Log.Message($"DEBUG: Removing gene {geneToRemove.def.label} with metabolism cost {geneToRemove.def.biostatMet}.\n" +
                    //    $"Metabolism before removal was {GetCurrentMetabolism()}. Target is {minMet}. Pre-removal-start metabolism was {metabolismInitial}");
                    genes.RemoveGene(geneToRemove);
                }
                else
                {
                    break;
                }
                // Make sure the while exists if something prevents the genes being removed.
                idx++;
                if (idx > 200)
                    break;
            }
        }

        public static void IntegrateGenes(Pawn_GeneTracker geneTracker)
        {
            var xenogenes = geneTracker.Xenogenes.ToList();

            // remove all xenogenes from the pawn
            foreach (var gene in xenogenes)
            {
                geneTracker.RemoveGene(gene);
            }

            // add them as endogenes
            foreach (var gene in xenogenes)
            {
                geneTracker.AddGene(gene.def, xenogene: false);
            }
        }
    }

}
