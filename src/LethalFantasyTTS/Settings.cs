using BepInEx.Configuration;
using System.Collections.Generic;

namespace LethalFantasyTTS
{
    internal sealed class VoiceSettings
    {
        public ConfigEntry<string> Voice;
        public ConfigEntry<string> Lang;
        public ConfigEntry<float> Speed;
        public ConfigEntry<float> Pitch;
        public ConfigEntry<float> FilePitch;
    }

    internal sealed class Settings
    {
        public readonly ConfigEntry<bool> Enabled;
        public readonly ConfigEntry<string> ServerUrl;
        public readonly ConfigEntry<float> Volume;

        public readonly ConfigEntry<bool> ReadNormal;
        public readonly ConfigEntry<bool> ReadFollowup;
        public readonly ConfigEntry<bool> ReadStory;

        public readonly ConfigEntry<float> SpatialBlend;
        public readonly ConfigEntry<float> MinDistance;
        public readonly ConfigEntry<float> MaxDistance;
        public readonly ConfigEntry<bool> LowPassByDistance;
        public readonly ConfigEntry<float> LowPassFarCutoff;
        public readonly ConfigEntry<bool> DebugLog;
        public readonly ConfigEntry<bool> ExportLines;
        public readonly ConfigEntry<bool> UseVoicePack;
        public readonly ConfigEntry<string> VoicePackDir;
        public readonly ConfigEntry<bool> UseServer;
        public readonly ConfigEntry<bool> SoundDelay;
        public readonly ConfigEntry<float> SoundDelayMax;
        public readonly ConfigEntry<bool> Deepen;
        public readonly ConfigEntry<float> DeepenPerTenfold;
        public readonly ConfigEntry<float> DeepenMinPitch;
        public readonly ConfigEntry<bool> Reverb;
        public readonly ConfigEntry<float> ReverbFullAtTenfold;
        public readonly ConfigEntry<float> EchoDelayPerTenfold;
        public readonly ConfigEntry<float> EchoWet;
        public readonly ConfigEntry<bool> IndoorMuffle;
        public readonly ConfigEntry<float> IndoorCutoff;
        public readonly ConfigEntry<float> IndoorStrength;
        public readonly ConfigEntry<float> ReverbAmount;
        public readonly ConfigEntry<string> DeepenMode;
        public readonly ConfigEntry<float> DeepenStrength;
        public readonly ConfigEntry<string> OverlapMode;
        public bool DeepenPhysical => string.Equals(DeepenMode.Value, "Physical", System.StringComparison.OrdinalIgnoreCase);
        public bool OverlapQueue => !string.Equals(OverlapMode.Value, "Cut", System.StringComparison.OrdinalIgnoreCase);
        public readonly ConfigEntry<bool> Atmosphere;
        public readonly ConfigEntry<float> AtmosphereLossDb;
        public readonly ConfigEntry<bool> Telepathy;
        public readonly ConfigEntry<float> TelepathyAboveHeight;
        public readonly ConfigEntry<float> TelepathyFade;
        public readonly ConfigEntry<float> TelepathyEthereal;
        public readonly ConfigEntry<bool> MouthSync;
        public readonly ConfigEntry<bool> ReadMemoryOrb;
        public readonly ConfigEntry<bool> TextSyncToVoice;
        public readonly ConfigEntry<float> MouthRate;

        // Only English voices for now; other languages are skipped.
        private readonly Dictionary<string, VoiceSettings> _englishVoices = new Dictionary<string, VoiceSettings>();

