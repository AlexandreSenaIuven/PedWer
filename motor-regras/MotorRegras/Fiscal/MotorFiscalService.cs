namespace MotorRegras.Fiscal;

/// <summary>
/// Porta fiel do motor fiscal do ERP (`Z:\FENDESV9\prg\` — `calcimp.PRG`,
/// `veicm3.PRG` 533 linhas, `totitem2.PRG` 1248 linhas, `fiscal.PRG`,
/// `pesquisacadsub.PRG`, todos lidos linha a linha em 24/08/2026),
/// **restrita ao caminho que o lançamento de pedido do WER alcança**:
/// `tipos.status="C"` (cliente), `tipos.tipo_es="S"` (saída/venda),
/// `gcajusta="PEDIDO"`, sem PEDTEMP legado.
///
/// ══ O QUE FICOU FORA, E POR QUÊ (cada exclusão é verificável) ══
/// - Fluxos de COMPRA/fornecedor (`status="F"`) e ENTRADA (`tipo_es="E"`) —
///   fora do escopo do Fluxo A por definição do plano.
/// - Simples Nacional (`pbindsimples`) — `wareasb.INDSIMPLES="F"` medido
///   nesta base (RF-059); o ramo CSOSN nunca roda para este cliente.
/// - Diferimento de ICMS (CST 51 / tabela `diferimento`) — depende do CST do
///   item, que este módulo ainda não coleta (RF-058 em aberto); lançar
///   exceção seria pior que calcular sem diferimento? NÃO: item com CST 51
///   é rejeitado explicitamente (código DIFERIMENTO_NAO_SUPORTADO) para
///   nunca sair um imposto silenciosamente errado.
/// - Benefício fiscal (`cadmat.beneficio`) — idem: rejeitado explicitamente.
/// - Suframa (`clientes.suframa` preenchido) — zera IPI e mexe em PIS/COFINS;
///   rejeitado explicitamente por ora.
/// - Rateio de frete/seguro/despesa (`wval_finan`/`wval_custo`/`wval_seguro`)
///   — o formulário web ainda não coleta esses valores; quando coletar,
///   `totitem2:106-199` é a referência do rateio.
/// - FCP/FCP-ST e DIFAL interestadual p/ consumidor final (EC 87/2015,
///   `pnaliq_inter`/`ncmuf`) — só incide para PF/não-contribuinte
///   interestadual; rejeitado explicitamente nesse cenário.
/// - Reforma tributária IBS/CBS — alíquotas vazias no wareascp deste cliente
///   (medido na análise); nada a calcular ainda.
/// - CFOPs de remessa 5917/6919 (zera base ICMS) — consignação, fora do fluxo.
///
/// ══ RISCO CONHECIDO ══ Este porte NÃO foi validado contra o VFP executando
/// (não há como rodar o app aqui). Fidelidade garantida por leitura, não por
/// comparação de saída. Antes de valer dinheiro, comparar alguns pedidos
/// reais calculados pelo VFP contra esta implementação.
/// </summary>
public static class MotorFiscalService
{
    // fiscal.PRG:mf_trunc — truncamento estável (ROUND intermediário a 4 casas mata lixo de ponto flutuante)
    private static decimal MfTrunc(decimal valor, int casas = 2)
    {
        var fator = (decimal)Math.Pow(10, casas);
        return Math.Truncate(Math.Round(valor * fator, 4)) / fator;
    }

    // fiscal.PRG:mf_totproduto — valor de mercadoria; wareascp.IND_ARREND="S" medido nesta base → ROUND
    private static decimal MfTotProduto(decimal qtd, decimal preco, bool indArrend) =>
        indArrend ? Math.Round(qtd * preco, 2, MidpointRounding.AwayFromZero) : MfTrunc(qtd * preco);

    // fiscal.PRG:mf_imposto — imposto SEMPRE arredonda 2 casas
    private static decimal MfImposto(decimal baseCalc, decimal aliq) =>
        Math.Round(baseCalc * aliq / 100m, 2, MidpointRounding.AwayFromZero);

