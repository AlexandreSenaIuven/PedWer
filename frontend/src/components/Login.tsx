import { useState } from 'react'
import type { FormEvent } from 'react'
import { api, type UsuarioLogado } from '../api'

interface LoginProps {
  empresas: { codigo: string; nome: string }[]
  onEntrar: (usuario: UsuarioLogado, empresaCodigo: string) => void
}

/**
 * Tela de entrada — contra `caduser` (RF de login web, 25/08/2026). A
 * empresa é escolhida aqui (não muda durante o pedido — RF-021: "a empresa
 * precisa ser conhecida antes do produto"): `tabplan` mais a opção
 * PRINCIPAL (estoque principal, `cod_empr` gravado em branco no pedido).
 */
export function Login({ empresas, onEntrar }: LoginProps) {
  const [usuario, setUsuario] = useState('')
  const [senha, setSenha] = useState('')
  const [empresaCodigo, setEmpresaCodigo] = useState(empresas[0]?.codigo ?? '')
  const [mostrarSenha, setMostrarSenha] = useState(false)
  const [carregando, setCarregando] = useState(false)
  const [erro, setErro] = useState<string | null>(null)

  async function aoEnviar(e: FormEvent) {
    e.preventDefault()
    if (!usuario.trim() || !senha || !empresaCodigo) return
    setCarregando(true)
    setErro(null)
    try {
      const logado = await api.login(usuario.trim(), senha)
      onEntrar(logado, empresaCodigo)
    } catch (err) {
      setErro(err instanceof Error ? err.message : 'Não foi possível entrar.')
    } finally {
      setCarregando(false)
    }
  }

  return (
    <div className="login-pagina">
      <form className="login-caixa" onSubmit={aoEnviar}>
        <h1 className="login-titulo">PEDWER WEB</h1>
        <p className="login-subtitulo">Use o usuário cadastrado pela sua empresa.</p>

        <label className="login-campo">
          Usuário
          <input
            value={usuario}
            onChange={(e) => setUsuario(e.target.value.toUpperCase())}
            autoFocus
            autoComplete="username"
            autoCapitalize="characters"
          />
        </label>

        <label className="login-campo">
          Senha
          <div className="login-campo-senha">
            <input
              type={mostrarSenha ? 'text' : 'password'}
              value={senha}
              onChange={(e) => setSenha(e.target.value)}
              autoComplete="current-password"
            />
            <button type="button" className="login-mostrar" onClick={() => setMostrarSenha((v) => !v)}>
              {mostrarSenha ? 'ocultar' : 'mostrar'}
            </button>
          </div>
        </label>

        <label className="login-campo">
          Empresa
          <select value={empresaCodigo} onChange={(e) => setEmpresaCodigo(e.target.value)}>
            {empresas.map((e) => (
              <option key={e.codigo} value={e.codigo}>
                {e.nome}
              </option>
            ))}
          </select>
        </label>

        {erro && <p className="erro">{erro}</p>}

        <button type="submit" className="primario login-entrar" disabled={carregando}>
          {carregando ? 'Entrando...' : 'Entrar'}
        </button>

        <p className="login-rodape">Sem acesso? Fale com o administrador da sua empresa.</p>
      </form>
    </div>
  )
}
