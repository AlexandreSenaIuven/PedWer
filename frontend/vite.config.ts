import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'

// https://vite.dev/config/
export default defineConfig({
  plugins: [react()],
  // Relativo: o mesmo build serve tanto na raiz de um domínio próprio
  // (https://pedwer.iuven.com.br/) quanto num subcaminho atrás de outro
  // site (http://<ip>/pedwer/) — caminho absoluto ("/assets/...") quebraria
  // o segundo caso.
  base: './',
})
