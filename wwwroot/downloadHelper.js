function downloadFile(filename, contentType, data) {
    const blob = new Blob([new Uint8Array(data)], { type: contentType });
    const link = document.createElement("a");
    link.href = URL.createObjectURL(blob);
    link.download = filename;
    document.body.appendChild(link);
    link.click();
    document.body.removeChild(link);
}