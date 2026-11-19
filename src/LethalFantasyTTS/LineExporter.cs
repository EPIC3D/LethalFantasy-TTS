using System;
using System.IO;
using System.Text;

namespace LethalFantasyTTS
{
    /// <summary>
    /// Dumps the game's English localization table (key + raw text) as JSON lines, so a voice pack
    /// can be generated offline. Raw text keeps the {they}/{them} identity placeholders and rich-text
    /// tags; the generator expands and strips them exactly like the plugin does at runtime.
    /// </summary>
    internal static class LineExporter
    {
        public static string Export(string path)
        {
            var locals = Localization.Instance.GetLocals(LocalizationLanguage.English).locals;
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            using (var w = new StreamWriter(path, false, new UTF8Encoding(false)))
            {
                foreach (var l in locals)
                {
                    if (string.IsNullOrWhiteSpace(l.key) || string.IsNullOrWhiteSpace(l.text)) continue;
                    w.Write("{\"key\":");
                    w.Write(SpeechPlayer.Json(l.key));
                    w.Write(",\"text\":");
                    w.Write(SpeechPlayer.Json(l.text));
                    w.Write("}\n");
                }
            }
            return locals.Length + " entries -> " + path + "; " + ExportEvents(Path.Combine(Path.GetDirectoryName(path), "dialogue_events.jsonl"));
        }

        /// <summary>
        /// Dumps every dialogue line of the loaded DialogueEventScriptableObject assets with its speaker,
        /// type and expression (the emotion shown by the face icon), for emotion-aware generation.
        /// </summary>
        private static string ExportEvents(string path)
        {
            var assets = UnityEngine.Resources.FindObjectsOfTypeAll<DialogueEventScriptableObject>();
            int count = 0;
            using (var w = new StreamWriter(path, false, new UTF8Encoding(false)))
            {
                foreach (var asset in assets)
                {
                    if (asset == null || asset.events == null) continue;
                    for (int e = 0; e < asset.events.Length; e++)
                    {
                        var lines = asset.events[e].lines;
                        if (lines == null) continue;
                        for (int i = 0; i < lines.Length; i++)
                        {
                            var l = lines[i];
                            if (string.IsNullOrWhiteSpace(l.text)) continue;
                            w.Write("{\"asset\":" + SpeechPlayer.Json(asset.name) +
                                ",\"event\":" + e + ",\"line\":" + i +
                                ",\"key\":" + SpeechPlayer.Json(l.text) +
                                ",\"character\":" + SpeechPlayer.Json(l.character.ToString()) +
                                ",\"type\":" + SpeechPlayer.Json(l.type.ToString()) +
                                ",\"expression\":" + SpeechPlayer.Json(l.expression.ToString()) +
                                ",\"preDelay\":" + SpeechPlayer.Json(l.preDelay.ToString()) +
                                ",\"proceedFast\":" + (l.proceedFast ? "true" : "false") + "}\n");
                            count++;
                        }
                    }
                }
            }
            return assets.Length + " dialogue assets, " + count + " lines -> " + path;
        }
    }
}
