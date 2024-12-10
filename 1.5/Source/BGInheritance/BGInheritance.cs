
using Verse;
using HarmonyLib;
using UnityEngine;
using RimWorld;
using System;
using System.Reflection;
//using VariedBodySizes;

namespace BGInheritance
{
    /// <summary>
    /// Better Children Skill Learning multiplies XP by 3 for children.
    /// </summary>
    [StaticConstructorOnStartup]
    internal class BGInheritanceMain : Mod
    {
        
        public static BGInheritanceMain instance = null;
        public static BGInheritance settings;

        //private string extractionBuff, multipakBuff, megaPackBuff, geneRegrowBuff, nutritionUse;
        public BGInheritanceMain(ModContentPack content) : base(content)
        {
            instance = this;
            settings = GetSettings<BGInheritance>();
            var minMetabolism = Mathf.Min(settings.metabolismLimit, -5);
            HarmonyPatches.SetBiostatRange(minMetabolism);
        }

        public override void DoSettingsWindowContents(Rect inRect)
        {
            Listing_Standard listStd = new Listing_Standard();

            listStd.Begin(inRect);

            listStd.CheckboxLabeled("BGI_InheritSharedArchite".Translate(), ref settings.inheritSharedArchiteGenes, 0);
            listStd.CheckboxLabeled("BGI_InheritArchite".Translate(), ref settings.inheritArchiteGenes, 0);
            listStd.CheckboxLabeled("BGI_InheritXeno".Translate(), ref settings.inheritXenoGenes, 0);
            //listStd.SliderLabeled("BGI_ParentBGeneInheritanceChance".Translate(), settings.parentBGeneInheritanceChance, 0f, 1f);
            listStd.Label("BGI_MetabolismLimit".Translate() + ": " + settings.metabolismLimit);
            settings.metabolismLimit = (int)listStd.Slider(settings.metabolismLimit, -99, 0);
            listStd.End();
            base.DoSettingsWindowContents(inRect);
        }

        public override string SettingsCategory()
        {
            return "BGInheritance".Translate();
        }

        //public static int GetBioStatRange()
        //{
        //    return Mathf.Min(settings.metabolismLimit, -5); ;
        //}
    }

    public class BGInheritance: ModSettings
    {
        public bool inheritSharedArchiteGenes = inheritSharedArchiteGenesDefault;
        public bool inheritArchiteGenes = inheritArchiteGenesDefault;
        public bool inheritXenoGenes = inheritXenoGenesDefault;
        //public float parentBGeneInheritanceChance = parentBGeneInheritanceChanceDefault;
        public int metabolismLimit = metabolismLimitDefault;

        public const bool inheritSharedArchiteGenesDefault = true;
        public const bool inheritArchiteGenesDefault = true;
        public const bool inheritXenoGenesDefault = true;
        //public const float parentBGeneInheritanceChanceDefault = 0.5f;
        public const int metabolismLimitDefault = -4;

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref inheritSharedArchiteGenes, "inheritSharedArchiteGenes", inheritSharedArchiteGenesDefault);
            Scribe_Values.Look(ref inheritArchiteGenes, "inheritArchiteGenes", inheritArchiteGenesDefault);
            Scribe_Values.Look(ref inheritXenoGenes, "inheritXenoGenes", inheritXenoGenesDefault);
            //Scribe_Values.Look(ref parentBGeneInheritanceChance, "parentBGeneInheritanceChance", parentBGeneInheritanceChanceDefault);
            Scribe_Values.Look(ref metabolismLimit, "metabolismLimit", metabolismLimitDefault);
            
        }
    }
}
