using RinhaBackend.Models;
using RinhaBackend.Repositories;
using RinhaBackend.Services;
using System.Diagnostics;
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
            await repository.ConnectAndPrepareAsync();
            TimeSpan interval = TimeSpan.FromSeconds(1);
            long timestamp = Stopwatch.GetTimestamp();
            while (!stoppingToken.IsCancellationRequested)
            {
                if (await reader.WaitToReadAsync(stoppingToken))
                {

                    while (reader.TryRead(out var pessoa))
                    {
                        pessoas.Add(pessoa);
                        await service.BroadcastAsync(pessoa);
                    }
                    if (Stopwatch.GetElapsedTime(timestamp) > interval
                        || pessoas.Count > 128)
                    {
                        await repository.BulkInsertAsync(pessoas);
                        pessoas.Clear();
                        timestamp = Stopwatch.GetTimestamp();
                    }
                }
            }
        }
    }
}
