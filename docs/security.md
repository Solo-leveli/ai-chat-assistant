# Security and trust boundaries

Threats include cross-account access, forged identity, prompt/document injection, arbitrary SQL, conversation theft, tool loops, secret disclosure, resource exhaustion and unsafe browser rendering.

Identity and account claims come only from validated authentication or an explicitly enabled Development-only server identity. Chat JSON cannot carry trusted account scope. Every registered tool validates exact arguments, obtains scope internally, and filters repositories before returning data. Missing and inaccessible records are indistinguishable. Location access additionally requires InventoryLocation role.

User messages, model calls, retrieved documents and external responses are untrusted. Prompts guide behavior but do not authorize. SQL executes only configured stored procedures with typed parameters; procedure contracts must enforce account scope too. Phase 1 has no write tools. Credentials remain server-side secure configuration. No request/response bodies or secret values are logged.

Audit stores correlation ID, user ID, tool, resource type, decision, outcome, duration and category. Development console audit is not a durable compliance sink. Conversation storage is bounded and expiring; production needs shared durable storage and approved retention. API rate limits are per authenticated user with concurrency and timeout bounds. Deployment needs TLS, trusted issuer configuration, shared rate limiting and a private health route.

Prompt injection cannot broaden repository scope. No system guarantees arbitrary LLM text is secret-free: the model is never given credentials, connection strings or private deployment configuration. Operational answers after tool failures are blocked rather than sent for model guessing. Output is displayed as text, never HTML. Real model behavior and approved document ingestion need further evaluation before production.

The current host rejects all non-Development startup to prevent fictional records being served as real business data. JWT is an adapter for sanctioned development integration, not proof of BOLT production authentication readiness.
