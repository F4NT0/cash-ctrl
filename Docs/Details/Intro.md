# Abertura do software cash-ctrl

## Como iniciar o software

Cash-Ctrl pode ser aberto de 3 formas:

### 1. com menu principal

- Se escrever no terminal `cash-ctrl` ele vai abrir o menu inicial com um ascii text com a fonte ANSI Shadow escrito em maiusculo `CASH-CTRL`.
- Abaixo do titulo centralizado haverá duas opções para escolher: 
    1. `Open controls...` : mostra um panel com opções de controles já existentes para selecionar e abrir.
    2. `Create new control`: cria um arquivo do zero, passando o nome do expense, ele vai salvar nos favoritos para poder abrir pelo open expense na próxima vez.

### 2. diretamente

- Se você estiver em um diretório onde tem um arquivo cash.
- Use o comando `cash-ctrl control-name` para abrir o control especificado diretamente no software.
- caso ele não exista, vai criar um novo controle no diretório atual com o nome passado.

### 3. buscando localmente

- Se você estiver em um diretório com vários controls.
- Use o comando `cash-ctrl .` para abrir um panel com seletor onde você pode escolher o control para abrir.
- Se não tiver nenhum nesse diretório, vai avisar que não tem nenhum criado e vai me dar a opção de criar um novo control.

## Criando expense

- Um control é um arquivo JSON com os dados de entrada de valores e saida de valores.
- Eles seguem uma estrutura específica que é lido e vai para o panel principal.
- Um control tem os seguintes dados de exemplo:

```JSON
{
 "Fevereiro 2026": {
    "total-value": 25000.00,    
    "Fruteirão": { 
        "date": "21/02/2026",
        "total": 25.00,
        "type": "fruteira",
        "origin": "expense",
        "details": [
           "banana": {
             "amount": 10.00,
             "item-price": 5.00,       
             "quantity": 2,
             "size": "Kg"       
           },
           "morango": {
             "amount": 5.00,
             "item-price": 5.00,
             "quantity": 1,
             "size": "Un"        
           }     
        ]    
    },
    "Mercado": {
        "date": "22/02/2026",
        "total": 150.00,
        "type": "mercado",
        "origin": "expense",
        "details": [
          "sal": {
            "amount": 50.00,
            "item-price": 50.00,
            "quantity": 1,
            "size": "Un"        
          },
          ...
        ]    
    },
    "Uber": {
      "date": "22/02/2026",
      "total": 50.00,
      "type": "Uber",
      "origin": "expense",
      "details": []      
    },
    "venda lixo eletronico": {
      "date": "23/02/2026",
      "total": 1200.00,
      "origin": "income"
    }    
 }
}
```
- No exemplo de control temos 3 tipos de gastos, 1 ida a fruteira, 1 ida ao mercado e 1 gasto em uber.
- O valor do total de um expense pode ser dado (como no caso do uber) ou ele é o resultado do cálculo de todos os `amounts` do `details` 
- No caso de um income, ele vai adicionar o valor do total no `total-value` que é o total de dinheiro que tenho na visualização.
- No caso de expense, ele vai somar todos os expenses e mostrar o total de todos os `total` na visualização.
- `total-value` é um valor que eu entrego para o sistema quando crio um arquivo de control, é como o saldo total de dinheiro que tenho para gastar no mês.

## Visualização 

- Quando for criar um novo control, deve perguntar o nome que vai ser dado.
- Quando for criar um novo control, deve mostrar onde vai ser salvo (deve mostrar o diretório atual com o caminho até o arquivo, quando eu escrevo o nome do arquivo ele deve mostrar a localização abaixo com o nome do arquivo.
- Quando for criar um novo control, deve perguntar o total de dinheiro que tem para gastar.
- Se já existir um control que deseja abrir já deve ir para a tela principal (será explicada mais a frente).
- Os expenses e incomes serão adicionados pela tela principal, nesse momento é para criar a tela incial e as formas de inicialização.
- caso eu for abrir nesse momento um control existente, deve mostrar um panel mostrando o nome do control, o valor total para gastar em verde e as opções de confirmar (Enter) ou sair (Esc).
- confirmando que desejo entrar no control, nesse momento somente mostra uma mensagem na tela escrito TBD centralizado em um panel na tela.
- caso não exista e estiver criando, após colocar os dados e confirmar ele vai para essa tela escrito TBD também.
