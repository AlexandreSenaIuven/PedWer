namespace MotorRegras.Tests;

public class PrecificacaoServiceTests
{
    private static readonly FatoresPrecoCliente SemFatores = new(0, 0, 0);
    private static readonly DateOnly Hoje = new(2026, 8, 20);

    [Fact]
    public void AjustarPrecoTabela_aplica_os_tres_fatores_em_sequencia_com_arredondamento_a_cada_etapa()
    {
        // RF-070: 100 * 1,215 (INSS 21,5%) = 121,50; * (1 - 0,10) = 109,35; * (1 - 0,05) = 103,8825 -> 103,88
        var fatores = new FatoresPrecoCliente(PercentualAdicao: 21.5m, CompensacaoIcms: 10m, DescontoDiretoria: 5m);

        var preco = PrecificacaoService.AjustarPrecoTabela(100m, fatores);

        Assert.Equal(103.88m, preco);
    }

    [Fact]
    public void CalcularPrecoItem_sem_negociacao_dentro_do_teto_aceita_o_preco_digitado()
    {
        // Preço de tabela ajustado = 100 (sem fatores). Teto = 15%. Desconto digitado = 10% -> aceito.
        var resultado = PrecificacaoService.CalcularPrecoItem(
            precoTabelaBase: 100m,
            fatores: SemFatores,
            negociacao: null,
            precoDigitado: 90m,
            percentualTetoDesconto: 15m,
            dataReferencia: Hoje);

        Assert.Equal(100m, resultado.PrecoTabelaAjustado);
        Assert.Equal(90m, resultado.PrecoFinal);
        Assert.Equal(10m, resultado.PercentualDesconto);
        Assert.Equal(OrigemPreco.TabelaComDesconto, resultado.Origem);
    }

    [Fact]
    public void CalcularPrecoItem_sem_negociacao_acima_do_teto_rejeita_RF086()
    {
        var ex = Assert.Throws<PrecificacaoException>(() =>
            PrecificacaoService.CalcularPrecoItem(
                precoTabelaBase: 100m,
                fatores: SemFatores,
                negociacao: null,
                precoDigitado: 80m, // desconto de 20%, teto é 15%
                percentualTetoDesconto: 15m,
                dataReferencia: Hoje));

        Assert.Equal("DESCONTO_ACIMA_DO_TETO", ex.Codigo);
    }

    [Fact]
    public void CalcularPrecoItem_aumento_de_preco_zera_o_desconto_RF084_e_nao_precisa_de_teto()
    {
        var resultado = PrecificacaoService.CalcularPrecoItem(
            precoTabelaBase: 100m,
            fatores: SemFatores,
            negociacao: null,
            precoDigitado: 110m, // preço digitado maior que o de tabela
            percentualTetoDesconto: 0m, // teto zero — mesmo assim não bloqueia aumento
            dataReferencia: Hoje);

        Assert.Equal(110m, resultado.PrecoFinal);
        Assert.Equal(OrigemPreco.TabelaSemDesconto, resultado.Origem);
    }

    [Fact]
    public void CalcularPrecoItem_preco_de_tabela_ajustado_zero_rejeita_RF080_D3()
    {
        var ex = Assert.Throws<PrecificacaoException>(() =>
            PrecificacaoService.CalcularPrecoItem(
                precoTabelaBase: 0m,
                fatores: SemFatores,
                negociacao: null,
                precoDigitado: 50m,
                percentualTetoDesconto: 10m,
                dataReferencia: Hoje));

        Assert.Equal("PRECO_TABELA_ZERO", ex.Codigo);
    }

    [Fact]
    public void CalcularPrecoItem_preco_final_zero_rejeita_RF087()
    {
        var ex = Assert.Throws<PrecificacaoException>(() =>
            PrecificacaoService.CalcularPrecoItem(
                precoTabelaBase: 100m,
                fatores: SemFatores,
                negociacao: null,
                precoDigitado: 0m,
                percentualTetoDesconto: 100m, // teto alto o suficiente para não ser a causa da rejeição
                dataReferencia: Hoje));

        Assert.Equal("PRECO_FINAL_ZERO", ex.Codigo);
    }

    [Fact]
    public void CalcularPrecoItem_com_negociacao_vigente_aplica_os_mesmos_3_fatores_no_preco_negociado_L1()
    {
        // Decisão L1: preço negociado é preço-BASE, recebe os mesmos fatores do preço de tabela.
        // Corrige D1 — antes, o preço negociado cru era comparado contra o preço de tabela ajustado.
        var fatores = new FatoresPrecoCliente(PercentualAdicao: 20m, CompensacaoIcms: 0m, DescontoDiretoria: 0m);
        var negociacao = new NegociacaoVigente(PrecoNegociado: 50m, DataValidade: Hoje, Autorizada: true);

        var resultado = PrecificacaoService.CalcularPrecoItem(
            precoTabelaBase: 100m,
            fatores: fatores,
            negociacao: negociacao,
            precoDigitado: null,
            percentualTetoDesconto: 0m,
            dataReferencia: Hoje);

        // Tabela: 100 * 1,20 = 120. Negociado: 50 * 1,20 = 60. Desconto = (120-60)/120*100 = 50%.
        Assert.Equal(120m, resultado.PrecoTabelaAjustado);
        Assert.Equal(60m, resultado.PrecoFinal);
        Assert.Equal(50m, resultado.PercentualDesconto);
        Assert.Equal(OrigemPreco.Negociado, resultado.Origem);
    }

    [Fact]
    public void CalcularPrecoItem_negociacao_expirada_e_ignorada_e_cai_no_fluxo_manual()
    {
        var negociacaoExpirada = new NegociacaoVigente(PrecoNegociado: 10m, DataValidade: Hoje.AddDays(-1), Autorizada: true);

        var resultado = PrecificacaoService.CalcularPrecoItem(
            precoTabelaBase: 100m,
            fatores: SemFatores,
            negociacao: negociacaoExpirada,
            precoDigitado: 100m,
            percentualTetoDesconto: 0m,
            dataReferencia: Hoje);

        Assert.Equal(OrigemPreco.TabelaSemDesconto, resultado.Origem);
    }

    [Fact]
    public void CalcularPrecoItem_negociacao_nao_autorizada_e_ignorada()
    {
        var negociacaoNaoAutorizada = new NegociacaoVigente(PrecoNegociado: 10m, DataValidade: Hoje, Autorizada: false);

        var resultado = PrecificacaoService.CalcularPrecoItem(
            precoTabelaBase: 100m,
            fatores: SemFatores,
            negociacao: negociacaoNaoAutorizada,
            precoDigitado: 100m,
            percentualTetoDesconto: 0m,
            dataReferencia: Hoje);

        Assert.Equal(OrigemPreco.TabelaSemDesconto, resultado.Origem);
    }
}
