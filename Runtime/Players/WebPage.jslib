mergeInto(LibraryManager.library, {
  ToabaPageNavigate: function (url) {
    window.location.assign(UTF8ToString(url));
  },

  ToabaPageReplace: function (url) {
    window.history.replaceState(null, '', UTF8ToString(url));
  },
});
