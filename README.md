# Museu Virtual de Tecnologia · Projeto Unity

Projeto Unity (URP) do **Museu Virtual de Tecnologia do LASSU-USP**: o espaço 3D em que o visitante navega e interage com as peças do acervo, no desktop e no navegador (WebGL).

Os objetos do acervo, escaneados a laser, são modelados no repositório irmão [`museu-virtual-3d`](https://github.com/lassu-usp/museu-virtual-3d) (privado), da nuvem de pontos até modelos otimizados para tempo real. Este repositório recebe esses modelos e cuida do resto: o espaço do museu, a montagem dos objetos no Unity, as dinâmicas de interação e os builds.

## Sobre o projeto

| | |
|---|---|
| **Laboratório** | LASSU · Laboratório de Sustentabilidade, Escola Politécnica da USP |
| **Projeto temático** | SFI2 · Fatiamento de Infraestruturas de Internet do Futuro (FAPESP 2018/23097-3) |
| **Frente de trabalho** | Realidade Virtual |
| **Coordenação** | Profa. Dra. Tereza Cristina Melo de Brito Carvalho |
| **Plataformas-alvo** | Desktop (primeira versão) e WebGL/navegador (versão otimizada) |
| **Motor** | Unity 6000.5.7f1 · Universal Render Pipeline (URP) 17.5 |

## Como os dois repositórios se dividem

```text
escaneamento a laser
        │  nuvem de pontos
        ▼
museu-virtual-3d       etapas 0–5: nuvem limpa, malha detalhada, low-poly,
                       texturas, LODs, colisores e exportação
        │  objetos/<codigo>_<nome>/05_export/desktop/ e webgl/
        ▼
museu-virtual-unity    etapa 6 em diante: materiais URP, LODGroup, física, prefab,
                       interação, espaço do museu e builds
```

| No `museu-virtual-3d` | Aqui |
|---|---|
| Nuvens de pontos, malhas detalhadas e arquivos `.blend` | Cópia dos pacotes de exportação (FBX, texturas e manifesto) |
| Geometria, UVs, texturas, LODs e malhas de colisão | Materiais URP, `LODGroup`, colisores, `Rigidbody` e prefabs `PF_*` |
| Scripts Python de processamento e a ficha de cada objeto | Espaço do museu, navegação, interação e builds |

**Fonte única:** geometria e texturas dos objetos do acervo só são editadas no `museu-virtual-3d`. Se algo estiver errado no Unity (escala, pivô, UV, normal, textura), corrija lá, reexporte a etapa 5 e atualize os arquivos aqui.

## Objetos do acervo

| Código | Objeto | Prefab | Integração no Unity |
|---|---|---|---|
| `ATR` | Atari | `PF_ATR_Atari2600` | não iniciada (pacote da etapa 5 em revisão) |
| `MFX` | MicroFlex4 | — | não iniciada |
| `MOT` | Motorola | — | não iniciada |
| `ORE` | Orelhão | — | não iniciada |
| `TJG` | Telejogo | — | não iniciada |

Status possíveis: `não iniciada` · `em andamento` · `em revisão` · `concluída`. O andamento da modelagem (etapas 0 a 5) e os responsáveis ficam na tabela de objetos do `museu-virtual-3d`.

## Espaço do museu

O espaço fica em `Assets/Museu/` e já está na cena principal, `Assets/Scenes/Museu.unity`:

- `Museu.prefab`: o espaço montado, com paredes, vitrines, quadros, luzes e textos;
- `Modelos/`: os FBX do espaço (`MuseuQuadrado.fbx`), dos quadros (`MuseuQuadros.fbx` e `Frame_light.fbx`) e dos textos expositivos (`Textos/`);
- `Materiais/`, `Texturas/` e `Fontes/`: materiais URP Lit, texturas e as fontes das legendas (TextMesh Pro).

Os quadros estão sem imagem: as pinturas de exemplo saíram do repositório, mas as legendas ainda trazem os dados delas. As obras expostas ainda serão definidas, só com imagens que tenham direito de uso.

## Estrutura

```text
.
├── Assets/
│   ├── Museu/                        espaço do museu
│   │   ├── Modelos/                  FBX do espaço, dos quadros e dos textos
│   │   ├── Materiais/                materiais URP Lit (MAT_Museu_<Nome>)
│   │   ├── Texturas/                 texturas dos materiais
│   │   ├── Fontes/                   fontes das legendas (TextMesh Pro)
│   │   └── Museu.prefab
│   ├── Objetos/                      objetos do acervo (criada com o primeiro objeto)
│   │   └── <codigo>_<nome>/          ex.: ATR_atari/, o mesmo nome da pasta no museu-virtual-3d
│   │       ├── desktop/              cópia de 05_export/desktop/: FBX, texturas/ e manifesto.json
│   │       ├── webgl/                cópia de 05_export/webgl/
│   │       ├── Materiais/            materiais URP Lit (MAT_<CODIGO>_<Componente>)
│   │       └── PF_<CODIGO>_<Nome>.prefab
│   ├── Scenes/                       cenas; Museu.unity é a principal
│   ├── Scripts/                      código C# de navegação e interação (criada com o primeiro script)
│   ├── Settings/                     assets do URP: PC_* (desktop) e Mobile_* (WebGL)
│   ├── TextMesh Pro/                 recursos essenciais do TextMesh Pro
│   └── InputSystem_Actions.inputactions   ações de entrada (Input System)
├── Packages/                         pacotes do projeto (manifest.json e packages-lock.json)
├── ProjectSettings/                  configurações do projeto
├── .gitattributes                    regras do Git LFS e do merge de cenas e prefabs
└── .gitignore                        Library/, Temp/, Logs/, UserSettings/, builds e arquivos gerados
```

As pastas `desktop/` e `webgl/` de cada objeto mantêm o nome que têm no `museu-virtual-3d`.

## Convenções

Herdadas do `museu-virtual-3d`, valem para tudo o que vem de lá:

- **Unidade:** metros. **Eixo vertical:** Y no Unity (o exportador já converte do Z do Blender).
- **Pivô:** no centro da base de cada peça, com a base em altura 0.
- **Nomes:** `<CODIGO>_<Componente>_<sufixo>`, sem acentos nem espaços. Ex.: `ATR_Joystick_LOD0`. O prefab do objeto é `PF_<CODIGO>_<Nome>` e os materiais, `MAT_<CODIGO>_<Componente>`, como no `manifesto.json`.
- **Níveis de detalhe:** `LOD0` (mais detalhado), `LOD1`, `LOD2`. Malhas de colisão com sufixo `_COL`.
- **Texturas no URP Lit:** `BaseColor` (sRGB), `Normal` (padrão OpenGL, Y+, o mesmo do Unity), `MetallicSmoothness` (metal no R, suavidade no A) e `Occlusion`. O `ORM` serve às fontes e ao `.glb`; não vai para o URP.
- **Interação de cada peça:** `estatica`, `pegavel` ou `removivel`, no campo `interacao` do `manifesto.json`.
- **Orçamento (LOD0):** no desktop, até ~30 mil triângulos por peça e texturas 2K; no WebGL, ~10–15 mil e texturas 1K comprimidas.

Deste repositório:

- **Nomes no espaço do museu:** materiais `MAT_Museu_<Nome>` e texturas `Museu_<Nome>_<Mapa>.png`, sem acentos nem espaços.
- **Arquivos `.meta`:** cada asset tem um `.meta` com o GUID que as cenas e os prefabs usam para encontrá-lo. Commite o `.meta` junto com o asset e mova ou renomeie assets **dentro do Unity**, não pelo Explorer. O projeto usa `Visible Meta Files` e serialização `Force Text`; mantenha essas opções.
- **Entrada:** o projeto está configurado para o Input System (*Active Input Handling: Input System Package (New)*). As ações do visitante ficam em `Assets/InputSystem_Actions.inputactions`, cujo mapa `Player` já traz `Move`, `Look`, `Sprint` e `Interact`.
- **Fontes do TextMesh Pro:** fontes dinâmicas, como a `Roboto-Bold SDF`, se regravam sozinhas quando o editor desenha caracteres novos. Se o `.asset` de uma fonte aparecer modificado sem você ter mexido nele, descarte a mudança antes do commit.
- **Codificação:** salve C#, JSON e Markdown em UTF-8, para os acentos não se corromperem entre editores.

## Como começar

1. Instale o [Unity Hub](https://unity.com/download) e, por ele, o editor **6000.5.7f1**, a versão gravada em `ProjectSettings/ProjectVersion.txt`, com o módulo de build para Web (WebGL). Abra o projeto sempre nessa versão: trocar de versão altera arquivos do projeto inteiro e precisa ser combinado com a equipe.
2. Instale o [Git](https://git-scm.com/) e o [Git LFS](https://git-lfs.com/) e rode uma vez na sua máquina:
   ```bash
   git lfs install
   ```
   Sem o LFS, FBX, imagens e fontes chegam como "ponteiros" de texto, e o Unity não consegue importá-los.
3. Clone o repositório:
   ```bash
   git clone https://github.com/lassu-usp/museu-virtual-unity.git
   ```
4. No Unity Hub, use **Add › Add project from disk** e escolha a pasta clonada. A primeira abertura gera a pasta `Library/` e leva alguns minutos.
5. Abra a cena `Assets/Scenes/Museu.unity`.
6. **Recomendado:** configure o Smart Merge do Unity, que mescla cenas e prefabs entendendo a estrutura deles. O `.gitattributes` já associa esses arquivos ao driver `unityyamlmerge`; falta registrá-lo no seu Git. No Git Bash, dentro do repositório:
   ```bash
   git config merge.unityyamlmerge.name "Unity SmartMerge"
   git config merge.unityyamlmerge.driver '"C:/Program Files/Unity/Hub/Editor/6000.5.7f1/Editor/Data/Tools/UnityYAMLMerge.exe" merge -h -p --force --fallback none %O %B %A %A'
   git config merge.unityyamlmerge.recursive binary
   ```
   Ajuste o caminho se o editor estiver instalado em outro lugar. Quando a mesma propriedade muda nos dois lados, o Git ainda acusa conflito: o terminal lista as propriedades em disputa, e o arquivo fica com o valor da outra branch, sem marcadores `<<<<<<<`. Ajuste no Unity e conclua o merge normalmente.

### Ferramentas

| Ferramenta | Uso |
|---|---|
| Unity 6000.5.7f1 (URP) | projeto, cenas e builds |
| Visual Studio, Rider ou VS Code | código C#; as configurações desses editores ficam fora do Git |
| Git e Git LFS | versionamento; FBX, imagens e fontes vão pelo LFS |

## Integrar um objeto do acervo

É a etapa 6 do fluxo do `museu-virtual-3d` (*Validação no Unity*). Lá, o guia do pacote de cada objeto (ex.: `objetos/ATR_atari/05_export/README.md`) traz os detalhes e os cuidados específicos. O roteiro geral:

1. **Pegue o pacote na `main` do `museu-virtual-3d`** e anote o commit (`git log -1 --format=%h`); ele vai na mensagem de commit daqui.
2. **Copie** `objetos/<codigo>_<nome>/05_export/desktop/` para `Assets/Objetos/<codigo>_<nome>/desktop/`: os FBX (níveis de detalhe e `_COL`), a pasta `texturas/` e o `manifesto.json`. Faça o mesmo com `webgl/` quando o build WebGL precisar dele. **Não copie** os `.glb` (o Unity não importa glTF sem um pacote extra) nem o `.blend` (o Unity tentaria convertê-lo abrindo o Blender da máquina).
3. **Configure a importação** no Inspector:
   - FBX, aba *Model*: `Normals` e `Tangents` em `Import`, para preservar as normais e tangentes exportadas. Confira a escala: as peças vêm em metros, com as medidas do `manifesto.json` (o console do Atari, por exemplo, tem ~34 cm de comprimento).
   - FBX, aba *Rig*: `Animation Type: None`, porque as peças não têm animação.
   - Texturas: `BaseColor` com `sRGB (Color Texture)` ligado; `Normal` com `Texture Type: Normal map`; `MetallicSmoothness` e `Occlusion` com `sRGB` desligado.
4. **Crie os materiais URP Lit** listados em `materiais` no `manifesto.json` (ex.: `MAT_ATR_Console`), na pasta `Materiais/` do objeto:

   | Mapa | No material URP Lit |
   |---|---|
   | `BaseColor` | Base Map, com cor branca |
   | `Normal` | Normal Map, intensidade 1 |
   | `MetallicSmoothness` | Metallic Map, com `Smoothness` em 1 e `Source: Metallic Alpha` |
   | `Occlusion` | Occlusion Map, intensidade 1 |

   Depois, na aba *Materials* de cada FBX, remapeie os materiais importados para esses `MAT_*`. Com os nomes iguais aos do FBX, o botão *Search and Remap* faz isso de uma vez.
5. **Monte o prefab** `PF_<CODIGO>_<Nome>` (ex.: `PF_ATR_Atari2600`), mantendo a hierarquia do pacote, com uma raiz `<CODIGO>_<Componente>_Root` por peça:
   - **Níveis de detalhe:** em cada raiz, junte as malhas `LOD0`…`LODn` num `LODGroup` próprio, para a peça poder ser manipulada sozinha. Comece pelas transições do manifesto (`lod_transicoes_altura_relativa_tela`) e calibre com a câmera real.
   - **Colisores:** siga `colisores.<peça>.tipo` do manifesto: `BoxCollider` do tamanho da caixa `_COL` ou `MeshCollider` com `Convex` ligado, usando a malha `_COL`. As malhas `_COL` não aparecem na cena: remova ou desative os renderers delas.
   - **Física:** peças `estatica` têm só o colisor; `pegavel` e `removivel` ganham um `Rigidbody`, começando cinemático. Confira o guia do pacote: no Atari, por exemplo, a caixa do console cobre o encaixe do cartucho, e a colisão entre os dois precisa ser ignorada enquanto ele estiver encaixado.
6. **Valide** numa cena de teste: escala, pivôs, normal maps, trocas de LOD, colisores (com *Gizmos*) e triângulos (janela *Stats*). Problemas de geometria ou textura voltam para o `museu-virtual-3d` (veja *Fonte única*, acima).
7. **Registre:** atualize a [tabela de objetos](#objetos-do-acervo) e a etapa 6 na ficha do objeto, no `museu-virtual-3d`. No commit, cite a origem. Ex.: `ATR: importa o pacote desktop da etapa 5 (museu-virtual-3d 07811b8)`.

**Para atualizar um objeto já integrado,** sobrescreva os arquivos no lugar, **sem apagar os `.meta`**. Assim os GUIDs, as configurações de importação e as referências dos prefabs continuam valendo; apagar e recriar a pasta quebra o prefab.

## Interação

| Dinâmica | Descrição | Status |
|---|---|---|
| Navegação em primeira pessoa | andar, olhar em volta e correr, com teclado e mouse | planejada |
| Peça estática | fica no lugar, só com colisor | planejada |
| Peça pegável | o visitante pega e manipula a peça | planejada |
| Peça removível | a peça sai de um encaixe, como o cartucho do console do Atari | planejada |

A dinâmica de cada peça vem do `manifesto.json` e da ficha do objeto. O código fica em `Assets/Scripts/` e usa as ações de `InputSystem_Actions.inputactions` (veja [Convenções](#convenções)).

## Desktop e WebGL

- A primeira versão é para desktop. A de navegador (WebGL) é otimizada e usa os pacotes `webgl/` dos objetos: menos triângulos, menos níveis de detalhe e texturas menores.
- O URP tem dois níveis de qualidade: `PC` (`Settings/PC_RPAsset`), padrão do desktop, e `Mobile` (`Settings/Mobile_RPAsset`), padrão do WebGL.
- Para gerar um build, use **File › Build Profiles**, escolha *Windows* ou *Web* e grave em `Builds/`, na raiz do repositório, que o Git ignora.

## Fluxo de trabalho

1. Crie uma branch a partir da `main`: `atr/06-unity` para integrar um objeto (a etapa 6 do `museu-virtual-3d`); `feat/<tema>`, `fix/<tema>` ou `docs/<tema>` para o resto (ex.: `feat/navegacao`).
2. **Cenas e prefabs mesclam mal**, mesmo sendo texto. Avise a equipe antes de editar uma cena ou um prefab compartilhado e prefira trabalhar em prefabs próprios, que depois entram na cena. Com o Smart Merge configurado (passo 6 de [Como começar](#como-começar)), boa parte dos conflitos se resolve sozinha.
3. Antes do PR, rode a cena no Play Mode e confira se o Console está sem erros.
4. Abra um Pull Request para a `main` com capturas de tela ou um vídeo curto e peça revisão de pelo menos uma pessoa.

**Commits:** mensagens curtas no imperativo, em português, com o código do objeto quando a mudança for de um objeto (`ATR: monta o prefab com LODGroup`) ou com o tipo da mudança nas demais (`feat: adiciona a navegação em primeira pessoa`).

**O que não entra:** `Library/`, `Temp/`, `Logs/`, `UserSettings/`, builds, os `.csproj`/`.slnx` gerados e as configurações de editor (`.vscode/`, `.idea/`); o `.gitignore` já cuida disso. Mudanças em `ProjectSettings/` e `Packages/` afetam todo mundo: commite-as separadas e explique no PR.

**Arquivos grandes:** como no `museu-virtual-3d`, evite commitar versões intermediárias pesadas; cada versão de um arquivo LFS ocupa espaço na cota da organização.
