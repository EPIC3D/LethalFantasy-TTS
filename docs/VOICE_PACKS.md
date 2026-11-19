# Voice packs – how to make your own

The plugin is only a player: it plays audio files it finds by name. Any voice pack – rendered with any
TTS engine, voice-converted, or recorded by a real person – works as long as it follows this layout.
You can also replace single lines of an existing pack.

## Folder layout

```
<game>/BepInEx/plugins/LethalFantasyTTS/voices/en/
    AKIPO/<KEY>.ogg
    RED/<KEY>.ogg      (Solana, the red twin)
    BLUE/<KEY>.ogg     (Selena, the blue twin)
```

- `en` = the language of the lines (only English is supported for now).
- The folder is the **speaker**. Some lines can be spoken by either twin; put the file in the folder
  of the twin who should say it (or in both).

## File names

`<KEY>.ogg` – `KEY` is the line's **translation key**, exactly as the game uses it (the same IDs as
on the game's translation site), e.g.

```
AKIPO/AKIPO_DIALOGUE_GROW_START2_8.ogg
RED/REDTWIN_DIALOGUE_HOUSE_DEATH_FAILURE_2_1.ogg
BLUE/BLUETWIN_DIALOGUE_TITLE_RANDOM_22_1.ogg
```

**Identity variants:** lines containing `{they}` / `{them}` / `{they're}` change with the player's
chosen identity. Make one file per variant:

```
<KEY>__he.ogg     <KEY>__she.ogg     <KEY>__they.ogg
```

The plugin first looks for `<KEY>__<identity>.ogg`, then for `<KEY>.ogg`.

## Audio format

- **Ogg Vorbis** (`.ogg`) – the only format the plugin loads.
- Mono recommended (the plugin plays it in 3D at the character's mouth). Any sample rate works;
  44.1 or 48 kHz is fine. Quality `-q:a 5…6` is plenty.
- Keep a short silence (~0.1–0.15 s) at the start so the first word isn't clipped; trim long trailing
  silence or breaths.
- Normalize loudness across the pack so lines don't jump in volume.

Example conversion: `ffmpeg -i line.wav -ac 1 -c:a libvorbis -q:a 6 KEY.ogg`

## Getting the list of lines

The game's text belongs to its author and is not distributed with this mod, but you can export the
line list from **your own copy** of the game:

1. In `BepInEx/config/lf-tts.cfg` set `[Developer] ExportLines = true` and start the game once.
2. It writes to `<game>/BepInEx/LethalFantasyTTS/`:
   - `english_lines.jsonl` – every key and its English text (`{"key": ..., "text": ...}`; raw text
     with `{they}` placeholders and rich-text tags such as `<i>`),
   - `dialogue_events.jsonl` – for each dialogue line: key, speaking character, line type and the
     facial expression (useful as an emotion hint).
3. Set `ExportLines` back to `false`.

Strip rich-text tags (`<color=...>`, `<i>`, …) and symbols like `~` before speaking a line.

## What not to voice

- The player's own answers and choices (keys ending in `_Q`, `_Q_A`, …) – the player character is
  never voiced.
- Narration (`_S` keys, line types `StoryDialogue` / `StoryPoise`).

## Tuning in game

Per character in the `.cfg`, `[Voice.English.<Character>] FilePitch` changes the playback pitch of the
files (e.g. `0.95` = slightly deeper; it also slows the tempo slightly). Volume and all 3D effects
apply to every pack.

## Sharing packs

Voice packs speak the game author's lines, so they are derived from the author's work: only publish a
pack if the game's author allows it. Packs for personal use are fine.
