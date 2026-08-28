using MotorRegras.Fiscal;

namespace MotorRegras.Tests;

public class MotorFiscalServiceTests
{
    private static ContextoFiscalItem Contexto(
        decimal quantidade = 2m,
        decimal precoUnitario = 100m,
        decimal percentualDesconto = 0m,
        decimal aliquotaIpi = 10m,
        string indIpi = "",
        string clienteEstado = "RJ",
        string cgcCpf = "C",
        string empresaEstado = "RJ",
        decimal empresaIcmEstado = 20m,
        string codProc = "000",
        string permIcm = "",
        decimal valIcm = 18m,
        decimal valIcmSu = 0m,
        decimal despSubst = 0m,
        bool temBeneficio = false,
        string suframa = "",
        decimal incideSuf = 0m,
        CadicmResultado? cadicm = null,
        CadsubResultado? cadsub = null,
        string pisCofins = "")
    {
        return new ContextoFiscalItem(
            quantidade,
            precoUnitario,
            percentualDesconto,
            aliquotaIpi,
            indIpi,
            new ClienteFiscal(clienteEstado, cgcCpf, "", suframa, incideSuf, 0m, ""),
            new ProdutoFiscal(codProc, "12345678", valIcm, 0m, valIcmSu, despSubst, 0m, permIcm, "", temBeneficio),
            new TipoOperacaoFiscal("5102", pisCofins, "", 1),
            new EmpresaFiscal(empresaEstado, empresaIcmEstado),
            cadicm,
            cadsub);
    }

    // ── IPI (calcimp.PRG:10,17-18 + totitem2:220,229) ──

    [Fact]
    public void Ipi_e_calculado_sobre_a_mercadoria_sem_desconto_e_sempre_arredonda()
    {
        // 2 × 100 = 200; IPI 10% = 20,00 — desconto NÃO reduz a base do IPI (totitem2:220)
        var r = MotorFiscalService.Calcular(Contexto(percentualDesconto: 5m));

        Assert.Equal(200m, r.BaseIpi);
        Assert.Equal(20m, r.ValorIpi);
    }

    [Fact]
    public void Ipi_com_ind_ipi_V_e_valor_por_quantidade_nao_percentual()
    {
        // totitem2:252-254 — wipi1 = wval1_ipi * wqtd1_mov
        var r = MotorFiscalService.Calcular(Contexto(indIpi: "V", aliquotaIpi: 3.5m, quantidade: 4m));

        Assert.Equal(14m, r.ValorIpi);
    }

    // ── ICMS: base e IPI-na-base (totitem2:269-270, 418-438) ──

    [Fact]
    public void Consumidor_final_soma_ipi_na_base_do_icms_contribuinte_nao()
    {
        // Contribuinte (cgc_cpf = "R"): base = mercadoria - desconto
        var contribuinte = MotorFiscalService.Calcular(Contexto(cgcCpf: "R", percentualDesconto: 10m));
        // Cliente comum (cgc_cpf = "C"): base = mercadoria - desconto + IPI
        var consumidor = MotorFiscalService.Calcular(Contexto(cgcCpf: "C", percentualDesconto: 10m));

        Assert.Equal(180m, contribuinte.BaseIcm); // 200 - 20
        Assert.Equal(200m, consumidor.BaseIcm); // 200 - 20 + 20 de IPI
    }

    [Fact]
    public void Icms_proprio_sempre_arredonda_a_2_casas()
    {
        // base 200 × 18% = 36,00
        var r = MotorFiscalService.Calcular(Contexto(percentualDesconto: 10m));

        Assert.Equal(Math.Round(r.BaseIcm * 18m / 100m, 2, MidpointRounding.AwayFromZero), r.ValorIcm);
    }

    // ── veicm3: resolução da alíquota ──

    [Theory]
    [InlineData("040", 0)] // isento
    [InlineData("041", 0)] // não tributado
    [InlineData("050", 0)] // suspensão
    public void Cod_proc_especial_zera_o_icms_quando_piscofins_P(string codProc, decimal esperado)
    {
        var r = MotorFiscalService.Calcular(Contexto(codProc: codProc, pisCofins: "P"));

        Assert.Equal(esperado, r.AliquotaIcmResolvida);
    }

