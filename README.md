
# Elden Ring Memory Reader & AI Companion

A C# project that reads in-game values like HP from Elden Ring's memory and plans to extend into an immersive AI-based encouragement system.

# Features

- Reads memory from Elden Ring via pointer chains.
  
- Handles module base address resolution and offset calculation.
  
- Integration with an LLM (OpenAI API) to impersonate an in-game maiden, commenting and encouraging the player.

- Dynamic API calls on ingame events.
  
- WPF overlay displaying messages in real-time over Elden Ring.

- Voice lines to add immersion.

- setting.ini to personalize functions.

- save game states so not to recomment on already seen events.

- Overlay does not display and no API call are made while elden ring is not full screen

- Auto closes when elden ring is no longer detected.
  

## Current State

- Successfully retrieves stats from Elden Ring’s memory using pointer path.
- Integrated with WPF to overlay AI messages over the game.
- Makes calls to server I made at https://github.com/mmahoney09110/OpenAI-Proxy-Server
- Uses generated voice lines to mimic ingame characters. (static voices for now)

## 🛠 Setup

1. Run Elden Ring in offline mode (disable EAC). **Not doing so could risk in getting banned!**
2. Open the project in Visual Studio.
3. Run as Administrator.
4. Readings (like HP) will be logged set to LLM and be given response back via overlay subtitles.

## Future Roadmap

- Add memory reads for additional stats (e.g., Stamina, FP, Position)
- Dynamic voice synthesis integration
- Different characters. 

## !!!Disclaimer!!!

This project is for educational and non-commercial use. Do **NOT** use it online or with EAC enabled. You are responsible for any consequences of modifying game memory.

## License

This project is licensed under the [MIT License](./LICENSE). You are free to use, modify, and distribute this code, as long as the original license is included in your distribution.

---

> *"Arise now, Ye Tarnished... and bring your HP bar with you."*
