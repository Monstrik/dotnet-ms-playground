const queryParameters = new URLSearchParams(window.location.search);
const isLocalDevMonitor = window.location.port === "5050" || window.location.port === "5158";
const defaultOrderPort = isLocalDevMonitor ? "5077" : "8091";
const defaultKitchenPort = isLocalDevMonitor ? "5087" : "8092";
const defaultDeliveryPort = isLocalDevMonitor ? "5097" : "8093";

const orderApiBase = queryParameters.get("orderApi") || `${window.location.protocol}//${window.location.hostname}:${defaultOrderPort}`;
const kitchenApiBase = queryParameters.get("kitchenApi") || `${window.location.protocol}//${window.location.hostname}:${defaultKitchenPort}`;
const deliveryApiBase = queryParameters.get("deliveryApi") || `${window.location.protocol}//${window.location.hostname}:${defaultDeliveryPort}`;

document.getElementById("orderApiBase").textContent = orderApiBase;
document.getElementById("kitchenApiBase").textContent = kitchenApiBase;
document.getElementById("deliveryApiBase").textContent = deliveryApiBase;
document.getElementById("queueMetricsUrl").textContent = `${orderApiBase}/queues`;

let hasHydratedConfigInputs = false;

async function callApi(baseUrl, path, options) {
  const response = await fetch(`${baseUrl}${path}`, options);
  const text = await response.text();

  let payload;
  try {
    payload = text ? JSON.parse(text) : null;
  } catch {
    payload = { raw: text };
  }

  if (!response.ok) {
    const errorMessage = payload?.error || `${response.status} ${response.statusText}`;
    throw new Error(errorMessage);
  }

  return payload;
}

function setServiceBadge(elementId, running) {
  const element = document.getElementById(elementId);
  if (running === true) {
    element.textContent = "Running";
    element.className = "status-badge status-running";
    return;
  }

  if (running === false) {
    element.textContent = "Stopped";
    element.className = "status-badge status-stopped";
    return;
  }

  element.textContent = "Unknown";
  element.className = "status-badge status-loading";
}

function setProcessingMetrics(producerStats, kitchenStats, deliveryStats) {
  document.getElementById("producerProcessing").textContent = String(producerStats?.processingCount ?? 0);
  document.getElementById("kitchenProcessing").textContent = String(kitchenStats?.processingCount ?? 0);
  document.getElementById("deliveryProcessing").textContent = String(deliveryStats?.processingCount ?? 0);
}

function setQueueMetrics(queueStats) {
  document.getElementById("queueNewMessages").textContent = String(queueStats?.ordersNew?.messages ?? "-");
  document.getElementById("queueNewConsumers").textContent = String(queueStats?.ordersNew?.consumers ?? "-");
  document.getElementById("queueReadyMessages").textContent = String(queueStats?.ordersReadyForDelivery?.messages ?? "-");
  document.getElementById("queueReadyConsumers").textContent = String(queueStats?.ordersReadyForDelivery?.consumers ?? "-");
  document.getElementById("queueDeliveredMessages").textContent = String(queueStats?.ordersDelivered?.messages ?? "-");
  document.getElementById("queueDeliveredConsumers").textContent = String(queueStats?.ordersDelivered?.consumers ?? "-");
}

function hydrateConfigInputs(producerConfig, kitchenConfig, deliveryConfig) {
  if (hasHydratedConfigInputs) {
    return;
  }

  if (producerConfig?.ordersPerMinute) {
    document.getElementById("ordersPerMinute").value = String(producerConfig.ordersPerMinute);
  }

  if (kitchenConfig?.minPreparationSeconds) {
    document.getElementById("kitchenMin").value = String(kitchenConfig.minPreparationSeconds);
  }

  if (kitchenConfig?.maxPreparationSeconds) {
    document.getElementById("kitchenMax").value = String(kitchenConfig.maxPreparationSeconds);
  }

  if (deliveryConfig?.minDeliverySeconds) {
    document.getElementById("deliveryMin").value = String(deliveryConfig.minDeliverySeconds);
  }

  if (deliveryConfig?.maxDeliverySeconds) {
    document.getElementById("deliveryMax").value = String(deliveryConfig.maxDeliverySeconds);
  }

  hasHydratedConfigInputs = true;
}

function normalizeDate(value) {
  if (!value) {
    return null;
  }

  const parsed = new Date(value);
  return Number.isNaN(parsed.getTime()) ? null : parsed;
}

function formatUtc(value) {
  const date = normalizeDate(value);
  if (!date) {
    return "";
  }

  return date.toLocaleTimeString(undefined, {
    hour: "2-digit",
    minute: "2-digit",
    second: "2-digit",
    hour12: false
  });
}

