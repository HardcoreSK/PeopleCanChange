using Verse.AI;
using Verse;
using Harmony;
using System.Reflection;

namespace DetectPyroBreak
{

    [StaticConstructorOnStartup]
    internal static class PCC_Init
    {
        static PCC_Init()
        {
            HarmonyInstance harmony = HarmonyInstance.Create("com.github.rimworld.mod.PyroDetect");
            harmony.PatchAll(Assembly.GetExecutingAssembly());
        }
    }

    [HarmonyPatch(typeof(MentalState_FireStartingSpree), "PostStart")]
    public static class PyroDetect_Patch
    {
        [HarmonyPostfix]
        static void Postfix(MentalState_FireStartingSpree __instance, string reason)
        {
            Log.Warning(__instance.pawn.Name + " has pyro break");
        }
    }

}