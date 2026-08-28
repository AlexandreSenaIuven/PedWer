import { NestFactory } from '@nestjs/core';
import { json } from 'express';
import { AppModule } from './app.module';

async function bootstrap() {
  const app = await NestFactory.create(AppModule);
  // Habilitado só para o front local (Vite, porta 5173) chamar esta API durante o desenvolvimento.
  app.enableCors({ origin: 'http://localhost:5173' });
  // Limite padrão do Express (100kb) é pequeno demais para o snapshot de
  // sincronização (agora completo: ~13k clientes e ~31k produtos por
  // empresa) — o console é o único que envia corpos grandes, então isto é
  // seguro de aumentar aqui.
  app.use(json({ limit: '100mb' }));
  await app.listen(process.env.PORT ?? 3001);
}
bootstrap();
