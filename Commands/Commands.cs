using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using DiscordMaINBot.Interfaces;
using DSharpPlus;
using DSharpPlus.Entities;
using DSharpPlus.SlashCommands;

namespace DiscordMaINBot.Commands;

public class Commands(IMaInService maInService) : ApplicationCommandModule
{
    [SlashCommand("ask", "Ask model AI a question with file")]
    public async Task AskWithFileCommand(InteractionContext ctx,
        [Option("question", "Your question for the bot")] string question,
        [Option("file", "File to analyze")] DiscordAttachment? file = null)
    {
        try
        {
            await ctx.DeferAsync();
            string responseText;
            if (file != null)
            {
                var filePath = Path.Combine(Path.GetTempPath(), file.FileName);
                using (var httpClient = new HttpClient())
                {
                    var response = await httpClient.GetAsync(file.Url);
                    if (response.IsSuccessStatusCode)
                    {
                        await using var fs = new FileStream(filePath, FileMode.Create, FileAccess.Write);
                        await response.Content.CopyToAsync(fs);
                    }
                    else
                    {
                        await ctx.EditResponseAsync(new DiscordWebhookBuilder().WithContent("Failed to download the file."));
                        return;
                    }
                }

                responseText = await maInService.AskQuestionWithFileAsync(question, [filePath]);

                if (File.Exists(filePath))
                {
                    File.Delete(filePath);
                }
            }
            else
            {
                responseText = await maInService.AskQuestionAsync(question);
            }
            

            await ctx.EditResponseAsync(new DiscordWebhookBuilder().WithContent(responseText));
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex.Message);
            await ctx.EditResponseAsync(new DiscordWebhookBuilder().WithContent("Something went wrong. Try again later"));
        }
    }
    
    [SlashCommand("translate", "Translate text to another language")]
    public async Task TranslateCommand(InteractionContext ctx,
        [Option("text", "Text to translate")] string text,
        [Option("language", "Language to translate")] string language)
    {
        try
        {
            await ctx.DeferAsync();
            var responseText = await maInService.TranslateMessageAsync(text, language);
            await ctx.EditResponseAsync(new DiscordWebhookBuilder().WithContent(responseText));
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex.Message);
            await ctx.EditResponseAsync(new DiscordWebhookBuilder().WithContent("Something went wrong. Try again later"));
        }
    }
    
    [SlashCommand("rewrite", "Rewrite text, check grammar and spelling")]
    public async Task RewriteTextCommand(InteractionContext ctx,
        [Option("text", "Text to translate")] string text)
    {
        try
        {
            await ctx.DeferAsync();
            var responseText = await maInService.RewriteMessageAsync(text);
            await ctx.EditResponseAsync(new DiscordWebhookBuilder().WithContent(responseText));
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex.Message);
            await ctx.EditResponseAsync(new DiscordWebhookBuilder().WithContent("Something went wrong. Try again later"));
        }
    }

    [SlashCommand("image", "Generate image based on text")]
    public async Task GenerateImageCommand(InteractionContext ctx,
        [Option("prompt", "Prompt for image generation")] string prompt)
    {
        try
        {
            await ctx.DeferAsync();
        
            if (!ImageThrottler.CanGenerate())
            {
                await ctx.EditResponseAsync(new DiscordWebhookBuilder()
                    .WithContent($"❌ Rate limit exceeded. {ImageThrottler.GetRemaining()} requests remaining this hour."));
                return;
            }

            ImageThrottler.RecordRequest();
            var image = await maInService.GenerateImageAsync(prompt);
        
            if (image == null)
            {
                await ctx.EditResponseAsync(new DiscordWebhookBuilder().WithContent("Failed to generate image."));
                return;
            }
        
            using var ms = new MemoryStream(image);
            ms.Position = 0;
            var response = await ctx.EditResponseAsync(new DiscordWebhookBuilder()
                .WithContent($"✅ Generated: `{prompt}`") //| {ImageThrottler.GetRemaining()} remaining
                .AddFile("image.png", ms));
            
            ImageThrottler.StorePrompt(response.Id, prompt);
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex.Message);
            await ctx.EditResponseAsync(new DiscordWebhookBuilder().WithContent("Something went wrong. Try again later"));
        }
    }
    
    
    [SlashCommand("answerthread", "Ask something and I will reply in a new thread.")]
    public async Task AnswerThreadAsync(InteractionContext ctx, 
        [Option("question", "The question you want to ask")] string question)
    {
        await ctx.DeferAsync(ephemeral: false);

        var answer = await maInService.AskQuestionAsync(question);
        if (ctx.Channel.IsThread)
        {
            await ctx.EditResponseAsync(new DiscordWebhookBuilder().WithContent(answer));
            return;
        }
        
        var botMessage = await ctx.EditResponseAsync(new DiscordWebhookBuilder()
            .WithContent($"{ctx.User.Mention} \u2705 I’ve created a thread with your question"));

        var thread = await botMessage.CreateThreadAsync(question, AutoArchiveDuration.Day);
        
        var channel = await ctx.Client.GetChannelAsync(thread.Id);
        if (channel is DiscordThreadChannel threadChannel)
        {
            await threadChannel.SendMessageAsync(answer);
        }
    }
}