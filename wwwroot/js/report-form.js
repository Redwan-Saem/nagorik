function validateDescription(text) {
  const t = (text || '').trim();

  if (t.length === 0) {
    return 'Please describe the issue.';
  }

  if (t.length > 500) {
    return 'Description must be 500 characters or fewer.';
  }

  return null;
}

function validatePhotoFile(file) {
  if (!file) {
    return 'Please add a photo.';
  }

  const ext = (file.name.split('.').pop() || '').toLowerCase();

  if (!['jpg', 'jpeg', 'png'].includes(ext)) {
    return 'Only JPG and PNG photos are allowed.';
  }

  if (file.size > 5 * 1024 * 1024) {
    return 'The photo must be 5 MB or smaller.';
  }

  return null;
}

function isFormValid(s) {
  return !!s.category &&
         !validateDescription(s.description) &&
         !validatePhotoFile(s.photo) &&
         s.latitude !== '' &&
         s.latitude != null &&
         s.longitude !== '' &&
         s.longitude != null &&
         (s.addressText || '').trim().length > 0;
}

if (typeof document !== 'undefined') {

  document.addEventListener('DOMContentLoaded', () => {

    const $ = id => document.getElementById(id);

    const state = () => ({
      category: $('Category').value,
      description: $('Description').value,
      photo: $('Photo').files[0] || null,
      latitude: $('Latitude').value,
      longitude: $('Longitude').value,
      addressText: $('AddressText').value
    });

    function refresh() {
      const s = state();

      $('char-count').textContent = s.description.length;

      $('Category-error').textContent = '';

      $('Description-error').textContent =
        s.description.length
          ? (validateDescription(s.description) || '')
          : '';

      $('Photo-error').textContent =
        s.photo
          ? (validatePhotoFile(s.photo) || '')
          : '';

      $('submit-report').disabled = !isFormValid(s);
    }

    $('Category').addEventListener('change', refresh);

    $('Description').addEventListener('input', refresh);

    $('Photo').addEventListener('change', () => {

      const f = $('Photo').files[0];

      if (f && !validatePhotoFile(f)) {

        $('photo-preview').src = URL.createObjectURL(f);

        $('photo-preview').style.display = 'block';

        $('remove-photo').style.display = 'inline-block';

      } else {

        $('photo-preview').style.display = 'none';

        $('remove-photo').style.display = 'none';
      }

      refresh();
    });

    $('remove-photo').addEventListener('click', () => {

      $('Photo').value = '';

      $('photo-preview').style.display = 'none';

      $('remove-photo').style.display = 'none';

      refresh();
    });

    document.addEventListener('location-changed', refresh);

    $('report-form').addEventListener('submit', async e => {

      e.preventDefault();

      const fd = new FormData($('report-form'));

      const res = await fetch('/api/reports', {
        method: 'POST',
        body: fd,
        headers: {
          'RequestVerificationToken':
            document.querySelector(
              'input[name="__RequestVerificationToken"]'
            ).value
        }
      });

      if (res.ok) {

        const j = await res.json();

        window.location.href =
          '/Reports/Confirmation/' + j.id;

      } else {

        $('submit-error').textContent =
          'Something went wrong. Please check the form.';

        $('submit-error').style.display = 'block';
      }
    });

    refresh();
  });
}

if (typeof module !== 'undefined') {
  module.exports = {
    validateDescription,
    validatePhotoFile,
    isFormValid
  };
}