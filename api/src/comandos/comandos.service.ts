import { randomUUID } from 'crypto';
import { Injectable, Logger, NotFoundException } from '@nestjs/common';
import { ComandoCriarPedido, ItemComando } from './comandos.types';

/**
 * Fila de comandos "criar pedido" — o console faz poll de
 * GET /comandos/pendentes (nunca recebe push) e reporta o resultado depois.
 * Em memória por ora; o mesmo aviso do CacheService vale aqui: precisa de
 * persistência real (Postgres) antes de valer para produção — um restart
 * da API perde comandos em andamento.
 */
@Injectable()
export class ComandosService {
  private readonly logger = new Logger(ComandosService.name);
  private comandos = new Map<string, ComandoCriarPedido>();

  criar(dados: Omit<ComandoCriarPedido, 'id' | 'status' | 'criadoEm'>): ComandoCriarPedido {
    const comando: ComandoCriarPedido = {
      ...dados,
      id: randomUUID(),
      status: 'Pendente',
      criadoEm: new Date(),
    };
    this.comandos.set(comando.id, comando);
    return comando;
  }

  /** O console chama isto — marca como "Processando" na hora para não pegar o mesmo comando duas vezes. */
  buscarPendentesEMarcarProcessando(): ComandoCriarPedido[] {
    const pendentes = [...this.comandos.values()].filter((c) => c.status === 'Pendente');
    pendentes.forEach((c) => (c.status = 'Processando'));
    return pendentes;
  }

  registrarResultado(id: string, sucesso: boolean, referenciaExterna?: string, erro?: string) {
    const comando = this.comandos.get(id);
    if (!comando) {
      this.logger.warn(`Resultado recebido para comando desconhecido: ${id}`);
      return;
    }
    comando.status = sucesso ? 'Gravado' : 'Erro';
    comando.erro = erro;
    if (sucesso && referenciaExterna) comando.referenciaExterna = referenciaExterna;
  }

  buscarPorId(id: string): ComandoCriarPedido {
    const comando = this.comandos.get(id);
    if (!comando) throw new NotFoundException(`Comando ${id} não encontrado.`);
    return comando;
  }
}

export type { ItemComando };
