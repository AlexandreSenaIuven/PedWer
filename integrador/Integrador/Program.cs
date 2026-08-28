using System.Data.OleDb;
using Dominio;
using Integrador.Escrita;
using Integrador.Leitura;
using Integrador.Servico;
using Integrador.Vfp;
using MotorRegras;

// Console (Integrador) — Fases 0, 3 e 5 do plano: leitura contra a base via
// VFPOLEDB, e escrita de teste (autorizada em 20/08/2026).

var pastaBaseConfigurada = Environment.GetEnvironmentVariable("PEDWER_PASTA_BASE");
if (string.IsNullOrWhiteSpace(pastaBaseConfigurada))
{
    Console.WriteLine("Defina a variável de ambiente PEDWER_PASTA_BASE com o caminho da base VFP desta instalação (ex.: Z:\\BASES_CLIENTES\\WER).");
    return;
}
VfpConexao.PastaBase = pastaBaseConfigurada;

if (args.Length == 0)
{
    Console.WriteLine("Uso:");
    Console.WriteLine("  schema <tabela> [codigoEmpresa]   — descreve as colunas reais de uma tabela");
    Console.WriteLine("  credito <codigoCliente>            — avalia crédito do grupo econômico do cliente");
    Console.WriteLine("  preco <empresa> <cliente> <grupo> <referencia> <tipoPrc> <qtd> [precoDigitado]");
    Console.WriteLine("  testar-transacao                    — grava e desfaz um registro de teste em pedido.dbf");
    Console.WriteLine("  gravar-pedido-teste <empresa> <cliente> <grupo> <referencia> <qtd>");
    Console.WriteLine("  servico <urlApiCentral>              — laço contínuo: sincroniza dados e busca comandos (só saída, nunca escuta)");
    return;
}

switch (args[0])
{
    case "schema":
        ComandoSchema(args);
        break;
    case "credito":
        ComandoCredito(args);
        break;
    case "preco":
        ComandoPreco(args);
        break;
    case "amostra":
        ComandoAmostra();
        break;
    case "sql":
        // diagnóstico somente-leitura: sql <base|codigoEmpresa> <SELECT ...>
        ComandoSql(args[1], string.Join(' ', args.Skip(2)));
        break;
    case "campos-obrigatorios":
        Console.WriteLine(string.Join(", ", EsquemaTabela.ColunasObrigatorias(
            args.Length > 2 ? VfpConexao.PastaEmpresa(args[2]) : VfpConexao.Caminho("PEDIDO.DBC"), args[1])));
        break;
    case "testar-transacao":
        ComandoTestarTransacao();
        break;
    case "servico":
        await ComandoServicoAsync(args);
        break;
    case "gravar-pedido-teste":
        ComandoGravarPedidoTeste(args);
        break;
    case "consultar-pedido":
        ComandoConsultarPedido(args[1]);
        break;
    case "apagar-pedido":
        ComandoApagarPedido(args[1]);
        break;
    default:
        Console.WriteLine($"Comando desconhecido: {args[0]}");
        break;
}

void ComandoAmostra()
{
    // Ajuda a achar casos de teste reais (somente leitura) para os comandos "credito" e "preco".
    using (var conn = VfpConexao.AbrirBase())
    using (var cmd = new System.Data.OleDb.OleDbCommand(
        "SELECT codigo, razao_soc, cgc, posicao, credito, csll, cond_pag FROM clientes WHERE credito > 0", conn))
    using (var reader = cmd.ExecuteReader())
    {
        Console.WriteLine("--- clientes com limite de crédito > 0 (5 primeiros) ---");
        var n = 0;
        while (reader.Read() && n < 5)
        {
            Console.WriteLine($"  {reader["codigo"]} | {reader["razao_soc"]} | cgc={reader["cgc"]} | posicao={reader["posicao"]} | credito={reader["credito"]} | csll={reader["csll"]} | cond_pag={reader["cond_pag"]}");
            n++;
        }
    }

    using (var conn = VfpConexao.AbrirEmpresa("02"))
    using (var cmd = new System.Data.OleDb.OleDbCommand(
        "SELECT grupo, referencia, descricao, prc_venda, gradecol FROM cadmat WHERE prc_venda > 0", conn))
    using (var reader = cmd.ExecuteReader())
    {
        Console.WriteLine("--- produtos (fenwin02) com prc_venda > 0 (5 primeiros) ---");
        var n = 0;
        while (reader.Read() && n < 5)
        {
            Console.WriteLine($"  {reader["grupo"]}|{reader["referencia"]} | {reader["descricao"]} | prc_venda={reader["prc_venda"]} | gradecol={reader["gradecol"]}");
            n++;
        }
    }
}

