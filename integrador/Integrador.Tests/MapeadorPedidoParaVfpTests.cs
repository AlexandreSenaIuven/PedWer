using Dominio;
using Integrador.Escrita;
using MotorRegras;

namespace Integrador.Tests;

public class MapeadorPedidoParaVfpTests
{
    private static readonly DateOnly Hoje = new(2026, 8, 20);

    private static ResultadoPrecoItem PrecoSimples(decimal precoFinal) =>
        new(PrecoTabelaAjustado: 100m, PrecoFinal: precoFinal, PercentualDesconto: 100m - precoFinal, Origem: OrigemPreco.TabelaSemDesconto);

    private static Pedido PedidoFechadoComUmItem(string? referenciaExterna = "FB2651A")
    {
        var pedido = new Pedido("PED", "02", "0000184", Hoje, "asena") { ReferenciaExterna = referenciaExterna };
        pedido.AdicionarItem("61", "01005000", "A", 2, PrecoSimples(precoFinal: 95m)); // 5% desconto -> negocio=1 "V" paga 5%
        pedido.Fechar("V001", "V002", "V");
        return pedido;
    }

    [Fact]
    public void Mapear_pedido_nao_fechado_rejeita()
    {
        var pedido = new Pedido("PED", "02", "0000184", Hoje, "asena") { ReferenciaExterna = "FB2651A" };

        var ex = Assert.Throws<MapeamentoException>(() => MapeadorPedidoParaVfp.Mapear(pedido, "S", "ASENA"));

        Assert.Equal("PEDIDO_NAO_FECHADO", ex.Codigo);
    }

    [Fact]
    public void Mapear_sem_referencia_externa_rejeita()
    {
        var pedido = PedidoFechadoComUmItem(referenciaExterna: null);

        var ex = Assert.Throws<MapeamentoException>(() => MapeadorPedidoParaVfp.Mapear(pedido, "S", "ASENA"));

        Assert.Equal("SEM_REFERENCIA_EXTERNA", ex.Codigo);
    }

    [Fact]
    public void Mapear_uma_linha_por_item_ativo_com_cabecalho_repetido()
    {
        var pedido = PedidoFechadoComUmItem();

        var linhas = MapeadorPedidoParaVfp.Mapear(pedido, esMov: "S", comprador: "ASENA");

        var linha = Assert.Single(linhas);
        Assert.Equal("FB2651A", linha.Codigo);
        Assert.Equal("S", linha.EsMov);
        Assert.Equal("PED", linha.TipoOper);
        Assert.Equal("0000184", linha.CodCli);
        Assert.Equal("02", linha.CodEmpr);
        Assert.Equal("V001", linha.CodVend);
        Assert.Equal("V002", linha.CodVend1);
        Assert.Equal("ASENA", linha.Comprador);
    }

    [Fact]
    public void Mapear_grava_percentual_comissao_no_campo_fisico_qtd_comp()
    {
        // 5% de desconto na escada "V" (negocio=1, piso) -> 5% de comissão (ver ComissaoServiceTests).
        var pedido = PedidoFechadoComUmItem();

        var linha = MapeadorPedidoParaVfp.Mapear(pedido, "S", "ASENA").Single();

        Assert.Equal(5m, linha.QtdComp);
    }

    [Fact]
    public void Mapear_grava_preco_de_tabela_ajustado_no_campo_fisico_qtd_larg()
    {
        var pedido = PedidoFechadoComUmItem();

        var linha = MapeadorPedidoParaVfp.Mapear(pedido, "S", "ASENA").Single();

        Assert.Equal(100m, linha.QtdLarg); // PrecoTabelaAjustado do helper PrecoSimples
    }

    [Fact]
    public void Mapear_ignora_itens_excluidos_mas_conta_no_total_de_itens_ativos()
    {
        var pedido = new Pedido("PED", "02", "0000184", Hoje, "asena") { ReferenciaExterna = "FB2651A" };
        pedido.AdicionarItem("61", "01005000", "A", 2, PrecoSimples(95m));
        pedido.AdicionarItem("61", "01005001", "A", 1, PrecoSimples(100m));
        pedido.ExcluirItem(2);
        pedido.Fechar("V001", "V002", "V");

        var linhas = MapeadorPedidoParaVfp.Mapear(pedido, "S", "ASENA");

        var linha = Assert.Single(linhas);
        Assert.Equal(1, linha.QtditensPe);
    }

    [Fact]
    public void Mapear_soma_o_total_da_nota_apenas_com_itens_ativos()
    {
        var pedido = new Pedido("PED", "02", "0000184", Hoje, "asena") { ReferenciaExterna = "FB2651A" };
        pedido.AdicionarItem("61", "01005000", "A", 2, PrecoSimples(95m));  // 2 * 95 = 190
        pedido.AdicionarItem("61", "01005001", "A", 1, PrecoSimples(100m)); // excluído, não deve contar
        pedido.ExcluirItem(2);
        pedido.Fechar("V001", "V002", "V");

        var linha = MapeadorPedidoParaVfp.Mapear(pedido, "S", "ASENA").Single();

        Assert.Equal(190m, linha.TotalNota);
    }
}
