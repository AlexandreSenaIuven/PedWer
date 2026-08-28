using MotorRegras;

namespace Dominio;

/// <summary>
/// Resultado fiscal do item, decidido pela API central (motor fiscal) e
/// carregado até a gravação em `pedido.dbf` — o console não recalcula
/// imposto, só transporta o que já foi decidido (mesmo padrão do preço).
/// </summary>
public sealed record FiscalItemPedido(
    decimal AliquotaIpi,
    decimal AliquotaIcm,
    decimal ValorIpi,
    decimal ValorIcm,
    decimal ValorIcmSt,
    decimal BaseIcm,
    decimal BaseIcmSt,
    decimal ValorMercadoria,
    string Cst,
    string Cfop,
    string CstPis,
    decimal AliqPis,
    string CstCof,
    decimal AliqCof,
    string Unidade);

/// <summary>
/// RF-001/002: item é entidade própria, não repetição do cabeçalho.
/// Identidade estável (`Numero`, fixado na criação) corrige RF-174 — hoje a
/// exclusão de item depende de aritmética de posição no grid porque cada
/// item vira duas linhas na lista.
/// </summary>
public sealed class ItemPedido
{
    public long? Id { get; internal set; }
    public int Numero { get; }
    public string ProdutoGrupo { get; }
    public string ProdutoReferencia { get; }
    public decimal Quantidade { get; }
    public decimal PrecoTabelaAjustado { get; }
    public decimal PrecoFinal { get; }
    public decimal PercentualDesconto { get; }
    public OrigemPreco OrigemPreco { get; }

    /// <summary>RF-054, decisão L2: só é preenchido no fechamento do pedido, com o vendedor do PEDIDO — nunca na digitação do item.</summary>
    public decimal? PercentualComissao { get; private set; }

    /// <summary>RF-054: campo próprio para M2/ML/M3 — nunca reaproveita `qtd_larg`/`qtd_comp` (RF-055, colisão dormente por configuração).</summary>
    public decimal? MedidaLargura { get; set; }

    /// <summary>Impostos e códigos fiscais do item — null quando o motor fiscal não calculou (cenário excluído).</summary>
    public FiscalItemPedido? Fiscal { get; set; }

    public EstadoItemPedido Estado { get; private set; } = EstadoItemPedido.Ativo;

    internal ItemPedido(
        int numero,
        string produtoGrupo,
        string produtoReferencia,
        decimal quantidade,
        ResultadoPrecoItem preco)
    {
        if (quantidade <= 0)
        {
            // RF-088: quantidade zero é rejeitada, nunca silenciosamente descartada.
            throw new DominioException("QUANTIDADE_INVALIDA", "Quantidade do item deve ser maior que zero.");
        }

        Numero = numero;
        ProdutoGrupo = produtoGrupo;
        ProdutoReferencia = produtoReferencia;
        Quantidade = quantidade;
        PrecoTabelaAjustado = preco.PrecoTabelaAjustado;
        PrecoFinal = preco.PrecoFinal;
        PercentualDesconto = preco.PercentualDesconto;
        OrigemPreco = preco.Origem;
    }

    internal void ResolverComissao(decimal percentualComissao) => PercentualComissao = percentualComissao;

    /// <summary>RF-173: exclusão de item já incluído — lógica, com identidade preservada (RF-162), não removida da lista.</summary>
    public void Excluir()
    {
        if (Estado == EstadoItemPedido.Excluido)
        {
            throw new DominioException("ITEM_JA_EXCLUIDO", $"Item {Numero} já está excluído.");
        }

        Estado = EstadoItemPedido.Excluido;
    }
}
