# Changelog

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
