using RinhaBackend.Models;
using System.Threading.Channels;

namespace RinhaBackend
{
    public class PersistencePessoasChannel
    {
        public Channel<Pessoa> Channel { get; }

        public PersistencePessoasChannel(Channel<Pessoa> channel)
        {
            this.Channel = channel;
        }
    }
}