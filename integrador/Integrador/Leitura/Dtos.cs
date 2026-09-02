namespace Integrador.Leitura;

// DTOs com nomes FÍSICOS do VFP — decisão L10 (20/08/2026): esta é
// exatamente a camada onde "mesmo nome" se aplica ao pé da letra. A tradução
// para nomes de negócio (percentualAdicao, compensacaoIcms, ...) acontece na
// API, nunca aqui.

public sealed record ClienteDto(
    string Codigo,
    string RazaoSoc,
    string Cgc,
    string Posicao,
    string CodVendor,
    string CodVend2,
    decimal Credito,
    decimal Csll,
    decimal Inss,
    decimal Irrf,
    decimal Iss,
    decimal Pis,
    decimal Cofins,
    string CondPag,
    string CodEmpr,
    // Campos fiscais (motor fiscal, 24/08/2026)
    string Estado,
    string CgcCpf,
    string Enquadra,
    string Suframa,
    decimal IncideSuf,
    decimal IncidePis,
    string Desonera,
    // Campos de impressão do pedido (layout mod. PE, 24/08/2026)
    string Endereco,
    string Bairro,
    string Cidade,
    string Cep,
    string Telefone1,
    string InscEsta,
    string TipoCli,
    string Comprador);

public sealed record CadmatDto(
    string Grupo,
    string Referencia,
    string Descricao,
    decimal PrcVenda,
    string Gradecol,
    string Gradegrp,
    decimal QtdPedida,
    decimal QtdFpedid,
    // Campos fiscais (motor fiscal, 24/08/2026)
    decimal Ipi,
    string IndIpi,
    string CodProc,
    string Ncm,
    decimal ValIcm,
    decimal ValIcmSu,
    decimal DespSubst,
    string PermIcm,
    string Legislacao,
    string Beneficio,
    string UnidEmb,
    decimal PesoUnit,
    decimal Volume,
    string CstPis,
    decimal AliqPis,
    string CstCof,
    decimal AliqCof,
    // Complemento da descrição (cor/tamanho/variante) — sempre mostrado
    // junto com Descricao no VFP original (ex.: botão "Últimas Compras").
    string Caracter);

public sealed record NegociaDto(
    string CodCli,
    string Grupo,
    string Referencia,
    string TipoPrc,
    DateTime? DtVenc,
    decimal Preco,
    decimal CodValida);

public sealed record VendedorDto(string CodVend, string Nome, string TipoVend);

/// <summary>
/// Login web (25/08/2026). `Senha` vem cifrada como no VFP — a comparação
/// acontece na API central, nunca aqui; o console só transporta o campo cru.
/// `CodVend` vincula ao vendedor (tabela `vendedor`) e é o que filtra quais
/// clientes o usuário enxerga (cod_vendor/cod_vend2 de `clientes`).
/// </summary>
public sealed record UsuarioDto(string Identific, string Senha, string Nome, string CodVend, bool Inativo);

public sealed record TiposDto(
    string Tipo,
    string Descricao,
    string TipoEs,
    string IndQtd,
    string Natureza,
    string IndValb,
    string Posicao,
    string IndComiss,
    string IndEmpenho,
    string IndSenha,
    string CodVencim,
    decimal Operacao,
    // Campos fiscais (motor fiscal, 24/08/2026)
    string PisCofins,
    string NfcE,
    decimal CodContro);

public sealed record VencimDto(
    string Codigo,
    string DescOcor,
    string Resumido,
    decimal LimVenda,
    string IndDiaPf,
    decimal CodOper,
    decimal DiaPref);

public sealed record VencimrDto(string Resumido, string IndCredit);

public sealed record TituloAbertoDto(
    string Codigo,
    decimal Valor,
    DateTime? DtVencim,
    string CodVencim);

/// <summary>Registro único de configuração — só os campos relevantes ao lançamento de pedido.</summary>
public sealed record WareasDto(
    string ContPed,
    string CompSeq,
    string IndPedDc,
    string IndVerFi,
    string TpPedido,
    decimal ConsMin,
    string IndGrade,
    string IndTotN,
    string IndImpped,
    decimal IndLinha,
    string IndPre,
    string IndNota,
    decimal ProxNota);

public sealed record WareascpDto(string Especifico, string IndPrazo, string IndLargco, string IndArrend, string IndOdbc);

public sealed record WareasbDto(string Indcredito, decimal LimitePed, string BloqPedid);

public sealed record TabcolDto(string Codigo, string Nome);

public sealed record CadsubDto(
    string Grupo,
    string Referencia,
    string Uf,
    string Enquadra,
    string Ncm,
    decimal ValIcm,
    decimal ValIcmSu,
    decimal Icmproprio,
    decimal Reducbase,
    decimal IcmReduc,
    decimal Prcmedio,
    decimal Fcp,
    decimal SubsTrib,
    string CstIcms,
    string Cfop,
    string Cfopsubst);

public sealed record CadicmDto(
    string Estado,
    string Enquadra,
    decimal ValIcm,
    decimal ValIcmE,
    decimal ValIcmSu,
    decimal SubsTrib,
    decimal AliqInter,
    decimal AliqFcp,
    string IncideFcp);

public sealed record TabplanDto(
    string CodEmpr,
    string NomeEmpr,
    string UfEmpr,
    decimal IcmEstado,
    // Dados de impressão do pedido (layout mod. PE, 24/08/2026)
    string Cgc,
    string Inscempr,
    string EnderE,
    string Bairro,
    string CidadeE,
    string CepE,
    string Telefone,
    string Fax,
    string Email);

public sealed record EscadaComissaoItemDto(decimal Negocio, decimal LimiteDesconto, decimal PercentualComissao);

/// <summary>
/// Uma linha de "Últimas Compras" (botão homônimo do ped_wer.scx, classe
/// `ultimas_compras` em artsoft.vcx) — origem real confirmada em
/// `query_cadmov_vendas.qpr` (25/08/2026): `cadmov` filtrada por
/// `es_mov="S"` (venda) e `tipos.ind_fatura="S"` (só operação faturável),
/// não `pedido.dbf`. `cadmov` tem 900k+ linhas por empresa — nunca
/// sincronizada por inteiro, só consultada sob demanda pelo console.
/// </summary>
public sealed record ItemCompraDto(
    DateTime DataMov,
    string NotaFiscal,
    string Grupo,
    string Referencia,
    decimal Quantidade,
    decimal ValorUnitario);
