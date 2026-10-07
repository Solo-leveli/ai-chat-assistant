# AI Chat Assistant — fictional-data prototype

A read-only enterprise chatbot foundation for future BOLT integration. This standalone AI Chat Assistant currently includes the logistics demo built from the original specification. Internal .NET project names still use BoltAI; real integrations remain placeholders.

## Architecture and components

One ASP.NET Core .NET 10 API, with Domain, Application and Infrastructure libraries. Authenticated request → scoped conversation → AI orchestration → approved tools → server authorization → account-filtered repository. The Responses API adapter receives only questions, bounded history, tool schemas and minimized results. It has no database access or credentials.

Implemented: eight read-only business tools; strict typed arguments; fixed server-side development identity; JWT adapter awaiting confirmed BOLT issuer/claims; fictional repositories and account-scoped document search; mock AI and OpenAI Responses adapter; bounded tool loop; safe failures; rate limiting and concurrency limits; expiring bounded conversations; structured audit and metrics; disabled SQL stored-procedure scaffold; accessible plain JavaScript demo.

```text
src/BoltAI.Api/             HTTP, authentication, rate limiting, demo
src/BoltAI.Application/     contracts, authorization, tools, orchestration
src/BoltAI.Domain/          records, minimized results, safe failures
src/BoltAI.Infrastructure/  mock/SQL repositories, AI adapters, audit, history
 tests/BoltAI.UnitTests/    scope, failures, orchestration, transport, SQL patterns
 tests/BoltAI.IntegrationTests/ authenticated HTTP tests
 tests/browser/            optional browser smoke check
 docs/                     design, API, security, integration questions
```

## Prerequisites and local run

Install .NET SDK **10.0.401** (or a later 10.0.4xx patch permitted by `global.json`). No database, OpenAI key or Node install is needed for the mock demo.

From the repository root:

```bash
# Open this folder in your terminal
dotnet restore --locked-mode
dotnet build --no-restore
dotnet test --no-restore
bash scripts/run-demo.sh
```

The script binds to your own machine's loopback port 5080 and explicitly enables Development authentication and Mock AI. Open `http://127.0.0.1:5080` **on the same machine** where you run the script. A cloud environment's localhost is not your browser's computer; cloud onboarding does not provide a browser preview.

The demo identity is `DEMO-U100`, account `C100`, roles `BoltReader` and `InventoryLocation`. Request headers cannot change it. Development authentication is opt-in, is prohibited outside Development and must not be exposed as a public service.

```bash
curl -s http://127.0.0.1:5080/internal/health
curl -s http://127.0.0.1:5080/api/chat/message \
  -H 'Content-Type: application/json' \
  -d '{"message":"Where is order 45821?"}'
```

Mock AI returns labeled structured fictional data rather than claiming a live model generated the answer. Try stock ABC123, shipment SH12345, recent orders and damaged goods. Order 99999 (C999) and missing order 00000 yield the same safe unavailable response. C200 order 60001 illustrates a second account; it is not authorized for the default C100 demo identity. Configure development account arrays server-side if needed.

## OpenAI configuration

The mock demo never calls OpenAI. To evaluate the real adapter, use a separate local Development process with your **non-production test credentials**, supplied through a secure environment/configuration provider. Do not commit values or put keys in browser code. Set `AI__Provider=OpenAI`, `AI__Model` to your approved model, and `AI__ApiKey` through a secret manager/environment. Retain `ASPNETCORE_ENVIRONMENT=Development` and explicitly enable `DevAuth__Enabled=true` only on loopback. Start with `dotnet run --project src/BoltAI.Api --no-launch-profile --urls http://127.0.0.1:5080`; the demo script deliberately resets the provider to Mock.

`AI:TimeoutSeconds` defaults to 30 (1–120), `MaxToolIterations` to 4 (1–8), and `MaxToolCalls` to 12 (1–24). Requests use Responses API with strict tool schemas, `store=false`, output token bounds, no automatic retries and cancellation. Network policy must permit `api.openai.com` for live evaluation. This has **not** been live-tested; automated transport tests use a fake HTTP handler. Third-party data disclosure, retention and approved model selection require Sprint review.

