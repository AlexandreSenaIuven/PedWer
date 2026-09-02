import type { ItemCompra } from '../api'

interface UltimasComprasProps {
  aberto: boolean
  clienteNome: string
  carregando: boolean
  erro: string | null
  compras: ItemCompra[] | null
  onFechar: () => void
}

/**
 * Botão "Últimas Compras" do ped_wer.scx original — mostra o histórico de
 * venda já faturada deste cliente (`cadmov`, não `pedido.dbf`). Consulta
 * sob demanda pelo console a cada abertura, não um dado sincronizado.
 */
export function UltimasCompras({ aberto, clienteNome, carregando, erro, compras, onFechar }: UltimasComprasProps) {
  if (!aberto) return null

  return (
    <div className="modal-fundo" onClick={onFechar}>
      <div className="modal-caixa" onClick={(e) => e.stopPropagation()}>
        <div className="modal-cabecalho">
          <h3>Últimas compras — {clienteNome}</h3>
          <button type="button" className="modal-fechar" onClick={onFechar} aria-label="Fechar">✕</button>
        </div>
        <div className="modal-resultados">
          {carregando && <p className="vazio">Consultando...</p>}
          {!carregando && erro && <p className="erro">{erro}</p>}
          {!carregando && !erro && compras && compras.length === 0 && <p className="vazio">Nenhuma compra faturada encontrada.</p>}
          {!carregando && !erro && compras && compras.length > 0 && (
            <table>
              <thead>
                <tr>
                  <th>Data</th>
                  <th>Nota Fiscal</th>
                  <th>Produto</th>
                  <th>Qtd</th>
                  <th>Valor unit.</th>
                  <th>Total</th>
                </tr>
              </thead>
              <tbody>
                {compras.map((c, i) => (
                  <tr key={i}>
                    <td>{new Date(c.dataMov + 'T00:00:00').toLocaleDateString('pt-BR')}</td>
                    <td>{c.notaFiscal}</td>
                    <td>{c.grupo}|{c.referencia}</td>
                    <td>{c.quantidade}</td>
                    <td>{c.valorUnitario.toFixed(2)}</td>
                    <td>{(c.quantidade * c.valorUnitario).toFixed(2)}</td>
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
