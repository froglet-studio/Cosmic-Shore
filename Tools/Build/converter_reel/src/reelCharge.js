// The Charge light in the reel, as OmniCombinePlayer drives it:
//   WriteSeamWalk  the waiting Charge crystal's seam walk, carried on the crystal's rigid frame (CrystalFrame), at
//                  strength fadeIn * effect * (1 - smooth(maxMorph / 0.05)), so it is gone before a petal morphs
//   WriteFlux      the woken omni's chained flux arcs, from the moment the omni is whole, scaled by its wake fx
//   StrikeTriangles  a raised triangle prism an arc passes through lights up (the gated FluxStrikes)
// Both layers wear OmniChargeArcs.mat. The walker is stateful (its legs are drawn as it advances), so the reel keeps one
// walker per idle stretch and rebuilds it when the clock goes backwards (a scrub) or restarts (the loop's cut).
(function (CR) {
  'use strict';
  const { CHARGE, MASS, Core } = CR.Combine;
  const { smooth } = CR.Combine.maths;
  const CA = CR.ChargeArcs, DP = CR.DepthPrisms;

  Object.assign(CR.ExtrasLook.defaults, {
    arcColor: '#7cfc00',     // OmniChargeArcs _BrightColor
    arcCore: '#d9f2ff',      // _ArcCoreColor (0.85, 0.95, 1.0)
    boltGain: 1,             // _BoltIntensity
    glowGain: 1,             // _GlowIntensity
  });
  CR.ExtrasLook.spec.push(
    ['Charge light', 'arcColor', null, null, null, 'Glow colour', 'OmniChargeArcs _BrightColor: the plasma\'s outer glow and the lit petals.', 'color'],
    ['Charge light', 'arcCore', null, null, null, 'Core colour', 'OmniChargeArcs _ArcCoreColor: the white-hot middle of a bolt.', 'color'],
    ['Charge light', 'boltGain', 0, 4, 0.05, 'Bolt intensity', 'OmniChargeArcs _BoltIntensity.'],
    ['Charge light', 'glowGain', 0, 4, 0.05, 'Petal glow', 'OmniChargeArcs _GlowIntensity: how brightly the lit pentagon faces glow.'],
  );

  const rowMajorAxisAngle = (axis, ang) => {
    const [x, y, z] = axis, c = Math.cos(ang), s = Math.sin(ang), t = 1 - c;
    return [t * x * x + c, t * x * y - s * z, t * x * z + s * y, t * x * y + s * z, t * y * y + c, t * y * z - s * x, t * x * z - s * y, t * y * z + s * x, t * z * z + c];
  };
  const mulRM = (a, b) => { const o = new Array(9); for (let r = 0; r < 3; r++) for (let c = 0; c < 3; c++) o[3 * r + c] = a[3 * r] * b[c] + a[3 * r + 1] * b[3 + c] + a[3 * r + 2] * b[6 + c]; return o; };
  const applyRM = (m, v) => [m[0] * v[0] + m[1] * v[1] + m[2] * v[2], m[3] * v[0] + m[4] * v[1] + m[5] * v[2], m[6] * v[0] + m[7] * v[1] + m[8] * v[2]];
  const applyRMT = (m, v) => [m[0] * v[0] + m[3] * v[1] + m[6] * v[2], m[1] * v[0] + m[4] * v[1] + m[7] * v[2], m[2] * v[0] + m[5] * v[1] + m[8] * v[2]];
  // object (Unity) -> GL world (z flipped): column-major mat4
  const glModel = (rm, t) => new Float32Array([rm[0], rm[3], -rm[6], 0, rm[1], rm[4], -rm[7], 0, rm[2], rm[5], -rm[8], 0, t[0], t[1], -t[2], 1]);

  class ReelCharge {
    constructor(reel, extras) {
      this.reel = reel;
      this.extras = extras;
      const A = reel.assets, d = A.data, arcs = A.arcs;
      this.settings = CA.settings(arcs.profile);
      const charge = reel.core.crystals[CHARGE].data;
      const cs = CA.ChargeSites.tryBuild(charge.basis, charge.tris, 1), os = CA.ChargeSites.tryBuild(d.omni.verts, d.omni.tris, A.cadence.plateThickness);
      if (!cs.ok) throw new Error('Charge crystal: ' + cs.problem);
      if (!os.ok) throw new Error('omni: ' + os.problem);
      this.chargeSites = cs.sites; this.omniSites = os.sites;
      this.chargeLight = new Float32Array(cs.sites.Faces.length);
      this.omniLight = new Float32Array(os.sites.Faces.length);
      this.writer = new CA.ChargeArcMeshWriter();
      this.uniforms = CR.ChargeArcsShader.uniformsFrom(arcs, 'OmniChargeArcs');
    }

    plan() {
      const p = this.reel.p;
      this.walker = null; this.walkerAt = Infinity;
      this.chain = new CA.ChargeFluxChain(this.omniSites, p.Seed ^ 0x5bd1e995);
    }

    // StrikeTriangles: before the triangles step, from the columns' last state.
    strike(tau, ph, cols) {
      if (ph.fx <= 0.001) { cols.strike.fill(0); return; }
      const ex = this.extras, shape = ex.triShape, split = shape.faceSplit * shape.splitUnit, M = ex.triM, n = cols.count;
      const centres = [], radii = [], places = [], seats = [], seatRadii = [];
      for (let j = 0; j < n; j++) {
        const place = cols.state[j][0];
        const b = DP.columnBody(cols.centres[j], cols.bounds[j], place, shape.reach, shape.sizeStart, split);
        centres.push(applyRM(M, b.centre)); radii.push(b.radius); places.push(place);
        const s = DP.columnBody(cols.centres[j], cols.bounds[j], 1, shape.reach, shape.sizeStart, split);
        seats.push(applyRM(M, s.centre)); seatRadii.push(s.radius);
      }
      CA.fluxStrikes(this.omniSites, this.chain, ph.fluxSeconds, CA.scaled(this.settings, ph.fx), centres, radii, places, seats, seatRadii, n, cols.strike);
    }

    frame(out) {
      const reel = this.reel, eye = out.camera.eye;
      out.seam = null; out.flux = null;
      if (out.crystals) {
        const r = out.crystals[CHARGE];
        let morph = 0; for (const w of reel.morph[CHARGE]) morph = Math.max(morph, w);
        const strength = r.effect * (1 - smooth(morph / 0.05));
        if (strength > 0.001) {
          const f = reel.core.crystalFrame(CHARGE, r.t, r.params);
          const rm = mulRM(rowMajorAxisAngle(f.tumbleAxis, f.tumble), rowMajorAxisAngle(f.spinAxis, f.spin));
          // This crystal's own idle clock (the game's _t). It restarts only at the cut, mid-flight, so one walker started
          // there serves the whole visible stretch, across the loop point too: the walk is a pure function of its start
          // and the clock, so a scrub rebuilds exactly the walk continuous play draws.
          const start = 17.31 * CHARGE, seconds = r.idle - reel.p.EffectPhase;
          if (!this.walker || seconds < this.walkerAt) this.walker = new CA.ChargeSeamWalker(this.chargeSites, reel.p.Seed, start);
          this.walkerAt = seconds;
          this.walker.advance(seconds, this.settings);
          const eyeLocal = applyRMT(rm, [eye[0] - f.centre[0], eye[1] - f.centre[1], eye[2] - f.centre[2]]);
          this.writer.clear();
          CA.seamWalk(this.chargeSites, this.walker, seconds, CA.scaled(this.settings, strength), this.writer, eyeLocal, this.chargeLight);
          CA.writeFaces(this.chargeSites, this.writer, this.chargeLight, true);
          out.seam = { model: glModel(rm, f.centre), positions: Float32Array.from(this.writer.positions), data: Float32Array.from(this.writer.data), indices: Uint32Array.from(this.writer.indices) };
        }
      }
      if (out.omni && out.omni.fx > 0.001) {
        this.writer.clear();
        CA.flux(this.omniSites, this.chain, out.phase.fluxSeconds, CA.scaled(this.settings, out.omni.fx), this.writer, eye, this.omniLight);
        CA.writeFaces(this.omniSites, this.writer, this.omniLight, false);
        out.flux = { model: glModel([1, 0, 0, 0, 1, 0, 0, 0, 1], [0, 0, 0]), positions: Float32Array.from(this.writer.positions), data: Float32Array.from(this.writer.data), indices: Uint32Array.from(this.writer.indices) };
      }
    }

    layers(renderer) {
      const gl = renderer.gl, S = CR.ChargeArcsShader, prog = renderer.program(S.vertex, S.fragment);
      const vao = gl.createVertexArray(), pb = gl.createBuffer(), db = gl.createBuffer(), ib = gl.createBuffer();
      gl.bindVertexArray(vao);
      gl.bindBuffer(gl.ARRAY_BUFFER, pb); let loc = gl.getAttribLocation(prog.p, 'aPosition'); gl.enableVertexAttribArray(loc); gl.vertexAttribPointer(loc, 3, gl.FLOAT, false, 0, 0);
      gl.bindBuffer(gl.ARRAY_BUFFER, db); loc = gl.getAttribLocation(prog.p, 'aData'); gl.enableVertexAttribArray(loc); gl.vertexAttribPointer(loc, 4, gl.FLOAT, false, 0, 0);
      gl.bindBuffer(gl.ELEMENT_ARRAY_BUFFER, ib);
      gl.bindVertexArray(null);
      const self = this;
      renderer.layers.push({
        draw(glc, frame, V) {
          const L = V.look, lin = CR.Render.hexToLinear;
          for (const layer of [frame.seam, frame.flux]) {
            if (!layer || !layer.indices.length) continue;
            gl.useProgram(prog.p);
            gl.enable(gl.BLEND); gl.blendFunc(gl.ONE, gl.ONE); gl.depthMask(false); gl.enable(gl.DEPTH_TEST); gl.depthFunc(gl.LEQUAL);
            gl.disable(gl.CULL_FACE); gl.enable(gl.POLYGON_OFFSET_FILL); gl.polygonOffset(-1, -1);
            gl.uniformMatrix4fv(prog.u.uObjectToClip, false, CR.Render.mul4(V.vp, layer.model));
            gl.uniform3fv(prog.u.uBrightColor, lin(L.arcColor)); gl.uniform3fv(prog.u.uArcCoreColor, lin(L.arcCore));
            gl.uniform1f(prog.u.uBoltIntensity, L.boltGain * self.uniforms.uBoltIntensity); gl.uniform1f(prog.u.uGlowIntensity, L.glowGain * self.uniforms.uGlowIntensity);
            if (prog.u.uFogAmount) gl.uniform1f(prog.u.uFogAmount, 0);
            gl.bindVertexArray(vao);
            gl.bindBuffer(gl.ARRAY_BUFFER, pb); gl.bufferData(gl.ARRAY_BUFFER, layer.positions, gl.STREAM_DRAW);
            gl.bindBuffer(gl.ARRAY_BUFFER, db); gl.bufferData(gl.ARRAY_BUFFER, layer.data, gl.STREAM_DRAW);
            gl.bufferData(gl.ELEMENT_ARRAY_BUFFER, layer.indices, gl.STREAM_DRAW);
            gl.drawElements(gl.TRIANGLES, layer.indices.length, gl.UNSIGNED_INT, 0);
          }
          gl.disable(gl.BLEND); gl.disable(gl.POLYGON_OFFSET_FILL); gl.depthMask(true);
          gl.bindVertexArray(null);
        },
      });
    }
  }

  CR.ReelCharge = ReelCharge;
})(globalThis.CR = globalThis.CR || {});
