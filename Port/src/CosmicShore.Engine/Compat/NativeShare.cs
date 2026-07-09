// Placeholder for the NativeShare plugin (global namespace, like the original). Ported code
// (e.g. PaintingShareExporter's non-standalone branch) builds a fluent share request and calls
// Share(). Headless / desktop has no OS share sheet, so this records the request and logs — the
// file it would have shared is already written to persistentDataPath. The real platform share
// backend replaces this at the presentation phase. Precedent: AudioSystem / CameraManager shells.

using System.Collections.Generic;

/// <summary>Fluent share-request builder mirroring the NativeShare plugin's chained API.</summary>
public class NativeShare
{
    readonly List<string> _files = new();
    string _subject = "";
    string _text = "";
    string _title = "";

    public NativeShare AddFile(string filePath, string mime = null) { _files.Add(filePath); return this; }
    public NativeShare SetSubject(string subject) { _subject = subject; return this; }
    public NativeShare SetText(string text) { _text = text; return this; }
    public NativeShare SetTitle(string title) { _title = title; return this; }

    public void Share()
    {
        CosmicShore.Engine.Debug.Log(
            $"[NativeShare] (headless no-op) subject='{_subject}', files=[{string.Join(", ", _files)}]");
    }
}
