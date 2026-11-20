# Changelog

## 0.5.0 – first release (pre-release): voice pack player, text follows the voice

Tested against LethalFantasy beta 1.0.3 (English).

- `[Text] SyncToVoice` (default on): while a voice line plays, the dialogue text is revealed in step with
  the voice (finishes with it, never jumps back, independent of the text speed setting) and auto-advance
  waits until the voice has finished. Clicking still skips.
- Hold-right-click skip works with voices: holding the skip button no longer resets the skip bar, and
  the voice stops when a dialogue is skipped or closed (`EndDialogue`, or the box closed while still speaking).
- `[Mouth] Sync` (optional, default off): the speaker's mouth follows the voice loudness (closed in pauses,
  wider on loud syllables) until the voice ends, instead of the game's random mouth that stops when the
  text is shown; `ShapesPerSecond` sets how often the shape may change.

- Plugin file renamed to `LFTTS.dll` (remove an old `LethalFantasyTTS.dll` from `BepInEx/plugins`).
- Settings file renamed to `BepInEx/config/lf-tts.cfg` (an existing
  `com.epicopy.copilot.lethalfantasytts.cfg` is copied over on first start).

Also in this release (developed as the unreleased 0.4.0):

- New voice pack layout: `voices/en/<AKIPO|RED|BLUE>/<Weblate KEY>[__he|she|they].ogg` – one folder per
  speaker, file name = the line's translation key, so single lines can be swapped by hand. Older flat
  and hash-named packs still work as fallback.
- Per-character `FilePitch` for the voice files (`[Voice.English.<Character>]`, default 1.0; e.g. 0.95 =
  slightly deeper, recommended for the optional Akipo acting pack).
- Story lines spoken by a character (`StoryDialogue`, e.g. Akipo in the nightgown story) are voiced;
  only real narration (`StoryPoise`) follows `ReadStory`.
- `UseServer` now defaults to **false** (the live Kokoro server is an optional add-on) and `ReadStory`
  to **false** (narration is not voiced). Existing configs keep their values.
- Recommended install: `BepInEx/plugins/LethalFantasyTTS/` with the voice packs in its `voices/` folder.

## 0.3.0 – voice packs, telepathy, atmosphere

Tested against LethalFantasy beta 1.0.2 (English).

- Pre-generated voice packs: lines are looked up by translation key
  (`voices/<lang>/KEY[__Character][__he|she|they].ogg`), falling back to the
  older hash-named packs (`UseVoicePack`, `VoicePackDir`).
- `UseServer`: the live TTS server is only asked for lines missing from the pack
  (and for narration).
- `[Developer] ExportLines`: writes all English lines and dialogue events as
  JSONL for voice pack generation.
- Telepathy: above giantess size 1440 m she speaks inside your head – clear,
  non-positional, with a fade range and an optional ethereal touch (`[Telepathy]`).
- Atmosphere: air absorbs high frequencies over the real distance, so far
  voices become a deep rumble (`Atmosphere`, `AtmosphereLossDb`).
- `DeepenMode` (Physical / Gentle) and `DeepenStrength`; `ReverbAmount`.
- Indoor muffle is now partial: `IndoorStrength` (default 40%), `IndoorCutoff`
  default raised to 2500 Hz. Existing configs keep their old value (1000).
- `[Lines] OverlapMode`: Queue (finish the current line, then say the newest)
  or Cut.
- Red's placeholder voice: softer accent (`af_bella*0.6+af_sarah*0.4`, pitch 1.08).
- Effects are only computed while a line is being spoken.

## 0.2.0 – live TTS prototype

Tested against LethalFantasy beta 1.0.2 (English).

- Local Kokoro-82M TTS server (`server/tts_server.py`, CPU by default).
- Dialogue lines (Normal, Followup, Story) are spoken in-game from the speaker's
  mouth (3D), with placeholder voices for Akipo, Red and Blue.
- Upcoming lines are prefetched and cached, so most lines play without waiting.
- Loudness scales with the giantess' size; distance low-pass, deeper voice and
  open-air reverb/echo as she grows; muffled while the player is under a roof.
- Optional speed-of-sound delay (off by default).
- Pauses with the ESC menu, stops on scene change.

## 0.1.0 – skeleton

- Logs every dialogue line (speaker, type, text).
