using System;
using System.Collections.Generic;
using System.Linq;
using AlienRace;
using RimWorld;
using SK;
using UnityEngine;
using Verse;
using Verse.AI;

namespace HSK.TraitGrowth
{
    // HSK-specific fork: use Core_SK and HAR directly.
    internal static class HSKTraitChanges
    {
        private static readonly HashSet<string> RandomBlacklist = new HashSet<string>
        {
            "Brawler", "Gay", "AnnoyingVoice", "CreepyBreathing", "Pyromaniac",
            "SpeedOffset", "Neurotic", "Beauty", "AIPawn_BaseTrait", "AIRobot_BaseTrait"
        };
        private static int cachedLimitTick = -1;
        private static int cachedWorldLimit;

        internal static bool IsMechanical(Pawn pawn) => SK_Utility.isMechanical(pawn);

        internal static bool RandomTraitAllowed(Pawn pawn, TraitDef def)
        {
            return def != null && !RandomBlacklist.Contains(def.defName)
                && def.GetGenderSpecificCommonality(pawn.gender) > 0.01f;
        }

        internal static int TraitLimit
        {
            get
            {
                int tick = Find.TickManager.TicksGame;
                if (cachedLimitTick != tick)
                {
                    cachedLimitTick = tick;
                    cachedWorldLimit = 3;
                    foreach (Pawn pawn in Find.WorldPawns.AllPawnsAlive)
                        if (pawn.RaceProps.Humanlike && pawn.story?.traits != null)
                            cachedWorldLimit = Math.Max(cachedWorldLimit, pawn.story.traits.allTraits.Count);
                }
                return cachedWorldLimit;
            }
        }

        internal static bool CanRemove(Trait trait)
        {
            return trait != null && trait.sourceGene == null && !trait.Suppressed
                && trait.def != null;
        }

        private static bool LuxuryTrait(TraitDef def) => def.defName == "Jealous" || def.defName == "Greedy" || def.defName == "Ascetic";

        private static List<Trait> ReplacedTraits(Pawn pawn, TraitDef def)
        {
            return pawn.story.traits.allTraits.Where(t => t.def == def || (LuxuryTrait(def) && LuxuryTrait(t.def))).ToList();
        }

        internal static bool CanGain(Pawn pawn, TraitDef def, int degree = 0, bool respectLimit = true)
        {
            if (def == null || !def.degreeDatas.Any(d => d.degree == degree)
                || def == null || AlienDisallows(pawn, def)) return false;
            List<Trait> replaced = ReplacedTraits(pawn, def);
            if (replaced.Any(t => !CanRemove(t) || (t.def == def && t.Degree == degree))) return false;
            if (respectLimit && replaced.Count == 0 && pawn.story.traits.allTraits.Count >= TraitLimit) return false;
            if (def.defName == "ShootingAccuracy" && pawn.story.traits.HasTrait(TraitDefOf.Brawler)) return false;
            return !pawn.story.traits.allTraits.Except(replaced).Any(t =>
                t.def.conflictingTraits?.Contains(def) == true || def.conflictingTraits?.Contains(t.def) == true);
        }

        internal static bool Gain(Pawn pawn, TraitDef def, int degree = 0, bool letter = true, bool respectLimit = true)
        {
            if (!CanGain(pawn, def, degree, respectLimit)) return false;
            List<Trait> replaced = ReplacedTraits(pawn, def);
            foreach (Trait old in replaced) pawn.story.traits.RemoveTrait(old);
            Trait gained = new Trait(def, degree);
            pawn.story.traits.GainTrait(gained);
            if (!pawn.story.traits.allTraits.Contains(gained))
            {
                foreach (Trait old in replaced) pawn.story.traits.GainTrait(old);
                return false;
            }
            Notify(pawn, replaced.Select(t => t.def).Concat(new[] { def }));
            if (letter && IsPlayerPawn(pawn))
            {
                LetterDef letterDef = def.defName == "NaturalMood"
                    ? (degree > 0 ? LetterDefOf.PositiveEvent : LetterDefOf.NegativeEvent)
                    : (def.defName == "Cannibal" ? LetterDefOf.NegativeEvent : LetterDefOf.NeutralEvent);
                Find.LetterStack.ReceiveLetter("HSKTraitGrowth_TraitChanged".Translate(),
                    "HSKTraitGrowth_GainedTrait".Translate(pawn, gained.Label), letterDef, pawn);
            }
            return true;
        }

