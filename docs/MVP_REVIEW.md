# Primordia MVP readiness review

Reviewed 15 September 2026 on `dev/ethan`, after merging `world/albin` and `san-dino-ai` (HEAD `e9cbbe34`). Target: standalone Meta Quest, using the specification's Quest 3 baseline.

## Assessment

Primordia is at the prototype-integration stage. The project contains substantial VR interaction, weapon, dinosaur, and environment work. Its central **hunt → loot → craft → upgrade → hunt tougher** loop still needs to be connected and completed.

The most valuable next milestone is a complete, repeatable 10–15 minute session in one compact area. Prioritize that loop, reliable combat, recovery after death, and measured Quest performance before expanding content.

This review covered the 30 scripts under `Assets/Scripts`, the dinosaur editor tool, relevant weapon/dinosaur prefabs and profiles, text scene wiring, build/XR/render configuration, README, and the [project specification](/Users/ethanhellyer/Documents/Primordia/Primordia_info_spec.pdf). It is a source/configuration review. No fresh Unity build, headset playthrough, or performance capture was run. `SampleScene.unity` is binary; its exact serialized wiring requires Unity inspection. Its source setup and embedded names were inspected. Existing local edits were included and left intact. Imported package internals and every third-party asset were not individually audited.

## 1. Current feature coverage

| Area | What exists | What is still needed |
| --- | --- | --- |
| VR player | XRI rig support, controller hands, locomotion configuration, snap turn, fall recovery, body holsters, saved vignette preference | Integrate into the world; expose comfort settings; validate both hands, reach, collision, recenter, suspend/resume |
| Weapons | Weapon definitions/catalogue, held hitscan guns, magazines/reload, melee speed threshold/cooldowns, damage interface, visual/haptic hooks | Reliable enemy hitboxes, audio, ammo UI and replenishment, progression/crafting integration |
| Dinosaurs | Six species and alpha profiles/prefabs, AI states, health/death, pack behavior, loot tables, optional AI manager | Fix collider/input issues; align navigation; populate the world; fair attack timing; audio; reliable spawning and manager registration |
| World | Terrain, nature assets, baked navigation, site markers | Player/encounter integration, campfire and house functionality, resource gathering, spawn rules, gameplay day/night and biomes |
| Loot/inventory | Loot tables and colored drop cubes; weapon sockets; per-gun ammo reserves | Stable item IDs, material stacks, collection, quantities UI, shared inventory |
| Crafting/equipment | Weapon data and prefabs | Recipes, costs, unlocks, bench interaction, inventory transactions, armor/lights |
| Progression | XP/level counter | Correct XR kill ownership, visible feedback, meaningful unlocks and reachable upgrades |
| Death/recovery | Health/death flag and event; position reset helper | Campfire respawn, health reset, retained session inventory/progress, fade and interaction recovery |
| Menu/UI | Play/Exit callbacks, visual menu, some exposed gameplay events | Correct launch flow, working Settings, pause/return flow, health/ammo/resources/XP/objective readouts and onboarding |
| Delivery/performance | Pinned Unity version, Android/ARM64/IL2CPP/OpenXR configuration, URP profile | Fresh merged-build verification, integrated scene list, device profiling and a repeatable playable APK |

## 2. Highest-priority defects and integration blockers

### P1 — Builds do not launch the integrated game

The global scene list enables only `SampleScene`, and the Android profile uses that global list. MainMenu's Play button also targets SampleScene. The newer `main` scene contains terrain and 3,786 nature prefab/model instances, with no authored gameplay scripts or player/dinosaur prefab instances found in its serialized references. `PlayerSpawn` is just a Transform; `HouseSite` is a visible placeholder.

**Action:** choose one canonical gameplay scene, place the working XR player and encounters into it, wire MainMenu → gameplay, and include both scenes in build order. Keep the weapon range and dinosaur test scene as development scenes. Establish one session owner for player health, resources, XP and world progress across transitions.

Evidence: [scene list](/Users/ethanhellyer/Documents/Primordia/PrimordiaGame/ProjectSettings/EditorBuildSettings.asset:7), [Android profile](</Users/ethanhellyer/Documents/Primordia/PrimordiaGame/Assets/Settings/Build Profiles/Android™.asset:21>), [Play callback](/Users/ethanhellyer/Documents/Primordia/PrimordiaGame/Assets/Scenes/MainMenu.unity:6446), [world spawn marker](/Users/ethanhellyer/Documents/Primordia/PrimordiaGame/Assets/Scenes/main.unity:190368).

### P1 — Runtime keyboard polling conflicts with the configured input system

