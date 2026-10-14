# digitalbreed.SyntyHeadConverter

A standalone Unity editor package for fitting expressive Synty Sidekick heads to POLYGON characters. Open **Tools → digitalbreed → Synty Head Converter**.

Build and compare converted characters in the Unity Editor, then save them as prefabs. Generated characters use ordinary Unity meshes, materials, blendshapes, avatars and animation assets.

## Tool screenshots

Click a screenshot to view it at full resolution.

<table>
  <tr>
    <td width="33%" align="center"><a href="Images~/synty-head-converter-06.png"><img src="Images~/synty-head-converter-06.png" alt="Synty Head Converter screenshot 06" width="100%"></a></td>
    <td width="33%" align="center"><a href="Images~/synty-head-converter-01.png"><img src="Images~/synty-head-converter-01.png" alt="Synty Head Converter screenshot 01" width="100%"></a></td>
    <td width="33%" align="center"><a href="Images~/synty-head-converter-02.png"><img src="Images~/synty-head-converter-02.png" alt="Synty Head Converter screenshot 02" width="100%"></a></td>
  </tr>
  <tr>
    <td width="33%" align="center"><a href="Images~/synty-head-converter-03.png"><img src="Images~/synty-head-converter-03.png" alt="Synty Head Converter screenshot 03" width="100%"></a></td>
    <td width="33%" align="center"><a href="Images~/synty-head-converter-04.png"><img src="Images~/synty-head-converter-04.png" alt="Synty Head Converter screenshot 04" width="100%"></a></td>
    <td width="33%" align="center"><a href="Images~/synty-head-converter-05.png"><img src="Images~/synty-head-converter-05.png" alt="Synty Head Converter screenshot 05" width="100%"></a></td>
  </tr>
</table>

## More Samples

**Before** is on the left and **After** is on the right in each pair. Click an image to view it at full resolution.

<table>
  <tr>
    <td width="33%" align="center">
      <code>SM_Chr_Party_Male_01</code><br>
      <a href="https://syntystore.com/products/polygon-nightclubs">POLYGON Nightclubs</a><br><br>
      <a href="Images~/SM_Chr_Party_Male_01-Before.png"><img src="Images~/SM_Chr_Party_Male_01-Before.png" alt="SM_Chr_Party_Male_01 before conversion" width="48%"></a>
      <a href="Images~/SM_Chr_Party_Male_01-After.png"><img src="Images~/SM_Chr_Party_Male_01-After.png" alt="SM_Chr_Party_Male_01 after conversion" width="48%"></a>
    </td>
    <td width="33%" align="center">
      <code>SM_Chr_Developer_Female_01</code><br>
      <a href="https://syntystore.com/products/polygon-office-pack">POLYGON Office Pack</a><br><br>
      <a href="Images~/SM_Chr_Developer_Female_01-Before.png"><img src="Images~/SM_Chr_Developer_Female_01-Before.png" alt="SM_Chr_Developer_Female_01 before conversion" width="48%"></a>
      <a href="Images~/SM_Chr_Developer_Female_01-After.png"><img src="Images~/SM_Chr_Developer_Female_01-After.png" alt="SM_Chr_Developer_Female_01 after conversion" width="48%"></a>
    </td>
    <td width="33%" align="center">
      <code>SM_Chr_Boss_Female_01</code><br>
      <a href="https://syntystore.com/products/polygon-office-pack">POLYGON Office Pack</a><br><br>
      <a href="Images~/SM_Chr_Boss_Female_01-Before.png"><img src="Images~/SM_Chr_Boss_Female_01-Before.png" alt="SM_Chr_Boss_Female_01 before conversion" width="48%"></a>
      <a href="Images~/SM_Chr_Boss_Female_01-After.png"><img src="Images~/SM_Chr_Boss_Female_01-After.png" alt="SM_Chr_Boss_Female_01 after conversion" width="48%"></a>
    </td>
  </tr>
  <tr>
    <td width="33%" align="center">
      <code>SM_Chr_Salesman_01</code><br>
      <a href="https://syntystore.com/products/polygon-palm-city">POLYGON Palm City</a><br><br>
      <a href="Images~/SM_Chr_Salesman_01-Before.png"><img src="Images~/SM_Chr_Salesman_01-Before.png" alt="SM_Chr_Salesman_01 before conversion" width="48%"></a>
      <a href="Images~/SM_Chr_Salesman_01-After.png"><img src="Images~/SM_Chr_Salesman_01-After.png" alt="SM_Chr_Salesman_01 after conversion" width="48%"></a>
    </td>
    <td width="33%" align="center">
      <code>SM_Chr_Visitor_Male_01</code><br>
      <a href="https://syntystore.com/products/polygon-horror-carnival">POLYGON Horror Carnival</a><br><br>
      <a href="Images~/SM_Chr_Visitor_Male_01-Before.png"><img src="Images~/SM_Chr_Visitor_Male_01-Before.png" alt="SM_Chr_Visitor_Male_01 before conversion" width="48%"></a>
      <a href="Images~/SM_Chr_Visitor_Male_01-After.png"><img src="Images~/SM_Chr_Visitor_Male_01-After.png" alt="SM_Chr_Visitor_Male_01 after conversion" width="48%"></a>
    </td>
    <td width="33%" align="center">
      <code>Character_Mother_01</code><br>
      <a href="https://syntystore.com/products/polygon-town-pack">POLYGON Town Pack</a><br><br>
      <a href="Images~/Character_Mother_01-Before.png"><img src="Images~/Character_Mother_01-Before.png" alt="Character_Mother_01 before conversion" width="48%"></a>
      <a href="Images~/Character_Mother_01-After.png"><img src="Images~/Character_Mother_01-After.png" alt="Character_Mother_01 after conversion" width="48%"></a>
    </td>
  </tr>
