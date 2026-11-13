mergeInto(LibraryManager.library, {
  ToabaPageLanguage: function () {
    var language = navigator.language || '';
    var size = lengthBytesUTF8(language) + 1;
    var buffer = _malloc(size);
    stringToUTF8(language, buffer, size);
    return buffer;
  },
});
