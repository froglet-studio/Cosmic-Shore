// The reel's two stateful layers, wired as OmniCombinePlayer wires them:
//   the Mass prisms     WriteMass: per-plate column centres (UV2), the depth columns stepped against the camera, the shape
//                       morphing from the Mass crystal's toward the omni triangles'; drawn with OmniDepthPrismShader
//   the omni triangles  OmniCrystalTriangles turned onto the omni (TriangleRotation), stepped as the omni wakes
//   the Charge light    the seam walk on the waiting Charge crystal, the chained flux on the woken omni, and the strikes
//                       that light a raised triangle prism (WriteSeamWalk, WriteFlux, StrikeTriangles)
//
// The ratchets and the Charge walk carry state from frame to frame. The reel has to scrub and to loop, so the
// state is BAKED: two loops are stepped at 60 Hz and the second is kept, so frame 0 follows the last frame.
(function (CR) {
  'use strict';
  const { MASS, CHARGE, Core } = CR.Combine;
  const DP = CR.DepthPrisms;
  const BAKE_HZ = 60;

  // The lab's knobs for these layers (merged into the page's SHIPPED / SPEC by app.js).
  CR.ExtrasLook = {
    defaults: {
      prismLink: 1,                  // lab: 1 = the prisms wear the body colours (both materials ship the body's pair)
      prismBright: '#7cfc00',        // MassDepthPrisms / OmniDepthPrisms _BrightColor
      prismDark: '#010201',          // _DarkColor
      strikeColor: '#d9f2ff',        // _StrikeColor (0.85, 0.95, 1.0)
      strikeGain: 1,                 // lab
    },
    spec: [
      ['Mass prisms', 'prismLink', 0, 1, 1, 'Colours', 'Both depth-prism materials ship the body\'s colour pair. Linked, they follow the Body colours.', 'select', ['Own colours', 'Follow the body']],
      ['Mass prisms', 'prismBright', null, null, null, 'Rim colour', 'MassDepthPrisms / OmniDepthPrisms _BrightColor (used when not following the body).', 'color'],
      ['Mass prisms', 'prismDark', null, null, null, 'Face colour', '_DarkColor (used when not following the body).', 'color'],
      ['Mass prisms', 'strikeColor', null, null, null, 'Strike colour', '_StrikeColor: a raised prism a Charge arc passes through lights up in this colour.', 'color'],
      ['Mass prisms', 'strikeGain', 0, 4, 0.05, 'Strike intensity', 'Lab: multiplies the strike colour.'],
    ],
    notModelled: [
      'The depth prisms\' orientation jitter (CrystalPoseJitter) is not used by the converter and is not drawn here.',
    ],
  };

  class ReelExtras {
    constructor(reel) {
      this.reel = reel;
      const A = reel.assets, depth = A.depth;
      this.prof = DP.profile(depth.profile);
      const mats = depth.materials, comb = depth.combine;
      this.mats = mats;
      this.massShape = DP.DepthShape.of(mats.mass, comb.massMeshScale, comb.depth.massThickness);
      this.triThin = DP.DepthShape.of(mats.omni, 1, comb.depth.omniThickness);
      this.triShape = DP.DepthShape.of(mats.omni, 1, 1);
      const mass = reel.core.crystals[MASS].data;
      this.massPlateOf = mass.plate;
      this.massSites = DP.massSites(mass.plateSite);
      this.massNp = this.massSites.length;
      this.triMesh = DP.trianglesMesh(depth);
      const rot = DP.triangleRotation(A.data.omniTriangleRotation);
      this.triM = rot.matrix;
      const plates = DP.tryGetPlates({ positions: this.triMesh.positions, uv2: this.triMesh.uv2 });
      if (plates.problem) throw new Error(plates.problem);
      this.triPlates = plates;
      this.arcs = CR.ReelCharge ? new CR.ReelCharge(reel, this) : null;
    }

    // Stage point -> triangle mesh point (transpose of the row-major rotation).
    toStage(v) { const m = this.triM; return [m[0] * v[0] + m[1] * v[1] + m[2] * v[2], m[3] * v[0] + m[4] * v[1] + m[5] * v[2], m[6] * v[0] + m[7] * v[1] + m[8] * v[2]]; }
    triBodies(tau) { const i = this.index(tau), out = []; for (let j = 0; j < 20; j++) out.push(Array.from(this.triBodyTable.subarray((i * 20 + j) * 4, (i * 20 + j) * 4 + 4))); return out; }
    toTri(v) { const m = this.triM; return [m[0] * v[0] + m[3] * v[1] + m[6] * v[2], m[1] * v[0] + m[4] * v[1] + m[7] * v[2], m[2] * v[0] + m[5] * v[1] + m[8] * v[2]]; }

    // WriteMass's columns for the Mass crystal's pose at tau (the pose must be in reel.poses[MASS]).
    massColumnsAt(r) {
      const fill = DP.massPlates(this.reel.poses[MASS], r.centre, this.massPlateOf, this.massSites, this.reel.morph[MASS]);
      const shape = DP.DepthShape.morph(this.massShape, this.triThin, fill.morph);
      return { plates: fill.plates, shape };
    }

    plan() {
      const reel = this.reel, T = reel.T, n = Math.round(T.L * BAKE_HZ), dt = 1 / BAKE_HZ;
      this.n = n;
      this.massState = new Float32Array(n * 4 * this.massNp);
      this.triState = new Float32Array(n * 4 * 20);
      this.massFar = new Float32Array(n);
      this.triFar = new Float32Array(n);
      this.triBodyTable = new Float32Array(n * 4 * 20);
      const massCols = new DP.DepthColumns(this.massNp);
      const p = this.triPlates;
      const triCols = new DP.DepthColumns(p.directions.length, p.directions, p.centres, p.tops, p.bounds, p.plateRadius);
      if (this.arcs) this.arcs.plan();
      for (let pass = 0; pass < 2; pass++) {
        for (let i = 0; i < n; i++) {
          const tau = i / BAKE_HZ, ph = reel.phase(tau), cam = reel.cameraAt(tau).eye;
          if (ph.crystals) {
            const r = reel.poseCrystal(MASS, tau, reel.camDir(tau));
            const c = this.massColumnsAt(r);
            massCols.setMassPlates(c.plates, c.shape.topHeight);
            massCols.step([cam[0] - r.centre[0], cam[1] - r.centre[1], cam[2] - r.centre[2]], dt, this.prof, c.shape.reach, c.shape.sizeStart, r.effect, r.columnPlace);
            if (pass === 1) this.massFar[i] = DP.massFarthest(r.centre, c.plates, c.shape, massCols.state);
          } else massCols.fresh = true;
          if (ph.omni) {
            if (this.arcs) this.arcs.strike(tau, ph, triCols);
            triCols.step(this.toTri(cam), dt, this.prof, this.triShape.reach, this.triShape.sizeStart, ph.fx);
          } else triCols.fresh = true;
          if (pass === 1 && ph.omni) {
            // how far out the omni's raised triangle prisms stand (the lab's "keep the raised prisms in frame")
            const sh = this.triShape, split = sh.faceSplit * sh.splitUnit;
            let far = 0;
            for (let j = 0; j < triCols.count; j++) {
              const b = DP.columnBody(triCols.centres[j], triCols.bounds[j], triCols.state[j][0], sh.reach, sh.sizeStart, split);
              far = Math.max(far, Math.hypot(b.centre[0], b.centre[1], b.centre[2]) + b.radius);
              const w = this.toStage(b.centre);
              this.triBodyTable.set([w[0], w[1], w[2], b.radius], (i * 20 + j) * 4);
            }
            this.triFar[i] = far * ph.appear;
          }
          if (pass === 1) {
            this.massState.set(massCols.stateArray().subarray(0, 4 * this.massNp), i * 4 * this.massNp);
            this.triState.set(triCols.stateArray().subarray(0, 80), i * 80);
          }
        }
      }
    }

    index(tau) { return Math.min(this.n - 1, Math.max(0, Math.round(tau * BAKE_HZ) % this.n)); }
    massReach(tau) { return this.massFar ? this.massFar[this.index(tau)] : 0; }
    triReach(tau) { return this.triFar ? this.triFar[this.index(tau)] : 0; }

    // Called by Reel.frame: the Mass columns' plate centres and state, the triangles' state and the Charge light.
    frame(out) {
      const i = this.index(out.tau);
      if (out.crystals) {
        const r = out.crystals[MASS], c = this.massColumnsAt(r);
        out.mass = { centre: r.centre, plates: c.plates, shape: c.shape, state: this.massState.subarray(i * 4 * this.massNp, (i + 1) * 4 * this.massNp) };
      }
      if (out.omni) out.triangles = { state: this.triState.subarray(i * 80, i * 80 + 80), appear: out.omni.appear };
      if (this.arcs) this.arcs.frame(out);
    }

    // ------------------------------------------------------------ drawing
    layers(renderer) {
      const gl = renderer.gl, S = CR.DepthPrismsShader;
      const prog = renderer.program(S.vertex, S.fragment);
      const self = this;
      // the Mass crystal: unwelded, flat face normals from the pose (Piece), plus TEXCOORD2 per corner
      const d = this.reel.core.crystals[MASS].data;
      const pc = CR.Render.piece(d.basis, d.tris, d.plate, MASS);
      const plateUv = new Float32Array(pc.corner.length * 4);
      const vaoOf = (pos, nrm, uv, dynamic) => {
        const vao = gl.createVertexArray(); gl.bindVertexArray(vao);
        const bufs = [];
        for (const [name, data, size] of [['aPosition', pos, 3], ['aNormal', nrm, 3], ['aPlate', uv, 4]]) {
          const b = gl.createBuffer(); gl.bindBuffer(gl.ARRAY_BUFFER, b); gl.bufferData(gl.ARRAY_BUFFER, data, dynamic ? gl.DYNAMIC_DRAW : gl.STATIC_DRAW);
          const loc = gl.getAttribLocation(prog.p, name);
          if (loc >= 0) { gl.enableVertexAttribArray(loc); gl.vertexAttribPointer(loc, size, gl.FLOAT, false, 0, 0); }
          bufs.push(b);
        }
        gl.bindVertexArray(null);
        return { vao, bufs };
      };
      const massGpu = vaoOf(pc.pos, pc.nrm, plateUv, true);
      // the triangles: indexed, static
      const tm = this.triMesh, triGpu = vaoOf(tm.positions, tm.normals, tm.uv2, false);
      gl.bindVertexArray(triGpu.vao);
      const ib = gl.createBuffer(); gl.bindBuffer(gl.ELEMENT_ARRAY_BUFFER, ib); gl.bufferData(gl.ELEMENT_ARRAY_BUFFER, tm.indices, gl.STATIC_DRAW);
      gl.bindVertexArray(null);
      const m = this.triM;
      // object -> GL world: Unity's frame with z flipped
      const modelOf = (rot, t) => new Float32Array([rot[0], rot[3], -rot[6], 0, rot[1], rot[4], -rot[7], 0, rot[2], rot[5], -rot[8], 0, t[0], t[1], -t[2], 1]);
      const I3 = [1, 0, 0, 0, 1, 0, 0, 0, 1];
      const lin = (hex, g) => CR.Render.hexToLinear(hex, g);
      const colours = (look, mat) => {
        const bright = look.prismLink ? lin(look.bodyBright, look.bodyGain) : lin(look.prismBright);
        const dark = look.prismLink ? lin(look.bodyDark) : lin(look.prismDark);
        if (look.prismLink && look.tint > 0) {
          const e = lin(look.elementColors[MASS], look.bodyGain);
          for (let k = 0; k < 3; k++) bright[k] += (e[k] - bright[k]) * look.tint;
        }
        return { uBright: [...bright, 1], uDark: [...dark, 1], uStrikeColor: [...lin(look.strikeColor, look.strikeGain), 1] };
      };
      const drawWith = (vao, count, indexed, shape, state, model, opacity, V, mat) => {
        gl.useProgram(prog.p);
        S.applyState(gl);
        const u = S.uniforms(mat, shape, Object.assign({
          uModel: model, uViewProj: V.vp, uCameraPos: V.eye, uOpacity: opacity, uDepthState: state,
          uSpread: mat._Spread.slice(0, 3).map((s) => s * V.unitScale),
        }, colours(V.look, mat)));
        gl.bindVertexArray(vao);
        let leaving = false;
        for (let j = 0; j < state.length / 4; j++) if (state[4 * j + 1] < 1) { leaving = true; break; }
        for (const passDef of S.passes) {
          if (passDef.uLeaving && !leaving) continue;
          u.uLeaving = passDef.uLeaving;
          S.setUniforms(gl, prog.p, u);
          if (indexed) gl.drawElements(gl.TRIANGLES, count, gl.UNSIGNED_SHORT, 0); else gl.drawArrays(gl.TRIANGLES, 0, count);
        }
      };
      renderer.layers.push({
        draw(glc, frame, V) {
          if (frame.mass) {
            const ms = frame.mass;
            CR.Render.writePiece(pc, self.reel.poses[MASS], ms.centre);
            for (let c = 0; c < pc.corner.length; c++) plateUv.set(ms.plates[pc.cornerPlate[c]], 4 * c);
            gl.bindBuffer(gl.ARRAY_BUFFER, massGpu.bufs[0]); gl.bufferSubData(gl.ARRAY_BUFFER, 0, pc.pos);
            gl.bindBuffer(gl.ARRAY_BUFFER, massGpu.bufs[1]); gl.bufferSubData(gl.ARRAY_BUFFER, 0, pc.nrm);
            gl.bindBuffer(gl.ARRAY_BUFFER, massGpu.bufs[2]); gl.bufferSubData(gl.ARRAY_BUFFER, 0, plateUv);
            const st = new Float32Array(240); st.set(ms.state);
            drawWith(massGpu.vao, pc.corner.length, false, ms.shape, st, modelOf(I3, ms.centre), 1, V, self.mats.mass);
          }
          if (frame.triangles) {
            const st = new Float32Array(240); st.set(frame.triangles.state);
            drawWith(triGpu.vao, tm.indices.length, true, self.triShape, st, modelOf(m, [0, 0, 0]), frame.triangles.appear, V, self.mats.omni);
          }
          gl.bindVertexArray(null);
        },
      });
      if (this.arcs) this.arcs.layers(renderer);
    }
  }

  CR.ReelExtras = ReelExtras;
})(globalThis.CR = globalThis.CR || {});
