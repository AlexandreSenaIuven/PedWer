namespace MotorRegras;

/// <summary>
/// Os três fatores multiplicativos do cadastro do cliente que ajustam o preço
/// de tabela (RF-070). Nomes de negócio (decisão L10) — os campos físicos do
/// VFP (`clientes.INSS/IRRF/ISS`) só existem no contrato interno com o
/// console C#, nunca aqui.
/// </summary>
public sealed record FatoresPrecoCliente(
    decimal PercentualAdicao,
    decimal CompensacaoIcms,
    decimal DescontoDiretoria);
