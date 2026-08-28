using Integrador.Leitura;

namespace Integrador.Servico;

/// <summary>
/// Empurra periodicamente uma cópia dos dados de referência para a API
/// central — nunca o contrário. É a metade "leitura" do modelo: a API
/// central passa a responder consultas (cliente, produto, crédito) com essa
/// cópia, sem depender do console estar de pé a cada clique.
///
/// Aceita ficar um pouco atrasado (o intervalo de sincronização) em troca de
/// nenhuma porta aberta na máquina do cliente — troca deliberada, registrada
/// para quem for operar isto: preço/crédito na tela podem estar até
/// `intervalo` desatualizados em relação ao VFP.
/// </summary>
public sealed class LacoSincronizacao(ApiCentralCliente api, string[] empresas, TimeSpan intervalo)
{
    private readonly ClienteRepositorio _clientes = new();
    private readonly CadmatRepositorio _produtos = new();
    private readonly VendedorRepositorio _vendedores = new();
    private readonly UsuarioRepositorio _usuarios = new();
    private readonly CreditoRepositorio _credito = new();
    private readonly TabcolRepositorio _tabcol = new();
    private readonly TiposRepositorio _tipos = new();
    private readonly VencimRepositorio _vencim = new();
    private readonly CadsubRepositorio _cadsub = new();
    private readonly CadicmRepositorio _cadicm = new();
    private readonly TabplanRepositorio _tabplan = new();
    private readonly NegociaRepositorio _negocia = new();

    public async Task ExecutarParaSempreAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                var cronometro = System.Diagnostics.Stopwatch.StartNew();
                var resumo = await SincronizarUmaVezAsync();
                Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] sincronização ok em {cronometro.ElapsedMilliseconds}ms ({resumo}).");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] sincronização falhou: {ex.Message}");
            }

            await Task.Delay(intervalo, ct);
        }
    }

    public async Task<string> SincronizarUmaVezAsync()
    {
        // TODOS os registros, não amostras — a busca da tela acontece sobre esta
        // cópia; o que não sincronizar simplesmente não existe para o usuário
        // (bug dos "só 20 itens" / 499 clientes, corrigido em 24/08/2026).
        var clientes = _clientes.ListarTodos();
        await api.SincronizarAsync("/sincronizacao/clientes", clientes);
        await api.SincronizarAsync("/sincronizacao/vendedores", _vendedores.ListarTodos());
        await api.SincronizarAsync("/sincronizacao/usuarios", _usuarios.ListarTodos());
        await api.SincronizarAsync("/sincronizacao/titulos", _credito.ListarTodosAbertos());
        await api.SincronizarAsync("/sincronizacao/tipos-operacao", _tipos.ListarTodos());
        await api.SincronizarAsync("/sincronizacao/condicoes-pagamento", _vencim.ListarTodos());
        await api.SincronizarAsync("/sincronizacao/empresas", _tabplan.ListarTodos());
        await api.SincronizarAsync("/sincronizacao/negociacoes", _negocia.ListarTodos());

        var totalProdutos = 0;
        foreach (var empresa in empresas)
        {
            var produtos = _produtos.ListarTodos(empresa);
            totalProdutos += produtos.Count;
            await api.SincronizarAsync($"/sincronizacao/produtos/{empresa}", produtos);
            await api.SincronizarAsync($"/sincronizacao/tabcol/{empresa}", _tabcol.ListarTodos(empresa));
            await api.SincronizarAsync($"/sincronizacao/cadsub/{empresa}", _cadsub.ListarTodos(empresa));
            await api.SincronizarAsync($"/sincronizacao/cadicm/{empresa}", _cadicm.ListarTodos(empresa));
        }

        return $"{clientes.Count} clientes, {totalProdutos} produtos";
    }
}