        public Settings(ConfigFile cfg)
        {
            Enabled = cfg.Bind("General", "Enabled", true, "Read dialogue aloud.");
            ServerUrl = cfg.Bind("General", "ServerUrl", "http://127.0.0.1:5123",
                "Address of the local TTS server (server/tts_server.py).");
            Volume = cfg.Bind("General", "Volume", 1.0f, new ConfigDescription("Speech volume.",
                new AcceptableValueRange<float>(0f, 1f)));
            DebugLog = cfg.Bind("General", "DebugLog", true, "Log timing and distance of every spoken line.");
            UseVoicePack = cfg.Bind("General", "UseVoicePack", true,
                "Play pre-generated voice files (voices/<lang>/<id>.ogg) when available.");
            VoicePackDir = cfg.Bind("General", "VoicePackDir", "",
                "Voice pack folder; empty = 'voices' next to the plugin DLL.");
            UseServer = cfg.Bind("General", "UseServer", false,
                "Ask the optional live Kokoro TTS server (server/tts_server.py) for lines missing from the voice pack.");
            ExportLines = cfg.Bind("Developer", "ExportLines", false,
                "On game start, write all English lines to BepInEx/LethalFantasyTTS/english_lines.jsonl (for voice pack generation).");

            ReadNormal = cfg.Bind("Lines", "ReadNormal", true, "Read normal dialogue lines.");
            ReadFollowup = cfg.Bind("Lines", "ReadFollowup", true, "Read follow-up replies of the second character.");
            ReadStory = cfg.Bind("Lines", "ReadStory", false, "Read story narration (StoryPoise lines). Story lines spoken by a character are always voiced.");

            SpatialBlend = cfg.Bind("Spatial", "SpatialBlend", 1.0f, new ConfigDescription(
                "0 = plain stereo, 1 = fully 3D from the speaker's head.", new AcceptableValueRange<float>(0f, 1f)));
            MinDistance = cfg.Bind("Spatial", "MinDistance", 15f,
                "Full volume within this distance (Unity units), multiplied by the giantess' scale.");
            MaxDistance = cfg.Bind("Spatial", "MaxDistance", 300f,
                "Volume fades out linearly until this distance, multiplied by the giantess' scale.");
            LowPassByDistance = cfg.Bind("Spatial", "LowPassByDistance", true, "Muffle the voice as the speaker gets farther away.");
            LowPassFarCutoff = cfg.Bind("Spatial", "LowPassFarCutoff", 3000f, "Low-pass cutoff (Hz) at MaxDistance.");

            SoundDelay = cfg.Bind("Effects", "SoundDelay", false, "Delay the voice by distance / speed of sound (343 units/s).");
            SoundDelayMax = cfg.Bind("Effects", "SoundDelayMax", 3f, "Maximum speed-of-sound delay in seconds (0 = no limit).");
            Deepen = cfg.Bind("Effects", "Deepen", true, "Lower the voice as the giantess grows (also slows it slightly).");
            DeepenPerTenfold = cfg.Bind("Effects", "DeepenPerTenfold", 0.08f, "Pitch drop per 10x growth (0.08 = 8%).");
            DeepenMinPitch = cfg.Bind("Effects", "DeepenMinPitch", 0.1f, "Lowest pitch the deepening may reach (0.1 = practically no limit).");
            Reverb = cfg.Bind("Effects", "Reverb", true, "Open-air reverb and echo that grow with the giantess.");
            ReverbFullAtTenfold = cfg.Bind("Effects", "ReverbFullAtTenfold", 3f,
                "Growth (in 10x steps) at which the echo is strongest; 3 = 1000x scale.");
            EchoDelayPerTenfold = cfg.Bind("Effects", "EchoDelayPerTenfold", 150f,
                "Echo (reflection from far terrain) delay in ms per 10x growth.");
            EchoWet = cfg.Bind("Effects", "EchoWet", 0.35f, "Echo loudness at full strength (0..1).");
            IndoorMuffle = cfg.Bind("Effects", "IndoorMuffle", true,
                "Muffle the voice while the player is under a roof (the characters are outside).");
            IndoorCutoff = cfg.Bind("Effects", "IndoorCutoff", 2500f, "Low-pass cutoff (Hz) under a roof at IndoorStrength 100 (the game itself uses 1000).");

            IndoorStrength = cfg.Bind("Effects", "IndoorStrength", 40f, new ConfigDescription(
                "How much of the indoor muffle is applied, in % (huts are small, doors and windows often open). " +
                "100 = full IndoorCutoff.", new AcceptableValueRange<float>(0f, 100f)));
            ReverbAmount = cfg.Bind("Effects", "ReverbAmount", 1f, new ConfigDescription(
                "Open-air reverb and echo amount: 0 = off, 1 = default, 2 = double.",
                new AcceptableValueRange<float>(0f, 2f)));
            DeepenMode = cfg.Bind("Effects", "DeepenMode", "Gentle", new ConfigDescription(
                "Physical = pitch falls with size like a real body (pitch ~ 1/size, scaled by DeepenStrength). " +
                "Gentle = DeepenPerTenfold per 10x growth. Unity pitch also slows the tempo; see DeepenMinPitch.",
                new AcceptableValueList<string>("Physical", "Gentle")));
            DeepenStrength = cfg.Bind("Effects", "DeepenStrength", 100f, new ConfigDescription(
                "Deepening strength in % (100 = fully physical / full DeepenPerTenfold, 0 = none).",
                new AcceptableValueRange<float>(0f, 100f)));
            OverlapMode = cfg.Bind("Lines", "OverlapMode", "Queue", new ConfigDescription(
                "When a new line arrives while one is still spoken: Queue = finish it, then say the newest line " +
                "(skipped lines in between are dropped); Cut = stop and start the new line at once.",
                new AcceptableValueList<string>("Queue", "Cut")));
            Atmosphere = cfg.Bind("Effects", "Atmosphere", true,
                "Air absorbs high frequencies over real distance (about 5 dB/km at 1 kHz, much more above): " +
                "far voices turn into a deep, muffled rumble.");
            AtmosphereLossDb = cfg.Bind("Effects", "AtmosphereLossDb", 20f,
                "Absorption (dB) at which a frequency counts as gone; lower = muffles sooner.");

            Telepathy = cfg.Bind("Telepathy", "Enabled", true,
                "Above TelepathyAboveHeight the giantess speaks telepathically: a clear voice inside your head, " +
                "not positional, no distance effects.");
            TelepathyAboveHeight = cfg.Bind("Telepathy", "AboveHeight", 1440f,
                "Giantess size in meters (as shown in-game) above which she speaks telepathically. 1440 = normal max size.");
            TelepathyFade = cfg.Bind("Telepathy", "FadeMeters", 200f,
                "Size range below AboveHeight over which the physical voice blends into telepathy.");
            TelepathyEthereal = cfg.Bind("Telepathy", "Ethereal", 0.5f, new ConfigDescription(
                "How dreamlike the telepathic voice sounds (soft reverb and chorus), 0 = dry.",
                new AcceptableValueRange<float>(0f, 1f)));

            TextSyncToVoice = cfg.Bind("Text", "SyncToVoice", true,
                "While a voice line plays, the dialogue text is revealed at the voice's pace (it finishes with the " +
                "voice) and auto-advance waits until the voice has finished. Clicking still skips.");
            ReadMemoryOrb = cfg.Bind("Story", "ReadMemoryOrb", true,
                "Play the audiobook for Memory Orb pages (voices/en/STORY/<page key>.ogg) while you read them.");
            MouthSync = cfg.Bind("Mouth", "Sync", true,
                "While a voice line plays, the speaker's mouth follows the audio loudness (closed in pauses, " +
                "wider on loud syllables) instead of the game's random mouth that stops when the text is shown.");
            MouthRate = cfg.Bind("Mouth", "ShapesPerSecond", 11f, new ConfigDescription(
                "How often the mouth shape may change per second.", new AcceptableValueRange<float>(4f, 20f)));

            Bind(cfg, "Akipo", "af_bella*0.7+af_jessica*0.3", "a", 1.0f, 1.09f);  // FilePitch 1.0 for the Chatterbox pack (0.95 suits the v4 acting pack)
            Bind(cfg, "RedTwin", "af_bella*0.6+af_sarah*0.4", "a", 1.08f, 1.0f);
            Bind(cfg, "BlueTwin", "af_bella*0.8+bf_alice*0.2", "a", 1.0f, 1.0f);
            Bind(cfg, "Narrator", "af_heart", "a", 1.0f, 1.0f);
        }

