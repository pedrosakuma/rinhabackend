using RinhaBackend.Collections;
using RinhaBackend.Models;
using System.Collections.Concurrent;
using System.Text;

namespace RinhaBackend.Repositories
{
    public sealed class PessoasCacheRepository
    {
        private readonly ConcurrentDictionary<Guid, TaskCompletionSource<byte[]>> requests;
        private readonly ConcurrentDictionary<Guid, byte[]> cacheById;
        private readonly ConcurrentDictionary<string, bool> cacheByApelido;
        private readonly AppendOnlyTextSearch<byte[]> search;

        //private PatriciaSuffixTrie<Pessoa> patriciaSuffixTrie;
        public PessoasCacheRepository()
        {
            requests = new ConcurrentDictionary<Guid, TaskCompletionSource<byte[]>>(16, 131072);
            cacheById = new ConcurrentDictionary<Guid, byte[]>(4, 131072);
            cacheByApelido = new ConcurrentDictionary<string, bool>(4, 131072);
            search = new AppendOnlyTextSearch<byte[]>(131072);
        }

        internal void Add(Pessoa pessoa, byte[] rawData)
        {
            cacheById.TryAdd(pessoa.Id, rawData);
            cacheByApelido.TryAdd(pessoa.Apelido, true);
            if (requests.TryRemove(pessoa.Id, out TaskCompletionSource<byte[]>? completion))
                completion.SetResult(rawData);
        }
        internal void AddSearch(Pessoa pessoa, byte[] serialized)
        {
            var length = pessoa.Apelido.Length + pessoa.Nome.Length + pessoa.Stack.Sum(s => s.Length) + 2 + pessoa.Stack.Length - 1;
            byte[] s = new byte[length];

            int index = 0;
            Encoding.ASCII.GetBytes(pessoa.Apelido.ToLower(), s.AsSpan(index));
            index += pessoa.Apelido.Length + 1;

            Encoding.ASCII.GetBytes(pessoa.Nome.ToLower(), s.AsSpan(index));
            index += pessoa.Nome.Length + 1;

            foreach (var stack in pessoa.Stack)
            {
                Encoding.ASCII.GetBytes(stack.ToLower(), s.AsSpan(index));
                index += stack.Length + 1;
            }
            search.Add(s, serialized);
        }

        internal bool Exists(string apelido)
        {
            return cacheByApelido.ContainsKey(apelido);
        }

        internal async Task<byte[]?> GetValueAsync(Guid id, TimeSpan timeSpan)
        {
            TaskCompletionSource<byte[]> completion = requests.GetOrAdd(id,
                k => new TaskCompletionSource<byte[]>());
            if (cacheById.TryGetValue(id, out byte[]? pessoaJson))
            {
                requests.TryRemove(id, out _);
            }
            else
            {
                try
                {
                    pessoaJson = await completion.Task.WaitAsync(timeSpan);
                }
                catch (TimeoutException) { }
            }
            return pessoaJson;
        }

        internal int Search(string criteria, Span<byte[]> result)
        {
            return search.Search(criteria.ToLower(), result);
        }
    }
}