void ComandoTestarTransacao()
{
    const string codigoTeste = "ZZCTESTE1"; // <= 10 chars: `codigo` em pedido.dbf é C(10) e trunca em silêncio
    var valoresConhecidos = new Dictionary<string, object?>
    {
        ["codigo"] = codigoTeste,
        ["es_mov"] = "S",
        ["tipo_oper"] = "PED",
        ["cod_cli"] = "0000184",
        ["data_ped"] = DateTime.Today,
    };

    Console.WriteLine("Passo 1: INSERT + ROLLBACK — o registro NÃO deve sobreviver.");
    using (var conn = VfpConexao.Abrir(VfpConexao.Caminho("PEDIDO.DBC")))
    {
        using var transacao = conn.BeginTransaction();
        var linhas = GravadorGenerico.InserirLinhaCompleta(conn, transacao, "pedido", valoresConhecidos);
        Console.WriteLine($"  INSERT afetou {linhas} linha(s). Revertendo...");
        transacao.Rollback();
    }

    var existeAposRollback = ExisteCodigo(codigoTeste);
    Console.WriteLine($"  Existe após ROLLBACK? {(existeAposRollback ? "SIM (transação NÃO funcionou)" : "não (transação funcionou)")}");

    Console.WriteLine("Passo 2: INSERT + COMMIT — o registro DEVE sobreviver.");
    using (var conn = VfpConexao.Abrir(VfpConexao.Caminho("PEDIDO.DBC")))
    {
        using var transacao = conn.BeginTransaction();
        GravadorGenerico.InserirLinhaCompleta(conn, transacao, "pedido", valoresConhecidos);
        transacao.Commit();
    }

    var existeAposCommit = ExisteCodigo(codigoTeste);
    Console.WriteLine($"  Existe após COMMIT? {(existeAposCommit ? "sim (esperado)" : "NÃO (algo errado)")}");

    Console.WriteLine("Limpando o registro de teste do passo 2...");
    using (var conn = VfpConexao.Abrir(VfpConexao.Caminho("PEDIDO.DBC")))
    using (var cmdDelete = new OleDbCommand("DELETE FROM pedido WHERE codigo = ?", conn))
    {
        cmdDelete.Parameters.AddWithValue("@codigo", codigoTeste);
        var apagados = cmdDelete.ExecuteNonQuery();
        Console.WriteLine($"  {apagados} registro(s) apagado(s).");
    }
}

bool ExisteCodigo(string codigo)
{
    using var conn = VfpConexao.Abrir(VfpConexao.Caminho("PEDIDO.DBC"));
    using var cmd = new OleDbCommand("SELECT codigo FROM pedido WHERE codigo = ?", conn);
    cmd.Parameters.AddWithValue("@codigo", codigo);
    using var reader = cmd.ExecuteReader();
    while (reader.Read())
    {
        if (reader["codigo"] is string c && c.Trim() == codigo) return true;
    }
    return false;
}

