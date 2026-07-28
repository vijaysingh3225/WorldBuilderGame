# Seamless Low-Poly Mannequin Candidate

`MannequinSeamlessLowPoly_v01.blend` is derived from the accepted reversible
H20 mannequin in:

`ArtSource/Characters/MannequinLowPoly/MannequinLowPoly_v01.blend`

The build performs a voxel union over the segmented body, removes tiny internal
fragments, relaxes the joint transitions, reduces the result, and transfers
the original 53-bone skin weights back onto the unified surface.

Validated source geometry:

- 2,596 triangles
- 1,300 Blender vertices
- One connected surface component
- Zero unweighted vertices
- Four bone influences per vertex maximum

The original segmented mannequin and the accepted H20 reduction remain
unchanged as disabled fallbacks in Unity.
