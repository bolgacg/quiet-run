/* quiet-run demo page. Every number shown is computed here from window.QR, which the C# study command
   wrote (docs/data.js), and window.QR_TESTS, which the tests command wrote from the dotnet test results. */
(function () {
  'use strict';
  var D = window.QR, TT = window.QR_TESTS;
  var NS = 'http://www.w3.org/2000/svg';
  var KINDS = ['leak', 'clog', 'air', 'quiet', 'drift'];
  var INRUN = ['leak', 'clog', 'air', 'quiet'];
  var NAMES = { leak: 'LEAK', clog: 'CLOG', air: 'AIR', quiet: 'QUIET', drift: 'DRIFT', healthy: 'OK', ok: 'not flagged' };
  var WHAT = { leak: 'pressure falls', clog: 'pressure rises', air: 'short dips', quiet: 'method stalls', drift: 'creeps up run by run' };
  var state = { setting: 'chosen', kind: 'leak', sev: 3 };

  function $(s, r) { return (r || document).querySelector(s); }
  function esc(s) { return String(s).replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;'); }
  function median(a) {
    if (!a.length) return NaN;
    var b = a.slice().sort(function (x, y) { return x - y; }), n = b.length;
    return n % 2 ? b[(n - 1) / 2] : (b[n / 2 - 1] + b[n / 2]) / 2;
  }
  function f1(x) { return (Math.round(x * 10) / 10).toFixed(1); }
  function clock(s) { s = Math.round(Math.max(0, s)); return Math.floor(s / 60) + ':' + ('0' + (s % 60)).slice(-2); }
  function dur(s) { return s < 90 ? Math.round(s) + ' s' : f1(s / 60) + ' min'; }
  function jit(i, k) { var x = Math.sin(i * 12.9898 + k * 78.233) * 43758.5453; return x - Math.floor(x); }
  function settingOf(name) { return D.settings.filter(function (s) { return s.name === name; })[0]; }
  function cellsOf(kind) { return D.cells.filter(function (c) { return c.kind === kind; }); }
  function cellOf(kind, sev) { return D.cells.filter(function (c) { return c.kind === kind && c.sev === sev; })[0]; }
  function caughtList(cell, who, setting) {
    var arr = who === 'fixed' ? cell.fixed.delay : cell.qr[setting].delay;
    return arr.filter(function (x) { return x !== null; });
  }
  function svgEl(tag, attrs, parent) {
    var e = document.createElementNS(NS, tag);
    for (var k in attrs) if (attrs[k] !== undefined && attrs[k] !== null) e.setAttribute(k, attrs[k]);
    if (parent) parent.appendChild(e);
    return e;
  }
  function text(parent, x, y, s, attrs) {
    var a = { x: x, y: y, 'font-size': 11, fill: '#5b6470' };
    for (var k in attrs || {}) a[k] = attrs[k];
    var t = svgEl('text', a, parent); t.textContent = s; return t;
  }
  function newSvg(host, W, H) {
    host.innerHTML = '';
    return svgEl('svg', { width: W, height: H, viewBox: '0 0 ' + W + ' ' + H, role: 'img' }, host);
  }
  var C = { ink: '#1a1d21', soft: '#5b6470', faint: '#8b95a1', rule: '#e2e0dc', rule2: '#c9c5be', qr: '#2a78d6', fx: '#eb6834', miss: '#efece7', neg: '#b03a3a' };

  /* ---------- headline numbers, computed from the cells ---------- */
  function summary(setting) {
    var s = { faults: 0, qr: 0, fx: 0, qrIn: [], fxIn: [], byKind: {} };
    D.cells.forEach(function (c) {
      var q = caughtList(c, 'qr', setting), f = caughtList(c, 'fixed');
      s.faults += c.qr[setting].delay.length; s.qr += q.length; s.fx += f.length;
      var bk = s.byKind[c.kind] || (s.byKind[c.kind] = { n: 0, qr: 0, fx: 0 });
      bk.n += c.qr[setting].delay.length; bk.qr += q.length; bk.fx += f.length;
      if (c.unit === 's') { s.qrIn = s.qrIn.concat(q); s.fxIn = s.fxIn.concat(f); }
    });
    s.qrMedMin = median(s.qrIn) / 60; s.fxMedMin = median(s.fxIn) / 60;
    var small = cellOf('leak', 1);
    s.smallLeak = caughtList(small, 'qr', setting).length; s.smallLeakN = small.qr[setting].delay.length;
    s.healthyN = D.healthy.runs; s.healthyQr = D.healthy.qr[setting].flagged; s.healthyFx = D.healthy.fixed.flagged;
    return s;
  }

  function renderHeader() {
    var s = summary('chosen');
    $('#dek').innerHTML = 'quiet-run is a C# tool that watches a liquid chromatography run as it happens and names what went wrong. ' +
      'On <strong>' + s.faults + ' synthetic runs</strong> with one injected fault each, it flagged <strong>' + s.qr +
      '</strong>; fixed pressure limits flagged <strong>' + s.fx + '</strong>. It still misses most small leaks.';
  }

  function renderQlines() {
    var s = summary(state.setting), set = state.setting;
    $('#qlines').innerHTML =
      '<div><b>How many faults does it catch?</b>At the ' + set + ' setting quiet-run flagged <span class="num">' + s.qr + '</span> of ' + s.faults +
      ' injected faults; the fixed limits flagged <span class="num">' + s.fx + '</span>.</div>' +
      '<div><b>How soon?</b>Within a run, its median alarm came <span class="num">' + f1(s.qrMedMin) + ' min</span> after the fault began, against ' +
      '<span class="num">' + f1(s.fxMedMin) + ' min</span> for the alarms the fixed limits did raise.</div>' +
      '<div class="hurt"><b>Where does it fail?</b>It flagged only <span class="num">' + s.smallLeak + ' of ' + s.smallLeakN +
      '</span> leaks that cost 2 percent of pressure.</div>' +
      '<div class="hurt"><b>What does it cost?</b><span class="num">' + s.healthyQr + ' of ' + s.healthyN + '</span> healthy runs got a false alarm, against ' +
      '<span class="num">' + s.healthyFx + '</span> for the fixed limits.</div>';
    var q = s.byKind.quiet, a = s.byKind.air;
    $('#bluftake').innerHTML = '<b>What to take from it</b>quiet-run\'s dots sit within a few minutes of the fault, and the fixed limits\' dots mostly sit in the grey column. ' +
      'The gap is widest where pressure barely moves: the fixed limits flagged ' + a.fx + ' of ' + a.n + ' air faults and ' + q.fx + ' of ' + q.n +
      ' stalls, quiet-run ' + a.qr + ' and ' + q.qr + '. Drift is the exception: both catch it, and quiet-run is only a little sooner.';
  }

  /* ---------- the BLUF chart ---------- */
  function drawBluf() {
    var host = $('#blufviz'), host2 = $('#blufviz2'), W = host.clientWidth || 600, narrow = W < 560;
    var set = state.setting;
    var labW = narrow ? 66 : 96, cntW = narrow ? 44 : 64, missW = narrow ? 28 : 44;
    var x0 = labW, xEnd = W - cntW - missW - 8, mx0 = xEnd + 6, mx1 = W - cntW;
    var laneH = narrow ? 19 : 22, headH = 18, gapG = 10, axisH = 46;
    var nA = INRUN.length;
    var hA = 20 + nA * (headH + 2 * laneH + gapG) + axisH;
    var hB = 20 + (headH + 2 * laneH + gapG) + axisH;
    var hC = 20 + 2 * laneH + gapG + axisH;
    var svgA = newSvg(host, W, hA), svgB = newSvg(host2, W, hB + hC + 6), svg = svgA;
    svgA.setAttribute('aria-label', 'Dot chart of time to first alarm within a run per fault type, quiet-run against fixed pressure limits');
    svgB.setAttribute('aria-label', 'Dot chart of runs to first alarm for drift, and bars of false alarms on healthy runs');

    function panel(y0, title, domainMax, ticks, axisTitle, kinds, floorVal, floorLabel, toX) {
      var top = y0 + 20, bottom = y0 + 20 + kinds.length * (headH + 2 * laneH + gapG);
      text(svg, 0, y0 + 12, title, { 'font-size': 10.5, 'letter-spacing': '0.12em', fill: C.faint, 'font-family': 'IBM Plex Mono, monospace' });
      // grid and missed zone
      ticks.forEach(function (t) {
        var x = toX(t);
        svgEl('line', { x1: x, x2: x, y1: top, y2: bottom, stroke: C.rule, 'stroke-width': 1 }, svg);
        text(svg, x, bottom + 14, String(t), { 'text-anchor': 'middle', 'font-size': 10.5, fill: C.soft, 'font-family': 'IBM Plex Mono, monospace' });
      });
      svgEl('rect', { x: mx0, y: top, width: mx1 - mx0, height: bottom - top, fill: C.miss }, svg);
      text(svg, (mx0 + mx1) / 2, top - 4, narrow ? 'none' : 'missed', { 'text-anchor': 'middle', 'font-size': 10, fill: C.soft });
      text(svg, (x0 + xEnd) / 2, bottom + 32, axisTitle, { 'text-anchor': 'middle', 'font-size': 11.5, fill: C.ink });
      // floor
      var fx = toX(floorVal);
      svgEl('line', { x1: fx, x2: fx, y1: top - 2, y2: bottom, stroke: C.ink, 'stroke-width': 1.3, 'stroke-dasharray': '4 3' }, svg);
      text(svg, fx - 4, top - 4, floorLabel, { 'text-anchor': 'end', 'font-size': 10, fill: C.ink });

      kinds.forEach(function (kind, gi) {
        var gy = top + gi * (headH + 2 * laneH + gapG);
        if (gi > 0) svgEl('line', { x1: 0, x2: W, y1: gy - gapG / 2, y2: gy - gapG / 2, stroke: C.rule, 'stroke-width': 1 }, svg);
        var head = text(svg, 0, gy + 12, NAMES[kind], { 'font-size': 11.5, fill: C.ink, 'font-weight': 600, 'font-family': 'IBM Plex Mono, monospace' });
        if (!narrow) text(svg, 46, gy + 12, WHAT[kind], { 'font-size': 11, fill: C.soft });
        var cells = cellsOf(kind);
        [['qr', C.qr, 'quiet-run'], ['fixed', C.fx, narrow ? 'fixed' : 'fixed limits']].forEach(function (lane, li) {
          var cy = gy + headH + li * laneH + laneH / 2;
          text(svg, x0 - 8, cy + 4, lane[2], { 'text-anchor': 'end', 'font-size': 11, fill: C.soft });
          var all = [], n = 0;
          cells.forEach(function (c) {
            var arr = lane[0] === 'fixed' ? c.fixed.delay : c.qr[set].delay;
            arr.forEach(function (d, i) {
              n++;
              var jy = (jit(i + c.sev * 31, li + gi * 7) - 0.5) * (laneH - 7);
              if (d === null) {
                var jx = mx0 + 4 + jit(i * 3 + c.sev, gi + li * 5) * (mx1 - mx0 - 8);
                svgEl('circle', { cx: jx, cy: cy + jy, r: 2.3, fill: 'none', stroke: C.faint, 'stroke-width': 1, opacity: 0.8 }, svg);
              } else {
                all.push(d);
                svgEl('circle', { cx: toX(c.unit === 's' ? d / 60 : d), cy: cy + jy, r: 2.5, fill: lane[1], opacity: 0.72 }, svg);
              }
            });
          });
          if (all.length) {
            var m = median(all), mxp = toX(cells[0].unit === 's' ? m / 60 : m);
            svgEl('line', { x1: mxp, x2: mxp, y1: cy - laneH / 2 + 1, y2: cy + laneH / 2 - 1, stroke: C.ink, 'stroke-width': 2.2 }, svg);
          }
          text(svg, W, cy + 4, all.length + '/' + n, { 'text-anchor': 'end', 'font-size': 11.5, fill: C.ink, 'font-family': 'IBM Plex Mono, monospace' });
        });
      });
      return bottom + axisH;
    }

    var floorA = median([].concat.apply([], INRUN.map(function (k) { return [].concat.apply([], cellsOf(k).map(function (c) { return c.floor; })); }))) / 60;
    var maxA = Math.ceil((D.meta.runSeconds - D.meta.faultStartMin) / 60);
    var toXA = function (v) { return x0 + (Math.min(v, maxA) / maxA) * (xEnd - x0); };
    var ticksA = narrow ? [0, 4, 8, 12, 16] : [0, 2, 4, 6, 8, 10, 12, 14, 16];
    panel(0, 'WITHIN ONE RUN', maxA, ticksA, 'minutes from the fault starting to the first alarm', INRUN, floorA,
      narrow ? 'run ends' : 'run ends (median)', toXA);

    svg = svgB;
    var maxB = D.meta.sequenceRuns - D.meta.driftStartRun;
    var toXB = function (v) { return x0 + (Math.min(v, maxB) / maxB) * (xEnd - x0); };
    var yC = panel(0, 'ACROSS RUNS', maxB, [0, 3, 6, 9, 12, 15], 'runs from the drift starting to the first alarm', ['drift'], maxB,
      'batch ends', toXB);

    // false alarms on healthy runs
    var top = yC + 20, n = D.healthy.runs;
    text(svg, 0, yC + 12, 'HEALTHY RUNS: FALSE ALARMS', { 'font-size': 10.5, 'letter-spacing': '0.12em', fill: C.faint, 'font-family': 'IBM Plex Mono, monospace' });
    var maxFa = Math.max.apply(null, D.settings.map(function (s) { return D.healthy.qr[s.name].flagged; }).concat([D.healthy.fixed.flagged]));
    var maxPct = Math.max(1, Math.ceil(maxFa / n * 100));
    var toXC = function (v) { return x0 + (v / maxPct) * (xEnd - x0); };
    var bottom = top + 2 * laneH;
    for (var t = 0; t <= maxPct; t++) {
      svgEl('line', { x1: toXC(t), x2: toXC(t), y1: top, y2: bottom, stroke: C.rule }, svg);
      text(svg, toXC(t), bottom + 14, t + '%', { 'text-anchor': 'middle', 'font-size': 10.5, fill: C.soft, 'font-family': 'IBM Plex Mono, monospace' });
    }
    [['quiet-run', D.healthy.qr[set].flagged, C.qr], [narrow ? 'fixed' : 'fixed limits', D.healthy.fixed.flagged, C.fx]].forEach(function (b, i) {
      var cy = top + i * laneH + laneH / 2;
      text(svg, x0 - 8, cy + 4, b[0], { 'text-anchor': 'end', 'font-size': 11, fill: C.soft });
      svgEl('rect', { x: x0, y: cy - 5, width: Math.max(1.5, toXC(b[1] / n * 100) - x0), height: 10, fill: b[2], opacity: 0.85 }, svg);
      text(svg, W, cy + 4, b[1] + '/' + n, { 'text-anchor': 'end', 'font-size': 11.5, fill: C.ink, 'font-family': 'IBM Plex Mono, monospace' });
    });
    text(svg, (x0 + xEnd) / 2, bottom + 32, 'share of ' + n + ' healthy runs that got an alarm', { 'text-anchor': 'middle', 'font-size': 11.5, fill: C.ink });
  }

  /* ---------- setting chips ---------- */
  function renderSettingChips() {
    var host = $('#setchips');
    D.settings.forEach(function (s) {
      var b = document.createElement('button');
      b.className = 'chip'; b.type = 'button'; b.dataset.setting = s.name;
      b.textContent = s.name;
      b.title = 'LEAK and CLOG fire when pressure sits ' + s.levelPct.toFixed(2) + '% off the envelope for ' + s.holdS + ' s; QUIET fires when the method clock stands still for ' + s.quietS + ' s.';
      b.addEventListener('click', function () { state.setting = s.name; refreshSetting(); });
      host.appendChild(b);
    });
  }
  function refreshSetting() {
    document.querySelectorAll('#setchips .chip').forEach(function (b) { b.setAttribute('aria-pressed', b.dataset.setting === state.setting ? 'true' : 'false'); });
    drawBluf(); renderQlines(); drawExample(); renderConfusion(); renderTrade();
  }

  /* ---------- the domain drawing ---------- */
  function drawDomain() {
    var host = $('#domainviz'), W = host.clientWidth || 600, narrow = W < 600;
    var svg;
    function box(x, y, w, h, label, sub, strong) {
      svgEl('rect', { x: x, y: y, width: w, height: h, rx: 4, fill: strong ? '#fff' : '#f3f1ed', stroke: C.rule2 }, svg);
      text(svg, x + w / 2, y + (sub ? h / 2 - 2 : h / 2 + 4), label, { 'text-anchor': 'middle', 'font-size': 12, fill: C.ink, 'font-weight': 600 });
      if (sub) text(svg, x + w / 2, y + h / 2 + 13, sub, { 'text-anchor': 'middle', 'font-size': 10.5, fill: C.soft });
    }
    function tag(x, y, name, words, anchor) {
      var t = text(svg, x, y, '', { 'text-anchor': anchor || 'start', 'font-size': 11.5, fill: C.soft });
      var a = svgEl('tspan', { 'font-weight': 600, fill: C.neg, 'font-family': 'IBM Plex Mono, monospace' }, t); a.textContent = name + ' ';
      var b = svgEl('tspan', {}, t); b.textContent = words;
    }
    if (!narrow) {
      var H = 232; svg = newSvg(host, W, H);
      var s = W / 760;
      var X = function (v) { return v * s; };
      box(X(150), 8, X(250), 44, 'control software', 'status, heartbeat, method clock', true);
      svgEl('line', { x1: X(200), x2: X(200), y1: 52, y2: 104, stroke: C.faint, 'stroke-dasharray': '3 3' }, svg);
      svgEl('rect', { x: X(10), y: 118, width: X(56), height: 46, rx: 6, fill: '#eef3fa', stroke: C.rule2 }, svg);
      text(svg, X(38), 145, 'solvent', { 'text-anchor': 'middle', 'font-size': 11, fill: C.soft });
      svgEl('line', { x1: X(66), x2: X(150), y1: 141, y2: 141, stroke: C.ink, 'stroke-width': 2 }, svg);
      box(X(150), 104, X(100), 74, 'pump', 'pressure, flow', true);
      svgEl('line', { x1: X(250), x2: X(330), y1: 141, y2: 141, stroke: C.ink, 'stroke-width': 2 }, svg);
      svgEl('circle', { cx: X(330), cy: 141, r: 6, fill: '#fff', stroke: C.ink, 'stroke-width': 1.5 }, svg);
      text(svg, X(330), 126, 'fitting', { 'text-anchor': 'middle', 'font-size': 10.5, fill: C.soft });
      svgEl('line', { x1: X(336), x2: X(370), y1: 141, y2: 141, stroke: C.ink, 'stroke-width': 2 }, svg);
      svgEl('rect', { x: X(370), y: 133, width: X(200), height: 16, rx: 3, fill: '#e9e5de', stroke: C.ink, 'stroke-width': 1 }, svg);
      text(svg, X(470), 126, 'column', { 'text-anchor': 'middle', 'font-size': 10.5, fill: C.soft });
      svgEl('line', { x1: X(570), x2: X(610), y1: 141, y2: 141, stroke: C.ink, 'stroke-width': 2 }, svg);
      box(X(610), 112, X(140), 58, 'mass spectrometer', null, false);
      tag(X(410), 30, 'QUIET', 'the method hangs on a step');
      tag(X(150), 198, 'AIR', 'gas bubbles in the pump');
      tag(X(330), 198, 'LEAK', 'a fitting lets liquid out', 'start');
      tag(X(370), 216, 'CLOG, DRIFT', 'the column narrows, fast or slowly');
      text(svg, X(10), 224, 'quiet-run reads every one of these once a second', { 'font-size': 10.5, fill: C.faint });
      if (W < 700) { /* keep tags from crowding at mid widths */ }
    } else {
      var H2 = 330; svg = newSvg(host, W, H2);
      var bx = 0, bw = Math.min(150, W * 0.42), cx = bx + bw / 2, tx = bw + 14;
      box(bx, 0, bw, 44, 'control software', 'status, heartbeat, clock', true);
      svgEl('line', { x1: cx, x2: cx, y1: 44, y2: 70, stroke: C.faint, 'stroke-dasharray': '3 3' }, svg);
      box(bx, 70, bw, 50, 'pump', 'pressure, flow', true);
      svgEl('line', { x1: cx, x2: cx, y1: 120, y2: 150, stroke: C.ink, 'stroke-width': 2 }, svg);
      svgEl('circle', { cx: cx, cy: 156, r: 6, fill: '#fff', stroke: C.ink, 'stroke-width': 1.5 }, svg);
      text(svg, cx + 10, 160, 'fitting', { 'font-size': 10.5, fill: C.soft });
      svgEl('line', { x1: cx, x2: cx, y1: 162, y2: 180, stroke: C.ink, 'stroke-width': 2 }, svg);
      svgEl('rect', { x: cx - 8, y: 180, width: 16, height: 80, rx: 3, fill: '#e9e5de', stroke: C.ink }, svg);
      text(svg, cx + 14, 224, 'column', { 'font-size': 10.5, fill: C.soft });
      svgEl('line', { x1: cx, x2: cx, y1: 260, y2: 280, stroke: C.ink, 'stroke-width': 2 }, svg);
      box(bx, 280, bw, 42, 'mass spectrometer', null, false);
      function vtag(y, name, l1, l2) {
        var t = text(svg, tx, y, '', { 'font-size': 11.5, fill: C.soft });
        var a = svgEl('tspan', { 'font-weight': 600, fill: C.neg, 'font-family': 'IBM Plex Mono, monospace' }, t); a.textContent = name;
        text(svg, tx, y + 15, l1, { 'font-size': 11, fill: C.soft });
        if (l2) text(svg, tx, y + 29, l2, { 'font-size': 11, fill: C.soft });
      }
      vtag(16, 'QUIET', 'the method hangs', 'on a step');
      vtag(90, 'AIR', 'gas bubbles', 'in the pump');
      vtag(150, 'LEAK', 'a fitting lets', 'liquid out');
      vtag(206, 'CLOG, DRIFT', 'the column narrows,', 'fast or slowly');
      text(svg, tx, 300, 'all read once', { 'font-size': 10.5, fill: C.faint });
      text(svg, tx, 314, 'a second', { 'font-size': 10.5, fill: C.faint });
    }
  }

  /* ---------- act one: one run ---------- */
  function exampleKey() {
    if (state.kind === 'healthy') return state.sev === 2 ? 'healthy-flagged' : 'healthy';
    return state.kind + '-' + state.sev;
  }
  function renderKindChips() {
    var host = $('#kindchips');
    KINDS.concat(['healthy']).forEach(function (k) {
      var b = document.createElement('button');
      b.className = 'chip'; b.type = 'button'; b.dataset.kind = k;
      b.textContent = NAMES[k];
      b.addEventListener('click', function () {
        state.kind = k;
        if (k === 'healthy') state.sev = 1; else if (state.sev < 1 || state.sev > 4) state.sev = 3;
        renderSevChips(); drawExample();
      });
      host.appendChild(b);
    });
  }
  function renderSevChips() {
    var host = $('#sevchips');
    host.querySelectorAll('.chip').forEach(function (c) { c.remove(); });
    var opts = state.kind === 'healthy'
      ? [[1, 'typical', 'a healthy run'], [2, 'false alarm', 'a healthy run that was flagged']]
      : [1, 2, 3, 4].map(function (s) { return [s, String(s), D.meta.severityLabels[state.kind][s - 1]]; });
    opts.forEach(function (o) {
      if (state.kind === 'healthy' && o[0] === 2 && !D.examples['healthy-flagged']) return;
      var b = document.createElement('button');
      b.className = 'chip'; b.type = 'button'; b.dataset.sev = o[0];
      b.innerHTML = esc(o[1]) + '<small>' + esc(o[2]) + '</small>';
      b.addEventListener('click', function () { state.sev = o[0]; drawExample(); });
      host.appendChild(b);
    });
    var M = D.meta, why = {
      leak: 'Severity is the share of pressure lost, reached over ' + M.leakRampS + ' s and kept to the end of the run.',
      clog: 'Severity is the pressure rise, reached over ' + (M.clogRampS / 60) + ' min and kept to the end of the run.',
      air: 'Severity is the depth of each of ' + M.airDips + ' short dips spread over ' + (M.airBurstS / 60) + ' min.',
      quiet: 'Severity is how long the method clock stands still while the heartbeat and the Running status carry on.',
      drift: 'Severity is how much pressure rises with each run, from run ' + (M.driftStartRun + 1) + ' of a batch of ' + M.sequenceRuns + '.',
      healthy: 'OK means a healthy run: a typical one, and one that got a false alarm at the chosen setting.'
    };
    $('#sevhint').textContent = 'Click a fault, then a severity. ' + why[state.kind];
  }
  function expectedAt(m) {
    var e = D.envelope.expected, bs = D.envelope.binSeconds, x = m / bs - 0.5;
    if (x <= 0) return e[0];
    var i = Math.floor(x);
    if (i >= e.length - 1) return e[e.length - 1];
    var f = x - i; return e[i] * (1 - f) + e[i + 1] * f;
  }

  function drawExample() {
    document.querySelectorAll('#kindchips .chip').forEach(function (b) { b.setAttribute('aria-pressed', b.dataset.kind === state.kind ? 'true' : 'false'); });
    document.querySelectorAll('#sevchips .chip').forEach(function (b) { b.setAttribute('aria-pressed', +b.dataset.sev === state.sev ? 'true' : 'false'); });
    var ex = D.examples[exampleKey()];
    var host = $('#exviz'), W = host.clientWidth || 600, narrow = W < 560;
    var set = settingOf(state.setting), lv = set.levelPct / 100;
    var p = ex.trace.p, m = ex.trace.m, n = p.length, runS = D.meta.runSeconds;
    var isDrift = ex.kind === 'drift';
    var padL = 44, padR = 8;
    var h0 = isDrift ? (narrow ? 190 : 156) : 0, band = 34, h1 = (narrow ? 180 : 250) + band, gap = 16, h2 = 64, axis = 40;
    var H = h0 + h1 + gap + h2 + axis;
    var svg = newSvg(host, W, H);
    svg.setAttribute('aria-label', 'Pressure trace of one synthetic run with the envelope, fixed limits and alarms');
    var X = function (t) { return padL + (t / runS) * (W - padL - padR); };

    if (isDrift) drawDriftStrip(svg, ex, W, padL, padR, h0 - 20, narrow);

    // y scale of the main plot
    var lo = Math.min(D.limits.low, Math.min.apply(null, p)), hi = Math.max(D.limits.high, Math.max.apply(null, p));
    for (var i = 0; i < n; i += 5) { var e0 = expectedAt(m[i]); lo = Math.min(lo, e0 * (1 - lv)); hi = Math.max(hi, e0 * (1 + lv)); }
    lo = Math.floor((lo - 1) / 5) * 5; hi = Math.ceil((hi + 1) / 5) * 5;
    var top = h0 + band, bot = h0 + h1;
    var Y = function (v) { return bot - (v - lo) / (hi - lo) * (bot - top); };
    var step = (hi - lo) > 40 ? 10 : 5;
    for (var v = lo; v <= hi; v += step) {
      svgEl('line', { x1: padL, x2: W - padR, y1: Y(v), y2: Y(v), stroke: C.rule }, svg);
      text(svg, padL - 6, Y(v) + 4, String(v), { 'text-anchor': 'end', 'font-size': 10.5, fill: C.soft, 'font-family': 'IBM Plex Mono, monospace' });
    }
    text(svg, 0, top - 0, 'pressure index', { 'font-size': 10.5, fill: C.faint, transform: 'translate(10,' + ((top + bot) / 2) + ') rotate(-90)', 'text-anchor': 'middle', x: 0, y: 0 });
    // envelope band
    var up = [], dn = [];
    for (var j = 0; j < n; j += 2) { var e = expectedAt(m[j]); up.push(X(j) + ',' + Y(e * (1 + lv))); dn.push(X(j) + ',' + Y(e * (1 - lv))); }
    svgEl('polygon', { points: up.concat(dn.reverse()).join(' '), fill: 'rgba(42,120,214,0.14)', stroke: 'none' }, svg);
    var mid = [];
    for (j = 0; j < n; j += 2) mid.push(X(j) + ',' + Y(expectedAt(m[j])));
    svgEl('polyline', { points: mid.join(' '), fill: 'none', stroke: C.qr, 'stroke-width': 1, 'stroke-dasharray': '4 3', opacity: 0.8 }, svg);
    // fixed limits
    [['high', D.limits.high], ['low', D.limits.low]].forEach(function (l) {
      svgEl('line', { x1: padL, x2: W - padR, y1: Y(l[1]), y2: Y(l[1]), stroke: C.fx, 'stroke-width': 1.4, 'stroke-dasharray': '6 4' }, svg);
      text(svg, W - padR, Y(l[1]) + (l[0] === 'high' ? -5 : 13), 'fixed ' + l[0] + ' limit ' + l[1].toFixed(1), { 'text-anchor': 'end', 'font-size': 10.5, fill: C.ink, 'class': 'halo' });
    });
    // pressure
    var pts = [];
    for (j = 0; j < n; j++) pts.push(X(j).toFixed(1) + ',' + Y(p[j]).toFixed(1));
    svgEl('polyline', { points: pts.join(' '), fill: 'none', stroke: C.ink, 'stroke-width': 1, 'stroke-linejoin': 'round' }, svg);

    // markers
    var alarms = (ex.alarms[state.setting] || []);
    var first = alarms[0] || null;
    var start = ex.start;
    // Marker labels sit in a two-row band above the plot: the alarm on the first row, the fault start on the
    // second, each beside its own line and pulled inside the edges, with a white halo so lines never cut them.
    function label(x, y, s, prefer, attrs) {
      var w = s.length * 6.3, a = { 'font-size': 10.5, fill: C.ink, 'class': 'halo' };
      for (var k in attrs || {}) a[k] = attrs[k];
      var right = prefer === 'start' ? (x + 4 + w <= W - padR) : !(x - 4 - w >= padL);
      a['text-anchor'] = right ? 'start' : 'end';
      return text(svg, x + (right ? 4 : -4), y, s, a);
    }
    if (start !== null && start !== undefined) {
      svgEl('line', { x1: X(start), x2: X(start), y1: h0 + 18, y2: bot + gap + h2, stroke: C.faint, 'stroke-width': 1.5 }, svg);
      label(X(start), h0 + 28, (narrow ? 'fault ' : 'fault starts ') + clock(start), 'end');
    }
    if (first) {
      svgEl('line', { x1: X(first.t), x2: X(first.t), y1: h0 + 2, y2: bot + gap + h2, stroke: C.qr, 'stroke-width': 2 }, svg);
      label(X(first.t), h0 + 12, 'quiet-run: ' + NAMES[first.k] + ' ' + clock(first.t), 'start', { 'font-weight': 600, 'font-size': 11 });
    }
    if (ex.fixed) {
      svgEl('line', { x1: X(ex.fixed.t), x2: X(ex.fixed.t), y1: top, y2: bot + gap + h2, stroke: C.fx, 'stroke-width': 2 }, svg);
      label(X(ex.fixed.t), bot - 22, 'fixed limit ' + clock(ex.fixed.t), 'start');
    }

    // method clock strip
    var t2 = bot + gap, b2 = t2 + h2;
    var Y2 = function (s) { return b2 - (s / runS) * (b2 - t2); };
    svgEl('rect', { x: padL, y: t2, width: W - padL - padR, height: h2, fill: '#faf9f7', stroke: C.rule }, svg);
    var mp = [];
    for (j = 0; j < n; j += 1) mp.push(X(j).toFixed(1) + ',' + Y2(m[j]).toFixed(1));
    svgEl('polyline', { points: mp.join(' '), fill: 'none', stroke: C.ink, 'stroke-width': 1.4 }, svg);
    text(svg, padL + 6, t2 + 13, narrow ? 'method clock' : 'method clock: ' + clock(m[n - 1]) + ' of ' + clock(runS) + ' done by the end', { 'font-size': 10.5, fill: C.soft, 'class': 'halo' });
    text(svg, padL - 6, b2, '0', { 'text-anchor': 'end', 'font-size': 10.5, fill: C.soft, 'font-family': 'IBM Plex Mono, monospace' });
    text(svg, padL - 6, t2 + 9, clock(runS), { 'text-anchor': 'end', 'font-size': 10, fill: C.soft, 'font-family': 'IBM Plex Mono, monospace' });
    // x axis
    var xt = narrow ? [0, 6, 12, 18, 21] : [0, 3, 6, 9, 12, 15, 18, 21];
    xt.forEach(function (mn) {
      text(svg, X(mn * 60), b2 + 14, String(mn), { 'text-anchor': 'middle', 'font-size': 10.5, fill: C.soft, 'font-family': 'IBM Plex Mono, monospace' });
    });
    text(svg, (padL + W - padR) / 2, b2 + 32, 'minutes into the run' + (isDrift ? ' (the run shown above in blue)' : ''), { 'text-anchor': 'middle', 'font-size': 11.5, fill: C.ink });

    renderReadout(ex, first, alarms);
  }

  function drawDriftStrip(svg, ex, W, padL, padR, h, narrow) {
    var sq = ex.sequence, nRun = sq.medianPct.length, set = state.setting;
    var vals = sq.medianPct.concat(sq.creepPct);
    var lo = Math.min(0, Math.min.apply(null, vals)), hi = Math.max.apply(null, vals);
    lo = Math.floor(lo - 0.5); hi = Math.ceil(hi + 0.5);
    var top = narrow ? 72 : 46, bot = h - 4;
    var bw = (W - padL - padR) / nRun;
    var Y = function (v) { return bot - (v - lo) / (hi - lo) * (bot - top); };
    var lines = narrow
      ? ['Each bar is one run of the batch: its median', 'pressure against the envelope, in %.', 'Dashed: the trend injected. Orange dot: fixed', 'limits tripped. Outlined: quiet-run\'s first alarm.']
      : ['Each bar is one run of the batch: its median pressure against the envelope, in %.', 'Dashed: the trend injected. Orange dot: the fixed limits tripped in that run. Outlined: quiet-run\'s first alarm.'];
    lines.forEach(function (l, i) { text(svg, 0, 11 + i * 14, l, { 'font-size': 11, fill: i < (narrow ? 2 : 1) ? C.ink : C.soft }); });
    var vstep = (hi - lo > 8 ? 4 : 2);
    for (var v = Math.ceil(lo / vstep) * vstep; v <= hi; v += vstep) {
      svgEl('line', { x1: padL, x2: W - padR, y1: Y(v), y2: Y(v), stroke: v === 0 ? C.rule2 : C.rule }, svg);
      text(svg, padL - 6, Y(v) + 4, (v > 0 ? '+' : '') + v, { 'text-anchor': 'end', 'font-size': 10, fill: C.soft, 'font-family': 'IBM Plex Mono, monospace' });
    }
    var firstInfo = sq.first[set];
    var flaggedRun = firstInfo ? firstInfo.run : -1;
    for (var i = 0; i < nRun; i++) {
      var x = padL + i * bw + bw * 0.15, w = bw * 0.7, val = sq.medianPct[i];
      var col = i === ex.shownRun ? C.qr : (i >= D.meta.driftStartRun ? '#b9b3a9' : '#dcd7cf');
      svgEl('rect', { x: x, y: Math.min(Y(val), Y(0)), width: w, height: Math.max(1, Math.abs(Y(val) - Y(0))), fill: col }, svg);
      if (sq.fixedTripped[i]) svgEl('circle', { cx: x + w / 2, cy: top - 4, r: 3, fill: C.fx }, svg);
      if (i === flaggedRun) svgEl('rect', { x: x - 2, y: top - 10, width: w + 4, height: bot - top + 12, fill: 'none', stroke: C.qr, 'stroke-width': 1.5, rx: 2 }, svg);
      if ((i + 1) % 5 === 0 || i === 0) text(svg, x + w / 2, bot + 13, String(i + 1), { 'text-anchor': 'middle', 'font-size': 10, fill: C.soft, 'font-family': 'IBM Plex Mono, monospace' });
    }
    var cp = sq.creepPct.map(function (c, i) { return (padL + i * bw + bw / 2) + ',' + Y(c); });
    svgEl('polyline', { points: cp.join(' '), fill: 'none', stroke: C.ink, 'stroke-width': 1.2, 'stroke-dasharray': '4 3' }, svg);
    var xs = padL + D.meta.driftStartRun * bw;
    svgEl('line', { x1: xs, x2: xs, y1: top - 6, y2: bot, stroke: C.faint, 'stroke-width': 1.5 }, svg);
    text(svg, xs + 4, bot - 4, 'drift starts', { 'font-size': 10, fill: C.ink });
  }

  function renderReadout(ex, first, alarms) {
    var set = state.setting, out = '';
    var startTxt = function (t) { return ex.start !== null && ex.start !== undefined ? ', ' + dur(t - ex.start) + ' after the fault began' : ''; };
    if (ex.kind === 'drift') {
      var fi = ex.sequence.first[set];
      if (fi) {
        var a0 = fi.alarms[0], k = fi.run - D.meta.driftStartRun + 1;
        out += '<div><div class="who q">quiet-run, ' + set + ' setting</div><div class="what">' + NAMES[a0.k] + ' in run ' + (fi.run + 1) + ' of 20, the ' + ordinal(k) +
          ' run of the drift, at ' + clock(a0.t) + '.</div><div class="why">' + esc(a0.r) + '</div></div>';
      } else out += '<div><div class="who q">quiet-run, ' + set + ' setting</div><div class="what">No alarm in the whole batch of 20 runs.</div></div>';
      var ft = ex.sequence.fixedTripped.indexOf(true);
      out += '<div><div class="who f">fixed limits</div><div class="what">' + (ft < 0 ? 'No alarm in the whole batch.' : 'First tripped in run ' + (ft + 1) + ' of 20, the ' + ordinal(ft - D.meta.driftStartRun + 1) + ' run of the drift.') + '</div>' +
        (ex.fixed ? '<div class="why">In the run shown: ' + esc(ex.fixed.r) + '</div>' : '') + '</div>';
    } else {
      out += '<div><div class="who q">quiet-run, ' + set + ' setting</div>' + (first
        ? '<div class="what">' + NAMES[first.k] + ' at ' + clock(first.t) + startTxt(first.t) + '.</div><div class="why">' + esc(first.r) + '</div>' +
          (alarms.length > 1 ? '<div class="why">Later in the run: ' + alarms.slice(1).map(function (a) { return NAMES[a.k] + ' at ' + clock(a.t); }).join(', ') + '.</div>' : '')
        : '<div class="what">No alarm in this run.</div><div class="why">' + (ex.kind === 'healthy' ? 'Pressure stayed inside the envelope, there were no bursts of short swings, and the method clock kept moving.' : 'The fault stayed inside what this setting accepts as normal.') + '</div>') + '</div>';
      out += '<div><div class="who f">fixed limits</div>' + (ex.fixed
        ? '<div class="what">Tripped at ' + clock(ex.fixed.t) + startTxt(ex.fixed.t) + '.</div><div class="why">' + esc(ex.fixed.r) + '</div>'
        : '<div class="what">No alarm in this run.</div>') + '</div>';
    }
    out += '<div class="cellstat">' + cellStat(ex) + '</div>';
    $('#readout').innerHTML = out;
  }
  function ordinal(k) { var s = ['th', 'st', 'nd', 'rd']; var v = k % 100; return k + (s[(v - 20) % 10] || s[v] || s[0]); }
  function cellStat(ex) {
    var set = state.setting;
    if (ex.kind === 'healthy')
      return 'Across all ' + D.healthy.runs + ' healthy runs, quiet-run flagged ' + D.healthy.qr[set].flagged + ' at this setting and the fixed limits flagged ' + D.healthy.fixed.flagged + '.';
    var c = cellOf(ex.kind, ex.sev), q = caughtList(c, 'qr', set), f = caughtList(c, 'fixed'), N = c.qr[set].delay.length;
    var fmt = function (arr) { return c.unit === 's' ? dur(median(arr)) : f1(median(arr)) + ' runs'; };
    return 'Across all ' + N + ' ' + (c.unit === 's' ? 'runs' : 'batches') + ' at this severity, quiet-run flagged <b>' + q.length + '</b>' +
      (q.length ? ' (median ' + fmt(q) + ' after the ' + (c.unit === 's' ? 'fault' : 'drift') + ' began)' : '') + ' and the fixed limits flagged <b>' + f.length + '</b>' +
      (f.length ? ' (median ' + fmt(f) + ')' : '') + '.' + (ex.rep !== undefined ? ' The run shown is number ' + (ex.rep + 1) + ' of the ' + N + '.' : '');
  }

  function renderAct1Verdict() {
    var set = 'chosen', s = summary(set), c = cellOf('leak', 3);
    var q = caughtList(c, 'qr', set), f = caughtList(c, 'fixed');
    var inN = 0, inQ = 0, inF = 0;
    INRUN.forEach(function (k) { inN += s.byKind[k].n; inQ += s.byKind[k].qr; inF += s.byKind[k].fx; });
    $('#act1verdict').innerHTML = '<b>The envelope flags most faults within a minute or two; the fixed limits flag few, and late.</b> ' +
      'Over the ' + inN + ' single-run faults at the chosen setting, quiet-run flagged ' + inQ + ' and the fixed limits flagged ' + inF + '. ' +
      'For leaks of ' + V.leak3pct + ' percent, quiet-run\'s median time to flag was ' + dur(median(q)) + ' and the fixed limits\' was ' + dur(median(f)) +
      ', when the shape of the run had dropped toward the low limit.';
  }

  /* ---------- act two: the names it gives ---------- */
  function renderConfusion() {
    var set = state.setting, cf = D.confusion[set];
    var cols = ['ok', 'leak', 'clog', 'air', 'quiet', 'drift'];
    var rows = KINDS.concat(['healthy']);
    var h = '<thead><tr><th>Injected</th>' + cols.map(function (c) { return '<th>' + (c === 'ok' ? 'not flagged' : NAMES[c]) + '</th>'; }).join('') + '<th>Runs</th></tr></thead><tbody>';
    rows.forEach(function (r) {
      var row = r === 'healthy' ? healthyRow(set) : cf[r];
      var total = cols.reduce(function (a, c) { return a + (row[c] || 0); }, 0);
      h += '<tr><td>' + (r === 'healthy' ? 'healthy' : NAMES[r]) + '</td>' + cols.map(function (c) {
        var v = row[c] || 0, diag = (c === r) || (r === 'healthy' && c === 'ok');
        var cls = 'n' + (diag ? ' diag' : v === 0 ? ' z' : (c === 'ok' || r === 'healthy') ? ' bad' : '');
        return '<td class="' + cls + '">' + v + '</td>';
      }).join('') + '<td class="n">' + total + '</td></tr>';
    });
    $('#conftable').innerHTML = h + '</tbody>';
    var dr = cf.drift, miss = cols.map(function () { return 0; });
    $('#confnote').innerHTML = 'Shaded cells are the right name. Each fault row has ' + D.meta.reps * 4 + ' runs, ' + D.meta.reps +
      ' at each severity; a drift row counts batches of ' + D.meta.sequenceRuns + ' runs, and only alarms after the drift began. The healthy row counts ' + D.healthy.runs +
      ' runs, and anything off its first column is a false alarm. Setting: ' + set + '.';
    var drClog = (dr.clog || 0), drN = cols.reduce(function (a, c) { return a + (dr[c] || 0); }, 0);
    $('#act2verdict').innerHTML = '<b>Slow drift is usually named CLOG first.</b> At the chosen setting, ' + (D.confusion.chosen.drift.clog || 0) + ' of ' + drN +
      ' drift batches were first flagged as CLOG inside a single run that had landed high by chance, before the cross-run rule had enough runs to say DRIFT. ' +
      'The pressure was above the envelope either way, so the alarm was right, but the name points at a sudden blockage when the cause is a slow one.';
    void drClog; void miss;
  }
  function healthyRow(set) {
    var r = { ok: D.healthy.runs - D.healthy.qr[set].flagged };
    var bk = D.healthy.qr[set].byKind; for (var k in bk) r[k] = bk[k];
    return r;
  }

  /* ---------- where it fails: settings side by side ---------- */
  function renderTrade() {
    var leak1 = cellOf('leak', 1), q1 = cellOf('quiet', 1), s0 = summary('chosen');
    var h = '<thead><tr><th>Check</th><th>Faults flagged, of ' + s0.faults + '</th><th>' + D.meta.severityLabels.leak[0].replace(' lost', '') + ' leaks, of ' + leak1.qr.chosen.delay.length +
      '</th><th>' + D.meta.severityLabels.quiet[0] + ' stalls, of ' + q1.qr.chosen.delay.length + '</th><th>Median time to flag</th><th>Healthy runs flagged, of ' + s0.healthyN + '</th></tr></thead><tbody>';
    D.settings.forEach(function (st) {
      var s = summary(st.name);
      h += '<tr class="' + (st.name === state.setting ? 'ref' : '') + '"><td>quiet-run, ' + st.name + '</td><td class="n">' + s.qr + '</td><td class="n">' +
        caughtList(leak1, 'qr', st.name).length + '</td><td class="n">' + caughtList(q1, 'qr', st.name).length +
        '</td><td class="n">' + f1(s.qrMedMin) + ' min</td><td class="n">' + s.healthyQr + '</td></tr>';
    });
    h += '<tr><td>fixed limits</td><td class="n">' + s0.fx + '</td><td class="n">' + caughtList(leak1, 'fixed').length +
      '</td><td class="n">' + caughtList(q1, 'fixed').length + '</td><td class="n">' + f1(s0.fxMedMin) + ' min</td><td class="n">' + s0.healthyFx + '</td></tr>';
    $('#tradetable').innerHTML = h + '</tbody>';
    var sen = summary('sensitive'), cho = summary('chosen');
    $('#tradenote').innerHTML = 'The shaded row is the setting selected at the top of the page. Median time to flag counts single-run faults that were flagged; drift is left out because it is counted in runs. ' +
      'The sensitive setting flags ' + caughtList(leak1, 'qr', 'sensitive').length + ' of ' + leak1.qr.sensitive.delay.length + ' two-percent leaks, against ' +
      caughtList(leak1, 'qr', 'chosen').length + ' for the chosen one, and pays with ' + sen.healthyQr + ' false alarms on ' + sen.healthyN + ' healthy runs, against ' + cho.healthyQr + '.';
  }

  function driftMed(who) {
    var all = [];
    cellsOf('drift').forEach(function (c) { all = all.concat(caughtList(c, who, 'chosen')); });
    return median(all);
  }
  function renderModelCard() {
    var cho = summary('chosen'), E = D.envelope, M = D.meta;
    var bins = E.expected.length;
    $('#modelgloss').innerHTML =
      '<div><b>what it is</b>An expected pressure for each of ' + bins + ' slices of ' + E.binSeconds + ' s along the method, and three spreads: how far the smoothed pressure strays (' +
        E.levelSigmaPct.toFixed(2) + '%), how far one second strays from its neighbours (' + E.spikeSigmaPct.toFixed(2) + '%), and how far one whole run\'s median strays from the average run\'s (' + E.runSigmaPct.toFixed(2) + '%).</div>' +
      '<div><b>what it predicts</b>Nothing about the future. It says what a healthy run of this method shows at each point, and the rules compare each second of a new run with it.</div>' +
      '<div><b>fitted on</b>' + M.trainingRuns + ' healthy synthetic runs and nothing else. The fixed limits were learned from the same ' + M.trainingRuns + ' runs, so both checks start from the same data.</div>' +
      '<div><b>scored on</b>' + D.healthy.runs + ' healthy runs and ' + cho.faults + ' runs with faults, generated separately from their own seeds. None of them was used to learn the envelope or to choose the settings, so no number here is tuned to itself.</div>' +
      '<div><b>error, in runs</b>At the chosen setting it missed ' + (cho.faults - cho.qr) + ' of ' + cho.faults + ' faults and raised ' + cho.healthyQr + ' false alarms on ' + cho.healthyN +
        ' healthy runs. The fixed limits missed ' + (cho.faults - cho.fx) + ' and raised ' + cho.healthyFx + '.</div>' +
      '<div><b>verdict</b>Useful on air, on stalls longer than its ' + settingOf('chosen').quietS + ' s wait, and on leaks and clogs of a few percent. Weak on the smallest leaks. On drift it flagged a median ' +
        f1(driftMed('qr')) + ' runs in, against ' + f1(driftMed('fixed')) + ' for a tight fixed limit.</div>';
  }

  function renderLimits() {
    var cho = summary('chosen'), set = settingOf('chosen');
    var lim = D.limits, lowest = Math.min.apply(null, D.envelope.expected), highest = Math.max.apply(null, D.envelope.expected);
    $('#limits').innerHTML =
      '<p><b>Everything is synthetic.</b> The faults are shaped the way I expect them to look. A real leak or bubble will be messier, and a real instrument will have faults I did not imagine. The next test is real traces, which I do not have.</p>' +
      '<p><b>Small leaks get past it.</b> The chosen setting flags ' + cho.smallLeak + ' of ' + cho.smallLeakN + ' two-percent leaks, because it waits for pressure to sit ' + set.levelPct.toFixed(1) +
      '% off the envelope, and healthy runs here differ from each other by about ' + D.envelope.runSigmaPct.toFixed(1) + '%. Comparing each run with its own first minutes would be a tighter reference. I have not built it, and it would miss a fault present from the first second.</p>' +
      '<p><b>Stalls under ' + set.quietS + ' seconds are ignored on purpose.</b> Healthy runs here pause their method clock for up to ' + D.meta.pauseMaxS + ' seconds, and the chosen setting leaves room above that.</p>' +
      '<p><b>The envelope belongs to one method.</b> It was learned from ' + D.meta.trainingRuns + ' healthy runs of one invented method. A new column, method or instrument needs its own healthy runs first.</p>' +
      '<p><b>The fixed limits were set kindly.</b> They sit ' + D.meta.fixedMarginIndex + ' index points outside the most extreme healthy pressure seen in training (' + lim.low.toFixed(1) + ' and ' + lim.high.toFixed(1) +
      ', around an expected range of ' + lowest.toFixed(1) + ' to ' + highest.toFixed(1) + '), which is tighter than most real setups and makes them faster here than usual. The same tightness is why they flagged ' + cho.healthyFx + ' of ' + cho.healthyN + ' healthy runs.</p>';
  }

  /* ---------- small numbers in the prose, filled from the study file ---------- */
  var V = {};
  function fillValues() {
    var leak3 = D.meta.severitySizes.leak[2] * 100;
    V = {
      trainingRuns: D.meta.trainingRuns, fixedDebounceS: D.meta.fixedDebounceS, healthyRuns: D.healthy.runs,
      leak3pct: Math.round(leak3), leak3left: Math.round(100 - leak3), faults: summary('chosen').faults,
      perKind: D.meta.reps * 4, sequenceRuns: D.meta.sequenceRuns, runMinutes: Math.round(D.meta.runSeconds / 60)
    };
    document.querySelectorAll('[data-v]').forEach(function (el) {
      if (V[el.dataset.v] === undefined) { if (window.console) console.error('no value for', el.dataset.v); return; }
      el.textContent = V[el.dataset.v];
    });
  }

  /* ---------- how I built it ---------- */
  function renderTests() {
    if (!TT) return;
    var label = { LeakTests: 'LEAK', ClogTests: 'CLOG', AirTests: 'AIR', QuietTests: 'QUIET', DriftTests: 'DRIFT', HealthyTests: 'OK, healthy runs', EnvelopeTests: 'Envelope and fixed limits', CsvAndSimulatorTests: 'CSV, JSON and the simulator' };
    var groups = {}, order = [];
    TT.tests.forEach(function (t) { if (!groups[t.cls]) { groups[t.cls] = []; order.push(t.cls); } groups[t.cls].push(t); });
    var h = '<div class="card-title" style="margin-top:18px">The ' + TT.total + ' tests</div><p class="small">' + TT.passed + ' of ' + TT.total +
      ' passed in the test run that wrote this list; the badge at the top of the page shows the latest run in GitHub Actions.</p><div class="tests">';
    order.forEach(function (cls) {
      h += '<div class="grp"><h4>' + esc(label[cls] || cls) + '</h4>' + groups[cls].map(function (t) {
        var ok = t.outcome === 'Passed';
        return '<div class="t"><i class="' + (ok ? '' : 'fail') + '">' + (ok ? 'pass' : 'fail') + '</i><span>' + esc(t.name) + '</span></div>';
      }).join('') + '</div>';
    });
    $('#testlist').innerHTML = h + '</div>';
  }
  function renderCli() {
    $('#cliout').innerHTML = '<span class="cmd">$ quiet-run check leak-7pct.csv --envelope envelope.json</span>\n' + esc(D.cliExample.trim());
  }
  function renderFooter() {
    $('#footnote').textContent = 'Every number on this page is generated by `' + D.meta.command + '` from the synthetic runs it creates, and computed in your browser from that file; none is typed.';
  }

  function selfCheck() {
    var problems = [];
    if (D.cells.length !== 20) problems.push('expected 20 cells, found ' + D.cells.length);
    D.cells.forEach(function (c) { if (c.qr.chosen.delay.length !== D.meta.reps) problems.push(c.kind + '-' + c.sev + ' has ' + c.qr.chosen.delay.length + ' runs'); });
    KINDS.forEach(function (k) { [1, 2, 3, 4].forEach(function (s) { if (!D.examples[k + '-' + s]) problems.push('missing example ' + k + '-' + s); }); });
    if (problems.length && window.console) console.error('quiet-run self-check:', problems);
  }

  /* ---------- the walkthrough ---------- */
  function tour() {
    var root = $('#tour'), hl = $('.tour-hl', root), card = $('.tour-card', root), idx = 0;
    var reduced = matchMedia('(prefers-reduced-motion: reduce)').matches;
    var STEPS = [
      { sel: 'header h1', k: 'The question', html: 'This page asks how soon software can tell that a liquid chromatography run has gone wrong. <b>quiet-run</b> is the C# tool built to answer it. Every run it is scored on here is synthetic.' },
      { sel: '#blufmain', k: 'The answer', html: 'The whole result. Each dot is a run with one injected fault: further left is flagged sooner, grey is never. Drift and false alarms are just below. <b>Click a setting</b> to see what a stricter quiet-run catches and costs.' },
      { sel: '#qlines', k: 'In four sentences', html: 'The same result as four questions with one answer each. The two in red are where quiet-run fails or costs something.' },
      { sel: '#domain', k: 'The faults', html: 'Where each of the five faults happens in a simplified flow path, and what quiet-run reads from the instrument once a second.' },
      { sel: '#exmain', k: 'One run', html: 'One run at a time. <b>Click a fault and a severity</b>, then check the blue alarm line and its reason against the trace. Try QUIET: the pressure looks normal and the method clock goes flat.' },
      { sel: '#confcard', k: 'The names', html: 'Whether quiet-run gave each fault its right name. Shaded cells are right; red cells are misses or false alarms.' },
      { sel: '#tradecard', k: 'The cost', html: 'What each setting catches and what it costs, next to the fixed limits. The limits under the table say where the whole exercise stops.' },
      { sel: '#builthead', k: 'How it was built', html: 'Rules and tests first, then code written by coding agents and read by me. Below it are the test list, generated from the last test run, and the command-line output, generated by the study.' }
    ];
    STEPS.forEach(function (s, i) { s.k += ' · ' + (i + 1) + ' of ' + STEPS.length; });
    function place() {
      var st = STEPS[idx], elm = document.querySelector(st.sel);
      if (!elm) { next(); return; }
      var r = elm.getBoundingClientRect(), sx = window.scrollX, sy = window.scrollY;
      var docTop = r.top + sy, docLeft = r.left + sx;
      root.style.height = document.documentElement.scrollHeight + 'px';
      var maxScroll = Math.max(0, document.documentElement.scrollHeight - innerHeight);
      var target = Math.max(0, Math.min(docTop - 14, maxScroll)), vTop = docTop - target;
      var cw = Math.min(400, innerWidth - 32);
      var dots = STEPS.map(function (_, i) { return '<i class="' + (i === idx ? 'on' : '') + '"></i>'; }).join('');
      card.innerHTML = '<div class="tk">' + st.k + '</div><p>' + st.html + '</p><div class="tour-nav"><div class="dots">' + dots + '</div>' +
        (idx > 0 ? '<button class="tour-btn" id="tprev">Back</button>' : '') +
        '<button class="tour-btn" id="tskip">Close</button><button class="tour-btn primary" id="tnext">' + (idx < STEPS.length - 1 ? 'Next' : 'Done') + '</button></div>';
      // Measure the card as written, so the highlight leaves exactly the room it needs.
      var ch = card.offsetHeight || 240;
      var fitsRight = r.left + r.width + 18 + cw <= innerWidth - 16;
      var hh = fitsRight ? r.height : Math.max(110, Math.min(r.height, innerHeight - vTop - ch - 30));
      hl.style.left = (docLeft - 6) + 'px'; hl.style.top = (docTop - 6) + 'px';
      hl.style.width = (r.width + 12) + 'px'; hl.style.height = (hh + 12) + 'px';
      var cx, cy;
      if (fitsRight) { cx = docLeft + r.width + 18; cy = docTop; }
      else { cx = Math.min(docLeft, sx + innerWidth - 16 - cw); cy = docTop + hh + 16; }
      card.style.left = Math.max(sx + 16, cx) + 'px';
      card.style.top = Math.max(target + 16, cy) + 'px';
      $('#tnext').onclick = next; $('#tskip').onclick = stop;
      var pv = $('#tprev'); if (pv) pv.onclick = function () { idx = Math.max(0, idx - 1); place(); };
      window.scrollTo({ top: target, behavior: reduced ? 'auto' : 'smooth' });
    }
    function next() { if (idx >= STEPS.length - 1) { stop(); return; } idx++; place(); }
    function stop() { root.classList.remove('on'); try { localStorage.setItem('qr-tour', 'seen'); } catch (e) { } }
    function start() { idx = 0; root.classList.add('on'); place(); }
    window.__qrTour = { start: start, place: place, go: function (i) { idx = i; root.classList.add('on'); place(); }, steps: STEPS.length };
    $('#tourbtn').addEventListener('click', start);
    var replace = function () { if (root.classList.contains('on')) place(); };
    window.addEventListener('resize', replace);
    window.addEventListener('load', replace);
    if (document.fonts && document.fonts.ready) document.fonts.ready.then(replace);
    var seen = false;
    try { seen = localStorage.getItem('qr-tour') === 'seen'; } catch (e) { }
    if (!seen && !location.search.includes('tour=off')) setTimeout(start, 700);
  }

  /* ---------- go ---------- */
  document.addEventListener('DOMContentLoaded', function () {
    if (typeof D === 'undefined') { $('#dek').textContent = 'The study file did not load.'; return; }
    selfCheck();
    fillValues();
    renderHeader();
    renderSettingChips();
    renderKindChips();
    renderSevChips();
    drawDomain();
    renderAct1Verdict();
    renderModelCard();
    renderLimits();
    renderTests();
    renderCli();
    renderFooter();
    refreshSetting();
    var lastW = window.innerWidth, timer = null;
    window.addEventListener('resize', function () {
      if (window.innerWidth === lastW) return;
      lastW = window.innerWidth;
      clearTimeout(timer);
      timer = setTimeout(function () { drawBluf(); drawDomain(); drawExample(); }, 120);
    });
    tour();
  });
})();
