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

    const batchThreshold = 50 * 1024 * 1024;
    let batch = [];
    let batchSize = 0;

    for (const { file, relativePath } of fileList) {
        batch.push({ file, relativePath });
        batchSize += file.size;

        if (batchSize >= batchThreshold) {
            const batchBytes = batch.reduce((s, b) => s + b.file.size, 0);
            await sendBatch(batch, dotNetHelper);
            completedFiles += batch.length;
            completedBytes += batchBytes;
            await dotNetHelper.invokeMethodAsync('OnUploadByteProgress', completedFiles, totalFiles, completedBytes, totalBytes);
            batch = [];
            batchSize = 0;
        }
    }

    if (batch.length > 0) {
        const batchBytes = batch.reduce((s, b) => s + b.file.size, 0);
        await sendBatch(batch, dotNetHelper);
        completedFiles += batch.length;
        completedBytes += batchBytes;
        await dotNetHelper.invokeMethodAsync('OnUploadByteProgress', completedFiles, totalFiles, completedBytes, totalBytes);
    }

    await dotNetHelper.invokeMethodAsync('OnUploadCompleted');
}

async function sendBatch(batch, dotNetHelper) {
    const currentPath = await dotNetHelper.invokeMethodAsync('GetCurrentPath');
    const currentBucket = await dotNetHelper.invokeMethodAsync('GetCurrentBucket');

    const byDir = new Map();
    for (const { file, relativePath } of batch) {
        const dirPart = relativePath.includes('/')
            ? relativePath.substring(0, relativePath.lastIndexOf('/'))
            : '';
        const uploadPath = dirPart
            ? (currentPath ? currentPath + '/' + dirPart : dirPart)
            : currentPath;

        if (!byDir.has(uploadPath)) byDir.set(uploadPath, []);
        byDir.get(uploadPath).push(file);
    }

    for (const [uploadPath, files] of byDir) {
        const fd = new FormData();
        for (const file of files) {
            fd.append('files', file, file.name);
        }
        const encodedBucket = encodeURIComponent(currentBucket);
        const encodedPath = uploadPath ? uploadPath.split('/').map(encodeURIComponent).join('/') : '';
        const response = await fetch(`/api/files/upload/${encodedBucket}/${encodedPath}`, {
            method: 'POST',
            body: fd
        });
        if (!response.ok) {
            const text = await response.text();
            console.error('Upload failed:', text);
            await dotNetHelper.invokeMethodAsync('OnUploadError', `Upload failed: ${response.statusText}`);
        }
    }
}
