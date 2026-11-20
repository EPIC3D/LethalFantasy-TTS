# LethalFantasy TTS

A BepInEx 5 plugin for **LethalFantasy** that voices the in-game dialogue: it plays pre-generated
voice files at the speaking character's mouth (3D, with size-based deepening, reverb, distance and
indoor muffling). An optional local Kokoro TTS server can read lines that have no voice file.

**LethalFantasy** is made by **LadyAlice**. This is an unofficial fan-made mod and is not affiliated
with or endorsed by the game's creator. Get the game and support its author on Patreon:
<https://www.patreon.com/cw/LadyAliceR18>.

This repository contains **only the plugin source and the Kokoro integration script**. No game files,
no BepInEx and no voice files are included.

## Install

1. Install **BepInEx 5** (x64, Mono) into the game folder:
   <https://github.com/BepInEx/BepInEx/releases> – start the game once so BepInEx creates its folders.
2. Unzip the plugin release into the game folder – it creates
   `<game>\BepInEx\plugins\LethalFantasyTTS\LFTTS.dll`.
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
   Release downloads: `Voices-Akipo`, `Voices-Red`, `Voices-Blue`, `Voices-MemoryOrb-Audiobook`
   (the Memory Orb story, read aloud when you open a page) or `Voices-All` (all four), plus the optional
   `Voices-Akipo-Interactions-Acting-WIP`.
   **Note:** the default voice packs are a **tech demo without acting direction**: there was no budget
   for the directed ("acted") version. The optional *Akipo Interactions – Acting* pack is a
   **work-in-progress demo** of that acted version (1017 Akipo interaction lines). It replaces files in
   `AKIPO/`, and `FilePitch = 0.95` is recommended for it.
   **Make your own voices:** the plugin is only a player – see [`docs/VOICE_PACKS.md`](docs/VOICE_PACKS.md)
   for the naming scheme, audio format and how to export the line list from your own game.
4. Settings: `<game>\BepInEx\config\lf-tts.cfg` (created on first
   start) – volume, 3D/effects, per-character `FilePitch` (e.g. `0.95` = slightly deeper).

## Optional: live Kokoro TTS for lines without a voice file

Kokoro is installed separately; this repo only contains the small HTTP bridge (`server/tts_server.py`).

1. Python 3.10–3.12, then: `pip install kokoro soundfile` – see <https://github.com/hexgrad/kokoro>
   (model: <https://huggingface.co/hexgrad/Kokoro-82M>, downloaded automatically on first use).
2. Start the bridge: `python server/tts_server.py` (listens on `http://127.0.0.1:5123`).
   - **CPU (default, recommended):** about 1 s per line and steady; the plugin prefetches the next
     lines, so most of them play without waiting.
   - **GPU (optional):** `python server/tts_server.py --device cuda` – needs an NVIDIA GPU and the CUDA
     build of PyTorch (`pip install kokoro` installs the CPU build; get the CUDA one from
     <https://pytorch.org/get-started/locally/>). Next to the game the GPU is often *slower and less
     even* than the CPU, because both compete for the graphics card.
   - Other options: `--port 5123`.
3. In `BepInEx\config\lf-tts.cfg`: `[General] UseServer = true`. Per-character Kokoro voices and blends are set
   in `[Voice.English.<Character>]` (`Voice = af_bella*0.7+af_jessica*0.3`, `Speed`, `Pitch`).

Only English is supported for now.

## Coming later: voice pack generator

The voice packs were made with a separate offline **renderer** (scene-aware acting directions, local
open TTS models, voice consistency and quality checks, game sound effects for the audiobook). It is not
part of this repository yet; it will be published separately once it is ready, so that anyone with a
suitable GPU can render their own voices for LethalFantasy.

## Build

1. Copy `GamePath.props.example` to `GamePath.props` and set your game folder (with BepInEx 5 installed).
2. `dotnet build src/LethalFantasyTTS -c Release`
3. Output: `src/LethalFantasyTTS/bin/Release/netstandard2.1/LFTTS.dll`.

## Game content and copyright

- All dialogue text and other literary and creative content of **LethalFantasy** – the lines, story,
  character names and characters – is the intellectual property of the game's author, **LadyAlice**.
- **None of this content is part of this repository**, and it is **not** licensed under this
  repository's MIT license. The MIT license covers only the source code written for this mod.
- The plugin reads the dialogue from the player's own copy of the game at runtime; it does not
  contain, reproduce or redistribute the game's text.
- Voice packs are spoken renditions of the author's lines and therefore derived from the author's
  work. They are published **with the game author's permission** as separate downloads, are **not**
  covered by the MIT license, and have their own license: [`LICENSE-VOICES.md`](LICENSE-VOICES.md)
  (free to use and share for LethalFantasy only; no selling, no use in other games or media, cloning or AI training
  for personal use only). The **audiobook pack** (Memory Orb story) also contains **sound effects taken
  from the game** (footsteps, destruction, growth, rumble, mana); they belong to the game's author or
  their licensors, are not covered by any license of this project and may only be used with LethalFantasy. Translation keys (e.g. `AKIPO_DIALOGUE_...`) are used as file names only
  to identify which line a file belongs to.

## Licenses and credits

- Code: MIT (see `LICENSE`) – the code only, not the game's content (see above).
- Unofficial fan mod for **LethalFantasy** by LadyAlice – not affiliated with or endorsed by the game's
  author. No game files are included; the game's text and characters belong to their author.
- Third-party software and models (BepInEx, HarmonyX, Kokoro, …) are **not included** – they are
  installed separately from their own sources. Credits and licenses: [`THIRD_PARTY_NOTICES.md`](THIRD_PARTY_NOTICES.md).

## Support

If you enjoy the game, please support its author, LadyAlice, first:
<https://www.patreon.com/cw/LadyAliceR18>.

If you'd also like to support this mod:

[![ko-fi](https://ko-fi.com/img/githubbutton_sm.svg)](https://ko-fi.com/E6Z42AUOBQ)
