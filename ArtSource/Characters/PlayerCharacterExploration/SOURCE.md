# Player Character Exploration

`PlayerCharacterExploration_v01.blend` is an isolated modeling workspace copied
from the exact seamless low-poly mannequin currently visible on the player:

`ArtSource/Characters/MannequinSeamlessLowPoly/MannequinSeamlessLowPoly_v01.blend`

The starting model is unchanged:

- `MannequinSeamlessLowPoly_Renderer`
- 1,300 vertices
- 2,596 triangles
- One connected surface
- The existing 53-bone playable rig
- Neutral starting pose with no animation assigned

The older H20 mesh remains in the Blender file as a hidden fallback. This
workspace lives outside Unity's `Assets` folder, so editing it does not change
the game. A future model candidate must be deliberately exported and validated
before it can replace the current player renderer.

Rebuild the workspace from the current source with:

```powershell
& 'C:\Program Files\Blender Foundation\Blender 4.4\blender.exe' `
  --background `
  --python Tools/Blender/create_player_character_exploration.py
```

## Equal-triangle experiment

`PlayerCharacterExploration_v02_EqualTriangles.blend` is a reversible
retopology experiment derived from `v01`. It does not replace or modify the
baseline file.

The candidate uses a uniform voxel surface, projects its vertices back onto the
existing character surface, permanently triangulates every face, and transfers
the existing skin weights. The playable skeleton is fingerprinted before and
after the operation and must remain identical.

Validated candidate:

- 3,268 triangles
- 1,636 vertices
- 100% triangle faces
- One connected surface
- Zero unweighted vertices
- Four bone influences per vertex maximum
- Original 53-bone skeleton unchanged
- Skeleton hidden by default in the Blender viewport
- Not integrated into Unity

Compared with `v01`, the middle 80% triangle-area ratio improved from `6.96x`
to `3.82x`, and the middle 80% edge-length ratio improved from `4.40x` to
`2.45x`. Local variation remains around the hands, neck, underarms, pelvis, and
joint transitions where smaller triangles help preserve the silhouette.

Rebuild the candidate with:

```powershell
& 'C:\Program Files\Blender Foundation\Blender 4.4\blender.exe' `
  --background `
  --python Tools/Blender/build_equal_triangle_character_candidate.py
```

## Reference-head study

`PlayerCharacterExploration_v03_ReferenceHead.blend` focuses only on matching
the supplied faceted-head reference. The visible head is a separate,
iteration-friendly study object over the unchanged v02 body.

This ring-built v03 study was rejected after creator review because its uniform
horizontal levels, flat cap, and extended lower shell read as a helmet rather
than a proportioned head. It remains only as reversible iteration history.

The head uses six deliberately shaped octagonal contour rings:

- 96 permanent triangles
- 50 vertices
- Broad, chamfered crown
- Planar front and side regions
- Wider lower-face ring forming a sharp jaw
- Short chin transition into the existing neck
- No ears
- Zero unweighted vertices
- Two bone influences per vertex maximum
- Existing `DEF-head` and `DEF-neck` groups only
- Original 53-bone skeleton fingerprint-verified unchanged

The original body geometry is unchanged. A viewport Mask modifier hides its old
head for this study, while the new head overlaps the neck internally. This is a
Blender exploration candidate, not a Unity-ready replacement mesh.

Rebuild the head study with:

```powershell
& 'C:\Program Files\Blender Foundation\Blender 4.4\blender.exe' `
  --background `
  --python Tools/Blender/build_reference_head_candidate.py
```

## Six-view reference head

`PlayerCharacterExploration_v04_SixViewHead.blend` replaces the rejected ring
construction with a bilaterally aligned, reshaped level-2 icosphere topology.
It was fitted against the supplied front, front-three-quarter, profile, rear,
and rear-three-quarter views.

Validated head:

- 42 vertices
- 80 permanent triangles
- Approximately `0.193 × 0.254 × 0.300 m`
- Domed rear cranium with a shallow chamfered crown
- Distinct forward facial shield
- Lifted rear jaw and occipital-to-neck transition
- Short squared chin plane
- No ears
- Zero unweighted vertices
- Two bone influences per vertex maximum
- Existing `DEF-head` and `DEF-neck` groups only
- Original 53-bone skeleton fingerprint-verified unchanged

The v02 body geometry is unchanged. Its old head is hidden with a viewport Mask
modifier while the new head overlaps the existing neck. The head is built in
the playable rig's canonical local `-Y` facing direction and shares the body's
existing display transform.

Rebuild the six-view candidate and all six orthographic validation renders
with:

```powershell
& 'C:\Program Files\Blender Foundation\Blender 4.4\blender.exe' `
  --background `
  --python Tools/Blender/build_six_view_reference_head_candidate.py
```

## Body-proportioned head placement

`PlayerCharacterExploration_v05_ProportionedHead.blend` preserves the v04 head
shape and topology while fitting it to the character's slimmer body.

Changes from v04:

- Head scale: `0.86 × 0.86 × 0.88`
- Final head size: approximately `0.166 × 0.219 × 0.264 m`
- Scale anchored at the chin/neck junction
- Shifted `8 mm` forward in canonical local space
- Lowered `15 mm`
- Old head and stray neck fragments masked
- A 32-triangle tapered neck connector overlaps the shoulders and head
- Connector uses only the existing `DEF-neck` and `DEF-head` bones

The head remains 42 vertices and 80 triangles. The body geometry and 53-bone
skeleton are unchanged, and this candidate is not integrated into Unity.

Rebuild with:

```powershell
& 'C:\Program Files\Blender Foundation\Blender 4.4\blender.exe' `
  --background `
  --python Tools/Blender/refine_six_view_head_proportions.py
```
