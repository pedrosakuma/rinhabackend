using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using RinhaBackend.Grpc;
using RinhaBackend.Models;

namespace RinhaBackend.Services
{
    public sealed class GrpcPessoasService : Grpc.Pessoas.PessoasBase
    {
        private readonly Dictionary<IServerStreamWriter<PessoaStreamResponse>, TaskCompletionSource> contexts;
        public GrpcPessoasService()
        {
            contexts = new Dictionary<IServerStreamWriter<PessoaStreamResponse>, TaskCompletionSource>(1);
        }
        public async Task BroadcastAsync(Pessoa pessoa)
        {
            var pessoaStream = new PessoaStreamResponse
            {
                Id = pessoa.Id.ToString(),
                Apelido = pessoa.Apelido,
                Nome = pessoa.Nome,
                Nascimento = Timestamp.FromDateTime(pessoa.Nascimento.ToDateTime(default(TimeOnly), DateTimeKind.Utc)),
            };
            pessoaStream.Stack.AddRange(pessoa.Stack);
            foreach (var item in contexts.Keys)
            {
                await item.WriteAsync(pessoaStream);
            }
        }
        public async void Complete()
        {
            foreach (var completion in contexts.Values)
                completion.SetResult();
        }
        public override async Task ReceivedPessoa(PessoaStreamRequest request, IServerStreamWriter<PessoaStreamResponse> responseStream, ServerCallContext context)
        {
            var completion = new TaskCompletionSource();
            context.CancellationToken.Register(() => completion.SetCanceled());
            lock (contexts)
            {
                contexts.Add(responseStream, completion);
            }
            try
            {
                await completion.Task;
            }
            finally
            {
                lock (contexts)
                    contexts.Remove(responseStream, out _);
            }
        }
    }
}