        internal static bool IsPlayerPawn(Pawn pawn)
        {
            return pawn.IsColonist || pawn.IsPrisonerOfColony || pawn.IsSlaveOfColony;
        }

        private static bool AlienDisallows(Pawn pawn, TraitDef def)
        {
            if (pawn == null || def == null
                || pawn.def is not ThingDef_AlienRace alien
                || alien.alienRace?.generalSettings?.disallowedTraits == null)
            {
                return false;
            }

            foreach (var entry in alien.alienRace.generalSettings.disallowedTraits)
            {
                // AlienChanceEntry.entry is deserialized from XML and can be
                // null for malformed/legacy race settings.
                if (entry?.entry?.def == def)
                {
                    return true;
                }
            }
            return false;
        }

        internal static void Notify(Pawn pawn, IEnumerable<TraitDef> changed)
        {
            pawn.Notify_DisabledWorkTypesChanged();
            pawn.skills?.Notify_SkillDisablesChanged();
            pawn.needs?.mood?.thoughts?.situational?.Notify_SituationalThoughtsDirty();
            if (!Controller.Settings.PassionUpdate || !TraitGrowthMod.Settings.updatePassions || pawn.skills == null) return;
            var affected = new HashSet<SkillDef>();
            if (changed != null)
                foreach (TraitDef def in changed)
                    foreach (TraitDegreeData degree in def.degreeDatas)
                        CollectSkills(degree, affected);
            var scores = new Dictionary<SkillDef, int>();
            foreach (Trait trait in pawn.story.traits.allTraits.Where(t => !t.Suppressed))
            {
                foreach (var gain in trait.CurrentData.skillGains ?? Enumerable.Empty<SkillGain>())
                    AddScore(scores, gain.skill, Math.Sign(gain.amount));
                if (trait.CurrentData.statOffsets == null) continue;
                foreach (var offset in trait.CurrentData.statOffsets)
                {
                    if (offset.stat == null) continue;
                    if (offset.stat.skillNeedFactors != null)
                        foreach (var need in offset.stat.skillNeedFactors)
                            AddScore(scores, need.skill, Math.Sign(offset.value));
                    if (offset.stat.skillNeedOffsets != null)
                        foreach (var need in offset.stat.skillNeedOffsets)
                            AddScore(scores, need.skill, Math.Sign(offset.value));
                }
            }
            // Like HSK, trait-derived scores replace passion on the affected skills only.
            foreach (SkillDef skill in changed == null ? scores.Keys : (IEnumerable<SkillDef>)affected)
                if (!pawn.skills.GetSkill(skill).TotallyDisabled)
                {
                    scores.TryGetValue(skill, out int score);
                    pawn.skills.GetSkill(skill).passion = (Passion)Mathf.Clamp(score, 0, 2);
                }
        }

        private static void CollectSkills(TraitDegreeData data, HashSet<SkillDef> skills)
        {
            if (data.skillGains != null)
                foreach (var gain in data.skillGains) skills.Add(gain.skill);
            if (data.statOffsets == null) return;
            foreach (var offset in data.statOffsets)
            {
                if (offset.stat?.skillNeedFactors != null)
                    foreach (var need in offset.stat.skillNeedFactors) skills.Add(need.skill);
                if (offset.stat?.skillNeedOffsets != null)
                    foreach (var need in offset.stat.skillNeedOffsets) skills.Add(need.skill);
            }
            skills.Remove(null);
        }

        private static void AddScore(Dictionary<SkillDef, int> scores, SkillDef skill, int amount)
        {
            if (skill == null) return;
            scores.TryGetValue(skill, out int old);
            scores[skill] = old + amount;
        }
    }

    public sealed class PawnGrowthState : IExposable
    {
        public int mood;
        public int negativeTime;
        public int cannibalExposure;
        public int prostheticAdaptation;
        public void ExposeData()
        {
            Scribe_Values.Look(ref mood, "mood");
            Scribe_Values.Look(ref negativeTime, "negativeTime");
            Scribe_Values.Look(ref cannibalExposure, "cannibalExposure");
            Scribe_Values.Look(ref prostheticAdaptation, "prostheticAdaptation");
        }
    }

    public sealed class TraitGrowthMapComponent : MapComponent
    {
        private Dictionary<Pawn, PawnGrowthState> states = new Dictionary<Pawn, PawnGrowthState>();
        private List<Pawn> keys;
        private List<PawnGrowthState> values;
        private static readonly HashSet<string> FoodMemories = new HashSet<string>
        {
            "AteHumanlikeMeatAsIngredient", "AteHumanlikeMeatAsIngredientCannibal", "AteHumanlikeMeatDirect"
        };

