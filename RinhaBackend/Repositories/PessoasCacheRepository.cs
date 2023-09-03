using Gma.DataStructures.StringSearch;
using RinhaBackend.Models;
using System.Collections.Concurrent;

namespace RinhaBackend.Repositories
{
    public class PessoasCacheRepository
    {
        private ConcurrentDictionary<Guid, List<TaskCompletionSource<byte[]>>> requests;
        private ConcurrentDictionary<Guid, byte[]> cacheById;
        private ConcurrentDictionary<string, bool> cacheByApelido;
        private PatriciaSuffixTrie<Pessoa> patriciaSuffixTrie;
        public PessoasCacheRepository()
        {
            requests = new ConcurrentDictionary<Guid, List<TaskCompletionSource<byte[]>>>(100, 1048576);
            cacheById = new ConcurrentDictionary<Guid, byte[]>(100, 1048576);
            cacheByApelido = new ConcurrentDictionary<string, bool>(100, 1048576);
            patriciaSuffixTrie = new PatriciaSuffixTrie<Pessoa>(1);
        }

        internal void Add(Pessoa pessoa, byte[] rawData)
        {
            cacheById.TryAdd(pessoa.Id, rawData);
            cacheByApelido.TryAdd(pessoa.Apelido, true);
            if (requests.TryRemove(pessoa.Id, out List<TaskCompletionSource<byte[]>>? completions))
                foreach (var completion in completions)
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
            if (!cacheById.TryGetValue(id, out byte[]? pessoaJson))
            {
                TaskCompletionSource<byte[]> completion = new TaskCompletionSource<byte[]>();
                CancellationTokenSource source = new CancellationTokenSource();
                source.CancelAfter(timeSpan);
                requests.AddOrUpdate(id,
                    k => new List<TaskCompletionSource<byte[]>>(1) { completion },
                    (k, l) =>
                    {
                        lock (l)
                            l.Add(completion);
                        return l;
                    });
                pessoaJson = await completion.Task.WaitAsync(source.Token);
            }
            return pessoaJson;
        }

        internal int Count()
        {
            return cacheById.Count;
        }
        internal Pessoa[] Search(string criteria)
        {
            lock (patriciaSuffixTrie)
            {
                return patriciaSuffixTrie.Retrieve(criteria.ToLower()).Take(50).ToArray();
            }
        }
    }
}
