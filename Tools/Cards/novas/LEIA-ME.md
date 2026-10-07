# Cartas novas (Fase 10, D-088)

Cada grupo de artista escreve dois arquivos nesta pasta:

- `px_<grupo>.py`: a versão em "pixel" (só serve de mapa de ocupação das estrelas e da lista de cartas). Define `il_<id>(t)` e acrescenta as linhas das suas cartas a `CARDS`.
- `hd_<grupo>.py`: a versão em alta resolução. Define `il_<id>(t)` com as ferramentas do `card_hd.py`.

Os dois são executados dentro de `card_pixel.py` / `card_hd.py` (mesmo espaço de nomes), então não se importa nada de lá.
Gerar: `python Tools/Cards/card_hd.py <id> [<id> ...]` (saída em `Assets/_Game/Art/Cards/`).
