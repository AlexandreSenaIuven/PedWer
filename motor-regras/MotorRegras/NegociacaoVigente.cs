namespace MotorRegras;

/// <summary>
/// Uma negociação de preço já filtrada pelas três guardas do cadastro
/// (encontrada, dentro da validade, autorizada — RF-075/076). Quem monta este
/// objeto decide se a negociação se aplica; o motor só recebe o resultado.
/// </summary>
public sealed record NegociacaoVigente(
    decimal PrecoNegociado,
    DateOnly DataValidade,
    bool Autorizada);
