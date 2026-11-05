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
         (s.addressText || '').trim().length > 0 &&
         s.addressText.length <= 300;
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

      $('AddressText-error').textContent = s.addressText.length > 300
        ? 'Use an address or landmark of 300 characters or fewer.'
        : '';

      $('submit-report').disabled = isSubmitting || !isFormValid(s);
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

      if (isSubmitting || !isFormValid(state())) return;

      isSubmitting = true;

      $('submit-report').disabled = true;
      $('retry-submit').style.display = 'none';
      $('submit-error').style.display = 'none';

      try {

        const res = await fetch('/api/reports', {
          method: 'POST',
          // credentials: 'include' ensures the auth_token cookie is sent with
          // this cross-origin-safe same-origin request so [Authorize] works.
          credentials: 'include',
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
          const problem = await res.json().catch(() => null);
          const errors = extractFieldErrors(problem);
          const fields = {
            Category: 'Category-error',
            Description: 'Description-error',
            Photo: 'Photo-error',
            AddressText: 'AddressText-error',
            Latitude: 'location-error',
            Longitude: 'location-error',
            Location: 'location-error'
          };
          for (const [field, id] of Object.entries(fields)) {
            if (errors[field]) $(id).textContent = errors[field];
          }
          showError(Object.values(errors).join(' ') ||
            'The server rejected this submission. Reload the form if the problem continues.');
        } else {

          $('retry-submit').style.display = 'inline-block';
          showError(
            'We could not send your report. Your information is saved on this page. Please retry.'
          );
        }

      } catch {

        $('retry-submit').style.display = 'inline-block';
        showError(
          'No connection. Your information is saved on this page. Please retry.'
        );
      }

      isSubmitting = false;

      $('submit-report').disabled = !isFormValid(state());
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
