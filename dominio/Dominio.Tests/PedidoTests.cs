using MotorRegras;

namespace Dominio.Tests;

public class PedidoTests
{
    private static readonly DateOnly Hoje = new(2026, 8, 20);

    private static ResultadoPrecoItem PrecoSimples(decimal precoFinal = 100m) =>
        new(PrecoTabelaAjustado: 100m, PrecoFinal: precoFinal, PercentualDesconto: 100m - precoFinal, Origem: OrigemPreco.TabelaSemDesconto);

    private static Pedido NovoPedido() => new("PED", "02", "0000184", Hoje, "asena");

    [Fact]
    public void AdicionarItem_com_primeiro_item_trava_a_linha_de_negocio_RF142()
    {
        var pedido = NovoPedido();

        pedido.AdicionarItem("61", "01005000", gradegrp: "A", quantidade: 2, PrecoSimples());

        Assert.Equal("A", pedido.LinhaNegocioGrupo);
    }

    [Fact]
    public void AdicionarItem_com_grupo_de_negocio_diferente_rejeita_RF142()
    {
        var pedido = NovoPedido();
        pedido.AdicionarItem("61", "01005000", gradegrp: "A", quantidade: 2, PrecoSimples());

        var ex = Assert.Throws<DominioException>(() =>
            pedido.AdicionarItem("62", "02001000", gradegrp: "B", quantidade: 1, PrecoSimples()));

        Assert.Equal("LINHA_NEGOCIO_DIVERGENTE", ex.Codigo);
    }

    [Fact]
    public void AdicionarItem_com_quantidade_zero_rejeita_RF088()
    {
        var pedido = NovoPedido();

        var ex = Assert.Throws<DominioException>(() =>
            pedido.AdicionarItem("61", "01005000", gradegrp: "A", quantidade: 0, PrecoSimples()));

        Assert.Equal("QUANTIDADE_INVALIDA", ex.Codigo);
    }

    [Fact]
    public void ExcluirItem_marca_estado_excluido_sem_remover_da_lista_RF162()
    {
        var pedido = NovoPedido();
        pedido.AdicionarItem("61", "01005000", "A", 2, PrecoSimples());

        pedido.ExcluirItem(1);

        Assert.Single(pedido.Itens); // continua na lista — identidade preservada
        Assert.Equal(EstadoItemPedido.Excluido, pedido.Itens[0].Estado);
        Assert.Empty(pedido.ItensAtivos);
    }

    [Fact]
    public void Fechar_sem_itens_ativos_rejeita()
    {
        var pedido = NovoPedido();

        var ex = Assert.Throws<DominioException>(() => pedido.Fechar("V001", "V002", "V"));

        Assert.Equal("PEDIDO_SEM_ITENS", ex.Codigo);
    }

    [Fact]
    public void Fechar_resolve_comissao_de_cada_item_com_o_vendedor_do_PEDIDO_decisao_L2()
    {
        var pedido = NovoPedido();
        // desconto de 5% -> negocio=1 ("V"): faixa piso é a de 0% -> 5% de comissão (ver ComissaoServiceTests).
        pedido.AdicionarItem("61", "01005000", "A", 2, PrecoSimples(precoFinal: 95m));

        pedido.Fechar(vendedorCodigo1: "V001", vendedorCodigo2: "V002", tipoVendedorParaComissao: "V");

        Assert.Equal(EstadoPedido.Fechado, pedido.Estado);
        Assert.Equal("V001", pedido.VendedorCodigo1);
        Assert.Equal(5m, pedido.Itens[0].PercentualComissao);
    }

    [Fact]
    public void Pedido_fechado_nao_aceita_mais_itens_nem_pode_ser_fechado_de_novo()
    {
        var pedido = NovoPedido();
        pedido.AdicionarItem("61", "01005000", "A", 2, PrecoSimples());
        pedido.Fechar("V001", "V002", "V");

        var exItem = Assert.Throws<DominioException>(() =>
            pedido.AdicionarItem("61", "01005001", "A", 1, PrecoSimples()));
        var exFechar = Assert.Throws<DominioException>(() => pedido.Fechar("V001", "V002", "V"));

        Assert.Equal("PEDIDO_NAO_EDITAVEL", exItem.Codigo);
        Assert.Equal("PEDIDO_NAO_EDITAVEL", exFechar.Codigo);
    }

    [Fact]
    public void Cancelar_e_logico_e_registra_autor_e_motivo_RF009()
    {
        var pedido = NovoPedido();

        pedido.Cancelar("asena", "Cliente desistiu");

        Assert.Equal(EstadoPedido.Cancelado, pedido.Estado);
        Assert.Equal("asena", pedido.AutorCancelamento);
        Assert.Equal("Cliente desistiu", pedido.MotivoCancelamento);
    }

    [Fact]
    public void Cancelar_duas_vezes_rejeita()
    {
        var pedido = NovoPedido();
        pedido.Cancelar("asena", "motivo 1");

        var ex = Assert.Throws<DominioException>(() => pedido.Cancelar("asena", "motivo 2"));

        Assert.Equal("PEDIDO_JA_CANCELADO", ex.Codigo);
    }
}
