# Testing

Run `dotnet restore --locked-mode`, `dotnet build --no-restore`, and `dotnet test --no-restore` from the repository root. Unit tests use xUnit; integration tests use WebApplicationFactory with explicit test-server identity configuration. No production credentials are needed.

Coverage of requested security scenarios:

| Scenario | Evidence |
|---|---|
| Own-account order | ScopeTests.OwnOrderAllowedAndMinimized; API own-order test |
| Cross-account order | ScopeTests.UnauthorizedAndMissingAreIdentical; safe HTTP 404 |
| All-customer prompt injection | OrchestrationTests.PromptInjectionCannotReadAllCustomers |
| Forged customerId | Body rejected; tool extra scope rejected; chat text cannot authorize |
| SQL-like input | Tool validation rejects; SQL scaffold proves values are separate parameters |
| Unknown order | Orchestration stops before fabricated final response |
| Repository failure | Safe dependency category and no second model call |
| Provider failure | Safe service category and provider-body redaction |
| Unknown tool | Dispatcher rejection |
| Malformed arguments | JSON/type/extra/duplicate validation rejection |
| Retrieved document injection | Scripted model attempts forbidden order after hostile retrieval and is denied |
| Prompt/config disclosure | Ungrounded disclosure text discarded; no credentials included in provider JSON |

Ten functional examples run through deterministic MockAiClient and the actual scoped dispatcher. Further tests cover location roles, document account ACLs, bounded history, conversation ownership/scope changes, cancellation, iteration limits, Responses transport envelopes and SQL parameter patterns. Integration tests cover authentication, input validation, conversation continuation, rate limiting, health and OpenAPI.

Optional browser test: start `bash scripts/run-demo.sh`. With Python Playwright installed and Chromium available, start `chromium --headless --no-sandbox --disable-dev-shm-usage --user-data-dir=/tmp/bolt-chromium --remote-debugging-port=9223 about:blank` in an isolated development environment; run `python3 tests/browser/smoke.py`. It checks keyboard send, answers, continued conversation, citations, safe error state, reset and text-only rendering. The Chromium flag is for this isolated cloud browser test, not a production browser recommendation.

Live OpenAI, real SQL Server, real BOLT authentication and real Sprint RAG are not exercised. Mock/fake injection tests establish code-enforced scope, not a universal guarantee about live model prose. Required production evaluations include factual grounding, prompt disclosure resistance, consent/data handling, real JWT claim mapping, stored procedure contracts, document ACLs and revocation.

## Executed verification — 2026-10-07

The reusable cloud setup script completed locked restore, build (zero warnings/errors), 41 passing unit tests and 13 passing API integration tests; zero failed or skipped tests. The final binary was restarted and the optional browser smoke script passed against it. Live local requests verified health and identical error objects for unknown/inaccessible orders. Runtime logs showed matching correlation IDs across chat, AI, tools, repository and RAG events. No live OpenAI or SQL connections were attempted.

These results apply to this prototype and current environment, not a production deployment or restoration into a new cloud task.
