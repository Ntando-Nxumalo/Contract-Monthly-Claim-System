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

  // Lightweight claim search/filter inputs
  document.querySelectorAll('[data-claims-search]').forEach(input => {
    const selectorList = (input.dataset.claimsSearch || '')
      .split(',')
      .map(s => s.trim())
      .filter(Boolean);
    if (selectorList.length === 0) return;

    const emptyTargets = (input.dataset.emptyState || '')
      .split(',')
      .map(s => s.trim())
      .filter(Boolean);

    const filterItems = () => {
      const query = input.value.trim().toLowerCase();
      let matches = 0;

      selectorList.forEach(sel => {
        document.querySelectorAll(sel).forEach(item => {
          if (item.dataset.emptyRow === 'true') {
            return;
          }
          const haystack = (item.dataset.search || item.textContent || '').toLowerCase();
          const isMatch = !query || haystack.includes(query);
          item.classList.toggle('d-none', !isMatch);
          if (isMatch) {
            matches++;
          }
        });
      });

      const shouldShowEmpty = query.length > 0 && matches === 0;

      if (emptyTargets.length) {
        emptyTargets.forEach(sel => {
          document.querySelectorAll(sel).forEach(el => {
            el.classList.toggle('d-none', !shouldShowEmpty);
          });
        });
      }
    };

    input.addEventListener('input', filterItems);
    filterItems();
  });
});
