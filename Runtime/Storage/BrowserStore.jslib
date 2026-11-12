// localStorage can throw, for example in an iframe whose storage is blocked or when it's full, so
// each call reports failure for BrowserStore.cs to raise.
mergeInto(LibraryManager.library, {
  ToabaStorageRead: function (key, failed) {
    var value;
    try {
      value = localStorage.getItem(UTF8ToString(key));
    } catch (e) {
      HEAP32[failed >> 2] = 1;
      return 0;
    }
    HEAP32[failed >> 2] = 0;
    if (value === null) return 0;
    var size = lengthBytesUTF8(value) + 1;
    var buffer = _malloc(size);
    stringToUTF8(value, buffer, size);
    return buffer;
  },

  ToabaStorageWrite: function (key, value) {
    try {
      localStorage.setItem(UTF8ToString(key), UTF8ToString(value));
      return 1;
    } catch (e) {
      return 0;
    }
  },

  ToabaStorageDelete: function (key) {
    try {
      localStorage.removeItem(UTF8ToString(key));
      return 1;
    } catch (e) {
      return 0;
    }
  },
});
