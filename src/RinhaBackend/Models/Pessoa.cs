namespace RinhaBackend.Models
{
    public record Pessoa(Guid Id, string Apelido, string Nome, DateOnly Nascimento, string[] Stack);
}
