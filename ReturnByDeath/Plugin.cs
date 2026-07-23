using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using ReturnByDeath.Patches;
using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace ReturnByDeath
{
    [BepInPlugin(modGUID, modName, modVersion)]
    public class ReturnByDeathBase : BaseUnityPlugin
    {
        private const string modGUID = "quocnam612.ReturnByDeath";
        private const string modName = "Rezero Return By Death SFX";
        private const string modVersion = "2.0.2";
        private readonly Harmony harmony = new Harmony(modGUID);
        public static ReturnByDeathBase Instance;
        internal ManualLogSource mls;
        internal static List<AudioClip> SoundFX;
        
        // Audio Config
        public static ConfigEntry<bool> EnableRbdSFX;
        public static ConfigEntry<bool> EnableWitchSFX;
        public static ConfigEntry<bool> EnableRingSFX;

        // Gameplay Config
        public static ConfigEntry<bool> EnableReturnByDeath;
        public static ConfigEntry<string> ManualSaveKey;

        void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
            }

            mls = BepInEx.Logging.Logger.CreateLogSource(modGUID);
            mls.LogInfo($"[{modName}] v{modVersion} is loaded!");

            EnableRbdSFX = Config.Bind(
                "Audio Toggles", 
                "EnableReturnByDeathSFX", 
                true, 
                "Toggle whether the Return By Death SFX (when player teleport)  is enabled."
            );

            EnableWitchSFX = Config.Bind(
                "Audio Toggles", 
                "EnableWitchSFX", 
                true, 
                "Toggle whether the Witch SFX (when a dead body is seen and intense fear reached) is enabled."
            );

            EnableRingSFX = Config.Bind(
                "Audio Toggles", 
                "EnableRingSFX", 
                true, 
                "Toggle whether the Truck delivery music (Subaru phone ring) is enabled."
            );

            EnableReturnByDeath = Config.Bind(
                "Gameplay",
                "EnableReturnByDeath",
                false,
                "Saves the local player's state every minute and restores it when otherwise-lethal damage is taken. Intended for single-player or consented private lobbies."
            );

            ManualSaveKey = Config.Bind(
            "Debug", 
            "ManualSaveKey", 
            "F5", 
            "Binding save checkpoint");

            SoundFX = new List<AudioClip> { null, null, null, null, null };
            LoadSounds();

            // Audio
            harmony.PatchAll(typeof(ReturnByDeathBase));
            harmony.PatchAll(typeof(SoundManagerPatch));
            harmony.PatchAll(typeof(DeadBodyInfoPatch));
            harmony.PatchAll(typeof(ItemDropshipAudioPatch));
            harmony.PatchAll(typeof(ShipTeleporterPatch));

            // Gameplay
            harmony.PatchAll(typeof(PlayerControllerPatch));
            harmony.PatchAll(typeof(LandminePatch));
            harmony.PatchAll(typeof(HoarderBugPatch));
            harmony.PatchAll(typeof(GrabbableObjectPatch));
            harmony.PatchAll(typeof(EnemyAIPatch));


            LethalThingsPatch.Apply(harmony);
            mls = Logger;
        }

        private void LoadSounds()
        {
            string soundsFolder = Path.Combine(Path.GetDirectoryName(Info.Location), "sounds");
            string[] soundFiles = { "rbd.wav", "witch1.wav", "witch2.wav", "ring.wav", "ringfar.wav" };

            for (int i = 0; i < soundFiles.Length; i++)
            {
                string soundFile = soundFiles[i];
                string soundPath = Path.Combine(soundsFolder, soundFile);
                if (!File.Exists(soundPath))
                {
                    mls.LogError($"Missing sound file: {soundPath}");
                    continue;
                }

                try
                {
                    AudioClip clip = LoadWav(soundPath);
                    SoundFX[i] = clip;
                    mls.LogInfo($"Loaded {soundFile} as SoundFX[{i}]");
                }
                catch (Exception exception)
                {
                    mls.LogError($"Failed to load {soundFile}: {exception.Message}");
                }
            }

            ShipTeleporterPatch.ApplyToExisting();
            LethalThingsPatch.ApplyToExisting();
        }

        private static AudioClip LoadWav(string path)
        {
            using (BinaryReader reader = new BinaryReader(File.OpenRead(path)))
            {
                if (new string(reader.ReadChars(4)) != "RIFF")
                    throw new InvalidDataException("Not a WAV file.");

                reader.ReadInt32();
                if (new string(reader.ReadChars(4)) != "WAVE")
                    throw new InvalidDataException("Not a WAV file.");

                short format = 0;
                short channels = 0;
                int sampleRate = 0;
                short bitsPerSample = 0;
                byte[] audioData = null;

                while (reader.BaseStream.Position < reader.BaseStream.Length)
                {
                    string chunkId = new string(reader.ReadChars(4));
                    int chunkSize = reader.ReadInt32();

                    if (chunkId == "fmt ")
                    {
                        format = reader.ReadInt16();
                        channels = reader.ReadInt16();
                        sampleRate = reader.ReadInt32();
                        reader.ReadInt32();
                        reader.ReadInt16();
                        bitsPerSample = reader.ReadInt16();
                        reader.BaseStream.Position += chunkSize - 16;
                    }
                    else if (chunkId == "data")
                    {
                        audioData = reader.ReadBytes(chunkSize);
                    }
                    else
                    {
                        reader.BaseStream.Position += chunkSize;
                    }

                    if (chunkSize % 2 != 0)
                        reader.BaseStream.Position++;
                }

                if (format != 1 || bitsPerSample != 16 || channels <= 0 || audioData == null)
                    throw new InvalidDataException("Only 16-bit PCM WAV files are supported.");

                float[] samples = new float[audioData.Length / 2];
                for (int i = 0; i < samples.Length; i++)
                    samples[i] = BitConverter.ToInt16(audioData, i * 2) / 32768f;

                AudioClip clip = AudioClip.Create(Path.GetFileNameWithoutExtension(path), samples.Length / channels, channels, sampleRate, false);
                clip.SetData(samples, 0);
                return clip;
            }
        }
    }
}
