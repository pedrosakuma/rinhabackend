using RinhaBackend.Models;
using System.Collections.Concurrent;
using TrieNet.PatriciaTrie;

namespace RinhaBackend.Repositories
{
    public sealed class PessoasCacheRepository
    {
        private ConcurrentDictionary<Guid, TaskCompletionSource<byte[]>> requests;
        private ConcurrentDictionary<Guid, byte[]> cacheById;
        private ConcurrentDictionary<string, bool> cacheByApelido;
        private PatriciaSuffixTrie<Pessoa> patriciaSuffixTrie;
        public PessoasCacheRepository()
        {
            requests = new ConcurrentDictionary<Guid, TaskCompletionSource<byte[]>>(16, 1048576);
            cacheById = new ConcurrentDictionary<Guid, byte[]>(4, 1048576);
            cacheByApelido = new ConcurrentDictionary<string, bool>(4, 1048576);
            patriciaSuffixTrie = new PatriciaSuffixTrie<Pessoa>(1);
        }

        internal void Add(Pessoa pessoa, byte[] rawData)
        {
            cacheById.TryAdd(pessoa.Id, rawData);
            cacheByApelido.TryAdd(pessoa.Apelido, true);
            if (requests.TryRemove(pessoa.Id, out TaskCompletionSource<byte[]>? completion))
                completion.SetResult(rawData);
        }
        internal void AddSearch(Pessoa pessoa)
        {
            lock (patriciaSuffixTrie)
            {
                patriciaSuffixTrie.Add(pessoa.Apelido.ToLower(), pessoa);
                patriciaSuffixTrie.Add(pessoa.Nome.ToLower(), pessoa);
                foreach (var s in pessoa.Stack)
                {
                    patriciaSuffixTrie.Add(s.ToLower(), pessoa);
                }
            }
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
                catch (TimeoutException)
                {
                    cacheById.TryGetValue(id, out pessoaJson);
                }
            }
            return pessoaJson;
        }

        internal IEnumerable<Pessoa> Search(string criteria)
        {
            lock(patriciaSuffixTrie)
                return patriciaSuffixTrie.Retrieve(criteria.ToLower()).Take(50).ToArray();
        }
    }
}

