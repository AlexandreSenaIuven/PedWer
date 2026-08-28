namespace MotorRegras.Fiscal;

public sealed record ResultadoFiscalItem(
    decimal ValorMercadoria, // pnTotitem (mf_totproduto)
    decimal ValorDesconto, // wind_desc
    decimal AliquotaIcmResolvida, // wicm (saída do veicm3)
    decimal BaseIpi, // wipi1 antes da alíquota
    decimal ValorIpi, // wipi1
    decimal BaseIcm, // wwicm (após IPI-na-base p/ consumidor final)
    decimal ValorIcm, // wwicm1 — ICMS próprio (destacado, já embutido no preço)
    decimal BaseIcmSt, // wba_calc2
    decimal AliquotaIcmSt, // WVAL_ICM_SU
    decimal MvaAplicado, // wdesp_subst
    bool UsouPautaFiscal,
    decimal ValorIcmSt, // wic_ret2 — este SIM soma ao total da nota
    decimal TotalItemComImpostos); // mercadoria - desconto + IPI + ST

public sealed class FiscalException(string codigo, string message) : Exception(message)
{
    public string Codigo { get; } = codigo;
}
