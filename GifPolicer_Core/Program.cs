using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using Telegram.Bot;
using Telegram.Bot.Args;
using Telegram.Bot.Types.Enums;

namespace GifPolicer_Core
{
    class Program
    {
        #region Public Fields

        public static TelegramBotClient BotClient = new TelegramBotClient("563275786:AAHm47AT1zRdtLyIbp3HEJNfIEZXlLJ_FiM"); // Your telegram bot id
        public static List<UserTracker> TrackedUsers = new List<UserTracker>();
        public static List<StrikeTracker> StrikedUsers = new List<StrikeTracker>();

        public static IConfigurationRoot Configuration;

        public static int MaxStrikeCount => int.Parse(Configuration["NumberOfStrikes"]);
        public static int DaysToRestrict => int.Parse(Configuration["DaysToRestrict"]);
        public static int NumberOfMinutes => int.Parse(Configuration["NumberOfMinutes"]);
        public static int MaxGifCount => int.Parse(Configuration["MaxGifCount"]);
        public static string TelegramToken => Configuration["TelegramToken"];

        #endregion Public Fields

        #region Public Methods

        public static void TimerCallback(Object o)
        {
            Console.Clear();
        }

        #endregion Public Methods

        #region Private Methods

        public enum BotCommands
        {
            status,
            setgifs,
            setminutes,
            setstrikes,
            setrestrictdays,
            strikes,
            donate
        }

        private static async void HandleMessage(MessageEventArgs message)
        {
            var adminUsers = await BotClient.GetChatAdministratorsAsync(message.Message.Chat.Id);

            if (message.Message.Chat.Type != ChatType.Private)
            { //Disallow PMing the Bot. 
                foreach (var entity in message.Message.Entities)
                {
                    if (entity.Type == MessageEntityType.BotCommand)
                    {
                        BotCommands command;
                        var splitMsg = message.Message.Text.Split(' ');

                        if (Enum.TryParse(splitMsg[0].Replace(@"/", "").Replace("@GroestlPoliceBot", ""), out command))
                        {
                            switch (command)
                            {
                                case BotCommands.status:
                                    var msg = $"Gifstapo Bot Removes Media Messages if more than {MaxGifCount} {(MaxGifCount < 2 ? "media message is" : "media messages are")} sent within {NumberOfMinutes} {(NumberOfMinutes < 2 ? "minute" : "minutes")}.";
                                    msg += $"{Environment.NewLine}Restrict Period: {DaysToRestrict} {(DaysToRestrict < 2 ? "day" : "days")}{Environment.NewLine}";
                                    msg += $"Strike Count {MaxStrikeCount}{Environment.NewLine}{Environment.NewLine}You will be restricted if you reach your maximum strike count.";
                                    await BotClient.SendTextMessageAsync(message.Message.Chat.Id, msg);
                                    break;
                                case BotCommands.strikes:
                                    var chatId = message.Message.Chat.Id;
                                    var user = message.Message.From.Id;
                                    var trackedUser = TrackedUsers.FindAll(d => d.ChatId == chatId && d.UserId == user);
                                    await BotClient.SendTextMessageAsync(message.Message.Chat.Id, $"You have {trackedUser.Count} strikes.");
                                    break;
                                case BotCommands.donate:
                                    await BotClient.SendTextMessageAsync(message.Message.Chat.Id, "You can donate with Groestlcoin to FYoKoGrSXGpTavNFVbvW18UYxo6JVbUDDa to support the developer. ");
                                    break;
                            }
                        }
                    }
                }
            }
        }

