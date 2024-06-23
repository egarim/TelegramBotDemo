using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

class Program
{
    // Replace this token with your bot's token from BotFather
    private static readonly string BotToken = "";
    private static ITelegramBotClient botClient;

    // Dictionary to hold user states, last interaction time, and conversation history
    private static Dictionary<long, (string state, DateTime lastInteraction, List<string> history)> userStates = new Dictionary<long, (string, DateTime, List<string>)>();

    static async Task Main(string[] args)
    {
        botClient = new TelegramBotClient(BotToken);

        var cts = new CancellationTokenSource();
        var cancellationToken = cts.Token;
        var receiverOptions = new ReceiverOptions
        {
            AllowedUpdates = Array.Empty<UpdateType>() // receive all update types
        };

        botClient.StartReceiving(
            HandleUpdateAsync,
            HandleErrorAsync,
            receiverOptions,
            cancellationToken
        );

        var me = await botClient.GetMeAsync();
        Console.WriteLine($"Start listening for @{me.Username}");
        Console.ReadLine();

        // Send cancellation request to stop bot
        cts.Cancel();
    }

    static async Task HandleUpdateAsync(ITelegramBotClient botClient, Update update, CancellationToken cancellationToken)
    {
        // Only process Message updates
        if (update.Type != UpdateType.Message)
            return;

        var message = update.Message;
        if (message.Type == MessageType.Text)
        {
            await HandleIncomingMessage(botClient, message);
        }
    }

    static async Task HandleIncomingMessage(ITelegramBotClient botClient, Message message)
    {
        long chatId = message.Chat.Id;
        string userMessage = message.Text;
        DateTime now = DateTime.UtcNow;

        if (!userStates.ContainsKey(chatId))
        {
            userStates[chatId] = ("START", now, new List<string>());
        }
        else
        {
            userStates[chatId] = (userStates[chatId].state, now, userStates[chatId].history);
        }

        userStates[chatId].history.Add($"User: {userMessage}");

        await HandleConversationState(botClient, chatId, userMessage, now);
    }

    static async Task HandleConversationState(ITelegramBotClient botClient, long chatId, string userMessage, DateTime now)
    {
        string currentState = userStates[chatId].state;
        string botMessage = "";

        switch (currentState)
        {
            case "START":
                botMessage = "Welcome! How can I help you today?";
                await SendMessage(botClient, chatId, botMessage);
                UpdateUserState(chatId, "AWAITING_RESPONSE", now, userMessage, botMessage);
                break;

            case "AWAITING_RESPONSE":
                if (userMessage.ToLower().Contains("help"))
                {
                    botMessage = "Sure, I can help you with that. What do you need help with?";
                    await SendMessage(botClient, chatId, botMessage);
                    UpdateUserState(chatId, "HELP_RESPONSE", now, userMessage, botMessage);
                }
                else
                {
                    botMessage = $"You said: {userMessage}";
                    await SendMessage(botClient, chatId, botMessage);
                    UpdateUserState(chatId, "START", now, userMessage, botMessage); // Reset state for simplicity
                }
                break;

            case "HELP_RESPONSE":
                botMessage = $"You need help with: {userMessage}. I'll get back to you shortly.";
                await SendMessage(botClient, chatId, botMessage);
                UpdateUserState(chatId, "START", now, userMessage, botMessage); // Reset state for simplicity
                break;

            default:
                botMessage = "I'm not sure how to handle that.";
                await SendMessage(botClient, chatId, botMessage);
                UpdateUserState(chatId, "START", now, userMessage, botMessage); // Reset state for simplicity
                break;
        }
    }

    static void UpdateUserState(long chatId, string newState, DateTime lastInteraction, string userMessage, string botMessage)
    {
        var (state, interaction, history) = userStates[chatId];
        history.Add($"Bot: {botMessage}");
        userStates[chatId] = (newState, lastInteraction, history);
    }

    static async Task SendMessage(ITelegramBotClient botClient, long chatId, string message)
    {
        await botClient.SendTextMessageAsync(
            chatId: chatId,
            text: message);
    }

    static Task HandleErrorAsync(ITelegramBotClient botClient, Exception exception, CancellationToken cancellationToken)
    {
        var ErrorMessage = exception switch
        {
            ApiRequestException apiRequestException
                => $"Telegram API Error:\n[{apiRequestException.ErrorCode}]\n{apiRequestException.Message}",
            _ => exception.ToString()
        };

        Console.WriteLine(ErrorMessage);
        return Task.CompletedTask;
    }
}