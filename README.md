# Primordia

Primordia is a first-person VR dinosaur-hunting survival game built in Unity for the Meta Quest 3. You head out from camp, hunt dinosaurs, butcher them for parts, and spend those parts on weapons for the next trip.

It's still in development. The current game takes place on an island, with a campsite and weapon shop beside the river.

## Playing the game

Choose **Play** from the main menu to enter the island. Camp is a safe zone, so it's a good place to get your gear sorted before heading out.

- **Hunt:** the island has Parasaurolophus, Triceratops, Stegosaurus, Apatosaurus, Velociraptor, and T-Rex. Dinosaurs also spawn as you explore, with a smaller population around the starting area.
- **Fight:** swing melee weapons, fire guns, or punch with an empty hand. Weapons can be stored in the holsters at your hips and behind your shoulders.
- **Butcher:** after a kill, put away your weapon and point an empty hand at the body. With grip released, press and hold the trigger to draw a temporary knife. Strike the body three times, pulling the blade clear between hits. Release the trigger to put the knife away.
- **Collect:** grab the dropped parts to add them straight to the shop's resource sack. Meat, hide, and pelt count as meat; bones, claws, horns, and other hard parts count as bones. Each butchered dinosaur drops at least one meat and one bone.
- **Buy:** return to the camp shop and spend your resources on weapons. Collect each purchase from the counter before buying another.

The shop currently sells **17 weapons**: nine melee weapons and eight guns, from the knife and wooden spear through to the Rex-Fang Greatblade and Apex Rifle. Prices are set low for the demo, at one or two meat and one or two bones per weapon. The sack starts with 25 meat and 10 bones so you can try some gear straight away.

Your health is shown on your left wrist. Press **Y** on the left controller to return to camp and restore your health, including after dying.

## Quest controls

| Action | Control |
| --- | --- |
| Move | Left thumbstick |
| Snap turn | Right thumbstick |
| Jump | **A** on the right controller |
| Grab a weapon or collect loot | Grip |
| Melee attack | Swing the weapon you're holding |
| Fire a gun | Trigger on the hand holding it |
| Reload | **B** on the right controller |
| Punch | Hold grip + trigger with an empty hand, then swing |
| Draw the butchering knife | Point an empty hand at a corpse and hold trigger with grip released |
| Return to camp and heal | **Y** on the left controller; **Home** in the editor |

For testing, **Settings > Invulnerability (dev)** in the main menu disables incoming damage. It defaults to **OFF** and remembers your choice between launches.

## Opening the project

You'll need Git, [Unity Hub](https://unity.com/download), and **Unity 6000.3.9f1**. Install these modules for that editor version through Unity Hub:

- Android Build Support
- Android SDK & NDK Tools
- OpenJDK

Use Unity's bundled Android tools to avoid SDK, NDK, or Java version mismatches.

1. Clone the repository:

   ```bash
   git clone https://github.com/MAvRK7/Primordia.git
   cd Primordia
   ```

2. In Unity Hub, choose **Add > Add project from disk** and select **`PrimordiaGame`** inside the repository.
3. Open it with **6000.3.9f1** and let Unity finish importing assets and restoring packages. The first import can take a while.
4. Open `Assets/Scenes/MainMenu.unity` for the full launch flow, or `Assets/Scenes/assembly_scene.unity` to work directly on the game.

The project already includes its Android OpenXR settings. If Unity reports XR configuration warnings, check **Edit > Project Settings > XR Plug-in Management > Project Validation**.

Editor Play Mode needs a compatible XR runtime or simulator for VR input. Use a Quest build to check controller behaviour, rendering, and performance on the headset.

## Building for Quest

Enable Developer Mode on the headset, connect it by USB, and accept the USB debugging prompt inside the headset.

1. Open **File > Build Profiles** in Unity.
2. Select **Android** and switch platforms if needed.
3. Check that both scenes are enabled in the Scene List, in this order:

   - `Assets/Scenes/MainMenu.unity`
   - `Assets/Scenes/assembly_scene.unity`

4. Choose **Build and Run** to install on the connected headset, or **Build** to save an APK. The usual output location, relative to the Unity project, is `../Builds/PrimordiaBuild.apk`.

To install an APK manually, run these from the repository root with `adb` on your PATH:

```bash
adb devices
adb install -r Builds/PrimordiaBuild.apk
```

Build outputs are ignored by Git, so a fresh clone doesn't include an APK.

### Project settings

| Setting | Value |
| --- | --- |
| Unity | 6000.3.9f1 |
| Platform | Android, ARM64, IL2CPP |
| Minimum Android API | 32 |
| Target Android API | Automatic / highest installed |
| Rendering | Universal Render Pipeline 17.3.0 |
| XR | OpenXR 1.16.1, Meta OpenXR 2.5.0 |
| Interaction | XR Interaction Toolkit 3.4.1, XR Hands 1.7.3 |
| Input System | 1.18.0 |

Package versions are recorded in `PrimordiaGame/Packages/manifest.json` and `packages-lock.json`. Let Unity restore those versions when first opening the project.

The Android app still uses the placeholder ID `com.DefaultCompany.VRTemplate`, version `1.0`, and version code `1`. Update the app identity, version, and signing settings before a production release.

## Repository layout

```text
Primordia/
├── PrimordiaGame/          # Open this folder in Unity Hub
│   ├── Assets/
│   │   ├── Scenes/         # Main menu, island, and development scenes
│   │   ├── Scripts/        # Player, combat, dinosaurs, spawning, and shop
│   │   ├── Prefabs/        # Player rig, dinosaurs, weapons, and campsite shop
│   │   └── Data/           # Weapon definitions and loot catalog
│   ├── Packages/
│   └── ProjectSettings/
├── Builds/                 # Local APK output; ignored by Git
├── docs/                   # Development notes and earlier reviews
├── Primordia_info_spec.pdf # Original game specification
└── README.md
```

The specification describes the wider plan for the game. Older reviews in `docs/` describe the project at the time they were written.

## Troubleshooting

- **Android isn't available in Build Profiles:** add the Android modules to Unity 6000.3.9f1 through Unity Hub.
- **Packages won't restore:** check your connection to the Unity Package Registry and retry in Package Manager.
- **The headset isn't detected:** check Developer Mode, the USB cable, and the debugging prompt, then run `adb devices`.
- **Missing assets or import errors:** make sure you opened `PrimordiaGame` with the correct editor version and let the import finish.
- **The wrong scene starts:** check the Scene List. `MainMenu` should be first, followed by `assembly_scene`.

## License and credits

Project code is licensed under the [MIT License](LICENSE). Third-party assets retain their own licenses.

- ["Low Poly Hand - 3D Model"](https://skfb.ly/prZXT) by ronildo.facanha is licensed under [CC BY 4.0](https://creativecommons.org/licenses/by/4.0/). The mesh was adapted and rigged for Primordia; see the [asset attribution](PrimordiaGame/Assets/ThirdParty/LowPolyHand/ATTRIBUTION.md).
