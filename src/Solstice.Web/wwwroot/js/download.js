// 触发浏览器下载。Blazor 不能直接写文件，必须经 <a download>。
// 字节以 base64 传入（JSON 序列化 byte[] 也是 base64，这里显式转换更清楚）。
window.solsticeDownload = function (fileName, base64) {
    const binary = atob(base64);
    const bytes = new Uint8Array(binary.length);
    for (let i = 0; i < binary.length; i++) {
        bytes[i] = binary.charCodeAt(i);
    }
    const blob = new Blob([bytes], { type: 'application/octet-stream' });
    const url = URL.createObjectURL(blob);
    const a = document.createElement('a');
    a.href = url;
    a.download = fileName;
    document.body.appendChild(a);
    a.click();
    a.remove();
    setTimeout(function () { URL.revokeObjectURL(url); }, 2000);
};
