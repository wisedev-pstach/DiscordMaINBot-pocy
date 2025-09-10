using DiscordMaINBot.Interfaces;
using MaIN.Core.Hub;
using MaIN.Core.Hub.Contexts;
using MaIN.Core.Hub.Utils;
using MaIN.Domain.Configuration;
using MaIN.Domain.Entities;
using MaIN.Domain.Entities.Agents.AgentSource;
using Microsoft.Extensions.Options;

namespace DiscordMaINBot.Services;

public class MaInService(IOptions<BotConfig> options) : IMaInService
{
    private readonly Dictionary<string, ChatContext> Cache = [];

    public async Task<string> ConverseAsync(string channelId, string prompt, bool noCache = false)
    {
        var ctx = !noCache && Cache.TryGetValue(channelId, out var value) ? value 
            : AIHub.Chat();
        
        ctx.WithModel(options.Value.Model)
            .WithInferenceParams(new InferenceParams()
            {
                ContextSize = 4096,
                MaxTokens = 2048
            })
            .WithBackend(InferBackendType())
            .WithSystemPrompt(options.Value.SystemPrompt)
            .WithMessage(prompt);

        if (!noCache)
        {
            Cache[channelId] = ctx;
        }
        
        var result = await ctx.CompleteAsync();
        return result.Message.Content;
    }

    public async Task<string> AskQuestionAsync(string question)
    {
        var backend = InferBackendType();
        var sysPrompt = options.Value.SystemPrompt;
        var context = await AIHub.Agent()
            .WithModel(options.Value.Model)
            .WithBackend(backend)
            .WithInitialPrompt($"{sysPrompt} |Answer cannot be longer than 2000 characters.")
            .WithMemoryParams(new MemoryParams
            {
                AnswerTokens = 2137,
                ContextSize = 2048
            })
            .CreateAsync();
        
        var response = await context.ProcessAsync(question);

        return response.Message.Content;
    }

    public async Task<string> AskQuestionWithFileAsync(string question, List<string> filesPath)
    {
        var backend = InferBackendType();
        var context = await AIHub.Agent()
            .WithBackend(backend)
            .WithModel(options.Value.Model)
            .WithInitialPrompt("Try to extract information from the file provided. There can be a lot of information " +
                               "so try to extract only the most relevant parts. User may ask follow-up questions")
            .WithSource(new AgentFileSourceDetails
            {
                Files = filesPath
            }, AgentSourceType.File)
            .WithMemoryParams(new MemoryParams
            {
                AnswerTokens = 2137,
                ContextSize = 2048
            })
            .WithSteps(StepBuilder.Instance
                .FetchData()
                .Answer()
                .Build())
            .CreateAsync();
        
        var response = await context.ProcessAsync(question);

        return response.Message.Content;
    }

    public async Task<string> TranslateMessageAsync(string message, string targetLanguage)
    {
        var backend = InferBackendType();
        var prompt = $"Task: Translate the following text into the language specified by the user." +
                     $"Input Text: {message}" +
                     $"Target Language: {targetLanguage}" +
                     $"Instructions:" +
                     $"Ensure accurate and natural translation appropriate for a native speaker." +
                     $"Preserve the tone and context of the original message." +
                     $"Do not include the original text in the response—only return the translated version." +
                     $"If the target language is the same as the source language, return the text unchanged.";
        
        var context = await AIHub.Agent()
            .WithBackend(backend)
            .WithModel(options.Value.Model)
            .WithBehaviour("Translator", Behaviour.Translator)
            .WithMemoryParams(new MemoryParams
            {
                AnswerTokens = 900
            })
            .WithSteps(StepBuilder.Instance
                .FetchData()
                .Become("Translator")
                .Answer()
                .Build())
            .CreateAsync();
        
        var response = await context.ProcessAsync(prompt);

        return response.Message.Content;
    }
    
    public async Task<string> RewriteMessageAsync(string message)
    {
        var backend = InferBackendType();
        var prompt = $"Task: Rewrite the following text to be grammatically correct, clear, and natural in tone." +
                     $"Input Text: {message}" +
                     $"Instructions:" +
                     $"Correct all grammar, spelling, punctuation, and vocabulary mistakes." +
                     $"Improve sentence structure and flow where necessary." +
                     $"Maintain the original meaning and intent of the text." +
                     $"Use natural, fluent language appropriate for the context." +
                     $"Do not explain your changes—just return the improved version of the text." +
                     $"If the input is already well-written, return it unchanged." +
                     "You have to answer in the same language as the input text.";
        
        var context = await AIHub.Agent()
            .WithBackend(backend)
            .WithModel(options.Value.Model)
            .WithBehaviour("Rewriter", Behaviour.Rewriter)
            .WithMemoryParams(new MemoryParams
            {
                AnswerTokens = 2137
            })
            .WithSteps(StepBuilder.Instance
                .FetchData()
                .Become("Rewriter")
                .Answer()
                .Build())
            .CreateAsync();
        
        var response = await context.ProcessAsync(prompt);

        return response.Message.Content;
    }

    public async Task<byte[]?> GenerateImageAsync(string prompt)
    {
        if (!ImageThrottler.CanGenerate())
        {
            throw new InvalidOperationException($"Global rate limit exceeded. {ImageThrottler.GetRemaining()} requests remaining this hour.");
        }

        ImageThrottler.RecordRequest();

        var context = AIHub.Chat()
            .WithBackend(BackendType.Gemini)
            .EnableVisual()
            .WithModel("imagen-4.0-fast-generate-001")
            .WithMessage(prompt);
    
        var response = await context.CompleteAsync();
        return response.Message.Image;
    }
    private BackendType InferBackendType()
    {
        var backend = options.Value.Backend != null ? Enum.Parse<BackendType>(options.Value.Backend) : BackendType.Self;
        return backend;
    }
}