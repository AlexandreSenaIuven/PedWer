using System.Data.OleDb;
using Integrador.Vfp;
using static Integrador.Leitura.LeitorHelpers;

namespace Integrador.Leitura;

/// <summary>
/// `wareas`/`wareascp`/`wareasb` — cada uma é um registro ÚNICO e global
/// (confirmado: `wareas` tem 1 registro na raiz). São os parâmetros que a
/// análise mede como "~45% inertes por configuração nesta instalação" — a
/// Fase 8 do plano exige reconferir isso na instalação real do cliente, não
/// assumir o snapshot.
/// </summary>
public sealed class ParametrosRepositorio
{
    public WareasDto CarregarWareas()
    {
        using var conn = VfpConexao.AbrirBase();
        using var cmd = new OleDbCommand(
            "SELECT cont_ped, comp_seq, ind_ped_dc, ind_ver_fi, tp_pedido, cons_min, " +
            "ind_grade, ind_tot_n, ind_impped, ind_linha, ind_pre, ind_nota, prox_nota FROM wareas",
            conn);
        using var reader = cmd.ExecuteReader();
        if (!reader.Read()) throw new InvalidOperationException("wareas está vazia — esperado 1 registro único.");

        return new WareasDto(
            Str(reader, "cont_ped"),
            Str(reader, "comp_seq"),
            Str(reader, "ind_ped_dc"),
            Str(reader, "ind_ver_fi"),
            Str(reader, "tp_pedido"),
            Dec(reader, "cons_min"),
            Str(reader, "ind_grade"),
            Str(reader, "ind_tot_n"),
            Str(reader, "ind_impped"),
            Dec(reader, "ind_linha"),
            Str(reader, "ind_pre"),
            Str(reader, "ind_nota"),
            Dec(reader, "prox_nota"));
    }

    public WareascpDto CarregarWareascp()
    {
        using var conn = VfpConexao.AbrirBase();
        using var cmd = new OleDbCommand("SELECT especifico, ind_prazo, ind_largco, ind_arrend, ind_odbc FROM wareascp", conn);
        using var reader = cmd.ExecuteReader();
        if (!reader.Read()) throw new InvalidOperationException("wareascp está vazia — esperado 1 registro único.");

        return new WareascpDto(
            Str(reader, "especifico"),
            Str(reader, "ind_prazo"),
            Str(reader, "ind_largco"),
            Str(reader, "ind_arrend"),
            Str(reader, "ind_odbc"));
    }

    public WareasbDto CarregarWareasb()
    {
        using var conn = VfpConexao.AbrirBase();
        using var cmd = new OleDbCommand("SELECT indcredito, limite_ped, bloq_pedid FROM wareasb", conn);
        using var reader = cmd.ExecuteReader();
        if (!reader.Read()) throw new InvalidOperationException("wareasb está vazia — esperado 1 registro único.");

        return new WareasbDto(
            Str(reader, "indcredito"),
            Dec(reader, "limite_ped"),
            Str(reader, "bloq_pedid"));
    }
}
