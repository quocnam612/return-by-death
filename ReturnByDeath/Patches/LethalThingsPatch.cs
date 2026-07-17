using HarmonyLib;
using LethalThings.MonoBehaviours;
using UnityEngine;

namespace ReturnByDeath.Patches
{
    [HarmonyPatch(typeof(TeleporterTrap))]
    internal class LethalThingsPatch
    {
        [HarmonyPatch("__initializeVariables")]
        [HarmonyPostfix]
        static void OverrideAudio(TeleporterTrap __instance)
        {
            __instance.teleporterBeamUpSFX = ReturnByDeathBase.SoundFX[0];
        }
    }
}