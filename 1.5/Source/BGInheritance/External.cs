using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse;

namespace BGInheritance
{
    /// <summary>
    /// Methods made to be called by other mods.
    /// </summary>
    public static class External
    {
        public static List<GeneDef> GetChildGenes(Pawn parentA, Pawn parentB)
        {
            var geneDefs = new List<GeneDef>();
            bool _ = false;
            HarmonyPatches.InheritancePrefix(ref geneDefs, parentA, parentB, ref _);
            return geneDefs;
        }
    }
}
