# Graphics pipeline - Shadows of Chandni

## Packages (already installed - nothing to add)
- Universal Render Pipeline 17.5 (`com.unity.render-pipelines.universal`), Forward+ renderer, Linear colour space
- Input System, glTFast (character models)
The PC quality level uses `Assets/Settings/PC_RPAsset.asset` + `PC_Renderer.asset`.
Make sure the "PC" quality level is selected (Edit > Project Settings > Quality), not "Mobile".

## What was changed in the project settings (PC_RPAsset / PC_Renderer)
| Setting | Before | After | Why |
|---|---|---|---|
| MSAA | off | 4x | clean edges on every mesh |
| Main light shadow map | 2048 | 4096 | crisp sun/moon shadows |
| Shadow distance | 50 | 35 | spends the resolution where you actually look |
| Soft shadows | High | High | (unchanged) |
| Colour grading mode | LDR | HDR | needed for ACES tonemapping + bloom |
| Additional lights per object | 4 | 8 | many neon/lantern lights in the market |
| SSAO intensity / radius | 0.4 / 0.3 | 0.85 / 0.45 | grounds objects, darkens corners and contact points |
Backups of the originals were not committed; the old numbers are in the table above.

## Runtime look (scripts)
| File | Role |
|---|---|
| `CinematicLook.cs` | URP Volume: ACES tonemapping, HQ bloom, vignette, colour grade (teal shadows / warm highlights, white balance, restrained saturation), bokeh DOF with auto-focus, motion blur, chromatic aberration, film grain. Also soft-shadow helper, trilight ambient, real-time reflection probe. **F2** cycles Cinematic / Balanced / Performance. |
| `ProcTex.cs` | Tileable procedural PBR textures (albedo + normal + smoothness): wood floor, wood, plaster, facade, fabric, rug, wet asphalt, stone. |
| `GameKit.cs` | `Surface / SurfaceZY / SurfaceFit` apply those textures to primitives with correct tiling. |
| `UiKit.cs` | One palette, two fonts, rounded dark-glass cards with drop shadow + gold border, key-cap prompts, bars, buttons. |

## Tuning cheat-sheet
- Too dark/bright: `ColorAdjustments.postExposure` in `CinematicLook.Apply`.
- Too blurry: raise `DepthOfField.aperture` (3.4 -> 6) or lower motion blur intensity.
- Slow: press F2 (Balanced / Performance), or drop MSAA to 2x in PC_RPAsset, or lower SSAO samples in PC_Renderer.
- Want it to look more AAA still: replace the primitive furniture with real PBR models and bake lighting
  (Window > Rendering > Lighting) - the scripts above keep working on top of them.

## Changes after first playtest
- Fixed the normal-map encoding bug (surfaces were lit with skewed normals -> dark, odd colours). Normal maps now use Unity's DXT5nm layout.
- Room brightness: exposure +0.75, stronger ambient/fill lights, only the ceiling bulb casts a point-light shadow.
- Reflection probe now uses a solid warm background (it used to capture the default blue sky).
- Default quality tier is Balanced (no DOF / motion blur / aberration); press F2 for Cinematic.
- Act II market rebuilt from scratch as an Indian bazaar lane (8 m wide, 3-4 storey faded buildings, ~22 stalls,
  shrine, gate arch, cow, scooters, autos, bunting). Bilingual Hindi/English signs are pre-rendered images:
  `swift Tools/make_signs.swift` regenerates `Assets/Resources/Signs/*.png` (correct Devanagari shaping).

## Thriller pass (Nov 2026)
- **One UI language** (`UiKit.cs`): near-black square panels, hairline borders, a single aged-brass accent, bone-white text,
  tracked capitals for labels. The journal in every act is now `UiKit.CaseFile` (numbered, scrollable dossier).
- **Cinema bars** slide in during conversations, document cards and act intros (`UiKit.Letterbox`).
- **Film grade** (`CinematicLook.Apply`): muted saturation, stronger contrast, cooler shadows, heavier vignette and grain,
  slight lens distortion. Default tier is now Cinematic (F2 to step down).
- **Characters** (`CharacterNPC.cs`): normals are re-smoothed at load (no more faceted low-poly shading) and skin, hair,
  eyes, cloth and metal each get their own surface response. (This only applies to the low-poly fallback models now.)
- **Render settings**: MSAA 4x, 4096 shadow maps, shadow distance 40, SSAO high samples / 0.9 intensity, 48 LUT.

