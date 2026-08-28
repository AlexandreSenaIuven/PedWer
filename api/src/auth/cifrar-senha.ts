/**
 * Porta fiel do algoritmo já usado (e validado) em outra aplicação do
 * cliente para o mesmo par de tabelas (`caduser`): cifra a senha digitada
 * no login para comparar com o valor já gravado em `caduser.senha` — o VFP
 * nunca grava a senha em texto puro, então o login não descriptografa nada,
 * cifra o que foi digitado e compara os dois lados cifrados.
 *
 * Confirmado com um par real (25/08/2026): usuário `WEBSERVICE` criado com
 * senha "1" gravou `caduser.senha = "%"`; `cifrarSenha("1") === "%"`.
 *
 * As quatro trocas finais compensam um artefato de code page (Windows-1252):
 * os bytes 0x87/0x92/0x8A/0x83 não existem como controle nessa code page —
 * são reaproveitados para ‡/'/Š/ƒ — então o resultado da aritmética precisa
 * ser realinhado a esses mesmos pontos de código antes de comparar.
 * Implementado com `String.fromCharCode` numérico (em vez de caracteres
 * literais ou `\u`) porque esses códigos de controle são removidos em
 * silêncio ao transitar por alguns pipelines de texto.
 */
export function cifrarSenha(cString: string): string {
  let resultado = '';
  for (let i = 0; i < cString.length; i++) {
    let codigo = cString.charCodeAt(i);
    if (cString.length > 10) {
      codigo = codigo + 3 + (i + 1 + cString.length);
    } else {
      codigo = codigo - 13 + (i + 1) * cString.length;
    }
    resultado += String.fromCharCode(codigo);
  }

  const trocas: Array<[number, number]> = [
    [0x87, 0x2021], // ‡
    [0x92, 0x2018], // '
    [0x8a, 0x0160], // Š
    [0x83, 0x0192], // ƒ
  ];
  for (const [de, para] of trocas) {
    resultado = resultado.split(String.fromCharCode(de)).join(String.fromCharCode(para));
  }

  return resultado;
}
