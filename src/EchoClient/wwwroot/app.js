const queryParameters = new URLSearchParams(window.location.search);
const isLocalDevClient = window.location.port === "5050" || window.location.port === "5158";
const defaultEchoPort = isLocalDevClient ? "5037" : "8082";
const defaultWeatherPort = isLocalDevClient ? "5047" : "8084";
const apiBase = queryParameters.get("api") || `${window.location.protocol}//${window.location.hostname}:${defaultEchoPort}`;
const weatherApiBase = queryParameters.get("weatherApi") || `${window.location.protocol}//${window.location.hostname}:${defaultWeatherPort}`;

document.getElementById("apiBase").textContent = apiBase;
document.getElementById("weatherApiBase").textContent = weatherApiBase;

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

document.getElementById("healthBtn").addEventListener("click", async () => {
  try {
    const data = await callApi(apiBase, "/health");
    show("healthOutput", data);
  } catch (error) {
    show("healthOutput", { error: String(error) });
  }
});

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

