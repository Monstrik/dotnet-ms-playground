const queryParameters = new URLSearchParams(window.location.search);
const isLocalDevMonitor = window.location.port === "5050" || window.location.port === "5158";
const defaultEchoPort = isLocalDevMonitor ? "5037" : "8082";
const defaultWeatherPort = isLocalDevMonitor ? "5047" : "8084";
const monitorBase = window.location.origin;
const apiBase = queryParameters.get("api") || `${window.location.protocol}//${window.location.hostname}:${defaultEchoPort}`;
const weatherApiBase = queryParameters.get("weatherApi") || `${window.location.protocol}//${window.location.hostname}:${defaultWeatherPort}`;

document.getElementById("monitorBase").textContent = monitorBase;
document.getElementById("apiBase").textContent = apiBase;
document.getElementById("weatherApiBase").textContent = weatherApiBase;
document.getElementById("echoHealthUrl").textContent = `${apiBase}/health`;
document.getElementById("weatherHealthUrl").textContent = `${weatherApiBase}/health`;

async function callApi(baseUrl, path, options) {
  const response = await fetch(`${baseUrl}${path}`, options);
  const text = await response.text();

  try {
    return JSON.parse(text);
  } catch {
    return { raw: text, status: response.status };
  }
}

function show(outputId, data) {
  document.getElementById(outputId).textContent = JSON.stringify(data, null, 2);
}

function setHealthCard(serviceName, status, details) {
  const statusElement = document.getElementById(`${serviceName}Status`);
  const detailsElement = document.getElementById(`${serviceName}Details`);

  statusElement.textContent = status.label;
  statusElement.className = `status-badge ${status.className}`;
  detailsElement.textContent = details;
}

async function probeHealth(serviceName, baseUrl) {
  const startedAt = performance.now();

  try {
    const data = await callApi(baseUrl, "/health");
    const duration = Math.round(performance.now() - startedAt);
    const label = data?.status === "healthy" ? "Healthy" : "Unhealthy";

    setHealthCard(serviceName, {
      label,
      className: data?.status === "healthy" ? "status-healthy" : "status-unhealthy"
    }, `status=${data?.status ?? "unknown"} • ${duration}ms`);

    return {
      service: serviceName,
      url: `${baseUrl}/health`,
      ok: data?.status === "healthy",
      durationMs: duration,
      payload: data
    };
  } catch (error) {
    setHealthCard(serviceName, {
      label: "Offline",
      className: "status-unhealthy"
    }, String(error));

    return {
      service: serviceName,
      url: `${baseUrl}/health`,
      ok: false,
      error: String(error)
    };
  }
}

async function refreshHealthDashboard() {
  setHealthCard("monitor", { label: "Checking…", className: "status-loading" }, "Waiting for response…");
  setHealthCard("echo", { label: "Checking…", className: "status-loading" }, "Waiting for response…");
  setHealthCard("weather", { label: "Checking…", className: "status-loading" }, "Waiting for response…");

  const results = await Promise.all([
    probeHealth("monitor", monitorBase),
    probeHealth("echo", apiBase),
    probeHealth("weather", weatherApiBase)
  ]);

  show("healthOutput", {
    checkedAt: new Date().toISOString(),
    services: results
  });
}

document.getElementById("refreshHealthBtn").addEventListener("click", refreshHealthDashboard);

document.getElementById("echoGetBtn").addEventListener("click", async () => {
  try {
    const message = document.getElementById("getMessage").value;
    const data = await callApi(apiBase, `/echo/${encodeURIComponent(message)}`);
    show("echoGetOutput", data);
  } catch (error) {
    show("echoGetOutput", { error: String(error) });
  }
});

document.getElementById("echoPostBtn").addEventListener("click", async () => {
  try {
    const message = document.getElementById("postMessage").value;
    const data = await callApi(apiBase, "/echo", {
      method: "POST",
      headers: {
        "Content-Type": "application/json"
      },
      body: JSON.stringify({ message })
    });
    show("echoPostOutput", data);
  } catch (error) {
    show("echoPostOutput", { error: String(error) });
  }
});

document.getElementById("weatherBtn").addEventListener("click", async () => {
  try {
    const city = document.getElementById("weatherCity").value;
    const data = await callApi(weatherApiBase, `/weather/${encodeURIComponent(city)}`);
    show("weatherOutput", data);
  } catch (error) {
    show("weatherOutput", { error: String(error) });
  }
});

refreshHealthDashboard();

