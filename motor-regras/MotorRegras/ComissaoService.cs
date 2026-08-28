namespace MotorRegras;

public sealed class ComissaoException(string codigo, string message) : Exception(message)
{
    public string Codigo { get; } = codigo;
}

/// <summary>
/// Escada desconto→comissão (RF-089 a RF-097). Decisão L2 (20/08/2026): o
/// tipo de vendedor usado aqui é o do VENDEDOR DO PEDIDO, resolvido no
/// fechamento — não o vendedor padrão do cliente na digitação do item. Quem
/// chama só faz isso depois que os dois vendedores do pedido (RF-127-133)
/// estiverem confirmados; o item entra sem faixa de comissão definitiva.
/// </summary>
public static class ComissaoService
{
    /// <summary>
    /// RF-091: mapeamento EXPLÍCITO de tipo de vendedor → escada. Hoje o
    /// código só testa `tipo_vend="V"` (10 de 193 vendedores) e usa um ELSE
    /// implícito para todo o resto (183 vendedores, 94,8%, pagando o dobro
    /// sem regra escrita para isso). Aqui a tabela é explícita e replica o
    /// efeito prático de hoje (V→1, tudo o mais→2) até que o negócio decida
    /// diferenciar R/I/branco de propósito — ver lacuna L-comissão no plano.
    /// Qualquer código fora desta tabela é rejeitado, nunca cai num "senão".
    /// </summary>
    private static readonly Dictionary<string, int> EscadaPorTipoVendedor = new(StringComparer.OrdinalIgnoreCase)
    {
        ["V"] = 1,
        ["R"] = 2,
        ["I"] = 2,
        [""] = 2,
    };

    /// <summary>
    /// Tabela viva `tabcomis` (5 campos, 10 registros, medida em 11/07/2026)
    /// — não a cópia de 2008 de 2 campos. `LimiteDesconto` é o limite
    /// superior de cada degrau.
    /// </summary>
    private static readonly Dictionary<int, IReadOnlyList<(decimal LimiteDesconto, decimal PercentualComissao)>> Escadas = new()
    {
        [1] = new[] { (0m, 5m), (10m, 3m), (20m, 2m), (30m, 2m), (99.99m, 2m) },
        [2] = new[] { (0m, 10m), (10m, 7m), (20m, 5m), (30m, 3m), (99.99m, 3m) },
    };

    public static decimal ResolverPercentualComissao(string tipoVendedorPedido, decimal percentualDescontoItem)
    {
        var chave = tipoVendedorPedido?.Trim() ?? "";
        if (!EscadaPorTipoVendedor.TryGetValue(chave, out var escadaId))
        {
            throw new ComissaoException(
                "TIPO_VENDEDOR_NAO_MAPEADO",
                $"Tipo de vendedor '{tipoVendedorPedido}' não tem escada de comissão mapeada (RF-091).");
        }

        var escada = Escadas[escadaId];

        // Decisão do usuário (20/08/2026): faixa = PISO — a última faixa cujo
        // limite é <= ao desconto do item. Substitui a divergência D7/RF-093
        // entre `totaliza` (teto) e `finaliza_item` (piso); a versão web usa
        // sempre esta regra, tanto na inclusão quanto na alteração do item.
        var candidatas = escada.Where(f => f.LimiteDesconto <= percentualDescontoItem).ToList();

        var faixa = candidatas.Count > 0
            ? candidatas.OrderByDescending(f => f.LimiteDesconto).First()
            : escada[0]; // desconto negativo (aumento de preço) cai na faixa máxima

        return faixa.PercentualComissao;
    }
}