        private void Bind(ConfigFile cfg, string who, string voice, string lang, float speed, float pitch, float filePitch = 1f)
        {
            string section = "Voice.English." + who;
            _englishVoices[who] = new VoiceSettings
            {
                Voice = cfg.Bind(section, "Voice", voice,
                    "Kokoro voice or weighted blend, e.g. af_bella*0.7+af_jessica*0.3"),
                Lang = cfg.Bind(section, "Lang", lang, "Kokoro pronunciation: a = American, b = British."),
                Speed = cfg.Bind(section, "Speed", speed, "Speaking tempo (1 = normal)."),
                Pitch = cfg.Bind(section, "Pitch", pitch, "Voice pitch (1 = normal, >1 higher). Tempo is kept."),
                FilePitch = cfg.Bind(section, "FilePitch", filePitch, new ConfigDescription(
                    "Playback pitch of the pre-generated voice files (1 = as recorded, 0.95 = a bit deeper). " +
                    "Unity changes the tempo with it (0.95 = 5% slower). Combined with the size deepening; " +
                    "the live TTS server uses Pitch instead.", new AcceptableValueRange<float>(0.7f, 1.3f))),
            };
        }

        /// <summary>Voice for a speaker in the current game language, or null if not configured.</summary>
        public VoiceSettings GetVoice(LocalizationLanguage language, CharacterType character, bool narration)
        {
            if (language != LocalizationLanguage.English)
                return null;
            string who = narration || character == CharacterType.None ? "Narrator" : character.ToString();
            return _englishVoices.TryGetValue(who, out var v) ? v : null;
        }

        /// <summary>Settings for playing voice files (FilePitch): the English section is used for every game
        /// language, because the packs fall back to the English audio.</summary>
        public VoiceSettings GetFileVoice(CharacterType character, bool narration) =>
            GetVoice(LocalizationLanguage.English, character, narration);
    }
}
