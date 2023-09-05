using System.Threading.Channels;

namespace RinhaBackend
{
    public class BaseChannel<T>
    {
        public Channel<T> Channel { get; }

        public BaseChannel()
        {
            this.Channel = System.Threading.Channels.Channel.CreateUnbounded<T>(new UnboundedChannelOptions
            {
                SingleReader = true
            });
        }
    }
}