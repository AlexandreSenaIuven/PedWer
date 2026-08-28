using System.Net.Http.Json;
using System.Text.Json;

namespace Integrador.Servico;

/// <summary>
/// Único ponto de saída para a API central (Node). O console NUNCA recebe
/// conexão — só inicia. Sincronização (push de dados de referência) e busca
/// de comandos pendentes (poll) passam por aqui.
/// </summary>
public sealed class ApiCentralCliente
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private readonly HttpClient _http;

    public ApiCentralCliente(string baseUrl)
    {
        _http = new HttpClient { BaseAddress = new Uri(baseUrl) };
    }

    public async Task SincronizarAsync(string caminho, object payload)
    {
        var resposta = await _http.PostAsJsonAsync(caminho, payload, Json);
        resposta.EnsureSuccessStatusCode();
    }

    public async Task<List<ComandoPendenteDto>> BuscarComandosPendentesAsync()
    {
        var resultado = await _http.GetFromJsonAsync<List<ComandoPendenteDto>>("/comandos/pendentes", Json);
        return resultado ?? new List<ComandoPendenteDto>();
    }

    public async Task ReportarResultadoAsync(string comandoId, ResultadoComandoRequest resultado)
    {
        var resposta = await _http.PostAsJsonAsync($"/comandos/{comandoId}/resultado", resultado, Json);
        resposta.EnsureSuccessStatusCode();
    }
}
