using HarmonyLib;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using Verse;

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

            bool parentAHasDominantGenes = geneSetA.Any(x => x.Active && (x.def.defName == "BGI_DominantGenes" || x.def.defName == "VRE_DominantGenome"));
            bool parentAHasRecessiveGenes = geneSetA.Any(x => x.Active && (x.def.defName == "BGI_RecessiveGenes" || x.def.defName == "VRE_RecessiveGenome"));
            bool parentAHasBinaryGenes = geneSetA.Any(x => x.Active && x.def.defName == "BGI_BinaryInheritance");

            bool parentBHasDominantGenes = geneSetB.Any(x => x.Active && (x.def.defName == "BGI_DominantGenes" || x.def.defName == "VRE_DominantGenome"));
            bool parentBHasRecessiveGenes = geneSetB.Any(x => x.Active && (x.def.defName == "BGI_RecessiveGenes" || x.def.defName == "VRE_RecessiveGenome"));
            bool parentBHasBinaryGenes = geneSetB.Any(x => x.Active && x.def.defName == "BGI_BinaryInheritance");

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

            var request = new PawnGenerationRequest(PawnKindDefOf.Colonist, Faction.OfPlayer, PawnGenerationContext.NonPlayer, -1, forceGenerateNewPawn: false, allowDead: false, allowDowned: true, canGeneratePawnRelations: true, mustBeCapableOfViolence: false, 1f, forceAddFreeWarmLayerIfNeeded: false, allowGay: true, allowPregnant: false, allowFood: true, allowAddictions: true, inhabitant: false, certainlyBeenInCryptosleep: false, forceRedressWorldPawnIfFormerColonist: false, worldPawnFactionDoesntMatter: false, 0f, 0f, null, 1f, null, null, null, null, null, null, null, null, null, null, null, null, forceNoIdeo: false, forceNoBackstory: false, forbidAnyTitle: false, forceDead: false, null, null, null, null, null, 0f, DevelopmentalStage.Newborn);
            Pawn babyPawn = PawnGenerator.GeneratePawn(request);

            // Create new GeneTracker
            //Pawn_GeneTracker geneTracker = new();

            var geneTracker = babyPawn.genes;

            // Remove all genes from the dummy pawn
            foreach (var gene in geneTracker.GenesListForReading.ToList())
            {
                geneTracker.RemoveGene(gene);
            }

            // Just to be sure.
            geneTracker.Xenogenes.Clear();
            geneTracker.Endogenes.Clear();

            // Add all genes from parentA to the dummy pawn
            foreach (var gene in geneDefsA)
            {
                geneTracker.AddGene(gene, false);
            }
            int endoMet = GetAllActiveEndoGenes(geneTracker).Sum(x => x.def.biostatMet);

            // If the dummy pawn has any gene not from the mother delete it. It is probably a hair or skin gene that the 
            // game pulled out of a magic hat.
            foreach (var gene in geneTracker.GenesListForReading.Where(x => !geneDefsA.Contains(x.def)).ToList())
            {
                geneTracker.RemoveGene(gene);
            }

            if (parentA != parentB)
            {
                // Add 25-75% of genes from father to the dummy pawn as xenogenes
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

                //GetAllActiveGenes(geneTracker).Sum(x => x.def.biostatMet);

                var finalXegenes = geneTracker.Xenogenes.Select(x => x.def).ToList();

                geneTracker.Xenogenes.Clear();

                foreach (var gene in finalXegenes)
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
                RemoveRandomToMetabolism(0, geneTracker, minMet: settings.metabolismLimit, exclusionList: genesSharedByBothParents);
                RemoveDueToMissingPrerequsite(geneTracker);
                RemoveRandomToMetabolism(0, geneTracker, minMet: settings.metabolismLimit);
                RemoveDueToMissingPrerequsite(geneTracker);

                // Integrate all the Xenogenes, turning them into Endogenes.
                IntegrateGenes(geneTracker);
            }
            else
            {
                parentB = null;
            }

            // Destroy baby
            babyPawn.Destroy();

            var allGeneDefsOnDummy = geneTracker.GenesListForReading.Select(x => x.def).ToList();

            if (pawnA != pawnB
                && Rand.Value < PregnancyUtility.InbredChanceFromParents(pawnA, pawnB, out var _)
                && !allGeneDefsOnDummy.Contains(GeneDefOf.Inbred))
            {
                allGeneDefsOnDummy.Add(GeneDefOf.Inbred);
            }

            return allGeneDefsOnDummy;
        }

        private static void RemoveDueToMissingPrerequsite(Pawn_GeneTracker geneTracker)
        {
            var toRemoveDueToMissingPrerequsite = new List<GeneDef>();
            // Iterrate through all the genes and make sure all prerequisites are met.
            foreach (var gene in geneTracker.GenesListForReading.Where(x => x.def.prerequisite != null))
            {
                if (!geneTracker.GenesListForReading.Select(x => x.def).Contains(gene.def.prerequisite))
                {
                    toRemoveDueToMissingPrerequsite.Add(gene.def);
                }
            }
            geneTracker.GenesListForReading.RemoveAll(x => toRemoveDueToMissingPrerequsite.Contains(x.def));
        }

        public static int Round(this float value)
        {
            return (int)Math.Round(value, MidpointRounding.AwayFromZero);
        }


        public static List<Gene> GetAllActiveEndoGenes(Pawn_GeneTracker geneTracker)
        {
            List<Gene> result = new List<Gene>();
            //if (pawn.genes == null) return result;

            var genes = geneTracker?.Endogenes;
            if (genes == null) return result;
            for (int i = 0; i < genes.Count; i++)
            {
                if (genes[i].Active)
                {
                    result.Add(genes[i]);
                }
            }
            return result;
        }

        public static List<Gene> GetAllActiveGenes(Pawn_GeneTracker geneTracker)
        {
            List<Gene> result = new List<Gene>();
            //if (pawn.genes == null) return result;

            var genes = geneTracker?.GenesListForReading;
            if (genes == null) return result;
            for (int i = 0; i < genes.Count; i++)
            {
                if (genes[i].Active)
                {
                    result.Add(genes[i]);
                }
            }
            return result;
        }

        public static void RemoveRandomToMetabolism(int initialMet, Pawn_GeneTracker genes, int minMet = -6, List<GeneDef> exclusionList = null)
        {
            exclusionList ??= new List<GeneDef>();
            int idx = 0;
            // Sum up the metabolism cost of the new genes
            while (genes.GenesListForReading.Where(x => x.Overridden == false).Sum(x => x.def.biostatMet) + initialMet < minMet || idx > 200)
            {
                if (genes.GenesListForReading.Count <= 1)
                    break;
                // Pick a random gene from the newGenes with a negative metabolism cost and remove it.
                var geneToRemove = genes.GenesListForReading.Where(x => x.def.biostatMet < 0 && !exclusionList.Contains(x.def)).RandomElement();
                if (geneToRemove != null)
                {
                    genes.RemoveGene(geneToRemove);
                    Log.Message($"DEBUG: Removed gene {geneToRemove.def.label} with metabolism cost {geneToRemove.def.biostatMet}. New metabololism level is {genes.GenesListForReading.Where(x => x.Overridden == false).Sum(x => x.def.biostatMet)}");
                }
                else
                {
                    break;
                }
                idx++;  // Ensure we don't get stuck in an infinite loop no matter what.
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
