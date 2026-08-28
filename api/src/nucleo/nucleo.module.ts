import { HttpModule } from '@nestjs/axios';
import { Module } from '@nestjs/common';
import { NucleoService } from './nucleo.service';

@Module({
  imports: [
    HttpModule.register({
      // Nucleo.Api — serviço central de regras (preço/comissão/crédito),
      // sem dependência de VFPOLEDB. Roda ao lado desta API, não na
      // máquina do cliente.
      baseURL: process.env.NUCLEO_API_URL ?? 'http://localhost:5060',
      timeout: 10_000,
    }),
  ],
  providers: [NucleoService],
  exports: [NucleoService],
})
export class NucleoModule {}
