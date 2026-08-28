using MotorRegras;
using MotorRegras.Fiscal;

// Núcleo de regras de negócio, exposto como serviço central — SEM
// dependência de VFPOLEDB, roda em qualquer lugar (não precisa estar na
// máquina do cliente). Reaproveita o motor-regras já testado (26 testes) em
// vez de reescrever a lógica em TypeScript — a API Node chama isto de forma
// síncrona (baixa latência, ambos no mesmo servidor central) para preço,
// comissão e crédito. Quem lê/escreve DBF é só o console (Integrador),
// numa máquina separada, falando com a API Node por sincronização
// periódica + fila de comandos — nunca com este serviço diretamente.

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

app.MapPost("/precificar", (PrecificarRequest req) =>
{
    var fatores = new FatoresPrecoCliente(req.PercentualAdicao, req.CompensacaoIcms, req.DescontoDiretoria);
    var negociacao = req.Negociacao is null
        ? null
        : new NegociacaoVigente(req.Negociacao.PrecoNegociado, DateOnly.Parse(req.Negociacao.DataValidade), req.Negociacao.Autorizada);

    try
    {
        var resultado = PrecificacaoService.CalcularPrecoItem(
            req.PrecoTabelaBase, fatores, negociacao, req.PrecoDigitado, req.PercentualTetoDesconto, DateOnly.Parse(req.DataReferencia));
        return Results.Ok(new
        {
            resultado.PrecoTabelaAjustado,
            resultado.PrecoFinal,
            resultado.PercentualDesconto,
            origem = resultado.Origem.ToString(),
        });
    }
    catch (PrecificacaoException ex)
    {
        return Results.BadRequest(new { codigo = ex.Codigo, erro = ex.Message });
    }
});

app.MapPost("/comissao", (ComissaoRequest req) =>
{
    try
    {
        var percentual = ComissaoService.ResolverPercentualComissao(req.TipoVendedor, req.PercentualDesconto);
        return Results.Ok(new { percentualComissao = percentual });
    }
    catch (ComissaoException ex)
    {
        return Results.BadRequest(new { codigo = ex.Codigo, erro = ex.Message });
    }
});

app.MapPost("/credito", (CreditoRequest req) =>
{
    var titulos = req.TitulosAbertos.Select(t => new TituloAberto(t.Valor, DateOnly.Parse(t.DataVencimento))).ToList();
    var resultado = AvaliacaoCreditoService.Avaliar(
        titulos, req.LimiteMatriz, req.DiasToleranciaAtraso, req.ValorPedidoAtual, DateOnly.Parse(req.DataReferencia));

    return Results.Ok(new
    {
        status = resultado.Status.ToString(),
        resultado.SaldoDevedorGrupo,
        resultado.LimiteMatriz,
    });
});

app.MapPost("/fiscal", (FiscalRequest req) =>
{
    var ctx = new ContextoFiscalItem(
        req.Quantidade,
        req.PrecoUnitario,
        req.PercentualDesconto,
        req.AliquotaIpi,
        req.IndIpi ?? "",
        new ClienteFiscal(req.ClienteEstado, req.ClienteCgcCpf, req.ClienteEnquadra ?? "", req.ClienteSuframa ?? "", req.ClienteIncideSuf, req.ClienteIncidePis, req.ClienteDesonera ?? ""),
        new ProdutoFiscal(req.ProdutoCodProc ?? "", req.ProdutoNcm ?? "", req.ProdutoValIcm, req.ProdutoValIcmE, req.ProdutoValIcmSu, req.ProdutoDespSubst, req.ProdutoIcmReduc, req.ProdutoPermIcm ?? "", req.ProdutoLegislacao ?? "", req.ProdutoTemBeneficio),
        new TipoOperacaoFiscal(req.TipoNatureza ?? "", req.TipoPisCofins ?? "", req.TipoNfcE ?? "", req.TipoCodContro),
        new EmpresaFiscal(req.EmpresaEstado, req.EmpresaIcmEstado),
        req.Cadicm is null ? null : new CadicmResultado(req.Cadicm.ValIcm, req.Cadicm.ValIcmE, req.Cadicm.ValIcmSu, req.Cadicm.SubsTrib, req.Cadicm.AliqInter, req.Cadicm.AliqFcp, req.Cadicm.IncideFcp ?? ""),
        req.Cadsub is null ? null : new CadsubResultado(req.Cadsub.ValIcm, req.Cadsub.ValIcmSu, req.Cadsub.IcmProprio, req.Cadsub.ReducBase, req.Cadsub.Prcmedio, req.Cadsub.Fcp, req.Cadsub.SubsTrib));

    try
    {
        var r = MotorFiscalService.Calcular(ctx);
        return Results.Ok(new
        {
            r.ValorMercadoria,
            r.ValorDesconto,
            r.AliquotaIcmResolvida,
            r.BaseIpi,
            r.ValorIpi,
            r.BaseIcm,
            r.ValorIcm,
            r.BaseIcmSt,
            r.AliquotaIcmSt,
            r.MvaAplicado,
            r.UsouPautaFiscal,
            r.ValorIcmSt,
            r.TotalItemComImpostos,
        });
    }
    catch (FiscalException ex)
    {
        return Results.BadRequest(new { codigo = ex.Codigo, erro = ex.Message });
    }
});

app.Run();

record NegociacaoRequest(decimal PrecoNegociado, string DataValidade, bool Autorizada);

record PrecificarRequest(
    decimal PrecoTabelaBase,
    decimal PercentualAdicao,
    decimal CompensacaoIcms,
    decimal DescontoDiretoria,
    NegociacaoRequest? Negociacao,
    decimal? PrecoDigitado,
    decimal PercentualTetoDesconto,
    string DataReferencia);

record ComissaoRequest(string TipoVendedor, decimal PercentualDesconto);

record TituloAbertoRequest(decimal Valor, string DataVencimento);

record CreditoRequest(
    List<TituloAbertoRequest> TitulosAbertos,
    decimal LimiteMatriz,
    int DiasToleranciaAtraso,
    decimal ValorPedidoAtual,
    string DataReferencia);

record CadicmRequest(decimal ValIcm, decimal ValIcmE, decimal ValIcmSu, decimal SubsTrib, decimal AliqInter, decimal AliqFcp, string? IncideFcp);

record CadsubRequest(decimal ValIcm, decimal ValIcmSu, decimal IcmProprio, decimal ReducBase, decimal Prcmedio, decimal Fcp, decimal SubsTrib);

record FiscalRequest(
    decimal Quantidade,
    decimal PrecoUnitario,
    decimal PercentualDesconto,
    decimal AliquotaIpi,
    string? IndIpi,
    string ClienteEstado,
    string ClienteCgcCpf,
    string? ClienteEnquadra,
    string? ClienteSuframa,
    decimal ClienteIncideSuf,
    decimal ClienteIncidePis,
    string? ClienteDesonera,
    string? ProdutoCodProc,
    string? ProdutoNcm,
    decimal ProdutoValIcm,
    decimal ProdutoValIcmE,
    decimal ProdutoValIcmSu,
    decimal ProdutoDespSubst,
    decimal ProdutoIcmReduc,
    string? ProdutoPermIcm,
    string? ProdutoLegislacao,
    bool ProdutoTemBeneficio,
    string? TipoNatureza,
    string? TipoPisCofins,
    string? TipoNfcE,
    int TipoCodContro,
    string EmpresaEstado,
    decimal EmpresaIcmEstado,
    CadicmRequest? Cadicm,
    CadsubRequest? Cadsub);
