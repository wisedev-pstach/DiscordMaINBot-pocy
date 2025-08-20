using System.Collections.Generic;
using System.Threading.Tasks;

namespace DiscordMaINBot.Interfaces;

public interface IMaInService
{
    Task<string> AskQuestionAsync(string question);
    Task<string> AskQuestionWithFileAsync(string question, List<string> filesPath);
    Task<string> TranslateMessageAsync(string message, string targetLanguage);
    Task<string> RewriteMessageAsync(string message);
    Task<byte[]?> GenerateImageAsync(string prompt);
}