    public static ResultadoFiscalItem Calcular(ContextoFiscalItem ctx, bool indArrend = true)
    {
        RejeitarCenariosNaoSuportados(ctx);

        // ── veicm3.PRG: resolução da alíquota de ICMS (wicm) ──
        var aliquotaIcm = ResolverAliquotaIcm(ctx);

        // ── totitem2.PRG: valores do item ──
        var pnTotitem = MfTotProduto(ctx.Quantidade, ctx.PrecoUnitario, indArrend);

        // totitem2:226 — desconto do item (tipo "P", percentual; "V" por valor não coletado ainda)
        var windDesc = Math.Round(pnTotitem * ctx.PercentualDesconto / 100m, 2, MidpointRounding.AwayFromZero);

        // totitem2:220/229 — IPI sobre a mercadoria SEM desconto (freteipi não incide: sem frete no fluxo)
        var baseIpi = pnTotitem;
        var wipi1 = MfImposto(baseIpi, ctx.AliquotaIpi);

        // totitem2:252-254 — ind_ipi="V": IPI é VALOR por quantidade, não percentual
        if (ctx.IndIpi == "V")
        {
            wipi1 = ctx.AliquotaIpi * ctx.Quantidade;
        }

        var wripi = wipi1;

        // totitem2:269-270 — base ICMS inicial: mercadoria - desconto
        var wwicm = pnTotitem - windDesc;

        // totitem2:418-438 — consumidor final (cgc_cpf fora de R/" "/I) soma IPI na base do ICMS.
        // Contribuinte (R/I/branco) não soma. (Ramo tembeneficioicms=false — benefício rejeitado acima.)
        var contribuinte = ctx.Cliente.CgcCpf is "R" or "I" or " " or "";
        if (!contribuinte)
        {
            wwicm += wripi; // wbasefrete = 0 (sem frete neste fluxo)
        }

        // totitem2:308/472 — ICMS próprio destacado (lnpis_ap = 0: Suframa rejeitado acima)
        var wwicm1 = MfImposto(wwicm, aliquotaIcm);
        var wwicm2 = wwicm1;

        // ── totitem2:980-1207 — ICMS-ST ──
        var (baseSt, aliquotaSt, mva, usouPauta, valorSt) = CalcularSt(ctx, pnTotitem, windDesc, wripi, wwicm2);

        // total do item como o VFP soma na nota: mercadoria - desconto + IPI + ST (wmerc_tot_g + wic_ret2)
        var totalComImpostos = pnTotitem - windDesc + wipi1 + valorSt;

        return new ResultadoFiscalItem(
            ValorMercadoria: pnTotitem,
            ValorDesconto: windDesc,
            AliquotaIcmResolvida: aliquotaIcm,
            BaseIpi: baseIpi,
            ValorIpi: wipi1,
            BaseIcm: wwicm,
            ValorIcm: wwicm1,
            BaseIcmSt: baseSt,
            AliquotaIcmSt: aliquotaSt,
            MvaAplicado: mva,
            UsouPautaFiscal: usouPauta,
            ValorIcmSt: valorSt,
            TotalItemComImpostos: totalComImpostos);
    }

    private static void RejeitarCenariosNaoSuportados(ContextoFiscalItem ctx)
    {
        if (ctx.Produto.TemBeneficio)
        {
            throw new FiscalException("BENEFICIO_NAO_SUPORTADO",
                "Produto tem benefício fiscal (cadmat.beneficio) — cálculo com benefício ainda não portado; corrija ou trate manualmente.");
        }

        if (!string.IsNullOrWhiteSpace(ctx.Cliente.Suframa) && ctx.Cliente.IncideSuf > 1)
        {
            throw new FiscalException("SUFRAMA_NAO_SUPORTADO",
                "Cliente Suframa com incidência — zeraria IPI e alteraria PIS/COFINS; cenário ainda não portado.");
        }

        var interestadual = ctx.Cliente.Estado != ctx.Empresa.Estado && ctx.Cliente.Estado != "EX";
        var consumidorFinalPf = ctx.Cliente.CgcCpf is "F" or "P";
        if (interestadual && consumidorFinalPf)
        {
            throw new FiscalException("DIFAL_NAO_SUPORTADO",
                "Venda interestadual a consumidor final PF exige DIFAL/FCP (EC 87/2015) — ainda não portado.");
        }

        if (ctx.TipoOperacao.CodContro > 1)
        {
            throw new FiscalException("ORCAMENTO_NAO_SUPORTADO",
                "tipos.cod_contro > 1 (orçamento) zera base de ICMS de forma própria — fora do fluxo do WER (RF-011).");
        }
    }