void ComandoGravarPedidoTeste(string[] a)
{
    var (empresa, codigoCliente, grupo, referencia, quantidadeTxt) = (a[1], a[2], a[3], a[4], a[5]);
    var quantidade = decimal.Parse(quantidadeTxt);
    var hoje = DateOnly.FromDateTime(DateTime.Today);

    var clienteRepo = new ClienteRepositorio();
    var cadmatRepo = new CadmatRepositorio();
    var vendedorRepo = new VendedorRepositorio();

    var cliente = clienteRepo.BuscarPorCodigo(codigoCliente);
    var produto = cadmatRepo.BuscarPorChave(empresa, grupo, referencia);
    if (cliente is null || produto is null)
    {
        Console.WriteLine("Cliente ou produto não encontrado.");
        return;
    }

    var fatores = new FatoresPrecoCliente(cliente.Inss, cliente.Irrf, cliente.Iss);
    var resultadoPreco = PrecificacaoService.CalcularPrecoItem(
        produto.PrcVenda, fatores, negociacao: null, precoDigitado: produto.PrcVenda, percentualTetoDesconto: 0m, dataReferencia: hoje);

    var pedido = new Pedido("PED", empresa, codigoCliente, hoje, autorCriacao: "asena")
    {
        ReferenciaExterna = $"ZC{DateTime.Now:HHmmss}", // <= 10 chars: `codigo` em pedido.dbf é C(10)
    };
    pedido.AdicionarItem(grupo, referencia, produto.Gradegrp, quantidade, resultadoPreco);

    var vendedor = vendedorRepo.BuscarPorCodigo(cliente.CodVendor);
    var tipoVendedor = vendedor?.TipoVend ?? "V";
    pedido.Fechar(cliente.CodVendor, string.IsNullOrWhiteSpace(cliente.CodVend2) ? cliente.CodVendor : cliente.CodVend2, tipoVendedor);

    Console.WriteLine($"Pedido de teste: referência {pedido.ReferenciaExterna}, 1 item, comissão {pedido.Itens[0].PercentualComissao}%");

    var linhas = MapeadorPedidoParaVfp.Mapear(pedido, esMov: "S", comprador: "TESTE_CLAUDE");

    try
    {
        new PedidoDbfRepositorio().GravarPedido(linhas);
        Console.WriteLine("Gravado com sucesso.");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Falhou: {ex.GetType().Name}: {ex.Message}");
        return;
    }

    var existe = ExisteCodigo(pedido.ReferenciaExterna);
    Console.WriteLine($"Confirmado por leitura: {(existe ? "presente" : "AUSENTE — algo errado")}");
}

void ComandoConsultarPedido(string codigo)
{
    using var conn = VfpConexao.Abrir(VfpConexao.Caminho("PEDIDO.DBC"));
    using var cmd = new OleDbCommand(
        "SELECT codigo, tipo_oper, cod_cli, cod_empr, cod_vend, cod_vend1, comprador, grupo, referencia, " +
        "qtd_itens, prc_venda, qtd_larg, qtd_comp, total_nota, qtditenspe FROM pedido WHERE codigo = ?",
        conn);
    cmd.Parameters.AddWithValue("@codigo", codigo);
    using var reader = cmd.ExecuteReader();
    var n = 0;
    while (reader.Read())
    {
        if ((reader["codigo"] as string)?.Trim() != codigo) continue;
        n++;
        Console.WriteLine($"  cod_cli={reader["cod_cli"]} cod_empr={reader["cod_empr"]} cod_vend={reader["cod_vend"]}/{reader["cod_vend1"]} comprador={reader["comprador"]}");
        Console.WriteLine($"  produto={reader["grupo"]}|{reader["referencia"]} qtd={reader["qtd_itens"]} prc_venda={reader["prc_venda"]} qtd_larg={reader["qtd_larg"]} qtd_comp={reader["qtd_comp"]} total_nota={reader["total_nota"]} qtditenspe={reader["qtditenspe"]}");
    }
    Console.WriteLine($"{n} linha(s) encontrada(s) para {codigo}.");
}

