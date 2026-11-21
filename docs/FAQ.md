# FAQ – LethalFantasy TTS

Questions about **making** voice packs: see the
[Voice Crucible FAQ](https://github.com/EPIC3D/LethalFantasy-VoiceCrucible/blob/main/docs/FAQ.md).

### Can a character have a male voice (e.g. with a VRM model replacer)?

Yes – the plugin does not care what the character looks like. Three ways:

1. **Live Kokoro voice** (needs the optional Kokoro server, `[General] UseServer = true`): in
   `BepInEx\config\lf-tts.cfg` set a male voice for that character and a neutral pitch, e.g.
   ```
   [Voice.English.Akipo]
   Voice = am_michael
   Pitch = 1.0
   ```
   Other male voices: `am_adam`, `am_eric`, `bm_george`, `bm_lewis` (`bm_` = British, then also `Lang = b`).
   Voice files always win over Kokoro – set `[General] UseVoicePack = false`, or remove that character's folder
   from `voices\en\`, to hear Kokoro for every line.
2. **A male voice pack** (best quality): render one with
   [Voice Crucible](https://github.com/EPIC3D/LethalFantasy-VoiceCrucible) using a male reference clip, then
   put the files into `voices\en\<CHARACTER>\`.
3. **Quick hack:** a lower `FilePitch` (e.g. `0.8`) in `[Voice.English.<Character>]` deepens the existing
   files – it also slows them down and still sounds like a pitched-down voice.

### Can I add a new character?

No. The plugin only voices the dialogue the game itself shows – it cannot add characters or lines. A VRM
model replacer changes how an existing character looks; that character's lines (and voice settings) stay the
same, so you can give it another voice (see above). If a game update adds a new speaking character, a voice
pack for it can be made with Voice Crucible.

### Can I replace single lines?

Yes. Every file is named after the line's translation key (`voices\en\<CHARACTER>\<KEY>.ogg`); replace any file
with your own recording. See [VOICE_PACKS.md](VOICE_PACKS.md).

### Some lines are silent.

Only lines with a voice file are spoken. Lines without one are read by the live Kokoro voice if you installed
it (`UseServer = true`), otherwise they stay silent. Player choices and narration are never voiced.

### Does it work in other game languages?

Yes – it looks in `voices\<language>\` first and falls back to the English pack, so the characters speak
English in every game language until a pack for that language exists. See [VOICE_PACKS.md](VOICE_PACKS.md).

### The voice is too high / too low.

`FilePitch` in `[Voice.English.<Character>]` (0.7–1.3) changes the voice files; `Pitch` changes the live
Kokoro voice. The voice also deepens as the giantess grows (`[Effects] Deepen`).

### Does the plugin send anything over the internet?

No. It only plays files from your disk. The optional Kokoro server runs on your own PC (`127.0.0.1`).

### Where do I report a bug?

Open an issue on GitHub with the game version, the plugin version, `BepInEx\LogOutput.log` and your
`BepInEx\config\lf-tts.cfg`. Please don't post personal data.
