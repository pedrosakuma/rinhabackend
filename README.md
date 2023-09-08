# RinhaBackend

## Resumo

Dotnet 8 preview com AOT, com leituras in memory e sincronismo entre nós com GRPC.
Para busca com filtro, foi utilizado uma lib implementando o algoritmo Trie.
Sincronismo com o banco de dados utilizando tabela temporária para realizar bulkcopy via binário seguido de select into com truncate.

[Resultado Gatling](https://htmlpreview.github.io/?https://github.com/pedrosakuma/rinhabackend/blob/master/docs/rinhabackendsimulation/index.html)