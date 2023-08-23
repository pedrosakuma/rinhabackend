using Npgsql;
using NpgsqlTypes;
using RinhaBackend.Models;
using RinhaBackend.Repositories;
using StackExchange.Redis;
using System.Text.Json;

namespace RinhaBackend.Workers
{
    public class LocalCacheWorker : BackgroundService
    {
        private readonly ConnectionMultiplexer connection;
        private readonly PessoasCacheRepository pessoasCacheRepository;

        public LocalCacheWorker(ConnectionMultiplexer connection, PessoasCacheRepository pessoasCacheRepository)
        {
            this.connection = connection;
            this.pessoasCacheRepository = pessoasCacheRepository;
        }
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var subscriber = connection.GetSubscriber();
            await subscriber.SubscribeAsync("PessoasChannel", (c, v) => {
                var pessoaRaw = (string)v;
                var pessoa = JsonSerializer.Deserialize<Pessoa>(pessoaRaw);
                pessoasCacheRepository.Add(pessoa, pessoaRaw);
            });
            TaskCompletionSource source = new TaskCompletionSource();
            stoppingToken.Register(() =>
            {
                source.SetResult();
            });
            await source.Task;
        }
    }
}
