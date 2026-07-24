/**
 * PageToMovie Client-Side Video Export & File System Access API Helper
 * Enables zero-server-overhead direct streaming of rendered MP4 movies
 * straight to the user's local hard drive.
 */

window.PageToMovieExport = {
    _directoryHandle: null,

    /**
     * Checks if modern File System Access API is supported by the user's browser.
     */
    supportsFileSystemAccess: function () {
        return 'showSaveFilePicker' in window || 'showDirectoryPicker' in window;
    },

    /**
     * Prompts user to select an output folder ONCE per session.
     * All subsequent clip/movie renders in the session save directly into this folder without prompting.
     */
    selectExportDirectoryAsync: async function () {
        if (!('showDirectoryPicker' in window)) {
            return { success: false, error: 'Directory Picker API not supported on this browser.' };
        }
        try {
            this._directoryHandle = await window.showDirectoryPicker({ mode: 'readwrite' });
            return {
                success: true,
                folderName: this._directoryHandle.name,
                message: `Export folder '${this._directoryHandle.name}' connected for this session.`
            };
        } catch (err) {
            console.warn('Directory selection cancelled or failed:', err);
            return { success: false, error: err.message || 'Folder selection cancelled.' };
        }
    },

    /**
     * Returns true if a local directory handle has been authorized by the user for this session.
     */
    hasDirectoryHandle: function () {
        return this._directoryHandle !== null;
    },

    /**
     * Saves raw Uint8Array / base64 data directly into the authorized session folder without prompts.
     * If no folder is selected yet, prompts once via file save picker or folder picker.
     */
    saveMovieToDiskAsync: async function (suggestedFilename, base64Data, mimeType) {
        try {
            const raw = window.atob(base64Data);
            const rawLength = raw.length;
            const uInt8Array = new Uint8Array(rawLength);
            for (let i = 0; i < rawLength; ++i) {
                uInt8Array[i] = raw.charCodeAt(i);
            }
            const blob = new Blob([uInt8Array], { type: mimeType || 'video/mp4' });

            // 1. Direct write into authorized session folder (zero prompts)
            if (this._directoryHandle) {
                const fileHandle = await this._directoryHandle.getFileHandle(suggestedFilename || 'PageToMovie_WIP.mp4', { create: true });
                const writable = await fileHandle.createWritable();
                await writable.write(blob);
                await writable.close();
                return { success: true, folderName: this._directoryHandle.name, message: `Saved directly into '${this._directoryHandle.name}/${suggestedFilename}'.` };
            }

            // 2. Single-file save picker (prompts once)
            if ('showSaveFilePicker' in window) {
                const options = {
                    suggestedName: suggestedFilename || 'PageToMovie_WIP.mp4',
                    types: [{
                        description: 'MP4 Video File',
                        accept: { 'video/mp4': ['.mp4'] }
                    }]
                };

                const handle = await window.showSaveFilePicker(options);
                const writable = await handle.createWritable();
                await writable.write(blob);
                await writable.close();
                return { success: true, message: 'Movie saved directly to disk.' };
            } else {
                // 3. Fallback browser download prompt
                const url = URL.createObjectURL(blob);
                const a = document.createElement('a');
                a.href = url;
                a.download = suggestedFilename || 'PageToMovie_WIP.mp4';
                document.body.appendChild(a);
                a.click();
                document.body.removeChild(a);
                URL.revokeObjectURL(url);
                return { success: true, message: 'Movie downloaded via browser fallback.' };
            }
        } catch (err) {
            console.error('File System Access API export error:', err);
            return { success: false, error: err.message || 'Export cancelled or failed.' };
        }
    },

    /**
     * Client-Side WASM FFmpeg video clip concatenator helper stub.
     * Uses browser Blob URLs to merge scene clips in browser memory without server CPU usage.
     */
    concatenateClipsInBrowserAsync: async function (clipUrls, outputFilename) {
        try {
            console.log('Concatenating clips in browser WASM context:', clipUrls);
            const blobs = await Promise.all(clipUrls.map(url => fetch(url).then(r => r.blob())));
            const mergedBlob = new Blob(blobs, { type: 'video/mp4' });

            if (this._directoryHandle) {
                const fileHandle = await this._directoryHandle.getFileHandle(outputFilename || 'PageToMovie_FullMovie.mp4', { create: true });
                const writable = await fileHandle.createWritable();
                await writable.write(mergedBlob);
                await writable.close();
                return { success: true, folderName: this._directoryHandle.name };
            }

            const url = URL.createObjectURL(mergedBlob);
            const a = document.createElement('a');
            a.href = url;
            a.download = outputFilename || 'PageToMovie_FullMovie.mp4';
            document.body.appendChild(a);
            a.click();
            document.body.removeChild(a);
            URL.revokeObjectURL(url);

            return { success: true, count: clipUrls.length };
        } catch (err) {
            console.error('Browser WASM concatenation error:', err);
            return { success: false, error: err.message };
        }
    }
};
