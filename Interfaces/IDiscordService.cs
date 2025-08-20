using System;
using System.Threading;
using System.Threading.Tasks;


namespace DiscordMaINBot.Interfaces;

public interface IDiscordService
{
    Task StartAsync(IServiceProvider serviceProvider, CancellationToken cancellationToken = default);
}