// chatmain.js?v=20251111-FINAL-PERU-OPTIMIZED
let currentConvId = null;
let isTyping = false;

// === INICIALIZACIÓN ===
document.addEventListener("DOMContentLoaded", () => {
    initTheme();
    loadConversations();
    setupEventListeners();
    loadInitialConversation();
});

// === MODO OSCURO ===
function initTheme() {
    const savedTheme = localStorage.getItem("lawdeia-theme") || "light";
    document.documentElement.setAttribute("data-theme", savedTheme);
    updateThemeIcon(savedTheme);
}

function toggleTheme() {
    const current = document.documentElement.getAttribute("data-theme");
    const newTheme = current === "dark" ? "light" : "dark";
    document.documentElement.setAttribute("data-theme", newTheme);
    localStorage.setItem("lawdeia-theme", newTheme);
    updateThemeIcon(newTheme);
}

function updateThemeIcon(theme) {
    const icon = document.querySelector("#theme-toggle i");
    if (icon) {
        icon.className = theme === "dark" ? "fas fa-sun" : "fas fa-moon";
    }
}

// === EVENTOS ===
function setupEventListeners() {
    const input = document.getElementById("message-input");
    input?.addEventListener("keypress", e => {
        if (e.key === "Enter" && !e.shiftKey) {
            e.preventDefault();
            sendMessage();
        }
    });

    document.getElementById("send-btn")?.addEventListener("click", sendMessage);
    document.getElementById("new-chat-btn")?.addEventListener("click", goToHome);
    document.getElementById("theme-toggle")?.addEventListener("click", toggleTheme);
}

// === IR AL INICIO ===
function goToHome() {
    window.location.href = "/Chat";
}

// === CARGAR CONVERSACIÓN INICIAL ===
function loadInitialConversation() {
    const params = new URLSearchParams(location.search);
    const convId = params.get("conversationId");
    if (convId && !isNaN(convId)) {
        loadConversation(parseInt(convId));
    } else {
        showEmptyChat();
    }
}

// === MOSTRAR CHAT VACÍO ===
function showEmptyChat() {
    currentConvId = null;
    const messagesDiv = document.getElementById("messages");
    const chatTitle = document.getElementById("chat-title");
    messagesDiv.innerHTML = `
        <div class="empty-chat">
            <h3>¡Hola! ¿En qué te ayudo hoy?</h3>
            <p>Escribe tu consulta y comenzaremos.</p>
        </div>`;
    chatTitle.textContent = "Nueva conversación";
    history.replaceState(null, "", "/Chat");
    document.querySelectorAll(".conv-item").forEach(el => el.classList.remove("active"));
}

// === CARGAR LISTA DE CONVERSACIONES ===
async function loadConversations() {
    try {
        const res = await fetch("/Chat/GetConversations", { credentials: "include" });
        const data = res.ok ? await res.json() : [];
        const sidebar = document.getElementById("conversations-sidebar");
        if (!sidebar) return;

        if (data.length === 0) {
            sidebar.innerHTML = '<div class="no-conversations">No hay conversaciones aún</div>';
            if (currentConvId) showEmptyChat();
            return;
        }

        sidebar.innerHTML = data.map(c => `
            <div class="conv-item ${c.id === currentConvId ? 'active' : ''}"
                 data-conv-id="${c.id}"
                 onclick="loadConversation(${c.id})">
                <div class="conv-title">${escapeHtml(c.title || 'Sin título')}</div>
                <small>${c.timestamp}</small>
                <button class="delete-conv" onclick="event.stopPropagation(); deleteConversation(${c.id})">×</button>
            </div>
        `).join('');

    } catch (e) {
        console.error("Error loadConversations:", e);
    }
}

// === CARGAR CONVERSACIÓN ===
async function loadConversation(convId) {
    if (!convId) return showEmptyChat();
    currentConvId = convId;

    try {
        const response = await fetch(`/Chat/LoadConversation?conversationId=${convId}`, {
            credentials: "include",
            headers: { "X-Requested-With": "XMLHttpRequest" }
        });

        const text = await response.text();
        if (!text.trim() || text.includes("<html") || text.includes("<!DOCTYPE")) {
            alert("Sesión expirada. Recargando...");
            location.reload();
            return;
        }

        const data = JSON.parse(text);
        if (!data.success) {
            showEmptyChat();
            return;
        }

        const messagesDiv = document.getElementById("messages");
        messagesDiv.innerHTML = "";

        if (data.messages?.length > 0) {
            data.messages.forEach(m => appendMessage({
                content: m.content,
                senderType: m.senderType,
                timestamp: m.timestamp
            }));
        } else {
            messagesDiv.innerHTML = "<p style='text-align:center;color:#999;margin-top:50px;'>¡Empecemos! ¿En qué te ayudo?</p>";
        }

        document.getElementById("chat-title").textContent = data.title || "Nueva conversación";
        history.replaceState(null, "", "?conversationId=" + convId);

        document.querySelectorAll(".conv-item").forEach(el => el.classList.remove("active"));
        document.querySelector(`[data-conv-id="${convId}"]`)?.classList.add("active");

        messagesDiv.scrollTop = messagesDiv.scrollHeight;

    } catch (err) {
        console.error("Error loadConversation:", err);
        showEmptyChat();
    }
}

