using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading;
using UnityEngine;

namespace LethalFantasyTTS
{
    /// <summary>
    /// Synthesizes lines on one background worker (current line first, then prefetched upcoming
    /// lines), caches the audio, and plays it on the main thread from an AudioSource that follows
    /// the speaker's mouth. Distances are relative to the giantess' size, so a bigger giantess is
    /// heard from proportionally farther away (and sounds deeper / echoes more).
    /// </summary>
    internal sealed class SpeechPlayer : MonoBehaviour
    {
        public static SpeechPlayer Instance { get; private set; }

        /// <summary>Returns the live player, recreating it if the game destroyed it (scene changes).</summary>
        public static SpeechPlayer Ensure()
        {
            if (Instance == null) // Unity null check: also true for destroyed objects
            {
                var go = new GameObject("LethalFantasyTTS");
                DontDestroyOnLoad(go);
                go.hideFlags = HideFlags.HideAndDontSave;
                Instance = go.AddComponent<SpeechPlayer>();
                Plugin.Log.LogInfo("[TTS] SpeechPlayer created");
            }
            return Instance;
        }

        // ---------------------------------------------------------------- synthesis worker (static, survives player recreation)

        private sealed class Entry
        {
            public string Body;
            public volatile bool Done;
            public float[] Samples;
            public int SampleRate;
            public long SynthMs;
        }

        private const int CacheSize = 64;
        private static readonly object Gate = new object();
        private static readonly Dictionary<string, Entry> Cache = new Dictionary<string, Entry>();
        private static readonly LinkedList<string> CacheOrder = new LinkedList<string>();
        private static readonly LinkedList<Entry> Pending = new LinkedList<Entry>();
        private static Thread _worker;
        private static bool _serverErrorLogged;

        /// <summary>Queue a line for synthesis; urgent lines go to the front.</summary>
        private static Entry Request(string body, bool urgent)
        {
            lock (Gate)
            {
                if (Cache.TryGetValue(body, out var e))
                {
                    if (urgent && !e.Done && Pending.Remove(e)) Pending.AddFirst(e);
                    CacheOrder.Remove(body);
                    CacheOrder.AddLast(body);
                    return e;
                }
                e = new Entry { Body = body };
                Cache[body] = e;
                CacheOrder.AddLast(body);
                if (urgent) Pending.AddFirst(e); else Pending.AddLast(e);
                while (CacheOrder.Count > CacheSize)
                {
                    string old = CacheOrder.First.Value;
                    CacheOrder.RemoveFirst();
                    if (Cache.TryGetValue(old, out var oe)) Pending.Remove(oe);
                    Cache.Remove(old);
                }
                if (_worker == null)
                {
                    _worker = new Thread(WorkerLoop) { IsBackground = true, Name = "LethalFantasyTTS" };
                    _worker.Start();
                }
                Monitor.Pulse(Gate);
                return e;
            }
        }

        private static void WorkerLoop()
        {
            while (true)
            {
                Entry e;
                lock (Gate)
                {
                    while (Pending.Count == 0) Monitor.Wait(Gate);
                    e = Pending.First.Value;
                    Pending.RemoveFirst();
                }
                var sw = System.Diagnostics.Stopwatch.StartNew();
                try
                {
                    byte[] wav = Post(Plugin.Cfg.ServerUrl.Value.TrimEnd('/') + "/tts", e.Body);
                    e.Samples = ParseWav(wav, out e.SampleRate);
                    _serverErrorLogged = false;
                }
                catch (Exception ex)
                {
                    e.Samples = new float[0];
                    lock (Gate) Cache.Remove(e.Body); // allow a retry later
                    if (!_serverErrorLogged)
                    {
                        _serverErrorLogged = true;
                        Plugin.Log.LogWarning("TTS server request failed (" + Plugin.Cfg.ServerUrl.Value + "): " + ex.Message);
                    }
                }
                e.SynthMs = sw.ElapsedMilliseconds;
                e.Done = true;
            }
        }

        /// <summary>Request body for a line, or null if this line should not be spoken.</summary>
        private static string BuildBody(CharacterType character, DialogueType type, string text, out bool narration)
        {
            var cfg = Plugin.Cfg;
            // StoryPoise = narration; StoryDialogue spoken by a character (e.g. Akipo in the nightgown story) is speech.
            narration = type == DialogueType.StoryPoise ||
                        (type == DialogueType.StoryDialogue && character == CharacterType.None);
            if (!cfg.Enabled.Value || string.IsNullOrWhiteSpace(text)) return null;
            if (type == DialogueType.Normal && !cfg.ReadNormal.Value) return null;
            if (type == DialogueType.Followup && !cfg.ReadFollowup.Value) return null;
            if (narration && !cfg.ReadStory.Value) return null;

            var voice = cfg.GetVoice(Localization.SelectedLanguage, character, narration);
            if (voice == null) return null;
            return "{\"text\":" + Json(text) + ",\"voice\":" + Json(voice.Voice.Value) +
                ",\"lang\":" + Json(voice.Lang.Value) +
                ",\"speed\":" + voice.Speed.Value.ToString(CultureInfo.InvariantCulture) +
                ",\"pitch\":" + voice.Pitch.Value.ToString(CultureInfo.InvariantCulture) + "}";
        }