    /// <summary>
    /// veicm3.PRG, ramo cliente/venda: decide a alíquota de ICMS (wicm).
    /// Ordem real do fonte: códigos fiscais especiais → cadsub → cadicm
    /// (interestadual) → alíquota 4% importado interestadual → cadmat/config
    /// da empresa (mesmo estado).
    /// </summary>
    private static decimal ResolverAliquotaIcm(ContextoFiscalItem ctx)
    {
        // veicm3:140-143 — códigos de processo especiais só valem quando tipos.piscofins="P"
        var codProc2 = ctx.TipoOperacao.PisCofins == "P" && ctx.Produto.CodProc.Length >= 3
            ? ctx.Produto.CodProc[1..]
            : "";

        // veicm3:172-174 — isento/suspenso/diferido: ICMS zero
        if (codProc2 is "40" or "41" or "50")
        {
            return 0m;
        }

        var interestadual = ctx.Cliente.Estado != ctx.Empresa.Estado;

        decimal wicm = 0m;

        // veicm3:108-114 — cadsub encontrado decide primeiro: val_icm (fora do estado) / icmproprio (dentro)
        if (ctx.Cadsub is not null)
        {
            wicm = interestadual ? ctx.Cadsub.ValIcm : ctx.Cadsub.IcmProprio;
        }

        // veicm3:158-162 / 186-197 — sem cadsub (ou wicm zerado), interestadual usa cadicm
        if (wicm == 0m && interestadual && ctx.Cadicm is not null && codProc2 != "60")
        {
            wicm = ctx.Cadicm.ValIcm;
        }

        // veicm3:216-224 — origem importada (cod_proc começando 1/2/3/8) interestadual: alíquota fixa 4%
        var origem = ctx.Produto.CodProc.Length > 0 ? ctx.Produto.CodProc[..1] : "";
        if (interestadual && origem is "1" or "2" or "3" or "8" && wicm > 0)
        {
            wicm = 4m;
        }

        // veicm3:227-231 / 347-352 — mesmo estado: cadmat.val_icm, senão a alíquota da config da empresa
        if (wicm == 0m && !interestadual && codProc2 != "60")
        {
            wicm = ctx.Produto.ValIcm;
            if (wicm == 0m)
            {
                wicm = ctx.Empresa.IcmEstado;
            }
        }

        return wicm;
    }