void ComandoApagarPedido(string codigo)
{
    using (var conn = VfpConexao.Abrir(VfpConexao.Caminho("PEDIDO.DBC")))
    using (var cmd = new OleDbCommand("DELETE FROM pedido WHERE codigo = ?", conn))
    {
        cmd.Parameters.AddWithValue("@codigo", codigo);
        Console.WriteLine($"{cmd.ExecuteNonQuery()} linha(s) apagada(s) em pedido.");
    }

    // auxiliar de teste: limpa também os "dados de entrega" do pedido (cligeral, tabela livre na raiz)
    using (var conn = VfpConexao.AbrirBase())
    using (var cmd = new OleDbCommand("DELETE FROM cligeral WHERE codigo = ?", conn))
    {
        cmd.Parameters.AddWithValue("@codigo", codigo);
        Console.WriteLine($"{cmd.ExecuteNonQuery()} linha(s) apagada(s) em cligeral.");
    }
}

async Task ComandoServicoAsync(string[] a)
{
    var urlApiCentral = a.Length > 1 ? a[1] : "http://localhost:3001";
    var empresas = new[] { "02", "03", VfpConexao.CodigoPrincipal };

    var api = new ApiCentralCliente(urlApiCentral);
    var sincronizacao = new LacoSincronizacao(api, empresas, TimeSpan.FromSeconds(30));
    var comandos = new LacoComandos(api, TimeSpan.FromSeconds(2));

    Console.WriteLine($"Serviço iniciado — API central: {urlApiCentral}");
    Console.WriteLine("Só faz chamadas de saída (sync a cada 30s, comandos a cada 2s). Ctrl+C para parar.");

    using var cts = new CancellationTokenSource();
    Console.CancelKeyPress += (_, e) => { e.Cancel = true; cts.Cancel(); };

    await sincronizacao.SincronizarUmaVezAsync(); // primeira sincronização já na subida, não espera 30s
    await Task.WhenAll(
        sincronizacao.ExecutarParaSempreAsync(cts.Token),
        comandos.ExecutarParaSempreAsync(cts.Token));
}

void ComandoSql(string alvo, string sql)
{
    if (!sql.TrimStart().StartsWith("SELECT", StringComparison.OrdinalIgnoreCase))
    {
        Console.WriteLine("Só SELECT é permitido neste comando de diagnóstico.");
        return;
    }

    var dataSource = alvo.Equals("base", StringComparison.OrdinalIgnoreCase) ? VfpConexao.PastaBase : VfpConexao.PastaEmpresa(alvo);
    using var conn = VfpConexao.Abrir(dataSource);
    using var cmd = new OleDbCommand(sql, conn);
    using var reader = cmd.ExecuteReader();

    while (reader.Read())
    {
        var partes = new List<string>();
        for (var i = 0; i < reader.FieldCount; i++)
        {
            partes.Add($"{reader.GetName(i)}=[{(reader.IsDBNull(i) ? "NULL" : reader.GetValue(i))}]");
        }
        Console.WriteLine(string.Join(" | ", partes));
    }
}

void ComandoSchema(string[] a)
{
    var tabela = a[1];
    var dataSource = a.Length > 2 ? VfpConexao.PastaEmpresa(a[2]) : VfpConexao.PastaBase;
    EsquemaTabela.Imprimir(dataSource, tabela);
}