## Realistic characters (Nov 2026)
- 14 avatars from the Microsoft Rocketbox library (MIT) live in `Assets/Resources/Characters/RB` (see CREDITS.txt there).
  `NpcStyle.Rb("Male_Adult_15", tint)` picks the avatar; the tint multiplies the clothing texture.
- They ship without animation clips. `CharacterNPC.RbAnimate` poses the skeleton in code every frame:
  arms down from the T-pose, breathing, head tracking, jaw + hand moving with the voice, walk cycle, wave.
- `Assets/Editor/RocketboxImport.cs` sets the import options (normal maps, no Animator).
- Go back to the low-poly set: `CharacterNPC.Realistic = false` (it is also the automatic fallback if a model is missing).
- Children in the market still use the low-poly model (the realistic set here is adults only).
- For a standalone build, the runtime materials need the URP Lit normal-map and alpha-clip shader variants included.

## Real-world assets pass (Nov 2026)
- **Motion capture**: `Assets/Resources/Anim` holds Unity Standard Assets idle + walk clips (male/female). The Rocketbox
  avatars are imported as Humanoid and `CharacterNPC.SetupRbClips` plays the clips, blended by the NPC's measured speed.
  Head look, jaw and the talking / waving hand are layered on top in `RbClips`.
- **Texture pack**: `Assets/Resources/Textures` has photo-scanned Poly Haven materials. `ProcTex.Get(key)` loads
  `{key}_albedo/_normal/_mask` when present and falls back to the procedural texture otherwise, so every existing
  `GameKit.Surface(...)` call picked them up. New keys: `brick`, `shutter`, `corrugated`, `concrete`, `metal`.
- **Vehicles** (`VehicleKit.cs`): bodies are lofted through rounded cross-sections (`Sec`), wheels are lathed.
  The scooter, auto-rickshaw and SUV body in `MarketWorld.cs` use it.
- **Props** (`PropKit.Place`): Poly Haven scans in `Assets/Resources/Props` replace the box clutter in the market.

## Market realism pass (Nov 2026)
- **Scale**: camera FOV 68 -> 56, walking pace 2.2 -> 1.75 m/s, much gentler depth of field and lens distortion
  (wide lens + strong background blur is what makes a scene read as a miniature). Scooters scaled to real size.
- **Stalls**: bamboo frames, sagging two-sided tarpaulins (`VehicleKit.Tarp`), jute drapes, scanned fruit and baskets.
- **Shop fronts**: corrugated tin shades, tube lights over the signs, rain pipes, stone steps, AC units.
- **`MarketWorld.TexturePass`** gives real materials to every part that was still a flat colour (matched by object name).
- **Inspector Reddy's exit**: `MarketWorld.SpawnBike` builds a commuter motorbike; `CharacterNPC.Ride` seats a character
  with two-bone IK (hands on grips, feet on pegs); `MarketStory.ReddyFlees` runs her to it and rides out through the gate.
  A run clip (`*_RunForwards`) blends in above about 2 m/s.

## Temple, cafe and Acts 4-5 pass (Nov 2026)
- **Blur when looking down**: depth of field now only switches on during conversations (`AutoFocus`).
- **Real skies**: `CinematicLook.Sky(cam, "name")` uses a Poly Haven HDRI from `Resources/Sky` as backdrop, ambient light
  and reflections; `NoSky` goes back to a flat background. Used by the cafe (morning) and the Act V terrace (sunrise).
- **Temple (Act III)**: stone sanctum you can look into, turned pillars, lathe-built shikhara with amalaka and brass
  kalash, cast bells, Ganesha shrine, tulsi planter. The idol is loaded from `Resources/Idols` - see README.txt there
  (put your own model in as `Kali`; a museum scan stands in until then).
- **Cafe Irani (Act IV)**: open-air terrace in morning sun with a pergola, scanned tables, chairs, plants and tea sets.
  The title card now reads 7:05 AM. Kitchen: white tiles, terracotta floor, scanned brass / enamel / ceramic vessels.
- **`ActStoryBase.TexturePass`**: scanned materials on every flat-coloured part in Acts III-V, matched by object name.
- New material keys: `sandstone`, `sandstone_floor`, `marble`, `checker`, `terracotta`, `tiles_white`, `cobble`, `blocks`
  (these keep their own colours - pass a white tint).
