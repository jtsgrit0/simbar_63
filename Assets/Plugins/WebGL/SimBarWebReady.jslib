mergeInto(LibraryManager.library, {
  SimBar_GetCanvasPixelRatio: function () {
    var canvas = Module["canvas"];
    return canvas && canvas.clientWidth > 0 ? canvas.width / canvas.clientWidth : (window.devicePixelRatio || 1);
  },
  SimBar_ReportGameReady: function () {
    requestAnimationFrame(function () {
      requestAnimationFrame(function () {
        window.simbarGameReadyAt = performance.now();
        window.dispatchEvent(new Event("simbar-game-ready"));
      });
    });
  }
});
