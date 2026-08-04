# Primordia

Primordia is a first-person VR dinosaur-hunting survival RPG built in Unity for the Meta Quest 3 standalone headset.

## Project requirements

Install the following before opening the project:

- [Unity Hub](https://unity.com/download)
- Unity Editor **6000.3.9f1** (revision `7a9955a4f2fa`)
- These Unity Editor modules:
  - Android Build Support
  - Android SDK & NDK Tools
  - OpenJDK
- Git
- A Meta Quest headset with Developer Mode and USB debugging enabled for on-device testing

Use the SDK, NDK, and JDK installed by Unity Hub. The toolchain bundled with the required Editor version is the tested configuration and avoids mismatched Android dependencies.

## First-time setup

1. Clone the repository:

   ```bash
   git clone https://github.com/MAvRK7/Primordia.git
   cd Primordia
   ```

2. In Unity Hub, choose **Add > Add project from disk** and select the `PrimordiaGame` directory inside the repository. Do not select the repository root.

3. Open the project with Unity **6000.3.9f1**. On the first launch, Unity will restore the packages in `Packages/manifest.json` and generate the local `Library` directory. This can take several minutes.

4. If Unity reports XR configuration warnings, open **Edit > Project Settings > XR Plug-in Management > Project Validation** and review them before applying a fix. The repository already includes Android OpenXR and Meta Quest settings.

5. Open `Assets/Scenes/SampleScene.unity`. Play Mode can be used for editor-level checks when a compatible OpenXR runtime or simulator is available; use an on-device build for accurate Quest input, performance, and rendering tests.

`SampleScene.unity` is currently the only enabled scene in **File > Build Profiles > Scene List**. `MainMenu.unity` and `BasicScene.unity` exist in the project but are not currently included in player builds.

## Build and install an APK

1. Connect the Quest by USB, put on the headset, and accept the USB debugging prompt.
2. In Unity, open **File > Build Profiles**.
3. Select **Android** and choose **Switch Platform** if Android is not already active.
4. Confirm that `Assets/Scenes/SampleScene.unity` is enabled in the Scene List.
5. Choose **Build and Run** to install directly to the connected headset, or choose **Build** and save the output as:

   ```text
   ../Builds/PrimordiaBuild.apk
   ```

6. To install an existing APK manually, make sure Android `adb` is on your `PATH`, then run:

   ```bash
   adb devices
   adb install -r Builds/PrimordiaBuild.apk
   ```

The root `Builds/` directory is ignored by Git, and the Unity project ignore rules also exclude `*.apk` and `*.aab` outputs. A fresh clone therefore does not include a playable APK; build it locally or obtain a separately distributed release artifact.

## Version and platform information

| Setting | Current value |
| --- | --- |
| Unity Editor | 6000.3.9f1 |
| Primary device | Meta Quest 3 standalone |
| Build platform | Android |
| Application/APK version | 1.0 |
| Android version code | 1 |
| Application ID | `com.DefaultCompany.VRTemplate` |
| Minimum Android API | 32 |
| Target Android API | Automatic/highest installed; the tested local APK resolves to API 36 |
| CPU architecture | ARM64 (`arm64-v8a`) |
| Scripting backend | IL2CPP |
| Graphics pipeline | Universal Render Pipeline 17.3.0 |
| XR runtime | OpenXR 1.16.1 |
| Meta OpenXR package | 2.5.0 |
| XR Interaction Toolkit | 3.4.1 |
| XR Hands | 1.7.3 |
| Input System | 1.18.0 |

The tested Android toolchain bundled with Unity uses Android Build Tools 36.0.0, NDK `27.2.12479018`, and Java 17.

Before creating a distributable release:

- Replace the placeholder company name and application ID in **Project Settings > Player**.
- Increment both **Version** and **Bundle Version Code**.
- Configure a production keystore and keep its credentials outside the repository. The current project does not use a custom keystore, so local builds use debug signing.
- Decide whether the automatic target SDK should be pinned for reproducible release builds.

## Key packages

Unity restores package versions automatically from `PrimordiaGame/Packages/manifest.json` and `packages-lock.json`. Important direct dependencies include:

- Universal Render Pipeline `17.3.0`
- OpenXR Plugin `1.16.1`
- Unity OpenXR: Meta `2.5.0`
- XR Interaction Toolkit `3.4.1`
- XR Hands `1.7.3`
- Input System `1.18.0`
- Android XR OpenXR `1.2.0`
- XR Composition Layers `2.4.0`

Do not manually upgrade these packages during initial setup; open the project with the required Unity version and let Package Manager restore the locked versions first.

## Repository layout

```text
Primordia/
├── PrimordiaGame/          # Unity project; open this directory in Unity Hub
│   ├── Assets/             # Scenes, scripts, models, textures, and XR settings
│   ├── Packages/           # Unity package manifest and lock file
│   └── ProjectSettings/    # Shared Unity and Android player settings
├── Builds/                 # Local build output; ignored by Git
├── Primordia_info_spec.pdf # Project specification
└── README.md
```

Unity-generated directories such as `Library`, `Temp`, `Logs`, `obj`, and `UserSettings` are not source files and should remain untracked.

## Troubleshooting

- **The Android build profile is unavailable:** add Android Build Support, Android SDK & NDK Tools, and OpenJDK to Unity 6000.3.9f1 through Unity Hub.
- **Package restore fails:** confirm internet access to the Unity Package Registry, then retry from **Window > Package Management > Package Manager**.
- **The headset is not listed:** confirm Developer Mode, reconnect the USB cable, accept the authorization prompt inside the headset, and check `adb devices`.
- **Unity opens with missing assets or widespread import errors:** confirm that Unity Hub was pointed at `PrimordiaGame`, close Unity, and allow the same Editor version to finish reimporting the project.
- **The wrong scene launches in a build:** check the enabled and ordered scenes under **File > Build Profiles > Scene List**.

## License

This project is licensed under the [MIT License](LICENSE).
