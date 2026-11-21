# Galeria das Órbitas

## Executar

Abra `Assets/Scenes/Museu.unity`, pressione **Play** e clique em **Entrar / continuar a visita**. Os controles estão no [README do projeto](../../README.md#interação).

O visitante da cena é o `MuseumVisitor`, de `Assets/Scripts/Visita/`, com os valores deste espaço: 3,2 m/s, corrida a 4,96 m/s e campo de visão de 68°. Ele controla caminhada, olhar, foco, pausa e inspeção das peças; os portões e o HUD das salas leem o estado dele.

## Galeria das Órbitas

A arquitetura propõe uma caminhada entre abóbadas curvas, claraboias luminosas e pórticos com as cores de cada década. Paredes, nichos e piso acompanham a identidade de cada sala, enquanto o fio e as incrustações de bronze conectam o percurso. As salas continuam na ordem de 1940 a 2000, com 28 pontos de exposição.

O hall apresenta a identidade LASSU e uma escultura de órbitas suspensas. Nichos em arco enquadram as peças; bancos e pequenos jardins oferecem pausas. Ao final, a pergunta “Que futuro queremos construir?” convida à reflexão e ao retorno pelas galerias.

Os textos curatoriais de cada sala aparecem integralmente no HUD, sem os antigos painéis físicos. Número da sala, título e descrição são escritos progressivamente, do início ao fim, e permanecem para leitura. Ao atravessar um portal, o texto anterior sai em fade-out e o seguinte entra em fade-in. As quebras de linha permanecem fixas durante a escrita; trocar de sala rapidamente cancela a animação anterior.

- Piso contínuo, sem degraus, com faixa principal de 4,5 m e portais com mais de 7 m de abertura.
- Percurso de aproximadamente 157 m da entrada ao ponto de contemplação final.
- Geometria original preservada na cena, desativada; nova arquitetura agrupada em `05_GalleryOfOrbits`.
- **Museum LASSU > Apply Artistic Architecture** recompõe a arquitetura da cena aberta, preservando referências de modelos e materiais existentes. Isso reposiciona os expositores e o visitante.
- **Rebuild Prototype** recompõe também a arquitetura artística a partir do catálogo; alterações manuais na cena devem ser transferidas ao catálogo antes dessa reconstrução completa.
- Materiais `Orbit_*` podem ser ajustados no Inspector e são preservados ao reaplicar a arquitetura.
- **Museum LASSU > Move Curatorial Panels To HUD** migra os textos dos painéis existentes e atualiza somente a interface, sem reconstruir a arquitetura. Em novas reconstruções, os textos vêm de `MuseumTourCatalog.asset`.

## Sete décadas, sete atmosferas

Cada década tem uma paleta arquitetônica, uma composição suspensa, relevos laterais e incrustações próprias no piso. Em 1940, bronze e âmbar acompanham lâminas escalonadas; 1950 reúne elipses e discos em coral e turquesa; 1960 explora círculos concêntricos dourados e laranja. As fitas onduladas de 1970 usam terracota e ocre; 1980 combina triângulos e pixels em magenta e ciano; 1990 forma uma rede de nós em azul e verde-lima; 2000 encerra com elipses fluidas em verde-água e tons perolados.

Essas composições pertencem à arquitetura e dão presença às salas mesmo sem as peças do acervo.

**Museum LASSU > Apply Decade Identities** atualiza os materiais e recompõe somente os grupos gerados `DecadeIdentity`, sem reposicionar o visitante ou os expositores. Materiais `Era_<ano>_<função>` preservam os ajustes do artista no Inspector. **Refine Materials And Details** e as reconstruções da arquitetura também reaplicam esse tratamento. Depois de alterações, recalcule reflexos e oclusão e salve a cena.

## Acabamentos e texturas

O museu recebeu piso de pedra com juntas e veios, reboco de relevo discreto, madeira nos portões e bancos, caixilhos nas claraboias e plantas com folhas. As paredes curvas e as abóbadas têm normais contínuas. As cores de cada década e o percurso original foram mantidos.

Os três conjuntos de texturas são compartilhados, com mapas de 512–1024 px, mipmaps e compressão; Android está configurado para ASTC 6×6. Oito cubemaps locais de 128 px fornecem reflexos pré-calculados. O refinamento anterior às identidades das décadas registrou nove luzes, uma com sombras, e a passagem de 108.192 para 109.152 triângulos (+0,89%), com 74 renderers ativos a menos. Esses valores documentam aquela etapa; não representam uma medição da cena atual nem de FPS no headset.

**Museum LASSU > Refine Materials And Details** reaplica os acabamentos e preserva os materiais já texturizados ajustados no Inspector. As reconstruções da arquitetura também incluem o refinamento. Após mudar geometria ou iluminação, use **Bake Gallery Reflections**, recalcule a oclusão e salve a cena.

[Fontes CC0 dos materiais](Art/SurfaceTextures/SOURCES.md).

## Portões ondulatórios

As sete passagens têm paredes lisas, sem as ripas decorativas ou placas. Os anos são numerais sólidos de bronze escurecido, com 82 cm de altura e 4 cm de relevo, nos dois sentidos. As faces e laterais recebem iluminação e projetam sombra sobre a parede. **Museum LASSU > Refine Threshold Signage** reaplica esse tratamento; a reconstrução da arquitetura também o mantém.

As sete malhas dos anos são compartilhadas entre frente e verso e geradas no Editor, sem gerar geometria durante a visita. Os contornos DejaVu Sans e sua licença ficam em `Art/Typography`; `Tools/generate_threshold_glyphs.py`, na raiz do projeto, documenta sua geração offline.

Os arcos de passagem recebem portões de ripas articuladas que acompanham o contorno do vão. A abertura propaga uma onda horizontal entre as ripas, recolhendo as duas metades junto às laterais. A interação funciona nos dois sentidos do percurso: aproxime-se até 3,2 m, olhe para a passagem e pressione `E` quando aparecer **ABRIR PORTÃO**. Apenas o portão em foco responde; paredes e outros obstáculos bloqueiam a interação. Uma peça do acervo sob a mira tem prioridade sobre o portão, e o toque que devolve uma peça não aciona um portão próximo.

Espere a indicação **PASSAGEM LIVRE** para atravessar. Após a travessia e o afastamento de 5 m do plano do arco, o mecanismo espera 0,65 s e percorre a animação no sentido inverso. A abertura e o fechamento duram 2,8 s cada. Voltar à zona de movimento interrompe o fechamento e reabre o portão; afastar-se novamente permite que feche. `E` pede abertura, inclusive durante o fechamento, sem alternar manualmente para fechar. Com o cursor liberado ou o jogo pausado, a interação fica suspensa.

Cada portão tem 28 ripas rígidas de madeira com contorno elíptico. Primeiro o conjunto avança 0,78 m para o lado oposto ao visitante, liberando o contorno de pedra; depois as articulações alternadas propagam a dobra horizontal do centro às laterais. As ripas preservam tamanho e colisão durante todo o movimento. A passagem livre entre os conjuntos recolhidos tem aproximadamente 3,7 m. Os parâmetros de animação e afastamento ficam no Inspector de `KineticGate`.

`MuseumKineticGate` controla a animação, a passagem e o retorno automático. `MuseumGateInteractor`, no visitante, seleciona o portão e exibe a indicação contextual no Canvas do HUD. O gerador mantém esses componentes nas reconstruções da arquitetura.

**Museum LASSU > Apply Kinetic Gates** instala os portões nos sete arcos de passagem da cena aberta. **Validate Kinetic Gates** verifica a colisão fechada, três faixas de travessia abertas, interseções entre ripas e arquitetura em oito poses pelos dois lados, fechamento automático e reversões de segurança. O teste em Play Mode confirmou entrada `E` pelo Input System, prompt do HUD, travessia e fechamento após afastamento. A validação geral do percurso considera os portões abertos e restaura a pose inicial ao terminar.

## Estrutura

- `Data/MuseumTourCatalog.asset`: ordem das décadas, textos curatoriais, cores e exposições.
- `Prefabs/Architecture/`: hall e módulo reutilizável de sala.
- `Prefabs/Exhibits/`: pedestal, vitrine e painel de parede reutilizáveis.
- `Prefabs/Player/MuseumPlayer.prefab`: visitante em primeira pessoa, com `MuseumVisitor` e `MuseumVisitorHud`.
- `Materials/`: paleta compartilhada e materiais de destaque por década.
- `Scripts/Architecture/`: catálogo e metadados/volume de cada sala.
- `Scripts/Exhibits/`: componente configurável de ponto de exposição.
- `Scripts/Player/`: seleção e abertura dos portões pelo visitante.
- `Scripts/UI/`: HUD de sala e dica de controles, no Canvas. Pausa, inspeção e mira ficam em `MuseumVisitorHud`.
- `Scripts/Editor/`: gerador e validador do protótipo.
- `Art/GalleryGeometry.asset`: malhas persistentes das curvas, abóbadas, arcos e esculturas.

## Expandir o percurso

1. Selecione `MuseumTourCatalog.asset`.
2. Adicione, remova ou reorganize décadas e exposições no Inspector.
3. Use **Museum LASSU > Rebuild Prototype**.

O gerador recompõe a cena a partir do catálogo, reinstala as peças do acervo e coloca a cena do museu como primeira cena habilitada nas Build Settings.

## Peças do acervo

As peças integradas ficam em pedestais ou, quando não cabem neles, no piso. **Museu › Acervo › Instalar peças na galeria** instala todas a partir das tabelas de `MuseumCollectionPlacement`:

| Peça | Sala | Ponto |
|---|---|---|
| Telejogo | 1970 | `Exhibit_01`, à esquerda |
| Atari 2600 | 1970 | `Exhibit_04`, à direita |
| MicroFlex-4 | 1980 | `Exhibit_01`, à esquerda |
| Patinho Feio | 1970 | no piso, entre `Exhibit_01` e `Exhibit_03`, à esquerda |

Nesses pedestais, a âncora gira 180° para a frente da peça encarar o visitante, a placa de legenda desce para a face frontal e uma luz de exposição ilumina o conjunto. No Scene View, fora do Play Mode, o pedestal ainda mostra o placeholder: a peça é instanciada ao executar. O processador de build troca as peças pelas variantes `_WebGL` no build para navegador.

A peça de piso fica num grupo próprio dentro da sala (`PTF_Exposicao`, com a peça e a luz), no meio do caminho entre os dois expositores e com o mesmo giro das peças dos pedestais. Ao contrário delas, já está na cena e aparece fora do Play Mode. O ponto escolhido fica fora das três faixas de caminhada e dos acessos aos expositores que a validação percorre.

## Substituir um placeholder

Cada instância de `MuseumExhibitDisplay` contém:

- título;
- década;
- descrição;
- estilo de display;
- `Model Prefab`;
- `Model Anchor`.

Associe o prefab real ao campo `Model Prefab`. Ao executar, o modelo é instanciado no `Model Anchor` e o placeholder é ocultado. O modelo deve ser preparado com escala e origem adequadas ao anchor; a arquitetura da sala não precisa ser refeita.

## Decisões de performance

- malhas geradas no Editor e primitives estáticas, sem geração de geometria em runtime;
- materiais compartilhados e instancing habilitado;
- oito luzes locais sem sombras e uma luz direcional com sombras suaves;
- sem pós-processamento pesado, partículas ou reflexos em tempo real;
- sinalização 3D com shader URP leve e teste de profundidade;
- colliders apenas nas superfícies físicas; linhas de orientação, luzes e esculturas suspensas são visuais.

## Oclusão entre salas e modelos do acervo

A câmera do visitante usa o **Occlusion Culling nativo do Unity**, com dados calculados no Editor e salvos em `Assets/Scenes/Museu/OcclusionCullingData.asset`. A arquitetura sólida atua como oclusora. Cada portão tem 14 volumes `OcclusionPortal` imóveis que acompanham o contorno do arco; são 98 portais nos sete portões e oito volumes de câmera cobrindo o hall e as salas.

Ao pedir a abertura com `E`, os portais liberam imediatamente a visibilidade para que o interior apareça durante a animação. Permanecem abertos durante todo o movimento e voltam a ocluir somente quando o portão termina de fechar. Desativar o mecanismo também libera seus portais. As ripas móveis participam como objetos que podem ser ocultados, sem serem gravadas como paredes estáticas no bake.

O sistema descarta da renderização objetos totalmente encobertos. As aberturas reais acima e ao lado das divisórias continuam permitindo as vistas correspondentes; uma sala posterior ainda visível por uma abertura pode continuar sendo desenhada. A oclusão preserva essa visibilidade e não desativa salas, colliders ou o HUD. [Funcionamento do Occlusion Culling](https://docs.unity3d.com/6000.0/Documentation/Manual/OcclusionCulling.html) e [portais nativos](https://docs.unity3d.com/6000.0/Documentation/Manual/class-OcclusionPortal.html).

Baseline de verificação da cena atual: da posição inicial, a câmera submete 1.058 renderers sem oclusão e 324 com todos os portões fechados, sem desenhar nenhuma peça do acervo. Na medição original do espaço, antes de instalar as peças, o primeiro portão aberto levava a 405 renderers, e o portal de 1970 passava de 640 para 73 e de 581 para 79, nos dois sentidos; nessas comparações, a captura com e sem oclusão foi pixel a pixel idêntica. Esses números medem submissões de renderers no protótipo atual e devem mudar com o acervo; não equivalem diretamente a FPS.

Para recalcular após alterar a arquitetura, regenerar os portões ou reorganizar objetos estáticos:

1. Saia do Play Mode e abra `Museu.unity`.
2. Use **Museum LASSU > Configure Room Occlusion** para atualizar flags, portais e volumes. As reconstruções da arquitetura e dos portões também refazem essa configuração.
3. Abra **Window > Rendering > Occlusion Culling**, aba **Bake**, e execute **Bake**. A configuração inicial usa `Smallest Occluder = 1.5`, `Smallest Hole = 0.10` e `Backface Threshold = 100`.
4. Aguarde o término, salve a cena e verifique a aba **Visualization** com a câmera do visitante, incluindo portões fechados, em movimento e abertos. O menu de configuração não executa o bake automaticamente.

Modelos associados a `MuseumExhibitDisplay` recebem `Renderer.allowOcclusionWhenDynamic = true` ao serem instanciados ou substituídos. Isso inclui filhos inativos e renderers desabilitados, preservando seus estados. Os prefabs originais permanecem intactos. Um carregador que adicione outros renderers depois da instanciação pode chamar `RefreshModelOcclusion()` no display; não há varredura da hierarquia a cada frame. Trocar somente o modelo dinâmico do acervo não exige um novo bake da arquitetura. Mantenha os limites de cada renderer próximos à peça: uma malha única abrangendo várias salas reduz a capacidade de ocultá-la.

Essa preparação reduz trabalho de desenho dos modelos encobertos, inclusive de futuras peças com muitas faces. Os modelos continuam carregados em memória; scripts, animações e física continuam executando normalmente. O streaming de assets e os níveis de detalhe são otimizações separadas. O ganho de tempo por frame depende do acervo e do dispositivo e deve ser medido no Profiler, sem estimativa fixa de FPS.

## Validar

Use **Museum LASSU > Validate Prototype**. Além da ordem das salas e das referências dos modelos, o validador verifica a cápsula real do visitante em três faixas do caminho sinuoso e nos acessos frontais dos expositores. As amostras têm espaçamento máximo de 0,5 m, com teste contínuo de colisão entre elas e verificação de apoio do piso.

A revisão arquitetônica foi verificada em Play Mode com `CharacterController.Move`: percurso completo sem desvio, apoio no piso e atualização do HUD pelas sete décadas. O desempenho em headset VR ainda requer medição no dispositivo de destino.
