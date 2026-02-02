using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using RimWorld;
using UnityEngine;
using Verse;

namespace PCC_Code
{
    public class PCCSettings : ModSettings
    {
		public float maxTraits = 4;
        public float minTraits = 1;
        public float freqChange = 10;
        public float freqRemove = 10; // chance for event to occur, out of 10
        public bool sexualitiesAllowedToChange = false; // defaults to false by verdict of friends more into social justice than me
        public bool moddedTraits = true;
        public bool usingNewSelector = true;
        public int ageLowerThreshold = 0;

        Dictionary<string, TraitSetting> traitSettings = new Dictionary<string, TraitSetting>(); // where the string is the defName of the TraitDef in question

        public TraitSetting GetTraitSetting(string t) {
            if (traitSettings == null) Log.Message("Null traitSettings!");
            if (!traitSettings.ContainsKey(t))
            {
                if (PCC_Mod.IncompatibleTraits.Contains(t)) traitSettings[t] = TraitSetting.NeverChangeModConflict;
                else traitSettings[t] = TraitSetting.AnyChange;
            }
            return traitSettings[t];
        }
        public void SetTraitSetting(string key, TraitSetting value) {            
            traitSettings[key] = value;
        }

        //public List<string> unloadedTraits = new List<string>();
        public bool hideTraitList = true; // not a saved value, just used for remembering whether the list is minimized, always starts minimized to ease clutter

        public bool doOptimistDepressive = true;

        public override void ExposeData()
        {
            base.ExposeData();
			Scribe_Values.Look(ref maxTraits, "maxTraits", 4f);
            Scribe_Values.Look(ref minTraits, "minTraits", 1f);
            Scribe_Values.Look(ref freqChange, "FreqChange", 10f);
            Scribe_Values.Look(ref freqRemove, "FreqRemove", 10f);
            Scribe_Values.Look(ref sexualitiesAllowedToChange, "gayAllowedToChange", false); // the tag is called gay as a holdover from previous versions, so it's save-compatible
            Scribe_Values.Look(ref moddedTraits, "moddedTraits", true);
            Scribe_Values.Look(ref usingNewSelector, "usingNewSelector", true);
            Scribe_Values.Look(ref doOptimistDepressive, "doOptimistDepressive", true);
            Scribe_Values.Look(ref ageLowerThreshold, "ageLowerThreshold", 0);

            Scribe_Collections.Look(ref traitSettings, "traitSettings", LookMode.Value);
            if (traitSettings == null) traitSettings = new Dictionary<string, TraitSetting>();
            else
            {
                foreach (string def in PCC_Mod.IncompatibleTraits)
                {
                    traitSettings[def] = TraitSetting.NeverChangeModConflict;
                }
            }
            //Scribe_Collections.Look(ref unloadedTraits, "unloadedTraits", LookMode.Value);
        }

        public enum TraitSetting
        {
            NeverChangeModConflict, // never change but user will not have the option to change this setting, because it would break something
            NeverChange,
            AnyChange,
            AddNoRemove,
            RemoveNoAdd
        }
    }
    class PCC_Mod : Mod
    {
        //private bool recheckDefList = true;
        public static Vector2 scrollPosition = new Vector2();
        int pageNum = 1;
        int entriesPerPage = 20;


        public static PCCSettings settings;
        public PCC_Mod(ModContentPack content) : base(content)
        {
            settings = GetSettings<PCCSettings>();
        }
        public override string SettingsCategory() => "PCC_ModTitle".Translate();

        Func<TraitDef, PCCSettings.TraitSetting> getTraitPayload = (TraitDef t) => settings.GetTraitSetting(t.defName);

