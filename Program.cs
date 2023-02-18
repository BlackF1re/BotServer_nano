using System;
using System.IO;
using System.Threading.Tasks;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Polling;
using Telegram.Bot.Types.Enums;
using System.Text;
using Microsoft.Data.Sqlite;
using Microsoft.VisualBasic;

internal class Program
{
    public static string logPath = "TGBot_server_log.txt"; // путь к логу (добавить изменение пути лога в интерфейсе?)
    public static string connString = "Data Source = appDB.db"; //путь к бд
    public static string botToken = "5969998133:AAF3nNDlYNfryOulNHKtsxlhuGo_roxXYXI"; //токен бота
    public static TelegramBotClient botClient = new(botToken); //инициализация клиента
    public static SqliteConnection sqliteConn = new(connString); //инициализация подключения к бд

    private static async Task Main() //string[] args
    {


        using CancellationTokenSource cts = new();
        User me = await botClient.GetMeAsync();

        botClient.StartReceiving(updateHandler: HandleUpdateAsync,
                                 pollingErrorHandler: HandlePollingErrorAsync,
                                 cancellationToken: cts.Token);
        //Console output on starting
        Console.WriteLine($"Start listening bot @{me.Username} named as {me.FirstName}. Timestamp: {DateTime.Now}\n");
        Console.WriteLine("-----------------------------------------------------------------------------------------------");
        Console.ReadLine();
        // отправка запроса отмены для остановки
        cts.Cancel();
    }

    private static async Task HandleUpdateAsync(ITelegramBotClient botClient, Update update, CancellationToken cancellationToken)
    {
        var message = update.Message;
        if (message is null)
        {
            return;
        }
        var chatId = message.Chat.Id;
        string? fixedForDbMessageText = null; //правка текста для экранирования запроса

        //заготовленные реакции бота на определенные типы сообщений
        #region bot's reactions on incoming message types
        string reaction_recievedAudio = "Good audio, but I don't have an ears";
        string reaction_recievedContact = "Would you like us to contact you later?";
        string reaction_recievedDocument = "Delete this document.";
        string reaction_recievedPhoto = "Nice photo, but send me a text.";
        string reaction_recievedSticker = "Answers with stickers do not count as answers. Respect your and other's time.";
        string reaction_recievedVideo = "Is it a video?";
        string reaction_recievedVoice = "Nice moan.";
        string reaction_recievedText = "Welcome!";
        #endregion

        #region sqlQueries
        string recievedMessageToDbQuery = $"INSERT INTO received_messages(username, is_bot, first_name, last_name, language_code, chat_id, message_id, message_date, chat_type, message_content) " +
        $"VALUES('@{message.Chat.Username}', '0', '{message.Chat.FirstName}', '{message.Chat.LastName}', 'ru', '{message.Chat.Id}', '{message.MessageId}', '{DateTime.Now}', '{message.Chat.Type}', '{message.Text}')";

        string recievedUnacceptableMessageToDbQuery = $"INSERT INTO received_messages(username, is_bot, first_name, last_name, language_code, chat_id, message_id, message_date, chat_type, message_content) " +
        $"VALUES('@{message.Chat.Username}', '0', '{message.Chat.FirstName}', '{message.Chat.LastName}', 'ru', '{message.Chat.Id}', '{message.MessageId}', '{DateTime.Now}', '{message.Chat.Type}', '{fixedForDbMessageText}')";

        string recievedPhotoMessageToDbQuery = $"INSERT INTO received_messages(username, is_bot, first_name, last_name, language_code, chat_id, message_id, message_date, chat_type, message_content) " +
        $"VALUES('@{message.Chat.Username}', '0', '{message.Chat.FirstName}', '{message.Chat.LastName}', 'ru', '{message.Chat.Id}', '{message.MessageId}', '{DateTime.Now}', '{message.Chat.Type}', '{message.Photo}')";

        string returningAllUserToBotPrivateMessages = $"SELECT * FROM received_messages WHERE username = '{message.Chat.Username}'";
        #endregion

        //модуль математических вычислений (soon...)
        if (message.Text != null && (message.Text.ToLower().Contains("calc") || message.Text.ToLower().Contains("рассч") ||
            message.Text.ToLower().Contains("счит") || message.Text.ToLower().Contains("счет") ||
            message.Text.ToLower().Contains("вычисл") || message.Text.ToLower().Contains("clc")))
        {
            await botClient.SendTextMessageAsync(chatId: message.Chat.Id,
                                                        text: "Math calcs are not implemented yet...",
                                                        cancellationToken: cancellationToken);
        }

        #region PrepairedMessageSending
        if (message.Audio is not null)
        {
            await PrepairedMessageSender(botClient, reaction_recievedAudio, message.Chat.Id, cancellationToken);

        }

        if (message.Contact is not null)
        {
            await PrepairedMessageSender(botClient, reaction_recievedContact, message.Chat.Id, cancellationToken);
        }

        if (message.Document is not null)
        {
            await PrepairedMessageSender(botClient, reaction_recievedDocument, message.Chat.Id, cancellationToken);
        }

        if (message.Photo is not null) // Telegram.Bot.Types.PhotoSize[4]} IT IS TRUE
        {
            await PrepairedMessageSender(botClient, reaction_recievedPhoto, message.Chat.Id, cancellationToken);

            LiveLogger(message); // живой лог
            FileLogger(message, Convert.ToString(message.Photo.Length), message.Chat.Id, logPath); // логгирование в файл
            DBQuerySender(sqliteConn, recievedPhotoMessageToDbQuery);

        }
        if (message.Sticker is not null)
        {
            await PrepairedMessageSender(botClient, reaction_recievedSticker, message.Chat.Id, cancellationToken);
        }

        if (message.Video is not null)
        {
            await PrepairedMessageSender(botClient, reaction_recievedVideo, message.Chat.Id, cancellationToken);
        }

        if (message.Voice is not null)
        {
            await PrepairedMessageSender(botClient, reaction_recievedVoice, message.Chat.Id, cancellationToken);

        }

        if (message.Text is not null)
        {
            //await PrepairedMessageSender(botClient, reaction_recievedText, message.Chat.Id, cancellationToken);
            //await PrepairedMessageSender(botClient, "Вы можете использовать следующее: \n", message.Chat.Id, cancellationToken);
            LiveLogger(message); // живой лог
            FileLogger(message, message.Text, message.Chat.Id, logPath); // логгирование в файл

            //checking SQL-problem symbols in message before writing it in db
            if (message.Text is not null && message.Text.Contains('\''))
            {
                string convertedMessageText = message.Text.Replace("'", "\\'");

                DBQuerySender(sqliteConn, recievedUnacceptableMessageToDbQuery);
            }

            else
            {
                DBQuerySender(sqliteConn, recievedMessageToDbQuery);

            }

            await ParrotedMessageSender(botClient, message, chatId, cancellationToken);
        }
        #endregion
    }

