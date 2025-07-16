using System.Collections.Concurrent;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;
using Telegram.Bot.Exceptions;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

public class Program
{
    private static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);
        builder.Services.AddSingleton<ITelegramBotClient>(new TelegramBotClient("7768693479:AAGV3XRKsYs87BK84NTfnSU5-fKdQTahjM4")); // Yangi token

        var app = builder.Build();

        var AllowedUsers = new HashSet<string>
        {
            "urazmetov_d",
            "Urazmetov_23",
            "Ulugbek19_04"
        };

        var UserDrafts = new ConcurrentDictionary<long, PostDraft>();

        // Webhook ni o'rnatish
        var botClient = app.Services.GetRequiredService<ITelegramBotClient>();
        _ = Task.Run(async () =>
        {
            await botClient.SetWebhook("https://telegrampostbot-d50y.onrender.com/api/update"); // Render URL
            Console.WriteLine("🤖 Webhook o'rnatildi...");
        });

        // Webhook endpoint
        app.MapPost("/api/update", async (HttpContext context, ITelegramBotClient bot) =>
        {
            using var reader = new StreamReader(context.Request.Body);
            var updateJson = await reader.ReadToEndAsync();
            Console.WriteLine($"Received update: {updateJson}"); // Debugging uchun
            var update = System.Text.Json.JsonSerializer.Deserialize<Update>(updateJson, new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            if (update != null)
            {
                await HandleUpdateAsync(bot, update, context.RequestAborted, AllowedUsers, UserDrafts);
            }
            else
            {
                Console.WriteLine("❌ Update deserializatsiya qilinmadi!");
            }
            return Results.Ok();
        });

        // API endpointlar
        app.MapPost("/users/add", (string username) =>
        {
            if (AllowedUsers.Add(username))
                return Results.Ok($"✅ {username} qo‘shildi");
            else
                return Results.BadRequest($"⚠️ {username} allaqachon bor");
        });

        app.MapGet("/", () => "✅ Bot ishlayapti");

        app.Run();
    }

    static async Task HandleUpdateAsync(
        ITelegramBotClient bot,
        Update update,
        CancellationToken cancellationToken,
        HashSet<string> AllowedUsers,
        ConcurrentDictionary<long, PostDraft> UserDrafts)
    {
        if (update == null) return; // Null tekshiruvi

        if (update.Message != null)
        {
            var message = update.Message;
            var chatId = message.Chat.Id;
            var userId = message.From?.Id.ToString();
            var username = message.From?.Username;

            Console.WriteLine($"Received message from chat {chatId}: {message.Text}"); // Debugging

            if (!IsUserAllowed(userId, username, AllowedUsers))
            {
                await bot.SendMessage(
                    chatId,
                    "❌ Sizda bu botdan foydalanish huquqi yo'q.\n\n📞 Ruxsat olish uchun admin bilan bog'laning: @Ulugbek19_04",
                    cancellationToken: cancellationToken);
                return;
            }

            if (message.Type == MessageType.Text && message.Text == "/start")
            {
                await bot.SendMessage(chatId, "📤 Rasm yoki video yuboring.", cancellationToken: cancellationToken);
                return;
            }

            if (message.Type == MessageType.Photo || message.Type == MessageType.Video)
            {
                var fileId = message.Photo?.LastOrDefault()?.FileId ?? message.Video?.FileId;
                UserDrafts[chatId] = new PostDraft { MediaFileId = fileId, IsPhoto = message.Type == MessageType.Photo };
                await bot.SendMessage(chatId, "📌 Sarlavhani yozing:", cancellationToken: cancellationToken);
                return;
            }

            if (message.Type == MessageType.Text && UserDrafts.ContainsKey(chatId))
            {
                var draft = UserDrafts[chatId];

                if (string.IsNullOrEmpty(draft.Title))
                {
                    draft.Title = message.Text?.ToUpper() ?? string.Empty;
                    await bot.SendMessage(chatId, "📝 Tavsifni yozing:", cancellationToken: cancellationToken);
                    return;
                }

                if (string.IsNullOrEmpty(draft.Description))
                {
                    draft.Description = FormatDescription(message.Text ?? string.Empty);

                    string previewCaption = $"<b>{draft.Title}</b>\n\n{draft.Description}\n\n📞 <i>Murojaat uchun:</i> 👉 @Urazmetov_23\n📱 <i>WhatsApp:</i> +79372807194";

                    var confirmMarkup = new InlineKeyboardMarkup(new[]
                    {
                        new[] { InlineKeyboardButton.WithCallbackData("✅ Tasdiqlayman", "confirm") },
                        new[]
                        {
                            InlineKeyboardButton.WithUrl("Telegram", "https://t.me/urazmetov_tv"),
                            InlineKeyboardButton.WithUrl("YouTube", "https://www.youtube.com/@urazmetovtv"),
                            InlineKeyboardButton.WithUrl("Instagram", "https://www.instagram.com/urazmetov_d/")
                        }
                    });

                    if (draft.IsPhoto)
                    {
                        await bot.SendPhoto(chatId, InputFile.FromFileId(draft.MediaFileId!), caption: previewCaption, parseMode: ParseMode.Html, replyMarkup: confirmMarkup, cancellationToken: cancellationToken);
                    }
                    else
                    {
                        await bot.SendVideo(chatId, InputFile.FromFileId(draft.MediaFileId!), caption: previewCaption, parseMode: ParseMode.Html, replyMarkup: confirmMarkup, cancellationToken: cancellationToken);
                    }
                }
            }
        }

        if (update?.CallbackQuery != null && update.CallbackQuery.Data == "confirm")
        {
            var callback = update.CallbackQuery;
            var chatId = callback.Message!.Chat.Id;
            var userId = callback.From?.Id.ToString();
            var username = callback.From?.Username;

            if (!IsUserAllowed(userId, username, AllowedUsers))
            {
                await bot.AnswerCallbackQuery(callback.Id, "❌ Sizda bu botdan foydalanish huquqi yo'q!", showAlert: true);
                return;
            }

            if (!UserDrafts.TryGetValue(chatId, out var draft)) return;

            string finalCaption = $"<b>{draft.Title}</b>\n\n{draft.Description}\n\n📞 <i>Murojaat uchun:</i> 👉 @Urazmetov_23\n📱 <i>WhatsApp:</i> +79372807194";

            var markup = new InlineKeyboardMarkup(new[]
            {
                new[]
                {
                    InlineKeyboardButton.WithUrl("Telegram", "https://t.me/urazmetov_tv"),
                    InlineKeyboardButton.WithUrl("YouTube", "https://www.youtube.com/@urazmetovtv"),
                    InlineKeyboardButton.WithUrl("Instagram", "https://www.instagram.com/urazmetov_d/")
                }
            });

            long urazmetovTvChatId = -1001234567890; // To'g'ri chat ID ni qo'ying
            var failedChannels = new List<long>();

            try
            {
                if (draft.IsPhoto)
                {
                    await bot.SendPhoto(urazmetovTvChatId, InputFile.FromFileId(draft.MediaFileId!), caption: finalCaption, parseMode: ParseMode.Html, replyMarkup: markup, cancellationToken: cancellationToken);
                }
                else
                {
                    await bot.SendVideo(urazmetovTvChatId, InputFile.FromFileId(draft.MediaFileId!), caption: finalCaption, parseMode: ParseMode.Html, replyMarkup: markup, cancellationToken: cancellationToken);
                }
                Console.WriteLine($"✅ @URAZMETOV TV kanaliga post muvaffaqiyatli yuborildi.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ @URAZMETOV TV kanaliga yuborishda xatolik: {ex.Message}");
                failedChannels.Add(urazmetovTvChatId);
            }

            var chatIds = await GetAuthorizedChatIdsAsync(bot, cancellationToken);
            foreach (var targetChatId in chatIds)
            {
                if (targetChatId != urazmetovTvChatId)
                {
                    try
                    {
                        if (draft.IsPhoto)
                        {
                            await bot.SendPhoto(targetChatId, InputFile.FromFileId(draft.MediaFileId!), caption: finalCaption, parseMode: ParseMode.Html, replyMarkup: markup, cancellationToken: cancellationToken);
                        }
                        else
                        {
                            await bot.SendVideo(targetChatId, InputFile.FromFileId(draft.MediaFileId!), caption: finalCaption, parseMode: ParseMode.Html, replyMarkup: markup, cancellationToken: cancellationToken);
                        }
                        Console.WriteLine($"✅ {targetChatId} kanaliga post muvaffaqiyatli yuborildi.");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"❌ {targetChatId} kanaliga yuborishda xatolik: {ex.Message}");
                        failedChannels.Add(targetChatId);
                    }
                }
            }

            if (failedChannels.Count == 0)
            {
                await bot.SendMessage(chatId, "✅ Post hamma kanalga yuborildi.", cancellationToken: cancellationToken);
            }
            else
            {
                await bot.SendMessage(chatId, $"⚠️ Quyidagi kanallarga yuborishda xatolik yuz berdi: {string.Join(", ", failedChannels)}", cancellationToken: cancellationToken);
            }

            await bot.AnswerCallbackQuery(callback.Id, "✅ Post yuborildi!");
            UserDrafts.TryRemove(chatId, out _);
        }
    }

    static bool IsUserAllowed(string? userId, string? username, HashSet<string> AllowedUsers)
    {
        return (!string.IsNullOrEmpty(userId) && AllowedUsers.Contains(userId)) ||
               (!string.IsNullOrEmpty(username) && AllowedUsers.Contains(username));
    }

    static string FormatDescription(string input)
    {
        var lines = (input ?? string.Empty).Split('\n', StringSplitOptions.RemoveEmptyEntries);
        return string.Join("\n", lines.Select(l => "🔹 " + l.Trim()));
    }

    static async Task<List<long>> GetAuthorizedChatIdsAsync(ITelegramBotClient bot, CancellationToken cancellationToken)
    {
        var chatIds = new List<long>();
        var offset = 0;

        while (true)
        {
            var updates = await bot.GetUpdates(offset, 100, cancellationToken: cancellationToken);
            if (updates.Length == 0) break;

            foreach (var update in updates)
            {
                if (update.Message?.Chat.Type == ChatType.Channel)
                {
                    try
                    {
                        await bot.SendMessage(update.Message.Chat.Id, "Test", cancellationToken: cancellationToken);
                        chatIds.Add(update.Message.Chat.Id);
                    }
                    catch (ApiRequestException ex) when (ex.ErrorCode == 403)
                    {
                        Console.WriteLine($"❌ {update.Message.Chat.Id} kanaliga kirish huquqi yo'q: {ex.Message}");
                    }
                }
            }

            offset = updates[^1].Id + 1;
        }

        return chatIds;
    }

    static Task HandleErrorAsync(ITelegramBotClient botClient, Exception exception, CancellationToken cancellationToken)
    {
        Console.WriteLine($"❌ Bot xatosi: {exception.Message}\nStackTrace: {exception.StackTrace}");
        return Task.CompletedTask;
    }
}

record PostDraft
{
    public string? MediaFileId { get; set; }
    public bool IsPhoto { get; set; }
    public string? Title { get; set; }
    public string? Description { get; set; }
}