function formatElapsedFromCreated(createdAt, stateAt) {
  const createdDate = normalizeDate(createdAt);
  const stateDate = normalizeDate(stateAt);
  if (!createdDate || !stateDate) {
    return "";
  }

  const totalSeconds = Math.max(0, Math.floor((stateDate.getTime() - createdDate.getTime()) / 1000));
  const hours = Math.floor(totalSeconds / 3600);
  const minutes = Math.floor((totalSeconds % 3600) / 60);
  const seconds = totalSeconds % 60;
  return `${String(hours).padStart(2, "0")}:${String(minutes).padStart(2, "0")}:${String(seconds).padStart(2, "0")}`;
}

function buildOrderSnapshot(producerStats, kitchenStats, deliveryStats) {
  const ordersById = new Map();

  function ensureOrder(orderId) {
    if (!ordersById.has(orderId)) {
      ordersById.set(orderId, {
        orderId,
        status: "new",
        createdAt: null,
        inProcessAt: null,
        lastStateAt: null,
        items: []
      });
    }

    return ordersById.get(orderId);
  }

  function updateOrder(orderId, status, stateTimestamp, items) {
    const order = ensureOrder(orderId);

    if (Array.isArray(items) && items.length > 0) {
      order.items = items;
    }

    if (status === "new" && !order.createdAt) {
      order.createdAt = stateTimestamp || order.createdAt;
    }

    if (status !== "new" && !order.inProcessAt) {
      order.inProcessAt = stateTimestamp || order.inProcessAt;
    }

    const currentStateTime = normalizeDate(order.lastStateAt)?.getTime() || 0;
    const incomingStateTime = normalizeDate(stateTimestamp)?.getTime() || 0;
    if (incomingStateTime >= currentStateTime) {
      order.status = status;
      order.lastStateAt = stateTimestamp || order.lastStateAt;
    }
  }

  for (const item of producerStats?.orders || []) {
    updateOrder(item.orderId, "new", item.timestamp, item.items || []);
  }

  for (const item of kitchenStats?.beingPrepared || []) {
    updateOrder(item.orderId, "being prepared", item.timestamp, item.items || []);
  }

  for (const item of kitchenStats?.readyForDelivery || []) {
    updateOrder(item.orderId, "ready for delivery", item.timestamp, item.items || []);
  }

  for (const item of deliveryStats?.beingDelivered || []) {
    updateOrder(item.orderId, "being delivered", item.timestamp);
  }

  for (const item of deliveryStats?.delivered || []) {
    updateOrder(item.orderId, "delivered", item.timestamp);
  }

  return Array.from(ordersById.values())
    .sort((a, b) => (normalizeDate(b.lastStateAt)?.getTime() || 0) - (normalizeDate(a.lastStateAt)?.getTime() || 0))
    .slice(0, 300);
}

function renderStateTable(bodyId, countId, rows) {
  const body = document.getElementById(bodyId);
  body.innerHTML = "";

  const badge = document.getElementById(countId);
  if (badge) {
    badge.textContent = String(rows.length);
  }

  if (rows.length === 0) {
    const emptyRow = document.createElement("tr");
    emptyRow.innerHTML = '<td colspan="4">No orders</td>';
    body.appendChild(emptyRow);
    return;
  }

  for (const order of rows) {
    const row = document.createElement("tr");

    const idCell = document.createElement("td");
    const code = document.createElement("code");
    code.textContent = order.orderId;
    idCell.appendChild(code);

    const createdCell = document.createElement("td");
    createdCell.textContent = formatUtc(order.createdAt);

    const inProcessCell = document.createElement("td");
    inProcessCell.textContent = formatElapsedFromCreated(order.createdAt, order.lastStateAt);

    const itemsCell = document.createElement("td");
    itemsCell.textContent = (order.items || []).join(", ");

    row.appendChild(idCell);
    row.appendChild(createdCell);
    row.appendChild(inProcessCell);
    row.appendChild(itemsCell);

    body.appendChild(row);
  }
}

function renderOrderStateTables(producerStats, kitchenStats, deliveryStats) {
  const orders = buildOrderSnapshot(producerStats, kitchenStats, deliveryStats);

  renderStateTable("ordersNewTableBody", "countNew", orders.filter((item) => item.status === "new"));
  renderStateTable("ordersPreparingTableBody", "countPreparing", orders.filter((item) => item.status === "being prepared"));
  renderStateTable("ordersReadyTableBody", "countReady", orders.filter((item) => item.status === "ready for delivery"));
  renderStateTable("ordersDeliveringTableBody", "countDelivering", orders.filter((item) => item.status === "being delivered"));
  renderStateTable("ordersDeliveredTableBody", "countDelivered", orders.filter((item) => item.status === "delivered"));
}