</table>

## Installation

**From Git:** In Unity Package Manager, choose **+ → Install package from git URL**, enter the following URL, then click **Install**:

```text
https://github.com/digitalbreed/SyntyHeadConverter.git
```

Git must be installed and available on your system's `PATH`. The package is at the repository root, so no `?path=` suffix is needed. To pin a specific revision, append `#` followed by a tag or commit hash. See [Unity's Git installation instructions](https://docs.unity3d.com/6000.0/Documentation/Manual/upm-ui-giturl.html).

**From disk or archive:** Choose **Install package from disk** and select this package's `package.json`, or install its `.tgz` archive.

The UPM identifier is `digitalbreed.syntyheadconverter`; the C# namespace and product name are `digitalbreed.SyntyHeadConverter`.

Requirements:

- Unity 6. Tested with 6000.3.23f1 and 6000.3.26f1.
- Newtonsoft JSON (`com.unity.nuget.newtonsoft-json` 3.2.1), installed automatically as a package dependency.
- Unity's Animation, IMGUI and Image Conversion modules (1.0.0), installed automatically as package dependencies. The converter does not require Unity's JSON Serialization module.
- Your licensed POLYGON character assets.
- Your licensed Sidekick FBXs containing native facial blendshapes, installed here or available in another project/folder on disk.
- The render pipeline required by your source character materials. The converter preserves those materials. URP integration and built-in-pipeline compilation are tested; HDRP shader selection is provided but has not been integration-tested.

Sidekick meshes already installed in the project, including UPM packages, are discovered automatically when exactly one complete installation is available. Converter caches are excluded. If several installations qualify, choose one in Settings. Parts are always resolved from one installation.

Alternatively, select a `SidekickCharacters` folder (or its `Meshes/Species/Humans` folder) anywhere on disk. This explicitly supports a separate staging project, for example `D:/AssetStaging/Assets/Synty/SidekickCharacters`. That project does not need to be open in Unity, and its assets and import settings are never modified. A selected folder must contain all required parts; missing parts are listed together instead of being borrowed from another installation. Clear the setting to return to automatic discovery.

Only the FBX parts needed by the recipe are copied into a local, Git-ignored cache when compatible imported models are not already available. New copies receive their import metadata before their first, individually targeted import. They preserve the source mesh scale, axes, normals and UV settings, while enabling readable geometry, quads and blendshapes. Donor copies do not import materials or animations and do not depend on an avatar in the staging project. Existing originals and cache entries are never reconfigured, overwritten or explicitly reimported. The package includes no Synty models or vendor texture atlases.

Cache keys include source content, relevant source metadata and the converter import-profile version. Unchanged entries are reused; changed, incomplete or modified entries are left untouched and replaced by new entries. Cache receipts under `Library/SyntyHeadConverter/Inputs` verify the copied FBX and its imported metadata. If those receipts are unavailable, the converter creates a new copy rather than attempting to repair an existing import.

## Convert a character

### Start a conversion

1. Select an original prefab and click **Analyze source / load existing fit**.
2. Set a character name and output folder beneath `Assets`.
3. In **Appearance**, choose the face, native nose and eyebrows, skin and eye colors, lip color, age marks, makeup and facial hair. Use **Lip fullness** to adjust either masculine or feminine heads.
4. Click **Build preview**. Compare the original and converted character before refining the fit.

Automatic analysis measures skull proportions as well as body height, so short characters with relatively large heads retain an appropriate face size. Hair, skin-color sampling and neck-band detection use the same scale and height offset. Use **Source-to-fit scale** and **Source height offset** to adjust the alignment when necessary. Export restores the source's body size, including scale already baked into its mesh.

### Fit the neck and collar

Inspect **Front**, **Side**, **Back** and **Under chin**. The latter looks upward from below the jaw. Drag to orbit and scroll to zoom.

- Adjust **Neck cut height** to choose where the original neck meets the replacement. Choose a level that leaves a continuous neck boundary clear of clothing and attachments.
- Adjust **Neck width** and **Neck depth** until the neck fits inside the collar. Check the sides and back as well as the front.
- Increase **Soften throat** if the throat projects too sharply.
- **Blend collar skin shading** creates a recessed neck transition for a shirt collar and blends the skin material. Leave it off for an exposed neck or a close-fitting choker; it does not correct an intersecting necklace.

Rebuild after changes. Test head turns in both directions: a neckline that looks closed in the neutral pose can still separate during motion. If the build reports that it cannot find a closed neck boundary, adjust the cut or correct the region classifications before continuing.

### Choose smooth or faceted facial shading

**Appearance → Face smoothing** blends from the existing skin normals (0) to smooth facial skin normals (1), including the nose. Intermediate values retain some of the POLYGON faceting. This changes shading only: vertex positions, polygon counts, skin weights and facial expressions remain unchanged. Rebuild the preview after adjusting it.

### Stop the head poking through the hair or headwear

Automatic analysis selects recognizable hair or headwear around the scalp, preferring separate hair meshes. It also enables fitting for integrated hair detected from the source palette. Saved selections remain unchanged. Inspect the back and crown after building; unusual hairstyles may still need a manual choice.

1. In **Fit**, set **Fit scalp beneath** to the relevant hair or headwear object. Choose **Integrated hair / headwear** when it belongs to the main character mesh.
2. Build a preview and inspect the rear scalp, temples and crown.
3. Increase **Hair clearance** gradually if skin still breaks through. Rebuild after each adjustment; excessive clearance can leave a dent beneath an exposed part of the scalp.
4. If the integrated hair is not identified correctly, highlight it in **Regions** and assign **Accessory**, then rebuild.

This fits the scalp beneath the selected surface while retaining the original hair silhouette. Hair surfaces inside the original head are ignored, protecting exposed bald areas from hidden hair-mesh closures. Select the surface covering the affected area; scalp fitting does not reposition glasses or correct unrelated attachments. Review head turns and facial expressions once the neutral fit is correct.

### Preserve accessories and remove duplicate facial details

On **Regions**, select an entry to highlight its original geometry, then choose its role:

| Role | Use |
| --- | --- |
| **Auto** | Use the recipe's automatic classification. |
| **Accessory** | Retain hair or attachments separately from the neck cut. |
| **Preserve** | Retain original surfaces such as clothing. |
| **Necklace** | Separate jewelry and repair the skin beneath it. |
| **Remove** | Discard original facial details or unwanted attachments. |

If two pairs of eyebrows appear, identify and **Remove** the original eyebrow surfaces while keeping the chosen native Sidekick eyebrows. Inspect the highlighted selection first: a region may contain more than the detail you want to remove. Region selections are tied to the source asset's fingerprint.

### Choose a Sidekick beard or moustache

**Sidekick facial hair ID** selects an optional 3D facial-hair mesh. The ID is the exact Sidekick FBX filename without its directory or `.fbx` extension, not a Unity asset GUID or a descriptive name such as "moustache".

1. Search your installed Sidekick assets for `09FCHR`. If you use an external Sidekick folder, look in `Meshes/Species/Humans` beneath that folder.
2. Inspect the models to choose the beard or moustache you want.
3. Copy its filename into **Sidekick facial hair ID**, for example `SK_HUMN_BASE_08_09FCHR_HU01`. Keep the spelling and capitalization; omit `.fbx`.
4. Choose its color with **Eyebrows / facial hair**, then **Build preview** and check the fit while opening the mouth and smiling.

The chosen model must exist in your project's Sidekick assets or the Sidekick folder selected in Settings. Its native expression blendshapes are retained. Leave the field empty to keep the original facial hair; an empty field does not request a clean-shaven face. Use **Regions → Remove** for original facial-hair surfaces you want to discard.

**Scruff** is a separate texture treatment for short stubble. It can be used with or without a beard mesh and does not change the beard's silhouette.

### Keep makeup matched to the complexion

Enable **Appearance → Adapt makeup to skin** to apply pigment and texture relative to the overlay's foundation color. This avoids painting a light foundation over dark skin while retaining blush, eye shadow and skin detail. It also retains age marks beneath the makeup. With the option off, the overlay's original colors are blended directly, including its foundation.

**Makeup base skin** is the plain skin color in the overlay, not the character's complexion. The default (#F4C5A9) matches the bundled Daytime Rose texture. For a custom overlay, choose its unpainted foundation color here. Both this option and Face smoothing are saved in recipes; existing appearances remain unchanged until enabled. Rebuild after changing either setting.

### Keep necklaces outside the skin

Automatic analysis recognizes compact closed neck bands, assigns them **Necklace**, and disables the recessed collar transition. For other jewelry, assign its regions to **Necklace** before adjusting the neck. Rebuild, then use **Neck width** and **Neck depth** to fit the skin inside the chain. Inspect the pendant, sides and rear clasp from below and during head turns.

If a highlighted region includes both jewelry and surrounding skin, do not classify the entire selection as jewelry. Such geometry needs a more precise source separation or advanced fitting recipe. **Preserve** alone retains a necklace's geometry but does not repair its skin underlay.

### Carry over a neck tattoo

Both characters are from [POLYGON Street Racer](https://syntystore.com/products/polygon-street-racer). **Before** is on the left and **After** is on the right in each pair. Click an image to view it at full resolution.

<table>
  <tr>
    <th width="50%"><code>SM_Chr_BoyRacer_Male_01</code></th>
    <th width="50%"><code>SM_Chr_Mechanic_Male_01</code></th>
  </tr>
  <tr>
    <td align="center">
      <a href="Images~/SM_Chr_BoyRacer_Male_01-Before.png"><img src="Images~/SM_Chr_BoyRacer_Male_01-Before.png" alt="SM_Chr_BoyRacer_Male_01 neck tattoo before conversion" width="48%"></a>
      <a href="Images~/SM_Chr_BoyRacer_Male_01-After.png"><img src="Images~/SM_Chr_BoyRacer_Male_01-After.png" alt="SM_Chr_BoyRacer_Male_01 neck tattoo after conversion" width="48%"></a>
    </td>
    <td align="center">
      <a href="Images~/SM_Chr_Mechanic_Male_01-Before.png"><img src="Images~/SM_Chr_Mechanic_Male_01-Before.png" alt="SM_Chr_Mechanic_Male_01 neck tattoo before conversion" width="48%"></a>
      <a href="Images~/SM_Chr_Mechanic_Male_01-After.png"><img src="Images~/SM_Chr_Mechanic_Male_01-After.png" alt="SM_Chr_Mechanic_Male_01 neck tattoo after conversion" width="48%"></a>
    </td>
  </tr>
</table>

First determine whether the tattoo belongs to a texture or to separate geometry, and whether it lies below or across the neck cut.

- **Tattoo on retained skin:** original surfaces normally retain their UVs and materials, so a texture tattoo below the cut can survive the conversion. Choose a cut above the artwork where the neck fit permits, and inspect the result. Scruff composition and skin-underlay repairs can replace affected skin textures or UVs, so check again after enabling those features.
- **Tattoo made from separate geometry:** highlight the patch in **Regions** and try **Preserve**. Verify that it still rests on the skin and deforms with it. This retains the patch; it does not project it onto the replacement neck. Avoid preserving a whole section of the original neck just to keep its tattoo, since this can leave overlapping skin.
- **Tattoo on replaced skin or crossing the cut:** the converter does not automatically transfer it. Finish the head and neck fit, apply the character, then transfer or repaint the artwork onto the final neck's UVs in a texture-painting tool. Use the final mesh and its assigned materials as the reference. Where the neck uses a flat-color material or unsuitable UVs, create a suitable UV layout and texture for those faces first.

For manual texture work, keep the tattoo source, editable painting project and customized material/texture copies in your character's source folder. Preview head turns to check that the artwork crosses the neck transition cleanly. Reapplying a conversion regenerates its output, so keep manual customizations separately and reapply them afterward. A front-face overlay is not a wraparound neck-tattoo map.

### Review, apply and reuse

Test head turns, blinking, mouth opening and smiles. Check hair clearance, glasses, jewelry and the entire neckline in both the neutral and animated views. Automatic validation checks mesh and animation consistency; visual review determines whether the fit suits the character.

Click **Apply to character assets** when satisfied. Reapplying preserves existing asset GUIDs. Save the conversion recipe to reuse the choices and fit with the original source assets.

## Facial textures

### Included textures: provenance and license

The four bundled PNGs in `Editor/Textures` (`MatureSoft.png`, `WeatheredCreases.png`, `DaytimeRose.png` and `FineStubbleTile.png`) were created with OpenAI image generation through the Codex `imagegen` workflow. They are AI-generated resources specifically for this project, not Synty vendor textures. The retained generation notes confirm that workflow but do not record an exact model version, so this package does not claim a verified "ChatGPT Images v2" attribution.

These textures are included under the package's [MIT License](LICENSE.md), to the extent the publisher holds rights in them. You may use, modify and redistribute them in free or commercial projects under that license, retaining its copyright and permission notice. The package charges no texture royalties.

OpenAI's terms explain the upstream ownership of generated output: between the user and OpenAI, the user owns the output to the extent permitted by applicable law, and OpenAI assigns its rights, if any, to the user. See **Content → Ownership of content** in the official [OpenAI Terms of Use](https://openai.com/policies/terms-of-use/#content), or the [Europe Terms of Use](https://openai.com/policies/eu-terms-of-use/#content) for the EEA, Switzerland and UK. Links checked on 2026-09-27.

Those are service terms governing generation, not a separate stock-texture license or a guarantee of exclusive rights. OpenAI notes that similar outputs may be generated for others. The package's MIT permission covers the rights its publisher can grant; it does not override third-party rights or the licenses of Synty models used with these textures. When generating your own overlays, follow the OpenAI agreement applicable to your account and ensure you have permission to use any reference images.

### Choose and combine treatments

The converter combines the **Skin** and **Lips** colors with transparent facial overlays. **Age texture strength** and **Makeup** control their contribution. Overlay colors are blended into the skin, so a strongly colored overlay can also change the apparent complexion.

| Treatment | Texture | Control |
| --- | --- | --- |
| Subtle aging and skin variation | `MatureSoft.png` | **Age texture → Mature soft**, then **Age texture strength** |
| More pronounced age lines | `WeatheredCreases.png` | **Age texture → Weathered creases**, then **Age texture strength** |
| Rose-toned makeup | `DaytimeRose.png` | **Makeup** |
| Short hair stubble | `FineStubbleTile.png` | **Scruff**, then **Scruff visibility** |

Age and makeup can be combined. Start with low strengths, build a preview, then inspect the forehead, eye corners, cheeks and mouth. Set lip color independently with **Lips**. Scruff covers the lower face, jaw and upper neck; **Eyebrows / facial hair** controls mesh-based facial hair rather than recoloring the stubble texture.

Character outputs include native `Models/FaceDetail.asset` (1024 × 1024), and `Models/JawWrap.asset` (2048 × 2048) when scruff is enabled. These are uncompressed sRGB `Texture2D` assets with mipmaps; the latter maps around the jaw and upper neck. Pixel data is regenerated without changing texture importers or refreshing the project. Repeated Apply updates the existing native textures and preserves their GUIDs.

Composed `FaceDetail.png` and `JawWrap.png` files remain in each local job's `authoring/unity` directory for image editing or export; use **Open local job folder** to find them. They are outside `Assets` and are not imported. When applying to an older PNG-based output, the converter creates native textures and updates the generated materials; existing PNGs and their metadata are retained unchanged.

### Prepare a custom overlay

Use an existing PNG in `Editor/Textures` as the layout reference. Age and makeup images are front-face color overlays with transparency, rather than full head UV atlases. Preserve the reference's canvas, feature positions, transparent openings and soft boundaries. Painting a portrait or using an arbitrary facial UV map will misalign the result.

For age marks, include subtle skin variation between the creases so the face does not look like smooth skin with isolated lines drawn on it. Keep lighting neutral: strong highlights and cast shadows become permanent painted features. Leave eyes, brows, lips and other openings transparent according to the reference. Export a true RGBA PNG; a painted checkerboard is not transparency.

Stubble uses a different layout: `FineStubbleTile.png` is a repeating transparent tile of short hairs. A replacement should tile without visible borders and use a similar hair size and density. Do not use the front-face overlay layout for this texture.

### Sample image-generation prompt

Attach `MatureSoft.png` as the reference image and adapt the age, complexion and details in this prompt:

> Edit the attached transparent facial texture overlay for a stylized low-poly game character aged about 55. Preserve the exact canvas dimensions, layout, feature positions and transparent openings of the reference. Keep its forehead, outer-eye, cheek and chin regions aligned and preserve the soft transparent boundaries. Add restrained forehead creases, crow's feet, subtle pores, a few faint age spots and gentle uneven pigmentation across the skin regions. Use a lightly weathered neutral complexion with fine, readable detail and natural variation between wrinkles. Use even, diffuse color without directional lighting, cast shadows or specular highlights. Leave the reference's eye, eyebrow, nose and mouth openings transparent; do not add eyes, lips, hair, teeth, a head silhouette or a background. Return a flat RGBA PNG texture overlay with real transparency, not a portrait, render or checkerboard background.

Inspect the generated alpha channel and alignment before using the image. Image generation may shift details despite the prompt; correct the layout in an image editor and test it on the actual character.

### Use a custom overlay in the converter

Import your overlay into your Unity project, then select it in **Appearance**:

| Picker | Expected image | Strength |
| --- | --- | --- |
| **Custom age texture** | Transparent front-face overlay using the age-mark layout | **Age texture strength** |
| **Custom makeup texture** | Transparent front-face overlay using the makeup layout | **Makeup** |
| **Custom scruff texture** | Seamless tile of short hairs on transparency | **Scruff visibility**, with **Scruff** enabled |

1. Store the image in a source folder under `Assets`. For color overlays, use the **Default** texture type, enable **sRGB (Color Texture)**, and retain the source alpha channel. RGBA PNG is recommended. **Read/Write** can remain disabled.
2. Drag the imported texture onto the appropriate picker, or use its object-selection button. A custom age texture overrides the bundled **Age texture** choice. Set the treatment's strength above zero.
3. **Build preview** and review its alignment, complexion and transparency from several angles and during expressions. The converter uses the imported image, including its imported resolution and compression; increase **Max Size** or reduce compression if fine detail is lost.
4. Save the recipe and apply when satisfied. Texture references are stored in the recipe; the output contains composed textures ready for use by the character.

Clear a picker to return to the bundled texture. Editing or reimporting a selected texture invalidates the existing preview; rebuild before applying. Keep custom source images and their `.meta` files alongside your recipes in version control so the characters can be rebuilt. No package modifications are needed.

## Build and validation

The native pipeline reads exact source bind coordinates, retains body weights and materials, cuts the donor along its anatomical neck boundary, and connects both neck rings. It preserves native expression deltas and transfers facial movement to retained beards. Source clothing is split consistently at the cut plane to avoid deforming T-junctions.

Validation covers finite mesh data, bone mapping, original body alignment, closed graft edges after fitting, a valid Humanoid avatar, 145 facial/head-motion samples, animated seam continuity, and actual skinned head movement at ±35°. Source changes and recipe edits invalidate old previews. Capture, construction and rendering use preview scenes; conversion does not switch or save the user's scene.

An applied result contains its prefab, facial-demo prefab/controller/clip, mesh, avatar, materials, composed textures and conversion recipe. Rebuild from the recipe and the original licensed inputs. Those inputs remain separate from this package.

## Local files and version control

- Final output and compact recipes: the chosen output folder.
- Imported input parts and preview assets: `Assets/SyntyHeadConverterCache`.
- Logs, validation evidence and rollback copies: `Library/SyntyHeadConverter`.

The converter adds specific cache exclusions to the project's `.gitignore`. It does not commit or upload files. Only final assets, recipes and required source inputs should be versioned; caches and rollback copies can be discarded when no build is running.

## Optional project integration

Register `HeadConversionExtensions` from an `[InitializeOnLoad]` class in your project's editor assembly. Callbacks run synchronously on Unity's editor main thread.

| Callback | When it runs | Contract |
| --- | --- | --- |
| `ConfigureRecipe` | During source analysis, after a new or saved recipe is selected, before automatic defaults and regions are finalized | Modify the in-memory recipe to supply defaults; preserve saved choices. Exceptions abort analysis. Skipped when `HeadSource.Suggest` receives `reuseExistingFit: false`. |
| `AdditionalApplyFiles` | Before Apply writes assets | Return project-relative files/directories beneath `Assets` that your integration may change. These are backed up with the output folder. Exceptions abort Apply. |
| `AfterApply` | After the converted prefab and compact recipe have been saved | Receives the recipe and saved prefab asset. Update and save dependent assets. Runs on every Apply, so updates must be repeatable. Exceptions trigger rollback of registered paths. |

Use `+=` for `ConfigureRecipe` and `AfterApply`; unsubscribe temporary handlers when their owner closes. `AdditionalApplyFiles` has one provider: assign with `=` and aggregate paths there if several integrations need rollback coverage. Include directories where `AfterApply` may create new assets. Existing file contents and metadata are restored after failure; newly created files within registered paths are removed. Unlisted files and in-memory scene changes are not covered.

Host callbacks execute their own code and remain responsible for their import behavior. The converter does not register asset postprocessors or perform project-wide refreshes, but an optional project's `AfterApply` handler can still call other tooling that does so.

Callbacks must not start another conversion, switch scenes or edit the original prefab. Apply hooks do not run for previews. XML documentation on each callback provides further lifecycle details.

## Package source guide

Every C# file starts with a concise purpose statement.

| File under `Editor/` | Responsibility |
| --- | --- |
| `AssemblyInfo.cs` | Grants the check assembly access to internal helpers. |
| `HeadConverterWindow.cs` | Window controls, recipe editing and comparison UI. |
| `HeadPreview.cs` | Isolated rendering, expression playback and region highlighting. |
| `HeadConversionRecipe.cs` | Serialized settings, documented defaults and region overrides. |
| `HeadConversionPipeline.cs` | Build/apply lifecycle, freshness checks and rollback. |
| `HeadConversionExtensions.cs` | Project-integration callback contracts. |
| `HeadSource.cs` | Source bind-pose capture, region classification and fit suggestions. |
| `SidekickAssets.cs` | Installed/external donor discovery, immutable copy reuse and native mesh/blendshape extraction. |
| `SidekickImportProfile.cs` | Metadata preparation before a new donor's first import and content-based cache keys. |
| `HeadAssetFolders.cs` | Native creation of new asset folders without a project refresh. |
| `HeadAssetTransaction.cs` | File backups and in-memory native-asset restoration after a failed Apply. |
| `NativeSurface.cs` | Attributed triangles, clipping, boundary loops and normal smoothing. |
| `NativeHeadBuilder.cs` | Source retention and donor-head assembly. |
| `NativeHeadBuilder.Fitting.cs` | Neck joins, hidden-boundary repair and jewelry separation. |
| `NativeHeadBuilder.Adjustments.cs` | Scalp/accessory fitting, throat shaping and lip fullness. |
| `FaceTextures.cs` | Facial overlay composition and cylindrical scruff mapping. |
| `HeadShaders.cs` | Render-pipeline-compatible shader selection. |
| `HeadAssetBuilder.cs` | Asset export, stable updates and deformation validation. |
| `HeadAssetBuilder.Mesh.cs` | Bone mapping, mesh assembly and material construction. |
| `HeadAssetBuilder.Animation.cs` | Humanoid avatar and facial-demo animation. |
| `RecipeOptions.cs` | Typed access to optional advanced JSON values through Newtonsoft `JObject`. |
| `HeadConverterJson.cs` | Newtonsoft serialization of converter fields, Unity value types and asset references. |

`Tests/Editor/HeadConverterIntegrationChecks.cs` supplies the opt-in checks below. The editor `.asmdef` limits the converter to Unity's editor; the test `.asmdef` separates fixture checks. `package.json` declares identity, Unity compatibility and dependencies. `.editorconfig` specifies C# layout conventions. `CHANGELOG.md` records changes. `Editor/Textures` contains the four overlays described in **Facial textures**. Unity `.meta` files preserve asset identity and import settings.

### JSON and serialized settings

Saved `.asset` recipes continue to use Unity's native asset serialization. All JSON uses Newtonsoft: editor-session recipe snapshots, preview freshness comparisons, neck diagnostics, test fixtures and advanced options. `HeadConverterJson` serializes public instance fields, preserving the existing recipe names and vector (`x`, `y`, `z`) and color (`r`, `g`, `b`, `a`) layouts without traversing Unity's computed properties. Partial updates retain missing fields and replace supplied arrays rather than appending to them.

JSON recipe snapshots store persistent Unity asset references by GUID and local file ID, preserving prefab and custom-texture references across editor reloads and asset moves within a project. Older session snapshots containing instance IDs remain readable while those objects are available in the editor. Nonpersistent references use instance IDs and are valid only while those objects remain available. Missing assets resolve to null. This does not change existing `.asset` recipes or advanced fitting JSON.

`RecipeOptions` is an accessor layer, not a parser. Missing properties use the accessor's default or an empty array. Malformed JSON, non-object roots and duplicate property names are rejected. Existing field names and advanced recipe layouts are retained.

RGB defaults in `HeadFitSettings` contain three 0..255 channels. Comments document each default color, scale and coordinate unit. The paired facial contour tables in `FaceTextures` are calibrated samples in canonical fitting meters; they define tint coverage rather than facial geometry. Keep their sample indices aligned when editing them.

## Integration checks

These checks launch a separate Unity editor process, convert fixture characters, apply their results twice, render comparisons and exit with a success or failure code. They are run through `-executeMethod`, not the Unity Test Runner window.

### 1. Prepare a disposable project

1. Create a separate Unity 6 project, for example `D:/UnityTests/SyntyHeadConverterChecks`. Use the render pipeline required by your character materials; for URP materials, use a URP template with its pipeline asset assigned. Open it once to finish package resolution and initial imports.
2. Install this converter package through Package Manager, using its `package.json` or `.tgz` archive. Both the editor assembly and the included check assembly must compile without errors.
3. Import the licensed POLYGON prefabs you want to test, together with their models, materials and textures. Preserve their asset paths and `.meta` files if copying dependencies from another project. A prefab alone is insufficient.
4. Install the licensed Sidekick assets in the test project, or have their `SidekickCharacters` directory available on disk.
5. Close this test project in Unity before starting the batch command. Another project may remain open. The command must point to the disposable project, not your working game project: checks create and modify assets and use a temporary scene.

### 2. Create the fixture file

Save UTF-8 JSON at `<test-project>/Library/SyntyHeadConverterChecks.fixture.json`. Create the `Library` directory if needed. There must be at least one entry, with a unique alphanumeric `id` starting with a letter (underscores are allowed).

The following example uses the POLYGON Office security character. Replace its prefab path and fitting values for a different character, and set `sidekickRoot` to your Sidekick folder:

```json
{
  "sidekickRoot": "D:/SyntyAssets/SidekickCharacters",
  "entries": [
    {
      "id": "Security",
      "prefab": "Assets/Synty/PolygonOffice/Prefabs/Characters/SM_Chr_Security_Male_01.prefab",
      "fittingJson": "{\"canonicalScale\":1,\"canonicalOffset\":0,\"preserveSourceWeights\":true,\"skinUvRegions\":[[0,0.04,0.15,0.22]],\"accessories\":[33,40,44],\"scalpHairEnvelope\":\"embedded\"}",
      "scruff": true,
      "customTextures": true
    }
  ]
}
```

| Field | Meaning |
| --- | --- |
| `sidekickRoot` | Absolute path to `SidekickCharacters` or `Meshes/Species/Humans`, including inside another staging project. Use an empty string to discover one complete installation already imported in the project. |
| `id` | Test-case name, up to 64 characters; used in output paths and log markers. |
| `prefab` | Original prefab's Unity asset path inside this test project, starting with `Assets/`. |
| `fittingJson` | Optional JSON **string** containing advanced fitting values and `HeadFitSettings` fields. It is not a filename or a serialized `HeadConversionRecipe` asset. Omit it or use an empty string for automatic fitting defaults. Nonempty overrides reset collar blending and throat softening to zero unless supplied explicitly. |
| `scruff` | Enables the bundled scruff treatment for this case. Defaults to false. |
| `expectedFemale` | Optional expected automatic face selection: 0 masculine, 1 feminine. Omit to skip. |
| `verifyProportionFit` | Checks the automatic child-proportion fit on the licensed kid fixtures, without legacy fitting overrides. |
| `expectedScalpShell` | Optional name of the separate hair mesh that automatic analysis should select. Also checks that fitting preserves its vertex positions. |
| `expectedSkinRgb` | Optional three-channel RGB array (0–255). Checks automatic skin sampling against this color, allowing one unit per channel. |
| `expectedNecklaceRegions` | Optional expected count of automatically classified necklace regions; also checks retained necklace vertex positions when greater than zero. Omit to skip. |
| `verifyPatternedCollar` | Patterned-collar regression for `SM_Chr_70s_Male_01`: verifies its raised shirt collar retains the original vertex positions across texture seams. Defaults to false. |
| `verifyRaisedCollar` | Cowgirl-specific regression: verifies the raised red jacket collar vertices retain their source positions. Enable only for `Character_Cowgirl_01` with its original palette. Defaults to false. |
| `customTextures` | Generates small test overlays and checks their color, alpha, saved references and change detection. Overrides age/makeup strengths and enables scruff for that case. Defaults to false. |

Use forward slashes in paths, or escape backslashes as `\\` in JSON. This fixture is local to the test project and is removed if you delete `Library`; keep a separate copy if you need to recreate the project. Add further entries to cover different heads, hair, collars and necklaces. Automatic fitting still requires visual review; a difficult character may need fitting overrides before it can pass.

### 3. Run Unity from PowerShell

Adjust the Unity executable and project paths below. Run from any working directory; the fixture and asset paths are resolved within `-projectPath`.

```powershell
$unityExecutable = 'C:/Program Files/Unity/Hub/Editor/6000.3.23f1/Editor/Unity.exe'
$testProject = 'D:/UnityTests/SyntyHeadConverterChecks'
$logDirectory = Join-Path $testProject 'Logs'
New-Item -ItemType Directory -Path $logDirectory -Force | Out-Null
$checkLog = Join-Path $logDirectory 'SyntyHeadConverterChecks.log'

$unityArguments = @(
    '-batchmode'
    '-projectPath'
    ('"{0}"' -f $testProject)
    '-syntHeadChecks'
    '-executeMethod'
    'digitalbreed.SyntyHeadConverter.Tests.HeadConverterIntegrationChecks.Run'
    '-logFile'
    ('"{0}"' -f $checkLog)
)

$unityProcess = Start-Process -FilePath $unityExecutable `
    -ArgumentList $unityArguments -WindowStyle Hidden -Wait -PassThru

Select-String -Path $checkLog -Pattern 'SYNTY_|error CS|Exception'
if ($unityProcess.ExitCode -ne 0 -or
    -not (Select-String -Path $checkLog -Pattern 'SYNTY_NATIVE_ALL_CHECKS_PASS' -Quiet)) {
    throw "Converter checks failed or did not finish. See $checkLog"
}
Write-Host "Converter checks passed. See $checkLog"
```

`-syntHeadChecks` explicitly enables this destructive test fixture. Do **not** add `-quit`: conversion advances on editor update callbacks, and the entry method returns before the work finishes. The runner calls `EditorApplication.Exit` itself. Do **not** add `-nographics`: comparison rendering and custom-texture readback need a graphics device. Unity must have a valid editor license on the machine.

The command waits until Unity exits. To watch progress in another PowerShell window, run `Get-Content '<path-to-log>' -Tail 30 -Wait`. First imports can take longer than subsequent runs; each conversion has a five-minute timeout once its build starts.

### 4. Check the results

A complete successful run returns exit code **0** and writes:

```text
SYNTY_RECIPE_JSON_CHECK_PASS
SYNTY_JSON_SERIALIZATION_CHECK_PASS
SYNTY_RECIPE_REFERENCES_CHECK_PASS
SYNTY_DONOR_PROFILE_CHECK_PASS
SYNTY_DONOR_REUSE_CHECK_PASS
SYNTY_NATIVE_TEXTURE_CHECK_PASS
SYNTY_APPLY_ROLLBACK_CHECK_PASS
SYNTY_CUSTOM_TEXTURE_CHECK_PASS
SYNTY_NATIVE_CHECK_PASS Security
SYNTY_NATIVE_ALL_CHECKS_PASS
```

The custom-texture marker appears only for entries enabling that check; each successful character gets its own `SYNTY_NATIVE_CHECK_PASS <id>`. A failed check exits with code **1** and logs its exception. Compilation, licensing or package-resolution failures may stop Unity before the checks start; inspect the full log when the final success marker is absent.

Outputs are inside the disposable project:

| Location | Contents |
| --- | --- |
| `Logs/SyntyHeadConverterChecks.log` | Full Unity log from the command above. |
| `Library/SyntyHeadConverterEvidence/<id>/` | Original/converted front, side, back, under-chin and head-turn PNGs, plus close neck views from above, below and during a head turn, and `validation.txt`. These are replaced on a later run with the same ID. |
| `Assets/ConverterCheckResults/<id>_<suffix>/` | Applied prefabs, meshes, materials, animations and saved recipes. Each run creates a fresh output folder. |
| `Assets/ConverterCheckInputs/` | Generated custom-overlay test images, when enabled. |
| `Assets/SyntyHeadConverterCache/` | Imported donor parts and preview assets. |
| `Library/SyntyHeadConverter/` | Per-build reports, composed textures and rollback data. |

Review the comparison PNGs as well as the numeric result. Checks cover cancellation, preview/apply separation, recipe freshness, scene preservation, native conversion, facial motion, animated seam continuity, compact recipes and GUID stability on repeated Apply. Custom-overlay checks additionally verify composition, stubble coverage, retained asset references, unchanged Read/Write settings and preview invalidation after image changes.

Donor checks cover metadata preservation, content-based cache invalidation, unchanged original FBXs/metadata and cache reuse on a second read. Native-texture checks compare output pixels to the composed PNGs and verify dimensions, sRGB, mipmaps and stable GUIDs. A deliberately failing Apply hook checks restoration of texture data and removal of newly created assets without a project refresh. These Unity integration checks remain opt-in; filesystem/metadata checks can also run without launching Unity.

## Authorship and third-party assets

The editor code is provided by digitalbreed; the included age, makeup and stubble overlays are AI-generated resources covered by the texture licensing note above. Synty Studios and its product names identify compatible third-party assets; this package is independent of Synty Studios. Each user supplies their own licensed source models. Generated character distribution remains subject to the source assets' terms.

## License

This package is licensed under the [MIT License](LICENSE.md). Third-party Synty assets retain their own license terms.
