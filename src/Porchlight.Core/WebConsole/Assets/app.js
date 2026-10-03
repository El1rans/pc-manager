// Porchlight web console. Read-only: this script only ever GETs /api/stats and draws it.
// Every value from the server is inserted with textContent, never as HTML.
(function () {
  "use strict";

  var POLL_MS = 2000;
  var HISTORY = 60;
  var FILLING_PERCENT = 85;

  var el = function (id) { return document.getElementById(id); };
  var history = {};
  var tiles = {};
  var timer = null;
  var failures = 0;

  function readKey() {
    var match = /(?:^|&)key=([^&]*)/.exec(location.hash.slice(1));
    return match ? decodeURIComponent(match[1]) : "";
  }

  function make(tag, className, text) {
    var node = document.createElement(tag);
    if (className) { node.className = className; }
    if (text !== undefined && text !== null) { node.textContent = text; }
    return node;
  }

  function scaled(value, step, units, digitsFrom) {
    var i = 0;
    while (value >= step && i < units.length - 1) { value /= step; i++; }
    return (i >= digitsFrom ? value.toFixed(1) : Math.round(value).toString()) + " " + units[i];
  }

  function bytes(value) { return scaled(Math.max(value || 0, 0), 1024, ["B", "KB", "MB", "GB", "TB"], 1); }
  function bitRate(bytesPerSecond) { return scaled(Math.max(bytesPerSecond || 0, 0) * 8, 1000, ["bps", "Kbps", "Mbps", "Gbps"], 1); }
  function percent(value) { return value === null || value === undefined ? "n/a" : Math.round(value) + "%"; }

  function duration(ms) {
    var minutes = Math.max(0, Math.floor(ms / 60000));
    var d = Math.floor(minutes / 1440), h = Math.floor((minutes % 1440) / 60), m = minutes % 60;
    if (d > 0) { return d + "d " + h + "h " + m + "m"; }
    if (h > 0) { return h + "h " + m + "m"; }
    return m + "m";
  }

  var SVG = "http://www.w3.org/2000/svg";

  // Each metric's colour and icon (docs/specs/39-vivid-colour.md), matching the app's Dashboard.
  var metricLook = {
    cpu: { hue: "blue", icon: "M4 4h8v8H4z M6 1.5V4 M10 1.5V4 M6 12v2.5 M10 12v2.5 M1.5 6H4 M1.5 10H4 M12 6h2.5 M12 10h2.5" },
    memory: { hue: "violet", icon: "M1.5 4.5h13v7h-13z M4.5 7v2 M7 7v2 M9.5 7v2 M12 7v2" },
    gpu: { hue: "green", icon: "M1.5 4h13v8h-13z M6 8a2 2 0 1 0 4 0a2 2 0 1 0-4 0 M3.5 6.5H5 M3.5 9.5H5" },
    disk: { hue: "teal", icon: "M2 3.5h12v9H2z M2 10h12 M11 11.3h.5" },
    down: { hue: "amber", icon: "M8 2.5v9 M4 8l4 4 4-4 M3 14h10" },
    up: { hue: "coral", icon: "M8 13.5v-9 M4 8l4-4 4 4 M3 2h10" }
  };

  // Same order and hash as the app's ProcessRowViewModel, so an app has the same colour in both.
  var letterHues = ["blue", "violet", "green", "teal", "coral", "amber"];

  function hueFor(name) {
    var sum = 0, upper = name.toUpperCase();
    for (var i = 0; i < upper.length; i++) { sum = (Math.imul(sum, 31) + upper.charCodeAt(i)) | 0; }
    return letterHues[(sum >>> 0) % letterHues.length];
  }

  function svgNode(tag, attributes) {
    var node = document.createElementNS(SVG, tag);
    Object.keys(attributes).forEach(function (name) { node.setAttribute(name, attributes[name]); });
    return node;
  }

  function tile(id, title, max) {
    if (tiles[id]) { return tiles[id]; }
    var look = metricLook[id];
    var root = make("div", "tile");
    root.setAttribute("data-hue", look.hue);
    var t = { root: root, max: max };
    var head = root.appendChild(make("div", "tile-head"));
    var icon = head.appendChild(make("span", "tile-icon"));
    icon.setAttribute("aria-hidden", "true");
    icon.appendChild(svgNode("svg", { viewBox: "0 0 16 16" })).appendChild(svgNode("path", { d: look.icon }));
    head.appendChild(make("div", "tile-title", title));
    t.value = root.appendChild(make("div", "tile-value", "n/a"));
    t.detail = root.appendChild(make("div", "tile-detail", ""));
    var wrap = root.appendChild(make("div", "spark-wrap"));
    var svg = wrap.appendChild(svgNode("svg", {
      "class": "spark", viewBox: "0 0 " + (HISTORY - 1) + " 100", preserveAspectRatio: "none", "aria-hidden": "true"
    }));
    svg.appendChild(svgNode("line", { x1: "0", x2: String(HISTORY - 1), y1: "100", y2: "100" }));
    t.area = svg.appendChild(svgNode("polygon", {}));
    t.line = svg.appendChild(svgNode("polyline", {}));
    t.dot = wrap.appendChild(make("span", "spark-dot"));
    t.dot.hidden = true;
    el("metric-tiles").appendChild(root);
    tiles[id] = t;
    history[id] = [];
    return t;
  }

  function updateTile(id, title, max, raw, text, detail) {
    var t = tile(id, title, max);
    t.value.textContent = text;
    t.detail.textContent = detail || "";
    var series = history[id];
    series.push(raw === null || raw === undefined ? null : raw);
    if (series.length > HISTORY) { series.shift(); }

    // Fixed 0-100 scale for percentages; rates scale to their own recent peak.
    var top = max;
    if (!top) {
      top = 1;
      for (var i = 0; i < series.length; i++) { if (series[i] > top) { top = series[i]; } }
    }
    var offset = HISTORY - series.length, points = [], firstX = null, lastX = null, lastY = null;
    for (var j = 0; j < series.length; j++) {
      if (series[j] === null) { continue; }
      var y = 100 - Math.min(series[j] / top, 1) * 100;
      if (firstX === null) { firstX = offset + j; }
      lastX = offset + j;
      lastY = y;
      points.push((offset + j) + "," + y.toFixed(1));
    }
    t.line.setAttribute("points", points.join(" "));
    t.area.setAttribute("points", points.length > 1 ? points.join(" ") + " " + lastX + ",100 " + firstX + ",100" : "");
    // Only when the newest sample is the right-hand edge, where the dot is drawn.
    t.dot.hidden = lastX !== HISTORY - 1;
    if (!t.dot.hidden) { t.dot.style.top = lastY.toFixed(1) + "%"; }
  }

  function renderPerformance(p, info) {
    p = p || {};
    var memoryPercent = p.memoryTotalBytes ? 100 * p.memoryUsedBytes / p.memoryTotalBytes : null;
    var cores = info && info.logicalProcessors ? info.logicalProcessors + " logical processors" : "";
    var gpuName = info && info.gpuNames && info.gpuNames.length ? info.gpuNames[0] : "";
    var disk = p.diskReadBytesPerSecond !== null && p.diskReadBytesPerSecond !== undefined
      ? "R " + bytes(p.diskReadBytesPerSecond) + "/s · W " + bytes(p.diskWriteBytesPerSecond) + "/s" : "";

    updateTile("cpu", "CPU", 100, p.cpuPercent, percent(p.cpuPercent), cores);
    updateTile("memory", "Memory", 100, memoryPercent, percent(memoryPercent),
      memoryPercent === null ? "" : bytes(p.memoryUsedBytes) + " of " + bytes(p.memoryTotalBytes));
    updateTile("gpu", "GPU", 100, p.gpuPercent, percent(p.gpuPercent), gpuName);
    updateTile("disk", "Disk", 100, p.diskActivePercent, percent(p.diskActivePercent), disk);
    updateTile("down", "Download", 0, p.networkDownloadBytesPerSecond,
      p.networkDownloadBytesPerSecond === null || p.networkDownloadBytesPerSecond === undefined ? "n/a" : bitRate(p.networkDownloadBytesPerSecond),
      "Total: " + bytes(p.networkTotalDownloadedBytes) + " since Porchlight started");
    updateTile("up", "Upload", 0, p.networkUploadBytesPerSecond,
      p.networkUploadBytesPerSecond === null || p.networkUploadBytesPerSecond === undefined ? "n/a" : bitRate(p.networkUploadBytesPerSecond),
      "Total: " + bytes(p.networkTotalUploadedBytes) + " since Porchlight started");
  }

  var severityLabel = { Normal: "✓ Normal", Caution: "⚠ Warm", Critical: "⚠ Hot" };

  function renderHardware(list) {
    var box = el("hardware-tiles");
    box.replaceChildren();
    el("hardware-card").hidden = !list || list.length === 0;
    (list || []).forEach(function (h) {
      var root = make("div", "tile");
      root.appendChild(make("div", "tile-title", h.title));
      root.appendChild(make("div", "tile-value", h.value));
      root.appendChild(make("div", "tile-detail", h.detail || ""));
      root.appendChild(make("div", "severity " + (severityLabel[h.severity] ? h.severity : "Normal"),
        severityLabel[h.severity] || severityLabel.Normal));
      box.appendChild(root);
    });
  }

  function renderProcesses(list) {
    var body = el("processes");
    body.replaceChildren();
    var largest = 0;
    (list || []).forEach(function (p) { if (p.workingSetBytes > largest) { largest = p.workingSetBytes; } });
    (list || []).forEach(function (p) {
      var row = make("tr");
      var app = row.appendChild(make("td")).appendChild(make("span", "app"));
      var initial = /[\p{L}\p{N}]/u.exec(p.name);
      var letter = app.appendChild(make("span", "app-letter", initial ? initial[0].toUpperCase() : "?"));
      letter.setAttribute("data-hue", hueFor(p.name));
      letter.setAttribute("aria-hidden", "true");
      app.appendChild(make("span", null, p.instanceCount > 1 ? p.name + " (" + p.instanceCount + ")" : p.name));
      row.appendChild(make("td", "num", p.cpuPercent.toFixed(1) + "%"));
      var memory = row.appendChild(make("td", "num"));
      var bar = memory.appendChild(make("span", "mem-bar"));
      bar.setAttribute("aria-hidden", "true");
      bar.appendChild(make("span")).style.width = (largest > 0 ? 100 * p.workingSetBytes / largest : 0).toFixed(1) + "%";
      memory.appendChild(document.createTextNode(bytes(p.workingSetBytes)));
      body.appendChild(row);
    });
  }

  function renderDrives(list) {
    var box = el("drives");
    box.replaceChildren();
    (list || []).forEach(function (d) {
      var root = make("div", "drive");
      var head = root.appendChild(make("div", "drive-head"));
      head.appendChild(make("span", null, d.label ? d.name + " (" + d.label + ")" : d.name));
      head.appendChild(make("span", "secondary", bytes(d.freeBytes) + " free of " + bytes(d.totalBytes)));
      var used = d.totalBytes > 0 ? 100 * (d.totalBytes - d.freeBytes) / d.totalBytes : 0;
      // Green / amber / red by how full the drive is, like the app (DriveRowViewModel.FillingPercent).
      var meter = root.appendChild(make("div", d.isLow ? "meter low" : used >= FILLING_PERCENT ? "meter filling" : "meter"));
      var fill = meter.appendChild(make("div"));
      fill.style.width = Math.max(0, Math.min(100, used)).toFixed(1) + "%";
      if (d.isLow) { root.appendChild(make("div", "drive-low", "⚠ Almost full")); }
      box.appendChild(root);
    });
  }

  function renderSystem(info) {
    var body = el("system");
    body.replaceChildren();
    if (!info) {
      var empty = make("tr");
      empty.appendChild(make("td", "secondary", "Still loading..."));
      body.appendChild(empty);
      return;
    }
    var rows = [
      ["Computer name", info.computerName],
      ["Operating system", info.osCaption + " (build " + info.osBuild + ")"],
      ["Manufacturer / model", info.manufacturer + " " + info.model],
      ["Processor", info.cpuName],
      ["Cores / logical processors", info.physicalCores + " / " + info.logicalProcessors],
      ["Graphics", info.gpuNames && info.gpuNames.length ? info.gpuNames.join(", ") : "Unknown"],
      ["Memory", bytes(info.totalRamBytes)]
    ];
    rows.forEach(function (r) {
      var row = make("tr");
      row.appendChild(make("th", null, r[0])).setAttribute("scope", "row");
      row.appendChild(make("td", null, r[1]));
      body.appendChild(row);
    });
  }

  function render(stats) {
    var info = stats.systemInfo;
    if (info) {
      el("computer-name").textContent = info.computerName;
      document.title = info.computerName + " - Porchlight";
      var subtitle = info.osCaption;
      if (info.lastBootTimeUtc) { subtitle += " · up " + duration(Date.now() - Date.parse(info.lastBootTimeUtc)); }
      el("subtitle").textContent = subtitle;
    }
    el("restart").hidden = !stats.isRestartPending;
    renderPerformance(stats.performance, info);
    renderHardware(stats.hardware);
    renderProcesses(stats.topProcesses);
    renderDrives(stats.drives);
    renderSystem(info);
  }

  function setConnection(text, bad) {
    var node = el("connection");
    node.textContent = text;
    node.className = bad ? "connection bad" : "connection";
  }

  function showKeyPanel(wrongKey) {
    stop();
    el("content").hidden = true;
    el("key-panel").hidden = false;
    el("key-error").hidden = !wrongKey;
    el("subtitle").textContent = "Access key needed";
    setConnection("", false);
    el("key-input").focus();
  }

  function stop() {
    if (timer !== null) { clearTimeout(timer); timer = null; }
  }

  function schedule() {
    stop();
    if (document.hidden) { return; }
    timer = setTimeout(poll, POLL_MS);
  }

  function poll() {
    var key = readKey();
    if (!key) { showKeyPanel(false); return; }

    fetch("api/stats", { headers: { "Authorization": "Bearer " + key }, cache: "no-store", credentials: "omit" })
      .then(function (response) {
        if (response.status === 401) { showKeyPanel(true); return null; }
        if (!response.ok) { throw new Error("HTTP " + response.status); }
        return response.json();
      })
      .then(function (stats) {
        if (!stats) { return; }
        failures = 0;
        el("key-panel").hidden = true;
        el("content").hidden = false;
        render(stats);
        setConnection("Live · updated " + new Date().toLocaleTimeString(), false);
        schedule();
      })
      .catch(function () {
        failures++;
        setConnection(failures > 1
          ? "Can't reach the PC. It may be asleep, off, or the web console was turned off. Retrying..."
          : "Reconnecting...", true);
        schedule();
      });
  }

  function connectWithTypedKey() {
    var key = el("key-input").value.trim();
    if (!key) { return; }
    el("key-input").value = "";
    // Kept in the address's #fragment, which browsers never send to a server, so the page can be
    // bookmarked without the key appearing in any request line or log.
    location.hash = "key=" + encodeURIComponent(key);
  }

  el("key-button").addEventListener("click", connectWithTypedKey);
  el("key-input").addEventListener("keydown", function (e) { if (e.key === "Enter") { connectWithTypedKey(); } });
  window.addEventListener("hashchange", function () { failures = 0; poll(); });
  document.addEventListener("visibilitychange", function () {
    // No point sampling the PC for a tab nobody is looking at.
    if (document.hidden) { stop(); } else { poll(); }
  });

  poll();
})();