void ComandoCredito(string[] a)
{
    var codigoCliente = a[1];
    var hoje = DateOnly.FromDateTime(DateTime.Today);

    var clienteRepo = new ClienteRepositorio();
    var vencimRepo = new VencimRepositorio();
    var creditoRepo = new CreditoRepositorio();

    var cliente = clienteRepo.BuscarPorCodigo(codigoCliente);
    if (cliente is null)
    {
        Console.WriteLine($"Cliente {codigoCliente} não encontrado.");
        return;
    }

    if (cliente.Cgc.Length < 10)
    {
        Console.WriteLine($"CGC do cliente ({cliente.Cgc}) tem menos de 10 dígitos — não é possível formar a raiz do grupo econômico.");
        return;
    }

    var raizCgc = cliente.Cgc[..10];
    var grupo = clienteRepo.BuscarGrupoEconomicoPorRaizCgc(raizCgc);
    var matriz = grupo.FirstOrDefault(c => c.Posicao == "M") ?? cliente;

    Console.WriteLine($"Cliente: {cliente.RazaoSoc} ({cliente.Codigo})");
    Console.WriteLine($"Grupo econômico (raiz CGC {raizCgc}): {grupo.Count} cliente(s)");
    Console.WriteLine($"Matriz: {matriz.RazaoSoc} ({matriz.Codigo}) — limite {matriz.Credito:C}");

    var codigosDoGrupo = grupo.Select(c => c.Codigo).ToList();
    var titulosAbertos = creditoRepo.BuscarTitulosAbertosDoGrupo(codigosDoGrupo);
    Console.WriteLine($"Títulos abertos no grupo: {titulosAbertos.Count}");

    var vencim = string.IsNullOrWhiteSpace(cliente.CondPag) ? null : vencimRepo.BuscarPorCodigo(cliente.CondPag);
    var diasTolerancia = (int)cliente.Cofins; // RF-192: clientes.COFINS é a tolerância de atraso em dias

    var titulosParaAvaliacao = titulosAbertos
        .Where(t => t.DtVencim.HasValue)
        .Select(t => new TituloAberto(t.Valor, DateOnly.FromDateTime(t.DtVencim!.Value)))
        .ToList();

    var resultado = AvaliacaoCreditoService.Avaliar(
        titulosParaAvaliacao,
        matriz.Credito,
        diasTolerancia,
        valorPedidoAtual: 0m,
        dataReferencia: hoje);

    Console.WriteLine($"Saldo devedor do grupo: {resultado.SaldoDevedorGrupo:C}");
    Console.WriteLine($"Status: {resultado.Status}");
}

void ComandoPreco(string[] a)
{
    var (empresa, codigoCliente, grupo, referencia, tipoPrc, quantidadeTxt) = (a[1], a[2], a[3], a[4], a[5], a[6]);
    decimal? precoDigitado = a.Length > 7 ? decimal.Parse(a[7]) : null;
    var hoje = DateOnly.FromDateTime(DateTime.Today);

    var clienteRepo = new ClienteRepositorio();
    var cadmatRepo = new CadmatRepositorio();
    var negociaRepo = new NegociaRepositorio();
    var tabcolRepo = new TabcolRepositorio();

    var cliente = clienteRepo.BuscarPorCodigo(codigoCliente);
    var produto = cadmatRepo.BuscarPorChave(empresa, grupo, referencia);
    if (cliente is null || produto is null)
    {
        Console.WriteLine("Cliente ou produto não encontrado.");
        return;
    }

    var fatores = new FatoresPrecoCliente(cliente.Inss, cliente.Irrf, cliente.Iss);
    var negociacaoDto = negociaRepo.BuscarPorChave(codigoCliente, grupo, referencia, tipoPrc);
    var negociacao = negociacaoDto is { CodValida: 1 } && negociacaoDto.DtVenc.HasValue
        ? new NegociacaoVigente(negociacaoDto.Preco, DateOnly.FromDateTime(negociacaoDto.DtVenc.Value), true)
        : null;

    var tabcol = tabcolRepo.BuscarPorCodigo(empresa, produto.Gradecol);
    var tetoColuna = TabcolRepositorio.NomeComoValor(tabcol);
    var tetoTotal = cliente.Pis + tetoColuna; // RF-083: descontoEspecialTeto (PIS) + teto por coluna de grade

    try
    {
        var resultado = PrecificacaoService.CalcularPrecoItem(produto.PrcVenda, fatores, negociacao, precoDigitado, tetoTotal, hoje);
        Console.WriteLine($"Preço de tabela ajustado: {resultado.PrecoTabelaAjustado:C}");
        Console.WriteLine($"Preço final: {resultado.PrecoFinal:C}");
        Console.WriteLine($"% desconto: {resultado.PercentualDesconto:F2}%");
        Console.WriteLine($"Origem: {resultado.Origem}");
    }
    catch (PrecificacaoException ex)
    {
        Console.WriteLine($"Rejeitado: [{ex.Codigo}] {ex.Message}");
    }
}
