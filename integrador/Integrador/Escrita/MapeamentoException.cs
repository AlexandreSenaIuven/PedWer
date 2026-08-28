namespace Integrador.Escrita;

public sealed class MapeamentoException(string codigo, string message) : Exception(message)
{
    public string Codigo { get; } = codigo;
}
