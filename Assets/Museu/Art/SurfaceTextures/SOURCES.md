# Texturas dos acabamentos

Fontes públicas do Poly Haven, obtidas em 5 de setembro de 2026. Os assets são disponibilizados sob [CC0](https://polyhaven.com/license).

| Família | Fonte | Uso |
| --- | --- | --- |
| Pedra | [Marble 01](https://polyhaven.com/a/marble_01) | Piso claro e faixa de pedra verde; juntas e veios na própria textura |
| Reboco | [Painted Plaster Wall](https://polyhaven.com/a/painted_plaster_wall) | Paredes, abóbadas, pórticos, bases e vasos, com tonalização por material |
| Madeira | [Rosewood Veneer1](https://polyhaven.com/a/rosewood_veneer1) | Ripas dos portões e bancos; nomes `Gate_Walnut*` mantidos por compatibilidade |

Arquivos de origem selecionados pela API pública `https://api.polyhaven.com/files/{id}` em resolução 1K: `Diffuse` em JPG, `nor_gl` em PNG e `Rough` em JPG. Albedos e normais são os arquivos originais. As máscaras de 512 px foram empacotadas no Editor a partir de Rough: **R = 0, G = 1, B = 0, A = 1 − roughness**, com amostragem bilinear em dados lineares. O shader URP/Lit lê a rugosidade invertida no alpha de `_MetallicGlossMap`; o multiplicador `_Smoothness` calibra o acabamento.

Importação: albedo em sRGB; normais como Normal Map/OpenGL, sem inverter o verde; máscaras lineares. Pedra a 1024 px, demais mapas a 512 px, mipmaps, filtragem trilinear, anisotropia 4 e leitura por CPU desabilitada. Android usa ASTC 6×6. Os três conjuntos são compartilhados por todos os materiais; não há mapas exclusivos por sala.

`MuseumSurfaceRefinement` prepara a importação e os materiais no Editor. As malhas derivadas recebem UVs em metros e tangentes; não há projeção triplanar nem geração de texturas em runtime. Os cubemaps em `../Reflections/` são capturas da própria cena, de 128 px por face.
