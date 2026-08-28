using MotorRegras;

namespace Dominio;

/// <summary>
/// Agregado do lançamento de pedido (Fluxo A). RF-001: cabeçalho separado do
/// item — os ~40 campos hoje repetidos em cada linha de `pedido.dbf` existem
/// aqui uma única vez. RF-023: uma vez que o primeiro item é aceito, o
/// cabeçalho está selado (tipo de operação, empresa, cliente, data não
/// podem mudar) — reforçado pelos setters privados abaixo.
/// </summary>
public sealed class Pedido
{
    private readonly List<ItemPedido> _itens = new();

    public long? Id { get; internal set; }

    /// <summary>RF-109/L3: identidade interna nunca é o código digitado pelo vendedor — aquele vira só referência.</summary>
    public string? ReferenciaExterna { get; set; }

    public string TipoOperacao { get; }
    public string CodigoEmpresa { get; }
    public string CodigoCliente { get; }
    public DateOnly Data { get; }

    /// <summary>Prazo de entrega (pedido.data_ent) — "PRAZO DE ENTREGA" no formulário impresso.</summary>
    public DateOnly? DataEntrega { get; set; }
    public string AutorCriacao { get; }

    public string? CondicaoPagamentoCodigo { get; set; }
    public string? CentroCustoCodigo { get; set; }
    public string? VendedorCodigo1 { get; private set; }
    public string? VendedorCodigo2 { get; private set; }

    /// <summary>RF-142: gradegrp do primeiro item aceito trava a linha de negócio do pedido inteiro.</summary>
    public string? LinhaNegocioGrupo { get; private set; }

    public EstadoPedido Estado { get; private set; } = EstadoPedido.Rascunho;
    public string? AutorCancelamento { get; private set; }
    public string? MotivoCancelamento { get; private set; }

    public IReadOnlyList<ItemPedido> Itens => _itens;
    public IEnumerable<ItemPedido> ItensAtivos => _itens.Where(i => i.Estado == EstadoItemPedido.Ativo);

    public Pedido(string tipoOperacao, string codigoEmpresa, string codigoCliente, DateOnly data, string autorCriacao)
    {
        TipoOperacao = tipoOperacao;
        CodigoEmpresa = codigoEmpresa;
        CodigoCliente = codigoCliente;
        Data = data;
        AutorCriacao = autorCriacao;
    }

    /// <summary>
    /// RF-020/142: adiciona um item já precificado (o motor de preço já rodou
    /// fora daqui — este agregado não faz I/O). `gradegrp` vem do produto e
    /// decide se ele pode conviver com os itens já aceitos.
    /// </summary>
    public ItemPedido AdicionarItem(string produtoGrupo, string produtoReferencia, string gradegrp, decimal quantidade, ResultadoPrecoItem preco)
    {
        ExigirRascunho();

        if (LinhaNegocioGrupo is null)
        {
            LinhaNegocioGrupo = gradegrp;
        }
        else if (LinhaNegocioGrupo != gradegrp)
        {
            throw new DominioException(
                "LINHA_NEGOCIO_DIVERGENTE",
                $"Produto do grupo de negócio '{gradegrp}' não pode conviver com itens do grupo '{LinhaNegocioGrupo}' no mesmo pedido (RF-142).");
        }

        var proximoNumero = _itens.Count + 1;
        var item = new ItemPedido(proximoNumero, produtoGrupo, produtoReferencia, quantidade, preco);
        _itens.Add(item);
        return item;
    }

    public void ExcluirItem(int numero)
    {
        ExigirRascunho();
        var item = _itens.SingleOrDefault(i => i.Numero == numero)
            ?? throw new DominioException("ITEM_NAO_ENCONTRADO", $"Item {numero} não existe neste pedido.");
        item.Excluir();
    }

    /// <summary>
    /// RF-127-133 + decisão L2: os dois vendedores só são conhecidos aqui, e
    /// é só agora que a faixa de comissão de cada item é resolvida — com o
    /// tipo do vendedor do PEDIDO, não do cliente. `tipoVendedorParaComissao`
    /// já vem resolvido pelo chamador (consulta a `vendedor.tipo_vend`).
    /// </summary>
    public void Fechar(string vendedorCodigo1, string vendedorCodigo2, string tipoVendedorParaComissao)
    {
        ExigirRascunho();

        if (!ItensAtivos.Any())
        {
            throw new DominioException("PEDIDO_SEM_ITENS", "Pedido não pode ser fechado sem nenhum item ativo.");
        }

        VendedorCodigo1 = vendedorCodigo1;
        VendedorCodigo2 = vendedorCodigo2;

        foreach (var item in ItensAtivos)
        {
            var percentual = ComissaoService.ResolverPercentualComissao(tipoVendedorParaComissao, item.PercentualDesconto);
            item.ResolverComissao(percentual);
        }

        Estado = EstadoPedido.Fechado;
    }

    /// <summary>RF-009: cancelamento é lógico, com autor e motivo — nunca `DELETE` físico.</summary>
    public void Cancelar(string autor, string motivo)
    {
        if (Estado == EstadoPedido.Cancelado)
        {
            throw new DominioException("PEDIDO_JA_CANCELADO", "Pedido já está cancelado.");
        }

        Estado = EstadoPedido.Cancelado;
        AutorCancelamento = autor;
        MotivoCancelamento = motivo;
    }

    private void ExigirRascunho()
    {
        if (Estado != EstadoPedido.Rascunho)
        {
            throw new DominioException("PEDIDO_NAO_EDITAVEL", $"Pedido está '{Estado}' — não pode ser alterado.");
        }
    }
}
