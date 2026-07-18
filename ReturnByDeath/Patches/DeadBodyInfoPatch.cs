using HarmonyLib;

namespace ReturnByDeath.Patches
{
    [HarmonyPatch(typeof(DeadBodyInfo), "DetectIfSeenByLocalPlayer")]
    internal class DeadBodyInfoPatch
    {
        [HarmonyPrefix]
        static void Before(DeadBodyInfo __instance, ref bool __state)
        {
            __state = __instance.seenByLocalPlayer;
        }

        [HarmonyPostfix]
        static void After(DeadBodyInfo __instance, bool __state)
        {
            if (!__state && __instance.seenByLocalPlayer)
                SoundManagerPatch.TriggerWitch2();
        }
    }
}
