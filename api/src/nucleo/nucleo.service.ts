import { HttpService } from '@nestjs/axios';
import { HttpException, Injectable } from '@nestjs/common';
import { AxiosError } from 'axios';
import { firstValueFrom } from 'rxjs';
import {
  CreditoPayload,
  FiscalPayload,
  PrecificarPayload,
  ResultadoCreditoNucleo,
  ResultadoFiscal,
  ResultadoPrecificacao,
} from './nucleo.types';

@Injectable()
export class NucleoService {
  constructor(private readonly http: HttpService) {}

  async precificar(payload: PrecificarPayload): Promise<ResultadoPrecificacao> {
    return this.post<ResultadoPrecificacao>('/precificar', payload);
  }

  async resolverComissao(tipoVendedor: string, percentualDesconto: number): Promise<number> {
    const resposta = await this.post<{ percentualComissao: number }>('/comissao', { tipoVendedor, percentualDesconto });
    return resposta.percentualComissao;
  }

  async avaliarCredito(payload: CreditoPayload): Promise<ResultadoCreditoNucleo> {
    return this.post<ResultadoCreditoNucleo>('/credito', payload);
  }

  async calcularFiscal(payload: FiscalPayload): Promise<ResultadoFiscal> {
    return this.post<ResultadoFiscal>('/fiscal', payload);
  }

  private async post<T>(path: string, body: unknown): Promise<T> {
    try {
      const resposta = await firstValueFrom(this.http.post<T>(path, body));
      return resposta.data;
    } catch (erro) {
      if (erro instanceof AxiosError) {
        if (erro.response) throw new HttpException(erro.response.data ?? erro.message, erro.response.status);
        throw new HttpException('Nucleo.Api indisponível.', 503);
      }
      throw new HttpException('Erro inesperado ao chamar o Nucleo.Api.', 500);
    }
  }
}
