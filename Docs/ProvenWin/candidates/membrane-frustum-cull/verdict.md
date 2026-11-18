### membrane-frustum-cull: CapsuleMembrane draws each camera only the capsules its frustum can touch (order kept, conservative spheres) -> **FAIL**

| API | GPU (FrameTimingManager) A ms | B ms | B lower in | Screen px different |
|---|---|---|---|---|
| editor | 1.8514 | 1.6471 | 87 % of frames | 0 |
| editor state_centre | 1.4408 | 1.2677 | 87 % of frames | 0 |

Reasons:
- editor: 'GPU (FrameTimingManager)' not categorically lower (B lower in 87 % of frames)
