using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Microsoft.Extensions.ObjectPool;
using RinhaBackend.Grpc;
using RinhaBackend.Models;

namespace RinhaBackend.Services
{
    public sealed class GrpcPessoasService : Grpc.Pessoas.PessoasBase
    {
        private readonly Dictionary<IServerStreamWriter<PessoaStreamResponse>, TaskCompletionSource> contexts;
        private readonly ObjectPool<PessoaStreamResponse> pool;
        public GrpcPessoasService()
        {
            contexts = new Dictionary<IServerStreamWriter<PessoaStreamResponse>, TaskCompletionSource>(1);
            pool = ObjectPool.Create(new PessoaStreamResponseObjectPoolPolicy());
        }
        public async Task BroadcastAsync(Pessoa pessoa)
        {
            var pessoaStream = pool.Get();
            try
            {
                pessoaStream.Id = pessoa.Id.ToString();
                pessoaStream.Apelido = pessoa.Apelido;
                pessoaStream.Nome = pessoa.Nome;
                pessoaStream.Nascimento = Timestamp.FromDateTime(pessoa.Nascimento.ToDateTime(default(TimeOnly), DateTimeKind.Utc));
                pessoaStream.Stack.AddRange(pessoa.Stack);

                foreach (var item in contexts.Keys)
                    await item.WriteAsync(pessoaStream);
            }
            finally
            {
                pool.Return(pessoaStream);
            }
        }

        public void Complete()
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

        private class PessoaStreamResponseObjectPoolPolicy : IPooledObjectPolicy<PessoaStreamResponse>
        {
            public PessoaStreamResponse Create()
            {
                return new PessoaStreamResponse();
            }

            public bool Return(PessoaStreamResponse obj)
            {
                obj.Stack.Clear();
                return true;
            }
        }
    }
}
