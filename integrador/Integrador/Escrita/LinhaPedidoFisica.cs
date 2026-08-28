namespace Integrador.Escrita;

/// <summary>
/// Uma linha física de `pedido.dbf` — nomes exatos do VFP (confirmados via
/// schema em 20/08/2026), cabeçalho REPETIDO em cada item, exatamente como
/// `pcondpg2.prg` grava hoje. Esta é a ponta do contrato interno API↔console
/// que usa nomes físicos (decisão L10) — nunca deveria vazar para fora do
/// console.
///
/// Propriedades `?` (nullable) são campos que este módulo (lançamento de
/// pedido, Fluxo A) deliberadamente NÃO resolve — ver a lista completa em
/// `dominio/README.md` ("O que NÃO está modelado ainda"). Ficam null aqui de
/// propósito; quem for gravar de fato precisa decidir o que fazer com eles
/// antes do primeiro `APPEND BLANK` real (motor fiscal, reserva de estoque,
/// bloqueio de faturamento, número de nota da baixa).
/// </summary>
public sealed record LinhaPedidoFisica(
    // Identidade (RF-001: chave real é ES_MOV + TIPO_OPER + CODIGO)
    string Codigo,
    string EsMov,
    string TipoOper,

    // Cabeçalho, repetido em toda linha — replica o formato de hoje, não o defeito:
    // como a Fase 5 grava tudo de uma vez (RF-163), não existe janela em que essas
    // colunas fiquem divergentes entre itens do mesmo pedido.
    string CodCli,
    DateTime DataPed,
    string? CCusto,
    string CodVend,
    string CodVend1,
    string CondPag,
    string CodEmpr,
    string Comprador,
    decimal TotalNota,

    // Item
    string MaterialTres, // material03 — sequência do item dentro do pedido
    string Grupo,
    string Referencia,
    decimal QtdItens,
    decimal PrcVenda,

    // Campos polivalentes do VFP (RF-054/RF-096-097) — o schema físico de
    // `pedido.dbf` NÃO muda; aqui é onde o significado de negócio do modelo
    // novo (`PercentualComissao`, `PrecoTabelaAjustado`) volta a ocupar os
    // campos físicos que os relatórios de comissão existentes (`relcomis`,
    // `relcomvend`, via `cadmov.qtd_comp` na baixa) ainda esperam.
    decimal QtdLarg,  // = preço de tabela ajustado (ValPrcWer), mesmo mapeamento de hoje
    decimal QtdComp,  // = percentual de comissão do item

    decimal QtditensPe // total de itens ativos do pedido — RF's "qtditenspe", reescrito hoje em fecha_nota
)
{
    // ── Campos fiscais do item (motor fiscal — 24/08/2026) ──
    public string Cfop { get; init; } = "";
    public string Cst { get; init; } = "";
    public string CstPis { get; init; } = "";
    public decimal AliqPis { get; init; }
    public string CstCof { get; init; } = "";
    public decimal AliqCof { get; init; }
    public decimal Ipi { get; init; } // alíquota IPI do item (pedido.ipi)
    public decimal Icm { get; init; } // alíquota ICMS do item (pedido.icm)
    public decimal IcmretM { get; init; } // ST do item (pedido.icmret_m)
    public decimal BaseicmRt { get; init; } // base ST do item (pedido.baseicm_rt)
    public string Unidade { get; init; } = "";

    // ── Totais fiscais do pedido (repetidos em cada linha, como o VFP grava) ──
    public decimal TotalIpi { get; init; }
    public decimal BaseIcm { get; init; }
    public decimal IcmsRet { get; init; } // ST total
    public decimal BcRetenc { get; init; } // base ST total
    public decimal TotalIcm { get; init; }
    public decimal TotMerc { get; init; }

    public string HoraIni { get; init; } = "";
    public string HoraFim { get; init; } = "";
    public DateTime? DataEnt { get; init; } // prazo de entrega; null → data vazia no DBF

    /// <summary>Reserva de estoque, posição de bloqueio e número de nota — fora deste módulo. Ver dominio/README.md.</summary>
    public string? Csosn { get; init; }
    public decimal? QtReserva { get; init; }
    public string? Posicao { get; init; }
    public string? Notafis { get; init; }
}
