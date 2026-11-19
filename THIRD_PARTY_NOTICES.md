# Third-party notices and credits

The code in this repository is licensed under the MIT License (see `LICENSE`).
This file lists the third-party software, models and content the project uses or depends on.
Nothing listed here is redistributed in this repository unless stated otherwise.

## LethalFantasy (the game)

- **LethalFantasy** © LadyAlice. This is an unofficial, fan-made mod; it is not affiliated with or
  endorsed by the game's author.
- The game's assemblies (`Assembly-CSharp.dll`, Unity modules) are only **referenced at build time**
  from the user's own game installation (`GamePath.props`) and are **never included** in this
  repository or in any release.
- All dialogue text, character names and characters belong to the game's author. Voice packs that
  speak the game's lines are distributed separately and only with the author's permission.

## Playback plugin (`src/`)

| Component | Use | License |
|---|---|---|
| [BepInEx 5](https://github.com/BepInEx/BepInEx) | mod loader, referenced at build time, installed by the user | LGPL-2.1 |
| [HarmonyX](https://github.com/BepInEx/HarmonyX) | runtime patching, ships with BepInEx | MIT |
| Unity engine modules | referenced from the game installation | Unity proprietary (not redistributed) |

## Optional Kokoro add-on (`server/`)

Only the bridge script `server/tts_server.py` is part of this repository. Kokoro and its
dependencies are installed separately by the user (`pip install kokoro`) from their own sources.

| Component | Use | License |
|---|---|---|
| [Kokoro-82M](https://huggingface.co/hexgrad/Kokoro-82M) (hexgrad) | TTS model weights and voices | Apache-2.0 |
| [kokoro](https://github.com/hexgrad/kokoro) | Python inference library | Apache-2.0 |
| [misaki](https://github.com/hexgrad/misaki) | grapheme-to-phoneme for Kokoro | Apache-2.0 |
| [eSpeak NG](https://github.com/espeak-ng/espeak-ng) | phonemizer fallback used by misaki for unknown words | GPL-3.0 |
| [PyTorch](https://pytorch.org/) | inference runtime | BSD-3-Clause |
| [NumPy](https://numpy.org/) | arrays | BSD-3-Clause |
| [python-soundfile](https://github.com/bastibe/python-soundfile) / libsndfile | audio I/O | BSD-3-Clause / LGPL-2.1 |

## Voice packs (separate downloads, not in this repository)

The pre-generated voice packs were produced offline with the following open models. The generated
audio is not itself covered by these software licenses, but they are credited here:

| Tool | Use | License |
|---|---|---|
| [Chatterbox](https://github.com/resemble-ai/chatterbox) (Resemble AI) | tech-demo voices | MIT – outputs carry Resemble AI's imperceptible "PerTh" watermark |
| [Qwen3-TTS](https://github.com/QwenLM/Qwen3-TTS) (Alibaba Qwen) | acting takes (v4 packs) | Apache-2.0 |
| [Seed-VC](https://github.com/Plachtaa/seed-vc) | voice conversion to the fixed character voice (v4 packs) | GPL-3.0 |
| [OpenAI Whisper](https://github.com/openai/whisper) | automatic quality check of the takes | MIT |
| [Resemblyzer](https://github.com/resemble-ai/Resemblyzer) | speaker-similarity check | Apache-2.0 |
| [SpeechMOS / UTMOS](https://github.com/tarepan/SpeechMOS) | speech-quality estimate | MIT |

Please report missing or incorrect attributions via an issue.
