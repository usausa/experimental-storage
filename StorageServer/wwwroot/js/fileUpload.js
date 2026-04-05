export function initDropZone(dropZoneElement, inputElement, dotNetHelper) {
    function preventDefaults(e) {
        e.preventDefault();
        e.stopPropagation();
    }

    function highlight() {
        dropZoneElement.classList.add('drop-zone-active');
    }

    function unhighlight() {
        dropZoneElement.classList.remove('drop-zone-active');
    }

    ['dragenter', 'dragover'].forEach(evt =>
        dropZoneElement.addEventListener(evt, e => { preventDefaults(e); highlight(); })
    );
    ['dragleave', 'drop'].forEach(evt =>
        dropZoneElement.addEventListener(evt, e => { preventDefaults(e); unhighlight(); })
    );

    dropZoneElement.addEventListener('drop', async e => {
        const items = e.dataTransfer.items;
        if (items) {
            const entries = [];
            for (const item of items) {
                const entry = item.webkitGetAsEntry?.();
                if (entry) entries.push(entry);
            }
            if (entries.length > 0) {
                const files = await collectFiles(entries);
                if (files.length > 0) {
                    await uploadFiles(files, dotNetHelper);
                }
                return;
            }
        }

        const files = e.dataTransfer.files;
        if (files.length > 0) {
            const fileList = [];
            for (const file of files) {
                fileList.push({ file, relativePath: file.name });
            }
            await uploadFiles(fileList, dotNetHelper);
        }
    });

    inputElement.addEventListener('change', async () => {
        const files = inputElement.files;
        if (files.length > 0) {
            const fileList = [];
            for (const file of files) {
                fileList.push({ file, relativePath: file.name });
            }
            await uploadFiles(fileList, dotNetHelper);
            inputElement.value = '';
        }
    });
}

async function collectFiles(entries) {
    const files = [];

    async function readEntry(entry, pathPrefix) {
        if (entry.isFile) {
            const file = await new Promise(resolve => entry.file(resolve));
            files.push({ file, relativePath: pathPrefix + file.name });
        } else if (entry.isDirectory) {
            const reader = entry.createReader();
            const subEntries = await new Promise(resolve => reader.readEntries(resolve));
            for (const subEntry of subEntries) {
                await readEntry(subEntry, pathPrefix + entry.name + '/');
            }
        }
    }

    for (const entry of entries) {
        await readEntry(entry, '');
    }
    return files;
}

async function uploadFiles(fileList, dotNetHelper) {
    // Check for duplicates and get confirmation
    const fileNames = fileList.map(f => {
        const rp = f.relativePath;
        return rp.includes('/') ? rp : f.file.name;
    });
    const proceed = await dotNetHelper.invokeMethodAsync('CheckDuplicates', fileNames);
    if (!proceed) return;

    const totalFiles = fileList.length;
    const totalBytes = fileList.reduce((sum, f) => sum + f.file.size, 0);
    let completedFiles = 0;
    let completedBytes = 0;

    await dotNetHelper.invokeMethodAsync('OnUploadStarted', totalFiles, totalBytes);

    const currentPath = await dotNetHelper.invokeMethodAsync('GetCurrentPath');
    const currentBucket = await dotNetHelper.invokeMethodAsync('GetCurrentBucket');

    for (const { file, relativePath } of fileList) {
        const dirPart = relativePath.includes('/')
            ? relativePath.substring(0, relativePath.lastIndexOf('/'))
            : '';
        const uploadPath = dirPart
            ? (currentPath ? currentPath + '/' + dirPart : dirPart)
            : currentPath;

        try {
            await uploadFileWithProgress(
                file, currentBucket, uploadPath, dotNetHelper,
                completedFiles, totalFiles, completedBytes, totalBytes
            );
        } catch (e) {
            await dotNetHelper.invokeMethodAsync('OnUploadError', `Upload failed: ${e.message}`);
            await dotNetHelper.invokeMethodAsync('OnUploadCompleted');
            return;
        }

        completedFiles++;
        completedBytes += file.size;
        await dotNetHelper.invokeMethodAsync('OnUploadByteProgress',
            completedFiles, totalFiles, completedBytes, totalBytes, '');
    }

    await dotNetHelper.invokeMethodAsync('OnUploadCompleted');
}

function uploadFileWithProgress(file, bucket, uploadPath, dotNetHelper,
    completedFiles, totalFiles, completedBytes, totalBytes) {
    return new Promise((resolve, reject) => {
        const fd = new FormData();
        fd.append('files', file, file.name);

        const encodedBucket = encodeURIComponent(bucket);
        const encodedPath = uploadPath
            ? uploadPath.split('/').map(encodeURIComponent).join('/')
            : '';
        const url = encodedPath
            ? `/api/files/upload/${encodedBucket}/${encodedPath}`
            : `/api/files/upload/${encodedBucket}`;

        const xhr = new XMLHttpRequest();

        let lastProgressTime = 0;
        xhr.upload.addEventListener('progress', e => {
            if (!e.lengthComputable) return;
            const now = Date.now();
            if (now - lastProgressTime < 100) return; // max 10 updates/sec
            lastProgressTime = now;
            dotNetHelper.invokeMethodAsync('OnUploadByteProgress',
                completedFiles, totalFiles,
                completedBytes + e.loaded, totalBytes,
                file.name);
        });

        xhr.addEventListener('load', () => {
            if (xhr.status >= 200 && xhr.status < 300) {
                resolve();
            } else {
                reject(new Error(xhr.statusText || `HTTP ${xhr.status}`));
            }
        });

        xhr.addEventListener('error', () => reject(new Error('Network error')));
        xhr.addEventListener('abort', () => reject(new Error('Upload aborted')));

        xhr.open('POST', url);
        xhr.send(fd);
    });
}
