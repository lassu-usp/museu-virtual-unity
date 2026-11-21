# Patinho Feio · integração Unity

Importado de `museu-virtual-3d/objetos/PTF_patinhofeio/05_export`, revisão `0756b6d`, em 21/11/2026. FBX, PNG e manifestos são cópias dos pacotes aprovados na etapa 5; fontes Blender e alternativas GLB permanecem no repositório de modelagem.

| Variante | Prefab | Triângulos por LOD | Atlas: computador / suporte |
|---|---|---|---|
| Desktop | `PF_PTF_PatinhoFeio` | 30.316 / 14.380 / 5.164 | 2048² / 1024² |
| WebGL | `PF_PTF_PatinhoFeio_WebGL` | 12.156 / 5.164 | 1024² / 512² |

Peça estática, por enquanto sem interação. Suporte (`PTF_Stand_Root`) e computador (`PTF_Computer_Root`) são raízes separadas, cada uma com seu `LODGroup` e um `BoxCollider` com as medidas da caixa `_COL` do pacote, sem `Rigidbody` nem `MuseumExhibit`. Conjunto de 1,06 × 1,79 × 0,74 m, em metros, com Y para cima e o pivô no centro da base do suporte; o computador conserva a inclinação de 0,42° do escaneamento.

Os 97 comandos e indicadores do painel (lentes, alavancas, seletores e botões) são filhos do computador, com os nomes, pivôs e materiais do pacote, e ficam no estado inicial: lentes apagadas, botões soltos, alavancas para cima e seletores em SEQUENCIAL e PROTEGIDA. No desktop, eles acompanham os níveis 0 e 1; no WebGL, só o nível 0. As propriedades de estado de cada comando seguem no `manifesto.json`, para a etapa de interação.

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
3. **Validar integração** confere hierarquia, triângulos por nível, comandos, materiais, texturas, colisores, base no piso e substituição WebGL. Relatório em `Logs/PTF06/asset-validation.txt`.

Em uma instância batch dedicada, `-executeMethod MuseuVirtual.Editor.PatinhoFeioValidation.IntegrateBatch` executa os três passos, a validação dos assets de Atari, MicroFlex-4 e Telejogo e a validação da galeria, e encerra o Editor com código 0 ou 1. Não execute esse método na sessão de edição interativa.

## Próximas etapas

- Interação: botões momentâneos, alavancas e seletores de duas posições e emissão das lentes por instância, com os estados do manifesto. Durante a operação do painel, use o nível mais detalhado.
- Descrição da peça e calibração das transições com a câmera e a distância de interação reais.
