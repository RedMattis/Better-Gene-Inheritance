using HarmonyLib;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Diagnostics.Eventing.Reader;
using System.Linq;
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
            //harmony.PatchAll();
            // Note the patches in "HarmonyPatches.cs are currently not being run. If PatchAll doesn't work they likely need to be called manually.

            patchType = typeof(BGI_HarmonyPatches);
            harmony.Patch(AccessTools.Method(typeof(PregnancyUtility), name:nameof(PregnancyUtility.GetInheritedGenes), new Type[]
            {
                typeof(Pawn),
                typeof(Pawn),
                typeof(bool).MakeByRefType()
            }), prefix: new HarmonyMethod(patchType, nameof(InheritancePostfix)));
        }
        //[HarmonyPostfix]
        public static bool InheritancePostfix(ref List<GeneDef> __result, Pawn father, Pawn mother, ref bool success)
        {
            try
            {
                Pawn_GeneTracker tracker1 = mother?.genes;
                Pawn_GeneTracker tracker2 = father?.genes;
                tracker1 ??= tracker2;
                tracker2 ??= tracker1;
                if (tracker1 == null || tracker2 == null)
                {
                    return true; // Continue to vanilla.
                }

                success = true;

                // Get GeneTracker order
                (var primaryTracker, var secondaryTracker) = GetGeneTrackerOrder(tracker1, tracker2);

                __result = GeneFunctions.GetChildGenes(primaryTracker, secondaryTracker);
                return false;
            }
            catch (Exception e)
            {
                Log.Error("Managed Exception in BGInheritance. Aborting inheritance changes: " + e.Message);
                return true;
            }
        }

        public static List<GeneDef> GetChildGenes(Pawn parentA, Pawn parentB)
        {
            var geneDefs = new List<GeneDef>();
            bool _ = false;
            InheritancePostfix(ref geneDefs, parentA, parentB, ref _);
            return geneDefs;
        }

        public static (Pawn_GeneTracker, Pawn_GeneTracker) GetGeneTrackerOrder(Pawn_GeneTracker parentA, Pawn_GeneTracker parentB)
        {
            bool parentAHasPrimaryGenes = parentA.GenesListForReading.Any(x => x.def.defName == "BGI_PrimaryGenes");
            bool parentBHasPrimaryGenes = parentB.GenesListForReading.Any(x => x.def.defName == "BGI_PrimaryGenes");

            bool parentAHasSecondaryGenes = parentA.GenesListForReading.Any(x => x.def.defName == "BGI_SecondaryGenes");
            bool parentBHasSecondaryGenes = parentB.GenesListForReading.Any(x => x.def.defName == "BGI_SecondaryGenes");

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


    public static class GeneFunctions
    {
        public static List<GeneDef> GetChildGenes(Pawn_GeneTracker parentA, Pawn_GeneTracker parentB)
        {
            var settings = BGInheritanceMain.settings;
            List<Gene> geneSetA;
            List<Gene> geneSetB;
            if (settings.inheritXenoGenes)
            {
                geneSetA = [.. parentA.GenesListForReading];
                geneSetB = [.. parentB.GenesListForReading];
            }
            else
            {
                geneSetA = [.. parentA.Endogenes];
                geneSetB = [.. parentB.Endogenes];
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

            // If inheritArchiteGenes is false, remove all archite genes from the gene sets unless inheritSharedArchiteGenes is true and the gene is shared by both parents.
            if (!settings.inheritArchiteGenes)
            {
                if (settings.inheritSharedArchiteGenes)
                {
                    geneDefsA.RemoveAll(x => x.biostatArc > 0 && !geneDefsB.Contains(x));
                    geneDefsB.RemoveAll(x => x.biostatArc > 0 && !geneDefsA.Contains(x));
                }
                else
                {
                    geneDefsA.RemoveAll(x => x.biostatArc > 0);
                    geneDefsB.RemoveAll(x => x.biostatArc > 0);
                }
            }
            // Remove other blacklisted genetypes
            // Check each gene via reflection to see if they have a property named "IsMutation" or "IsEvolution" if the property exists at all, remove them.
            geneDefsA.RemoveAll(x => AccessTools.Property(x.GetType(), "IsMutation") != null || AccessTools.Property(x.GetType(), "IsEvolution") != null);
            geneDefsB.RemoveAll(x => AccessTools.Property(x.GetType(), "IsMutation") != null || AccessTools.Property(x.GetType(), "IsEvolution") != null);

            var genesSharedByBothParents = geneDefsA.Intersect(geneDefsB).ToList();
            int parentBGeneCount = geneDefsB.Count();

            // Add 20% to 100% of ParentB's Genes to transfer:
            int minimum = (int)(parentBGeneCount * 0.1);
            int maximum = parentBGeneCount - 1;
            int numberOfGenesToTransfer = Rand.RangeInclusive(minimum, maximum);

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
            while (genes.GenesListForReading.Where(x=>x.Overridden == false).Sum(x => x.def.biostatMet) + initialMet < minMet || genes.GenesListForReading.Count <= 1 || idx > 200)
            {
                if (genes.GenesListForReading.Count == 1)
                    break;
                // Pick a random gene from the newGenes with a negative metabolism cost and remove it.
                var geneToRemove = genes.GenesListForReading.Where(x => x.def.biostatMet < 0 && !exclusionList.Contains(x.def)).RandomElement();
                if (geneToRemove != null)
                {
                    genes.RemoveGene(geneToRemove);
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