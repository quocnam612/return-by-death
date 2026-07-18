using HarmonyLib;
using System;
using System.Reflection;

namespace ReturnByDeath.Patches
{
    internal class LethalThingsPatch
    {
        internal static void Apply(Harmony harmony)
        {
            Type teleporterTrap = AccessTools.TypeByName("LethalThings.MonoBehaviours.TeleporterTrap");
            if (teleporterTrap == null)
                return;

            MethodInfo initializeVariables = AccessTools.Method(teleporterTrap, "__initializeVariables");
            if (initializeVariables != null)
                harmony.Patch(initializeVariables, postfix: new HarmonyMethod(typeof(LethalThingsPatch), nameof(OverrideAudio)));
        }

        static void OverrideAudio(object __instance)
        {
            AccessTools.Field(__instance.GetType(), "teleporterBeamUpSFX")?.SetValue(__instance, ReturnByDeathBase.SoundFX[0]);
        }
    }
}
