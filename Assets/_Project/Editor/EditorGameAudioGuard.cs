using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace WorldBuilder.Editor
{
    [InitializeOnLoad]
    internal static class EditorGameAudioGuard
    {
        static EditorGameAudioGuard()
        {
            EditorApplication.delayCall += EnsureGameAudioEnabled;
        }

        private static void EnsureGameAudioEnabled()
        {
            Type audioUtility =
                typeof(AudioImporter).Assembly.GetType(
                    "UnityEditor.AudioUtil");
            MethodInfo getMasterMute = audioUtility?.GetMethod(
                "GetMasterMute",
                BindingFlags.Static |
                BindingFlags.Public |
                BindingFlags.NonPublic);
            MethodInfo setMasterMute = audioUtility?.GetMethod(
                "SetMasterMute",
                BindingFlags.Static |
                BindingFlags.Public |
                BindingFlags.NonPublic);
            if (getMasterMute == null || setMasterMute == null)
            {
                return;
            }

            bool muted = (bool)getMasterMute.Invoke(null, null);
            if (!muted)
            {
                return;
            }

            setMasterMute.Invoke(null, new object[] { false });
            Debug.Log(
                "WorldBuilder restored Unity Game-view audio playback.");
        }
    }
}
