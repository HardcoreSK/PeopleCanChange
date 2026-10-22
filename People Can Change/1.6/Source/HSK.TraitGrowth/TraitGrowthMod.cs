using UnityEngine;
using Verse;

namespace HSK.TraitGrowth
{
    public class TraitGrowthSettings : ModSettings
    {
        public bool enableMoodGrowth = true;
        public bool updatePassions = true;
        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref enableMoodGrowth, "enableMoodGrowth", true);
            Scribe_Values.Look(ref updatePassions, "updatePassions", true);
        }
    }

    public class TraitGrowthMod : Mod
    {
        public static TraitGrowthSettings Settings;
        public TraitGrowthMod(ModContentPack content) : base(content)
        {
            Settings = GetSettings<TraitGrowthSettings>();
        }
        public override string SettingsCategory() => "HSKTraitGrowth_Title".Translate();
        public override void DoSettingsWindowContents(Rect inRect)
        {
            var list = new Listing_Standard();
            list.Begin(inRect);
            list.Label("HSKTraitGrowth_Summary".Translate());
            list.Gap();
            list.CheckboxLabeled("HSKTraitGrowth_EnableMoodGrowth".Translate(), ref Settings.enableMoodGrowth);
            list.CheckboxLabeled("HSKTraitGrowth_UpdatePassions".Translate(), ref Settings.updatePassions);
            list.End();
        }
    }
}
