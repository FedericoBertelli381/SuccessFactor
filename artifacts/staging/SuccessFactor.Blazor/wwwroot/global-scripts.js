window.successFactorDownloadFile = (fileName, contentType, base64Content) => {
    const bytes = Uint8Array.from(atob(base64Content), char => char.charCodeAt(0));
    const blob = new Blob([bytes], { type: contentType || "application/octet-stream" });
    const url = URL.createObjectURL(blob);
    const anchor = document.createElement("a");

    anchor.href = url;
    anchor.download = fileName || "export.csv";
    anchor.style.display = "none";
    document.body.appendChild(anchor);
    anchor.click();
    anchor.remove();
    URL.revokeObjectURL(url);
};