        public override void DoSettingsWindowContents(Rect inRect) {
            Listing_Standard ls = new Listing_Standard();
            Rect rect = new Rect(inRect.x, inRect.y, inRect.width, inRect.height);
            Rect rect2 = new Rect(0f, 0f, inRect.width - 16f, inRect.height + (entriesPerPage+2) * 55); // Add 2 to give some extra padding for bottom elements. Previous formula: + DefDatabase<TraitDef>.AllDefs.Count() * 55
            Widgets.BeginScrollView(rect, ref scrollPosition, rect2, true);
            ls.Begin(rect2);
            ls.Label("PCC_MaxTraitsExplanation".Translate());
            settings.maxTraits = ls.Slider(settings.maxTraits, settings.minTraits, 10);
            ls.Label("PCC_CurrentValue".Translate() + ((int)settings.maxTraits).ToString());
            
            ls.Label("PCC_MinTraitsExplanation".Translate());
            settings.minTraits = ls.Slider(settings.minTraits, 0, 3);
            ls.Label("PCC_CurrentValue".Translate() + ((int)settings.minTraits).ToString());
            
            ls.Label("PCC_ChangeFreq".Translate() + ((int)settings.freqChange).ToString());
            settings.freqChange = ls.Slider(settings.freqChange, 0, 10);
            ls.Label("PCC_RemoveFreq".Translate() + ((int)settings.freqRemove).ToString());
            settings.freqRemove = ls.Slider(settings.freqRemove, 0, 10);

            ls.CheckboxLabeled("PCC_DoOptimistDepressive".Translate(), ref settings.doOptimistDepressive);

            ls.Label("PCC_AgeThresExplanation".Translate() + ((int)settings.ageLowerThreshold).ToString());
            settings.ageLowerThreshold = (int)ls.Slider(settings.ageLowerThreshold, 0, 25);

            ls.Label("PCC_OddsExplanation".Translate());
            if (!settings.usingNewSelector) { // old trait selector
                ls.CheckboxLabeled("PCC_AllowSexTraitsSimple".Translate(), ref settings.sexualitiesAllowedToChange);
                ls.CheckboxLabeled("PCC_UseModdedTraitsSimple".Translate(), ref settings.moddedTraits);
                if (ls.ButtonText("PCC_AdvancedSettings".Translate())) {
                    settings.usingNewSelector = true;
                }
            }
            else {
                ls.Label("PCC_SettingsListBegin".Translate());
                if (settings.hideTraitList) {
                    if (ls.ButtonText("PCC_Show".Translate())) {
                        settings.hideTraitList = false;
                    }
                }
                else {
                    if (ls.ButtonText("PCC_Hide".Translate())) {
                        settings.hideTraitList = true;
                    }
                    ls.Gap();

                    if (ls.ButtonText("PCC_Next".Translate()))
                    {
                        if (pageNum < ((DefDatabase<TraitDef>.AllDefs.Count() + entriesPerPage - 1) / entriesPerPage)) // First page is 1. Formula: divide and round up a by b: (a+b-1)/b for integer division
                            pageNum++;
                    }
                    if (ls.ButtonText("PCC_Previous".Translate()))
                    {
                        if (pageNum > 1) // First page is 1 
                            pageNum--;
                    }
                    ls.Gap();

                    List<TraitDef> l = DefDatabase<TraitDef>.AllDefs.OrderBy(item => item.defName).ToList();
                    foreach (TraitDef def in l.Skip((pageNum-1)*entriesPerPage).Take(entriesPerPage)) { // Should skip Defs from mods not currently loaded, which will have settings saved but no Def to find them from
                        ls.Label(def.defName + " (" + def.degreeDatas[0].label + "): ");                        
                        Widgets.Dropdown(ls.GetRect(25f), def, getTraitPayload, DropdownForTrait, TraitSettingString(settings.GetTraitSetting(def.defName)));
                        ls.Gap();
                    }
                }
                
                ls.Label("PCC_SomeTraitsStillChange".Translate());
                if (ls.ButtonText("PCC_SimpleSettings".Translate())) {
                    settings.usingNewSelector = false;
                }
            }
            //settings.disabledDefs = settings.disabledDefs.OrderBy(item => item.defName).ToList();
            settings.Write();
            ls.End();
            Widgets.EndScrollView();
        }

        private static IEnumerable<Widgets.DropdownMenuElement<PCCSettings.TraitSetting>> DropdownForTrait(TraitDef trait)
        {
            if (IncompatibleTraits.Contains(trait.defName))
            {
                yield return new Widgets.DropdownMenuElement<PCCSettings.TraitSetting>
                {
                    option = new FloatMenuOption(TraitSettingString(PCCSettings.TraitSetting.NeverChangeModConflict), delegate
                    {
                        settings.SetTraitSetting(trait.defName, PCCSettings.TraitSetting.NeverChangeModConflict);
                    }),
                    payload = PCCSettings.TraitSetting.NeverChangeModConflict
                };
            } else
            {
                yield return new Widgets.DropdownMenuElement<PCCSettings.TraitSetting>
                {
                    option = new FloatMenuOption(TraitSettingString(PCCSettings.TraitSetting.AnyChange), delegate
                    {
                        settings.SetTraitSetting(trait.defName, PCCSettings.TraitSetting.AnyChange);
                    }),
                    payload = PCCSettings.TraitSetting.AnyChange
                };
                yield return new Widgets.DropdownMenuElement<PCCSettings.TraitSetting>
                {
                    option = new FloatMenuOption(TraitSettingString(PCCSettings.TraitSetting.AddNoRemove), delegate
                    {
                        settings.SetTraitSetting(trait.defName, PCCSettings.TraitSetting.AddNoRemove);
                    }),
                    payload = PCCSettings.TraitSetting.AddNoRemove
                };
                yield return new Widgets.DropdownMenuElement<PCCSettings.TraitSetting>
                {
                    option = new FloatMenuOption(TraitSettingString(PCCSettings.TraitSetting.RemoveNoAdd), delegate
                    {
                        settings.SetTraitSetting(trait.defName, PCCSettings.TraitSetting.RemoveNoAdd);
                    }),
                    payload = PCCSettings.TraitSetting.RemoveNoAdd
                };
                yield return new Widgets.DropdownMenuElement<PCCSettings.TraitSetting>
                {
                    option = new FloatMenuOption(TraitSettingString(PCCSettings.TraitSetting.NeverChange), delegate
                    {
                        settings.SetTraitSetting(trait.defName, PCCSettings.TraitSetting.NeverChange);
                    }),
                    payload = PCCSettings.TraitSetting.NeverChange
                };
            }
            /*foreach (DrugPolicy assignedDrugs in Current.Game.drugPolicyDatabase.AllPolicies)
            {
                yield return new Widgets.DropdownMenuElement<DrugPolicy>
                {
                    option = new FloatMenuOption(assignedDrugs.label, delegate
                    {
                        pawn.drugs.CurrentPolicy = assignedDrugs;
                    }),
                    payload = assignedDrugs
                };
            }*/
        }

