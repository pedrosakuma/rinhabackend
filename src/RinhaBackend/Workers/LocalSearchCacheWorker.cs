using RinhaBackend.Models;
using RinhaBackend.Repositories;
using System.Threading.Channels;

namespace RinhaBackend.Workers
{
    public class LocalSearchCacheWorker : BackgroundService
    {
        private readonly Channel<Pessoa> channel;
        private readonly PessoasCacheRepository pessoasCacheRepository;

        public LocalSearchCacheWorker(LocalSearchPessoasChannel channel, PessoasCacheRepository pessoasCacheRepository)
        {
            this.channel = channel.Channel;
            this.pessoasCacheRepository = pessoasCacheRepository;
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
                        pessoasCacheRepository.AddSearch(pessoa);
                }
            }
        }
    }
}
