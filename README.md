# RinhaBackend

## Resumo

Dotnet 8 preview com AOT, com leituras in memory e sincronismo entre n�s com GRPC.
Para busca com filtro, foi utilizado uma lib implementando o algoritmo Trie.
Sincronismo com o banco de dados utilizando tabela tempor�ria para realizar bulkcopy via bin�rio seguido de select into com truncate.

[Resultado Gatling](https://htmlpreview.github.io/?https://github.com/pedrosakuma/rinhabackend/blob/master/docs/rinhabackendsimulation-20230905201847355/index.html)