namespace MotorRegras.Fiscal;

/// <summary>
/// Tudo que o motor fiscal precisa para um item — porta de `veicm3.PRG` +
/// `totitem2.PRG` + `calcimp.PRG` (fontes reais em `Z:\FENDESV9\prg\`, lidos
/// em 24/08/2026), **restrito ao ramo cliente/venda/lançamento**
/// (`tipos.status="C"`, `tipos.tipo_es="S"`, `gcajusta="PEDIDO"` — os únicos
/// que este módulo (Fluxo A) alcança). Ver `MotorFiscalService` para a
/// lista completa do que foi excluído e por quê.
/// </summary>
public sealed record ContextoFiscalItem(
    decimal Quantidade,
    decimal PrecoUnitario,
    decimal PercentualDesconto, // wdesc/wperc_desc — já no formato percentual (0-100)
    decimal AliquotaIpi, // PEDTEMP.ipi — hoje assumido = cadmat.ipi (não achamos onde mais seria atribuído; gap registrado)
    string IndIpi, // cadmat.ind_ipi — "V" soma ipi_fabr ao preço (RF-073); não usado no cálculo do IMPOSTO em si

    ClienteFiscal Cliente,
    ProdutoFiscal Produto,
    TipoOperacaoFiscal TipoOperacao,
    EmpresaFiscal Empresa,
    CadicmResultado? Cadicm,
    CadsubResultado? Cadsub);

public sealed record ClienteFiscal(
    string Estado,
    string CgcCpf, // "C"/"F"/"P"/"R"/"I"/"E" — classificação fiscal do cliente (não é o CNPJ/CPF em si)
    string Enquadra,
    string Suframa,
    decimal IncideSuf,
    decimal IncidePis,
    string Desonera);

public sealed record ProdutoFiscal(
    string CodProc,
    string Ncm,
    decimal ValIcm,
    decimal ValIcmE,
    decimal ValIcmSu,
    decimal DespSubst,
    decimal IcmReduc,
    string PermIcm,
    string Legislacao,
    bool TemBeneficio); // cadmat.beneficio não vazio — quando true, o resultado é rejeitado explicitamente (não implementado)

public sealed record TipoOperacaoFiscal(
    string Natureza,
    string PisCofins, // tipos.piscofins = "P" liga a leitura de cadmat.cod_proc[2..] para os códigos especiais 40/41/50/60
    string NfcE,
    int CodContro); // > 1 = orçamento; fora do escopo (RF-011: WER não usa orçamento)

public sealed record EmpresaFiscal(string Estado, decimal IcmEstado);

public sealed record CadicmResultado(
    decimal ValIcm,
    decimal ValIcmE,
    decimal ValIcmSu, // cadicm.val_icm_su — alíquota interna do ST por UF
    decimal SubsTrib, // cadicm.subs_trib — MVA por UF
    decimal AliqInter,
    decimal AliqFcp,
    string IncideFcp);

public sealed record CadsubResultado(
    decimal ValIcm,
    decimal ValIcmSu,
    decimal IcmProprio,
    decimal ReducBase,
    decimal Prcmedio,
    decimal Fcp,
    decimal SubsTrib);