        public override void ExposeData()
        {
            base.ExposeData();
            if (Scribe.mode == LoadSaveMode.Saving)
                states = states.Where(p => p.Key != null && !p.Key.Destroyed).ToDictionary(p => p.Key, p => p.Value);
            Scribe_Collections.Look(ref states, "pawnGrowth", LookMode.Reference, LookMode.Deep, ref keys, ref values);
            if (Scribe.mode == LoadSaveMode.PostLoadInit && states == null)
                states = new Dictionary<Pawn, PawnGrowthState>();
        }

        public TraitGrowthMapComponent(Map map) : base(map) { }

        private PawnGrowthState StateFor(Pawn pawn)
        {
            if (!states.TryGetValue(pawn, out PawnGrowthState state))
            {
                state = new PawnGrowthState();
                states.Add(pawn, state);
            }
            return state;
        }

        public override void MapComponentTick()
        {
            if (Find.TickManager.TicksGame % 120 != 0) return;
            foreach (Pawn pawn in map.mapPawns.FreeColonistsAndPrisonersSpawned.ToList())
            {
                if (PawnEligibility.PawnIsInvalid(pawn)) continue;
                AdaptToProsthetics(pawn, StateFor(pawn));
                Tick(pawn);
            }
        }

        private void AdaptToProsthetics(Pawn pawn, PawnGrowthState state)
        {
            if (!pawn.story.traits.HasTrait(TraitDefOf.BodyPurist)) return;
            int parts = pawn.health.hediffSet.hediffs.Count(h => h.def.addedPartProps?.betterThanNatural == true);
            if (parts == 0) return;
            state.prostheticAdaptation += parts;
            if (state.prostheticAdaptation <= 1800000) return;
            state.prostheticAdaptation = 0;
            if (!Rand.Chance(1f / 3f)) return;
            Trait purist = pawn.story.traits.GetTrait(TraitDefOf.BodyPurist);
            if (!HSKTraitChanges.CanRemove(purist)) return;
            pawn.story.traits.RemoveTrait(purist);
            bool transhumanist = Rand.Chance(1f / 3f)
                && HSKTraitChanges.Gain(pawn, TraitDefOf.Transhumanist, 0, false, respectLimit: false);
            HSKTraitChanges.Notify(pawn, new[] { TraitDefOf.BodyPurist, TraitDefOf.Transhumanist });
            if (HSKTraitChanges.IsPlayerPawn(pawn))
                Find.LetterStack.ReceiveLetter(
                    (transhumanist ? "HSKTraitGrowth_TranshumanistTitle" : "HSKTraitGrowth_BodyPuristLostTitle").Translate(),
                    (transhumanist ? "HSKTraitGrowth_TranshumanistMessage" : "HSKTraitGrowth_BodyPuristLostMessage").Translate(pawn),
                    LetterDefOf.NegativeEvent, pawn);
        }

