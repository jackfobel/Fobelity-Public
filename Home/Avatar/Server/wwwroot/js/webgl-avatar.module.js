// WebGL + Azure TTS (visemes) – Browser/ESM

import * as THREE from 'three';
import { GLTFLoader } from 'three/addons/loaders/GLTFLoader.js';
import { DRACOLoader } from 'three/addons/loaders/DRACOLoader.js';
import { MeshoptDecoder } from 'three/addons/libs/meshopt_decoder.module.js';

import { FBXLoader } from 'three/addons/loaders/FBXLoader.js';
import { retargetClip } from 'three/addons/utils/SkeletonUtils.js';

async function loadSignalR() {
  if (window.signalR) return window.signalR;
  try {
    const m = await import('https://unpkg.com/@microsoft/signalr@8.0.7/dist/esm/index.js');
    return m;
  } catch (e) {
    console.warn('[hub] ESM load failed; falling back to UMD…', e);
    await new Promise((res, rej) => {
      const s = document.createElement('script');
      s.src = 'https://unpkg.com/@microsoft/signalr@8.0.7/dist/browser/signalr.min.js';
      s.onload = res; s.onerror = rej; document.head.appendChild(s);
    });
    return window.signalR;
  }
}



(function runWhenReady(fn) {
  function ready() {
    return document.getElementById('avatarCanvas')
      && document.getElementById('btnSpeak')
      && document.getElementById('animSelect');
  }
  function tick() { ready() ? fn() : setTimeout(tick, 25); }
  document.readyState === 'loading'
    ? document.addEventListener('DOMContentLoaded', tick, { once: true })
    : tick();
})(function init() {

  // ------------------ TUNABLES ------------------
  const RISE = 0.22;   // how fast a morph rises toward target per frame (0..1)
  const DECAY = 0.10;  // how fast a morph decays toward 0 per frame (0..1)
  const HOLD_MS = 110; // time a viseme stays at peak before decaying
  const VOWEL_IDS = new Set([1, 2, 3, 4, 6]);
  const JAW_CANDIDATES = ['jawopen', 'jawOpen', 'jawforward', 'jawForward'];
  const BLINK_CANDIDATES = ['eyeBlinkLeft', 'eyeBlinkRight', 'blink', 'eyesClosed', 'eyeClose'];
  const BLINK_EVERY_MS = [2800, 4300];
  const BLINK_LEN_MS = 140;
  const ANIM_DIR = '/models/anims/';
  const ANIMS_TO_LOAD = ['Idle', 'Laughing', 'Pointing', 'Salute', 'StandingUp', 'Angry', 'SlideHipHopDance', 'Dying'];

  // --- Viseme intensity shaping (reduce extremes) ---
  const VISEME_INTENSITY = 0.75;   // global scale for all visemes (0..1). Try 0.65–0.85
  const GAMMA = 1.35;              // >1 compresses peaks; 1.2–1.6 is subtle

  // Per-shape soft caps (keeps things looking natural)
  const SHAPE_LIMITS = {
    jawOpen: 0.55,
    mouthFunnel: 0.60,
    mouthPucker: 0.60,
    mouthStretchLeft: 0.60,
    mouthStretchRight: 0.60,
    mouthClose: 0.75
  };
  // ------------------------------------------------

  const $ = id => document.getElementById(id);
  const log = m => { const el = $('log'); if (el) el.innerHTML += `${new Date().toLocaleTimeString()}  ${m}<br/>`; };

  console.log(`[webgl-avatar] init (Three r${THREE.REVISION})`);

  // --- Scene ---
  const canvas = $('avatarCanvas');
  const renderer = new THREE.WebGLRenderer({ canvas, antialias: true, alpha: true, preserveDrawingBuffer: false });
  const scene = new THREE.Scene();
  const camera = new THREE.PerspectiveCamera(35, canvas.clientWidth / canvas.clientHeight, 0.1, 100);
  camera.position.set(0, 1.6, 3);

  // Simple orbit (no extras)
  const orbit = (() => {
    let phi = 0, theta = 0.35, dist = 2, pivot = new THREE.Vector3(0, 1.4, 0);
    let dragging = false, lastX = 0, lastY = 0;

    canvas.addEventListener('pointerdown', e => { dragging = true; lastX = e.clientX; lastY = e.clientY; canvas.setPointerCapture(e.pointerId); });
    canvas.addEventListener('pointerup', e => { dragging = false; canvas.releasePointerCapture(e.pointerId); });
    canvas.addEventListener('pointermove', e => {
      if (!dragging) return;
      const dx = (e.clientX - lastX) * 0.005;
      const dy = (e.clientY - lastY) * 0.005;
      lastX = e.clientX; lastY = e.clientY;
      phi -= dx;
      theta = THREE.MathUtils.clamp(theta + dy, -1.2, 1.2);
    });
    canvas.addEventListener('wheel', e => { dist *= (1 + Math.sign(e.deltaY) * 0.08); dist = THREE.MathUtils.clamp(dist, 1.4, 8); });
    canvas.addEventListener('dblclick', () => { phi = 0; theta = 0.35; dist = 3; });

    return {
      update() {
        const x = pivot.x + dist * Math.cos(theta) * Math.sin(phi);
        const y = pivot.y + dist * Math.sin(theta);
        const z = pivot.z + dist * Math.cos(theta) * Math.cos(phi);
        camera.position.set(x, y, z);
        camera.lookAt(pivot);
      }
    };
  })();

  scene.add(new THREE.HemisphereLight(0xffffff, 0x222233, 1.0));
  const key = new THREE.DirectionalLight(0xffffff, 1.2);
  key.position.set(1, 2, 2);
  scene.add(key);

  const clock = new THREE.Clock();

  // --- background state (hoisted to avoid TDZ in resize) ---
  let bgMesh = null;   // camera-locked plane
  let bgTex = null;   // THREE.Texture
  const BG_DIST = 8;
  const BG_MARGIN = 1.08;


  let modelRoot = null;
  let skinnedTarget = null;   // first SkinnedMesh with a skeleton (for animation retarget)
  let morphedMeshes = [];     // [{ mesh, dict, influences, rev }]
  let morphEntries = [];      // [{ nameLower, mm, idx, meshNameLower }]
  let visemeCache = new Map();// id -> { mm, idx }
  let mixer = null; let clips = {}; // { name: THREE.AnimationAction }
  let animNames = [];         // loaded animation names (for UI)

  // activeTargets: key `${mm.uuid}:${idx}` -> { weight, untilTs, name }
  const activeTargets = new Map();

  // speaking TTL for HUD – avoids flicker back to "idle" between packets
  let speakingUntil = 0;



  function resize() {
    const w = canvas.clientWidth, h = canvas.clientHeight;
    if (canvas.width !== w || canvas.height !== h) {
      renderer.setSize(w, h, false);
      camera.aspect = w / h;
      camera.updateProjectionMatrix();
      if (bgTex) fitBackgroundToView();  // <- guard
    }
  }

  function shapeWeight(name, w) {
    // Global intensity then gentle compression (pow with gamma > 1)
    const scaled = Math.max(0, w * VISEME_INTENSITY);
    const compressed = Math.pow(scaled, GAMMA);
    const cap = SHAPE_LIMITS[name] ?? 1.0;
    return Math.min(compressed, cap);
  }


  function smoothToward(current, target, upRate, downRate) {
    return (target > current)
      ? Math.min(target, current + upRate)
      : Math.max(target, current - downRate);
  }

  // HUD (bottom-right overlay inside the stage)
  //const hud = (() => {
  //  const wrap = document.createElement('div');
  //  wrap.className = 'hud';
  //  wrap.innerHTML = `
  //    <div class="row"><div class="kv">Viseme</div><div id="hudVis" class="val">idle</div></div>
  //    <div style="height:6px"></div>
  //    <div class="kv">Driven morphs (this frame)</div>
  //    <div id="hudList" class="name small">none</div>
  //  `;

    //const stageEl =
    //  document.querySelector('.stage') ||
    //  (canvas && canvas.parentElement) ||
    //  document.body;

    //try { stageEl.appendChild(wrap); } catch { document.body.appendChild(wrap); }

    //const vis = wrap.querySelector('#hudVis');
    //const list = wrap.querySelector('#hudList');
    //return {
    //  setVisemeLabel(s) { vis.textContent = s; },
    //  setMorphList(names) { list.textContent = names.length ? names.join(', ') : 'none'; }
    //};
 // })();

  function render() {
    requestAnimationFrame(render);
    resize();

    const now = performance.now();

    // Per-frame target → influence smoothing
    const thisFrameNames = new Set();

    for (const mm of morphedMeshes) {
      const inf = mm.mesh.morphTargetInfluences; if (!inf) continue;

      for (let i = 0; i < inf.length; i++) {
        const key = `${mm.mesh.uuid}:${i}`;
        const tgt = activeTargets.get(key);
        const desired = (tgt && tgt.untilTs > now) ? (tgt.weight || 0) : 0;
        inf[i] = smoothToward(inf[i], desired, RISE, DECAY);
        if (desired > 0.15 && tgt && tgt.name) thisFrameNames.add(tgt.name);
        if (tgt && tgt.untilTs <= now) activeTargets.delete(key);
      }
    }

    // HUD
    //const speaking = now < speakingUntil;
    //hud.setVisemeLabel(speaking ? 'speaking' : 'idle');
    //hud.setMorphList([...thisFrameNames]);

    // Animations
    const dt = clock.getDelta();
    if (mixer) mixer.update(dt);

    // Camera
    orbit.update();

    renderer.render(scene, camera);
  }
  render();

  // --- GLB loader ---
  const loader = new GLTFLoader();
  loader.setMeshoptDecoder(MeshoptDecoder);
  const draco = new DRACOLoader();
  draco.setDecoderPath('https://unpkg.com/three@0.160.0/examples/jsm/libs/draco/');
  loader.setDRACOLoader(draco);

  function rebuildMorphIndex() {
    morphEntries = [];
    for (const mm of morphedMeshes) {
      const meshNameLower = (mm.mesh.name || '').toLowerCase();
      mm.rev = {};
      for (const [name, idx] of Object.entries(mm.dict)) {
        mm.rev[idx] = name;
        morphEntries.push({
          nameLower: name.toLowerCase(),
          mm, idx,
          meshNameLower
        });
      }
    }
    visemeCache.clear();
  }

  function chooseMorphByCandidates(candidates) {
    if (!candidates || !candidates.length) return null;
    let best = null; let bestScore = -Infinity;
    for (const cRaw of candidates) {
      const c = cRaw.toLowerCase();
      for (const entry of morphEntries) {
        const { nameLower, meshNameLower } = entry;
        if (!nameLower.includes(c)) continue;
        let score = 0;
        if (nameLower === c) score += 3;
        if (nameLower.startsWith(c)) score += 1;
        if (/head|face|mouth|lip|jaw/.test(meshNameLower)) score += 1;
        if (score > bestScore) { bestScore = score; best = entry; }
      }
    }
    return best;
  }

  function scheduleKey(mm, idx, weight, holdMs = HOLD_MS) {
    const key = `${mm.mesh.uuid}:${idx}`;
    const prev = activeTargets.get(key);
    const until = Math.max(performance.now() + holdMs, prev ? prev.untilTs : 0);
    const w = Math.max(prev ? prev.weight : 0, weight);
    const name = mm.rev?.[idx] ?? `${mm.mesh.name}:${idx}`;
    activeTargets.set(key, { weight: w, untilTs: until, name });
  }


  function setBackgroundTexture(tex) {
    bgTex = tex;
    bgTex.colorSpace = THREE.SRGBColorSpace;
    bgTex.needsUpdate = true;

    if (!bgMesh) {
      const geom = new THREE.PlaneGeometry(2, 2);
      const mat = new THREE.MeshBasicMaterial({ map: bgTex, depthWrite: false });
      bgMesh = new THREE.Mesh(geom, mat);
      bgMesh.renderOrder = -1;
      camera.add(bgMesh);       // lock to camera
      scene.add(camera);        // ensure camera in scene
    } else {
      bgMesh.material.map = bgTex;
      bgMesh.material.needsUpdate = true;
      bgMesh.visible = true;
    }
    fitBackgroundToView();
  }

  function fitBackgroundToView() {
    if (!bgMesh || !bgTex || !bgTex.image) return;
    const vFov = THREE.MathUtils.degToRad(camera.fov);
    const viewHeight = 2 * Math.tan(vFov * 0.5) * BG_DIST;
    const viewWidth = viewHeight * camera.aspect;

    const imgW = bgTex.image.width || 1;
    const imgH = bgTex.image.height || 1;
    const imgAspect = imgW / imgH;

    let planeW = viewWidth * BG_MARGIN;
    let planeH = planeW / imgAspect;
    if (planeH < viewHeight * BG_MARGIN) {
      planeH = viewHeight * BG_MARGIN;
      planeW = planeH * imgAspect;
    }

    bgMesh.scale.set(planeW, planeH, 1);
    bgMesh.position.set(0, 0, -BG_DIST);
  }

  function clearBackgroundTexture() {
    if (bgMesh) bgMesh.visible = false;
  }

  function loadModel(url) {
    log(`Loading model: ${url}`);
    loader.load(url, (gltf) => {
      if (modelRoot) scene.remove(modelRoot);
      modelRoot = gltf.scene;
      scene.add(modelRoot);

      modelRoot.position.set(0, 0, 0);
      modelRoot.traverse(o => { if (o.isMesh) o.castShadow = o.receiveShadow = true; });

      // pick first skinned mesh with skeleton for animation retarget
      skinnedTarget = null;
      modelRoot.traverse(o => { if (!skinnedTarget && o.isSkinnedMesh && o.skeleton) skinnedTarget = o; });

      morphedMeshes = [];
      modelRoot.traverse(obj => {
        if (obj.isMesh && obj.morphTargetDictionary && obj.morphTargetInfluences) {
          morphedMeshes.push({ mesh: obj, dict: obj.morphTargetDictionary, influences: obj.morphTargetInfluences, rev: {} });
        }
      });

      // animations baked inside GLB
      mixer = new THREE.AnimationMixer(gltf.scene);
      clips = {};
      animNames = [];
      if (gltf.animations && gltf.animations.length) {
        for (const clip of gltf.animations) {
          clips[clip.name] = mixer.clipAction(clip);
          animNames.push(clip.name);
        }
      }

      rebuildMorphIndex();
      refreshMorphDump();

      // show any internal clips immediately
      refreshAnimUI();

      // then augment with external FBX
      loadExternalAnims(ANIMS_TO_LOAD).then(() => {
        refreshAnimUI();
        // auto-play Idle if present
        if (clips['Idle']) clips['Idle'].setLoop(THREE.LoopRepeat).fadeIn(0.25).play();
      });

      // (re)start blink loop after model load
      startBlinkLoop();
    }, undefined, (err) => log(`Model load error: ${err?.message || err}`));
  }

  // initial model (fixed path)
  loadModel('/models/myavatar.glb');

  // --- External animations (FBX) ---
  async function loadExternalAnims(names) {
    const fbx = new FBXLoader();
    fbx.setPath(ANIM_DIR);
    let loaded = 0;
    for (const name of names) {
      const url = `${name}.fbx`;
      log(`[anim] loading ${ANIM_DIR}${url}`);
      try {
        await new Promise((resolve, reject) => {
          fbx.load(url, obj => {
            const srcSkinned = findFirstSkinned(obj);
            const base = obj.animations && obj.animations[0];
            if (!base) { resolve(); return; }

            if (!srcSkinned || !skinnedTarget) {
              log(`[anim] retarget skipped for "${name}": source or target skeleton missing`);
              // fallback: still add raw clip (may work if rigs match)
              const raw = base.clone(); raw.name = name;
              clips[name] = mixer.clipAction(raw);
              animNames.push(name);
              loaded++; resolve(); return;
            }

            const clip = retargetClip(skinnedTarget, srcSkinned, base);
            clip.name = name;
            clips[name] = mixer.clipAction(clip);
            animNames.push(name);
            loaded++; resolve();
          }, undefined, reject);
        });
      } catch (e) {
        log(`[anim] load failed for "${name}": ${e?.message || e}`);
      }
    }
    if (!loaded) log('[anim] no external animations were loaded.');
  }

  function findFirstSkinned(root) {
    let hit = null;
    root.traverse(o => { if (!hit && o.isSkinnedMesh && o.skeleton) hit = o; });
    return hit;
  }

  // --- UI: animation select & buttons ---
  function refreshAnimUI() {
    const sel = $('animSelect');
    if (!sel) return;
    sel.innerHTML = '';
    const names = Array.from(new Set(animNames)).sort();
    if (!names.length) {
      const opt = document.createElement('option');
      opt.value = ''; opt.textContent = '(no animations found)';
      sel.appendChild(opt);
      return;
    }
    for (const n of names) {
      const opt = document.createElement('option');
      opt.value = n; opt.textContent = n;
      sel.appendChild(opt);
    }
    if (names.includes('Idle')) sel.value = 'Idle';
  }

  function playSelected(loop = true) {
    const name = $('animSelect').value;
    playAnimation(name, { loop });
  }

  $('btnAnimPlay').onclick = () => playSelected(true);
  $('btnAnimOnce').onclick = () => playSelected(false);
  $('btnAnimStop').onclick = () => { for (const a of Object.values(clips)) a.stop(); };

  // --- Viseme → Morph mapping (weighted ARKit) ---
  const VISEME2ARKIT = {
    0: {},
    1: { jawOpen: 0.7, mouthUpperUpLeft: 0.2, mouthUpperUpRight: 0.2 },
    2: { jawOpen: 0.7, mouthUpperUpLeft: 0.2, mouthUpperUpRight: 0.2 },
    3: { mouthFunnel: 0.6, jawOpen: 0.4 },
    4: { mouthStretchLeft: 0.5, mouthStretchRight: 0.5, jawOpen: 0.25 },
    5: { mouthStretchLeft: 0.4, mouthStretchRight: 0.4, jawOpen: 0.3 },
    6: { mouthPucker: 0.7, jawOpen: 0.3 },
    7: { mouthPressLeft: 0.25, mouthPressRight: 0.25, mouthFunnel: 0.25, jawOpen: 0.2 },
    8: { tongueOut: 0.35, jawOpen: 0.25 },
    9: { tongueOut: 0.6, jawOpen: 0.2 },
    10: { mouthClose: 0.65, mouthPressLeft: 0.2, mouthPressRight: 0.2 },
    11: { mouthFunnel: 0.55, jawOpen: 0.25 },
    12: { mouthClose: 0.65, mouthPressLeft: 0.2, mouthPressRight: 0.2 },
    13: { mouthFunnel: 0.6, jawOpen: 0.3 },
    14: { mouthPressLeft: 0.5, mouthPressRight: 0.5, mouthFunnel: 0.3 },
    15: { mouthClose: 0.85 },
    16: { mouthClose: 0.85 },
    17: { mouthClose: 0.85 },
    18: { mouthClose: 0.85 },
    19: { mouthFunnel: 0.6, mouthClose: 0.25, jawOpen: 0.2 },
    20: { mouthFunnel: 0.6, mouthClose: 0.25, jawOpen: 0.2 },
    21: { mouthClose: 1.0 }
  };

  const INDEX_CACHE = new Map(); // nameLower -> { mm, idx }
  function findMorphByName(name) {
    const key = name.toLowerCase();
    if (INDEX_CACHE.has(key)) return INDEX_CACHE.get(key);
    const hit = morphEntries.find(e => e.nameLower === key) || null;
    INDEX_CACHE.set(key, hit);
    return hit;
  }

  function scheduleWeighted(map, gain = 1.0) {
    for (const [name, w] of Object.entries(map)) {
      const entry = findMorphByName(name);
      if (!entry) continue;
      const shaped = shapeWeight(name, (w || 0) * gain);
      if (shaped > 0) {
        scheduleKey(entry.mm, entry.idx, shaped, HOLD_MS);
      }
    }
  }


  function scheduleViseme(id, weight = 1.0) {
    const map = VISEME2ARKIT[id];
    if (map) scheduleWeighted(map, weight);

    // Only add vowel → jaw coupling if the viseme map didn't already drive jaw.
    const mapHasJaw = !!(map && (map.jawOpen != null || map.mouthOpen != null));
    if (VOWEL_IDS.has(id) && !mapHasJaw) {
      const jawHit = chooseMorphByCandidates(JAW_CANDIDATES);
      if (jawHit) {
        const w = shapeWeight('jawOpen', weight);
        if (w > 0) scheduleKey(jawHit.mm, jawHit.idx, w, HOLD_MS);
      }
    }

    // keep HUD in 'speaking' briefly to avoid flicker
    speakingUntil = Math.max(speakingUntil, performance.now() + 800);
  }


  function clearAllVisemes() {
    activeTargets.clear();
    for (const mm of morphedMeshes) {
      for (let i = 0; i < mm.influences.length; i++) mm.influences[i] = 0;
    }
  }

  // --- Tiny blink loop (optional) ---
  let blinkTimer = null;
  function startBlinkLoop() {
    if (blinkTimer) { clearTimeout(blinkTimer); blinkTimer = null; }
    const blink = () => {
      const b = chooseMorphByCandidates(BLINK_CANDIDATES);
      if (b) {
        scheduleKey(b.mm, b.idx, 1.0, BLINK_LEN_MS * 0.5);
        setTimeout(() => scheduleKey(b.mm, b.idx, 0.0, 1), BLINK_LEN_MS);
      }
      const next = BLINK_EVERY_MS[0] + Math.random() * (BLINK_EVERY_MS[1] - BLINK_EVERY_MS[0]);
      blinkTimer = setTimeout(blink, next);
    };
    blinkTimer = setTimeout(blink, 800 + Math.random() * 800);
  }

  // --- Speech SDK ---
  let speechSynthesizer = null;

  const speakQueue = [];
  let speakRunning = false;

  async function enqueueSpeak(fn) {
    speakQueue.push(fn);
    if (speakRunning) return;
    speakRunning = true;
    try {
      while (speakQueue.length) {
        const next = speakQueue.shift();
        await next();
      }
    } finally {
      speakRunning = false;
    }
  }


  async function getSpeechAuth() {
    const r = await fetch('/api/getSpeechToken');
    if (!r.ok) throw new Error(`getSpeechToken failed: ${r.status} ${r.statusText}`);
    let token = (await r.text() || '').trim();
    const region = r.headers.get('SpeechRegion') || '';
    const privateEp = r.headers.get('SpeechPrivateEndpoint') || '';
    if (token.startsWith('aad#')) token = token.split('#')[2] || ''; // strip AAD prefix
    if (!token) throw new Error('Empty speech token');
    return { token, region, privateEp };
  }

  function looksLikeCustomHost(host) {
    return /tts\.speech\.microsoft\.com/i.test(host) || /cognitiveservices/i.test(host);
  }

  async function ensureSpeechSdk() {
    if (window.SpeechSDK) return window.SpeechSDK;

    const urls = [
      // 1) local file (works offline, bypasses blockers)
      '/lib/speech/microsoft.cognitiveservices.speech.sdk.bundle-min.js',
      // 2) unpkg CDN (known good path)
      'https://unpkg.com/microsoft-cognitiveservices-speech-sdk@1/distrib/browser/microsoft.cognitiveservices.speech.sdk.bundle-min.js',
      // 3) official redirect (usually blocked on corp machines)
      'https://aka.ms/csspeech/jsbrowserpackageraw'
    ];

    let lastErr;
    for (const url of urls) {
      try {
        console.log('[speech] loading SDK:', url);
        await new Promise((res, rej) => {
          const s = document.createElement('script');
          s.src = url; s.async = true;
          s.onload = res;
          s.onerror = (e) => rej(new Error(`load failed: ${url}`));
          document.head.appendChild(s);
        });
        if (window.SpeechSDK) {
          console.log('[speech] SDK ready');
          return window.SpeechSDK;
        }
      } catch (e) {
        console.warn('[speech] ' + e.message);
        lastErr = e;
      }
    }
    throw new Error('Speech SDK failed to load' + (lastErr ? `: ${lastErr.message}` : ''));
  }

  async function speakSsml(ssml, voiceOverride, animationName) {
    try {
      $('btnSpeak')?.setAttribute('disabled', 'true');
      $('btnStop')?.removeAttribute('disabled');

      // Start animation immediately (before audio starts)
      if (animationName) playAnimation(animationName, { loop: false });

      const { token, region, privateEp } = await getSpeechAuth();
      const SpeechSDK = await ensureSpeechSdk();

      const speechConfig = SpeechSDK.SpeechConfig.fromAuthorizationToken(token, region);
      speechConfig.speechSynthesisVoiceName =
        voiceOverride || $('ttsVoice')?.value || 'en-US-JennyNeural';

      if (privateEp) {
        const wss = privateEp.replace(/^https:\/\//i, 'wss://').replace(/\/+$/, '') +
          '/tts/cognitiveservices/websocket/v1';
        speechConfig.setProperty(SpeechSDK.PropertyId.SpeechServiceConnection_Endpoint, wss);
      }

      const audioConfig = SpeechSDK.AudioConfig.fromDefaultSpeakerOutput();
      const synth = new SpeechSDK.SpeechSynthesizer(speechConfig, audioConfig);
      speechSynthesizer = synth;

      // IMPORTANT: close THIS instance, and only clear the global if it still points to this instance
      function closeSynth() {
        try { synth.close(); } catch { }
        finally {
          if (speechSynthesizer === synth) speechSynthesizer = null;
        }
      }


      let audioStart = 0;
      speechSynthesizer.synthesizing = () => {
        if (audioStart === 0) audioStart = performance.now();
        speakingUntil = Math.max(speakingUntil, performance.now() + 800);
      };

      speechSynthesizer.visemeReceived = (_s, e) => {
        const offsetMs = Number(e.audioOffset) / 10000.0;
        const when = (audioStart || performance.now()) + offsetMs;
        const delay = Math.max(0, when - performance.now());
        setTimeout(() => scheduleViseme(e.visemeId, 1.0), delay);
      };

      speechSynthesizer.synthesisCompleted = () => {
        speakingUntil = performance.now() + 350;
        $('btnSpeak')?.removeAttribute('disabled');
        $('btnStop')?.setAttribute('disabled', 'true');
        setTimeout(clearAllVisemes, 120);
        closeSynth();

        // Return to Idle when finished
        scheduleReturnToIdle();

        resolve();
      };

      speechSynthesizer.canceled = () => {
        $('btnSpeak')?.removeAttribute('disabled');
        $('btnStop')?.setAttribute('disabled', 'true');
        clearAllVisemes();
        closeSynth();

        // Return to Idle when canceled
        scheduleReturnToIdle();

        resolve();
      };

      speechSynthesizer.speakSsmlAsync(ssml);

    } catch (err) {
      console.error('Speak SSML error:', err);
      $('btnSpeak')?.removeAttribute('disabled');
      $('btnStop')?.setAttribute('disabled', 'true');

      // Safety: return to Idle on error too
      scheduleReturnToIdle();
      resolve();
    }
  }

  async function speakText(text, voiceOverride, animationName) {
    return new Promise(async (resolve) => {
      try {
        $('btnSpeak')?.setAttribute('disabled', 'true');
        $('btnStop')?.removeAttribute('disabled');

        if (animationName) playAnimation(animationName, { loop: false });

        const { token, region, privateEp } = await getSpeechAuth();
        const SpeechSDK = await ensureSpeechSdk();

        const speechConfig = SpeechSDK.SpeechConfig.fromAuthorizationToken(token, region);
        speechConfig.speechSynthesisVoiceName =
          voiceOverride || $('ttsVoice')?.value || 'en-US-JennyNeural';

        if (privateEp) {
          const wss = privateEp.replace(/^https:\/\//i, 'wss://').replace(/\/+$/, '') +
            '/tts/cognitiveservices/websocket/v1';
          speechConfig.setProperty(SpeechSDK.PropertyId.SpeechServiceConnection_Endpoint, wss);
        }

        const audioConfig = SpeechSDK.AudioConfig.fromDefaultSpeakerOutput();
        const synth = new SpeechSDK.SpeechSynthesizer(speechConfig, audioConfig);
        speechSynthesizer = synth;

        // IMPORTANT: close THIS instance, and only clear the global if it still points to this instance
        function closeSynth() {
          try { synth.close(); } catch { }
          finally {
            if (speechSynthesizer === synth) speechSynthesizer = null;
          }
        }


        let audioStart = 0;
        speechSynthesizer.synthesizing = () => {
          if (audioStart === 0) audioStart = performance.now();
          speakingUntil = Math.max(speakingUntil, performance.now() + 800);
        };

        speechSynthesizer.visemeReceived = (_s, e) => {
          const offsetMs = Number(e.audioOffset) / 10000.0;
          const when = (audioStart || performance.now()) + offsetMs;
          const delay = Math.max(0, when - performance.now());
          setTimeout(() => scheduleViseme(e.visemeId, 1.0), delay);
        };

        speechSynthesizer.synthesisCompleted = () => {
          speakingUntil = performance.now() + 350;
          $('btnSpeak')?.removeAttribute('disabled');
          $('btnStop')?.setAttribute('disabled', 'true');
          setTimeout(clearAllVisemes, 120);
          closeSynth();

          scheduleReturnToIdle();

          resolve();
        };

        speechSynthesizer.canceled = () => {
          $('btnSpeak')?.removeAttribute('disabled');
          $('btnStop')?.setAttribute('disabled', 'true');
          clearAllVisemes();
          closeSynth();

          scheduleReturnToIdle();

          resolve();
        };

        const sanitized = sanitizeForTts(text);
        speechSynthesizer.speakTextAsync(sanitized);

      } catch (err) {
        console.error('Speak error:', err);
        $('btnSpeak')?.removeAttribute('disabled');
        $('btnStop')?.setAttribute('disabled', 'true');
        scheduleReturnToIdle();
        resolve();
      }
    });
  }


  const DEFAULT_IDLE = 'Idle';

  const MIN_ONESHOT_MS = 8000;

  let oneShotHoldUntil = 0;
  let idleTimer = null;

  // --- Animation marker parsing: [[ANIM:Name]] ---
  const ANIM_RX = /\[\[\s*ANIM\s*:\s*([A-Za-z0-9_-]+)\s*\]\]/gi;

  function extractAnimMarker(input) {
    if (!input) return { clean: input, anim: null };

    let anim = null;
    const clean = String(input).replace(ANIM_RX, (_m, name) => {
      anim = name;           // last marker wins
      return "";
    }).trim();

    return { clean, anim };
  }


  function scheduleReturnToIdle() {
    if (idleTimer) { clearTimeout(idleTimer); idleTimer = null; }

    const now = performance.now();
    const delay = Math.max(0, oneShotHoldUntil - now);

    idleTimer = setTimeout(() => {
      // If something extended the hold while we were waiting, keep waiting.
      if (performance.now() < oneShotHoldUntil) return scheduleReturnToIdle();
      returnToIdle();
    }, delay);
  }


  function stopAllAnimations(fadeOutSec = 0.2) {
    for (const a of Object.values(clips)) {
      try { a.fadeOut(fadeOutSec); } catch { /* ignore */ }
    }
  }

  function playAnimation(name, { loop = false, fadeInSec = 0.25, fadeOutSec = 0.2 } = {}) {
    const chosen = clips[name] || clips[DEFAULT_IDLE];
    if (!chosen) return;

    // fade others out
    for (const [n, a] of Object.entries(clips)) {
      if (a && a !== chosen) {
        try { a.fadeOut(fadeOutSec); } catch { }
      }
    }

    chosen.reset();
    chosen.setLoop(loop ? THREE.LoopRepeat : THREE.LoopOnce);
    chosen.clampWhenFinished = !loop;
    chosen.fadeIn(fadeInSec).play();

    // If it's a one-shot, prevent Idle from stealing focus too early.
    if (!loop) {
      const clipDurMs = (chosen.getClip()?.duration ?? 0) * 1000;
      const holdMs = Math.max(MIN_ONESHOT_MS, clipDurMs);
      oneShotHoldUntil = performance.now() + holdMs;
    }
  }


  // convenience: always return to Idle looping
  function returnToIdle() {
    if (clips[DEFAULT_IDLE]) playAnimation(DEFAULT_IDLE, { loop: true, fadeInSec: 0.2, fadeOutSec: 0.2 });
  }



  // keep the existing manual button wiring but route it through speakText:
  $('btnSpeak').onclick = () => speakText($('ttsText').value, $('ttsVoice').value);


  function stopSpeaking() {
    try {
      if (speechSynthesizer) {
        speechSynthesizer.stopSpeakingAsync(
          () => { log('Stopped.'); clearAllVisemes(); speakingUntil = performance.now() + 200; },
          e => { log(`Stop error: ${e}`); }
        );
      }
    } finally {
      $('btnSpeak').disabled = false; $('btnStop').disabled = true;
    }
  }

  //$('btnSpeak').onclick = speak;
  $('btnStop').onclick = stopSpeaking;

  // ---- View: background image (URL or file -> THREE texture) ----
  $('btnBgSet').onclick = async () => {
    const url = $('bgUrl').value.trim();
    const file = $('bgFile').files[0];

    if (!url && !file) return;

    if (file) {
      const blobUrl = URL.createObjectURL(file);
      loadBgTexture(blobUrl);
      log('[view] background set (file)');
      return;
    }
    if (url) {
      loadBgTexture(url);
      log('[view] background set');
    }
  };
  $('btnBgClear').onclick = () => { clearBackgroundTexture(); log('[view] background cleared'); };

  function sanitizeForTts(s) {
    if (!s) return '';
    let t = String(s).trim();

    // Drop leading "text:" label if present (case-insensitive).
    t = t.replace(/^\s*text\s*:\s*/i, '');

    // If the model produced LaTeX/MathJax-ish output, strip it for speech.
    const looksLikeLatex =
      /\\\(|\\\)|\\\[|\\\]|\\quad|\\text\{|\\frac\{|\\sqrt\{|\\[a-zA-Z]{2,}/.test(t);

    if (looksLikeLatex) {
      // Convert \( ... \) and \[ ... \] to just the inside text
      t = t.replace(/\\\(([\s\S]*?)\\\)/g, '$1');
      t = t.replace(/\\\[([\s\S]*?)\\\]/g, '$1');

      // Common LaTeX commands → speech-friendly spacing or content
      t = t.replace(/\\text\{([^}]*)\}/g, '$1');
      t = t.replace(/\\quad|\\qquad/g, ' ');
      t = t.replace(/\\times/g, ' times ');
      t = t.replace(/\\div/g, ' divided by ');
      t = t.replace(/\\cdot/g, ' times ');

      // Unescape common escaped symbols (e.g. \%, \_, \{ \})
      t = t.replace(/\\([\\{}_#%&$])/g, '$1');

      // Drop any remaining \command tokens (best-effort)
      t = t.replace(/\\[a-zA-Z]+/g, '');

      // Drop any stray backslashes left over
      t = t.replace(/\\/g, '');
    }

    // Replace colons/semicolons with a natural pause instead of “colon”.
    t = t.replace(/\s*[:;]\s*/g, ', ');

    // Collapse whitespace
    t = t.replace(/\s{2,}/g, ' ').trim();


    // Remove any animation markers (or other [[...]] control tokens if you want)
    t = t.replace(ANIM_RX, '').trim();

    // Drop leading "text:" label if present (case-insensitive).
    t = t.replace(/^\s*text\s*:\s*/i, '');

    // Replace remaining colons/semicolons with a natural pause.
    t = t.replace(/\s*[:;]\s*/g, ', ');

    // Collapse any double spaces introduced.
    t = t.replace(/\s{2,}/g, ' ');

    return t;
  }

  function loadBgTexture(src) {
    const tl = new THREE.TextureLoader();
    try { tl.setCrossOrigin('anonymous'); } catch { }
    tl.load(
      src,
      tex => setBackgroundTexture(tex),
      undefined,
      () => log('[view] background failed to load')
    );
  }

  // ---- dump morphs (right column) ----
  function refreshMorphDump() {
    const dump = [];
    for (const mm of morphedMeshes) {
      dump.push(`Mesh: ${mm.mesh.name}\n` + Object.keys(mm.dict).map(k => `  - ${k}`).join('\n'));
    }
    const md = $('morphDump'); if (md) md.textContent = dump.join('\n\n') || '(no morph targets found)';
    const mi = $('modelInfo'); if (mi) mi.innerHTML = `Meshes with morphs: ${morphedMeshes.length}`;
    log(`Model loaded. Morph meshes: ${morphedMeshes.length}`);
  }

  function looksLikeSsml(s) {
    return !!s && /^\s*<speak\b/i.test(s);
  }

  (async function connectHub() {
    try {
      const clientId = document.getElementById('clientId')?.value || 'default';
      const sr = await loadSignalR();
      const HubConnectionBuilder = (sr.HubConnectionBuilder || window.signalR?.HubConnectionBuilder);
      const LogLevel = (sr.LogLevel || window.signalR?.LogLevel);

      const conn = new HubConnectionBuilder()
        .withUrl(`/avatarHub?clientId=${encodeURIComponent(clientId)}`)
        .withAutomaticReconnect()
        .configureLogging(LogLevel.Information)
        .build();

      //conn.on('speak', ({ text, ssml, voice }) => {
      //  if (ssml) return speakSsml(ssml, voice);
      //  if (looksLikeSsml(text)) return speakSsml(text, voice);
      //  if (text) return speakText(text, voice);
      //});

      conn.on('speak', ({ text, ssml, voice, animation }) => {
        // If server already sent animation, prefer it.
        let anim = animation;

        if (ssml) {
          const r = extractAnimMarker(ssml);
          ssml = r.clean;
          anim = anim || r.anim;
          return speakSsml(ssml, voice, anim);
        }

        if (looksLikeSsml(text)) {
          const r = extractAnimMarker(text);
          text = r.clean;
          anim = anim || r.anim;
          return speakSsml(text, voice, anim);
        }

        if (text) {
          const r = extractAnimMarker(text);
          text = r.clean;
          anim = anim || r.anim;
          return speakText(text, voice, anim);
        }
      });




      await conn.start();
      log('[hub] connected');
    } catch (e) {
      log(`[hub] connection failed: ${e?.message || e}`);
    }
  })();



  (function installAudioUnlock() {
    let unlocked = false;
    async function unlock() {
      if (unlocked) return;
      try {
        const ctx = new (window.AudioContext || window.webkitAudioContext)();
        if (ctx.state === 'suspended') await ctx.resume();
        unlocked = true;
        log('[audio] audio context unlocked');
      } catch (e) {
        log('[audio] unlock failed: ' + (e?.message || e));
      }
    }
    // Any click/tap unlocks audio; in kiosk we’ll allow autoplay via flag.
    document.addEventListener('pointerdown', unlock, { once: true });
  })();

  function playAnim(name) {
    // Keep the legacy helper name, but route through the working mixer/actions map.
    playAnimation(name, { loop: false });

    // optional: after N seconds, return to idle
    setTimeout(returnToIdle, 2500);
  }



});
