using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using Verse;

namespace BGInheritance
{
    public static class XenotypeHandling
    {
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

        public static void SetXenotypIconEtc(Pawn baby, List<Pawn> parents)
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
                    var parentGenes = parent.genes.GenesListForReading.Select(x => x.def).ToList();
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

        public static XenotypeIconDef TryFindIconDef(Pawn parent)
        {
            var iconPath = parent.genes.Xenotype.iconPath;
            var xIconDef = DefDatabase<XenotypeIconDef>.AllDefsListForReading
                .FirstOrDefault(x => string.Equals(x.texPath, iconPath, StringComparison.OrdinalIgnoreCase));
            return xIconDef;
        }
    }
}