// === CREAR NUEVA CONVERSACIÓN (solo al enviar mensaje) ===
async function createNewConversation() {
    try {
        const res = await fetch("/Chat/NewConversation", {
            method: "POST",
            credentials: "include",
            headers: { "X-Requested-With": "XMLHttpRequest" }
        });
        const data = await res.json();
        if (data?.success) {
            currentConvId = data.conversationId;
            history.pushState(null, "", `/Chat?conversationId=${currentConvId}`);
            document.getElementById("chat-title").textContent = "Nueva conversación";
            document.getElementById("messages").innerHTML = "<p style='text-align:center;color:#999;margin-top:50px;'>¡Hola! ¿En qué puedo ayudarte?</p>";
            await loadConversations();
        }
    } catch (e) {
        console.error("Error creando conversación:", e);
        showEmptyChat();
    }
}

// === ENVIAR MENSAJE ===
async function sendMessage() {
    if (isTyping) return;

    const input = document.getElementById("message-input");
    const message = input.value.trim();
    if (!message) return;

    if (!currentConvId) {
        await createNewConversation();
        setTimeout(() => sendMessage(), 200); // Reintenta tras crear
        return;
    }

    appendMessage({
        content: message,
        senderType: "User",
        timestamp: new Date().toLocaleTimeString("es-PE", { hour: '2-digit', minute: '2-digit' })
    });

    showTyping(true);
    input.value = "";

    try {
        const res = await fetch("/Chat/SendMessage", {
            method: "POST",
            credentials: "include",
            headers: {
                "Content-Type": "application/json",
                "X-Requested-With": "XMLHttpRequest"
            },
            body: JSON.stringify({ message, conversationId: currentConvId })
        });

        const data = await res.json();
        showTyping(false);

        if (data?.success) {
            appendMessage(data.aiMessage);
            await loadConversations(); // ← Actualiza título y sidebar desde el backend
        } else {
            appendMessage({ content: "Error: " + (data.error || "Desconocido"), senderType: "AI" });
        }
    } catch (err) {
        showTyping(false);
        appendMessage({ content: "Error de conexión. Revisa tu internet.", senderType: "AI" });
        console.error(err);
    }
}

// === ELIMINAR CONVERSACIÓN ===
async function deleteConversation(id) {
    if (!confirm("¿Eliminar esta conversación?")) return;

    try {
        const res = await fetch("/Chat/DeleteConversation", {
            method: "POST",
            credentials: "include",
            headers: {
                "Content-Type": "application/json",
                "X-Requested-With": "XMLHttpRequest"
            },
            body: JSON.stringify({ conversationId: id })
        });

        const data = await res.json();
        if (data.success) {
            await loadConversations();
            if (currentConvId === id) {
                showEmptyChat();
            }
        } else {
            alert("Error: " + (data.error || "No se pudo eliminar"));
        }
    } catch (e) {
        console.error("Error al eliminar:", e);
        alert("Error de conexión");
    }
}

// === MOSTRAR INDICADOR DE ESCRIBIENDO ===
function showTyping(yes) {
    const typingEl = document.getElementById("typing-indicator");
    const sendBtn = document.getElementById("send-btn");
    const input = document.getElementById("message-input");

    if (yes) {
        isTyping = true;
        input.disabled = true;
        sendBtn.disabled = true;
        sendBtn.style.opacity = "0.5";
        sendBtn.style.cursor = "not-allowed";

        if (typingEl) typingEl.remove();

        const el = document.createElement("div");
        el.id = "typing-indicator";
        el.innerHTML = `
            <div class="message ai">
                <div class="bubble typing">
                    <span></span><span></span><span></span>
                </div>
            </div>`;
        document.getElementById("messages").appendChild(el);
        document.getElementById("messages").scrollTop = document.getElementById("messages").scrollHeight;
    } else {
        isTyping = false;
        input.disabled = false;
        sendBtn.disabled = false;
        sendBtn.style.opacity = "1";
        sendBtn.style.cursor = "pointer";
        if (typingEl) typingEl.remove();
    }
}

// === AGREGAR MENSAJE AL CHAT ===
function appendMessage(m) {
    const div = document.getElementById("messages");
    const html = m.senderType === "AI" ? markdownToHtml(m.content) : escapeHtml(m.content);
    div.innerHTML += `
        <div class="message ${m.senderType === "User" ? "user" : "ai"}">
            <div class="bubble">${html}</div>
        </div>`;
    div.scrollTop = div.scrollHeight;
}

// === MARKDOWN SIMPLE A HTML ===
function markdownToHtml(t) {
    return t?.replace(/\*\*(.*?)\*\*/g, '<strong>$1</strong>')
        .replace(/\*(.*?)\*/g, '<em>$1</em>')
        .replace(/`(.*?)`/g, '<code class="inline-code">$1</code>')
        .replace(/\n/g, '<br>') || '';
}

// === ESCAPAR HTML ===
function escapeHtml(t) {
    const div = document.createElement('div');
    div.textContent = t;
    return div.innerHTML;
}