================================================================================
  BASIC UI MOD  —  Anthro Heat  (Free Version)
  Simple real-time character HUD with arousal bars and climax tracking
  Created by Furrnudes, redistribution of this source code is prohibitted
================================================================================
  Author:      Furrnudes.com
  Game:        Anthro Heat 1.1.3+
  Engine:      Unity 2021.3.45.f2
  Framework:   Heat Modding Project V1.0.1
================================================================================
  FEATURES
================================================================================
  ◈ COMPACT HUD (F9 to toggle)
    • Per-character cards with live arousal data
    • Arousal bar with colour gradient (cyan → red)
    • Climax count per character
    • Status indicator: Idle / Active / Close / CLIMAX!
    • Session timer and total climax counter
    • Character spawn/despawn detection
================================================================================
  CONTROLS
================================================================================
  KEY              ACTION
  ──────────────   ─────────────────────────────
  F9               Toggle HUD
================================================================================
  INSTALLATION
================================================================================
  1. Open the Heat Modding Project in Unity 2021.3.45.f2

  2. Copy the "Basic UI Mod" folder into your project's
     Assets/_YourMod/ directory:
       Assets/_YourMod/BasicUI/

  3. Create a new Empty GameObject in the scene

  4. Add these components to it:
       • Mod_Object        (REQUIRED - registers with mod loader)
       • BasicUIManager     (HUD)

  5. Save as a prefab

  6. Export via:  Tools > ModTool > Export Mod

  7. Place the exported .mod file in:
       <GameInstall>/Mods/

================================================================================
  FILE STRUCTURE
================================================================================

  Basic UI Mod/
  ├── Scripts/
  │   ├── BasicUIManager.cs         HUD overlay (singleton)
  │   └── BasicCharacterTracker.cs  Per-character data poller (reflection)
  └── README.txt                    This file


================================================================================
  COMPATIBILITY
================================================================================

  • Works alongside other mods (uses unique GUI.Window IDs)
  • Does not modify any game state
  • Read-only reflection, no game systems are patched or hooked
  • Safe to add/remove at any time
  • Do NOT use alongside Advanced UI Mod — pick one or the other

================================================================================