        /// <summary>Start synthesizing a line that will probably be shown soon.</summary>
        public static void Prefetch(CharacterType character, DialogueType type, string text, string key)
        {
            if (!Plugin.Cfg.UseServer.Value) return;
            string body = BuildBody(character, type, text, out bool narration);
            if (body == null || (!narration && FindPackFile(character, text, key) != null)) return;
            Request(body, urgent: false);
        }

        // ---------------------------------------------------------------- playback (main thread)

        private AudioSource _source;
        private AudioLowPassFilter _lowPass;
        private AudioReverbFilter _reverb;
        private AudioEchoFilter _echo;
        private float _indoor; // 0 = outside, 1 = under a roof (smoothed like the game's OccludeAudio)
        private Transform _follow;
        private Giantess _speaker;
        private bool _pausedByMenu;
        private LocalizationLanguage _lastSkippedLanguage = (LocalizationLanguage)(-1);

        private Entry _wanted;
        private CharacterType _wantedCharacter;
        private float _basePitch = 1f;
        private bool _wantedNarration;
        private float _wantedSince;
        private string _wantedKey;
        private int _wantedChars;
        private float _playAt = -1f; // realtime when the delayed (speed of sound) clip should start

        private void Awake()
        {
            _source = gameObject.AddComponent<AudioSource>();
            _source.playOnAwake = false;
            _source.rolloffMode = AudioRolloffMode.Linear;
            _source.dopplerLevel = 0f;
            _lowPass = gameObject.AddComponent<AudioLowPassFilter>();
            _lowPass.cutoffFrequency = 22000f;
            _reverb = gameObject.AddComponent<AudioReverbFilter>();
            _reverb.reverbPreset = AudioReverbPreset.Mountains; // characters are always outdoors
            _reverb.enabled = false;
            _echo = gameObject.AddComponent<AudioEchoFilter>();
            _echo.dryMix = 1f;
            _echo.enabled = false;
            _chorus = gameObject.AddComponent<AudioChorusFilter>();
            _chorus.enabled = false;
            _highPass = gameObject.AddComponent<AudioHighPassFilter>();
            _highPass.cutoffFrequency = 10f;
            _highPass.enabled = false;
        }

        private AudioChorusFilter _chorus;
        private AudioHighPassFilter _highPass;

        // OverlapMode "Queue": a line that arrives while another is still playing waits for it
        // (only the newest waiting line is kept, so speech never lags far behind the text).
        private sealed class QueuedLine
        {
            public CharacterType Character;
            public DialogueType Type;
            public string Text, Key;
            public float Since;
        }

        private QueuedLine _queued;
        private bool _loading; // a voice pack clip is being loaded

        private bool Busy => _source.isPlaying || _playAt >= 0f || _wanted != null || _loading || _pausedByMenu;

        /// <summary>0 = physical voice, 1 = fully telepathic (giantess taller than the configured height).</summary>
        private float TelepathyAmount()
        {
            var cfg = Plugin.Cfg;
            if (!cfg.Telepathy.Value || _speaker == null || LootZoneManager.Instance == null) return 0f;
            float meters = _speaker.GetScaleInMeters;
            float top = cfg.TelepathyAboveHeight.Value;
            return Mathf.InverseLerp(top - Mathf.Max(1f, cfg.TelepathyFade.Value), top, meters);
        }

        /// <summary>Low-pass cutoff (Hz) where air absorption over this distance reaches AtmosphereLossDb.
        /// Approximates ISO 9613-1 at 20 C / 50% RH: alpha(f) ~ 5 dB/km * (f / 1 kHz)^1.6.</summary>
        private static float AtmosphereCutoff(float meters, float lossDb)
        {
            float km = Mathf.Max(0.001f, meters / 1000f);
            float f = 1000f * Mathf.Pow(lossDb / (5f * km), 1f / 1.6f);
            return Mathf.Clamp(f, 150f, 22000f);
        }

        private void OnDestroy() => Plugin.Log.LogWarning("[TTS] SpeechPlayer destroyed");

        private int _packSeq;

        /// <summary>Folder per speaker inside voices/&lt;lang&gt;/ (user layout, easy to swap single lines).</summary>
        private static string CharacterFolder(CharacterType c)
        {
            switch (c)
            {
                case CharacterType.Akipo: return "AKIPO";
                case CharacterType.RedTwin: return "RED";
                case CharacterType.BlueTwin: return "BLUE";
                default: return null;
            }
        }

