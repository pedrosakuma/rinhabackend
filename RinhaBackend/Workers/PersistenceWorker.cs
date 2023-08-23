using Npgsql;
using NpgsqlTypes;
using RinhaBackend.Models;
using StackExchange.Redis;
using StackExchange.Redis.KeyspaceIsolation;
using System.Text.Json;

namespace RinhaBackend.Workers
{
    public class PersistenceWorker : BackgroundService
    {
        private readonly string? appName;
        private readonly ConnectionMultiplexer connection;
        private readonly NpgsqlConnection pgConnection;

        public PersistenceWorker(ConnectionMultiplexer connection, NpgsqlConnection pgConnection, IConfiguration configuration)
        {
            this.appName = configuration["APP_NAME"];
            this.connection = connection;
            this.pgConnection = pgConnection;
        }
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var database = connection.GetDatabase();
            try
            {
                await database.StreamCreateConsumerGroupAsync("PessoasStream", "ApiPersistence", StreamPosition.NewMessages);
            }
            catch(Exception e)
            {
                Console.WriteLine("Consumer group already exists");
            }
            bool connectionOpened = false;

            while (!connectionOpened)
            {
                try
                {
                    await pgConnection.OpenAsync();
                    connectionOpened = true;
                }
                catch
                {
                    await Task.Delay(1000);
                }
            }
            int zeroMessages = 0;
            while (!stoppingToken.IsCancellationRequested)
            {
                var streamEntries = await database.StreamReadGroupAsync("PessoasStream", "ApiPersistence", appName, StreamPosition.NewMessages, 10);
                if (streamEntries.Length == 0)
                    zeroMessages++;
                else
                {
                    using (var batch = pgConnection.CreateBatch())
                    {
                        using (var writer = pgConnection.BeginBinaryImport(
                            "COPY pessoas (id, apelido, nome, nascimento, stack) FROM STDIN (FORMAT BINARY)"
                        ))
                        {
                            foreach (var streamEntry in streamEntries)
                            {
                                byte[] rawData = streamEntry.Values[0].Value!;
                        
                                var pessoa = JsonSerializer.Deserialize<Pessoa>(rawData)!;
                                await writer.StartRowAsync();
                                await writer.WriteAsync(pessoa.Id, NpgsqlDbType.Uuid);
                                await writer.WriteAsync(pessoa.Apelido, NpgsqlDbType.Text);
                                await writer.WriteAsync(pessoa.Nome, NpgsqlDbType.Text);
                                await writer.WriteAsync(pessoa.Nascimento, NpgsqlDbType.Date);
                                await writer.WriteAsync(pessoa.Stack, NpgsqlDbType.Array | NpgsqlDbType.Text);
                            }
                            await writer.CompleteAsync();
                        }
                    }
                    zeroMessages = 0;
                }
                await Task.Delay(zeroMessages * 100);
            }
        }
    }
}
