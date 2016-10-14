using Discord;
using Discord.WebSocket;
using Discord.Net.WebSockets;
using Discord.Addons.WS4NetCompatibility;
using System.Threading.Tasks;
using System;

namespace ExampleProject
{
    public class Program
    {
        public static void Main(string[] args) =>
            new Program().Run().GetAwaiter().GetResult();

        public async Task Run()
        {
            var client = new DiscordSocketClient(new DiscordSocketConfig
            {
                WebSocketProvider = new WebSocketProvider(() => new WS4NetProvider()),
                LogLevel = LogSeverity.Debug
            });

            var token = Environment.GetEnvironmentVariable("discord-foxboat-token");
            client.Log += msg =>
            {
                Console.WriteLine(msg.ToString());
                return Task.CompletedTask;
            };

            await client.LoginAsync(TokenType.Bot, token);
            await client.ConnectAsync();

            await Task.Delay(-1);
        }
    }
}