        /// <summary>Pre-generated clip for this line, or null.
        /// Preferred: voices/en/&lt;AKIPO|RED|BLUE&gt;/KEY[__he|she|they].ogg – named after the Weblate
        /// translation key, one folder per speaker, so players can replace single lines by hand.
        /// Legacy: voices/en/KEY[__Character][__he|she|they].ogg, then sha1("Character|text")[:16].ogg.</summary>
        private static string FindPackFile(CharacterType character, string text, string key)
        {
            var cfg = Plugin.Cfg;
            if (!cfg.UseVoicePack.Value || character == CharacterType.None ||
                Localization.SelectedLanguage != LocalizationLanguage.English)
                return null;
            string dir = cfg.VoicePackDir.Value;
            if (string.IsNullOrEmpty(dir))
                dir = Path.Combine(Path.GetDirectoryName(typeof(Plugin).Assembly.Location), "voices");
            string langDir = Path.Combine(dir, "en");
            if (!string.IsNullOrEmpty(key))
            {
                // The game's own identity word for {they}: he / she / they.
                string ident = Localization.AddIdentity("{they}");
                string folder = CharacterFolder(character);
                if (folder != null)
                {
                    string charDir = Path.Combine(langDir, folder);
                    foreach (string name in new[] { key + "__" + ident, key })
                    {
                        string p = Path.Combine(charDir, name + ".ogg");
                        if (File.Exists(p)) return p;
                    }
                }
                string ch = "__" + character;
                foreach (string name in new[] { key + ch + "__" + ident, key + ch, key + "__" + ident, key })
                {
                    string p = Path.Combine(langDir, name + ".ogg");
                    if (File.Exists(p)) return p;
                }
            }
            string id;
            using (var sha = System.Security.Cryptography.SHA1.Create())
            {
                byte[] h = sha.ComputeHash(Encoding.UTF8.GetBytes(character + "|" + text));
                var sb = new StringBuilder(16);
                for (int i = 0; i < 8; i++) sb.Append(h[i].ToString("x2"));
                id = sb.ToString();
            }
            string path = Path.Combine(langDir, id + ".ogg");
            return File.Exists(path) ? path : null;
        }

        private System.Collections.IEnumerator LoadPack(string path, int seq)
        {
            var uri = new Uri(path).AbsoluteUri;
            _loading = true;
            using (var req = UnityEngine.Networking.UnityWebRequestMultimedia.GetAudioClip(uri, AudioType.OGGVORBIS))
            {
                yield return req.SendWebRequest();
                if (seq != _packSeq) yield break; // replaced by a newer line (it owns _loading now)
                _loading = false;
                if (!string.IsNullOrEmpty(req.error))
                {
                    Plugin.Log.LogWarning("[TTS] voice pack load failed: " + path + ": " + req.error);
                    yield break;
                }
                var clip = UnityEngine.Networking.DownloadHandlerAudioClip.GetContent(req);
                try { PlayClip(clip, "pack " + Path.GetFileName(path)); }
                catch (Exception ex) { Plugin.Log.LogError("[TTS] playback failed: " + ex); }
            }
        }

        // ---- Memory Orb audiobook: voices/en/STORY/<page key>.ogg, plain stereo, stops when the orb closes ----
        private bool _storyActive;

        public void PlayStory(string key)
        {
            if (!Plugin.Cfg.Enabled.Value || !Plugin.Cfg.ReadMemoryOrb.Value || string.IsNullOrEmpty(key)) return;
            string dir = Plugin.Cfg.VoicePackDir.Value;
            if (string.IsNullOrEmpty(dir))
                dir = Path.Combine(Path.GetDirectoryName(typeof(Plugin).Assembly.Location), "voices");
            string path = Path.Combine(Path.Combine(Path.Combine(dir, "en"), "STORY"), key + ".ogg");
            Stop();
            _storyActive = true;
            if (!File.Exists(path))
            {
                if (Plugin.Cfg.DebugLog.Value) Plugin.Log.LogInfo("[TTS] no story audio: " + key);
                return;
            }
            _wantedCharacter = CharacterType.None;
            _wantedNarration = true;  // no speaker in the world -> not positional
            _wantedKey = key;
            _wantedChars = 0;
            _wantedSince = Time.realtimeSinceStartup;
            StartCoroutine(LoadPack(path, ++_packSeq));
        }

        public void Speak(CharacterType character, DialogueType type, string text, string key)
        {
            // Queue mode: let the current line finish; keep only the newest waiting line.
            if (Plugin.Cfg.OverlapQueue && Busy)
            {
                if (_queued != null && Plugin.Cfg.DebugLog.Value)
                    Plugin.Log.LogInfo("[TTS] skipped (newer line waiting): " + _queued.Text);
                _queued = new QueuedLine { Character = character, Type = type, Text = text, Key = key,
                    Since = Time.realtimeSinceStartup };
                return;
            }
            SpeakNow(character, type, text, key);
        }