    /// <summary>
    /// totitem2.PRG:1050-1207 — ICMS-ST do item, ramo venda/cliente sem
    /// diferimento. Gates reais do fonte:
    /// (a) wind_ret=0 — em venda/cliente (totitem2:761-784), cgc_cpf "R"
    ///     entra no primeiro ramo SEM setar wind_ret (revendedor → ST!),
    ///     "I" seta wind_ret=1, e no ELSE só "C"/"E" ficam com wind_ret=0.
    ///     Ou seja: ST calcula para R, C e E; bloqueia para I, F, P, branco.
    ///     (Correção de 24/08/2026 — a 1ª versão bloqueava "R", invertido.)
    /// (b) produto marcado: perm_icm="F" em saída (wareas.ind_pre "S"/"P",
    ///     medido "S" nesta base) — totitem2:1050.
    /// (c) temsubs (totitem2:683-758): sem cadsub/cadicm com alíquota
    ///     própria, a alíquota herdada do CADMAT só vale se o CST do item
    ///     for x10/x30/x70; senão WVAL_ICM_SU é zerado.
    /// </summary>
    private static (decimal BaseSt, decimal AliquotaSt, decimal Mva, bool UsouPauta, decimal ValorSt) CalcularSt(
        ContextoFiscalItem ctx, decimal pnTotitem, decimal windDesc, decimal wripi, decimal wwicm2)
    {
        var windRet = ctx.Cliente.CgcCpf is not ("R" or "C" or "E");

        var produtoMarcadoSt = ctx.Produto.PermIcm == "F";

        if (windRet || !produtoMarcadoSt)
        {
            return (0m, 0m, 0m, false, 0m);
        }

        // totitem2:683-758 — temsubs: CST (= cadmat.cod_proc, RF-058 fallback; regime normal,
        // INDSIMPLES="F" medido) precisa ser x10/x30/x70 para a alíquota do cadmat valer.
        var cst2 = ctx.Produto.CodProc.Length >= 3 ? ctx.Produto.CodProc[1..] : "";
        var temSubs = cst2 is "10" or "30" or "70";

        // totitem2:659-681 — alíquota interna do ST (WVAL_ICM_SU): cadicm > 0 → cadicm; senão cadmat;
        // e sem temsubs a herança do cadmat é zerada (totitem2:756-758).
        var aliquotaSt = ctx.Cadicm is { ValIcmSu: > 0 } ? ctx.Cadicm.ValIcmSu : ctx.Produto.ValIcmSu;
        if (!temSubs && (ctx.Cadicm is null || ctx.Cadicm.ValIcmSu <= 0))
        {
            aliquotaSt = 0m;
        }

        // totitem2:1062-1097 — MVA (wdesp_subst): cadsub → cadicm → cadmat → MVAs fixos de SP
        decimal mva;
        if (ctx.Cadsub is not null)
        {
            aliquotaSt = ctx.Cadsub.ValIcmSu;
            mva = ctx.Cadsub.SubsTrib;
        }
        else if (ctx.Cadicm is not null)
        {
            // totitem2:1069-1078 — cadicm posicionado: val_icm_su>0 usa-o (mesmo sem temsubs);
            // subs_trib>0 usa-o, senão cadmat.desp_subst
            if (ctx.Cadicm.ValIcmSu > 0)
            {
                aliquotaSt = ctx.Cadicm.ValIcmSu;
            }
            mva = ctx.Cadicm.SubsTrib > 0 ? ctx.Cadicm.SubsTrib : ctx.Produto.DespSubst;
        }
        else
        {
            mva = ctx.Produto.DespSubst;
            // totitem2:1085-1094 — MVAs fixos de SP (165,55 / 71,60 / 38,90) dependem de
            // clientes.status_mov e wareas.estado="SP" — a base do WER é RJ; ramo inalcançável
            // aqui, registrado de propósito em vez de portado às cegas.
        }

        if (aliquotaSt == 0m)
        {
            return (0m, 0m, mva, false, 0m);
        }

        // totitem2:906-908 — wmerc_tot: mercadoria - desconto, truncado a 3 casas
        var wmercTot = Math.Truncate((pnTotitem - windDesc) * 1000m) / 1000m;

        // totitem2:1147-1162 — pauta fiscal: preço < 90% da pauta ⇒ base = pauta × qtd (MVA ignorado)
        decimal wbaCalc2;
        var usouPauta = false;
        if (ctx.Cadsub is { Prcmedio: > 0 } && ctx.PrecoUnitario < ctx.Cadsub.Prcmedio * 0.90m)
        {
            wbaCalc2 = ctx.Cadsub.Prcmedio * ctx.Quantidade + wripi;
            usouPauta = true;
        }
        else
        {
            // wba_calc1 = ROUND((wmerc_tot + wripi) × MVA / 100, 2); wba_calc3=0 (sem frete); lntotalpis=0 (sem Suframa)
            var wbaCalc1 = Math.Round((wmercTot + wripi) * mva / 100m, 2, MidpointRounding.AwayFromZero);
            wbaCalc2 = wmercTot + wbaCalc1 + wripi;
        }

        // totitem2:1171-1173 — redução de base (pnredbasesubst = cadsub.reducbase, corrigindo DF-R2/RF-060:
        // o WER original nunca preenchia isso; aqui vem direto do cadastro, como RF-060 manda)
        var reducao = ctx.Cadsub?.ReducBase ?? 0m;
        if (reducao > 0m)
        {
            wbaCalc2 -= wbaCalc2 * reducao / 100m;
        }

        // totitem2:1177/1187 — ST devido = base × alíquota interna − ICMS próprio
        var wicRet1 = wbaCalc2 * aliquotaSt / 100m;
        var wicRet2 = wicRet1 > 0 ? Math.Round(wicRet1 - wwicm2, 2, MidpointRounding.AwayFromZero) : 0m;

        return (wbaCalc2, aliquotaSt, mva, usouPauta, wicRet2);
    }
}
