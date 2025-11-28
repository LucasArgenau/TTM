TTM – Sistema de Gerenciamento de Torneios de Tênis de Mesa
🏓 Visão Geral

O TTM é um sistema web desenvolvido em C# (.NET MVC) com Entity Framework Core e banco de dados SQL Server, projetado para gerenciar torneios de tênis de mesa.
O sistema permite criar torneios, importar jogadores via CSV, gerar confrontos automáticos por grupos, registrar resultados, visualizar rankings e perfis, além de exportar relatórios.

Este projeto foi desenvolvido para fins acadêmicos e demonstra boas práticas de backend, arquitetura MVC, lógica de negócios e persistência de dados.

🚀 Tecnologias Utilizadas

C#
ASP.NET Core MVC
Entity Framework Core (Code First + Fluent API)
SQL Server (LocalDB, Express ou instância remota)
LINQ / Repositórios / Services
CSV Import (personalizado)

📌 Funcionalidades Principais
Admin

Criar e gerenciar torneios
Importar jogadores via arquivo CSV
Gerar confrontos automaticamente
Cadastrar resultados de cada jogo
Exportar relatórios em CSV
Visualizar ranking e desempenho dos jogadores

Jogador (Player)

Criado automaticamente na importação
Possui perfil com informações e histórico
Senha gerada via hash

🗂️ Estrutura das Entidades
User
Id
Username
PasswordHash
Role (Admin/Player)
CreatedAt
Player

Id
FullName
Nickname
Rating
Email
CreatedAt

Tournament
Id
Name
Description
StartDate
EndDate
CreatedBy

Game
Id
TournamentId
PlayerAId
PlayerBId
ScoreA
ScoreB
PlayedAt
ResultStatus (Scheduled / Finished)

Result / Ranking
WinnerId
Sets / Pontuação
Atualização do rating (opcional)
Obs: Nomens e campos podem variar conforme sua implementação, mas representam a estrutura lógica do projeto.

📥 Importação de Jogadores (CSV)

Formato recomendado do arquivo:

Username,FullName,Nickname,Rating,Email
jdoe,John Doe,JD,1200,jdoe@example.com
maria,María Silva,Mari,1350,maria@example.com


Regras:

Se o Username já existir → atualiza
Se não existir → cria Player + User
Hash de senha gerado automaticamente
Sistema retorna um relatório com erros, novas inserções e atualizações

🧠 Geração Automática de Confrontos

O sistema cria confrontos automaticamente seguindo:

Lista todos os jogadores do torneio

Distribui em grupos (balanceado por rating, se aplicável)

Gera partidas no formato round-robin (todos jogam entre si)

Salva cada confronto na tabela Game

Exibe os jogos para registro futuro dos resultados

🏆 Ranking

Após finalizar jogos, o ranking é calculado com base em:
Número de vitórias
Sets vencidos
Critério de desempate (definido no código)
O ranking é exibido no painel do torneio.

🗃️ Banco de Dados (SQL Server)

O projeto utiliza SQL Server via Entity Framework Core.
Exemplo de connection string:
"ConnectionStrings": {
  "DefaultConnection": "Server=localhost\\SQLEXPRESS;Database=TTMDb;Trusted_Connection=True;MultipleActiveResultSets=true"
}

⚙️ Como Rodar o Projeto
1. Clone o repositório
git clone https://github.com/LucasArgenau/TTM.git
cd TTM

2. Configure o banco no appsettings.json

Exemplo com SQL Express:

"ConnectionStrings": {
  "DefaultConnection": "Server=localhost\\SQLEXPRESS;Database=TTMDb;Trusted_Connection=True;"
}

3. Execute as migrations

Instale o EF CLI (se necessário):

dotnet tool install --global dotnet-ef


Crie o banco:

dotnet ef database update

4. Execute o projeto
dotnet run


Acesse no navegador:
👉 https://localhost:5001 ou http://localhost:5000

🔐 Usuário Admin (Seed)

O sistema pode gerar automaticamente um admin inicial (exemplo):

Username: admin
Password: admin123 (hash internamente)
Role: Admin

(Ajustar conforme sua implementação.)

🧪 Testes

Sugestões implementadas/possíveis:
Testes unitários para:
Importação CSV
Matchmaking (geração de confrontos)
Ranking
Testes de integração usando banco InMemory

📈 Possíveis Melhorias Futuras

Módulo de fase eliminatória (mata-mata)
Implementar Identity completo
Dashboard com gráficos
API + frontend em React
Exportação em PDF/Excel
Estatísticas avançadas dos jogadores

👨‍💻 Autor

Lucas Argenau
LinkedIn: https://www.linkedin.com/in/lucas-ribeiro-0697a5233/
GitHub: https://github.com/LucasArgenau

📄 Licença

MIT — pode ser usada livremente para fins acadêmicos e profissionais.