        private void SpeakNow(CharacterType character, DialogueType type, string text, string key)
        {
            int packSeq = ++_packSeq;
            _loading = false;
            string body = BuildBody(character, type, text, out bool narration);
            if (body == null)
            {
                var language = Localization.SelectedLanguage;
                if (Plugin.Cfg.Enabled.Value && Plugin.Cfg.GetVoice(language, character, narration) == null &&
                    _lastSkippedLanguage != language)
                {
                    _lastSkippedLanguage = language;
                    Plugin.Log.LogWarning("No TTS voice configured for language " + language + "; lines are not read.");
                }
                return;
            }

            _source.Stop(); // a new line replaces whatever is being said
            _playAt = -1f;
            _wanted = null;
            _wantedCharacter = character;
            _wantedNarration = narration;
            _wantedSince = Time.realtimeSinceStartup;
            _wantedKey = key;
            _wantedChars = System.Text.RegularExpressions.Regex.Replace(text ?? "", "<[^>]*>", "").Length;

            string pack = narration ? null : FindPackFile(character, text, key);
            if (pack != null)
                StartCoroutine(LoadPack(pack, packSeq));
            else if (Plugin.Cfg.UseServer.Value)
                _wanted = Request(body, urgent: true);
            else if (Plugin.Cfg.DebugLog.Value)
                Plugin.Log.LogInfo("[TTS] not in voice pack: " + character + "|" + text);
        }

        public void Stop()
        {
            if (Plugin.Cfg.DebugLog.Value) Plugin.Log.LogInfo("[TTS] stop");
            _wanted = null;
            _queued = null;
            _loading = false;
            _packSeq++;
            _playAt = -1f;
            if (_source) _source.Stop();
        }

        private void Update()
        {
            if (_storyActive && !MemoryManager.IsActive) { _storyActive = false; Stop(); }
            bool paused = PauseMenu.IsPaused;
            if (paused != _pausedByMenu)
            {
                _pausedByMenu = paused;
                if (paused) _source.Pause(); else _source.UnPause();
            }

            var e = _wanted;
            if (e != null && e.Done)
            {
                _wanted = null;
                if (e.Samples != null && e.Samples.Length > 0)
                {
                    try { Play(e); }
                    catch (Exception ex) { Plugin.Log.LogError("[TTS] playback failed: " + ex); }
                }
            }

            if (_playAt >= 0f && !_pausedByMenu && Time.realtimeSinceStartup >= _playAt)
            {
                _playAt = -1f;
                _source.Play();
            }

            // Queue mode: start the waiting line once the current one has finished.
            if (_queued != null && !Busy)
            {
                var q = _queued;
                _queued = null;
                if (Plugin.Cfg.DebugLog.Value)
                    Plugin.Log.LogInfo("[TTS] queued line starts after " +
                        ((Time.realtimeSinceStartup - q.Since) * 1000f).ToString("0") + " ms");
                try { SpeakNow(q.Character, q.Type, q.Text, q.Key); }
                catch (Exception ex) { Plugin.Log.LogError("[TTS] queued line failed: " + ex); }
            }
        }

        private void Play(Entry e)
        {
            var clip = AudioClip.Create("tts", e.Samples.Length, 1, e.SampleRate, false);
            clip.SetData(e.Samples, 0);
            PlayClip(clip, "server, synth " + e.SynthMs + " ms");
        }

        private void PlayClip(AudioClip clip, string origin)
        {
            var cfg = Plugin.Cfg;
            _follow = null;
            _speaker = null;
            if (!_wantedNarration) FindMouth(_wantedCharacter, out _follow, out _speaker);
            // Per-character pitch for the voice files (the live server applies its own Pitch).
            var voice = cfg.GetVoice(Localization.SelectedLanguage, _wantedCharacter, _wantedNarration);
            _basePitch = !origin.StartsWith("server") && voice != null
                ? Mathf.Clamp(voice.FilePitch.Value, 0.7f, 1.3f) : 1f;

            if (_source.clip != null) Destroy(_source.clip);
            _source.clip = clip;
            _source.volume = cfg.Volume.Value;
            _source.spatialBlend = _follow != null ? cfg.SpatialBlend.Value : 0f;
            LateUpdate();

            // Speed of sound: real distance, capped so huge giantesses do not wait for ages.
            float distance = _follow != null && ListenerPos(out var lp) ? Vector3.Distance(lp, _follow.position) : 0f;
            float delay = cfg.SoundDelay.Value ? distance / 343f : 0f;
            if (cfg.SoundDelayMax.Value > 0f) delay = Mathf.Min(delay, cfg.SoundDelayMax.Value);
            _playAt = Time.realtimeSinceStartup + delay;

            if (cfg.DebugLog.Value)
            {
                Plugin.Log.LogInfo("[TTS] play " + _wantedCharacter + " (" + origin + "), waited " +
                    ((Time.realtimeSinceStartup - _wantedSince) * 1000f).ToString("0") + " ms, " +
                    clip.length.ToString("0.0") + " s audio, distance " +
                    distance.ToString("0.0") + ", scale " + (_speaker != null ? _speaker.scale.ToString("0.00") : "-") +
                    ", delay " + (delay * 1000f).ToString("0") + " ms, pitch " + _source.pitch.ToString("0.00"));
            }
        }

