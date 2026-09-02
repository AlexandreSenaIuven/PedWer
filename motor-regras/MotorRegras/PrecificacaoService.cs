namespace MotorRegras;

/// <summary>
/// Motor puro de formação de preço e desconto do item (Fase 1 do plano,
/// RF-070 a RF-088). Sem I/O: quem chama já resolveu cliente, produto,
/// negociação e teto de coluna de grade — este serviço só decide o valor.
/// </summary>
public static class PrecificacaoService
{
    /// <summary>
    /// RF-070: três ajustes multiplicativos sequenciais, cada um arredondado
    /// a 2 casas antes do próximo (a ordem importa — não é comutativo).
    /// </summary>
    public static decimal AjustarPrecoTabela(decimal precoBase, FatoresPrecoCliente fatores)
    {
        var preco = precoBase;
        preco = Arredondar(preco * (1 + fatores.PercentualAdicao / 100m));
        preco = Arredondar(preco * (1 - fatores.CompensacaoIcms / 100m));
        preco = Arredondar(preco * (1 - fatores.DescontoDiretoria / 100m));
        return preco;
    }

    /// <summary>
    /// Decide o preço final do item e o % de desconto resultante.
    ///
    /// - Com negociação vigente: decisão L1 (20/08/2026) — o preço negociado
    ///   é tratado como preço-BASE, recebendo os mesmos 3 fatores do cliente
    ///   antes de fechar o item. Corrige D1/RF-079 (antes, o preço negociado
    ///   cru era comparado contra o preço de tabela já ajustado).
    /// - Sem negociação: preço digitado, sujeito ao teto aditivo
    ///   (RF-083/084); acima do teto é rejeitado e quem chama restaura o
    ///   preço de tabela (RF-086).
    /// - Preço de tabela ajustado igual a zero é rejeitado explicitamente
    ///   (RF-080/D3 — hoje division by zero silenciosa produz comissão errada
    ///   sem aviso).
    /// - Preço final igual a zero é rejeitado (RF-087/088).
    /// </summary>
    public static ResultadoPrecoItem CalcularPrecoItem(
        decimal precoTabelaBase,
        FatoresPrecoCliente fatores,
        NegociacaoVigente? negociacao,
        decimal? precoDigitado,
        decimal percentualTetoDesconto,
        DateOnly dataReferencia)
    {
        var precoTabelaAjustado = AjustarPrecoTabela(precoTabelaBase, fatores);

        if (precoTabelaAjustado <= 0)
        {
            throw new PrecificacaoException(
                "PRECO_TABELA_ZERO",
                "Preço de tabela ajustado é zero ou negativo — não é possível calcular desconto (RF-080/D3).");
        }

        decimal precoFinal;
        OrigemPreco origem;

        if (negociacao is { Autorizada: true } && negociacao.DataValidade >= dataReferencia)
        {
            var precoNegociadoAjustado = AjustarPrecoTabela(negociacao.PrecoNegociado, fatores);
            precoFinal = precoNegociadoAjustado;
            origem = OrigemPreco.Negociado;
        }
        else
        {
            if (precoDigitado is null)
            {
                throw new PrecificacaoException(
                    "PRECO_AUSENTE",
                    "Valor unitário do item não foi informado.");
            }

            // RF-084: desconto negativo (preço digitado maior que o de tabela) é zerado —
            // aumento de preço não passa pela checagem de teto de desconto.
            var descontoConcedido = Math.Max(0m, precoTabelaAjustado - precoDigitado.Value);
            var tetoValor = Arredondar(precoTabelaAjustado * percentualTetoDesconto / 100m); // RF-084, fórmula literal

            if (descontoConcedido > tetoValor)
            {
                // Comparação interna é em R$ (RF-084, fórmula literal do VFP), mas quem
                // vende pensa em percentual — a mensagem mostra os dois lados convertidos,
                // não os valores em R$ usados na conta.
                var descontoPercentualConcedido = Arredondar(descontoConcedido / precoTabelaAjustado * 100m);
                throw new PrecificacaoException(
                    "DESCONTO_ACIMA_DO_TETO",
                    $"Desconto de {descontoPercentualConcedido:F2}% excede o teto de {percentualTetoDesconto:F2}% para este cliente/produto (RF-083/086).");
            }

            precoFinal = precoDigitado.Value;
            origem = descontoConcedido > 0 ? OrigemPreco.TabelaComDesconto : OrigemPreco.TabelaSemDesconto;
        }

        if (precoFinal <= 0)
        {
            throw new PrecificacaoException(
                "PRECO_FINAL_ZERO",
                "Valor unitário do item não pode ser zero (RF-087).");
        }

        var percentualDesconto = (precoTabelaAjustado - precoFinal) / precoTabelaAjustado * 100m;

        return new ResultadoPrecoItem(precoTabelaAjustado, precoFinal, percentualDesconto, origem);
    }

    private static decimal Arredondar(decimal valor) => Math.Round(valor, 2, MidpointRounding.AwayFromZero);
}
