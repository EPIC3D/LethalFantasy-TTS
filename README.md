# LethalFantasy TTS

A BepInEx 5 plugin for **LethalFantasy** that voices the in-game dialogue: it plays pre-generated
voice files at the speaking character's mouth (3D, with size-based deepening, reverb, distance and
indoor muffling). An optional local Kokoro TTS server can read lines that have no voice file.

**LethalFantasy** is made by **LadyAlice**. This is an unofficial fan-made mod and is not affiliated
with or endorsed by the game's creator.

This repository contains **only the plugin source and the Kokoro integration script**. No game files,
no BepInEx and no voice files are included.

## Install

1. Install **BepInEx 5** (x64, Mono) into the game folder:
   <https://github.com/BepInEx/BepInEx/releases> – start the game once so BepInEx creates its folders.
2. Unzip the plugin release into the game folder – it creates
   `<game>\BepInEx\plugins\LethalFantasyTTS\LethalFantasyTTS.dll`.
3. Unzip the voice packs (separate downloads, one per character) into the game folder too; they go to
   `<game>\BepInEx\plugins\LethalFantasyTTS\voices\en\`:
   ```
   voices/en/AKIPO/<KEY>.ogg
   voices/en/RED/<KEY>.ogg
   voices/en/BLUE/<KEY>.ogg
   ```
   Files are named after the game's translation key (as on the game's Weblate), with
   `__he` / `__she` / `__they` for lines that depend on the player's identity. Any single file can
   be replaced by your own recording or render.
   **Make your own voices:** the plugin is only a player – see [`docs/VOICE_PACKS.md`](docs/VOICE_PACKS.md)
   for the naming scheme, audio format and how to export the line list from your own game.
4. Settings: `<game>\BepInEx\config\lf-tts.cfg` (created on first
   start) – volume, 3D/effects, per-character `FilePitch` (e.g. `0.95` = slightly deeper).

## Optional: live Kokoro TTS for lines without a voice file

Kokoro is installed separately; this repo only contains the small HTTP bridge (`server/tts_server.py`).

1. Python 3.10–3.12, then: `pip install kokoro soundfile` – see <https://github.com/hexgrad/kokoro>
   (model: <https://huggingface.co/hexgrad/Kokoro-82M>, downloaded automatically on first use).
2. Start the bridge: `python server/tts_server.py` (listens on `http://127.0.0.1:5123`, CPU by default).
3. In `BepInEx\config\lf-tts.cfg`: `[General] UseServer = true`. Per-character Kokoro voices and blends are set
   in `[Voice.English.<Character>]` (`Voice = af_bella*0.7+af_jessica*0.3`, `Speed`, `Pitch`).

Only English is supported for now.

## Build

1. Copy `GamePath.props.example` to `GamePath.props` and set your game folder (with BepInEx 5 installed).
2. `dotnet build src/LethalFantasyTTS -c Release`
3. Output: `src/LethalFantasyTTS/bin/Release/netstandard2.1/LethalFantasyTTS.dll`.

## Game content and copyright

- All dialogue text and other literary and creative content of **LethalFantasy** – the lines, story,
  character names and characters – is the intellectual property of the game's author, **LadyAlice**.
- **None of this content is part of this repository**, and it is **not** licensed under this
  repository's MIT license. The MIT license covers only the source code written for this mod.
- The plugin reads the dialogue from the player's own copy of the game at runtime; it does not
  contain, reproduce or redistribute the game's text.
- Voice packs are spoken renditions of the author's lines and therefore derived from the author's
  work. They are not included here, are not covered by the MIT license, and are only published with
  the author's permission. Translation keys (e.g. `AKIPO_DIALOGUE_...`) are used as file names only
  to identify which line a file belongs to.

## Licenses and credits

- Code: MIT (see `LICENSE`) – the code only, not the game's content (see above).
- Unofficial fan mod for **LethalFantasy** by LadyAlice – not affiliated with or endorsed by the game's
  author. No game files are included; the game's text and characters belong to their author.
- Third-party software and models (BepInEx, HarmonyX, Kokoro, …) are **not included** – they are
  installed separately from their own sources. Credits and licenses: [`THIRD_PARTY_NOTICES.md`](THIRD_PARTY_NOTICES.md).
