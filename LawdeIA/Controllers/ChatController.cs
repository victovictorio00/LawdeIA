using LawdeIA.Data;
using LawdeIA.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

[Authorize]
public class ChatController : Controller
{
    private readonly LawdeIAContext _context;
    private readonly ILogger<ChatController> _logger;
    private readonly HttpClient _httpClient;

    private const string GEMINI_API_KEY = "AIzaSyAWFhRSrUJJRQpMGLhhqPwaIPVqry6esCg";
    private const string GEMINI_MODEL = "gemini-2.5-flash";
    private const double TEMPERATURE = 0.5;
    private const int MAX_TOKENS = 800;
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(12);

    public ChatController(LawdeIAContext context, ILogger<ChatController> logger)
    {
        _context = context;
        _logger = logger;
        _httpClient = new HttpClient { Timeout = Timeout };
    }

    public async Task<IActionResult> Index(int? conversationId = null)
    {
        try
        {
            if (!User.Identity.IsAuthenticated) return RedirectToAction("Login", "Auth");
            var userId = GetUserId();
            if (userId == 0) return RedirectToAction("Login", "Auth");

            var user = await _context.Users.FindAsync(userId);
            if (user == null) return RedirectToAction("Login", "Auth");

            ViewBag.Username = user.Username ?? "Usuario";
            ViewBag.Email = user.Email ?? "Email no disponible";
            ViewBag.FullName = user.FullName ?? user.Username ?? "Usuario";

            var conversations = await _context.Conversations
                .Where(c => c.UserID == userId && c.Status == "Active")
                .OrderByDescending(c => c.LastUpdated)
                .Select(c => new
                {
                    c.ConversationID,
                    c.Title,
                    c.LastUpdated,
                    Messages = c.Messages
                        .OrderBy(m => m.CreatedAt)
                        .Select(m => new
                        {
                            m.MessageID,
                            m.Content,
                            m.SenderType,
                            m.CreatedAt
                        })
                        .ToList()
                })
                .ToListAsync();

            var currentConversationData = conversationId.HasValue && conversationId > 0
                ? conversations.FirstOrDefault(c => c.ConversationID == conversationId.Value)
                : conversations.FirstOrDefault();

            Conversation currentConversation = null;
            if (currentConversationData != null)
            {
                currentConversation = new Conversation
                {
                    ConversationID = currentConversationData.ConversationID,
                    Title = currentConversationData.Title,
                    LastUpdated = currentConversationData.LastUpdated,
                    Messages = currentConversationData.Messages.Select(m => new Message
                    {
                        MessageID = m.MessageID,
                        Content = m.Content,
                        SenderType = m.SenderType,
                        CreatedAt = m.CreatedAt
                    }).ToList()
                };
            }
            else
            {
                currentConversation = new Conversation
                {
                    ConversationID = 0,
                    Title = "Nueva conversación",
                    Messages = new List<Message>()
                };
            }

            var viewModel = new ChatViewModel
            {
                CurrentConversationId = currentConversation.ConversationID,
                UserConversations = conversations.Select(c => new Conversation
                {
                    ConversationID = c.ConversationID,
                    Title = c.Title,
                    LastUpdated = c.LastUpdated,
                    Messages = c.Messages.Select(m => new Message
                    {
                        MessageID = m.MessageID,
                        Content = m.Content,
                        SenderType = m.SenderType,
                        CreatedAt = m.CreatedAt
                    }).ToList()
                }).ToList(),
                CurrentMessages = currentConversation.Messages.ToList(),
                AIProvider = "Gemini"
            };

            return View(viewModel);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error en Index");
            return RedirectToAction("Error", "Home");
        }
    }

