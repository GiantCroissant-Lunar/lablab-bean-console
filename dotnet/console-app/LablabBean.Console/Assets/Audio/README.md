# Audio Assets

This directory contains audio files for the dungeon crawler game.

## Directory Structure

```
Audio/
├── Music/           # Background music tracks
│   ├── dungeon-ambient.mp3
│   ├── battle-theme.mp3
│   └── victory-theme.mp3
└── SoundEffects/    # Sound effects
    ├── footstep.mp3
    ├── sword-swing.mp3
    ├── enemy-hit.mp3
    ├── player-hit.mp3
    ├── door-open.mp3
    ├── item-pickup.mp3
    └── level-up.mp3
```

## Supported Formats

The AudioService uses LibVLCSharp which supports:

- MP3
- WAV
- OGG
- FLAC
- M4A
- And many other formats

## Adding New Audio

1. Place audio files in the appropriate subdirectory
2. Use descriptive filenames (lowercase with hyphens)
3. Update the `AudioManager` class to reference new audio files
4. Ensure files are set to "Copy to Output Directory" in the .csproj if needed

## Free Audio Resources

You can find free game audio from:

- [FreeSound.org](https://freesound.org/)
- [OpenGameArt.org](https://opengameart.org/)
- [Incompetech](https://incompetech.com/) (Creative Commons music)
- [ZapSplat](https://www.zapsplat.com/)

## License

Make sure any audio files you add comply with your project's license and attribution requirements.
