# Low-Poly Mannequin H20 Candidate

`MannequinLowPoly_v01.blend` is a reversible geometry-only derivative of the
existing project mannequin:

`Assets/_Project/Art/Prototype/Humanoid/AnimationLibrary_Unity_Standard.fbx`

The Blender build script reduces the original body while retaining its
proportions, separated-joint construction, two material regions, complete
53-bone skinning, and animation contract. The head shell is replaced by a
purpose-built 20-triangle faceted form fitted to the original head bounds.

Runtime geometry:

- 1,972 triangles overall
- Exactly 20 triangles in the replacement head shell
- 1,117 Blender source vertices
- Zero unweighted vertices

The original mannequin mesh remains untouched and is retained in the scene as
a disabled fallback.
