import type { SaldoEmpresa } from '../api'

interface SaldoGeralProps {
  aberto: boolean
  produtoNome: string
  carregando: boolean
  erro: string | null
  saldos: SaldoEmpresa[] | null
  onFechar: () => void
}

/**
 * Botão "Saldo Geral" do ped_wer.scx original — saldo de estoque
 * (`cadmat.qtdreal`/`qt_reserva`) deste produto em cada empresa real (nunca
 * a "PRINCIPAL", que é catálogo, não estoque físico). Definição não
 * confirmada no código-fonte original — melhor interpretação disponível,
 * conforme conversa com o usuário (02/09/2026): "provavelmente saldo do
 * produto em todos os estoques".
 */
export function SaldoGeral({ aberto, produtoNome, carregando, erro, saldos, onFechar }: SaldoGeralProps) {
  if (!aberto) return null

  const total = saldos?.reduce((acc, s) => acc + (s.qtdReal - s.qtReserva), 0) ?? 0

  return (
    <div className="modal-fundo" onClick={onFechar}>
      <div className="modal-caixa" onClick={(e) => e.stopPropagation()}>
        <div className="modal-cabecalho">
          <h3>Saldo geral — {produtoNome}</h3>
          <button type="button" className="modal-fechar" onClick={onFechar} aria-label="Fechar">✕</button>
        </div>
        <div className="modal-resultados">
          {carregando && <p className="vazio">Consultando...</p>}
          {!carregando && erro && <p className="erro">{erro}</p>}
          {!carregando && !erro && saldos && saldos.length === 0 && <p className="vazio">Nenhum saldo encontrado.</p>}
          {!carregando && !erro && saldos && saldos.length > 0 && (
            <>
              <table>
                <thead>
                  <tr>
                    <th>Empresa</th>
                    <th>Qtd. real</th>
                    <th>Reservado</th>
                    <th>Disponível</th>
                  </tr>
                </thead>
                <tbody>
                  {saldos.map((s) => (
                    <tr key={s.codigoEmpresa}>
                      <td>{s.codigoEmpresa}</td>
                      <td>{s.qtdReal}</td>
                      <td>{s.qtReserva}</td>
                      <td>{s.qtdReal - s.qtReserva}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
              <p className="modal-total">Total disponível: {total}</p>
            </>
          )}
        </div>
      </div>
    </div>
  )
}
