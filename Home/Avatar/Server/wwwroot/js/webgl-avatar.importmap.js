// /wwwroot/js/webgl-avatar.importmap.js
(() => {
  if (window.__threeImportMapAdded) return; // avoid duplicate import-map warnings
  window.__threeImportMapAdded = true;

  const map = {
    imports: {
      "three": "https://cdn.jsdelivr.net/npm/three@0.160.0/build/three.module.js",
      "three/addons/": "https://cdn.jsdelivr.net/npm/three@0.160.0/examples/jsm/"
    }
  };

  const s = document.createElement('script');
  s.type = 'importmap';
  s.textContent = JSON.stringify(map);
  document.currentScript.after(s);

  import('/js/webgl-avatar.module.js')
    .catch(e => console.error('[webgl-avatar] failed to load module:', e));
})();
