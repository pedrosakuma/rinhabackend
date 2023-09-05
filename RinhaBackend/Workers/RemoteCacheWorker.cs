using RinhaBackend.Grpc;
using RinhaBackend.Models;
using System.Threading.Channels;

namespace RinhaBackend.Workers
{
    public class RemoteCacheWorker : BackgroundService
    {
        private readonly Channel<Pessoa> channel;
        private readonly ILogger<RemoteCacheWorker> logger;
        private readonly Pessoas.PessoasClient pessoasClient;
        private readonly string appName;

        public RemoteCacheWorker(ILogger<RemoteCacheWorker> logger, LocalPessoasChannel channel, Pessoas.PessoasClient pessoasClient, IConfiguration configuration)
        {
            this.channel = channel.Channel;
            this.logger = logger;
            this.pessoasClient = pessoasClient;
            this.appName = configuration["APP_NAME"];
        }
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var writer = channel.Writer;
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    var streaming = pessoasClient.ReceivedPessoa(new PessoaStreamRequest { Source = appName }, cancellationToken: stoppingToken);
                    var responseStream = streaming.ResponseStream;
                    while (await responseStream.MoveNext(stoppingToken))
                    {
                        var current = responseStream.Current;
                        Guid.TryParse(current.Id, out Guid id);
                        await writer.WriteAsync(new Pessoa(
                            id,
                            current.Apelido,
                            current.Nome,
                            DateOnly.FromDateTime(current.Nascimento.ToDateTime()),
                            current.Stack.ToArray()));
                    }
                }
                catch (Exception e)
                {
                    logger.LogError("Exception on receive {e}", e);
                    await Task.Delay(1000);
                }
            }
        }
    }
}
