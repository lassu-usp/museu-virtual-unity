# Patinho Feio · integração Unity

Importado de `museu-virtual-3d/objetos/PTF_patinhofeio/05_export`, revisão `0756b6d`, em 21/11/2026. FBX, PNG e manifestos são cópias dos pacotes aprovados na etapa 5; fontes Blender e alternativas GLB permanecem no repositório de modelagem.

| Variante | Prefab | Triângulos por LOD | Atlas: computador / suporte |
|---|---|---|---|
| Desktop | `PF_PTF_PatinhoFeio` | 30.316 / 14.380 / 5.164 | 2048² / 1024² |
| WebGL | `PF_PTF_PatinhoFeio_WebGL` | 12.156 / 5.164 | 1024² / 512² |

Suporte e computador são peças fixas, cada uma com seu LODGroup e colisor. O conjunto mede 1,06 × 1,79 × 0,74 m. O computador recebe a interação com **E**. Durante a operação, a câmera enquadra os controles no nível de detalhe máximo. **E**, **Esc** ou **Voltar à sala** devolvem a câmera ao visitante.

## Operação do painel

As 12 alavancas fornecem RC, com P(0) à esquerda e peso /800. A posição para baixo vale 1. A barra indica hexadecimal. Cada grupo de quatro chaves forma um dígito, com os pesos 8, 4, 2 e 1 da esquerda para a direita. O seletor Sequencial incrementa a posição depois de Armazenamento ou Exposição. A proteção cobre /F80–/FFF. Os botões de modo selecionam a operação que Partida inicia. Os 97 elementos vêm do mesmo manifesto nas duas variantes.

- **Executar da ferrite.** Em /010 há um exemplo escrito para o museu. Aperte Preparação, forme /010 e aperte Partida. Forme /005, selecione Normal e aperte Partida. PNL 0, INC e PARE deixam /06 em ACC. Outra Partida continua em /013, num laço que incrementa o acumulador. Espera permite interrompê-lo na fronteira de instrução.
- **Programar à mão.** Em /020, grave /88 e /9D (PNL 0 e PARE) com Armazenamento, Sequencial e uma Partida por byte. Confira os bytes com Exposição. Enderece /020 outra vez, ajuste RC e execute em Normal. O acumulador mostra os oito bits baixos escolhidos nas chaves.
- **Retenção.** A montagem começa ligada e preparada. A ferrite conserva as alterações durante a visita, inclusive ao sair do painel ou desligar e religar o computador. Uma transação de memória abortada pode deixar a célula indeterminada. O sinal de interrogação na ficha indica registrador indeterminado. O exemplo volta ao reiniciar a cena.

O núcleo C# em `Assets/Scripts/PatinhoFeio/Core` deriva de `Pato-Feio/unity/pato_feio_emulator/Assets/PatinhoFeio/Core`. A referência de operação é `Pato-Feio/docs/front-panel.md`, com Fregni (1972), Souza (1976) e o manual do montador (1977). A questão Q-022 registra a perda do carregador residente de /F80. O micro-pré-loader rememorado em 2016 é outro estágio. Esta montagem utiliza carga manual e programa residente. A leitora de fita e a DECwriter ficam desconectadas.

O relógio lógico mantém as bordas de 50 ns do núcleo. A exposição avança até 24 mil bordas por segundo e limita cada quadro a 600. O painel parado só avança em resposta aos comandos. Depois de Preparação, as interfaces desconectadas dispensam a projeção de seus 15 estados a cada borda. As lentes mostram a fração dos passos em que o bit esteve ligado no quadro. Os pilotos de fase representam as fases lógicas do núcleo. O piloto Externo fica apagado.

Materiais URP Lit:

- `MAT_PTF_Computer` e `MAT_PTF_Stand`: atlas com BaseColor sRGB e Normal, MetallicSmoothness e Occlusion lineares;
- sete materiais sem textura, dos comandos e das teclas, com a cor base, o metal e a rugosidade dos materiais dos GLB do pacote;
- `MAT_PTF_Vidro`: transparente, quase incolor e sem sombra. O URP Lit não tem transmissão, então o vidro não refrata.

Transições do manifesto: desktop 0,30 / 0,12 / 0,025 e WebGL 0,25 / 0,025, com descarte abaixo de 0,025. A peça mede quase 1,8 m e, ao contrário das peças pequenas dos pedestais, não precisa do último nível visível a grande distância.

Em `Museu.unity`, o Patinho Feio fica no piso da sala de 1970, no grupo `PTF_Exposicao`: entre os expositores `Exhibit_01` (Telejogo) e `Exhibit_03`, à esquerda do caminho, virado para ele como as peças dos pedestais e com uma luz de exposição própria. Fica fora das faixas de caminhada e dos acessos aos expositores conferidos pela validação da galeria. `PTF_Validacao.unity` permite observar a montagem sob iluminação neutra. O processador WebGL troca a peça pela variante `_WebGL` na cópia de build.

## Reprodução

No menu **Museu › Patinho Feio**:

1. **Importar pacote e montar prefabs** copia os FBX, as texturas e os manifestos do repositório irmão, sem os `.glb` e o `.blend`, configura importadores e materiais e gera os dois prefabs. `PatinhoFeioIntegration.BuildPrefabs()` reconstrói os prefabs só com as cópias deste repositório.
2. **Instalar na galeria e cena de validação** instala as peças do acervo na galeria, inclusive esta, no piso, e recria a cena isolada.
3. **Validar integração** confere hierarquia, triângulos por nível, comandos, materiais, texturas, colisores, base no piso e substituição WebGL. O validador também roda o contador em Normal e mede tempo e alocações do núcleo e das lâmpadas no Editor. Relatório em `Logs/PTF06/asset-validation.txt`.

Em uma instância batch dedicada, `-executeMethod MuseuVirtual.Editor.PatinhoFeioValidation.IntegrateBatch` executa os três passos, a validação dos assets de Atari, MicroFlex-4 e Telejogo e a validação da galeria, e encerra o Editor com código 0 ou 1. Não execute esse método na sessão de edição interativa.
