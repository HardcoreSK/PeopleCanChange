using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace HSK.TraitGrowth
{
    public class IncidentWorker_GainTrait : IncidentWorker
    {
        protected override bool TryExecuteWorker(IncidentParms parms)
        {
            Pawn pawn = PawnEligibility.GetRandPawn(parms.target);
            if (pawn == null || pawn.story.traits.allTraits.Count >= HSKTraitChanges.TraitLimit) return false;
            var candidates = DefDatabase<TraitDef>.AllDefsListForReading
                .Where(d => d != TraitDefOf.DislikesWomen && HSKTraitChanges.RandomTraitAllowed(pawn, d))
                .Select(d => d == TraitDefOf.DislikesMen && pawn.gender == Gender.Male ? TraitDefOf.DislikesWomen : d)
                .Where(d => d.degreeDatas.Any(degree => HSKTraitChanges.CanGain(pawn, d, degree.degree))).ToList();
            if (candidates.Count == 0) return false;
            TraitDef chosen = candidates.RandomElement();
            int degreeToGain = chosen.degreeDatas.Where(d => HSKTraitChanges.CanGain(pawn, chosen, d.degree)).RandomElement().degree;
            return HSKTraitChanges.Gain(pawn, chosen, degreeToGain);
        }
    }

    public class IncidentWorker_LoseTrait : IncidentWorker
    {
        protected override bool TryExecuteWorker(IncidentParms parms)
        {
            Pawn pawn = PawnEligibility.GetRandPawn(parms.target);
            if (pawn == null) return false;
            // HSK samples once; selecting a protected trait consumes the opportunity.
            Trait chosen = pawn.story.traits.allTraits.RandomElementWithFallback();
            if (!HSKTraitChanges.CanRemove(chosen) || !HSKTraitChanges.RandomTraitAllowed(pawn, chosen.def)) return false;
            string label = chosen.Label;
            pawn.story.traits.RemoveTrait(chosen);
            HSKTraitChanges.Notify(pawn, new[] { chosen.def });
            Find.LetterStack.ReceiveLetter("HSKTraitGrowth_TraitChanged".Translate(),
                "HSKTraitGrowth_LostTrait".Translate(pawn, label), LetterDefOf.NeutralEvent, pawn);
            return true;
        }
    }

    internal static class PawnEligibility
    {
        internal static Pawn GetRandPawn(IIncidentTarget target)
        {
            if (!(target is Map map)) return null;
            return map.mapPawns.FreeColonistsAndPrisonersSpawned
                .Where(p => p.IsColonist && !p.NonHumanlikeOrWildMan() && !PawnIsInvalid(p))
                .RandomElementWithFallback();
        }

        internal static bool PawnIsInvalid(Pawn pawn)
        {
            if (pawn == null || pawn.Dead || pawn.story?.traits == null || pawn.needs?.mood == null
                || pawn.DevelopmentalStage == DevelopmentalStage.Newborn || HSKTraitChanges.IsMechanical(pawn)
                || (pawn.mutant != null && pawn.mutant.HasTurned)) return true;
            return pawn.health.hediffSet.HasHediff(SK.HediffDefOfLocal.Coma);
        }
    }
}
