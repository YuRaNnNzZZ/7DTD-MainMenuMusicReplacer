using System.IO;
using UnityEngine;
using UnityEngine.Networking;

namespace MainMenuMusicReplacer
{
    abstract class IAudioEntry
    {
        public abstract AudioClip TryLoadClip(string modDirPath);
    }

    class BundleAudioEntry : IAudioEntry
    {
        private string bundlePath;
        private string name;

        public BundleAudioEntry(string bundlePath, string name)
        {
            this.bundlePath = bundlePath;
            this.name = name;
        }

        public override AudioClip TryLoadClip(string modDirPath)
        {
            string fullFilePath = Path.Combine(modDirPath, bundlePath);

            Log.Out("[Main Menu Music Replacer] Loading '{0}' from bundle '{1}'", name, bundlePath);

            if (!File.Exists(fullFilePath))
            {
                Log.Warning("[Main Menu Music Replacer] Bundle '{0}' doesn't exist!", bundlePath);

                return null;
            }

            AudioClip audioClip = DataLoader.LoadAsset<AudioClip>("#" + fullFilePath + "?" + name);

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
    }

    class FileAudioEntry : IAudioEntry
    {
        private string filePath;

        public FileAudioEntry(string filePath)
        {
            this.filePath = filePath;
        }

        public override AudioClip TryLoadClip(string modDirPath)
        {
            string fullFilePath = Path.Combine(modDirPath, filePath);

            if (!File.Exists(fullFilePath))
            {
                Log.Warning("[Main Menu Music Replacer] Audio file '{0}' doesn't exist!", filePath);

                return null;
            }

            UnityWebRequest www = UnityWebRequestMultimedia.GetAudioClip("file:///" + fullFilePath.Replace("\\", "/"), AudioType.UNKNOWN);

            Log.Out("[Main Menu Music Replacer] Loading '{0}'", filePath);
            www.SendWebRequest();
            while (!www.isDone) { }

            if (www.result != UnityWebRequest.Result.Success)
            {
                Log.Error("[Main Menu Music Replacer] Failed to load audio clip: {0}", www.error);
            }
            else
            {
                return DownloadHandlerAudioClip.GetContent(www);
            }

            return null;
        }
    }
}
