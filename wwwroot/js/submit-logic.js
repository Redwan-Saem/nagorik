function classifyResponse(status) {
  if (status >= 200 && status < 300) return 'success';
  if (status === 400 || status === 422) return 'validation';
  return 'retryable';
}

function extractFieldErrors(problemJson) {
  const out = {};
  const errs = (problemJson && problemJson.errors) || {};

  for (const k of Object.keys(errs)) {
    out[k] = errs[k].join(' ');
  }

  return out;
}

if (typeof module !== 'undefined')
  module.exports = { classifyResponse, extractFieldErrors };