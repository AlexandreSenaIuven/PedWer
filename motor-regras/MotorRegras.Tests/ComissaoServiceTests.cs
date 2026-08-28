namespace MotorRegras.Tests;

public class ComissaoServiceTests
{
    [Theory]
    // negocio=1 (tipo "V"): degraus 0->5, 10->3, 20->2, 30->2, 99.99->2
    [InlineData("V", -5, 5)] // aumento de preço: cai na faixa máxima
    [InlineData("V", 0, 5)]
    [InlineData("V", 2.5, 5)] // decisão do usuário: PISO — mantém a faixa anterior até cruzar o degrau
    [InlineData("V", 9.999, 5)]
    [InlineData("V", 10, 3)] // no degrau exato já entra na nova faixa
    [InlineData("V", 15, 3)]
    [InlineData("V", 30, 2)]
    [InlineData("V", 99.99, 2)]
    [InlineData("V", 100, 2)] // acima do último degrau ainda usa o último
    public void ResolverPercentualComissao_escada_negocio1_usa_piso(string tipoVendedor, decimal desconto, decimal esperado)
    {
        var resultado = ComissaoService.ResolverPercentualComissao(tipoVendedor, desconto);

        Assert.Equal(esperado, resultado);
    }

    [Theory]
    // negocio=2 (todo o resto — R, I, branco): degraus 0->10, 10->7, 20->5, 30->3, 99.99->3
    [InlineData("R", 5, 10)]
    [InlineData("I", 12, 7)]
    [InlineData("", 25, 5)]
    public void ResolverPercentualComissao_escada_negocio2_para_tipos_nao_V(string tipoVendedor, decimal desconto, decimal esperado)
    {
        var resultado = ComissaoService.ResolverPercentualComissao(tipoVendedor, desconto);

        Assert.Equal(esperado, resultado);
    }

    [Fact]
    public void ResolverPercentualComissao_tipo_nao_mapeado_rejeita_RF091()
    {
        var ex = Assert.Throws<ComissaoException>(() =>
            ComissaoService.ResolverPercentualComissao("X", 5m));

        Assert.Equal("TIPO_VENDEDOR_NAO_MAPEADO", ex.Codigo);
    }
}
