// 全屏拖放区：把拖进来的文件交给 Blazor 的 InputFile，并触发 change。
// 只在浏览器端生效；没有 JS 时仍然可以点击选择文件。
window.solsticeDrop = {
    attach: function (zoneEl) {
        if (!zoneEl || zoneEl.dataset.solsticeAttached === '1') {
            return;
        }
        const inputEl = zoneEl.querySelector('input[type=file]');
        if (!inputEl) {
            return;
        }
        zoneEl.dataset.solsticeAttached = '1';

        const over = (on) => zoneEl.classList.toggle('is-over', on);

        ['dragenter', 'dragover'].forEach(function (name) {
            zoneEl.addEventListener(name, function (ev) {
                ev.preventDefault();
                ev.stopPropagation();
                over(true);
            });
        });

        ['dragleave', 'dragend'].forEach(function (name) {
            zoneEl.addEventListener(name, function (ev) {
                ev.preventDefault();
                over(false);
            });
        });

        zoneEl.addEventListener('drop', function (ev) {
            ev.preventDefault();
            ev.stopPropagation();
            over(false);
            const dt = ev.dataTransfer;
            if (dt && dt.files && dt.files.length > 0) {
                inputEl.files = dt.files;
                inputEl.dispatchEvent(new Event('change', { bubbles: true }));
            }
        });

        zoneEl.addEventListener('click', function (ev) {
            if (ev.target !== inputEl) {
                inputEl.click();
            }
        });
    }
};
