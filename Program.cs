using System;
using DiscordMaINBot;
using DiscordMaINBot.Interfaces;
using DiscordMaINBot.Services;
using Microsoft.Extensions.DependencyInjection;

var serviceCollection = new ServiceCollection();
var configuration = serviceCollection.RegisterConfiguration();
serviceCollection.AddSingleton<RandomMessageService>();
serviceCollection.AddServices(configuration);
serviceCollection.AddOptions<BotConfig>().Bind(configuration.GetSection("BotConfig"));
serviceCollection.AddMaIn(configuration);

var serviceProvider = serviceCollection.BuildServiceProvider();

var myService = serviceProvider.GetRequiredService<IDiscordService>();

await myService.StartAsync(serviceProvider);

Console.ReadLine();