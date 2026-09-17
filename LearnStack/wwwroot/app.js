// Toggle dark mode class on the <html> element so custom CSS can respond
window.setDarkMode = function (isDark) {
    if (isDark) {
        document.documentElement.classList.add('dark');
    } else {
        document.documentElement.classList.remove('dark');
    }
};

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

// Detect the browser's IANA time zone (e.g. "America/Los_Angeles") so we can
// store it against the user's profile for timezone-aware date display.
window.getBrowserTimeZone = function () {
    try {
        return Intl.DateTimeFormat().resolvedOptions().timeZone;
    } catch {
        return null;
    }
};

