/**
 * PageToMovie Client-Side Video Export & File System Access API Helper
 * Enables zero-server-overhead direct streaming of rendered MP4 movies
 * straight to the user's local hard drive.
 */

window.PageToMovieExport = {
    /**
     * Checks if modern File System Access API is supported by the user's browser.
     */
    supportsFileSystemAccess: function () {
        return 'showSaveFilePicker' in window;
    },

    /**
     * Saves raw video Uint8Array / Blob directly to user's selected disk location using File System Access API.
     * Fallbacks to standard browser download prompt if File System Access API is unavailable.
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
                // Fallback standard download link
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
            // Fetches clip streams and merges blob segments
            const blobs = await Promise.all(clipUrls.map(url => fetch(url).then(r => r.blob())));
            const mergedBlob = new Blob(blobs, { type: 'video/mp4' });

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