function setSummaries(producerStats, kitchenStats, deliveryStats) {
  document.getElementById("producerStatsSummary").textContent =
    `Produced: ${producerStats?.producedCount ?? 0}`;

  document.getElementById("kitchenStatsSummary").textContent =
    `Prepared: ${kitchenStats?.preparedCount ?? 0} | In progress: ${kitchenStats?.processingCount ?? 0}`;

  document.getElementById("deliveryStatsSummary").textContent =
    `Delivered: ${deliveryStats?.deliveredCount ?? 0} | In progress: ${deliveryStats?.processingCount ?? 0}`;
}

function setDiagnostics(payload) {
  document.getElementById("workflowRawOutput").textContent = JSON.stringify(payload, null, 2);
}

async function refreshWorkflowDashboard() {
  try {
    const [producerStats, producerConfig, queueStats, kitchenStats, kitchenConfig, deliveryStats, deliveryConfig] = await Promise.all([
      callApi(orderApiBase, "/stats"),
      callApi(orderApiBase, "/config"),
      callApi(orderApiBase, "/queues"),
      callApi(kitchenApiBase, "/stats"),
      callApi(kitchenApiBase, "/config"),
      callApi(deliveryApiBase, "/stats"),
      callApi(deliveryApiBase, "/config")
    ]);

    setServiceBadge("orderProducerStatus", producerStats.running);
    setServiceBadge("kitchenStatus", kitchenStats.running);
    setServiceBadge("deliveryStatus", deliveryStats.running);
    setProcessingMetrics(producerStats, kitchenStats, deliveryStats);
    setQueueMetrics(queueStats);
    setSummaries(producerStats, kitchenStats, deliveryStats);
    hydrateConfigInputs(producerConfig, kitchenConfig, deliveryConfig);
    renderOrderStateTables(producerStats, kitchenStats, deliveryStats);

    setDiagnostics({
      refreshedAtUtc: new Date().toISOString(),
      producerStats,
      producerConfig,
      queueStats,
      kitchenStats,
      kitchenConfig,
      deliveryStats,
      deliveryConfig
    });
  } catch (error) {
    setDiagnostics({ refreshedAtUtc: new Date().toISOString(), error: String(error) });
  }
}

async function postControl(baseUrl, path) {
  await callApi(baseUrl, path, { method: "POST" });
  await refreshWorkflowDashboard();
}

document.getElementById("producerStartBtn").addEventListener("click", () => postControl(orderApiBase, "/control/start"));
document.getElementById("producerStopBtn").addEventListener("click", () => postControl(orderApiBase, "/control/stop"));
document.getElementById("kitchenStartBtn").addEventListener("click", () => postControl(kitchenApiBase, "/control/start"));
document.getElementById("kitchenStopBtn").addEventListener("click", () => postControl(kitchenApiBase, "/control/stop"));
document.getElementById("deliveryStartBtn").addEventListener("click", () => postControl(deliveryApiBase, "/control/start"));
document.getElementById("deliveryStopBtn").addEventListener("click", () => postControl(deliveryApiBase, "/control/stop"));

document.getElementById("producerApplyConfigBtn").addEventListener("click", async () => {
  const ordersPerMinute = Number(document.getElementById("ordersPerMinute").value);
  await callApi(orderApiBase, "/config", {
    method: "PUT",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ ordersPerMinute })
  });
  await refreshWorkflowDashboard();
});

document.getElementById("kitchenApplyConfigBtn").addEventListener("click", async () => {
  const minPreparationSeconds = Number(document.getElementById("kitchenMin").value);
  const maxPreparationSeconds = Number(document.getElementById("kitchenMax").value);
  await callApi(kitchenApiBase, "/config", {
    method: "PUT",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ minPreparationSeconds, maxPreparationSeconds })
  });
  await refreshWorkflowDashboard();
});

document.getElementById("deliveryApplyConfigBtn").addEventListener("click", async () => {
  const minDeliverySeconds = Number(document.getElementById("deliveryMin").value);
  const maxDeliverySeconds = Number(document.getElementById("deliveryMax").value);
  await callApi(deliveryApiBase, "/config", {
    method: "PUT",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ minDeliverySeconds, maxDeliverySeconds })
  });
  await refreshWorkflowDashboard();
});

document.getElementById("resetQueuesBtn").addEventListener("click", async () => {
  await callApi(orderApiBase, "/admin/reset", { method: "POST" });
  await callApi(kitchenApiBase, "/admin/reset-state", { method: "POST" });
  await callApi(deliveryApiBase, "/admin/reset-state", { method: "POST" });
  await refreshWorkflowDashboard();
});

document.getElementById("refreshNowBtn").addEventListener("click", refreshWorkflowDashboard);

setInterval(refreshWorkflowDashboard, 2000);
refreshWorkflowDashboard();