        internal void Tick(Pawn pawn)
        {
            if (Find.TickManager.TicksGame % 1200 != 0) return;
            PawnGrowthState state = StateFor(pawn);
            TraitDef hardened = TraitDefOfLocal.Hardened;
            TraitDef naturalMood = TraitDefOfLocal.NaturalMood;
            TraitDef cannibal = TraitDefOfLocal.Cannibal;
            // Repair misplaced Android traits only on biological pawns, as HSK did.
            if (pawn.RaceProps.FleshType == FleshTypeDefOf.Normal)
                foreach (Trait trait in pawn.story.traits.allTraits.ToList())
                    if ((trait.def.defName == "AIPawn_BaseTrait" || trait.def.defName == "AIRobot_BaseTrait")
                        && trait.sourceGene == null)
                    {
                        pawn.story.traits.RemoveTrait(trait);
                        HSKTraitChanges.Notify(pawn, new[] { trait.def });
                    }

            if (!pawn.story.traits.HasTrait(cannibal))
            {
                // This is accumulated exposure to surviving memories, not a count of new meals.
                int memories = pawn.needs.mood.thoughts.memories.Memories.Count(m => FoodMemories.Contains(m.def.defName));
                state.cannibalExposure = Math.Min(2201, state.cannibalExposure + memories);
                if (state.cannibalExposure > 2200 && HSKTraitChanges.Gain(pawn, cannibal, respectLimit: false))
                    state.cannibalExposure = 0;
            }
            if (!TraitGrowthMod.Settings.enableMoodGrowth || pawn.story.traits.HasTrait(hardened)) return;
            MentalBreaker breaker = pawn.mindState.mentalBreaker;
            if (pawn.InAggroMentalState) state.mood -= 5;
            else if (breaker.BreakExtremeIsImminent) state.mood -= 4;
            else if (breaker.BreakMajorIsImminent) state.mood -= 2;
            else if (breaker.BreakMinorIsImminent) state.mood -= 1;
            else if (pawn.needs.mood.CurLevelPercentage > 0.9f) state.mood += 3;
            else if (pawn.needs.mood.CurLevelPercentage > 0.7f) state.mood += 1;
            else state.mood -= Math.Sign(state.mood);
            state.mood = Mathf.Clamp(state.mood, -100000, 100000);
            if (state.mood < -800)
            {
                if (Rand.Chance(0.2f))
                {
                    int degree = pawn.story.traits.GetTrait(naturalMood)?.Degree == -1 ? -2 : -1;
                    // Do not downgrade an already depressive pawn.
                    if (pawn.story.traits.GetTrait(naturalMood)?.Degree != -2)
                        HSKTraitChanges.Gain(pawn, naturalMood, degree, respectLimit: false);
                }
                else state.mood += Rand.RangeInclusive(200, 400);
            }
            if (state.mood < 0) state.negativeTime = Math.Min(7800, state.negativeTime + 1);
            bool depressive = pawn.story.traits.GetTrait(naturalMood)?.Degree == -2;
            if ((depressive && state.negativeTime >= 2600) || state.negativeTime >= 7800)
                Harden(pawn, hardened);
            if (state.mood > 1400)
            {
                if (Rand.Chance(0.2f))
                {
                    if (pawn.story.traits.GetTrait(naturalMood)?.Degree != 2)
                        HSKTraitChanges.Gain(pawn, naturalMood, 1, respectLimit: false);
                }
                else state.mood = 0;
            }
        }

        private static void Harden(Pawn pawn, TraitDef hardened)
        {
            if (hardened == null) return;
            var removals = pawn.story.traits.allTraits.Where(t => t.def.defName == "NaturalMood"
                || hardened.conflictingTraits?.Contains(t.def) == true || t.def.conflictingTraits?.Contains(hardened) == true).ToList();
            if (removals.Any(t => !HSKTraitChanges.CanRemove(t))) return;
            foreach (Trait old in removals) pawn.story.traits.RemoveTrait(old);
            if (!HSKTraitChanges.Gain(pawn, hardened, 0, false, respectLimit: false))
            {
                foreach (Trait old in removals) pawn.story.traits.GainTrait(old);
                HSKTraitChanges.Notify(pawn, removals.Select(t => t.def));
                return;
            }
            HSKTraitChanges.Notify(pawn, removals.Select(t => t.def).Concat(new[] { hardened }));
            int rewards = Math.Max(removals.Count(t => t.def.defName != "NaturalMood"),
                pawn.story.traits.allTraits.Count < HSKTraitChanges.TraitLimit ? 1 : 0);
            for (int i = 0; i < rewards; i++)
            {
                var candidates = new[] { TraitDefOfLocal.Nerves, TraitDefOfLocal.Numb, TraitDefOfLocal.Tough, TraitDefOfLocal.Confident }
                    .Where(d => d != null)
                    .Select(d => new Trait(d, d.defName == "Nerves" ? 2 : 0))
                    .Where(t => HSKTraitChanges.CanGain(pawn, t.def, t.Degree)).ToList();
                if (candidates.Count == 0) break;
                Trait chosen = candidates.RandomElement();
                HSKTraitChanges.Gain(pawn, chosen.def, chosen.Degree, false);
            }
            // Preserve HSK's priority: Leader, otherwise Psychopath, otherwise Villian.
            float roll = Rand.Value;
            foreach (var option in new[] { new { def = TraitDefOfLocal.Leader, chance = 0.30f },
                new { def = TraitDefOf.Psychopath, chance = 0.25f }, new { def = TraitDefOfLocal.Villian, chance = 0.20f } })
            {
                TraitDef def = option.def;
                if (roll <= option.chance && HSKTraitChanges.Gain(pawn, def, 0, false)) break;
            }
            if (HSKTraitChanges.IsPlayerPawn(pawn))
                Find.LetterStack.ReceiveLetter("HSKTraitGrowth_TraitChanged".Translate(),
                    "HSKTraitGrowth_HardenedMessage".Translate(pawn), LetterDefOf.PositiveEvent, pawn);
        }
    }
}
