namespace Integrador.Servico;

// Contrato do lado "comandos" — nomes de negócio, porque quem monta isto é
// a API central (Node), que já decidiu preço/comissão usando o núcleo de
// regras. O console só reconfirma as invariantes (Dominio.Pedido) e grava.

public sealed record FiscalComandoDto(
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

public sealed record ItemComandoDto(
    string Grupo,
    string Referencia,
    string Gradegrp,
    decimal Quantidade,
    decimal PrecoTabelaAjustado,
    decimal PrecoFinal,
    decimal PercentualDesconto,
    string Origem,
    FiscalComandoDto? Fiscal);

public sealed record ComandoPendenteDto(
    string Id,
    string TipoOperacao,
    string CodigoEmpresa,
    string CodigoCliente,
    string Data,
    string? DataEntrega,
    string Autor,
    string ReferenciaExterna,
    string? CondicaoPagamentoCodigo,
    string VendedorCodigo1,
    string VendedorCodigo2,
    string TipoVendedorParaComissao,
    List<ItemComandoDto> Itens,
    string? Tipo,               // null/"CriarPedido" ou "GravarEntrega"
    EntregaComandoDto? Entrega);

/// <summary>Tela "Dados Para Entrega" (form ped_ph do ERP) → cligeral.dbf.</summary>
public sealed record EntregaComandoDto(
    string? Endereco,
    string? Bairro,
    string? Cidade,
    string? Estado,
    string? Referencia,
    string? Observacao1,
    string? Observacao2);

public sealed record ResultadoComandoRequest(bool Sucesso, string? ReferenciaExterna, string? Erro);
