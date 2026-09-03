import type { ItemGiro } from '../api'

interface GiroProps {
  aberto: boolean
  produtoNome: string
  carregando: boolean
  erro: string | null
  giro: ItemGiro[] | null
  onFechar: () => void
}

/**
 * Botão "Giro" do ped_wer.scx original — histórico de venda faturada deste
 * PRODUTO (mesma origem de "Últimas Compras", `cadmov`, só que filtrada por
 * produto em vez de cliente). Definição não confirmada no código-fonte
 * original (VCX/SCT não legível com segurança) — ver comentário em
 * `CadmovRepositorio.ListarGiro` no console.
 */
export function Giro({ aberto, produtoNome, carregando, erro, giro, onFechar }: GiroProps) {
  if (!aberto) return null

  return (
    <div className="modal-fundo" onClick={onFechar}>
      <div className="modal-caixa" onClick={(e) => e.stopPropagation()}>
        <div className="modal-cabecalho">
          <h3>Giro — {produtoNome}</h3>
          <button type="button" className="modal-fechar" onClick={onFechar} aria-label="Fechar">✕</button>
        </div>
        <div className="modal-resultados">
          {carregando && <p className="vazio">Consultando...</p>}
          {!carregando && erro && <p className="erro">{erro}</p>}
          {!carregando && !erro && giro && giro.length === 0 && <p className="vazio">Nenhuma venda faturada encontrada.</p>}
          {!carregando && !erro && giro && giro.length > 0 && (
            <table>
              <thead>
                <tr>
                  <th>Data</th>
                  <th>Nota Fiscal</th>
                  <th>Cliente</th>
                  <th>Qtd</th>
                  <th>Valor unit.</th>
                  <th>Total</th>
                </tr>
              </thead>
              <tbody>
                {giro.map((g, i) => (
                  <tr key={i}>
                    <td>{new Date(g.dataMov + 'T00:00:00').toLocaleDateString('pt-BR')}</td>
                    <td>{g.notaFiscal}</td>
                    <td>
                      {g.cliFor}
                      {g.clienteNome ? ` — ${g.clienteNome}` : ''}
                    </td>
                    <td>{g.quantidade}</td>
                    <td>{g.valorUnitario.toFixed(2)}</td>
                    <td>{(g.quantidade * g.valorUnitario).toFixed(2)}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          )}
        </div>
      </div>
    </div>
  )
}
