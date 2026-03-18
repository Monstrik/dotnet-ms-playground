const apiBase = window.location.origin;

document.getElementById("apiBase").textContent = apiBase;

async function callApi(path, options) {
  const response = await fetch(`${apiBase}${path}`, options);
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
    const data = await callApi("/health");
    show("healthOutput", data);
  } catch (error) {
    show("healthOutput", { error: String(error) });
  }
});

document.getElementById("echoGetBtn").addEventListener("click", async () => {
  try {
    const message = document.getElementById("getMessage").value;
    const data = await callApi(`/echo/${encodeURIComponent(message)}`);
    show("echoGetOutput", data);
  } catch (error) {
    show("echoGetOutput", { error: String(error) });
  }
});

document.getElementById("echoPostBtn").addEventListener("click", async () => {
  try {
    const message = document.getElementById("postMessage").value;
    const data = await callApi("/echo", {
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

