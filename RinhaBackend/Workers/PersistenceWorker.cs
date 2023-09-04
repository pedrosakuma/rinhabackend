using MessagePack;
using Npgsql;
using NpgsqlTypes;
using RinhaBackend.Models;
using RinhaBackend.Repositories;
using RinhaBackend.Services;
using System.Data;
using System.Threading.Channels;

namespace RinhaBackend.Workers
{
    public class PersistenceWorker : BackgroundService
    {
        private readonly PessoasPersistedRepository repository;
        private readonly Channel<Pessoa> channel;
        private readonly GrpcPessoasService service;

        public PersistenceWorker(PessoasPersistedRepository repository,
            PersistencePessoasChannel channel,
            GrpcPessoasService service)
        {
            this.repository = repository;
            this.channel = channel.Channel;
            this.service = service;
        }
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var reader = channel.Reader;
            List<Pessoa> pessoas = new List<Pessoa>(1024);
            while (!stoppingToken.IsCancellationRequested)
            {
                await repository.ConnectAndPrepareAsync();

                if (await reader.WaitToReadAsync(stoppingToken))
                {
                    while (reader.TryRead(out var pessoa))
                    {
                        await service.BroadcastAsync(pessoa);
                        pessoas.Add(pessoa);
                    }
                    await repository.BulkInsertAsync(pessoas);
                    pessoas.Clear();
                }
            }
        }
    }
}
