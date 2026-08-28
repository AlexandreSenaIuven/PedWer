namespace MotorRegras;

/// <summary>
/// RF-210: fórmula de arredondamento do total do item, medida em produção
/// (`wareascp.IND_ARREND="S"`, ativa nesta instalação):
///
///     ROUND( INT( ((qtd*preco)*100)*10 ) / 1000, 2 )
///
/// Reproduzida LITERALMENTE — truncar a 3 casas e só então arredondar a 2 —
/// e não por uma equivalente algébrica (arredondar direto a 2 casas), porque
/// as duas formas divergem em valores na borda do centavo (ver testes).
/// </summary>
public static class CalculadoraTotalItem
{
    public static decimal CalcularTotalItem(decimal quantidade, decimal precoUnitario)
    {
        var bruto = quantidade * precoUnitario;
        var truncadoATresCasas = Math.Truncate(bruto * 1000m) / 1000m;
        return Math.Round(truncadoATresCasas, 2, MidpointRounding.AwayFromZero);
    }
}
