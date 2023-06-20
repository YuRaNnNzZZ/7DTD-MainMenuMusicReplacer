using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Xml;
using HarmonyLib;
using UnityEngine;
using UnityEngine.Networking;

namespace MainMenuMusicReplacer
{
    public class MainMenuMusicReplacer : IModApi
    {
        public static string fullModDirPath;

        private static bool loaded = false;
        private static readonly List<String> audioClips = new List<string>();

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

                        string fullBundlePath = fullModDirPath + "/" + bundlePath;
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
                                    audioClips.Add(bundlePath + "?" + clipName);
                                }
                            }
                        }
                        else
                        {
                            Log.Warning("[Main Menu Music Replacer] Requested bundle {0} not found!", bundlePath);
                        }

                        continue;
                    }

                    if (xnode.Name == "AudioClip")
                    {
                        XmlNode bundlePathNode = xnode.Attributes.GetNamedItem("BundlePath");
                        string bundlePath = bundlePathNode?.Value?.ToString();

                        string fullBundlePath = fullModDirPath + "/" + bundlePath;
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
                                audioClips.Add(bundlePath + "?" + clipName);
                            }
                        }
                        else
                        {
                            Log.Warning("[Main Menu Music Replacer] Requested bundle {0} not found!", bundlePath);
                        }
                    }
                }
            }

            Log.Out("[Main Menu Music Replacer] Added {0} audio clips", audioClips.Count);
            loaded = audioClips.Count > 0;
        }

        public static AudioClip ChooseRandomClip()
        {
            if (!loaded) return null;

            string fullPath = string.Format("#{0}/{1}", fullModDirPath, audioClips[UnityEngine.Random.Range(0, audioClips.Count)]);

            Log.Out("[Main Menu Music Replacer] Loading " + fullPath);

            AudioClip audioClip = audioClip = DataLoader.LoadAsset<AudioClip>(fullPath);

            if (audioClip != null)
            {
                // Log.Out("[Main Menu Music Replacer] Audio clip loaded");
            }
            else
            {
                Log.Warning("[Main Menu Music Replacer] Audio clip not loaded");
            }

            return audioClip;
        }

        [HarmonyPatch(typeof(BackgroundMusicMono), "Start")]
        public class NMSMusicStart
        {
            public static void Postfix(BackgroundMusicMono __instance, ref AudioSource ___audioSource)
            {
                if (!loaded) return;

                if (___audioSource.clip == GameManager.Instance.BackgroundMusicClip)
                {
                    ___audioSource.Stop();
                    ___audioSource.clip = ChooseRandomClip();
                    ___audioSource.Play();
                }
            }
        }

        [HarmonyPatch(typeof(BackgroundMusicMono), "Update")]
        public class NMSMusicUpdate
        {
            public static void Postfix(BackgroundMusicMono __instance, ref AudioSource ___audioSource)
            {
                if (!loaded) return;

                if (___audioSource.volume <= 0f && ___audioSource.clip != GameManager.Instance.BackgroundMusicClip)
                {
                    Resources.UnloadAsset(___audioSource.clip);
                    ___audioSource.clip = GameManager.Instance.BackgroundMusicClip;

                    return;
                }

                if (___audioSource.volume > 0f && ___audioSource.clip == GameManager.Instance.BackgroundMusicClip)
                {
                    ___audioSource.Stop();
                    ___audioSource.clip = ChooseRandomClip();
                    ___audioSource.Play();
                }
            }
        }
    }
}
