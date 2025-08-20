using System.Collections.Generic;
using System.Threading.Tasks;
using DiscordMaINBot.Interfaces;
using MaIN.Core.Hub;
using MaIN.Core.Hub.Utils;
using MaIN.Domain.Entities;
using MaIN.Domain.Entities.Agents.AgentSource;

namespace DiscordMaINBot.Services;

public class MaInService : IMaInService
{
    public async Task<string> AskQuestionAsync(string question)
    {
        var context = await AIHub.Agent()
            .WithModel("gemma3:4b")
            .WithInitialPrompt("Answer cannot be longer than 2000 characters.")
            .WithMemoryParams(new MemoryParams
            {
                AnswerTokens = 500
            })
            .CreateAsync();
        
        var response = await context.ProcessAsync(question);

        return response.Message.Content;
    }

    public async Task<string> AskQuestionWithFileAsync(string question, List<string> filesPath)
    {
        var context = AIHub.Agent()
            .WithModel("gemma3:4b")
            .WithInitialPrompt("Try to extract information from the file provided. There can be a lot of information " +
                               "so try to extract only the most relevant parts. User may ask follow-up questions")
            .WithSource(new AgentFileSourceDetails
            {
                Files = filesPath
            }, AgentSourceType.File)
            .WithMemoryParams(new MemoryParams
            {
                AnswerTokens = 400
            })
            .WithSteps(StepBuilder.Instance
                .FetchData()
                .Answer()
                .Build())
            .Create();
        
        var response = await context.ProcessAsync(question);

        return response.Message.Content;
    }

    public async Task<string> TranslateMessageAsync(string message, string targetLanguage)
    {
        var prompt = $"Task: Translate the following text into the language specified by the user." +
                     $"Input Text: {message}" +
                     $"Target Language: {targetLanguage}" +
                     $"Instructions:" +
                     $"Ensure accurate and natural translation appropriate for a native speaker." +
                     $"Preserve the tone and context of the original message." +
                     $"Do not include the original text in the response—only return the translated version." +
                     $"If the target language is the same as the source language, return the text unchanged.";
        
        var context = AIHub.Agent()
            .WithModel("gemma3:12b")
            .WithBehaviour("Translator", Behaviour.Translator)
            .WithMemoryParams(new MemoryParams
            {
                AnswerTokens = 600
            })
            .WithSteps(StepBuilder.Instance
                .FetchData()
                .Become("Translator")
                .Answer()
                .Build())
            .Create();
        
        var response = await context.ProcessAsync(prompt);

        return response.Message.Content;
    }
    
    public async Task<string> RewriteMessageAsync(string message)
    {
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
        
        var context = AIHub.Agent()
            .WithModel("gemma3:12b")
            .WithBehaviour("Rewriter", Behaviour.Rewriter)
            .WithMemoryParams(new MemoryParams
            {
                AnswerTokens = 600
            })
            .WithSteps(StepBuilder.Instance
                .FetchData()
                .Become("Rewriter")
                .Answer()
                .Build())
            .Create();
        
        var response = await context.ProcessAsync(prompt);

        return response.Message.Content;
    }

    public async Task<byte[]?> GenerateImageAsync(string prompt)
    {
        var context = AIHub.Chat()
            .EnableVisual()
            .WithMessage(prompt);
        
        var response = await context.CompleteAsync();

        return response.Message.Image;
    }
}