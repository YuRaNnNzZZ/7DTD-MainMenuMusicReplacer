using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Xml;
using HarmonyLib;
using UnityEngine;

namespace MainMenuMusicReplacer
{
    public class MainMenuMusicReplacer : IModApi
    {
        public static string fullModDirPath;

        private static bool loaded = false;
        private static readonly List<IAudioEntry> audioClips = new List<IAudioEntry>();

        public void InitMod(Mod _modInstance)
        {
            var harmony = new Harmony(GetType().ToString());
            harmony.PatchAll(Assembly.GetExecutingAssembly());

            fullModDirPath = _modInstance.Path;
            LoadClips();
        }

        private void LoadClips() {
            if (fullModDirPath == null) return;

            string configFilePath = fullModDirPath + "/MusicInfo.xml";

            if (!File.Exists(configFilePath)) {
                Log.Error("[Main Menu Music Replacer] No MusicInfo.xml config file found in mod directory!");

                return;
            }

            XmlDocument xDoc = new XmlDocument();
            xDoc.Load(configFilePath);

            Dictionary<string, bool> visitedFiles = new Dictionary<string, bool>();

            XmlElement xRoot = xDoc.DocumentElement;
            if (xRoot != null)
            {
                foreach (XmlNode xnode in xRoot)
                {
                    if (xnode.Name == "Bundle")
                    {
                        XmlNode bundlePathNode = xnode.Attributes.GetNamedItem("Path");
                        string bundlePath = bundlePathNode?.Value?.ToString();

                        string fullBundlePath = Path.Combine(fullModDirPath, bundlePath);
                        if (!visitedFiles.ContainsKey(fullBundlePath))
                        {
                            bool exists = File.Exists(fullBundlePath);
                            visitedFiles.Add(fullBundlePath, exists);

                            if (exists)
                            {
                                DataLoader.PreloadBundle(fullBundlePath);
                            }
                        }

                        if (visitedFiles.GetValueSafe(fullBundlePath))
                        {
                            foreach (XmlNode cnode in xnode)
                            {
                                if (cnode.Name != "AudioClip") continue;

                                XmlNode clipNameNode = cnode.Attributes.GetNamedItem("Name");
                                string clipName = clipNameNode?.Value?.ToString();

                                if (clipName != null)
                                {
                                    audioClips.Add(new BundleAudioEntry(bundlePath, clipName));
                                }
                            }
                        }
                        else
                        {
                            Log.Warning("[Main Menu Music Replacer] Requested bundle '{0}' not found!", bundlePath);
                        }

                        continue;
                    }

                    if (xnode.Name == "AudioClip")
                    {
                        XmlNode bundlePathNode = xnode.Attributes.GetNamedItem("BundlePath");
                        string bundlePath = bundlePathNode?.Value?.ToString();

                        string fullBundlePath = Path.Combine(fullModDirPath, bundlePath);
                        if (!visitedFiles.ContainsKey(fullBundlePath))
                        {
                            bool exists = File.Exists(fullBundlePath);
                            visitedFiles.Add(fullBundlePath, exists);

                            if (exists)
                            {
                                DataLoader.PreloadBundle(fullBundlePath);
                            }
                        }

                        if (visitedFiles.GetValueSafe(fullBundlePath))
                        {

                            XmlNode clipNameNode = xnode.Attributes.GetNamedItem("Name");
                            string clipName = clipNameNode?.Value?.ToString();

                            if (clipName != null)
                            {
                                audioClips.Add(new BundleAudioEntry(bundlePath, clipName));
                            }
                        }
                        else
                        {
                            Log.Warning("[Main Menu Music Replacer] Requested bundle '{0}' not found!", bundlePath);
                        }
                    }

                    if (xnode.Name == "File")
                    {
                        XmlNode filePathNode = xnode.Attributes.GetNamedItem("Path");
                        string filePath = filePathNode?.Value?.ToString();

                        string fullFilePath = Path.Combine(fullModDirPath, filePath);
                        if (!visitedFiles.ContainsKey(fullFilePath))
                        {
                            bool exists = File.Exists(fullFilePath);
                            visitedFiles.Add(fullFilePath, exists);
                        }

                        if (visitedFiles.GetValueSafe(fullFilePath))
                        {
                            audioClips.Add(new FileAudioEntry(filePath));
                        }
                        else
                        {
                            Log.Warning("[Main Menu Music Replacer] Requested file '{0}' not found!", filePath);
                        }
                    }
                }
            }

            Log.Out("[Main Menu Music Replacer] Added {0} audio clips", audioClips.Count);
            loaded = audioClips.Count > 0;
        }

        public static AudioClip ChooseRandomClip()
        {
            if (!loaded || audioClips.Count == 0) return null;

            IAudioEntry entry = audioClips[UnityEngine.Random.Range(0, audioClips.Count)];

            AudioClip audioClip = entry.TryLoadClip(fullModDirPath);
            if (audioClip == null)
            {
                audioClips.Remove(entry);

                return ChooseRandomClip();
            }

            return audioClip;
        }

        [HarmonyPatch(typeof(BackgroundMusicMono), "Play")]
        public class NMSMusicReplace
        {
            public static void Prefix(BackgroundMusicMono __instance, BackgroundMusicMono.MusicTrack musicTrack)
            {
                if (__instance.currentlyPlaying == musicTrack) return;

                if (musicTrack == BackgroundMusicMono.MusicTrack.BackgroundMusic)
                {
                    BackgroundMusicMono.MusicTrackState musicTrackState = __instance.musicTrackStates[musicTrack];

                    if (musicTrackState != null && musicTrackState.AudioSource != null)
                    {
                        AudioClip clip = ChooseRandomClip();
                        if (clip == null) return;

                        Resources.UnloadAsset(__instance.musicTrackStates[musicTrack].AudioSource.clip);

                        AudioSource audioSource = __instance.gameObject.AddComponent<AudioSource>();
                        audioSource.volume = 0f;
                        audioSource.clip = clip;
                        audioSource.loop = true;

                        __instance.musicTrackStates[musicTrack] = new BackgroundMusicMono.MusicTrackState(audioSource);
                    }
                }
            }
        }
    }
}