        // ---- Mouth: imitation driven by the audio loudness (MouthSync) ----
        private bool _mouthDriving;
        private GiantessExpressionManager _mouthMgr;
        private float _mouthNext, _mouthPeak;
        private readonly float[] _mouthBuf = new float[512];
        private readonly System.Random _mouthRnd = new System.Random();

        private void UpdateMouth()
        {
            bool speaking = Plugin.Cfg.MouthSync.Value && _source.isPlaying && _source.clip != null &&
                            _speaker != null && _speaker.expressionManager != null;
            if (!speaking)
            {
                // Voice finished but the same line's text is still being revealed (slow text speed):
                // keep the mouth closed instead of letting the game flap it silently.
                if (_mouthDriving && LineStillShowing())
                {
                    _mouthMgr.mouthOverride.ClearDialogueMovement();
                    var d = DialogueManager.Instance;
                    if (d != null) d.ignoreMouth = true;
                    return;
                }
                if (_mouthDriving) StopMouth();
                return;
            }
            if (!_mouthDriving)
            {
                _mouthDriving = true;
                _mouthMgr = _speaker.expressionManager;
                _mouthPeak = 0.05f;
            }
            var dm = DialogueManager.Instance;
            if (dm != null) dm.ignoreMouth = true; // stop the game's random text-reveal mouth while we drive it
            if (Time.unscaledTime < _mouthNext) return;
            _mouthNext = Time.unscaledTime + 1f / Mathf.Max(4f, Plugin.Cfg.MouthRate.Value);

            // Loudness of the clip at the play position (not of the 3D output, so distance doesn't close the mouth).
            float rms;
            try
            {
                var clip = _source.clip;
                int pos = Mathf.Clamp(_source.timeSamples, 0, Mathf.Max(0, clip.samples - _mouthBuf.Length / clip.channels));
                clip.GetData(_mouthBuf, pos);
                double sum = 0;
                for (int i = 0; i < _mouthBuf.Length; i++) sum += _mouthBuf[i] * _mouthBuf[i];
                rms = (float)Math.Sqrt(sum / _mouthBuf.Length);
            }
            catch (Exception)
            {
                _source.GetOutputData(_mouthBuf, 0);
                double sum = 0;
                for (int i = 0; i < _mouthBuf.Length; i++) sum += _mouthBuf[i] * _mouthBuf[i];
                rms = (float)Math.Sqrt(sum / _mouthBuf.Length);
            }
            _mouthPeak = Mathf.Max(_mouthPeak * 0.995f, rms);
            float level = rms / Mathf.Max(0.02f, _mouthPeak);

            var mouth = _mouthMgr.mouthOverride;
            if (level < 0.15f)
                mouth.ClearDialogueMovement(); // pause -> the expression's own (closed) mouth
            else if (level > 0.65f)
                mouth.SetDialogueMovement(_mouthRnd.Next(2) == 0 ? ExpressionEnum.MouthA : ExpressionEnum.MouthO);
            else if (level > 0.35f)
                mouth.SetDialogueMovement(_mouthRnd.Next(2) == 0 ? ExpressionEnum.MouthE : ExpressionEnum.MouthO);
            else
                mouth.SetDialogueMovement(_mouthRnd.Next(2) == 0 ? ExpressionEnum.MouthI : ExpressionEnum.MouthU);
        }

        private bool LineStillShowing()
        {
            var dm = DialogueManager.Instance;
            if (dm == null || _mouthMgr == null || string.IsNullOrEmpty(_wantedKey)) return false;
            return dm.IsInProgress && dm.lastExpression.text == _wantedKey;
        }

        // ---- Text pace follows the voice (TextSyncToVoice) ----
        // The game shows floor(stateTime * charSpeed * TEXT_SPEED_SCALER) characters. Instead of changing the
        // speed (that either can't slow down or makes the text jump back), we set stateTime so the visible
        // characters follow the voice's progress – monotonic, independent of the player's text speed setting.
        private string _syncKey;
        private float _closedFor;
        private float _syncStartShown, _syncShown;

