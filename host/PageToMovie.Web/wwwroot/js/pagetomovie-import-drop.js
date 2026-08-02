/**
 * Book import drop zone: prevent browser from navigating to dropped files,
 * and hand the File off to the Blazor InputFile so OnChange fires.
 */
(function (global) {
  function prevent(e) {
    e.preventDefault();
    e.stopPropagation();
  }

  /**
   * @param {HTMLElement} zoneEl  drop target (label / wrapper)
   * @param {HTMLInputElement} inputEl  Blazor InputFile's underlying <input type="file">
   */
  function attach(zoneEl, inputEl) {
    if (!zoneEl || !inputEl) return;
    if (zoneEl.__ptmDropBound) return;
    zoneEl.__ptmDropBound = true;

    ["dragenter", "dragover", "dragleave", "drop"].forEach(function (type) {
      zoneEl.addEventListener(type, prevent, false);
    });

    zoneEl.addEventListener(
      "drop",
      function (e) {
        prevent(e);
        var files = e.dataTransfer && e.dataTransfer.files;
        if (!files || files.length === 0) return;
        try {
          var dt = new DataTransfer();
          dt.items.add(files[0]);
          inputEl.files = dt.files;
          // Blazor InputFile listens for change on the input
          inputEl.dispatchEvent(new Event("change", { bubbles: true }));
        } catch (err) {
          console.warn("ptmImportDrop: failed to assign dropped file", err);
        }
      },
      false
    );
  }

  /**
   * Find the file input inside the zone and bind. Safe to call after re-render.
   * @param {string} zoneSelector
   */
  function attachBySelector(zoneSelector) {
    var zone = document.querySelector(zoneSelector);
    if (!zone) return false;
    var input = zone.querySelector('input[type="file"]');
    if (!input) return false;
    attach(zone, input);
    return true;
  }

  global.ptmImportDrop = {
    attach: attach,
    attachBySelector: attachBySelector,
  };
})(window);
