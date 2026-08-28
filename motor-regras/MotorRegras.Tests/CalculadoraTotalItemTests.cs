namespace MotorRegras.Tests;

public class CalculadoraTotalItemTests
{
    [Fact]
    public void CalcularTotalItem_caso_simples()
    {
        var total = CalculadoraTotalItem.CalcularTotalItem(quantidade: 3m, precoUnitario: 10.5m);

        Assert.Equal(31.50m, total);
    }

    [Theory]
    [InlineData(3, 3.3335, 10.00)]
    [InlineData(1, 10.005, 10.01)]
    [InlineData(1, 10.004999, 10.00)]
    public void CalcularTotalItem_reproduz_a_formula_literal_do_RF210(decimal quantidade, decimal precoUnitario, decimal esperado)
    {
        // Nota sobre por que a fórmula é reproduzida literalmente (truncar a 3 casas, depois
        // arredondar a 2) mesmo que, em ARITMÉTICA DECIMAL EXATA como a do C#, essa sequência
        // seja matematicamente equivalente a arredondar direto a 2 casas (é possível provar que
        // "round half away from zero" nunca discorda entre as duas formas quando não há erro de
        // representação). A divergência real que RF-210 documenta vem do VFP calcular em ponto
        // flutuante binário (double), onde o produto `qtd*preco*100*10` pode ter erro de
        // representação e o INT() truncar um valor ligeiramente diferente do "verdadeiro". Como
        // esta biblioteca usa `decimal` (base 10 exata) deliberadamente — é a escolha certa para
        // dinheiro —, ela não reproduz esse artefato de ponto flutuante bit a bit. Se algum dia for
        // preciso conferir centavo a centavo contra o VFP, isso exigiria emular aritmética binária
        // de precisão dupla, o que é um requisito separado, não implementado aqui.
        var total = CalculadoraTotalItem.CalcularTotalItem(quantidade, precoUnitario);

        Assert.Equal(esperado, total);
    }
}
