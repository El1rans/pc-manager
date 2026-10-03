// Porchlight web console. Read-only: this script only ever GETs /api/stats, /api/updates,
// /api/startup and /api/security and draws them.
// Every value from the server is inserted with textContent, never as HTML.
(function () {
  "use strict";

  var POLL_MS = 2000;
  var DETAILS_POLL_MS = 30000;
  var HISTORY = 60;

  var el = function (id) { return document.getElementById(id); };
  var history = {};
  var tiles = {};
  var timer = null;
  var failures = 0;
  var detailsTimer = null;

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

  function tile(id, title, max) {
    if (tiles[id]) { return tiles[id]; }
    var root = make("div", "tile");
    var t = { root: root, max: max };
    root.appendChild(make("div", "tile-title", title));
    t.value = root.appendChild(make("div", "tile-value", "n/a"));
    t.detail = root.appendChild(make("div", "tile-detail", ""));
    var svg = document.createElementNS("http://www.w3.org/2000/svg", "svg");
    svg.setAttribute("class", "spark");
    svg.setAttribute("viewBox", "0 0 " + (HISTORY - 1) + " 100");
    svg.setAttribute("preserveAspectRatio", "none");
    svg.setAttribute("aria-hidden", "true");
    var base = document.createElementNS("http://www.w3.org/2000/svg", "line");
    base.setAttribute("x1", "0"); base.setAttribute("x2", String(HISTORY - 1));
    base.setAttribute("y1", "100"); base.setAttribute("y2", "100");
    svg.appendChild(base);
    t.line = document.createElementNS("http://www.w3.org/2000/svg", "polyline");
    svg.appendChild(t.line);
    root.appendChild(svg);
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
    var offset = HISTORY - series.length, points = [];
    for (var j = 0; j < series.length; j++) {
      if (series[j] === null) { continue; }
      var y = 100 - Math.min(series[j] / top, 1) * 100;
      points.push((offset + j) + "," + y.toFixed(1));
    }
    t.line.setAttribute("points", points.join(" "));
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
    (list || []).forEach(function (p) {
      var row = make("tr");
      row.appendChild(make("td", null, p.instanceCount > 1 ? p.name + " (" + p.instanceCount + ")" : p.name));
      row.appendChild(make("td", "num", p.cpuPercent.toFixed(1) + "%"));
      row.appendChild(make("td", "num", bytes(p.workingSetBytes)));
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
      var meter = root.appendChild(make("div", d.isLow ? "meter low" : "meter"));
      var fill = meter.appendChild(make("div"));
      var used = d.totalBytes > 0 ? 100 * (d.totalBytes - d.freeBytes) / d.totalBytes : 0;
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

  var levelLabel = { Good: "\u2713 OK", Attention: "\u26A0 Look at this", Unknown: "? Couldn't check" };
  var impactLabel = {
    High: "\u26A0 High", Medium: "Medium", Low: "\u2713 Low", NotMeasured: "Not measured"
  };

  function renderUpdates(u) {
    var summary = el("updates-summary"), table = el("updates-table"), body = el("updates");
    body.replaceChildren();
    if (!u.hasChecked) {
      summary.textContent = "Porchlight has not checked for app updates yet. Open Updates in Porchlight on the PC.";
      table.hidden = true;
      return;
    }
    var when = u.checkedAt ? " (last checked " + new Date(u.checkedAt).toLocaleString() + ")" : "";
    summary.textContent = (u.count === 0 ? "All apps were up to date" :
      u.count === 1 ? "1 app has an update" : u.count + " apps have updates") + when + ".";
    table.hidden = !u.items || u.items.length === 0;
    (u.items || []).forEach(function (item) {
      var row = make("tr");
      row.appendChild(make("td", null, item.name));
      row.appendChild(make("td", null, item.installedVersion));
      row.appendChild(make("td", null, item.availableVersion));
      body.appendChild(row);
    });
  }

  function renderStartup(s) {
    var summary = el("startup-summary"), table = el("startup-table"), body = el("startup");
    body.replaceChildren();
    var items = s.items || [];
    var on = items.filter(function (i) { return i.isEnabled; }).length;
    var text = on + " of " + items.length + " programs start when someone signs in.";
    if (s.impactNeedsAdmin) { text += " How much each slows start-up can only be read on the PC, as administrator."; }
    summary.textContent = text;
    table.hidden = items.length === 0;
    items.forEach(function (item) {
      var row = make("tr");
      row.appendChild(make("td", null, item.publisher ? item.name + " (" + item.publisher + ")" : item.name));
      row.appendChild(make("td", null, item.isEnabled ? "\u2713 On" : "Off"));
      row.appendChild(make("td", impactLabel[item.impact] ? "impact-" + item.impact : null,
        impactLabel[item.impact] || impactLabel.NotMeasured));
      body.appendChild(row);
    });
  }

  function securityItem(title, card) {
    var root = make("div", "security-item");
    root.appendChild(make("div", "security-title", title));
    root.appendChild(make("div", "security-verdict", card.verdict));
    root.appendChild(make("div", "level " + (levelLabel[card.level] ? card.level : "Unknown"),
      levelLabel[card.level] || levelLabel.Unknown));
    if (card.lines && card.lines.length) {
      var list = root.appendChild(make("ul", "security-lines"));
      card.lines.forEach(function (line) { list.appendChild(make("li", null, line)); });
    }
    return root;
  }

  function renderSecurity(s) {
    el("security-headline").textContent = s.headline;
    var box = el("security-cards");
    box.replaceChildren();
    box.appendChild(securityItem("Antivirus and firewall", s.protection));
    box.appendChild(securityItem("Windows Update", s.windowsUpdate));
    box.appendChild(securityItem("Who can connect to this PC", s.remoteAccess));
  }

  var detailViews = [
    { path: "api/updates", render: renderUpdates },
    { path: "api/startup", render: renderStartup },
    { path: "api/security", render: renderSecurity }
  ];

  function pollDetails() {
    var key = readKey();
    if (!key) { return; }
    // A failure of one view never blocks the others; the live stats poll shows the wrong-key and
    // unreachable messages.
    detailViews.forEach(function (view) {
      fetch(view.path, { headers: { "Authorization": "Bearer " + key }, cache: "no-store", credentials: "omit" })
        .then(function (response) { return response.ok ? response.json() : null; })
        .then(function (data) { if (data) { view.render(data); } })
        .catch(function () { /* keep showing the last values */ });
    });
  }

  function stopDetails() {
    if (detailsTimer !== null) { clearInterval(detailsTimer); detailsTimer = null; }
  }

  function startDetails() {
    if (detailsTimer !== null || document.hidden) { return; }
    pollDetails();
    detailsTimer = setInterval(pollDetails, DETAILS_POLL_MS);
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
    stopDetails();
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
        startDetails();
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
  window.addEventListener("hashchange", function () { failures = 0; stopDetails(); poll(); });
  document.querySelectorAll(".nav-button").forEach(function (button) {
    // Scrolls instead of linking to "#section": the address's #fragment holds the access key.
    button.addEventListener("click", function () {
      var target = el(button.getAttribute("data-target"));
      if (target) { target.scrollIntoView({ behavior: "smooth", block: "start" }); }
    });
  });
  document.addEventListener("visibilitychange", function () {
    // No point sampling the PC for a tab nobody is looking at.
    if (document.hidden) { stop(); stopDetails(); } else { poll(); }
  });

  poll();
})();
