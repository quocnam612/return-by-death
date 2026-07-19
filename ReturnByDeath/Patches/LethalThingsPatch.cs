using HarmonyLib;
using System;
using System.Reflection;
using UnityEngine;

namespace ReturnByDeath.Patches
{
    internal class LethalThingsPatch
    {
        internal static void Apply(Harmony harmony)
        {
            if (!ReturnByDeathBase.EnableRbdSFX.Value) return;

            Type teleporterTrap = AccessTools.TypeByName("LethalThings.MonoBehaviours.TeleporterTrap");
            if (teleporterTrap == null)
                return;

            MethodInfo initializeVariables = AccessTools.Method(teleporterTrap, "__initializeVariables");
            if (initializeVariables != null)
                harmony.Patch(initializeVariables, postfix: new HarmonyMethod(typeof(LethalThingsPatch), nameof(OverrideAudio)));
        }

        static void OverrideAudio(object __instance)
        {
            ApplySound(__instance);
        }

        internal static void ApplyToExisting()
        {
            if (!ReturnByDeathBase.EnableRbdSFX.Value) return;

            Type teleporterTrap = AccessTools.TypeByName("LethalThings.MonoBehaviours.TeleporterTrap");
            if (teleporterTrap == null)
                return;

            foreach (UnityEngine.Object trap in Resources.FindObjectsOfTypeAll(teleporterTrap))
                ApplySound(trap);
        }

        private static void ApplySound(object instance)
        {
            if (!ReturnByDeathBase.EnableRbdSFX.Value) return;

            if (ReturnByDeathBase.SoundFX != null && ReturnByDeathBase.SoundFX.Count > 0 && ReturnByDeathBase.SoundFX[0] != null)
                AccessTools.Field(instance.GetType(), "teleporterBeamUpSFX")?.SetValue(instance, ReturnByDeathBase.SoundFX[0]);
        }
    }
}