        public static string TraitSettingString(PCCSettings.TraitSetting s)
        {
            switch (s)
            {
                case PCCSettings.TraitSetting.NeverChangeModConflict:
                    return "PCC_TSNeverChangeModConflict".Translate();
                case PCCSettings.TraitSetting.NeverChange:
                    return "PCC_TSNeverChange".Translate();
                case PCCSettings.TraitSetting.AnyChange:
                    return "PCC_TSAnyChange".Translate();
                case PCCSettings.TraitSetting.AddNoRemove:
                    return "PCC_TSAddNoRemove".Translate();
                case PCCSettings.TraitSetting.RemoveNoAdd:
                    return "PCC_TSRemoveNoAdd".Translate();
            }
            return "";
        }

        public static readonly string[] IncompatibleTraits =
        {
            "VPE_Thrall",
            // begin A Rimworld Of Magic
            "TM_ArcaneConduitTD",
            "TM_ManaWellTD",
            "TM_BoundlessTD",
            "TM_FaeBloodTD",
            "TM_GiantsBloodTD",
            "Undead",
            "TM_Gifted",
            "TM_Empath",
            "InnerFire",
            "HeartOfFrost",
            "StormBorn",
            "Arcanist",
            "Druid",
            "Paladin",
            "Summoner",
            "Necromancer",
            "Lich",
            "Priest",
            "TM_Bard",
            "Succubus",
            "Warlock",
            "Geomancer",
            "Technomancer",
            "BloodMage",
            "Enchanter",
            "Chronomancer",
            "ChaosMage",
            "TM_TheShadow",
            "TM_Brightmage",
            "TM_Shaman",
            "TM_Golemancer",
            "TM_Possessed",
            "TM_Possessor",
            "PhysicalProdigy",
            "TM_Apothecary",
            "Gladiator",
            "Ranger",
            "TM_Sniper",
            "Bladedancer",
            "Faceless",
            "TM_Psionic",
            "DeathKnight",
            "TM_Monk",
            "TM_Commander",
            "TM_SuperSoldier", // end A Rimworld Of Magic
            // begin Kure's Rimworld of Magic Class Expansion Pack
            "TM_RedMage",
"TM_Ninja",
"TM_ElementalGunner",
"TM_Bombardier",
"TM_Railbreaker",
"TM_Samurai",
"TM_Gadgeteer",
"TM_WindMage",
"TM_Medic",
"TM_ChromaMage",
"TM_Harbinger",
"TM_Reaper",
"TM_Berserker",
"TM_Scion",
"TM_Plaguedoctor",
"TM_Cleric",
"TM_Arbormancer",
"TM_Chainwarden",
"TM_Ambusher",
"TM_Saboteur",
"TM_Keeper",
"TM_Pridestalker",
"TM_Starseeker",
"TM_Aspirant",
"TM_Hawkeye",
"TM_Atronach",
"TM_SupportSpecialist",
"TM_Scrapper",
"TM_Oracle",
"TM_Mystic",
"TM_Spellbreaker",
"TM_Duskbringer",
"TM_Thunderblessed",
"TM_Deadeye",
"TM_Spellthief",
"TM_Dragoon",
"TM_BlackMage",
"TM_Scholar",
"TM_Gunbreaker",
"TM_Gunner",
"TM_Warrior",
"TM_Nomad",
"TM_Guardian",
"TM_Holosmith",
"TM_Troubadour",
"TM_Gambler",
"TM_Chef",
"TM_Miner",
"TM_Striker",
"TM_RuneKnight",
"TM_BlossomBlader",
"TM_Ravenous",
"TM_MechHunter",
"TM_Gunslinger",
"TM_Shadowbringer",
"TM_Warden",
"TM_Chempunk",
"TM_Explorer",
"TM_Architect"
            // end Kure's Rimworld of Magic Class Expansion Pack
        };
    }
}
