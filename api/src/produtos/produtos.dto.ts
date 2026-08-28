import { ProdutoFisico } from '../cache/cache.types';

export interface ProdutoResumo {
  grupo: string;
  referencia: string;
  descricao: string;
  precoTabela: number;
  gradecol: string;
  gradegrp: string;
}

export function traduzirProduto(p: ProdutoFisico): ProdutoResumo {
  return {
    grupo: p.grupo,
    referencia: p.referencia,
    descricao: p.descricao,
    precoTabela: p.prcVenda,
    gradecol: p.gradecol,
    gradegrp: p.gradegrp,
  };
}
