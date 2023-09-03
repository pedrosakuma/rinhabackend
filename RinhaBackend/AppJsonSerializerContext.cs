using RinhaBackend.Models;
using System.Text.Json.Serialization;
using System.Threading.Channels;

namespace RinhaBackend
{
    [JsonSerializable(typeof(CreateRequestPessoa))]
    [JsonSerializable(typeof(Pessoa))]
    [JsonSerializable(typeof(Pessoa[]))]
    [JsonSerializable(typeof(Channel<Pessoa>))]
    [JsonSerializable(typeof(int))]
    public partial class AppJsonSerializerContext : JsonSerializerContext
    {

    }
}