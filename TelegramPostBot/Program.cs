using System.Collections.Concurrent;
using Telegram.Bot;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;
using Message = Telegram.Bot.Types.Message;

public class Program
{
    private static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);
        var app = builder.Build();

        const string BotToken = "7768693479:AAEnE4f5_yZI7Ix_9AA2KDL_YyaCn8jLcHM"; 
        var BotClient = new TelegramBotClient(BotToken);

        var ChannelIds = new List<string>
{
    "@ForNewProjects",
    "@YourOtherChannel"
};

        var AllowedUsers = new HashSet<string>
{
    "urazmetov_d",
    "Ulugbek19_04"
};

        var UserDrafts = new ConcurrentDictionary<long, PostDraft>();

        var cts = new CancellationTokenSource();
        var receiverOptions = new ReceiverOptions { AllowedUpdates = Array.Empty<UpdateType>() };

        _ = Task.Run(async () =>
        {
            Console.WriteLine("🤖 Bot ishga tushdi (Web API + Polling)...");
            await BotClient.ReceiveAsync(
                new DefaultUpdateHandler(
                    (bot, update, token) => HandleUpdateAsync(bot, update, token, AllowedUsers, UserDrafts, ChannelIds),
                    HandleErrorAsync),
                receiverOptions,
                cts.Token
            );
        });

        // === API endpointlar ===
        app.MapPost("/users/add", (string username) =>
        {
            if (AllowedUsers.Add(username))
                return Results.Ok($"✅ {username} qo‘shildi");
            else
                return Results.BadRequest($"⚠️ {username} allaqachon bor");
        });

        app.MapGet("/", () => "✅ Bot ishlayapti");

        app.Run();

        // === Telegram logika ===

        static async Task HandleUpdateAsync(
            ITelegramBotClient bot,
            Update update,
            CancellationToken cancellationToken,
            HashSet<string> AllowedUsers,
            ConcurrentDictionary<long, PostDraft> UserDrafts,
            List<string> ChannelIds)
        {
            if (update.Message is Telegram.Bot.Types.Message message)
            {
                var chatId = message.Chat.Id;
                var userId = message.From?.Id.ToString();
                var username = message.From?.Username;

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
                        draft.Title = message.Text.ToUpper();
                        await bot.SendMessage(chatId, "📝 Tavsifni yozing:", cancellationToken: cancellationToken);
                        return;
                    }

                    if (string.IsNullOrEmpty(draft.Description))
                    {
                        draft.Description = FormatDescription(message.Text);

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
                            await bot.SendPhoto(chatId, InputFile.FromFileId(draft.MediaFileId!), previewCaption, ParseMode.Html, replyMarkup: confirmMarkup, cancellationToken: cancellationToken);
                        }
                        else
                        {
                            await bot.SendVideo(chatId, InputFile.FromFileId(draft.MediaFileId!), previewCaption, ParseMode.Html, replyMarkup: confirmMarkup, cancellationToken: cancellationToken);
                        }
                    }
                }
            }

            if (update.CallbackQuery is { Data: "confirm" } callback)
            {
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

                foreach (var channelId in ChannelIds)
                {
                    try
                    {
                        if (draft.IsPhoto)
                        {
                            await bot.SendPhoto(channelId, InputFile.FromFileId(draft.MediaFileId!), finalCaption, ParseMode.Html, replyMarkup: markup);
                        }
                        else
                        {
                            await bot.SendVideo(channelId, InputFile.FromFileId(draft.MediaFileId!), finalCaption, ParseMode.Html, replyMarkup: markup);
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"❌ {channelId} ga yuborishda xatolik: {ex.Message}");
                    }
                }

                await bot.SendMessage(chatId, "✅ Post hamma kanalga yuborildi.");
                await bot.AnswerCallbackQuery(callback.Id, "✅ Post yuborildi!");
                UserDrafts.TryRemove(chatId, out _);
            }
        }

        static bool IsUserAllowed(string? userId, string? username, HashSet<string> AllowedUsers)
        {
            return !string.IsNullOrEmpty(userId) && AllowedUsers.Contains(userId) ||
                   !string.IsNullOrEmpty(username) && AllowedUsers.Contains(username);
        }

        static string FormatDescription(string input)
        {
            var lines = input.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            return string.Join("\n", lines.Select(l => "🔹 " + l.Trim()));
        }
        static Task HandleErrorAsync(ITelegramBotClient botClient, Exception exception, CancellationToken cancellationToken)
        {
            Console.WriteLine("❌ Bot xatosi: " + exception.Message);
            return Task.CompletedTask;
        }

    }
}

record PostDraft
{
    public string? MediaFileId { get; set; }
    public bool IsPhoto { get; set; }
    public string? Title { get; set; }
    public string? Description { get; set; }
}
