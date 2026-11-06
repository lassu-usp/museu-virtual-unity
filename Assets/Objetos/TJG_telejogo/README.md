# Telejogo · integração Unity

Importado em 06/11/2026 de um pacote entregue diretamente, sem revisão de origem no `museu-virtual-3d`. FBX e PNG são cópias sem alteração. Nos manifestos, a única diferença é `interacao`: o pacote traz `estatica`, e aqui o console é `pegavel`. Os `.glb` e `.blend` da entrega ficam fora de `Assets/`, como nos outros objetos. Quando o pacote entrar no repositório de modelagem, com `pegavel` no manifesto, sobrescreva os arquivos no lugar e registre a revisão aqui. A validação falha se `interacao` voltar a ser `estatica`.

| Variante | Prefab | Triângulos por LOD | Atlas |
|---|---|---|---|
| Desktop | `PF_TJG_Telejogo` | 11.816 / 5.908 / 3.626 | 2048² |
| WebGL | `PF_TJG_Telejogo_WebGL` | 5.908 / 4.138 | 1024² |

Console pegável com `BoxCollider` e `Rigidbody` cinemático: E retira/devolve, arrastar gira, roda aproxima/afasta e R realinha. A devolução restaura a pose e o colisor. Medidas: 33,6 × 15,0 × 8,3 cm, em metros no FBX, com Y para cima e pivô no centro da base.

Material URP Lit `MAT_TJG_Console`, com BaseColor sRGB e Normal, MetallicSmoothness e Occlusion lineares. O pacote chama o material de `M_TJG_Console`; o importador compartilhado gera o nome do projeto e remapeia os FBX. O LODGroup usa transições desktop 0,30 / 0,12 / 0,004 e WebGL 0,25 / 0,004. A inspeção força o nível mais detalhado.

`Museu.unity` contém o expositor à direita do Atari, em posição simétrica à do MicroFlex-4. `TJG_Validacao.unity` permite observar a montagem sob iluminação neutra. O processador WebGL troca automaticamente o prefab na cópia de build, preservando posição, rotação, escala e estado ativo.

## Reprodução

No menu **Museu › Telejogo**:

1. **Configurar pacote e montar prefabs** configura importadores e materiais e gera os dois prefabs, usando somente as cópias deste repositório.
2. **Adicionar exposição e cena de validação** adiciona a exposição caso ainda não exista e recria a cena isolada. A posição de uma exposição existente é preservada.
3. **Validar integração** verifica hierarquia, escala, triângulos, materiais, texturas, colisor, transições e substituição WebGL. Relatório em `Logs/TJG06/asset-validation.txt`.

Em uma instância batch dedicada, `-executeMethod MuseuVirtual.Editor.TelejogoValidation.IntegrateBatch` executa os três passos e a validação de assets do Atari e do MicroFlex-4. `-executeMethod MuseuVirtual.Editor.MuseumFocusValidation.RunBatch` testa, no Play Mode, seleção próxima e distante, retirada e devolução dos dois prefabs, além do trajeto até o expositor e dos comandos E, arrastar e R na cena principal. Essa execução também cobre a regressão do Atari e do MicroFlex-4, grava as imagens do Telejogo em `Logs/TJG06/` e encerra o Editor com código 0 ou 1. Não execute esses métodos de batch na sessão de edição interativa.
