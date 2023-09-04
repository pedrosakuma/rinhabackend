using Npgsql;
using NpgsqlTypes;
using RinhaBackend.Models;
using RinhaBackend.Workers;
using System.Data;

namespace RinhaBackend.Repositories
{
    public class PessoasPersistedRepository
    {
        private readonly ILogger<PersistenceWorker> logger;
        private readonly NpgsqlConnection pgConnection;

        public PessoasPersistedRepository(ILogger<PersistenceWorker> logger,
            NpgsqlConnection pgConnection)
        {
            this.logger = logger;
            this.pgConnection = pgConnection;
        }
        public async Task<int?> CountAsync()
        {
            var command = pgConnection.CreateCommand();
            command.CommandText = """
                SELECT COUNT(1) FROM public.pessoas;
                """;
            return (int?)await command.ExecuteScalarAsync();
        }
        public async Task ConnectAndPrepareAsync()
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
        }

        public async Task BulkInsertAsync(List<Pessoa> pessoas)
        {
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
            catch (Exception e)
            {
                logger.LogError("Error persisting {exception} ", e);
            }
        }
    }
}