    private static Task HandlePollingErrorAsync(ITelegramBotClient botClient, Exception exception, CancellationToken cancellationToken) //обработчик ошибок API
    {
        var ErrorMessage = exception switch
        {
            ApiRequestException apiRequestException
                => $"Telegram API Error:\n[{apiRequestException.ErrorCode}]\n{apiRequestException.Message}\nTimestamp: {DateTime.Now}",
            _ => exception.ToString()
        };

        Console.WriteLine(ErrorMessage);
        return Task.CompletedTask;
    }

    private static void LiveLogger(Message message) //вывод полученных сообщений в консоль
    {
        Console.WriteLine($"Received a '{message.Text}' message from @{message.Chat.Username} aka {message.Chat.FirstName} {message.Chat.LastName} in chat {message.Chat.Id} at {DateTime.Now}."); //эхо
        Console.WriteLine("!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!");
        Console.WriteLine($"Raw message: {Newtonsoft.Json.JsonConvert.SerializeObject(message)}");
        Console.WriteLine("--------------------------------------------------------------------------------------------------------------------");
    }

    private static async void FileLogger(Message message, string messageText, long chatId, string logPath) //логгирование полученных сообщений
    {
        using StreamWriter logWriter = new(logPath, true); //инициализация экземпляра Streamwriter

        await logWriter.WriteLineAsync($"Received a '{messageText}' message from @{message.Chat.Username} aka {message.Chat.FirstName} {message.Chat.LastName} in chat {chatId} at {DateTime.Now}."); //эхо                                                                                                                                                                                                       //await logWriter.FlushAsync();
        await logWriter.WriteLineAsync("!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!");
        await logWriter.WriteLineAsync($"Raw message: {Newtonsoft.Json.JsonConvert.SerializeObject(message)}");
        await logWriter.WriteLineAsync("--------------------------------------------------------------------------------------------------------------------");

    }

    private static async Task PrepairedMessageSender(ITelegramBotClient botClient, string sendingMessage, long chatId, CancellationToken cancellationToken)
    {
        await botClient.SendTextMessageAsync(chatId: chatId,
                                             text: sendingMessage,
                                             cancellationToken: cancellationToken);
    }

    private static void DBQuerySender(SqliteConnection sqliteConn, string queryText)
    {
        sqliteConn.Open(); //открытие соединения
        SqliteCommand command = new() //инициализация экземпляра SqliteCommand
        {
            Connection = sqliteConn, //соединение для выполнения запроса
            CommandText = queryText //текст запроса
        };
        command.ExecuteNonQuery(); //выполнение запроса и возврат количества измененных строк
        sqliteConn.Close(); //закрытие соединения
    }

    private static async Task ParrotedMessageSender(ITelegramBotClient botClient, Message? message, long chatId, CancellationToken cancellationToken) //отправка пользователю текста его сообщения
    {
        if (message is not null)
        {
            await botClient.SendTextMessageAsync(chatId: chatId,
                                                 text: $"I received the following message:\n{message.Text}",
                                                 cancellationToken: cancellationToken);
        }
        else await ErrorSender(botClient, chatId, cancellationToken);
    }

    private static async Task ErrorSender(ITelegramBotClient botClient, long chatId, CancellationToken cancellationToken)
    {
        await botClient.SendTextMessageAsync(
        chatId: chatId,
        text: $"BotServer_nano error. message.Text is null?",
        cancellationToken: cancellationToken);
    }
}