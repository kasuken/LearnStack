// File download helper
window.downloadFile = function (fileName, base64Content, contentType) {
    const linkSource = `data:${contentType};base64,${base64Content}`;
    const downloadLink = document.createElement('a');
    downloadLink.href = linkSource;
    downloadLink.download = fileName;
    downloadLink.click();
};

// Force a full browser navigation (bypasses Blazor enhanced navigation)
window.forceNavigate = function (url) {
    window.location.href = url;
};

