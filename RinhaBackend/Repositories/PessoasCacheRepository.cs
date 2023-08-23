using Gma.DataStructures.StringSearch;
using RinhaBackend.Models;
using System.Collections.Concurrent;
using System.Text;

namespace RinhaBackend.Repositories
{
    public class PessoasCacheRepository
    {
        private ConcurrentDictionary<Guid, byte[]> cacheById;
        private ConcurrentDictionary<string, bool> cacheByApelido;
        private PatriciaSuffixTrie<Pessoa> patriciaSuffixTrie;
        public PessoasCacheRepository()
        {
            cacheById = new ConcurrentDictionary<Guid, byte[]>(100, 1048576);
            cacheByApelido = new ConcurrentDictionary<string, bool>(100, 1048576);
            patriciaSuffixTrie = new PatriciaSuffixTrie<Pessoa>(3);
        }

        internal void Add(Pessoa pessoa, string pessoaRaw)
        {
            cacheById.TryAdd(pessoa.Id, Encoding.UTF8.GetBytes(pessoaRaw));
            cacheByApelido.TryAdd(pessoa.Apelido, true);
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

        internal bool TryGetValue(Guid id, out byte[] pessoaJson)
        {
            return cacheById.TryGetValue(id, out pessoaJson);
        }

        internal int Count()
        {
            return cacheById.Count;
        }
        internal Pessoa[] Search(string criteria)
        {
            lock (patriciaSuffixTrie)
            {
                return patriciaSuffixTrie.Retrieve(criteria.ToLower()).ToArray();
            }
        }
    }
}
