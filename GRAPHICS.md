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
