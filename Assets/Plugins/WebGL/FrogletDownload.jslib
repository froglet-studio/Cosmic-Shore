// Browser-side helper for the WebGL player (Docs/WEBGL_TOYBOX.md). Imported from C# with
// [DllImport("__Internal")] under #if UNITY_WEBGL && !UNITY_EDITOR; the .meta enables it for WebGL only.
mergeInto(LibraryManager.library, {
  // Hands `text` to the browser as a download named `filename`: a Blob behind a temporary object URL,
  // clicked through a temporary <a download>. Returns 1 when the download was started, 0 on failure.
  // Pointers are made unsigned first: above a 2 GB heap (the build allows 4 GB) they arrive negative.
  FrogletDownloadText: function (filenamePtr, textPtr, mimePtr) {
    try {
      var filename = UTF8ToString(filenamePtr >>> 0) || "download.txt";
      var text = UTF8ToString(textPtr >>> 0);
      var mime = UTF8ToString(mimePtr >>> 0) || "application/octet-stream";
      var url = URL.createObjectURL(new Blob([text], { type: mime }));
      var link = document.createElement("a");
      link.href = url;
      link.download = filename;
      link.rel = "noopener";
      link.style.display = "none";
      document.body.appendChild(link);
      link.click();
      document.body.removeChild(link);
      // Revoked later, not now: some browsers read the URL after click() returns.
      setTimeout(function () { URL.revokeObjectURL(url); }, 60000);
      return 1;
    } catch (e) {
      console.warn("FrogletDownloadText: " + e);
      return 0;
    }
  },
});
