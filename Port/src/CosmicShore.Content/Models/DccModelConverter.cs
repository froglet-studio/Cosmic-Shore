using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace CosmicShore.Content.Models
{
    /// <summary>
    /// Blender (<c>.blend</c>) and Maya (<c>.ma</c>/<c>.mb</c>) files, imported the way Unity does:
    /// Unity cannot read them either - its ModelImporter runs the installed Blender or Maya in the
    /// background to export an FBX, then imports that. Prisma does the same, with the same export
    /// settings as Unity's <c>Unity-BlenderToFBX.py</c> (and Maya's FBX plugin), caches the FBX by
    /// the file's path, size and time, and hands it to <see cref="FbxModelImporter"/>.
    /// Without Blender/Maya on the machine the file does not import, exactly as in Unity.
    /// Override the search with <c>PRISMA_BLENDER</c> (blender executable) or <c>PRISMA_MAYAPY</c>.
    /// </summary>
    public static class DccModelConverter
    {
        public static bool IsBlender(string path) => path != null && path.EndsWith(".blend", StringComparison.OrdinalIgnoreCase);
        public static bool IsMaya(string path) => path != null && (path.EndsWith(".ma", StringComparison.OrdinalIgnoreCase) || path.EndsWith(".mb", StringComparison.OrdinalIgnoreCase));
        public static bool IsDccPath(string path) => IsBlender(path) || IsMaya(path);

        /// <summary>The folder converted FBX files are kept in (overridable for tests).</summary>
        public static string CacheDir = Path.Combine(Path.GetTempPath(), "prisma-dcc");

        /// <summary>How long one conversion may take.</summary>
        public static TimeSpan Timeout = TimeSpan.FromMinutes(3);

        /// <summary>Which application converts this file, or null for a file that needs none.</summary>
        public static string ToolFor(string path) => IsBlender(path) ? "Blender" : IsMaya(path) ? "Maya" : null;

        /// <summary>The Blender executable: PRISMA_BLENDER, then PATH, then the usual install folders.</summary>
        public static string FindBlender() => Prisma.DccLocator.FindBlender();

        /// <summary>Maya's Python (mayapy): PRISMA_MAYAPY, MAYA_LOCATION/bin, then PATH and the usual install folders.</summary>
        public static string FindMayaPy() => Prisma.DccLocator.FindMayaPy();

        /// <summary>
        /// The FBX for a Blender/Maya file, converting it when the cache has none for this version of
        /// the file. Null with <paramref name="error"/> set when the application is missing or fails.
        /// </summary>
        public static string ToFbx(string source, out string error)
        {
            error = null;
            var full = Path.GetFullPath(source);
            var info = new FileInfo(full);
            if (!info.Exists) { error = "file not found"; return null; }
            string tool = ToolFor(full);
            if (tool == null) { error = "not a Blender or Maya file"; return null; }
            string exe = tool == "Blender" ? FindBlender() : FindMayaPy();
            if (exe == null)
            {
                error = tool == "Blender"
                    ? "Blender is not installed (Unity needs it for .blend files too); install it or set PRISMA_BLENDER to blender's path"
                    : "Maya is not installed (Unity needs it for .ma/.mb files too); install it or set PRISMA_MAYAPY to mayapy's path";
                return null;
            }

            var key = Convert.ToHexString(SHA1.HashData(Encoding.UTF8.GetBytes($"{full}|{info.Length}|{info.LastWriteTimeUtc.Ticks}")))[..16];
            // One folder per version, the FBX named like the source: the model's root object takes
            // the file's name, and every fileID in the model prefab hashes from it.
            var dir = Path.Combine(CacheDir, key);
            Directory.CreateDirectory(dir);
            var fbx = Path.Combine(dir, Path.GetFileNameWithoutExtension(full) + ".fbx");
            if (File.Exists(fbx) && new FileInfo(fbx).Length > 0) return fbx;

            var tmp = Path.Combine(dir, "export.part.fbx");
            var script = Path.Combine(dir, tool == "Blender" ? "export.blender.py" : "export.maya.py");
            File.WriteAllText(script, tool == "Blender" ? BlenderScript : MayaScript);
            var psi = new ProcessStartInfo(exe)
            {
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
            };
            if (tool == "Blender")
                foreach (var a in new[] { "--background", "--factory-startup", full, "--python", script, "--", tmp }) psi.ArgumentList.Add(a);
            else
                foreach (var a in new[] { script, full, tmp }) psi.ArgumentList.Add(a);
            try
            {
                using var p = Process.Start(psi)!;
                var output = new StringBuilder();
                p.OutputDataReceived += (_, e) => { if (e.Data != null) lock (output) output.AppendLine(e.Data); };
                p.ErrorDataReceived += (_, e) => { if (e.Data != null) lock (output) output.AppendLine(e.Data); };
                p.BeginOutputReadLine();
                p.BeginErrorReadLine();
                if (!p.WaitForExit((int)Timeout.TotalMilliseconds))
                {
                    try { p.Kill(entireProcessTree: true); } catch (InvalidOperationException) { }
                    error = $"{tool} took longer than {Timeout.TotalMinutes:0} minutes";
                    return null;
                }
                p.WaitForExit();
                if (p.ExitCode != 0 || !File.Exists(tmp))
                {
                    string last;
                    lock (output) last = string.Join(" | ", output.ToString().Split('\n').Select(l => l.Trim()).Where(l => l.Length > 0).TakeLast(3));
                    error = $"{tool} could not export it (exit {p.ExitCode}): {last}";
                    return null;
                }
                File.Move(tmp, fbx, overwrite: true);
                return fbx;
            }
            catch (System.ComponentModel.Win32Exception e) { error = $"{tool} did not start: {e.Message}"; return null; }
            finally
            {
                try { File.Delete(script); } catch (IOException) { }
                try { if (File.Exists(tmp)) File.Delete(tmp); } catch (IOException) { }
            }
        }

        /// <summary>Converts (or takes the cached FBX) and imports it; the model keeps the source's path.</summary>
        public static ImportedModel Import(string source, ModelImportSettings settings, string guid, out string error)
        {
            var fbx = ToFbx(source, out error);
            if (fbx == null) return null;
            var model = FbxModelImporter.Import(fbx, settings, guid);
            model.Path = source;
            return model;
        }

        // Unity's Editor/Data/Tools/Unity-BlenderToFBX.py (Blender 2.80+), the export it runs for a .blend.
        const string BlenderScript = """
import bpy, sys
out = sys.argv[sys.argv.index("--") + 1]
for ob in bpy.data.objects:
    ob.hide_viewport = False
    try:
        ob.hide_set(False)
    except Exception:
        pass
bpy.ops.export_scene.fbx(
    filepath=out,
    check_existing=False,
    use_selection=False,
    use_active_collection=False,
    object_types={'ARMATURE', 'CAMERA', 'LIGHT', 'MESH', 'OTHER', 'EMPTY'},
    use_mesh_modifiers=True,
    mesh_smooth_type='OFF',
    use_custom_props=True,
    bake_anim_use_nla_strips=False,
    bake_anim_use_all_actions=False,
    apply_scale_options='FBX_SCALE_ALL')
""";

        // What Unity's Maya import does: open the scene in mayapy and export it with the FBX plugin.
        const string MayaScript = """
import sys
import maya.standalone
maya.standalone.initialize(name='python')
import maya.cmds as cmds
import maya.mel as mel
src, out = sys.argv[1], sys.argv[2]
cmds.loadPlugin('fbxmaya', quiet=True)
cmds.file(src, open=True, force=True)
mel.eval('FBXResetExport')
mel.eval('FBXExportSmoothingGroups -v true')
mel.eval('FBXExportShapes -v true')
mel.eval('FBXExportSkins -v true')
mel.eval('FBXExportBakeComplexAnimation -v true')
mel.eval('FBXExport -f "%s"' % out.replace('\\', '/'))
maya.standalone.uninitialize()
""";
    }
}
