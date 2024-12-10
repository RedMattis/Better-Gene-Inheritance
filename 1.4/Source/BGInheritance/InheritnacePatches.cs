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

            patchType = typeof(BGI_HarmonyPatches);
            harmony.Patch(AccessTools.Method(typeof(PregnancyUtility), name:nameof(PregnancyUtility.GetInheritedGenes), new Type[]
            {
                typeof(Pawn),
                typeof(Pawn),
                typeof(bool).MakeByRefType()
            }), null, postfix: new HarmonyMethod(patchType, nameof(InheritancePostfix)));
        }
        //[HarmonyPostfix]
        public static void InheritancePostfix(ref List<GeneDef> __result, Pawn father, Pawn mother, ref bool success)
        {
            Pawn_GeneTracker tracker1 = mother?.genes;
            Pawn_GeneTracker tracker2 = father?.genes;
            tracker1 ??= tracker2;
            tracker2 ??= tracker1;
            if (tracker1 == null || tracker2 == null)
            {
                return;
            }


            // Randomize which parent to inherit the most genes from.
            if (Rand.Chance(0.5f))
            {
                __result = GeneFunctions.GetChildGenes(tracker1, tracker2);
            }
            else
            {
                __result = GeneFunctions.GetChildGenes(tracker2, tracker1);
            }

            //if (success)
            //{
            //    //try
            //    //{
                    
            //    //}
            //    //catch (Exception e)
            //    //{
            //    //    Log.Error($"Error in inheritance.\n{e.Message}\n\nUsing vanilla inheritance instead.");
            //    //}
            //}
        }
    }


    public static class GeneFunctions
    {
        public static List<GeneDef> GetChildGenes(Pawn_GeneTracker parentA, Pawn_GeneTracker parentB)
        {
            var settings = BGInheritanceMain.settings;
            List<GeneDef> geneSetA;
            List<GeneDef> geneSetB;
            if (settings.inheritXenoGenes)
            {
                geneSetA = parentA.GenesListForReading.Select(x => x.def).ToList();
                geneSetB = parentB.GenesListForReading.Select(x => x.def).ToList();
            }
            else
            {
                geneSetA = parentA.Endogenes.Select(x => x.def).ToList();
                geneSetB = parentB.Endogenes.Select(x => x.def).ToList();
            }

            bool parentAHasDominantGenes = geneSetA.Any(x => x.defName == "BGI_DominantGenes" || x.defName == "VRE_DominantGenome");
            bool parentBHasDominantGenes = geneSetB.Any(x => x.defName == "BGI_DominantGenes" || x.defName == "VRE_DominantGenome");
            bool parentAHasRecessiveGenes = geneSetA.Any(x => x.defName == "BGI_RecessiveGenes" || x.defName == "VRE_RecessiveGenome");
            bool parentBHasRecessiveGenes = geneSetB.Any(x => x.defName == "BGI_RecessiveGenes" || x.defName == "VRE_RecessiveGenome");

            if (parentAHasDominantGenes && !parentBHasDominantGenes)
            {
                return geneSetA;
            }
            else if (!parentAHasDominantGenes && parentBHasDominantGenes)
            {
                return geneSetB;
            }
            else if (parentAHasRecessiveGenes && !parentBHasRecessiveGenes)
            {
                return geneSetB;
            }
            else if (!parentAHasRecessiveGenes && parentBHasRecessiveGenes)
            {
                return geneSetA;
            }
            
            // If inheritArchiteGenes is false, remove all archite genes from the gene sets unless inheritSharedArchiteGenes is true and the gene is shared by both parents.
            if (!settings.inheritArchiteGenes)
            {
                if (settings.inheritSharedArchiteGenes)
                {
                    geneSetA.RemoveAll(x => x.biostatArc > 0 && !geneSetB.Contains(x));
                    geneSetB.RemoveAll(x => x.biostatArc > 0 && !geneSetA.Contains(x));
                }
                else
                {
                    geneSetA.RemoveAll(x => x.biostatArc > 0);
                    geneSetB.RemoveAll(x => x.biostatArc > 0);
                }            
            }
            var genesSharedByBothParents = geneSetA.Intersect(geneSetB).ToList();
            int parentBGeneCount = geneSetB.Count();

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
            babyPawn.genes.Xenogenes.Clear();
            babyPawn.genes.Endogenes.Clear();

            // Add all genes from parentA to the dummy pawn
            foreach (var gene in geneSetA)
            {
                geneTracker.AddGene(gene, false);
            }
            int endoMet = GetAllActiveEndoGenes(geneTracker).Sum(x => x.def.biostatMet);

            // If the dummy pawn has any gene not from the mother delete it. It is probably a hair or skin gene that the 
            // game pulled out of a magic hat.
            foreach (var gene in geneTracker.GenesListForReading.Where(x => !geneSetA.Contains(x.def)).ToList())
            {
                geneTracker.RemoveGene(gene);
            }

            if (parentA != parentB)
            {
                // Add 25-75% of genes from father to the dummy pawn as xenogenes
                int count = 0;
                var bGenes = new List<GeneDef>();
                while (count < numberOfGenesToTransfer && geneSetB.Count > 0)
                {
                    var gene = geneSetB.RandomElement();
                    geneSetB.Remove(gene);
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

                // Remove all overriden genes from the baby pawn
                foreach (var gene in geneTracker.GenesListForReading.Where(x => x.Overridden).ToList())
                {
                    geneTracker.RemoveGene(gene);
                }

                RemoveRandomToMetabolism(0, geneTracker, minMet: settings.metabolismLimit, exclusionList: genesSharedByBothParents);
                RemoveRandomToMetabolism(0, geneTracker, minMet: settings.metabolismLimit);

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