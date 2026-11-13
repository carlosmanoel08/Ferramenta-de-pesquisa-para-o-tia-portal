# Requisitos — Pesquisa XML TIA

## Decisões registradas

- A ferramenta será um aplicativo desktop Windows em C#/.NET 8 com WPF, publicado como pasta autocontida; não dependerá de instalação de Python, banco de dados ou servidor.
- A fonte inicial é a pasta `refs`, mantida somente para leitura. O caminho poderá ser substituído na interface sem editar XMLs.
- A base SQLite local ficará em `%LOCALAPPDATA%\XmlSearch\search.db`. A indexação acontece por ação explícita no botão **Reindexar**; arquivos inalterados são reaproveitados e registros de XML removido são excluídos do índice.
- A busca é parcial (`contém`) e não diferencia maiúsculas de minúsculas, cobrindo nome, tipo, comentário e texto/código extraído.
- Os filtros independentes são Projeto, CPU e Tipo. Vários valores do mesmo filtro são aceitos; filtros diferentes são combinados.
- Tipos: Bloco (objetos `SW.Blocks.*`), Tag (definições PLC), Variável (seções Input, Output, InOut, Temp, Constant e Return) e Membro DB (membros recursivos de blocos DB).
- O resultado informa projeto, CPU, caminho relativo, seção/tipo e network. Network só é mostrada quando existir explicitamente no XML; para itens declarativos é “Não aplicável”.
- XML inválido, inacessível ou com erro de leitura não interrompe a indexação; o primeiro aviso identificável é exibido ao fim da operação.

## Limites da primeira versão

- Não há monitoramento contínuo da pasta de origem.
- A tabela limita uma consulta a 1.000 resultados para preservar responsividade.
- O visualizador abre o XML no aplicativo padrão do Windows; não edita arquivos nem mostra o XML inteiro dentro da ferramenta.
