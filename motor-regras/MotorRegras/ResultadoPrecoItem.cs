namespace MotorRegras;

public enum OrigemPreco
{
    TabelaSemDesconto,
    TabelaComDesconto,
    Negociado,
}

public sealed record ResultadoPrecoItem(
    decimal PrecoTabelaAjustado,
    decimal PrecoFinal,
    decimal PercentualDesconto,
    OrigemPreco Origem);

/// <summary>
/// Erro de negócio da precificação — sempre com um código estável para o
/// chamador decidir a mensagem ao operador (catálogo V01-V87).
/// </summary>
public sealed class PrecificacaoException(string codigo, string message) : Exception(message)
{
    public string Codigo { get; } = codigo;
}
