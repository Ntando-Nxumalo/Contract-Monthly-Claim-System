// Please see documentation at https://learn.microsoft.com/aspnet/core/client-side/bundling-and-minification
// for details on configuring this project to bundle and minify static web assets.

// Global UI initializers
document.addEventListener('DOMContentLoaded', () => {
  // Bootstrap 5 tooltips
  if (window.bootstrap) {
    const tooltipTriggerList = [].slice.call(document.querySelectorAll('[data-bs-toggle="tooltip"]'));
    tooltipTriggerList.map(el => new bootstrap.Tooltip(el));
  }

  // Button loading state helper (opt-in via data-loading-text)
  document.querySelectorAll('form').forEach(form => {
    form.addEventListener('submit', () => {
      form.querySelectorAll('button[type="submit"]').forEach(btn => {
        const original = btn.innerHTML;
        if (!btn.dataset.originalHtml) btn.dataset.originalHtml = original;
        if (btn.dataset.loadingText) {
          btn.innerHTML = btn.dataset.loadingText;
        }
        btn.disabled = true;
      });
    });
  });
});
