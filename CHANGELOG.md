# Changelog

## 0.2.0 - 2026-10-19

- Give retained lower-neck skin the head's shading independently of collar fitting, preserving source atlases, UVs and tattoos while leaving clothing and hair materials unchanged. Keep retained artwork out of scruff texture replacement.
- Add explicit tattoo-island material selection and native mesh/material repair helpers, with checks for albedo, garment, skinning and facial-expression preservation.
- Rebuild live index buffers after replacing native meshes and separating submeshes, including rollback. Fix stale draw ranges that rendered neck, face and eyebrow triangles with incorrect materials; add GPU index-buffer checks.
- Clarify that the collar-inset control changes geometry independently of skin shading, and document retained tattoo-island selection.

## 0.1.0

- Support explicit Sidekick folders in separate staging projects and package-aware installed-asset discovery. Resolve one complete installation and exclude converter caches.
- Prepare metadata before individually importing new donor copies; reuse immutable content/profile-keyed copies without reconfiguring or reimporting originals or existing cache entries.
- Save generated textures as native assets with stable GUIDs, retaining composed PNGs in local job folders. Remove project-wide refreshes and texture reimports from build and rollback.
- Add donor-source preservation, cache reuse, native-texture and failed-apply rollback checks.
- Use Newtonsoft for all converter JSON, preserving field layouts and reading legacy editor-session instance IDs. Store persistent recipe asset references by GUID and local file ID.
- Add JSON round-trip, partial-update, array-replacement and recipe-reference checks.
- Declare the required Unity Animation, IMGUI and Image Conversion modules; remove the converter's use of Unity's JSON Serialization module.

- Exclude flared shirt collars from necklace underlay generation; add close neck inspection renders above, below and during a head turn.

- Fit disproportionate heads from skull measurements, applying height offsets consistently during source analysis.
- Recognize feminine names without case sensitivity, including Schoolgirl.

- Protect exposed bald scalp from hair-mesh closures inside the original head.

- Add optional facial normal smoothing and skin-tone-aware makeup with a configurable foundation reference.
- Clarify the age texture strength control.

- Handle inward collar notches by ordering the generated inset ring while preserving the original boundary.

- Select scalp-fitting hair/headwear automatically during source analysis; preserve explicit saved selections.
- Add crown/rear head-turn renders and original-hair preservation checks.

- Preserve connected garments across texture seams and match collar neck rings by angle to prevent twisted joins.

- Sample facial skin by surface area so detailed lipstick meshes cannot dominate the automatic skin color.
- Separate compact closed neck bands automatically and require neck boundaries to surround the neck axis.

- Preserve raised garment collars during head removal and keep neck-strip triangle winding consistent.
- Add oblique neck and head-turn comparison views, plus a raised-collar regression check.

- Documented source responsibilities, fitting defaults and integration callback contracts.
- Descriptive local names and reusable loop tables.
- Advanced recipe JSON uses the Unity Newtonsoft dependency.

- Standalone `digitalbreed.SyntyHeadConverter` editor package.
- Native Unity head fitting, expression transfer and face-texture composition.
- Reusable recipes and source-region overrides.
- Custom age, makeup and scruff texture pickers with saved recipe references and preview invalidation on texture changes.
- Lip fullness for masculine and feminine heads.
- Connected neck fitting, jewelry separation, scalp clearance and native facial-part selection.
- Original/converted animated comparison with corrected vertical orbit and under-chin camera.
- Stable asset updates, local rollback and optional host-project integration callbacks.
- No Blender, Python or game runtime dependency.
