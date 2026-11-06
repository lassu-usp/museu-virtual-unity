# MicroFlex-4 · integração Unity

Importado de `museu-virtual-3d/objetos/MFX_microflex4/05_export`, revisão `869cf9b`, em 06/11/2026. FBX, PNG e manifestos são cópias dos pacotes aprovados; fontes Blender e alternativas GLB permanecem no repositório de modelagem.

| Variante | Prefab | Triângulos por LOD | Atlas |
|---|---|---|---|
| Desktop | `PF_MFX_Microflex4` | 16.242 / 9.272 / 4.208 | 2048² |
| WebGL | `PF_MFX_Microflex4_WebGL` | 10.556 / 5.278 | 1024² |

Gabinete estático com `BoxCollider`, alça fixa e ficha informativa. Teclado pegável com `MeshCollider` convexo e `Rigidbody` cinemático: E retira/devolve, arrastar gira, roda aproxima/afasta e R realinha. A devolução restaura a pose e os colisores. Não há interação independente do cabo ou da alça.

Materiais URP Lit com BaseColor sRGB; Normal, MetallicSmoothness e Occlusion lineares. Geometria em metros, Y para cima, pivôs e giro relativo do teclado preservados. Cada peça possui seu próprio LODGroup; transições desktop 0,30 / 0,12 / 0,004 e WebGL 0,25 / 0,004. Inspeção força o nível mais detalhado.

Em `Museu.unity`, o MicroFlex-4 fica no pedestal `Exhibit_01` da sala de 1980. `MFX_Validacao.unity` permite observar a montagem sob iluminação neutra. O processador WebGL troca automaticamente o prefab na cópia de build.

## Reprodução

No menu **Museu › MicroFlex**:

1. **Importar pacote e montar prefabs** copia os entregáveis do repositório irmão, configura importadores e gera os dois prefabs. `MicroflexIntegration.BuildPrefabs()` também permite reconstruí-los usando somente as cópias deste repositório.
2. **Adicionar exposição e cena de validação** configura a ação Z, instala as peças nos pedestais da galeria e recria a cena isolada.
3. **Validar integração** verifica hierarquia, escala, triângulos, materiais, texturas, colisores e substituição WebGL. Relatório em `Logs/MFX06/asset-validation.txt`.

Em uma instância batch dedicada, `-executeMethod MuseuVirtual.Editor.MicroflexValidation.IntegrateBatch` executa importação e validação dos assets. `-executeMethod MuseuVirtual.Editor.MuseumFocusValidation.RunBatch` testa entradas no Play Mode, inspeção/devolução dos dois MFX e regressão do Atari, e percorre a galeria; grava relatório e imagens em `Logs/MFX06/` e `Logs/Galeria/` e encerra o Editor com código 0 ou 1. Não execute esses métodos de batch na sessão de edição interativa.

O zoom geral usa `Player/Focus` (Z), reduz o campo de visão pela metade e mantém o olhar disponível. Movimento, outros comandos, pausa e perda de foco da janela restauram a visão original. A validação inclui entradas simultâneas, movimento mantido e movimento analógico lento.