        private void UpdateTextSync()
        {
            var dm = DialogueManager.Instance;
            if (dm == null || !Plugin.Cfg.TextSyncToVoice.Value || _wantedNarration || string.IsNullOrEmpty(_wantedKey) ||
                dm.lastExpression.text != _wantedKey || dm.state != DialogueManager.DialogueState.Hold || _source.clip == null)
                return;
            float rate = Mathf.Max(0.01f, dm.charSpeed * DialogueManager.TEXT_SPEED_SCALER);
            if (_syncKey != _wantedKey)
            {
                if (!_source.isPlaying) return; // voice not started yet: the game's own pace until it does
                _syncKey = _wantedKey;
                _syncStartShown = Mathf.Min(_wantedChars, dm.stateTime * rate);
                _syncShown = _syncStartShown;
            }
            if (dm.finishedText)
            {
                if (_source.isPlaying) dm.stateTime = 0f; // hold auto-advance while the voice still speaks
                return;
            }
            float target;
            if (_source.isPlaying)
            {
                float frac = Mathf.Clamp01(_source.time / Mathf.Max(0.1f, _source.clip.length - 0.2f));
                target = _syncStartShown + (_wantedChars - _syncStartShown) * frac;
            }
            else
                target = _wantedChars + 1f; // voice finished: show the rest now
            _syncShown = Mathf.Max(_syncShown, target); // never hide characters already shown
            dm.stateTime = (_syncShown + 0.5f) / rate;
        }

        private static bool PlayerHoldsSkip()
        {
            var dm = DialogueManager.Instance;
            if (dm == null || dm.skipInputGameObject == null || !dm.skipInputGameObject.activeInHierarchy) return false;
            if (Input.GetMouseButton(1)) return true;
            var input = KiriInputManager.Instance;
            return input != null && input.GetKey(KiriKey.Interact);
        }

        private void StopMouth()
        {
            _mouthDriving = false;
            if (_mouthMgr != null) _mouthMgr.mouthOverride.ClearDialogueMovement();
            _mouthMgr = null;
            var dm = DialogueManager.Instance;
            if (dm != null) dm.ignoreMouth = false;
        }

