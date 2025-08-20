using System;
using DiscordMaINBot;
using DiscordMaINBot.Interfaces;
using Microsoft.Extensions.DependencyInjection;


var serviceCollection = new ServiceCollection();
var configuration = serviceCollection.RegisterConfiguration();
serviceCollection.AddServices(configuration);
serviceCollection.AddMaIn(configuration);

var serviceProvider = serviceCollection.BuildServiceProvider();

var myService = serviceProvider.GetRequiredService<IDiscordService>();

await myService.StartAsync(serviceProvider);

Console.ReadLine();