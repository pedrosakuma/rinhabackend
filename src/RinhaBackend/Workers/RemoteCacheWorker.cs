using RinhaBackend.Grpc;
using RinhaBackend.Models;
using System.Threading.Channels;

namespace RinhaBackend.Workers
{
    public class RemoteCacheWorker : BackgroundService
    {
        private readonly ILogger<RemoteCacheWorker> logger;
        private readonly LocalPessoasChannel channel;
        private readonly Pessoas.PessoasClient pessoasClient;
        private readonly string? appName;

        public RemoteCacheWorker(ILogger<RemoteCacheWorker> logger, LocalPessoasChannel channel, Pessoas.PessoasClient pessoasClient, IConfiguration configuration)
        {
            this.logger = logger;
            this.channel = channel;
            this.pessoasClient = pessoasClient;
            this.appName = configuration["APP_NAME"];
        }
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var cacheWriter = channel.Channel.Writer;
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    var streaming = pessoasClient.ReceivedPessoa(new PessoaStreamRequest { Source = appName }, cancellationToken: stoppingToken);
                    var responseStream = streaming.ResponseStream;
                    logger.LogWarning("Connected to GRPC pair");
                    while (await responseStream.MoveNext(stoppingToken))
                    {
                        var current = responseStream.Current;
                        Guid.TryParse(current.Id, out Guid id);
                        await cacheWriter.WriteAsync(new Pessoa(
                            id,
                            current.Apelido,
                            current.Nome,
                            DateOnly.FromDateTime(current.Nascimento.ToDateTime()),
                            current.Stack.ToArray()));
                    }
                }
                catch (Exception e)
                {
                    logger.LogWarning("Connecting to GRPC pair");
                    await Task.Delay(1000);
                }
            }
        }
    }
}