        private void LateUpdate()
        {
            var cfg = Plugin.Cfg;
            // Safety net: with the text following the voice the dialogue stays open until the voice ends, so a
            // voice that keeps playing after the dialogue closed means the player skipped it.
            if (cfg.TextSyncToVoice.Value && _source.isPlaying && !_wantedNarration)
            {
                var dmx = DialogueManager.Instance;
                if (dmx != null && !dmx.IsInProgress) { if ((_closedFor += Time.unscaledDeltaTime) > 0.3f) { Stop(); _closedFor = 0f; } }
                else _closedFor = 0f;
            }
            // "Hold right click to skip": while the skip button is held, don't touch stateTime, so the game's
            // skip progress bar can fill (resetting it made the bar restart forever). The voice keeps playing;
            // if the skip completes, the dialogue closes and the safety net above stops the voice.
            bool holdingSkip = PlayerHoldsSkip();
            if (holdingSkip) _syncKey = null; // re-anchor the text sync if the player lets go early
            if (!holdingSkip)
            try { UpdateTextSync(); }
            catch (Exception ex) { if (cfg.DebugLog.Value) Plugin.Log.LogWarning("[TTS] text sync: " + ex.Message); }
            try { UpdateMouth(); }
            catch (Exception ex) { if (cfg.DebugLog.Value) Plugin.Log.LogWarning("[TTS] mouth: " + ex.Message); }
            if (_follow == null)
            {
                _lowPass.cutoffFrequency = 22000f;
                _reverb.enabled = false;
                _echo.enabled = false;
                _chorus.enabled = false;
                _highPass.enabled = false;
                _source.pitch = _basePitch;
                return;
            }

            // Only work while a line is playing (or about to start); idle costs nothing.
            if (!_source.isPlaying && _playAt < 0f && !_pausedByMenu) return;

            var player = PlayerDriver.LocalPlayer;
            bool underRoof = cfg.IndoorMuffle.Value && player != null && player.underRoof;
            _indoor = Mathf.MoveTowards(_indoor, underRoof ? 1f : 0f, Time.unscaledDeltaTime * 2f);

            transform.position = _follow.position;
            // Distances scale with the giantess, so loudness stays even as she grows.
            float scale = _speaker != null ? Mathf.Max(1f, _speaker.scale) : 1f;
            float min = cfg.MinDistance.Value * scale, max = cfg.MaxDistance.Value * scale;
            _source.minDistance = min;
            _source.maxDistance = max;

            // Bigger body -> deeper voice. Physical: pitch ~ 1/size (vocal folds and tract scale with her),
            // weakened by DeepenStrength %. Gentle: the old fixed drop per 10x growth.
            // Note: Unity's pitch also changes tempo, hence the DeepenMinPitch floor.
            float growth = Mathf.Log10(scale);
            float pitch = 1f;
            if (cfg.Deepen.Value)
            {
                float strength = Mathf.Max(0f, cfg.DeepenStrength.Value) / 100f;
                pitch = cfg.DeepenPhysical
                    ? Mathf.Pow(scale, -strength)
                    : 1f - cfg.DeepenPerTenfold.Value * strength * growth;
                pitch = Mathf.Max(cfg.DeepenMinPitch.Value, pitch);
            }
            _source.pitch = pitch * _basePitch;

            // Bigger body -> more open-air echo, scaled by ReverbAmount (0 = off, 1 = default, 2 = double).
            float amount = Mathf.Clamp(cfg.ReverbAmount.Value, 0f, 2f);
            _reverb.enabled = cfg.Reverb.Value && growth > 0.01f && amount > 0f;
            float space = Mathf.Clamp01(growth / Mathf.Max(0.1f, cfg.ReverbFullAtTenfold.Value)) * amount;
            if (_reverb.enabled)
            {
                _reverb.reverbPreset = AudioReverbPreset.User;
                _reverb.dryLevel = 0f;
                _reverb.room = Mathf.Min(0f, Mathf.LerpUnclamped(-3000f, -600f, space));
                _reverb.reverbLevel = Mathf.Min(2000f, Mathf.LerpUnclamped(-2000f, 0f, space));
                _reverb.decayTime = Mathf.Clamp(Mathf.LerpUnclamped(1.0f, 4.0f, space), 0.1f, 20f);
                _reverb.reflectionsDelay = Mathf.Clamp(Mathf.LerpUnclamped(0.02f, 0.2f, space), 0f, 0.3f);
                _reverb.reverbDelay = Mathf.Clamp(Mathf.LerpUnclamped(0.03f, 0.1f, space), 0f, 0.1f);
            }

            // Distinct echo from far terrain: longer and stronger the bigger she is.
            _echo.enabled = _reverb.enabled;
            if (_echo.enabled)
            {
                _echo.delay = Mathf.Clamp(cfg.EchoDelayPerTenfold.Value * growth, 10f, 5000f);
                _echo.decayRatio = Mathf.Clamp(Mathf.LerpUnclamped(0.15f, 0.45f, space), 0f, 0.7f);
                _echo.wetMix = Mathf.Clamp01(cfg.EchoWet.Value * space);
            }

            float cutoff = 22000f;
            bool hasListener = ListenerPos(out var lp);
            float distance = hasListener ? Vector3.Distance(lp, _follow.position) : 0f;
            if (cfg.LowPassByDistance.Value && hasListener)
            {
                float t = Mathf.InverseLerp(min, max, distance);
                // Interpolate in log-frequency so the change sounds even.
                cutoff = Mathf.Exp(Mathf.Lerp(Mathf.Log(22000f), Mathf.Log(cfg.LowPassFarCutoff.Value), t));
            }
            // Real air absorption along the straight (slanted) path from the mouth to the listener.
            if (cfg.Atmosphere.Value && hasListener)
                cutoff = Mathf.Min(cutoff, AtmosphereCutoff(distance, cfg.AtmosphereLossDb.Value));
            // Indoors (small huts, doors and windows often open): only partly muffled, by IndoorStrength.
            float indoor = _indoor * Mathf.Clamp01(cfg.IndoorStrength.Value / 100f);
            if (indoor > 0f)
                cutoff = Mathf.Exp(Mathf.Lerp(Mathf.Log(cutoff), Mathf.Log(Mathf.Min(cutoff, cfg.IndoorCutoff.Value)), indoor));

            // Telepathy: the physical voice fades into a magical voice inside the player's head -
            // not positional, clear, with a shimmering chorus, a soft ringing echo, a dreamy space and
            // the low body thinned out (it has no physical source). Strength = TelepathyEthereal.
            float tp = TelepathyAmount();
            float eth = cfg.TelepathyEthereal.Value * tp;
            _chorus.enabled = tp > 0f && eth > 0f;
            _highPass.enabled = tp > 0f && eth > 0f;
            if (tp > 0f)
            {
                _source.spatialBlend = Mathf.Lerp(cfg.SpatialBlend.Value, 0f, tp);
                _source.pitch = Mathf.Lerp(_source.pitch, _basePitch, tp);
                cutoff = Mathf.Exp(Mathf.Lerp(Mathf.Log(cutoff), Mathf.Log(22000f), tp));

                bool hadReverb = _reverb.enabled;
                float room0 = hadReverb ? _reverb.room : -10000f, level0 = hadReverb ? _reverb.reverbLevel : -10000f;
                _reverb.enabled = true;
                _reverb.reverbPreset = AudioReverbPreset.User;
                _reverb.dryLevel = 0f;
                _reverb.room = Mathf.Lerp(room0, Mathf.Lerp(-10000f, -400f, eth), tp);
                _reverb.reverbLevel = Mathf.Lerp(level0, Mathf.Lerp(-10000f, 200f, eth), tp);
                _reverb.decayTime = Mathf.Lerp(_reverb.decayTime, 3.5f, tp);
                _reverb.decayHFRatio = Mathf.Lerp(0.83f, 1.6f, eth); // bright, airy tail
                _reverb.reflectionsDelay = Mathf.Lerp(_reverb.reflectionsDelay, 0.005f, tp);
                _reverb.reverbDelay = Mathf.Lerp(_reverb.reverbDelay, 0.03f, tp);

                // A short "ringing" echo instead of the terrain echo.
                _echo.enabled = true;
                _echo.delay = Mathf.Lerp(_echo.delay, 140f, tp);
                _echo.decayRatio = Mathf.Lerp(_echo.decayRatio, 0.35f, tp);
                _echo.wetMix = Mathf.Lerp(_echo.wetMix, 0.3f * cfg.TelepathyEthereal.Value, tp);

                // Shimmer: three slowly drifting voices around the real one.
                _chorus.dryMix = 1f;
                _chorus.wetMix1 = 0.45f * eth;
                _chorus.wetMix2 = 0.35f * eth;
                _chorus.wetMix3 = 0.25f * eth;
                _chorus.delay = 30f;
                _chorus.rate = 0.5f;
                _chorus.depth = 0.2f;

                // Less chest and body: a voice without a mouth.
                _highPass.cutoffFrequency = Mathf.Lerp(10f, 220f, eth);
            }
            else
            {
                _source.spatialBlend = cfg.SpatialBlend.Value;
            }
            _lowPass.cutoffFrequency = cutoff;
        }

