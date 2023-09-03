using RinhaBackend.Models;
using System.Threading.Channels;

namespace RinhaBackend
{
    public class LocalSearchPessoasChannel
    {
        public Channel<Pessoa> Channel { get;}

        public LocalSearchPessoasChannel(Channel<Pessoa> channel)
        {
            this.Channel = channel;
        }
    }
}