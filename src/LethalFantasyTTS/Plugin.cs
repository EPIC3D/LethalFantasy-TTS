using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace LethalFantasyTTS
{
    [BepInPlugin(Guid, "Epicopy + Copilot LethalFantasy TTS", "0.4.0")]
    public class Plugin : BaseUnityPlugin
    {
        public const string Guid = "com.epicopy.copilot.lethalfantasytts";

        internal static ManualLogSource Log;
        internal static Settings Cfg;
        private Harmony _harmony;

        private void Awake()
        {
            Log = Logger;
            Cfg = new Settings(Config);

            SceneManager.activeSceneChanged += (_, __) =>
            {
                if (SpeechPlayer.Instance != null) SpeechPlayer.Instance.Stop();
            };

            _harmony = new Harmony(Guid);
            _harmony.PatchAll(typeof(Plugin).Assembly);
            Log.LogInfo("LethalFantasy TTS loaded; server " + Cfg.ServerUrl.Value);
        }

        private static bool _exported;

        /// <summary>Runs once, from the first dialogue line (the plugin's own Start() never runs in this game).</summary>
        internal static void ExportOnce()
        {
            if (_exported || !Cfg.ExportLines.Value) return;
            _exported = true;
            try
            {
                string path = System.IO.Path.Combine(Paths.BepInExRootPath, "LethalFantasyTTS", "english_lines.jsonl");
                Log.LogInfo("[TTS] exported " + LineExporter.Export(path));
            }
            catch (System.Exception e)
            {
                Log.LogError("[TTS] export failed: " + e);
            }
        }
    }

    // DialogueManager.SetupNextString runs once per displayed line and leaves the
    // final localized text in nextText and the speaker in nextCharacter.
    [HarmonyPatch(typeof(DialogueManager), "SetupNextString")]
    internal static class DialogueCapture
    {
        private static readonly Regex RichTextTags = new Regex("<[^>]+>", RegexOptions.Compiled);

        [HarmonyPostfix]
        private static void Postfix(DialogueManager __instance)
        {
            // Never let a plugin error break the game's own dialogue.
            Plugin.ExportOnce();
            try { Capture(__instance); }
            catch (System.Exception e) { Plugin.Log.LogError("[TTS] capture failed: " + e); }
        }

        private static void Capture(DialogueManager __instance)
        {
            if (__instance == null || string.IsNullOrWhiteSpace(__instance.nextText))
                return;

            string text = RichTextTags.Replace(__instance.nextText, string.Empty).Trim();
            DialogueType type = __instance.lastExpression.type;
            CharacterType character = __instance.nextCharacter;
            Plugin.Log.LogInfo("[Dialogue] " + character + " (" + type + ", " +
                Localization.SelectedLanguage + "): " + text);

            if (text.Length > 0)
                SpeechPlayer.Ensure().Speak(character, type, text, __instance.lastExpression.text);

            // Synthesize the queued next lines now so they are ready when shown.
            // (Same text pipeline as SetupNextString, minus the rare affection-title format.)
            if (__instance.textSequence != null)
            {
                foreach (var next in __instance.textSequence)
                {
                    string t = Localization.AddIdentity(Localization.GetString(next.text));
                    SpeechPlayer.Prefetch(next.character, next.type,
                        RichTextTags.Replace(t ?? string.Empty, string.Empty).Trim(), next.text);
                }
            }
        }
    }

    // Diagnostics only: the game may call this mid-sequence, so it does not stop speech (yet).
    [HarmonyPatch(typeof(DialogueManager), nameof(DialogueManager.InterruptCurrentDialogue), new System.Type[0])]
    internal static class DialogueInterrupt
    {
        [HarmonyPostfix]
        private static void Postfix()
        {
            if (Plugin.Cfg.DebugLog.Value) Plugin.Log.LogInfo("[TTS] InterruptCurrentDialogue");
        }
    }
}
