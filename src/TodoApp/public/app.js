const queryParameters = new URLSearchParams(window.location.search);
const isLocalDev = window.location.port === "5089" || window.location.port === "5159" || window.location.port === "5160";
const defaultTodoPort = isLocalDev ? "5067" : "8088";
const todoApiBase = queryParameters.get("todoApi") || `${window.location.protocol}//${window.location.hostname}:${defaultTodoPort}`;

const statusDot = document.getElementById("statusDot");
const statusText = document.getElementById("statusText");
const todoInput = document.getElementById("todoInput");
const addBtn = document.getElementById("addBtn");
const todosContainer = document.getElementById("todosContainer");
const errorMessage = document.getElementById("errorMessage");

let isConnected = false;

async function callTodoApi(path, options = {}) {
  try {
    const response = await fetch(`${todoApiBase}${path}`, {
      ...options,
      headers: {
        "Content-Type": "application/json",
        ...options.headers
      }
    });

    const text = await response.text();
    let data;
    try {
      data = JSON.parse(text);
    } catch {
      data = { raw: text, status: response.status };
    }

    if (!response.ok) {
      throw new Error(data.error || `HTTP ${response.status}`);
    }

    return data;
  } catch (error) {
    console.error("API Error:", error);
    throw error;
  }
}

function showError(message) {
  errorMessage.textContent = message;
  errorMessage.classList.add("show");
  setTimeout(() => {
    errorMessage.classList.remove("show");
  }, 5000);
}

function setStatus(connected) {
  isConnected = connected;
  if (connected) {
    statusDot.className = "status-dot connected";
    statusText.textContent = "Connected to TodoService";
    todoInput.disabled = false;
    addBtn.disabled = false;
  } else {
    statusDot.className = "status-dot disconnected";
    statusText.textContent = "Disconnected from TodoService";
    todoInput.disabled = true;
    addBtn.disabled = true;
  }
}

function formatDate(dateString) {
  const date = new Date(dateString);
  return date.toLocaleDateString("en-US", { month: "short", day: "numeric", hour: "2-digit", minute: "2-digit" });
}

function renderTodoItem(item) {
  const li = document.createElement("li");
  li.className = `todo-item ${item.isCompleted ? "completed" : ""}`;
  li.innerHTML = `
    <input type="checkbox" class="todo-checkbox" ${item.isCompleted ? "checked" : ""} data-id="${item.id}">
    <span class="todo-text">${escapeHtml(item.title)}</span>
    <span class="todo-date">${formatDate(item.createdAtUtc)}</span>
    <button class="todo-delete" data-id="${item.id}">Delete</button>
  `;

  const checkbox = li.querySelector(".todo-checkbox");
  const deleteBtn = li.querySelector(".todo-delete");

  checkbox.addEventListener("change", async () => {
    try {
      await callTodoApi(`/todos/${item.id}`, {
        method: "PATCH",
        body: JSON.stringify({ isCompleted: checkbox.checked })
      });
      li.classList.toggle("completed");
    } catch (error) {
      showError("Failed to update todo");
      checkbox.checked = !checkbox.checked;
    }
  });

  deleteBtn.addEventListener("click", async () => {
    try {
      await callTodoApi(`/todos/${item.id}`, {
        method: "DELETE"
      });
      li.remove();
      if (document.querySelectorAll(".todo-item").length === 0) {
        renderEmptyState();
      }
    } catch (error) {
      showError("Failed to delete todo");
    }
  });

  return li;
}

function renderEmptyState() {
  todosContainer.innerHTML = `
    <div class="empty-state">
      <div class="empty-state-icon">✓</div>
      <p>No todos yet. Add one to get started!</p>
    </div>
  `;
}

function renderTodos(todos) {
  if (todos.length === 0) {
    renderEmptyState();
    return;
  }

  const ul = document.createElement("ul");
  ul.className = "todos-list";
  todos.forEach(todo => {
    ul.appendChild(renderTodoItem(todo));
  });

  todosContainer.innerHTML = "";
  todosContainer.appendChild(ul);
}

async function loadTodos() {
  try {
    const todos = await callTodoApi("/todos");
    renderTodos(Array.isArray(todos) ? todos : []);
    setStatus(true);
  } catch (error) {
    console.error("Failed to load todos:", error);
    setStatus(false);
    todosContainer.innerHTML = `
      <div class="empty-state">
        <div class="empty-state-icon">⚠️</div>
        <p>Unable to connect to TodoService</p>
        <p style="font-size: 0.9rem; margin-top: 0.5rem;">Make sure the service is running at: ${todoApiBase}</p>
      </div>
    `;
  }
}

async function addTodo() {
  const title = todoInput.value.trim();
  if (!title) {
    showError("Please enter a todo title");
    return;
  }

  try {
    const newTodo = await callTodoApi("/todos", {
      method: "POST",
      body: JSON.stringify({ title })
    });

    todoInput.value = "";

    if (document.querySelector(".empty-state")) {
      renderTodos([newTodo]);
    } else {
      const ul = todosContainer.querySelector(".todos-list");
      if (ul) {
        ul.appendChild(renderTodoItem(newTodo));
      }
    }
  } catch (error) {
    showError("Failed to add todo");
  }
}

function escapeHtml(text) {
  const map = {
    "&": "&amp;",
    "<": "&lt;",
    ">": "&gt;",
    '"': "&quot;",
    "'": "&#039;"
  };
  return text.replace(/[&<>"']/g, m => map[m]);
}

addBtn.addEventListener("click", addTodo);
todoInput.addEventListener("keypress", (e) => {
  if (e.key === "Enter") {
    addTodo();
  }
});

// Initial load
loadTodos();

// Refresh todos every 30 seconds
setInterval(loadTodos, 30000);

