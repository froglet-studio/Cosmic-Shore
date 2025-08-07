## Setup Checklist

### Pre-Setup Validation
- [ ] Run BlendShapeValidator to confirm all 4 blend shapes exist
- [ ] Check that blend shape names match expected patterns
- [ ] Ensure mesh has valid normals and tangents (validator logs warnings if missing)

### Core Setup
- [ ] Create shader file: BlendShapeAnimation.shader
- [ ] Create extractor script: BlendShapeExtractor.cs
- [ ] Create material with BlendShapeAnimation shader
- [ ] Add BlendShapeExtractor to model GameObject
- [ ] Run "Extract and Setup" command

### Testing & Debugging
- [ ] Add BlendShapeDebugger for manual testing
- [ ] Test each blend shape individually using manual weights
- [ ] Verify animation loops seamlessly
- [ ] Check normal preservation at transition points

### Optimization
- [ ] Enable GPU Instancing on material
- [ ] Set texture to Point filter, Clamp wrap
- [ ] Test with multiple instances using BatchProcessor
- [ ] Monitor performance with PerformanceMonitor

### Final Verification
- [ ] Animation runs at target framerate
- [ ] No normal/lighting artifacts
- [ ] Seamless loop with no visible jumps
- [ ] Ready for production use
