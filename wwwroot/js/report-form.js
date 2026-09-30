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

    let isSubmitting = false;

    async function sendReport() {

      if (isSubmitting) return;

      isSubmitting = true;

      $('submit-report').disabled = true;
      $('retry-submit').style.display = 'none';
      $('submit-error').style.display = 'none';

      try {

        const res = await fetch('/api/reports', {
          method: 'POST',
          body: new FormData($('report-form')),
          headers: {
            'RequestVerificationToken':
              document.querySelector(
                'input[name="__RequestVerificationToken"]'
              ).value
          }
        });

        const kind = classifyResponse(res.status);

        if (kind === 'success') {

          const j = await res.json();

          window.location.href =
            '/Reports/Confirmation/' + j.id;

          return;
        }

        if (kind === 'validation') {

          const fe = extractFieldErrors(await res.json());

          if (fe.Photo) {
            $('Photo-error').textContent = fe.Photo;
          }

          if (fe.Location) {
            $('location-error').textContent = fe.Location;
          }

          showError(
            'Please fix the highlighted problems and submit again.'
          );

        } else {

          showError(
            'We could not send your report. Your information is saved on this page. Please retry.'
          );
        }

      } catch {

        showError(
          'No connection. Your information is saved on this page. Please retry.'
        );
      }

      isSubmitting = false;

      $('submit-report').disabled = false;
      $('retry-submit').style.display = 'inline-block';
    }

    function showError(msg) {
      $('submit-error').textContent = msg;
      $('submit-error').style.display = 'block';
    }

    $('report-form').addEventListener('submit', e => {
      e.preventDefault();
      sendReport();
    });

    $('retry-submit').addEventListener('click', sendReport);

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