`appsettings.Example.json` contains non-secret placeholders only and is not loaded automatically. No key or connection string is stored in tracked configuration. Default startup requires either explicit development identity or confirmed HTTPS authority/audience configuration. JWT integrations must confirm claim semantics; a body `customerId` is rejected. The entire fictional-data prototype refuses non-Development startup until real integrations are approved.

## SQL and documents

`SqlBusinessRepository` is an **unregistered scaffold** using Microsoft.Data.SqlClient. Deployment must supply approved procedure names, parameter names, account TVP type/column and strongly typed result mappers through `SqlRepositoryContracts`. Those contracts are not known. Command type is StoredProcedure; scope and identifiers are typed parameters. TLS certificate verification is enforced. Account filtering is also applied after mapping. The API explicitly rejects `Sql:Enabled=true`; no production SQL connection has been attempted. Separate mappings for specialized status/availability/location procedures remain an integration decision; the scaffold currently uses four aggregate repository operations. Database identity must be read-only with EXECUTE only on approved procedures, whose implementations must enforce scope before returning rows.

Mock damaged-goods content is fictional and visibly marked as **not Sprint policy**. Real search/index storage and document ACLs remain unknown. Retrieved content is untrusted data and cannot broaden account scope. Source metadata is included in chat responses.

## Security, errors and observability

Unknown tools and malformed/extraneous/duplicate arguments are rejected. Authorization is implemented in code; prompt instructions do not authorize. Missing and unauthorized resources are indistinguishable. A failed tool aborts orchestration before a model can invent a fallback. Ungrounded text without any verified lookup is replaced with a fixed capabilities response. This does not prove arbitrary live LLM answers are correct: further grounding evaluations and output controls are required before production.

Conversations are process-local, owner-and-scope bound, expire after 30 minutes, retain 12 messages and cap at 1,000 entries. Rate limiting is 20 chat requests/user/minute, with 16 global concurrent HTTP requests and no queue. Conversation ownership, roles and accounts are rechecked each request. Real BOLT account revocation may require updated claims/introspection, to be designed with the team.

Audit logs include correlation/user IDs, fixed operation name, resource type, outcome, duration and error category. Prompt/response bodies, identifiers, keys, tokens and SQL details are not logged. Metrics use the `BoltAI` meter with `bolt.operations` and `bolt.operation.duration`, tagged by operation, success and category for chat, AI, tools, repositories and RAG. Attach the approved exporter/collector later; logs are not a durable compliance store.

API error bodies contain safe code/message plus request ID; see [API contract](docs/api.md). Health is authenticated and reports process readiness only, not upstream readiness. OpenAPI is at `/openapi/v1.json` in Development; no Swagger UI dependency is added.

## Validation and remaining work

See [testing](docs/testing.md), [architecture](docs/architecture.md), [security](docs/security.md) and [integration questions](docs/integration-questions.md). The suite covers all twelve requested security scenarios and ten functional examples using mocks/fakes. SQL command tests create commands without connecting to a database. Real BOLT authentication, SQL contracts, approved documents, live model quality, hosting/TLS, distributed rate limits, durable audit/conversation storage and retention remain unresolved. No production credentials were requested or systems contacted.

Recommended next step: confirm BOLT identity/account claims and approved read-only database/document contracts, then add contract tests against sanctioned non-production services before enabling those integrations.

## Visual Studio on Windows

Install Visual Studio 2026 with the ASP.NET and web development workload and .NET 10 SDK 10.0.401 (or a later 10.0.4xx patch). Open BoltAI.sln, set BoltAI.Api as the startup project and select the AI Chat Assistant Demo launch profile. Press F5. The profile runs on loopback with fixed fictional development identity and Mock AI; no credentials are needed. Keep Visual Studio debugging active while opening the site.
