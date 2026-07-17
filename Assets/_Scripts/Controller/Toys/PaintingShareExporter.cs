using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using CosmicShore.Data;
using CosmicShore.ScriptableObjects;
using CosmicShore.Utility;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Exports a finished painting as a single self-contained HTML file - an inline-WebGL prism
    /// reconstruction (no external scripts, works anywhere a browser opens it) with drag-to-orbit,
    /// pinch/wheel zoom, and a gentle auto-spin - then hands it to the platform share sheet via
    /// NativeShare. The reconstruction uses the saved drawing state (position/orientation/size/
    /// domain of every prism); paintings finished before prism capture existed fall back to boxes
    /// laid along the stroke polylines so sharing always works.
    /// </summary>
    public static class PaintingShareExporter
    {
        /// <summary>Write the web reconstruction for a painting. Returns false when there is nothing to export.</summary>
        public static bool TryExport(PaintingDefinitionSO painting, ToyContext context, out string filePath)
        {
            filePath = null;
            if (!painting) return false;

            painting.EnsureStrokes();
            var records = PaintingPrismStore.GetPrisms(painting.PaintingId, painting.Strokes.Count);
            if (records.Count == 0)
                records = FallbackFromStrokes(painting);
            if (records.Count == 0) return false;

            string html = BuildHtml(painting.DisplayName, records,
                ToyFactory.DomainAccentColor(context, Domains.Jade),
                ToyFactory.DomainAccentColor(context, Domains.Ruby),
                ToyFactory.DomainAccentColor(context, Domains.Gold));

            string safeId = SanitizeFileName(painting.PaintingId);
            filePath = Path.Combine(Application.persistentDataPath, $"{safeId}_masterpiece.html");
            try
            {
                File.WriteAllText(filePath, html);
            }
            catch (Exception e)
            {
                // Full disk / sandboxed path - fail the share quietly rather than throwing out of
                // the gate's trigger callback.
                CSDebug.LogWarning($"[PaintingShareExporter] Could not write reconstruction: {e.Message}");
                filePath = null;
                return false;
            }
            CSDebug.Log($"[PaintingShareExporter] Wrote {records.Count}-prism reconstruction to {filePath}");
            return true;
        }

        /// <summary>
        /// Hand an exported reconstruction to the platform share sheet (mobile). The editor and
        /// desktop have no share sheet (NativeShare just logs there), so they open the viewer in
        /// the default browser instead - flying the SHARE gate always visibly does something.
        /// </summary>
        public static void Share(string filePath, string paintingName)
        {
#if UNITY_EDITOR || UNITY_STANDALONE
            Application.OpenURL("file:///" + filePath.Replace('\\', '/'));
            CSDebug.Log($"[PaintingShareExporter] Opened reconstruction in browser: {filePath}");
#else
            try
            {
                new NativeShare()
                    .AddFile(filePath, "text/html")
                    .SetSubject($"My Cosmic Shore masterpiece: {paintingName}")
                    .SetText($"I painted the {paintingName} with my vessel's trail in Cosmic Shore. " +
                             "Open the file and spin it around!")
                    .Share();
            }
            catch (Exception e)
            {
                CSDebug.LogWarning($"[PaintingShareExporter] Share sheet unavailable ({e.Message}). " +
                                   $"Reconstruction saved at: {filePath}");
            }
#endif
        }

        /// <summary>
        /// For paintings completed before drawing-state capture existed: approximate the artwork
        /// with one thin box per stroke segment, in painting-local space.
        /// </summary>
        public static List<PaintingPrismRecord> FallbackFromStrokes(PaintingDefinitionSO painting)
        {
            var records = new List<PaintingPrismRecord>();
            foreach (var stroke in painting.Strokes)
            {
                for (int i = 1; i < stroke.points.Count; i++)
                {
                    Vector3 a = stroke.points[i - 1], b = stroke.points[i];
                    Vector3 seg = b - a;
                    if (seg.sqrMagnitude < 1e-4f) continue;
                    records.Add(PaintingPrismRecord.From(
                        (a + b) * 0.5f,
                        Quaternion.LookRotation(seg.normalized, Vector3.up),
                        new Vector3(3f, 3f, seg.magnitude),
                        stroke.domain,
                        PrismType.Interactive));
                }
            }
            return records;
        }

        // ── HTML generation (pure - unit-testable) ──────────────────────────

        public static string BuildHtml(string title, IReadOnlyList<PaintingPrismRecord> records,
            Color jade, Color ruby, Color gold)
        {
            var data = new StringBuilder(records.Count * 64);
            data.Append('[');
            for (int i = 0; i < records.Count; i++)
            {
                var r = records[i];
                if (i > 0) data.Append(',');
                data.Append('[');
                AppendFloats(data, r.px, r.py, r.pz, r.qx, r.qy, r.qz, r.qw, r.sx, r.sy, r.sz);
                data.Append(',').Append(r.domain).Append(']');
            }
            data.Append(']');

            string palette =
                $"{{1:[{Rgb(jade)}],2:[{Rgb(ruby)}],4:[{Rgb(gold)}],0:[0.6,0.6,0.65],3:[0.45,0.6,0.9]}}";

            return ViewerTemplate
                .Replace("__TITLE__", WebUtilityEscape(title))
                .Replace("__COUNT__", records.Count.ToString(CultureInfo.InvariantCulture))
                .Replace("__PALETTE__", palette)
                .Replace("__DATA__", data.ToString());
        }

        static void AppendFloats(StringBuilder sb, params float[] values)
        {
            for (int i = 0; i < values.Length; i++)
            {
                if (i > 0) sb.Append(',');
                sb.Append(Math.Round(values[i], 3).ToString(CultureInfo.InvariantCulture));
            }
        }

        static string Rgb(Color c) => string.Format(CultureInfo.InvariantCulture,
            "{0:0.###},{1:0.###},{2:0.###}",
            Mathf.Clamp01(c.r), Mathf.Clamp01(c.g), Mathf.Clamp01(c.b)); // trail colours are HDR

        static string WebUtilityEscape(string s) =>
            (s ?? "").Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");

        static string SanitizeFileName(string s)
        {
            var sb = new StringBuilder(s.Length);
            foreach (char c in s)
                sb.Append(char.IsLetterOrDigit(c) ? c : '_');
            return sb.Length > 0 ? sb.ToString() : "painting";
        }

        // Domain enum values: Jade=1, Ruby=2, Blue=3, Gold=4 (palette keys above).
        // Unity is left-handed; the viewer flips Z (positions z→-z, quaternions x,y→-x,-y).
        const string ViewerTemplate = @"<!DOCTYPE html>
<html><head><meta charset='utf-8'><meta name='viewport' content='width=device-width,initial-scale=1'>
<title>__TITLE__ - Cosmic Shore</title>
<style>
html,body{margin:0;height:100%;overflow:hidden;background:#05070f;font-family:system-ui,sans-serif}
canvas{display:block;width:100%;height:100%;touch-action:none}
#hud{position:fixed;left:0;right:0;bottom:14px;text-align:center;color:#9fb4d8;pointer-events:none}
#hud b{color:#e8f1ff;font-weight:600}
</style></head><body>
<canvas id='c'></canvas>
<div id='hud'><b>__TITLE__</b> · painted in Cosmic Shore · __COUNT__ prisms · drag to spin</div>
<script>
'use strict';
var PRISMS=__DATA__;
var PALETTE=__PALETTE__;
var canvas=document.getElementById('c');
var gl=canvas.getContext('webgl')||canvas.getContext('experimental-webgl');
if(!gl){document.getElementById('hud').textContent='WebGL unavailable';throw new Error('no webgl');}

function qrot(q,v){ // rotate v by quaternion q=[x,y,z,w]
  var x=q[0],y=q[1],z=q[2],w=q[3],vx=v[0],vy=v[1],vz=v[2];
  var tx=2*(y*vz-z*vy),ty=2*(z*vx-x*vz),tz=2*(x*vy-y*vx);
  return [vx+w*tx+y*tz-z*ty, vy+w*ty+z*tx-x*tz, vz+w*tz+x*ty-y*tx];
}

/* Build one interleaved buffer: pos(3) normal(3) color(3) per vertex, 36 verts per prism. */
var FACES=[ /* normal, then 4 corners (unit cube, CCW from outside) */
 [[0,0,1], [-1,-1,1],[1,-1,1],[1,1,1],[-1,1,1]],
 [[0,0,-1],[1,-1,-1],[-1,-1,-1],[-1,1,-1],[1,1,-1]],
 [[1,0,0], [1,-1,1],[1,-1,-1],[1,1,-1],[1,1,1]],
 [[-1,0,0],[-1,-1,-1],[-1,-1,1],[-1,1,1],[-1,1,-1]],
 [[0,1,0], [-1,1,1],[1,1,1],[1,1,-1],[-1,1,-1]],
 [[0,-1,0],[-1,-1,-1],[1,-1,-1],[1,-1,1],[-1,-1,1]]];
var verts=new Float32Array(PRISMS.length*36*9);
var mn=[1e9,1e9,1e9],mx=[-1e9,-1e9,-1e9];
var o=0;
for(var p=0;p<PRISMS.length;p++){
  var d=PRISMS[p];
  var pos=[d[0],d[1],-d[2]];               // z-flip: Unity left-handed -> GL right-handed
  var q=[-d[3],-d[4],d[5],d[6]];           // quaternion under z-mirror
  var s=[d[7]/2,d[8]/2,d[9]/2];
  var col=PALETTE[d[10]]||PALETTE[0];
  for(var a=0;a<3;a++){mn[a]=Math.min(mn[a],pos[a]-8);mx[a]=Math.max(mx[a],pos[a]+8);}
  for(var f=0;f<6;f++){
    var face=FACES[f];
    var n=qrot(q,face[0]);
    var c=[];
    for(var k=1;k<=4;k++){
      var lc=[face[k][0]*s[0],face[k][1]*s[1],face[k][2]*s[2]];
      var wc=qrot(q,lc);
      c.push([wc[0]+pos[0],wc[1]+pos[1],wc[2]+pos[2]]);
    }
    var order=[0,1,2,0,2,3];
    for(var t=0;t<6;t++){
      var v=c[order[t]];
      verts[o++]=v[0];verts[o++]=v[1];verts[o++]=v[2];
      verts[o++]=n[0];verts[o++]=n[1];verts[o++]=n[2];
      verts[o++]=col[0];verts[o++]=col[1];verts[o++]=col[2];
    }
  }
}
var center=[(mn[0]+mx[0])/2,(mn[1]+mx[1])/2,(mn[2]+mx[2])/2];
var radius=Math.max(mx[0]-mn[0],mx[1]-mn[1],mx[2]-mn[2])*0.62+1;

var VS='attribute vec3 aP;attribute vec3 aN;attribute vec3 aC;uniform mat4 uMVP;uniform mat3 uR;varying vec3 vN;varying vec3 vC;void main(){gl_Position=uMVP*vec4(aP,1.0);vN=uR*aN;vC=aC;}';
var FS='precision mediump float;varying vec3 vN;varying vec3 vC;void main(){vec3 n=normalize(vN);float l=0.35+0.65*max(dot(n,normalize(vec3(0.4,0.7,0.6))),0.0);vec3 c=vC*l+vC*vC*0.25;gl_FragColor=vec4(c,1.0);}';
function sh(type,src){var s=gl.createShader(type);gl.shaderSource(s,src);gl.compileShader(s);return s;}
var prog=gl.createProgram();
gl.attachShader(prog,sh(gl.VERTEX_SHADER,VS));gl.attachShader(prog,sh(gl.FRAGMENT_SHADER,FS));
gl.linkProgram(prog);gl.useProgram(prog);
var buf=gl.createBuffer();gl.bindBuffer(gl.ARRAY_BUFFER,buf);gl.bufferData(gl.ARRAY_BUFFER,verts,gl.STATIC_DRAW);
function attr(name,size,off){var a=gl.getAttribLocation(prog,name);gl.enableVertexAttribArray(a);gl.vertexAttribPointer(a,size,gl.FLOAT,false,36,off);}
attr('aP',3,0);attr('aN',3,12);attr('aC',3,24);
var uMVP=gl.getUniformLocation(prog,'uMVP');
var uR=gl.getUniformLocation(prog,'uR');
gl.enable(gl.DEPTH_TEST);gl.clearColor(0.02,0.03,0.06,1);

var yaw=0.6,pitch=0.25,dist=radius*2.1,auto=true,lastTouch=0;
function onDrag(dx,dy){yaw+=dx*0.008;pitch=Math.max(-1.4,Math.min(1.4,pitch+dy*0.008));auto=false;lastTouch=performance.now();}
var drag=false,px=0,py=0;
canvas.addEventListener('mousedown',function(e){drag=true;px=e.clientX;py=e.clientY;});
window.addEventListener('mouseup',function(){drag=false;});
window.addEventListener('mousemove',function(e){if(drag){onDrag(e.clientX-px,e.clientY-py);px=e.clientX;py=e.clientY;}});
canvas.addEventListener('wheel',function(e){dist*=(1+e.deltaY*0.001);dist=Math.max(radius*0.6,Math.min(radius*6,dist));e.preventDefault();},{passive:false});
var pinch=0;
canvas.addEventListener('touchstart',function(e){if(e.touches.length===1){px=e.touches[0].clientX;py=e.touches[0].clientY;}else if(e.touches.length===2){pinch=Math.hypot(e.touches[0].clientX-e.touches[1].clientX,e.touches[0].clientY-e.touches[1].clientY);}e.preventDefault();},{passive:false});
canvas.addEventListener('touchmove',function(e){
 if(e.touches.length===1){onDrag(e.touches[0].clientX-px,e.touches[0].clientY-py);px=e.touches[0].clientX;py=e.touches[0].clientY;}
 else if(e.touches.length===2){var d2=Math.hypot(e.touches[0].clientX-e.touches[1].clientX,e.touches[0].clientY-e.touches[1].clientY);if(pinch>0){dist*=pinch/d2;dist=Math.max(radius*0.6,Math.min(radius*6,dist));}pinch=d2;}
 e.preventDefault();},{passive:false});

function mat(){ // column-major MVP + rotation for normals
  var cy=Math.cos(yaw),sy=Math.sin(yaw),cp=Math.cos(pitch),sp=Math.sin(pitch);
  var R=[cy,sy*sp,-sy*cp, 0,cp,sp, sy,-cy*sp,cy*cp]; // yaw then pitch (3x3, column-major)
  var w=canvas.width,h=canvas.height,aspect=w/h,f=1/Math.tan(0.4),near=radius*0.05,far=radius*20;
  var eye=[0,0,dist];
  var M=new Float32Array(16);
  // model rotation + center offset, then translate eye, then perspective - composed directly.
  function rv(v){return [R[0]*v[0]+R[3]*v[1]+R[6]*v[2],R[1]*v[0]+R[4]*v[1]+R[7]*v[2],R[2]*v[0]+R[5]*v[1]+R[8]*v[2]];}
  var t=rv([-center[0],-center[1],-center[2]]);
  var A=f/aspect,B=f,C=(far+near)/(near-far),D=(2*far*near)/(near-far);
  // columns: rotated basis with perspective applied
  var col=[[R[0],R[1],R[2]],[R[3],R[4],R[5]],[R[6],R[7],R[8]]];
  for(var i=0;i<3;i++){
    M[i*4+0]=A*col[i][0];
    M[i*4+1]=B*col[i][1];
    M[i*4+2]=C*col[i][2];
    M[i*4+3]=-col[i][2];
  }
  var tz=t[2]-eye[2];
  M[12]=A*t[0];M[13]=B*t[1];M[14]=C*tz+D;M[15]=-tz;
  return {M:M,R:new Float32Array(R)};
}
function frame(now){
  if(auto||now-lastTouch>3500){auto=true;yaw+=0.004;}
  var dw=canvas.clientWidth*(window.devicePixelRatio||1),dh=canvas.clientHeight*(window.devicePixelRatio||1);
  if(canvas.width!==dw||canvas.height!==dh){canvas.width=dw;canvas.height=dh;gl.viewport(0,0,dw,dh);}
  var m=mat();
  gl.clear(gl.COLOR_BUFFER_BIT|gl.DEPTH_BUFFER_BIT);
  gl.uniformMatrix4fv(uMVP,false,m.M);
  gl.uniformMatrix3fv(uR,false,m.R);
  gl.drawArrays(gl.TRIANGLES,0,PRISMS.length*36);
  requestAnimationFrame(frame);
}
requestAnimationFrame(frame);
</script></body></html>";
    }
}
