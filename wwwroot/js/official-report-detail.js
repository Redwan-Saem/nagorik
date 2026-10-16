(() => {
    const photo = document.getElementById('official-report-photo');
    if (!photo) return;
    const unavailable = () => {
        document.getElementById('official-photo-preview').hidden = true;
        document.getElementById('official-photo-unavailable').hidden = false;
    };
    photo.addEventListener('error', unavailable);
    if (photo.complete && photo.naturalWidth === 0) unavailable();
})();