    [HttpPost]
    public async Task<IActionResult> SendMessage([FromBody] SendMessageRequest request)
    {
        try
        {
            var userId = GetUserId();
            if (userId == 0) return Json(new { success = false, error = "No autenticado" });
            if (string.IsNullOrWhiteSpace(request.Message)) return Json(new { success = false, error = "Mensaje vacío" });

            if (!request.ConversationId.HasValue || request.ConversationId == 0)
            {
                var newConv = await CreateNewConversationAsync(userId);
                request.ConversationId = newConv.ConversationID;
            }

            var conversation = await _context.Conversations
                .FirstOrDefaultAsync(c => c.ConversationID == request.ConversationId.Value && c.UserID == userId);
            if (conversation == null) return Json(new { success = false, error = "Conversación no encontrada" });

            var userMessage = new Message
            {
                ConversationID = request.ConversationId.Value,
                SenderType = "User",
                Content = request.Message.Trim(),
                CreatedAt = DateTime.UtcNow,
                IsEdited = false
            };
            _context.Messages.Add(userMessage);
            await _context.SaveChangesAsync();

            var aiResponse = await CallGeminiWithRetry(request.Message, conversation.ConversationID);

            var aiMessage = new Message
            {
                ConversationID = request.ConversationId.Value,
                SenderType = "AI",
                Content = aiResponse,
                CreatedAt = DateTime.UtcNow,
                ParentMessageID = userMessage.MessageID,
                IsEdited = false
            };
            _context.Messages.Add(aiMessage);

            // ACTUALIZAR TÍTULO CON RESPUESTA DE IA (más relevante)
            if (string.IsNullOrWhiteSpace(conversation.Title) || conversation.Title.StartsWith("Chat ") || conversation.Title.Contains("..."))
            {
                conversation.Title = GenerateConversationTitle(aiResponse);
            }

            conversation.LastUpdated = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            await LogAuditAsync(userId, "MessageSent", new
            {
                conversationId = request.ConversationId.Value,
                userMessageId = userMessage.MessageID,
                aiMessageId = aiMessage.MessageID
            });

            return Json(new
            {
                success = true,
                conversationId = request.ConversationId.Value,
                userMessage = new
                {
                    messageId = userMessage.MessageID,
                    content = userMessage.Content,
                    senderType = "User",
                    timestamp = userMessage.CreatedAt.ToPeruTime().ToString("HH:mm")
                },
                aiMessage = new
                {
                    messageId = aiMessage.MessageID,
                    content = aiMessage.Content,
                    senderType = "AI",
                    timestamp = aiMessage.CreatedAt.ToPeruTime().ToString("HH:mm")
                },
                typing = false
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error en SendMessage");
            return Json(new { success = false, error = "Error interno del servidor" });
        }
    }

    [HttpGet]
    public async Task<IActionResult> LoadConversation(int conversationId)
    {
        try
        {
            var userId = GetUserId();
            if (userId == 0) return Json(new { success = false, error = "Usuario no autenticado" });

            var conversationData = await _context.Conversations
                .AsNoTracking()
                .Where(c => c.ConversationID == conversationId && c.UserID == userId && c.Status == "Active")
                .Select(c => new
                {
                    c.ConversationID,
                    c.Title,
                    Messages = c.Messages
                        .OrderBy(m => m.CreatedAt)
                        .Select(m => new
                        {
                            m.MessageID,
                            m.Content,
                            m.SenderType,
                            m.CreatedAt
                        })
                        .ToList()
                })
                .FirstOrDefaultAsync();

            if (conversationData == null)
                return Json(new { success = false, error = "Conversación no encontrada" });

            var messages = conversationData.Messages.Select(m => new
            {
                messageId = m.MessageID,
                content = m.Content,
                senderType = m.SenderType,
                timestamp = m.CreatedAt.ToPeruTime().ToString("HH:mm")
            }).ToList();

            return Json(new
            {
                success = true,
                conversationId = conversationData.ConversationID,
                title = conversationData.Title ?? "Sin título",
                messages
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error en LoadConversation para ID: {ConversationId}", conversationId);
            return Json(new { success = false, error = "Error interno del servidor" });
        }
    }

    [HttpPost]
    public async Task<IActionResult> NewConversation()
    {
        try
        {
            var userId = GetUserId();
            if (userId == 0) return Json(new { success = false, error = "Usuario no autenticado" });

            var conv = await CreateNewConversationAsync(userId);
            await LogAuditAsync(userId, "ConversationCreated", new { conv.ConversationID });

            return Json(new
            {
                success = true,
                conversationId = conv.ConversationID,
                title = conv.Title,
                timestamp = conv.CreatedAt.ToPeruTime().ToString("dd/MM/yyyy HH:mm")
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error en NewConversation");
            return Json(new { success = false, error = "Error al crear conversación" });
        }
    }

    [HttpPost]
    public async Task<IActionResult> DeleteConversation([FromBody] DeleteConversationRequest request)
    {
        try
        {
            var userId = GetUserId();
            if (userId == 0) return Json(new { success = false, error = "Usuario no autenticado" });
            if (!request.ConversationId.HasValue || request.ConversationId <= 0)
                return Json(new { success = false, error = "ID de conversación inválido" });

            var conv = await _context.Conversations
                .FirstOrDefaultAsync(c => c.ConversationID == request.ConversationId.Value && c.UserID == userId);
            if (conv == null) return Json(new { success = false, error = "Conversación no encontrada" });

            conv.Status = "Deleted";
            conv.LastUpdated = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            await LogAuditAsync(userId, "ConversationDeleted", new { conversationId = request.ConversationId.Value });
            return Json(new { success = true });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error en DeleteConversation para ID: {ConversationId}", request?.ConversationId);
            return Json(new { success = false, error = "Error al eliminar conversación" });
        }
    }

    [HttpGet]
    public async Task<IActionResult> GetUserInfo()
    {
        try
        {
            var userId = GetUserId();
            if (userId == 0) return Json(new { success = false, error = "Usuario no autenticado" });

            var user = await _context.Users.FindAsync(userId);
            return Json(new
            {
                success = true,
                username = user?.Username ?? "Usuario",
                email = user?.Email ?? "Email no disponible",
                fullName = user?.FullName ?? user?.Username ?? "Usuario"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error en GetUserInfo");
            return Json(new { success = false, error = "Error al obtener información del usuario" });
        }
    }

    [HttpGet]
    public async Task<IActionResult> GetConversations()
    {
        try
        {
            var userId = GetUserId();
            if (userId == 0) return Json(new { success = false, error = "Usuario no autenticado" });

            var conversations = await _context.Conversations
                .Where(c => c.UserID == userId && c.Status == "Active")
                .OrderByDescending(c => c.LastUpdated)
                .Select(c => new
                {
                    id = c.ConversationID,
                    title = c.Title ?? "Sin título",
                    timestamp = c.LastUpdated.ToPeruTime().ToString("dd/MM HH:mm")
                })
                .ToListAsync();

            return Json(conversations);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error en GetConversations");
            return Json(new { success = false, error = "Error al cargar conversaciones" });
        }
    }

    #region Métodos Privados

    private int GetUserId()
    {
        return int.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out int id) ? id : 0;
    }

    private async Task<Conversation> CreateNewConversationAsync(int userId)
    {
        var conv = new Conversation
        {
            UserID = userId,
            Title = "Nueva conversación",
            CreatedAt = DateTime.UtcNow,
            LastUpdated = DateTime.UtcNow,
            Status = "Active"
        };
        _context.Conversations.Add(conv);
        await _context.SaveChangesAsync();

        var meta = new ConversationMetadata
        {
            ConversationID = conv.ConversationID,
            ModelUsed = GEMINI_MODEL,
            Parameters = JsonSerializer.Serialize(new { temperature = TEMPERATURE, maxTokens = MAX_TOKENS }),
            CreatedAt = DateTime.UtcNow
        };
        _context.ConversationMetadata.Add(meta);
        await _context.SaveChangesAsync();

        return conv;
    }

    private async Task<string> CallGeminiWithRetry(string userMessage, int conversationId)
    {
        string fullResponse = "";
        int maxRetries = 2;

        for (int i = 0; i <= maxRetries; i++)
        {
            var prompt = BuildPromptWithFullMemory(userMessage, conversationId, fullResponse, i > 0);
            var response = await SendToGemini(prompt);
            if (string.IsNullOrWhiteSpace(response)) continue;

            fullResponse += response;
            if (!IsTruncated(response)) break;
            if (i == maxRetries) fullResponse += " [Respuesta incompleta]";
        }

        return fullResponse.Trim();
    }

    private async Task<string> SendToGemini(string prompt)
    {
        try
        {
            var body = new
            {
                contents = new[] { new { parts = new[] { new { text = prompt } } } },
                generationConfig = new { temperature = TEMPERATURE, maxOutputTokens = MAX_TOKENS, topP = 0.9 }
            };

            var json = JsonSerializer.Serialize(body, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            var response = await _httpClient.PostAsync(GeminiUrl(), content);

            if (!response.IsSuccessStatusCode) return null;

            var result = await response.Content.ReadAsStringAsync();
            var gemini = JsonSerializer.Deserialize<GeminiResponse>(result);
            return gemini?.Candidates?[0]?.Content?.Parts?[0]?.Text?.Trim();
        }
        catch
        {
            return null;
        }
    }

    private string GeminiUrl() => $"https://generativelanguage.googleapis.com/v1beta/models/{GEMINI_MODEL}:generateContent?key={GEMINI_API_KEY}";

    private bool IsTruncated(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return true;
        if (text.Length < 30) return false;

        var lower = text.Trim().ToLower();
        var endings = new[] { "siempre es", "es importante", "recuerda que", "por favor", "en conclusión", "continúa", "sigue" };
        return !text.EndsWithAny(".", "!", "?", ":", ")", "]", "\"", "'") &&
               endings.Any(e => lower.EndsWith(e));
    }

    private string BuildPromptWithFullMemory(string userMessage, int conversationId, string previous = "", bool isRetry = false)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Eres LAWDGE IA, tu asistente legal favorito en Perú.");
        sb.AppendLine("Tu misión es ayudar con una sonrisa, siempre con claridad y calidez.");
        sb.AppendLine("REGLAS DE ORO:");
        sb.AppendLine("1. Responde en español claro, amable y completo.");
        sb.AppendLine("2. Usa 1-3 oraciones como máximo. Siempre termina con punto o signo.");
        sb.AppendLine("3. Incluye 1-2 emoticones por respuesta (ej: sonriendo, corazón, bombilla, cohete, bandera de Perú).");
        sb.AppendLine("4. Sé útil, positivo y cercano. Haz que la persona sienta que estás de su lado.");
        sb.AppendLine("5. Usa lenguaje sencillo, evita tecnicismos innecesarios.");
        sb.AppendLine("6. RECUERDA TODO EL HISTORIAL y responde en contexto.");
        sb.AppendLine("");

        var allMessages = _context.Messages
            .Where(m => m.ConversationID == conversationId)
            .OrderBy(m => m.CreatedAt)
            .Select(m => new { m.SenderType, m.Content })
            .ToList();

        if (allMessages.Any())
        {
            sb.AppendLine("=== HISTORIAL COMPLETO DE LA CONVERSACIÓN ===");
            foreach (var m in allMessages)
            {
                var sender = m.SenderType == "User" ? "Tú" : "LAWDGE IA";
                sb.AppendLine($"{sender}: {m.Content}");
            }
            sb.AppendLine("=== FIN DEL HISTORIAL ===");
            sb.AppendLine("");
        }

        if (isRetry && !string.IsNullOrWhiteSpace(previous))
        {
            sb.AppendLine($"RESPUESTA ANTERIOR (continúa): {previous}");
            sb.AppendLine("CONTINÚA Y TERMINA CON CALIDEZ.");
        }
        else
        {
            sb.AppendLine($"PREGUNTA ACTUAL: {userMessage}");
            sb.AppendLine("RESPUESTA AMIGABLE, EN CONTEXTO Y COMPLETA:");
        }

        return sb.ToString();
    }

    private string GenerateConversationTitle(string aiResponse)
    {
        if (string.IsNullOrWhiteSpace(aiResponse)) return "Nueva conversación";

        var clean = aiResponse
            .Split('\n')[0]
            .Replace("sonriendo", "").Replace("corazón", "").Replace("bombilla", "").Replace("cohete", "").Replace("bandera de Perú", "")
            .Trim();

        return clean.Length > 37 ? clean.Substring(0, 37) + "..." : clean;
    }

    private async Task LogAuditAsync(int? userId, string action, object details)
    {
        try
        {
            _context.AuditLogs.Add(new AuditLog
            {
                UserID = userId,
                Action = action,
                Details = JsonSerializer.Serialize(details),
                CreatedAt = DateTime.UtcNow
            });
            await _context.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error en LogAuditAsync");
        }
    }

    #endregion
}

// EXTENSIÓN PARA FINALES DE ORACIÓN
public static class StringExtensions
{
    public static bool EndsWithAny(this string str, params string[] values)
    {
        return values.Any(v => str.EndsWith(v, StringComparison.OrdinalIgnoreCase));
    }
}

// ZONA HORARIA PERÚ
public static class DateTimeExtensions
{
    private static readonly TimeZoneInfo PeruTimeZone = TimeZoneInfo.FindSystemTimeZoneById("SA Pacific Standard Time");
    public static DateTime ToPeruTime(this DateTime utc) => TimeZoneInfo.ConvertTimeFromUtc(utc, PeruTimeZone);
}

// MODELOS GEMINI
public class GeminiResponse { [JsonPropertyName("candidates")] public GeminiCandidate[] Candidates { get; set; } }
public class GeminiCandidate { [JsonPropertyName("content")] public GeminiContent Content { get; set; } }
public class GeminiContent { [JsonPropertyName("parts")] public GeminiPart[] Parts { get; set; } }
public class GeminiPart { [JsonPropertyName("text")] public string Text { get; set; } }