mergeInto(LibraryManager.library, {
  SimBarGetPixelRatio: function () {
    return window.devicePixelRatio || 1;
  }
});