The project enables the new Input System only. `PlayerNoise.Update` polls legacy WASD; `DinoHealth.Update` polls a legacy debug kill key every frame. These calls are incompatible with that configuration and would produce input exceptions when those components run. Walking noise also cannot represent Quest thumbstick/room-scale movement through WASD polling.

**Action:** derive movement noise from actual player movement and remove or appropriately isolate keyboard debug input. Keep normal gameplay on the Input System already used by weapons/XRI.

Evidence: [input setting](/Users/ethanhellyer/Documents/Primordia/PrimordiaGame/ProjectSettings/ProjectSettings.asset:1016), [movement noise](/Users/ethanhellyer/Documents/Primordia/PrimordiaGame/Assets/Scripts/Player/PlayerNoise.cs:24), [debug kill input](/Users/ethanhellyer/Documents/Primordia/PrimordiaGame/Assets/Scripts/Dino/DinoHealth.cs:93). Unity documents the corresponding failure in its [input issue tracker](https://issuetracker.unity.com/issues/13441/error-invalidoperationexception-you-are-trying-to-read-input-using-the-unityengineinput-class-but-you-have-switched-active-input-handling-to-input-system-package-in-player-settings-is-present-when-usi).

### P1 — Several authored dinosaurs cannot receive weapon collisions

Regular and alpha Velociraptor, T-Rex, Apatosaurus and Stegosaurus prefabs have no Collider components. Their model imports also disable generated colliders. Weapon raycasts and melee contacts require colliders to reach the existing damage resolver. The Training Raptor has a collider, but its profile awards zero XP and its prefab lacks the regular loot component; successful range combat therefore does not demonstrate the reward loop.

**Action:** give each shipped enemy a deliberate damage hitbox and physical collision setup; connect health, loot and XP on the same production prefab. Validate scale and clearance against the XR player's body.

Evidence: [Velociraptor prefab](</Users/ethanhellyer/Documents/Primordia/PrimordiaGame/Assets/Prefabs/Dinos/Regular dinos/Velociraptor.prefab:161>), [model collider import](/Users/ethanhellyer/Documents/Primordia/PrimordiaGame/Assets/DinoModels/Velociraptor/Velociraptor.fbx.meta:212), [weapon collision query](/Users/ethanhellyer/Documents/Primordia/PrimordiaGame/Assets/Scripts/Ranged.cs:244), [training profile generation](/Users/ethanhellyer/Documents/Primordia/PrimordiaGame/Assets/Scripts/Editor/PrimordiaSampleSceneSetup.cs:197).

### P1 — Navigation configurations differ between development scenes

The twelve species/alpha prefabs use agent type `-1372625422`. `main` has a surface configured for that type, while `sanDinoTest` has its surface configured for type `0`. Training Raptor uses type `0`. These setups cannot be freely interchanged; the actual baked data and agent placement also need checking in Unity.

**Action:** standardize the shipped prefab agent types and compatible surfaces, rebake the canonical world, and verify every encounter spawns on navigable ground. Confirm chase, slopes, retreat, and death/replacement in the real gameplay scene.

Evidence: [production agent](</Users/ethanhellyer/Documents/Primordia/PrimordiaGame/Assets/Prefabs/Dinos/Regular dinos/Velociraptor.prefab:168>), [world surface](/Users/ethanhellyer/Documents/Primordia/PrimordiaGame/Assets/Scenes/main.unity:65701), [test surface](/Users/ethanhellyer/Documents/Primordia/PrimordiaGame/Assets/Scenes/sanDinoTest.unity:538).

### P1 — Loot cannot be collected or spent

`DinoLoot` creates colored cubes and adds Rigidbody physics. It does not give them item IDs, stack counts, collection behavior, or inventory credit. `Item` is empty. `BodyInventoryRig` positions weapon holsters; it does not store crafting materials. No authored recipe/crafting service was found.

**Action:** implement item definitions, quantity-based inventory, loot collection and Scrap gathering, then one complete crafting transaction. The spec permits auto-collection, which is a practical first version. A transaction must check all inputs and the unlock, consume resources once, grant a usable item, and update the UI. Add loot cleanup or pooling so repeated hunts do not accumulate loose physics objects/material instances.

Evidence: [drop creation](/Users/ethanhellyer/Documents/Primordia/PrimordiaGame/Assets/Scripts/Dino/DinoLoot.cs:24), [empty item base](/Users/ethanhellyer/Documents/Primordia/PrimordiaGame/Assets/Scripts/Item.cs:3), [holster implementation](/Users/ethanhellyer/Documents/Primordia/PrimordiaGame/Assets/Scripts/BodyInventoryRig.cs:97). Spec pp13–14.

### P1 — XR kills do not correctly resolve progression ownership

Weapons pass an XR interactor/controller child as the attacker. Dinosaur death checks for a Player tag and `PlayerProgression` on that exact object. It does not resolve the owning player root. The progression class itself only changes numbers and logs; it has no recipe/unlock consumers or player-facing display. The sample range's editor setup adds health/noise without adding progression.

**Action:** give combat a consistent player-owner identity, award kill rewards once through it, attach progression to the integrated player, and connect XP/level changes to recipe unlocks and UI. Tune the first upgrade to be reachable in the first short session.

Evidence: [gun attacker](/Users/ethanhellyer/Documents/Primordia/PrimordiaGame/Assets/Scripts/Ranged.cs:150), [melee attacker](/Users/ethanhellyer/Documents/Primordia/PrimordiaGame/Assets/Scripts/Melee.cs:144), [XP ownership check](/Users/ethanhellyer/Documents/Primordia/PrimordiaGame/Assets/Scripts/Dino/DinoHealth.cs:64), [level counter](/Users/ethanhellyer/Documents/Primordia/PrimordiaGame/Assets/Scripts/Player/PlayerProgression.cs:11).

### P1 — Death has no recovery behavior

At zero HP, `PlayerHealth` sets `IsDead` and emits an event. No authored subscriber implements a death/respawn sequence. There is no HP reset/heal API. The rig's fall recovery moves its Transform but does not revive the player. This leaves the player able to continue interacting while enemies consider them dead.

**Action:** implement fade → suspend gameplay interactions → return to campfire → restore health/dead state → resume. Retain loot, gear and XP as required by the spec. Handle lost weapons and leaving the playable bounds without requiring the Editor.

Evidence: [health/death](/Users/ethanhellyer/Documents/Primordia/PrimordiaGame/Assets/Scripts/Player/PlayerHealth.cs:24), [position-only recovery](/Users/ethanhellyer/Documents/Primordia/PrimordiaGame/Assets/Scripts/PlayerRigController.cs:160). Spec pp16–17.

## 3. Work needed for readable, repeatable gameplay

### Combat feedback and fairness

- All eight gun prefabs have unassigned firing, dry-fire and reload audio; melee hit audio is also unassigned. Dinosaur sound hooks lack assigned clips/AudioSources. Add a small complete sound set for the chosen first encounters and weapons.
- Connect health, damage, ammo/reload, loot, XP and unlock feedback to readable world-space UI. Exposed events are groundwork; empty listeners do not give a player feedback.
- Dinosaur damage currently happens immediately when the attack animation is triggered, without a contact/windup or occlusion check. Combat state uses planar distance. Add a readable attack cue and damage timing, and prevent attacks through solid obstacles or inappropriate vertical separation.
- Hearing refreshes the recent-sensing timer before the chase branch, so the later noise-investigation branch is bypassed for the existing positive give-up timers. Separate hearing a location from seeing/identifying prey. Leashing also switches to wandering without clearing the old destination or returning home; make disengagement and encounter reset deliberate.
- Reload currently accepts any `isSelected` gun, including selection by a holster socket. Restrict reload to the intended held weapon and define left/right-handed behavior.
- Validate high-speed melee contact on the headset before deciding whether the current trigger-entry approach needs swept collision detection.

Evidence: [gun audio slots](</Users/ethanhellyer/Documents/Primordia/PrimordiaGame/Assets/Prefabs/Weapons/Scrap Revolver.prefab:10361>), [melee audio slot](/Users/ethanhellyer/Documents/Primordia/PrimordiaGame/Assets/Prefabs/Knife.prefab:487), [dinosaur attack](/Users/ethanhellyer/Documents/Primordia/PrimordiaGame/Assets/Scripts/Dino/DinoAttack.cs:34), [reload selection](/Users/ethanhellyer/Documents/Primordia/PrimordiaGame/Assets/Scripts/Ranged.cs:163).

Hearing/leash evidence: [sensing and combat selection](/Users/ethanhellyer/Documents/Primordia/PrimordiaGame/Assets/Scripts/Dino/DinoAI.cs:163), [wander sampling](/Users/ethanhellyer/Documents/Primordia/PrimordiaGame/Assets/Scripts/Dino/DinoAI.cs:478).

### Ammo, encounters and resource replenishment

Ammo reserves are currently stored on individual guns. `Ammo.Add` exists, but no collection/crafting route replenishes reserves through normal gameplay. Build one working ammo recipe and connect it to the same inventory used by crafting. A melee fallback should keep an empty gun from ending progression.

No runtime encounter population/spawn/respawn system was found. Start with a small authored set of spawn points and a strict active-enemy limit. Replace defeated encounters under clear rules, avoid spawning in the player's view or campfire safe area, and keep every required resource renewable or otherwise sufficient for the session.

The optional `DinoAIManager` is not wired into the inspected scene/prefab assets. Dinosaurs register only during OnEnable if its singleton already exists. If registration runs before manager initialization, those dinosaurs can remain outside its active set. Add deterministic initialization/registration before relying on it for performance. Replace repeated global dinosaur searches with a registered population when profiling or encounter count warrants it.

Evidence: [ammo reserve](/Users/ethanhellyer/Documents/Primordia/PrimordiaGame/Assets/Scripts/Ammo.cs:3), [AI registration](/Users/ethanhellyer/Documents/Primordia/PrimordiaGame/Assets/Scripts/Dino/DinoAI.cs:64), [manager active set](/Users/ethanhellyer/Documents/Primordia/PrimordiaGame/Assets/Scripts/Dino/DinoAIManager.cs:133).

### Menu, onboarding and comfort

MainMenu Settings has an empty callback. A saved vignette setting exists in code, but no scene binding exposes it. Add functional comfort controls, a pause/return path, and short in-world instructions for moving, grabbing, holstering, attacking, reloading, collecting and crafting. Give the player one clear first goal.

Validate readable text at headset distance, both hands, seated/standing holster reach, tracking loss/recenter, collision on slopes, headset suspend/resume, and recovery from dropped essential gear. These are device checks, not confirmed failures from source alone.

Evidence: [empty Settings callback](/Users/ethanhellyer/Documents/Primordia/PrimordiaGame/Assets/Scenes/MainMenu.unity:339), [available comfort setter](/Users/ethanhellyer/Documents/Primordia/PrimordiaGame/Assets/Scripts/PlayerRigController.cs:134).

## 4. Quest performance and build readiness

Use **sustained 72 FPS** as the first acceptance target, matching the project spec. That allows approximately **13.9 ms per frame** for each CPU/GPU pipeline budget. Measure on the standalone headset; a smooth Editor or PC-VR run does not establish Quest performance. Meta's [performance guidance](https://developers.meta.com/horizon/documentation/unity/unity-perf/) also specifies a 72 FPS minimum for interactive applications.

Prioritize these measurements:

1. CPU/GPU frame time in the densest part of the actual world and during the largest allowed encounter.
2. Draw calls, foliage overdraw, visible object counts, terrain cost and memory. The world has 3,786 prefab/model instances, including 2,530 grass instances. Counts alone do not prove a bottleneck; they identify a profiling priority.
3. AI sensing, pathfinding and allocations. The manager is currently optional/unwired, and target selection searches all dinosaurs.
4. Repeated hunts: loot physics/material accumulation, destroyed/replaced enemies, and memory stability.
5. A sustained 15–20 minute session, including device temperature effects, pause/resume and repeated respawn.

The Android URP asset enables HDR, 4× MSAA and a 4096 main-light shadow map. These settings warrant device measurement and tuning; do not assume they are responsible for a measured slowdown before profiling. Inspect foliage LOD/culling/instancing and camera-visible content alongside render settings.

Evidence: [Android quality selection](/Users/ethanhellyer/Documents/Primordia/PrimordiaGame/ProjectSettings/QualitySettings.asset:323), [URP settings](</Users/ethanhellyer/Documents/Primordia/PrimordiaGame/Assets/Settings/Project Configuration/Performance URP Config.asset:26>).

Before distributing the MVP APK, verify a fresh import and Android build from the merged source, correct startup scene, controller/XR initialization, and absence of runtime exceptions. Set a deliberate application identifier/version for the delivered artifact; production store signing can follow when store distribution is actually planned.

Follow-up cleanup on 15 September: the Unity MCP package, generated Claude skills, server configuration, compiler flags, and its installed NuGet dependencies were removed at the user's request. The original review's concern about including that plugin in player builds is resolved in source; Unity package reload/build verification remains outstanding.

Evidence: [player identity](/Users/ethanhellyer/Documents/Primordia/PrimordiaGame/ProjectSettings/ProjectSettings.asset:170).

## 5. Scope: first playable milestone and full specification

### Recommended first playable milestone

A proposed smaller milestone that demonstrates the game's central loop:

- One compact playable area and one functional campfire/bench.
- Two encounter roles: manageable starter prey and a more dangerous target.
- Starter knife, one craftable melee upgrade, one gun and one ammo recipe.
- A few canonical materials, including gathered Scrap.
- Visible resources, health, ammo, XP and a clear crafting goal.
- Kill → collect → return → craft → equip → hunt tougher.
- Death returns the player to camp with retained session progress.
- Replenishable encounters/resources and a restart/return route.
- Comfortable controller operation and sustained 72 FPS on the selected Quest baseline.

The strongest acceptance criterion is that a new player can complete this loop without verbal coaching or Editor intervention. A visible upgrade/tougher-hunt milestone is enough; a narrative campaign ending is unnecessary for this sandbox milestone.

### Full Core scope in the PDF

The document labels a broader set of features as Core. The smaller milestone above does not silently replace those requirements. Unless that specification is explicitly revised, the eventual Core delivery still includes:

- Six dinosaur species and their required behaviors.
- Eight melee weapons and eight guns, with crafted ammunition.
- Three armor tiers and two light tiers.
- Level unlocks and holster/storage expansion.
- Campfire tiers, safe-zone rules, apex exception and kill quests.
- Three house tiers and their rewards.
- Three biomes with capped distance-based spawning/despawning.
- Gameplay day/night modifiers.
- VR UI, sound/VFX and comfort/performance validation.

The spec lists **cross-session JSON saving as Nice-to-have**, and alpha variants/ecosystem expansion outside Core. Preserve the existing work, but defer further expansion there until the central loop is complete. Session state must survive death and relevant scene transitions; disk persistence is a separate decision. See spec pp3–5, 13–19 and 26.

### Reconcile content data before filling recipe tables

Use stable IDs rather than display names. Existing drops vary `Bone`/`bone`, `Crest`/`crest` and `Alpha Pelt`/`Alpha pelt`. Required recipe materials such as Thick Hide and Frill Plate are absent from their expected current drop tables. The PDF also varies material names, lists Shell unlock at level 15 in one place and 14 in another, and describes top gear as near-one-shotting T-Rex while its table lists six gun/seven melee hits.

Create one canonical item, drop, recipe, unlock and balance table. Confirm every upgrade has obtainable ingredients, and that the player can afford ammunition and the next upgrade through ordinary hunts. Resolve numerical contradictions before adding the rest of the catalogue. Evidence: [Triceratops drops](</Users/ethanhellyer/Documents/Primordia/PrimordiaGame/Assets/DinoProfiles/Regular Dinos/Triceratops.asset:35>), [Apatosaurus drops](</Users/ethanhellyer/Documents/Primordia/PrimordiaGame/Assets/DinoProfiles/Regular Dinos/Apatosaurus.asset:29>); spec pp12–17.

## 6. Recommended implementation order

| Milestone | Deliverable | Completion check |
| --- | --- | --- |
| 1. Integrated baseline | Canonical gameplay scene, XR player, two enemies, build/menu routing; fix input, hitboxes, navigation and player ownership | Fresh Quest build enters the actual world; move, grab, fight and receive correctly attributed rewards without runtime exceptions |
| 2. Complete reward loop | Item IDs, inventory, collection, Scrap, bench, one upgrade and ammo recipe, XP/unlock UI | Hunt → collect → craft → equip → tougher hunt succeeds without granting resources through the Editor |
| 3. Recovery and repetition | Campfire respawn, retained session state, enemy/resource replenishment, lost-gear recovery | Player can die, return, recover and complete another hunt without restarting Unity |
| 4. Player-facing finish | Sound, attack cues, health/ammo/resources UI, onboarding, comfort settings and pause/return | A first-time tester completes the 10–15 minute loop with no coaching |
| 5. Device acceptance | Profiling, bounded encounters, world/render tuning and APK verification | Sustained 72 FPS in representative worst-case play; repeated loops and suspend/resume remain usable |
| 6. Full Core expansion | Remaining weapons/species, armor/lights, campfire/house tiers, biome/night systems | Every required recipe/unlock is reachable and each Core system is demonstrated in the delivered build |

Start device measurement at milestone 1 and repeat after material scene/system changes. Milestone 5 is the acceptance gate, not the first time performance is considered.

Parallelize work around agreed interfaces: player/session/inventory; enemy damage/rewards/spawning; world/campfire/navigation; UI/audio/content. Agree item IDs, damage ownership and session events first so those workstreams connect cleanly.

**Immediate recommendation:** complete milestones 1–3 using a small subset of the existing assets. That produces a repeatable game loop and makes subsequent content work directly useful.
