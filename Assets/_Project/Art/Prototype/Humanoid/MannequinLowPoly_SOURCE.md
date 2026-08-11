# Mannequin Low-Poly H20

This FBX is generated from the existing project mannequin by:

`Tools/Blender/build_low_poly_mannequin.py`

Its editable Blender source is:

`ArtSource/Characters/MannequinLowPoly/MannequinLowPoly_v01.blend`

The candidate changes geometry only. Unity maps its 53 skin bones onto the
unchanged V67 playable rig and regenerates bind poses against those exact
transforms. The original mannequin renderer remains present as a disabled,
immediate fallback.

- Total triangles: 1,972
- Head-shell triangles: 20
- Materials: one neutral charcoal-gray Player material across both submeshes
