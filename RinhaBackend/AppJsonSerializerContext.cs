using RinhaBackend.Models;
using System.Text.Json.Serialization;

namespace RinhaBackend
{
    [JsonSerializable(typeof(CreateRequestPessoa))]
    [JsonSerializable(typeof(Pessoa))]
    [JsonSerializable(typeof(IEnumerable<Pessoa>))]
    [JsonSerializable(typeof(int?))]
    public partial class AppJsonSerializerContext : JsonSerializerContext
    {

    }
}