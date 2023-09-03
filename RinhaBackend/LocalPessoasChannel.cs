using RinhaBackend.Models;
using System.Threading.Channels;

namespace RinhaBackend
{
    public class LocalPessoasChannel
    {
        public Channel<Pessoa> Channel { get;}

        public LocalPessoasChannel(Channel<Pessoa> channel)
        {
            this.Channel = channel;
        }
    }
}