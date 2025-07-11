using System.Collections.Concurrent;
using Telegram.Bot;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

namespace MultiChannelBot;

internal class Program
{
    private const string BotToken = "7768693479:AAEnE4f5_yZI7Ix_9AA2KDL_YyaCn8jLcHM";
    private static readonly ITelegramBotClient BotClient = new TelegramBotClient(BotToken);

    // Bir nechta kanal ID larini shu yerga yozing (bot admin bo'lishi kerak)
    private static readonly List<string> ChannelIds = new()
    {
        "@ForNewProjects",
        "@YourOtherChannel"
    };

    
    private static readonly HashSet<string> AllowedUsers = new()
    {
        "urazmetov_d",
        "Ulugbek19_04"
    };

    private static readonly ConcurrentDictionary<long, PostDraft> UserDrafts = new();

    public static async Task Main(string[] args)
    {
        Console.WriteLine("🤖 Bot ishga tushdi...");

        using var cts = new CancellationTokenSource();

        var receiverOptions = new ReceiverOptions
        {
            AllowedUpdates = Array.Empty<UpdateType>()
        };

        var handler = new DefaultUpdateHandler(HandleUpdateAsync, HandleErrorAsync);
        await BotClient.ReceiveAsync(handler, receiverOptions, cts.Token);
    }

    private static async Task HandleUpdateAsync(ITelegramBotClient bot, Update update, CancellationToken cancellationToken)
    {
        if (update.Message is Message message)
        {
            var chatId = message.Chat.Id;
            var userId = message.From?.Id.ToString();
            var username = message.From?.Username;

            // Foydalanuvchi ruhsati tekshiruvi
            if (!IsUserAllowed(userId, username))
            {
                await bot.SendMessage(
                    chatId,
                    "❌ Sizda bu botdan foydalanish huquqi yo'q.\n\n" +
                    "📞 Ruxsat olish uchun admin bilan bog'laning: @Ulugbek19_04",
                    cancellationToken: cancellationToken
                );
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
                UserDrafts[chatId] = new PostDraft
                {
                    MediaFileId = fileId,
                    IsPhoto = message.Type == MessageType.Photo
                };

                await bot.SendMessage(chatId, "📌 Sarlavhani yozing:", cancellationToken: cancellationToken);
                return;
            }

            if (message.Type == MessageType.Text && UserDrafts.ContainsKey(chatId))
            {
                var draft = UserDrafts[chatId];

                if (string.IsNullOrEmpty(draft.Title))
                {
                    draft.Title = message.Text.ToUpper(); // Katta harflar bilan
                    await bot.SendMessage(chatId, "📝 Tavsifni yozing:", cancellationToken: cancellationToken);
                    return;
                }

                if (string.IsNullOrEmpty(draft.Description))
                {
                    draft.Description = FormatDescription(message.Text);

                    string previewCaption = $"<b>{draft.Title}</b>\n\n{draft.Description}\n\n" +
                                            "📞 <i>Murojaat uchun:</i> 👉 @Urazmetov_23\n" +
                                            "📱 <i>WhatsApp:</i> +79372807194";

                    var confirmMarkup = new InlineKeyboardMarkup(new[]
                    {
                        new[]
                        {
                            InlineKeyboardButton.WithCallbackData("✅ Tasdiqlayman", "confirm")
                        },
                        new[]
                        {
                            InlineKeyboardButton.WithUrl("Telegram", "https://t.me/urazmetov_tv"),
                            InlineKeyboardButton.WithUrl("YouTube", "https://www.youtube.com/@urazmetovtv"),
                            InlineKeyboardButton.WithUrl("Instagram", "https://www.instagram.com/urazmetov_d/")
                        }
                    });

                    // Media bilan preview yuborish
                    if (draft.IsPhoto)
                    {
                        await bot.SendPhoto(
                            chatId: chatId,
                            photo: InputFile.FromFileId(draft.MediaFileId!),
                            caption: previewCaption,
                            parseMode: ParseMode.Html,
                            replyMarkup: confirmMarkup,
                            cancellationToken: cancellationToken
                        );
                    }
                    else
                    {
                        await bot.SendVideo(
                            chatId: chatId,
                            video: InputFile.FromFileId(draft.MediaFileId!),
                            caption: previewCaption,
                            parseMode: ParseMode.Html,
                            replyMarkup: confirmMarkup,
                            cancellationToken: cancellationToken
                        );
                    }
                }
            }
        }

        if (update.CallbackQuery is { Data: "confirm" } callback)
        {
            var chatId = callback.Message!.Chat.Id;
            var userId = callback.From?.Id.ToString();
            var username = callback.From?.Username;

            // Callback query uchun ham ruhsat tekshiruvi
            if (!IsUserAllowed(userId, username))
            {
                await BotClient.AnswerCallbackQuery(
                    callback.Id,
                    "❌ Sizda bu botdan foydalanish huquqi yo'q!",
                    showAlert: true
                );
                return;
            }

            if (!UserDrafts.TryGetValue(chatId, out var draft)) return;

            string finalCaption = $"<b>{draft.Title}</b>\n\n{draft.Description}\n\n" +
                                  "📞 <i>Murojaat uchun:</i> 👉 @Urazmetov_23\n" +
                                  "📱 <i>WhatsApp:</i> +79372807194";

            var socialMediaMarkup = new InlineKeyboardMarkup(new[]
            {
                new[]
                {
                    InlineKeyboardButton.WithUrl("Telegram", "https://t.me/urazmetov_tv"),
                    InlineKeyboardButton.WithUrl("YouTube", "https://www.youtube.com/@urazmetovtv"),
                    InlineKeyboardButton.WithUrl("Instagram", "https://www.instagram.com/urazmetov_d/")
                }
            });

            foreach (var channelId in ChannelIds)
            {
                try
                {
                    if (draft.IsPhoto)
                    {
                        await BotClient.SendPhoto(
                            chatId: channelId,
                            photo: InputFile.FromFileId(draft.MediaFileId!),
                            caption: finalCaption,
                            parseMode: ParseMode.Html,
                            replyMarkup: socialMediaMarkup
                        );
                    }
                    else
                    {
                        await BotClient.SendVideo(
                            chatId: channelId,
                            video: InputFile.FromFileId(draft.MediaFileId!),
                            caption: finalCaption,
                            parseMode: ParseMode.Html,
                            replyMarkup: socialMediaMarkup
                        );
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"❌ {channelId} ga yuborishda xatolik: {ex.Message}");
                }
            }

            await BotClient.SendMessage(chatId, "✅ Post hamma kanalga yuborildi.");
            await BotClient.AnswerCallbackQuery(callback.Id, "✅ Post yuborildi!");
            UserDrafts.TryRemove(chatId, out _);
        }
    }

    private static bool IsUserAllowed(string? userId, string? username)
    {
        // User ID yoki username orqali tekshirish
        if (!string.IsNullOrEmpty(userId) && AllowedUsers.Contains(userId))
            return true;

        if (!string.IsNullOrEmpty(username) && AllowedUsers.Contains(username))
            return true;

        return false;
    }

    private static string FormatDescription(string input)
    {
        var lines = input.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        return string.Join("\n", lines.Select(l => "🔹 " + l.Trim()));
    }

    private static Task HandleErrorAsync(ITelegramBotClient bot, Exception exception, CancellationToken cancellationToken)
    {
        Console.WriteLine("❌ Xatolik: " + exception.Message);
        return Task.CompletedTask;
    }

    private class PostDraft
    {
        public string? MediaFileId { get; set; }
        public bool IsPhoto { get; set; }
        public string? Title { get; set; }
        public string? Description { get; set; }
    }
}