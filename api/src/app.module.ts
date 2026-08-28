import { Module } from '@nestjs/common';
import { ServeStaticModule } from '@nestjs/serve-static';
import { join } from 'path';
import { AuthModule } from './auth/auth.module';
import { ClientesModule } from './clientes/clientes.module';
import { ComandosModule } from './comandos/comandos.module';
import { PedidosModule } from './pedidos/pedidos.module';
import { ProdutosModule } from './produtos/produtos.module';
import { ReferenciasModule } from './referencias/referencias.module';
import { SincronizacaoModule } from './sincronizacao/sincronizacao.module';

@Module({
  imports: [
    AuthModule,
    ClientesModule,
    ProdutosModule,
    PedidosModule,
    SincronizacaoModule,
    ComandosModule,
    ReferenciasModule,
    // Front publicado (frontend/dist copiado para api/public na publicação —
    // ver docs/publicacao.md). Precisa ser o ÚLTIMO import: o Nest resolve
    // rotas na ordem dos módulos, e isto intercepta qualquer caminho que
    // nenhum controller acima já tenha respondido. Em dev (vite na 5173),
    // a pasta não existe — serve-static só deixa de achar arquivo e cai no
    // 404 do Nest, sem quebrar o `npm run start:dev`.
    ServeStaticModule.forRoot({ rootPath: join(__dirname, '..', 'public') }),
  ],
})
export class AppModule {}