    [Fact]
    public void Mesmo_estado_sem_cadsub_usa_val_icm_do_produto()
    {
        var r = MotorFiscalService.Calcular(Contexto(valIcm: 18m));

        Assert.Equal(18m, r.AliquotaIcmResolvida);
    }

    [Fact]
    public void Mesmo_estado_com_produto_sem_aliquota_cai_na_config_da_empresa()
    {
        var r = MotorFiscalService.Calcular(Contexto(valIcm: 0m, empresaIcmEstado: 20m));

        Assert.Equal(20m, r.AliquotaIcmResolvida);
    }

    [Fact]
    public void Interestadual_usa_cadicm()
    {
        var r = MotorFiscalService.Calcular(Contexto(
            clienteEstado: "MG",
            cadicm: new CadicmResultado(12m, 0m, 0m, 0m, 0m, 0m, "")));

        Assert.Equal(12m, r.AliquotaIcmResolvida);
    }

    [Fact]
    public void Interestadual_com_origem_importada_forca_4_por_cento()
    {
        // veicm3:216-224 — cod_proc começando em 1/2/3/8 interestadual → 4%
        var r = MotorFiscalService.Calcular(Contexto(
            clienteEstado: "MG",
            codProc: "100",
            cadicm: new CadicmResultado(12m, 0m, 0m, 0m, 0m, 0m, "")));

        Assert.Equal(4m, r.AliquotaIcmResolvida);
    }

    [Fact]
    public void Cadsub_encontrado_decide_a_aliquota_antes_do_cadicm()
    {
        // dentro do estado → icmproprio do cadsub
        var r = MotorFiscalService.Calcular(Contexto(
            cadsub: new CadsubResultado(ValIcm: 12m, ValIcmSu: 0m, IcmProprio: 20m, ReducBase: 0m, Prcmedio: 0m, Fcp: 0m, SubsTrib: 0m)));

        Assert.Equal(20m, r.AliquotaIcmResolvida);
    }

    // ── ICMS-ST (totitem2:1050-1207) ──

    private static ContextoFiscalItem ContextoSt(
        decimal precoUnitario = 100m,
        decimal quantidade = 2m,
        decimal prcmedio = 0m,
        decimal reducBase = 0m,
        decimal mva = 40m,
        decimal aliquotaSt = 18m)
    {
        return Contexto(
            quantidade: quantidade,
            precoUnitario: precoUnitario,
            cgcCpf: "C", // wind_ret=0 → ST roda
            permIcm: "F", // produto marcado para ST
            cadsub: new CadsubResultado(ValIcm: 0m, ValIcmSu: aliquotaSt, IcmProprio: 18m, ReducBase: reducBase, Prcmedio: prcmedio, Fcp: 0m, SubsTrib: mva));
    }

    [Fact]
    public void St_calcula_para_cliente_revendedor_R()
    {
        // Correção 24/08/2026: cgc_cpf "R" mantém wind_ret=0 (totitem2:761-767) — revenda
        // é exatamente o caso da substituição tributária. A 1ª versão bloqueava, invertido.
        var r = MotorFiscalService.Calcular(Contexto(cgcCpf: "R", permIcm: "F",
            cadsub: new CadsubResultado(0m, 18m, 18m, 0m, 0m, 0m, 40m)));

        Assert.True(r.ValorIcmSt > 0);
    }

    [Fact]
    public void St_nao_calcula_para_consumidor_final_F()
    {
        // cgc_cpf "F" (mesmo estado, sem DIFAL) → wind_ret=1 (totitem2:782-784) → sem ST
        var r = MotorFiscalService.Calcular(Contexto(cgcCpf: "F", permIcm: "F",
            cadsub: new CadsubResultado(0m, 18m, 18m, 0m, 0m, 0m, 40m)));

        Assert.Equal(0m, r.ValorIcmSt);
    }

    [Fact]
    public void St_sem_cadsub_nem_cadicm_com_aliquota_propria_exige_cst_de_substituicao()
    {
        // temsubs (totitem2:683-758): CST "000" (cod_proc) não é x10/x30/x70 → alíquota
        // herdada do cadmat é zerada → ST 0
        var semCst = MotorFiscalService.Calcular(Contexto(cgcCpf: "R", permIcm: "F", codProc: "000", valIcmSu: 22m, despSubst: 40m));
        // CST "010" → temsubs → alíquota do cadmat vale
        var comCst = MotorFiscalService.Calcular(Contexto(cgcCpf: "R", permIcm: "F", codProc: "010", valIcmSu: 22m, despSubst: 40m));

        Assert.Equal(0m, semCst.ValorIcmSt);
        Assert.True(comCst.ValorIcmSt > 0);
    }

