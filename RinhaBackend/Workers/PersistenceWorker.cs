using MessagePack;
using Npgsql;
using NpgsqlTypes;
using RinhaBackend.Models;
using RinhaBackend.Services;
using System.Data;
using System.Threading.Channels;

namespace RinhaBackend.Workers
{
    public class PersistenceWorker : BackgroundService
    {
        private const string CommandText = """
            insert into pessoas
            (id, apelido, nome, nascimento, stack)
            values ($1, $2, $3, $4, $5)
            on conflict do nothing;
        """;
        private readonly string? appName;
        private readonly ILogger<PersistenceWorker> logger;
        private readonly NpgsqlConnection pgConnection;
        private readonly Channel<Pessoa> channel;
        private readonly GrpcPessoasService service;

        public PersistenceWorker(ILogger<PersistenceWorker> logger, NpgsqlConnection pgConnection,
            PersistencePessoasChannel channel,
            GrpcPessoasService service,
            IConfiguration configuration)
        {
            this.appName = configuration["APP_NAME"];
            this.logger = logger;
            this.pgConnection = pgConnection;
            this.channel = channel.Channel;
            this.service = service;
        }
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var reader = channel.Reader;
            List<Pessoa> pessoas = new List<Pessoa>(1024);
            while (!stoppingToken.IsCancellationRequested)
            {
                while (pgConnection.State != ConnectionState.Open)
                {
                    try
                    {
                        await pgConnection.OpenAsync();
                        var command = pgConnection.CreateCommand();
                        command.CommandText = """
                            CREATE TEMP TABLE pessoas_temp(
                             id UUID PRIMARY KEY NOT NULL,
                             apelido VARCHAR(50) UNIQUE NOT NULL,
                             nome VARCHAR(300) NOT NULL,
                             nascimento DATE NOT NULL,
                             stack TEXT[] NOT NULL
                            );
                            """;
                        command.ExecuteNonQuery();
                    }
                    catch
                    {
                        await Task.Delay(1000);
                    }
                }

                if (await reader.WaitToReadAsync(stoppingToken))
                {
                    while (reader.TryRead(out var pessoa))
                    {
                        await service.BroadcastAsync(pessoa);
                        pessoas.Add(pessoa);
                    }
                    try
                    {
                        using (var writer = pgConnection.BeginBinaryImport(
                            "COPY pessoas_temp (id, apelido, nome, nascimento, stack) FROM STDIN (FORMAT BINARY)"
                        ))
                        {
                            foreach (var pessoa in pessoas)
                            {
                                await writer.StartRowAsync();
                                await writer.WriteAsync(pessoa.Id, NpgsqlDbType.Uuid);
                                await writer.WriteAsync(pessoa.Apelido, NpgsqlDbType.Text);
                                await writer.WriteAsync(pessoa.Nome, NpgsqlDbType.Text);
                                await writer.WriteAsync(pessoa.Nascimento, NpgsqlDbType.Date);
                                await writer.WriteAsync(pessoa.Stack, NpgsqlDbType.Array | NpgsqlDbType.Text);
                            }
                            await writer.CompleteAsync();
                        }
                        var mergeAndTruncateTempCommand = pgConnection.CreateCommand();
                        mergeAndTruncateTempCommand.CommandText = """
                        insert into public.pessoas
                        select * from pessoas_temp
                        on conflict do nothing;
                        truncate pessoas_temp;
                        """;
                        await mergeAndTruncateTempCommand.ExecuteNonQueryAsync();
                    }
                    catch(Exception e)
                    { 
                        logger.LogError("Error persisting {exception} ", e);
                    }
                    pessoas.Clear();
                }
            }
        }
    }
}
