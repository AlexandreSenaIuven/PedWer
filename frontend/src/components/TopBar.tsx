import { useState } from 'react'
import type { UsuarioLogado } from '../api'

interface TopBarProps {
  empresas: { codigo: string; nome: string }[]
  empresaSelecionada: string
  usuario: UsuarioLogado
  onSair: () => void
}

function iniciais(nome: string) {
  const partes = nome.trim().split(/\s+/).filter(Boolean)
  if (partes.length === 0) return '?'
  return partes.length === 1 ? partes[0].slice(0, 2).toUpperCase() : (partes[0][0] + partes[partes.length - 1][0]).toUpperCase()
}

/** Barra superior — seletor de empresa (contexto global, não por pedido) e avatar do usuário logado com opção de saída. */
export function TopBar({ empresas, empresaSelecionada, usuario, onSair }: TopBarProps) {
  const [menuAberto, setMenuAberto] = useState(false)

  return (
    <div className="topbar">
      <div className="topbar-direita">
        <select
          className="topbar-empresa"
          value={empresaSelecionada}
          disabled
          title="Empresa escolhida no login — para trocar, saia e entre novamente"
          onChange={() => {}}
        >
          {empresas.map((e) => (
            <option key={e.codigo} value={e.codigo}>{e.nome}</option>
          ))}
        </select>

        <div className="topbar-usuario">
          <button type="button" className="topbar-avatar" onClick={() => setMenuAberto((v) => !v)} title={usuario.nome}>
            {iniciais(usuario.nome)}
          </button>
          {menuAberto && (
            <div className="topbar-menu" onMouseLeave={() => setMenuAberto(false)}>
              <div className="topbar-menu-usuario">
                {usuario.nome}
                {usuario.vendedorNome && <div className="topbar-menu-vendedor">Vendedor: {usuario.vendedorNome}</div>}
              </div>
              <button type="button" className="topbar-menu-item" onClick={onSair}>
                Sair
              </button>
            </div>
          )}
        </div>
      </div>
    </div>
  )
}
