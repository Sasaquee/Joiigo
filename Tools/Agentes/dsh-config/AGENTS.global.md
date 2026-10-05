# Regras globais (valem em qualquer pasta)

## API gratuita da NVIDIA

A conta gratuita da NVIDIA tem cerca de 40 requisições por minuto **por chave**, compartilhadas entre todos os modelos, agentes e sessões. O limite varia por modelo e pelo tráfego: num modelo lotado, a requisição pode ficar parada sem responder.

1. No máximo **3 agentes ao mesmo tempo**: o principal + 2 subagentes. No `workflow`, dispare `agent()` em lotes de até 2 e espere o lote terminar.
2. Uma sessão do harness por vez trabalhando com a chave.
3. Mire em no máximo 30 requisições por minuto: prefira passos que façam várias coisas (ler vários arquivos de uma vez, comandos agrupados).
4. Nunca testar todos os modelos em paralelo. Os IDs do catálogo já foram conferidos; se precisar testar, um por vez.
5. Modelo sem resposta em 2 minutos ou com 2 erros seguidos: cancele, espere 60 s e use o reserva do papel.
6. Erro 429: pare tudo por 60 s antes de chamar de novo.
7. Ao esperar um job em segundo plano, consulte com espera de até 2 minutos; se não andou entre duas consultas, aplique a regra 5.

## Segurança

- Nunca leia, extraia nem exiba o conteúdo de `~/.dsh/.credentials.yaml` ou chaves de API. O harness injeta as chaves sozinho.
- Não edite a configuração do harness (`~/.dsh/profiles/**`) a menos que o usuário peça isso explicitamente.
