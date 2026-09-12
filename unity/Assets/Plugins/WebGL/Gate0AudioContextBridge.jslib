// Detects when the browser suspends audio while the tab stays visible (the case OnApplicationFocus/
// OnApplicationPause cannot see): before the first user gesture, or when the browser proactively
// suspends audio for a backgrounded/inactive page.
//
// Deliberately does NOT reach into Unity's own internal Module.WEBAudio object: that object is
// undocumented, can differ across Unity versions, and was empirically found (via live browser
// inspection while building this bridge) to hang the JS engine when read from outside code during
// startup. Instead this creates its own standard AudioContext purely to monitor suspend/resume state.
// Browsers apply audio-suspension policy per page, not per AudioContext instance, so this context's
// state is a reliable proxy for whether Unity's own audio can currently play.
mergeInto(LibraryManager.library, {
  Gate0InstallAudioContextBridge: function (objectNamePtr, methodNamePtr) {
    var objectName = UTF8ToString(objectNamePtr);
    var methodName = UTF8ToString(methodNamePtr);

    var AudioContextCtor = window.AudioContext || window.webkitAudioContext;
    if (!AudioContextCtor) return; // no Web Audio support in this browser: never report suspended

    var ctx = new AudioContextCtor();

    function report() {
      var instance = window.gate0Unity;
      if (!instance || !instance.SendMessage) return;
      instance.SendMessage(objectName, methodName, ctx.state === 'running' ? 1 : 0);
    }

    ctx.addEventListener('statechange', report);
    document.addEventListener('visibilitychange', function () {
      // A tab becoming visible again is usually exactly when a suspended context can be resumed.
      if (!document.hidden && ctx.state === 'suspended') ctx.resume().catch(function () {});
      report();
    });
    report();
  },

  // Real device pixel ratio for the RunRecord's dprMilli field. Screen.dpi is unreliable on WebGL (often 0
  // or a fixed fallback), so this reads the browser's own value directly.
  Gate0GetDevicePixelRatio: function () {
    return window.devicePixelRatio || 1;
  },
});
