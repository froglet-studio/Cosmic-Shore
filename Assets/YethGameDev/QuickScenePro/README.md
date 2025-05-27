# QuickScene Pro

**Version:** 1.0.0  
**Developers:** yethgamedev 

---

## Overview

**QuickScene Pro** is a Unity Editor extension that lets you:
- Quickly open any scene (single or additive)  
- Mark your most-used scenes as favorites  
- Persist favorites per user  
- Search and filter long scene lists  
- Clear all favorites with one click  
- Enjoy a clean, color-coded UI  

Designed for mid-to-large Unity teams to speed up scene navigation.

---

## Installation

1. **Import Package**  
   In Unity: `Assets → Import Package → Custom Package…` and select `SceneSwitcherMaster_v1.0.0.unitypackage`.
2. **Open the Window**  
   Go to `Window → Scene Switcher` or hit **Ctrl+Alt+M** (Cmd+Alt+M on macOS).
3. **Demo Project**  
   Explore the `Demo/` folder to see sample scenes and how favorites/additive loads work out of the box.

---

## Usage

1. **Search**  
   Type in the search bar to filter scenes by name.  
2. **Open**  
   Click **O** (green) to open normally; **A** (light-blue) to open additively.  
3. **Favorites**  
   Click the star button to toggle a favorite. Your favorites list appears at the top.  
4. **Clear All Favorites**  
   Click the "Clear All Favorites" button to reset the list.  
5. **Refresh**  
   Click **Refresh Scenes** anytime to re-scan your project’s scenes.  
6. **Version**  
   The current version is displayed at the bottom-left corner.

---

## Folder Structure

```
Assets/
└── YethGameDev/
    └── SceneSwitcherMaster/
        ├── Editor/             # Tool scripts
        ├── Demo/               # Example scenes & setup
        ├── Documentation/      # PDF docs & this README.md
        ├── Resources/Icons/    # (Optional) custom icons
        ├── LICENSE.txt
        └── README.md
```

---

## License

This tool is released under the MIT License. See [LICENSE.txt](LICENSE.txt) for details.
