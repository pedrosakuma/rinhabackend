using RinhaBackend.Models;
using RinhaBackend.Repositories;
using System.Text.Json;
using System.Threading.Channels;

namespace RinhaBackend.Workers
{
    public class LocalCacheWorker : BackgroundService
    {
        private readonly Channel<Pessoa> channel;
        private readonly PessoasCacheRepository pessoasCacheRepository;
        private readonly AppJsonSerializerContext jsonSerializerContext;

        public LocalCacheWorker(LocalPessoasChannel channel, PessoasCacheRepository pessoasCacheRepository, AppJsonSerializerContext jsonSerializerContext)
        {
            this.channel = channel.Channel;
            this.pessoasCacheRepository = pessoasCacheRepository;
            this.jsonSerializerContext = jsonSerializerContext;
        }
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var reader = channel.Reader;
            while (!stoppingToken.IsCancellationRequested)
            {
                if (await reader.WaitToReadAsync(stoppingToken))
                {
                    var pessoa = await reader.ReadAsync(stoppingToken);
                    if (pessoa != null)
                        pessoasCacheRepository.Add(pessoa,
                            JsonSerializer.SerializeToUtf8Bytes(pessoa, jsonSerializerContext.Pessoa));
                }
            }
        }
    }
}
