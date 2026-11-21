// Browser-side helper for the WebGL player (Docs/WEBGL_TOYBOX.md): lets a KEY press start FMOD's audio.
// Imported by WebAudioResume.cs with [DllImport("__Internal")]; the .meta enables it for WebGL only.
//
// Browsers start an AudioContext suspended until the page gets a user gesture. FMOD 2.03.13's HTML5
// outputs (html5/lib/3.1.39) resume theirs from 'click' and 'touchend' listeners on window only, so a
// player who only uses the keyboard (or whose host page focused the game for them) stays silent. A
// keydown is a user gesture too: this calls the same resume function FMOD's own listeners call, while
// FMOD is still waiting for one (Module.mInputRegistered), then removes itself. The names are FMOD's
// internals; if a later FMOD drops them this does nothing and the click/touch path still works.
mergeInto(LibraryManager.library, {
  FrogletResumeAudioOnKeydown: function () {
    if (Module.frogletKeydownResume) return;
    var win = Module.window ? Module.window : window;
    var onKeydown = function () {
      if (!Module.mInputRegistered) return;   // FMOD has not started yet, or has already resumed
      var resume = (Module._as_script_node && Module["OutputWebAudio_resumeAudio"])
        || Module["OutputAudioWorklet_resumeAudio"]
        || Module["OutputWebAudio_resumeAudio"];
      if (typeof resume !== "function") return;
      resume();
      if (!Module.mInputRegistered) win.removeEventListener("keydown", onKeydown, true);
    };
    Module.frogletKeydownResume = onKeydown;
    win.addEventListener("keydown", onKeydown, true);
  },
});