    [Fact]
    public void St_nao_calcula_para_produto_sem_marcacao()
    {
        var r = MotorFiscalService.Calcular(Contexto(cgcCpf: "C", permIcm: "",
            cadsub: new CadsubResultado(0m, 18m, 18m, 0m, 0m, 0m, 40m)));

        Assert.Equal(0m, r.ValorIcmSt);
    }

    [Fact]
    public void St_formula_completa_mva_sobre_mercadoria_mais_ipi()
    {
        // mercadoria 200, sem desconto, IPI 10% = 20.
        // wba_calc1 = ROUND((200+20) × 40/100, 2) = 88,00
        // wba_calc2 = 200 + 88 + 20 = 308,00
        // wic_ret1  = 308 × 18% = 55,44
        // ICMS próprio: base (200+20 IPI consumidor) × aliq resolvida (icmproprio 18 do cadsub) = 39,60
        // wic_ret2  = ROUND(55,44 − 39,60, 2) = 15,84
        var r = MotorFiscalService.Calcular(ContextoSt());

        Assert.Equal(308m, r.BaseIcmSt);
        Assert.Equal(15.84m, r.ValorIcmSt);
        Assert.False(r.UsouPautaFiscal);
    }

    [Fact]
    public void St_com_pauta_fiscal_quando_preco_abaixo_de_90_por_cento_da_pauta()
    {
        // pauta 150, preço 100 (< 135): base = 150 × 2 + IPI 20 = 320; MVA ignorado (totitem2:1147-1158)
        var r = MotorFiscalService.Calcular(ContextoSt(prcmedio: 150m));

        Assert.True(r.UsouPautaFiscal);
        Assert.Equal(320m, r.BaseIcmSt);
    }

    [Fact]
    public void St_sem_pauta_quando_preco_dentro_dos_90_por_cento()
    {
        // pauta 105, preço 100 (>= 94,50): fórmula normal com MVA
        var r = MotorFiscalService.Calcular(ContextoSt(prcmedio: 105m));

        Assert.False(r.UsouPautaFiscal);
    }

    [Fact]
    public void St_aplica_reducao_de_base_do_cadastro_corrigindo_DF_R2()
    {
        // RF-060: redução vem de cadsub.reducbase — o WER original nunca aplicava (DF-R2)
        var semReducao = MotorFiscalService.Calcular(ContextoSt(reducBase: 0m));
        var comReducao = MotorFiscalService.Calcular(ContextoSt(reducBase: 50m));

        Assert.Equal(semReducao.BaseIcmSt / 2m, comReducao.BaseIcmSt);
    }

    // ── Rejeições explícitas (nunca calcular errado em silêncio) ──

    [Fact]
    public void Produto_com_beneficio_fiscal_e_rejeitado()
    {
        var ex = Assert.Throws<FiscalException>(() => MotorFiscalService.Calcular(Contexto(temBeneficio: true)));
        Assert.Equal("BENEFICIO_NAO_SUPORTADO", ex.Codigo);
    }

    [Fact]
    public void Cliente_suframa_e_rejeitado()
    {
        var ex = Assert.Throws<FiscalException>(() => MotorFiscalService.Calcular(Contexto(suframa: "12345", incideSuf: 2m)));
        Assert.Equal("SUFRAMA_NAO_SUPORTADO", ex.Codigo);
    }

    [Fact]
    public void Interestadual_pessoa_fisica_e_rejeitado_por_difal()
    {
        var ex = Assert.Throws<FiscalException>(() => MotorFiscalService.Calcular(Contexto(clienteEstado: "MG", cgcCpf: "F")));
        Assert.Equal("DIFAL_NAO_SUPORTADO", ex.Codigo);
    }

    // ── Total do item ──

    [Fact]
    public void Total_do_item_soma_mercadoria_menos_desconto_mais_ipi_mais_st()
    {
        var r = MotorFiscalService.Calcular(ContextoSt());

        Assert.Equal(r.ValorMercadoria - r.ValorDesconto + r.ValorIpi + r.ValorIcmSt, r.TotalItemComImpostos);
    }
}
