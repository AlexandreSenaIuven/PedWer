-- Esquema PostgreSQL do lançamento de pedido (Fase 2 do plano em
-- C:\Users\asena\.claude\plans\dreamy-growing-bear.md). Contraparte SQL do
-- modelo de domínio em dominio/Dominio (Pedido/ItemPedido) — os dois devem
-- ser mantidos em sincronia manualmente até existir uma ferramenta de
-- migração.
--
-- Ainda não executado: não há PostgreSQL disponível nesta máquina para
-- validar. Revisar com atenção antes da primeira migração real.
--
-- Convenção de nomes (decisão L10, 20/08/2026): nomes de negócio em tudo —
-- nenhum campo físico ambíguo do VFP (INSS/IRRF/ISS/PIS/COFINS/CSLL de
-- `clientes`, `qtd_comp`/`qtd_larg` de `pedido`) aparece aqui. A tradução
-- para/de nomes físicos mora só no contrato interno do console C# (ver
-- integrador/README.md). Campos como `produto_grupo`/`produto_referencia`
-- não são "nomes físicos vazando" — são conceitos sem ambiguidade nenhuma
-- dos dois lados, coincidência de nome não é o problema que a decisão L10
-- endereça.
--
-- Este banco NÃO duplica cadastro do ERP (cliente, produto, vendedor,
-- condição de pagamento, tipo de operação, centro de custo): os códigos
-- abaixo são referências resolvidas pelo console C# no momento do uso, não
-- chaves estrangeiras para tabelas locais.

CREATE TYPE estado_pedido AS ENUM ('rascunho', 'fechado', 'cancelado');
CREATE TYPE estado_item_pedido AS ENUM ('ativo', 'excluido');
CREATE TYPE origem_preco AS ENUM ('tabela_sem_desconto', 'tabela_com_desconto', 'negociado');

-- Decisão L3: a numeração sai do DBF (o contador `wareas.cont_ped` não é
-- seguro sob concorrência e, medido, quase não é usado na prática — RF-102).
-- Esta sequência é a fonte de verdade do número interno; o código digitado
-- pelo vendedor vira `referencia_externa`, sem unicidade forçada.
CREATE SEQUENCE pedido_numero_seq;

CREATE TABLE pedido (
    id                          bigserial PRIMARY KEY,
    numero                      bigint NOT NULL DEFAULT nextval('pedido_numero_seq') UNIQUE,
    referencia_externa          varchar(20),                       -- RF-109
    tipo_operacao               varchar(3) NOT NULL,                -- RF-020 (= tipos.tipo no VFP)
    codigo_empresa              varchar(2) NOT NULL,                -- RF-003/021
    codigo_cliente              varchar(7) NOT NULL,                -- RF-023
    data_pedido                 date NOT NULL,
    condicao_pagamento_codigo   varchar(2),
    centro_custo_codigo         varchar(4),
    vendedor_codigo_1           varchar(4),                         -- RF-127-133; só preenchidos no fechamento
    vendedor_codigo_2           varchar(4),
    linha_negocio_grupo         varchar(4),                         -- RF-142 — travado pelo primeiro item
    estado                      estado_pedido NOT NULL DEFAULT 'rascunho',
    autor_criacao               varchar(40) NOT NULL,                -- RF-004
    data_criacao                timestamptz NOT NULL DEFAULT now(),
    autor_cancelamento          varchar(40),                        -- RF-009: cancelamento é lógico, nunca DELETE físico
    motivo_cancelamento         text,
    data_cancelamento           timestamptz,
    total_nota                  numeric(15, 2),                     -- gravado uma vez no fechamento (RF-163); nunca reescrito por item (corrige a Janela 2 do doc de integração)

    CONSTRAINT chk_cancelamento_consistente CHECK (
        estado <> 'cancelado' OR (autor_cancelamento IS NOT NULL AND motivo_cancelamento IS NOT NULL)
    )
);

CREATE INDEX idx_pedido_cliente ON pedido (codigo_cliente);
CREATE INDEX idx_pedido_empresa_tipo ON pedido (codigo_empresa, tipo_operacao);
CREATE INDEX idx_pedido_referencia_externa ON pedido (referencia_externa) WHERE referencia_externa IS NOT NULL;

CREATE TABLE item_pedido (
    id                          bigserial PRIMARY KEY,
    pedido_id                   bigint NOT NULL REFERENCES pedido (id),
    numero                      int NOT NULL,                       -- RF-174: identidade estável, não posição no grid
    produto_grupo               varchar(4) NOT NULL,
    produto_referencia          varchar(10) NOT NULL,
    quantidade                  numeric(14, 3) NOT NULL CHECK (quantidade > 0),  -- RF-088
    preco_tabela_ajustado       numeric(15, 2) NOT NULL,
    preco_final                 numeric(15, 2) NOT NULL,
    percentual_desconto         numeric(7, 2) NOT NULL,
    origem_preco                origem_preco NOT NULL,
    percentual_comissao         numeric(5, 2),                      -- RF-054 + decisão L2: só após o fechamento
    medida_largura              numeric(7, 3),                      -- RF-054: campo próprio (M2/ML/M3), nunca reaproveita qtd_larg/qtd_comp

    estado                      estado_item_pedido NOT NULL DEFAULT 'ativo',

    UNIQUE (pedido_id, numero)
);

CREATE INDEX idx_item_pedido_pedido ON item_pedido (pedido_id);

-- RF-166: alteração precisa registrar O QUE mudou, não só "houve alteração"
-- (hoje o log só grava código do pedido + tipo de operação).
CREATE TABLE pedido_auditoria (
    id              bigserial PRIMARY KEY,
    pedido_id       bigint NOT NULL REFERENCES pedido (id),
    autor           varchar(40) NOT NULL,
    data_evento     timestamptz NOT NULL DEFAULT now(),
    campo           varchar(60) NOT NULL,
    valor_anterior  text,
    valor_novo      text
);

CREATE INDEX idx_pedido_auditoria_pedido ON pedido_auditoria (pedido_id);
