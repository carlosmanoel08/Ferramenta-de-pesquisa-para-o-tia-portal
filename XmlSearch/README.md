# Pesquisa XML TIA

Aplicativo local para pesquisar exportações XML do TIA Portal por projeto, CPU e tipo de elemento.

## Pré-requisitos para desenvolvimento

- Windows.
- SDK .NET 8 (a máquina atual possui apenas o runtime .NET 3.1, portanto precisa do SDK para compilar).

## Executar em desenvolvimento

```powershell
dotnet restore .\XmlSearch.sln
dotnet run --project .\src\XmlSearch.App\XmlSearch.App.csproj
```

A aplicação procura `refs` no diretório atual e nos diretórios-pai. Também é possível informar outro caminho no campo **Pasta fonte**. Clique em **Reindexar** antes da primeira pesquisa.

## Teste estrutural do parser

```powershell
dotnet run --project .\tests\XmlSearch.Indexer.Tests\XmlSearch.Indexer.Tests.csproj
```

O teste cria uma exportação Siemens mínima temporária e valida bloco, tag, variável, membro DB aninhado e network.

## Publicação sem instalação

```powershell
dotnet publish .\src\XmlSearch.App\XmlSearch.App.csproj -c Release -r win-x64 --self-contained true -o .\publish
```

Distribua o conteúdo de `publish`. O índice é criado no perfil do usuário em `%LOCALAPPDATA%\XmlSearch`, nunca dentro de `refs`.
