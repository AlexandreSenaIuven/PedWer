namespace Dominio;

/// <summary>
/// Ciclo de vida do PEDIDO em si (RF-005/006) — não confundir com o estado de
/// baixa (E2-E5 da análise), que é agregação do estado dos itens e pertence
/// ao Fluxo B (baixa), fora do escopo deste módulo.
/// </summary>
public enum EstadoPedido
{
    Rascunho,
    Fechado,
    Cancelado,
}

/// <summary>
/// RF-162: estado explícito do item, substituindo os três sentinelas de hoje
/// ("quantidade=0", "grupo+referência vazios", "vwbarra=espaços") que
/// significam a mesma coisa sem um campo próprio.
/// </summary>
public enum EstadoItemPedido
{
    Ativo,
    Excluido,
}
