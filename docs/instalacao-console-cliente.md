# Instalação do console (Integrador) na máquina do cliente

O console é o **único** componente que roda no servidor do cliente — front,
API e motor de regras ficam centralizados (hoje neste servidor de
demonstração, `pedwer.iuven.com.br` / `177.39.19.101/pedwer/`). O console só
faz chamadas de **saída**: nunca abre porta, nunca recebe conexão. Ele lê os
DBFs via VFPOLEDB e envia snapshots para a API central a cada 30s, além de
buscar comandos pendentes (novo pedido, gravar dados de entrega) a cada 2s.

## Pré-requisitos no servidor do cliente

1. Windows Server (ou Windows 10/11) de 64 bits — o console roda em modo
   32 bits (WoW64, obrigatório por causa do VFPOLEDB), compatível com
   qualquer Windows 64 bits atual.
2. Acesso de leitura/escrita à pasta onde ficam os DBFs do WER (a mesma
   pasta que o `pedwer.app`/ERP já usa nessa máquina).
3. **Microsoft OLE DB Provider for Visual FoxPro 9.0** (VFPOLEDB) instalado.
   Verificar antes de instalar qualquer coisa nova (PowerShell como
   administrador):
   ```powershell
   Get-ItemProperty 'HKLM:\SOFTWARE\Classes\VFPOLEDB.1' -ErrorAction SilentlyContinue
   ```
   Se não retornar nada, precisa instalar. Os links antigos da Microsoft
   (download id 14839) foram descontinuados; a fonte atual é o repositório
   VFPX (sucessor oficial do Visual FoxPro, mantido pela Microsoft/
   comunidade) no GitHub — buscar `VFPOLEDBSetup.msi` em
   `github.com/VFPX/VFP9SP2Hotfix3` (~1,2MB). Instalação silenciosa:
   ```powershell
   msiexec /i VFPOLEDBSetup.msi /quiet /norestart
   ```
4. Acesso de **saída** (outbound) HTTPS para a API central (porta 443) —
   nenhuma porta precisa ser aberta de entrada no servidor do cliente, nem
   regra de NAT/firewall além de permitir esse tráfego saindo.
5. **Não precisa instalar .NET.** A build é publicada *self-contained*
   (runtime embutido no próprio `.exe`) — um binário só, sem dependência do
   que já está (ou não está) instalado na máquina do cliente.

## Empacotar (feito uma vez, aqui no ambiente de build)

```powershell
dotnet publish integrador/Integrador -c Release -r win-x86 --self-contained true -p:PublishSingleFile=true -o <pasta-de-saida>
```

Gera um `Integrador.exe` único (~65MB), sem DLLs soltas nem dependência de
runtime instalado. Testado e confirmado rodando standalone (25/08/2026).

## Levar para o cliente

1. Copiar `Integrador.exe` para uma pasta no servidor do cliente, ex.:
   `C:\PedWer\Integrador.exe`.
2. Copiar também `nssm.exe` (mesmo utilitário usado neste servidor de
   demonstração, `nssm.cc`) para essa pasta.

## Configurar e instalar como serviço Windows

PowerShell como Administrador, no servidor do cliente:

```powershell
# 1. Registrar o serviço (ajustar a URL da API central)
C:\PedWer\nssm.exe install PedWerIntegrador C:\PedWer\Integrador.exe "servico https://pedwer.iuven.com.br"

# 2. Apontar para a pasta REAL dos DBFs deste cliente — cada instalação tem a sua
C:\PedWer\nssm.exe set PedWerIntegrador AppEnvironmentExtra "PEDWER_PASTA_BASE=<caminho real dos DBFs deste cliente>"

# 3. Diretório de trabalho e logs
C:\PedWer\nssm.exe set PedWerIntegrador AppDirectory C:\PedWer
C:\PedWer\nssm.exe set PedWerIntegrador AppStdout C:\PedWer\service-out.log
C:\PedWer\nssm.exe set PedWerIntegrador AppStderr C:\PedWer\service-err.log
C:\PedWer\nssm.exe set PedWerIntegrador AppRotateFiles 1

# 4. Reinício automático (a API central pode não estar de pé no exato instante do boot)
C:\PedWer\nssm.exe set PedWerIntegrador AppRestartDelay 10000

# 5. Iniciar
Start-Service PedWerIntegrador
```

`PEDWER_PASTA_BASE` é obrigatório desde 26/08/2026 — antes disso o caminho
da base vinha fixo no código (`Z:\BASES_CLIENTES\WER`, o caminho deste
servidor de testes); sem essa variável configurada, o console recusa
iniciar com uma mensagem clara em vez de apontar silenciosamente para a
base errada.

## Verificar que está funcionando

- `Get-Content C:\PedWer\service-out.log -Tail 5` — deve mostrar linhas
  como `sincronização ok em ...ms (N clientes, M produtos)` a cada 30s.
- Do lado central, checar `GET https://pedwer.iuven.com.br/sincronizacao/status`
  — `totalClientes`/`empresasComProdutos` devem refletir os dados reais
  desse cliente específico (não os de outro cliente já instalado).

## Se algo falhar

- **"Defina a variável de ambiente PEDWER_PASTA_BASE"** logo na subida →
  passo 2 não foi feito ou está com o caminho errado.
- **"sincronização falhou: ..."** no log → geralmente é a URL da API
  central errada/inacessível (testar `Test-NetConnection pedwer.iuven.com.br -Port 443`
  no servidor do cliente) ou VFPOLEDB não instalado (repetir a checagem do
  pré-requisito 3).
- Erro de conexão OLE DB ao abrir a base → confirmar que o caminho em
  `PEDWER_PASTA_BASE` existe e que a conta que roda o serviço (por padrão,
  `Local System`) tem permissão de leitura nele.

## Múltiplos clientes

Cada cliente é uma instalação independente do console, com seu próprio
`PEDWER_PASTA_BASE`, todos apontando para a **mesma** API central. Não há
necessidade de nada específico por cliente do lado da API — o
`referenciaExterna`/comandos já carregam contexto suficiente; se no futuro
for preciso distinguir de qual cliente veio cada sincronização, isso ainda
não existe (lacuna registrada, não implementada).
