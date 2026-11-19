# LethalFantasy TTS

A BepInEx 5 plugin for **LethalFantasy** that reads the in-game dialogue aloud
(text-to-speech). Work in progress – currently it only captures and logs the
dialogue lines.

**LethalFantasy** is made by **LadyAlice**. This is an unofficial fan-made mod
and is not affiliated with or endorsed by the game's creator.

## Build

1. Copy `GamePath.props.example` to `GamePath.props` and set your game folder
   (it must have BepInEx 5 installed).
2. `dotnet build src/LethalFantasyTTS -c Release`
3. Copy `src/LethalFantasyTTS/bin/Release/netstandard2.1/LethalFantasyTTS.dll`
   into `<game>\BepInEx\plugins`.
