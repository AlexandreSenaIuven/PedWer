using System.Data.OleDb;
using Integrador.Servico;
using Integrador.Vfp;
using static Integrador.Leitura.LeitorHelpers;

namespace Integrador.Escrita;

/// <summary>
/// `cligeral.dbf` — tabela livre na raiz da base (145.782 registros) onde o
/// ERP guarda os "dados gerais" do pedido preenchidos na tela "Dados Para
/// Entrega" (form ped_ph, aberto pelo rel_ped2 antes de imprimir o PED_WE).
///
/// Fiel ao fonte (ped_ph.Load): chave é SÓ `codigo` (`SET ORDER TO CLIGERAL`
/// + `SEEK pedido.codigo`; se EOF → APPEND BLANK + REPLACE codigo). Os
/// campos ligados na tela: `obs` (1ª observação), `obs2` (2ª observação) e
/// `cligeral.cond` — abreviação VFP de `condpag` C(32) — onde a "Referência"
/// é gravada. Endereço/bairro/cidade/UF são memvars só de impressão lá;
/// aqui também vão para ender_ent/bairro_ent/cidade_ent/uf_ent (colunas
/// que existem e ficam vazias no VFP). tipo_oper/es_mov/cli_for ficam em
/// branco como nos registros reais (ex.: AB17436A) — preencher quebraria a
/// busca do VFP, que faz SEEK só pelo código.
/// </summary>
public sealed class CligeralRepositorio
{
    public void Gravar(string codigoPedido, EntregaComandoDto entrega)
    {
        using var conn = VfpConexao.AbrirBase();

        var valores = new Dictionary<string, object?>
        {
            ["codigo"] = codigoPedido,
            ["obs"] = entrega.Observacao1 ?? "",
            ["obs2"] = entrega.Observacao2 ?? "",
            ["condpag"] = Truncar(entrega.Referencia, 32),     // = cligeral.cond no ped_ph (Referência)
            ["referencia"] = Truncar(entrega.Referencia, 50),  // coluna com o nome certo — redundante de propósito
            ["ender_ent"] = Truncar(entrega.Endereco, 55),
            ["bairro_ent"] = Truncar(entrega.Bairro, 60),
            ["cidade_ent"] = Truncar(entrega.Cidade, 60),
            ["uf_ent"] = Truncar(entrega.Estado, 2),
        };

        if (Existe(conn, codigoPedido))
        {
            using var cmd = new OleDbCommand(
                "UPDATE cligeral SET obs = ?, obs2 = ?, condpag = ?, referencia = ?, ender_ent = ?, bairro_ent = ?, cidade_ent = ?, uf_ent = ? WHERE codigo = ?",
                conn);
            foreach (var chave in new[] { "obs", "obs2", "condpag", "referencia", "ender_ent", "bairro_ent", "cidade_ent", "uf_ent" })
            {
                cmd.Parameters.AddWithValue("@" + chave, valores[chave]);
            }
            cmd.Parameters.AddWithValue("@codigo", codigoPedido);
            cmd.ExecuteNonQuery();
            return;
        }

        GravadorGenerico.InserirLinhaCompleta(conn, null, "cligeral", valores);
    }

    private static bool Existe(OleDbConnection conn, string codigo)
    {
        using var cmd = new OleDbCommand("SELECT codigo FROM cligeral WHERE codigo = ?", conn);
        cmd.Parameters.AddWithValue("@codigo", codigo);
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            if (ChaveExata(Str(reader, "codigo"), codigo)) return true; // RF-105: nunca confiar em "=" puro
        }
        return false;
    }

    private static string Truncar(string? valor, int largura)
    {
        var v = valor ?? "";
        return v.Length <= largura ? v : v[..largura];
    }
}