        private static async void BotOnMessageReceived(object sender, MessageEventArgs messageEventArgs)
        {
            if ((messageEventArgs.Message.Type == MessageType.Photo || messageEventArgs.Message.Type == MessageType.Sticker || messageEventArgs.Message.Type == MessageType.Document) && messageEventArgs.Message.Date >= DateTime.UtcNow.AddMinutes(-NumberOfMinutes))
            {
                try
                {
                    var chatId = messageEventArgs.Message.Chat.Id;
                    var userId = messageEventArgs.Message.From.Id;
                    var userName = string.IsNullOrEmpty(messageEventArgs.Message.From.Username) ? messageEventArgs.Message.From.FirstName + " " + messageEventArgs.Message.From.LastName : "@" + messageEventArgs.Message.From.Username;

                    if (TrackedUsers.Any(d => d.ChatId == chatId && d.UserId == userId))
                    {
                        foreach (var userTracker in TrackedUsers.Where(d => d.ChatId == chatId && d.UserId == userId && d.PostDate < DateTime.UtcNow.AddMinutes(-NumberOfMinutes)).ToList())
                        {
                            TrackedUsers.Remove(userTracker);
                        }
                    }


                    var newTracking = new UserTracker
                    {
                        ChatId = chatId,
                        UserId = userId,
                        PostDate = messageEventArgs.Message.Date
                    };
                    TrackedUsers.Add(newTracking);

                    if (TrackedUsers.Count(d => d.ChatId == chatId && d.UserId == userId) > MaxGifCount)
                    {
                        foreach (var strikeTracker in StrikedUsers.Where(d => d.ChatId == chatId && d.UserId == userId && d.LastStrike < DateTime.UtcNow.AddDays(-DaysToRestrict)))
                        {
                            StrikedUsers.Remove(strikeTracker);
                        }

                        var usr = StrikedUsers.FirstOrDefault(d => d.ChatId == chatId && d.UserId == userId);

                        if (usr == null)
                        {
                            usr = new StrikeTracker { ChatId = chatId, UserId = userId };
                            StrikedUsers.Add(usr);
                        }
                        usr.Strikes++;
                        usr.LastStrike = messageEventArgs.Message.Date;

                        await BotClient.DeleteMessageAsync(messageEventArgs.Message.Chat.Id, messageEventArgs.Message.MessageId);

                        if (usr.Strikes >= MaxStrikeCount)
                        {
                            try
                            {
                                await BotClient.RestrictChatMemberAsync(chatId, userId, DateTime.UtcNow.AddDays(1), true, false, false, false);
                            }
                            catch { } //Clearly a naughty admin

                            await BotClient.SendTextMessageAsync(chatId, $"{userName} - Please do not spam media messages! Max Strike Count has been reached. Media Messages Restricted.");
                        }
                        else
                        {
                            await BotClient.SendTextMessageAsync(chatId, $"{userName} - Please do not spam media messages! {MaxStrikeCount - usr.Strikes} strike{(usr.Strikes == 2 ? "" : "s") } remaining.");
                        }
                        Console.WriteLine($"{DateTime.Now} - Deleted Media Message from {userName}");

                    }
                }

                //Prevent exception being thrown on previously deleted messages, sometimes they come through multiple times for some reason.
                catch (Exception e)
                {
                    Console.WriteLine($"{DateTime.Now} - Error: {Environment.NewLine}{Environment.NewLine} {e.Message}");

                }
            }
            else if (messageEventArgs.Message.Entities != null && messageEventArgs.Message.Entities.Any())
            {
                HandleMessage(messageEventArgs);
            }
        }

        private static void BotOnQueryReceived(object sender, CallbackQueryEventArgs e)
        {
            Console.WriteLine("Query");
            Console.WriteLine(e.CallbackQuery.Message + "Data: " + e.CallbackQuery.Data);
        }

        private static void Main()
        {
            var builder = new ConfigurationBuilder().SetBasePath(Directory.GetCurrentDirectory()).AddJsonFile("appconfig.json");
            Configuration = builder.Build();

            var me = BotClient.GetMeAsync().Result;


            Console.Title = me.Username;
            BotClient.OnMessage += BotOnMessageReceived;
            BotClient.OnCallbackQuery += BotOnQueryReceived;
            BotClient.OnInlineQuery += BotClientOnOnInlineQuery;
            Timer t = new Timer(TimerCallback, null, 0, 600000);



            BotClient.StartReceiving();
            Console.ReadLine();
        }

        private static void BotClientOnOnInlineQuery(object sender, InlineQueryEventArgs e)
        {
            Console.WriteLine("Inline");
            Console.WriteLine(e.InlineQuery.Query);
        }

        #endregion Private Methods

        public class StrikeTracker
        {
            public long ChatId { get; set; }
            public long UserId { get; set; }
            public byte Strikes { get; set; }
            public DateTime LastStrike { get; set; }
        }

        public class UserTracker
        {
            public long ChatId { get; set; }
            public long UserId { get; set; }
            public DateTime PostDate { get; set; }
        }
    }
}
