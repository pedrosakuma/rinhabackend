using Google.Protobuf.Collections;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using RinhaBackend.Grpc;
using RinhaBackend.Models;
using System.Collections.Concurrent;

namespace RinhaBackend.Services
{
    public class GrpcPessoasService : Grpc.Pessoas.PessoasBase
    {
        private readonly ConcurrentDictionary<IServerStreamWriter<PessoaStreamResponse>, TaskCompletionSource> contexts;
        public GrpcPessoasService()
        {
            contexts = new ConcurrentDictionary<IServerStreamWriter<PessoaStreamResponse>, TaskCompletionSource>();
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
            contexts.GetOrAdd(responseStream, completion);
            try
            {
                await completion.Task;
            }
            finally
            {
                contexts.Remove(responseStream, out _);
            }
        }
    }
}
