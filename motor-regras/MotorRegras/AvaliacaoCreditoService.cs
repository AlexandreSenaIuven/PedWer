namespace MotorRegras;

public enum StatusCredito { Aprovado, BloqueadoAtraso, BloqueadoLimiteZerado, BloqueadoLimiteExcedido }

public sealed record TituloAberto(decimal Valor, DateOnly DataVencimento);

public sealed record ResultadoAvaliacaoCredito(StatusCredito Status, decimal SaldoDevedorGrupo, decimal LimiteMatriz);

/// <summary>
/// RF-190 a RF-203: as três travas do crédito, numa única função chamada nos
/// mesmos termos no lançamento e na baixa (RF-203) — hoje existem três
/// implementações divergentes (checagem invertida no cabeçalho — RF-140,
/// checagem da matriz na baixa, checagem do registro corrente na condição de
/// pagamento — RF-195). Aqui a decisão é sempre a mesma, dado o mesmo dado de
/// entrada.
/// </summary>
public static class AvaliacaoCreditoService
{
    public static ResultadoAvaliacaoCredito Avaliar(
        IReadOnlyCollection<TituloAberto> titulosAbertosDoGrupo,
        decimal limiteMatriz,
        int diasToleranciaAtraso,
        decimal valorPedidoAtual,
        DateOnly dataReferencia)
    {
        // RF-192/RF-193: título vencido há mais que a tolerância bloqueia,
        // independentemente de valor.
        var emAtraso = titulosAbertosDoGrupo.Any(t =>
            t.DataVencimento.AddDays(diasToleranciaAtraso) < dataReferencia);
        if (emAtraso)
        {
            return new ResultadoAvaliacaoCredito(StatusCredito.BloqueadoAtraso, 0m, limiteMatriz);
        }

        // RF-191 (T2): limite zerado bloqueia, mesmo sem saldo devedor.
        if (limiteMatriz <= 0)
        {
            return new ResultadoAvaliacaoCredito(StatusCredito.BloqueadoLimiteZerado, 0m, limiteMatriz);
        }

        var saldoDevedorGrupo = titulosAbertosDoGrupo.Sum(t => t.Valor);

        // RF-191 (T3): saldo devedor do grupo + este pedido não pode exceder o limite.
        if (saldoDevedorGrupo + valorPedidoAtual > limiteMatriz)
        {
            return new ResultadoAvaliacaoCredito(StatusCredito.BloqueadoLimiteExcedido, saldoDevedorGrupo, limiteMatriz);
        }

        return new ResultadoAvaliacaoCredito(StatusCredito.Aprovado, saldoDevedorGrupo, limiteMatriz);
    }
}