        private static AudioListener _listener;

        private static bool ListenerPos(out Vector3 pos)
        {
            if (_listener == null || !_listener.isActiveAndEnabled)
                _listener = FindObjectOfType<AudioListener>();
            pos = _listener != null ? _listener.transform.position : Vector3.zero;
            return _listener != null;
        }

        private static void FindMouth(CharacterType character, out Transform mouth, out Giantess gts)
        {
            mouth = null;
            gts = null;
            if (character == CharacterType.None || GiantessManager.Instance == null) return;
            gts = GiantessManager.Instance.GetGiantess(character).gts;
            if (gts == null) return;
            mouth = gts.mouthTransform != null ? gts.mouthTransform
                : gts.headTransform != null ? gts.headTransform : gts.transform;
        }

        // ---------------------------------------------------------------- helpers

        // Minimal HTTP/1.0 POST over a raw socket: Mono's HttpWebRequest hangs inside Unity.
        private static byte[] Post(string url, string json)
        {
            var uri = new Uri(url);
            byte[] body = Encoding.UTF8.GetBytes(json);
            string head = "POST " + uri.PathAndQuery + " HTTP/1.0\r\nHost: " + uri.Host + ":" + uri.Port +
                "\r\nContent-Type: application/json\r\nContent-Length: " + body.Length +
                "\r\nConnection: close\r\n\r\n";
            using (var client = new System.Net.Sockets.TcpClient())
            {
                client.ReceiveTimeout = 30000;
                client.SendTimeout = 5000;
                client.Connect(uri.Host, uri.Port);
                using (var stream = client.GetStream())
                using (var ms = new MemoryStream())
                {
                    byte[] h = Encoding.ASCII.GetBytes(head);
                    stream.Write(h, 0, h.Length);
                    stream.Write(body, 0, body.Length);
                    stream.CopyTo(ms);
                    byte[] resp = ms.ToArray();
                    int split = IndexOf(resp, new byte[] { 13, 10, 13, 10 });
                    if (split < 0) throw new IOException("bad HTTP response");
                    string status = Encoding.ASCII.GetString(resp, 0, Math.Min(split, 64));
                    byte[] payload = new byte[resp.Length - split - 4];
                    Array.Copy(resp, split + 4, payload, 0, payload.Length);
                    if (!status.Contains(" 200 "))
                        throw new IOException(status.Split('\r')[0] + ": " + Encoding.UTF8.GetString(payload));
                    return payload;
                }
            }
        }

        private static int IndexOf(byte[] data, byte[] pattern)
        {
            for (int i = 0; i <= data.Length - pattern.Length; i++)
            {
                int j = 0;
                while (j < pattern.Length && data[i + j] == pattern[j]) j++;
                if (j == pattern.Length) return i;
            }
            return -1;
        }

        // Mono 16-bit PCM WAV -> float samples.
        private static float[] ParseWav(byte[] wav, out int sampleRate)
        {
            sampleRate = BitConverter.ToInt32(wav, 24);
            int pos = 12;
            while (pos + 8 <= wav.Length)
            {
                string id = Encoding.ASCII.GetString(wav, pos, 4);
                int size = BitConverter.ToInt32(wav, pos + 4);
                pos += 8;
                if (id == "data")
                {
                    size = Math.Min(size, wav.Length - pos);
                    var samples = new float[size / 2];
                    for (int i = 0; i < samples.Length; i++)
                        samples[i] = BitConverter.ToInt16(wav, pos + i * 2) / 32768f;
                    return samples;
                }
                pos += size + (size & 1);
            }
            return new float[0];
        }

        internal static string Json(string s)
        {
            var sb = new StringBuilder("\"");
            foreach (char c in s)
            {
                switch (c)
                {
                    case '"': sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': break;
                    case '\t': sb.Append("\\t"); break;
                    default:
                        if (c < 0x20) sb.Append("\\u").Append(((int)c).ToString("x4"));
                        else sb.Append(c);
                        break;
                }
            }
            return sb.Append('"').ToString();
        }
    }
}
