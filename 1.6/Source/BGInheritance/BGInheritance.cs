
using Verse;
using HarmonyLib;
using UnityEngine;
using RimWorld;
using System;
using System.Reflection;
//using VariedBodySizes;
using static BGInheritance.SettingsWidgets;

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
        }

        public override void DoSettingsWindowContents(Rect inRect)
        {
            Listing_Standard listStd = new();

            listStd.Begin(inRect);
            if (listStd.ButtonText("BGI_ResetSettings".Translate()))
            {
                ResetToDefault();
            }
            CreateSettingsSlider(listStd,"BGI_ParentBGeneInheritanceChanceMin".Translate(), ref settings.secondMinPercent, min:-1f, max:2f, (f) => $"{f:F2}");
            CreateSettingsSlider(listStd, "BGI_ParentBGeneInheritanceChanceMax".Translate(), ref settings.secondMaxPercent, min:-1f, max:2f, (f) => $"{f:F2}");

            CreateSettingsSlider(listStd, "BGI_InheritXeno".Translate(), ref settings.inheritXenoGenes, min: -0f, max: 1f, (f) => $"{f:F2}");

            listStd.CheckboxLabeled("BGI_InheritSharedArchite".Translate(), ref settings.inheritSharedArchiteGenes, 0);
            CreateSettingsSlider(listStd, "BGI_InheritArchite".Translate(), ref settings.inheritArchiteGenes, min: -0f, max: 1f, (f) => $"{f:F2}");

            listStd.Label("BGI_MetabolismLimit".Translate() + ": " + settings.metabolismLimit);
            settings.metabolismLimit = (int)listStd.Slider(settings.metabolismLimit, -99, 0);
            listStd.End();
            base.DoSettingsWindowContents(inRect);
        }

        public void ResetToDefault()
        {
            settings.secondMinPercent = BGInheritance.secondMinPercentDefault;
            settings.secondMaxPercent = BGInheritance.secondMaxPercentDefault;
            settings.inheritSharedArchiteGenes = BGInheritance.inheritSharedArchiteGenesDefault;
            settings.inheritArchiteGenes = BGInheritance.inheritArchiteGenesDefault;
            settings.inheritXenoGenes = BGInheritance.inheritXenoGenesDefault;
            settings.metabolismLimit = BGInheritance.metabolismLimitDefault;
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
        public float inheritArchiteGenes = inheritArchiteGenesDefault;
        public float inheritXenoGenes = inheritXenoGenesDefault;
        public float secondMinPercent = secondMinPercentDefault;
        public float secondMaxPercent = secondMaxPercentDefault;
        public int metabolismLimit = metabolismLimitDefault;

        public const bool inheritSharedArchiteGenesDefault = true;
        public const float inheritArchiteGenesDefault = 1f;
        public const float inheritXenoGenesDefault = 1f;
        public const float secondMinPercentDefault = 0.1f;
        public const float secondMaxPercentDefault = 1f;
        public const int metabolismLimitDefault = -4;

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref secondMinPercent, "secondMinPercent", secondMinPercentDefault);
            Scribe_Values.Look(ref secondMaxPercent, "secondMaxPercent", secondMaxPercentDefault);
            Scribe_Values.Look(ref inheritSharedArchiteGenes, "inheritSharedArchiteGenes", inheritSharedArchiteGenesDefault);
            Scribe_Values.Look(ref inheritArchiteGenes, "inheritArchiteGenes", inheritArchiteGenesDefault);
            Scribe_Values.Look(ref inheritXenoGenes, "inheritXenoGenes", inheritXenoGenesDefault);
            Scribe_Values.Look(ref metabolismLimit, "metabolismLimit", metabolismLimitDefault);
            
        }
    }
}
