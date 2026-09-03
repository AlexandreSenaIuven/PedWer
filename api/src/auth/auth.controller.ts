import { BadRequestException, Body, Controller, Post, UnauthorizedException } from '@nestjs/common';
import { CacheService } from '../cache/cache.service';
import { cifrarSenha } from './cifrar-senha';

class LoginBody {
  usuario!: string;
  senha!: string;
}

/**
 * Login web (25/08/2026) — contra `caduser`, sincronizado pelo console como
 * qualquer outra tabela pequena (`vendedor`, `tipos-operacao`...). Sem
 * sessão/token ainda: a resposta traz o suficiente para o front lembrar
 * quem está logado e qual vendedor filtra a carteira de clientes.
 */
@Controller('auth')
export class AuthController {
  constructor(private readonly cache: CacheService) {}

  @Post('login')
  login(@Body() body: LoginBody) {
    // Maiúsculas sempre: `caduser.identific` é indexado ao pé da letra (Map
    // case-sensitive) e o VFP grava esse campo em maiúsculas.
    const usuario = body.usuario?.trim().toUpperCase();
    const senha = body.senha ?? '';
    if (!usuario || !senha) throw new BadRequestException('Informe usuário e senha.');

    const registro = this.cache.buscarUsuario(usuario);
    const senhaCorreta = registro ? cifrarSenha(senha) === registro.senha : false;
    if (!registro || registro.inativo || !senhaCorreta) {
      // Mensagem genérica de propósito — não revela se o usuário existe.
      throw new UnauthorizedException('Usuário ou senha inválidos.');
    }

    const vendedorCodigo = registro.codVend.trim();
    const vendedor = vendedorCodigo ? this.cache.buscarVendedor(vendedorCodigo) : null;

    return {
      codigo: registro.identific,
      nome: registro.nome || registro.identific,
      vendedorCodigo,
      vendedorNome: vendedor?.nome ?? '',
    };
  }
}
