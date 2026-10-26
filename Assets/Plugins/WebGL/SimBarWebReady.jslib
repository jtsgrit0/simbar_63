mergeInto(LibraryManager.library, {
  SimBar_ReportGameReady: function () {
    requestAnimationFrame(function () {
      requestAnimationFrame(function () {
        window.simbarGameReadyAt = performance.now();
        window.dispatchEvent(new Event("simbar-game-ready"));
      });
    });
  }
});
