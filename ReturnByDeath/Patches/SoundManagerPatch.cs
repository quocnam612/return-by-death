using HarmonyLib;
using UnityEngine;

namespace ReturnByDeath.Patches
{
    [HarmonyPatch(typeof(SoundManager))]
    internal class SoundManagerPatch
    {
        private static AudioSource witchAudioSource;
        private static bool isFadingOut = false;
        private static int currentWitchTrack = -1;

        public static bool hasJustDiscoveredBody = false;

        [HarmonyPatch("Start")]
        [HarmonyPostfix]
        static void CreateCustomWitchAudioSource(SoundManager __instance)
        {
            witchAudioSource = __instance.gameObject.AddComponent<AudioSource>();
            witchAudioSource.playOnAwake = false;
            witchAudioSource.loop = true;
            witchAudioSource.volume = 0f;
        }

        public static void TriggerWitch2()
        {
            hasJustDiscoveredBody = true;
        }

        [HarmonyPatch("SetFearAudio")]
        [HarmonyPostfix]
        static void UpdateWitchFearAudio(SoundManager __instance)
        {
            if (witchAudioSource == null) return;

            if (GameNetworkManager.Instance.localPlayerController.isPlayerDead)
            {
                witchAudioSource.volume = 0f;
                witchAudioSource.Stop();
                isFadingOut = false;
                currentWitchTrack = -1;
                hasJustDiscoveredBody = false;
                return;
            }

            float fearLevel = StartOfRound.Instance.fearLevel;

            int targetTrack = -1;

            if (hasJustDiscoveredBody)
            {
                targetTrack = 2;
            }
            else if (fearLevel > 0.4f)
            {
                targetTrack = 1;
            }

            if (targetTrack != -1)
            {
                isFadingOut = false;

                if (currentWitchTrack != targetTrack || !witchAudioSource.isPlaying)
                {
                    currentWitchTrack = targetTrack;

                    if (ReturnByDeathBase.SoundFX != null && ReturnByDeathBase.SoundFX.Count > targetTrack)
                    {
                        witchAudioSource.clip = ReturnByDeathBase.SoundFX[targetTrack];
                        witchAudioSource.time = 0f;
                        witchAudioSource.Play();
                    }
                }

                float targetVolume = (targetTrack == 2) ? 1f : Mathf.Clamp(fearLevel - 0.2f, 0.1f, 1f);
                witchAudioSource.volume = targetVolume;
            }
            else
            {
                if (witchAudioSource.isPlaying)
                {
                    isFadingOut = true;
                    witchAudioSource.volume = Mathf.Lerp(witchAudioSource.volume, 0f, 2f * Time.deltaTime);

                    if (witchAudioSource.volume < 0.01f)
                    {
                        witchAudioSource.Stop();
                        witchAudioSource.volume = 0f;
                        isFadingOut = false;
                        currentWitchTrack = -1;
                        hasJustDiscoveredBody = false;
                    }
                }
            }
        }
    }
}