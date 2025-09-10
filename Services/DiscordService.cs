using System;
using System.Threading;
using System.Threading.Tasks;
using DiscordMaINBot.Interfaces;
using DSharpPlus;
using DSharpPlus.AsyncEvents;
using DSharpPlus.Entities;
using DSharpPlus.EventArgs;
using DSharpPlus.SlashCommands;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;

namespace DiscordMaINBot.Services;

public class DiscordService(
    IOptions<BotConfig> options,
    RandomMessageService randomMessageService,
    IMaInService maInService) : IDiscordService
{
    public async Task StartAsync(IServiceProvider serviceProvider, CancellationToken cancellationToken = default)
    {
        var discordToken = options.Value.DiscordToken;
        var discordConfiguration = new DiscordConfiguration
        {
            Intents = DiscordIntents.All,
            Token = discordToken,
            TokenType = TokenType.Bot,
            AutoReconnect = true
        };
        var client = new DiscordClient(discordConfiguration);

        var slash = client.UseSlashCommands(new SlashCommandsConfiguration
        {
            Services = serviceProvider
        });
        
        slash.RegisterCommands<Commands.Commands>();
        client.MessageCreated += HandleNewMessage;

        // Set up random messaging
        randomMessageService.SetClient(client);
        StartRandomMessageTimer();
        
        await client.ConnectAsync();
        await Task.Delay(-1, cancellationToken);
    }

    private async Task HandleNewMessage(DiscordClient client, MessageCreateEventArgs args)
    {
        if (!args.Author.IsBot)
        {
            if(args.Channel.IsPrivate)
            {
                await args.Channel.TriggerTypingAsync();
                var conversationResult =
                    await maInService.ConverseAsync(
                        args.Channel.Id.ToString(), 
                        args.Message.Content);
                
                await args.Channel.SendMessageAsync(conversationResult);
            }
            else if (args.Message.ReferencedMessage != null && 
                     args.Message.ReferencedMessage.Author.Id == client.CurrentUser.Id &&
                     args.Message.ReferencedMessage.Attachments.Any(a => a.FileName.EndsWith(".png")))
            {
                await args.Channel.TriggerTypingAsync();
            
                try
                {
                    if (!ImageThrottler.CanGenerate())
                    {
                        await args.Channel.SendMessageAsync($"❌ Rate limit exceeded. {ImageThrottler.GetRemaining()} remaining.");
                        return;
                    }

                    var oldPrompt = ImageThrottler.GetPrompt(args.Message.ReferencedMessage.Id);
                    var newPrompt = string.IsNullOrEmpty(oldPrompt) ? args.Message.Content : $"{oldPrompt} + {args.Message.Content}";
                
                    ImageThrottler.RecordRequest();
                    var image = await maInService.GenerateImageAsync(newPrompt);
                
                    if (image != null)
                    {
                        using var ms = new MemoryStream(image);
                        var response = await args.Channel.SendMessageAsync(new DiscordMessageBuilder()
                            .WithContent("✅Done") //| {ImageThrottler.GetRemaining()} remaining"
                            .AddFile("combined.png", ms)
                            .WithReply(args.Message.Id));
                        
                        ImageThrottler.StorePrompt(response.Id, newPrompt);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.Message);
                    await args.Channel.SendMessageAsync("❌ Image generation failed.");
                }
            }
            else if (args.Message.MentionedUsers.Contains(client.CurrentUser))
            {
                await args.Channel.TriggerTypingAsync();
                
                var previousMessages = await args.Channel.GetMessagesBeforeAsync(args.Message.Id, 4);
                
                var contextBuilder = new System.Text.StringBuilder();
                contextBuilder.AppendLine("Recent conversation context:");
                
                foreach (var msg in previousMessages.Reverse())
                {
                    if (!msg.Author.IsBot || msg.Author.Id == client.CurrentUser.Id)
                    {
                        var author = msg.Author.IsBot ? "Assistant" : msg.Author.Username;
                        contextBuilder.AppendLine($"{author}: {msg.Content}");
                    }
                }
                
                var messageContent = args.Message.Content
                    .Replace($"<@{client.CurrentUser.Id}>", "")
                    .Replace($"<@!{client.CurrentUser.Id}>", "")
                    .Trim();
                
                contextBuilder.AppendLine($"{args.Author.Username}: {messageContent}");
                var conversationResult =
                    await maInService.ConverseAsync(
                        args.Channel.Id.ToString(), 
                        contextBuilder.ToString());
                
                await args.Channel.SendMessageAsync(conversationResult);
            }
        }
    }
    
    private void StartRandomMessageTimer()
    {
        var interval = TimeSpan.FromMinutes(options.Value.RandomMessageIntervalMinutes);
        _ = new Timer(async _ => 
        {
            try 
            {
                await randomMessageService.TryRandomMessage();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Random message timer error: {ex.Message}");
            }
        }, null, interval, interval);
    }
}