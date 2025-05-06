
# Elden Ring Memory Reader & AI Companion

A C# project that reads in-game values like HP from Elden Ring's memory and plans to extend into an immersive AI-based encouragement system.

# Features

- Reads memory from Elden Ring via pointer chains.
- Handles module base address resolution and offset calculation.
- No EAC (Easy Anti-Cheat) support – must be run offline.
- Requires Administrator privileges.
- Planned: Integration with an LLM (e.g., ChatGPT) to impersonate an in-game maiden, commenting and encouraging the player.
- Planned: WPF overlay displaying messages in real-time over Elden Ring.

## Current State

Successfully retrieves HP from Elden Ring’s memory using pointer path:
```
[ModuleBase] + 0x3D65F88 → WorldChrMan Base Pointer → +0x10EF8 → +0x0 → +0x190 → +0x0 → +0x138
```

## 🛠 Setup

1. Run Elden Ring in **offline mode** (disable EAC).
2. Open the project in Visual Studio.
3. Run as Administrator.
4. Readings (like HP) will be logged to the console.

## Future Roadmap

- Add memory reads for additional stats (e.g., Stamina, FP, Position)
- Send stat data to an LLM endpoint (e.g., OpenAI API)
- Create an AI personality that acts as your Maiden
- Build a WPF overlay to display dialogue and status updates
- Optional voice synthesis integration

## !!!Disclaimer!!!

This project is for educational and non-commercial use. Do **NOT** use it online or with EAC enabled. You are responsible for any consequences of modifying game memory.

## License

MIT License
