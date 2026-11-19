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
2. Copy `LethalFantasyTTS.dll` into `<game>\BepInEx\plugins\`.
3. Put the voice packs (separate download) into `<game>\BepInEx\plugins\voices\en\`:
   ```
   voices/en/AKIPO/<KEY>.ogg
   voices/en/RED/<KEY>.ogg
   voices/en/BLUE/<KEY>.ogg
   ```
   Files are named after the game's translation key (as on the game's Weblate), with
   `__he` / `__she` / `__they` for lines that depend on the player's identity. Any single file can
   be replaced by your own recording or render.
4. Settings: `<game>\BepInEx\config\com.epicopy.copilot.lethalfantasytts.cfg` (created on first
   start) – volume, 3D/effects, per-character `FilePitch` (e.g. `0.95` = slightly deeper).

## Optional: live Kokoro TTS for lines without a voice file

Kokoro is installed separately; this repo only contains the small HTTP bridge (`server/tts_server.py`).

1. Python 3.10–3.12, then: `pip install kokoro soundfile` – see <https://github.com/hexgrad/kokoro>
   (model: <https://huggingface.co/hexgrad/Kokoro-82M>, downloaded automatically on first use).
2. Start the bridge: `python server/tts_server.py` (listens on `http://127.0.0.1:5123`, CPU by default).
3. In the plugin's `.cfg`: `[General] UseServer = true`. Per-character Kokoro voices and blends are set
   in `[Voice.English.<Character>]` (`Voice = af_bella*0.7+af_jessica*0.3`, `Speed`, `Pitch`).

Only English is supported for now.

## Build

1. Copy `GamePath.props.example` to `GamePath.props` and set your game folder (with BepInEx 5 installed).
2. `dotnet build src/LethalFantasyTTS -c Release`
3. Output: `src/LethalFantasyTTS/bin/Release/netstandard2.1/LethalFantasyTTS.dll`.

## Licenses and credits

- Code: MIT (see `LICENSE`).
- Unofficial fan mod for **LethalFantasy** by LadyAlice – not affiliated with or endorsed by the game's
  author. No game files are included; the game's text and characters belong to their author.
- Third-party software and models (BepInEx, HarmonyX, Kokoro, …) are **not included** – they are
  installed separately from their own sources. Credits and licenses: [`THIRD_PARTY_NOTICES.md`](THIRD_PARTY_NOTICES.md).
